using QProtocol.DataStreaming.DataPackets;
using QProtocol.DataStreaming.Headers;
using System.Diagnostics;
using System.Net.Sockets;

namespace TimeSyncSystems;

public class DataStreamer
{
    private readonly string ipAddress = string.Empty;
    private readonly int port = 0;
    private readonly int sampleRate = 0;

    private TcpClient tcpClient;
    private NetworkStream networkStreamer;
    private byte[] buffer = new byte[1024];

    private Task streamingThread;
    private readonly CancellationTokenSource tokenSource = new();
    private readonly CancellationToken token;
    private Stopwatch firstPacketStartTime; 

    public List<AnalogDataPacket> AnalogDataPackets { get; } = new();

    public DataStreamer(string ipAddress, int port, int sampleRate)
    {
        this.ipAddress = ipAddress;
        this.port = port;
        this.sampleRate = sampleRate;
        token = tokenSource.Token;
    }

    public void StartStreaming()
    {
        // This will initiate the data stream. After this we need to gather data fast.
        streamingThread = Task.Factory.StartNew(StreamData, token);
    }

    public void StopStreaming() 
    {
        if (AnalogDataPackets.Count == 0)
        {
            networkStreamer.Close();
            tcpClient.Close();
            return;
        }

        while (firstPacketStartTime.ElapsedMilliseconds < 2000)
        {
            Thread.Sleep(25);
        }

        tokenSource.Cancel();
        while (!streamingThread.IsCanceled && !streamingThread.IsCompleted) 
        {
            Thread.Sleep(1);
        }

        networkStreamer.Close();
        tcpClient.Close();
    }

    private void StreamData()
    {
        tcpClient = new TcpClient(ipAddress, port);
        networkStreamer = tcpClient.GetStream();

        while (token.IsCancellationRequested == false)
        {
            if (networkStreamer.DataAvailable == false)
            {
                Thread.Sleep(1);
                continue;
            }

            networkStreamer.ReadExactly(buffer, 0, (int)PacketHeader.BinarySize);

            PacketHeader packetHeader;
            using (var memoryStream = new MemoryStream(buffer))
            using (var packetHeaderReader = new BinaryReader(memoryStream))
            {
                packetHeader = new PacketHeader(packetHeaderReader);
                if (buffer.Length < packetHeader.PayloadSize)
                {
                    buffer = new byte[packetHeader.PayloadSize];
                }

                if (packetHeader.PayloadType != 0)
                {
                    networkStreamer.ReadExactly(buffer, 0, (int)packetHeader.PayloadSize);
                    continue;
                }
            }

            var bytesLeft = packetHeader.PayloadSize;
            if (bytesLeft == 0)
            {
                continue;
            }

            if (firstPacketStartTime == null)
            {
                firstPacketStartTime = Stopwatch.StartNew();
            }

            networkStreamer.ReadExactly(buffer, 0, (int)bytesLeft);
            using (var memoryStream = new MemoryStream(buffer))
            using (var payloadReader = new BinaryReader(memoryStream))
            {
                while (bytesLeft > 0)
                {
                    if (bytesLeft < GenericChannelHeader.BinarySize)
                    {
                        throw new InvalidOperationException($"Invalid payload size. This will only happen when an invalid amount of data was copied from the streamer.");
                    }

                    var genericChannelHeader = new GenericChannelHeader(payloadReader);
                    bytesLeft -= genericChannelHeader.GetBinarySize();

                    switch (genericChannelHeader.ChannelType)
                    {
                        case ChannelTypes.Analog:
                            var analogChannelHeader = new AnalogChannelHeader(genericChannelHeader, payloadReader);
                            bytesLeft -= analogChannelHeader.GetBinarySize();

                            var analogDataPacket = new AnalogDataPacket(genericChannelHeader, analogChannelHeader, payloadReader);
                            bytesLeft -= analogDataPacket.GetBinarySize();

                            AnalogDataPackets.Add(analogDataPacket);
                            break;

                        // These data packets might be in the stream, just discard it.
                        case ChannelTypes.CanFd:
                            var canFdChannelHeader = new CanFdChannelHeader(payloadReader);
                            bytesLeft -= canFdChannelHeader.GetBinarySize();

                            var canFdDataPacket = new CanFdDataPacket(genericChannelHeader, canFdChannelHeader, payloadReader);
                            bytesLeft -= canFdDataPacket.GetBinarySize();
                            break;

                        case ChannelTypes.Tacho:
                            var tachoDataPacket = new TachoDataPacket(genericChannelHeader, payloadReader);
                            bytesLeft -= tachoDataPacket.GetBinarySize();
                            break;

                        default:
                            throw new InvalidOperationException($"The channel type received in the stream is not supported by this example.");
                    }
                }
            }
        }
    }
}

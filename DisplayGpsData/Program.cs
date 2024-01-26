using QClient.RestfulClient;
using QProtocol;
using QProtocol.DataStreaming.DataPackets;
using QProtocol.DataStreaming.Headers;
using QProtocol.GenericDefines;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

var ipAddress = args[0];
var systemRestfulInterface = new RestfulInterface($"http://{ipAddress}:8080");

var streamingSetup = systemRestfulInterface.Get<DataStreamSetup>(EndPoints.DataStreamSetup);
var tcpClient = new TcpClient(ipAddress, streamingSetup.TCPPort);
var networkStreamer = tcpClient.GetStream();
var buffer = new byte[1024];

try
{
    var stopwatch = Stopwatch.StartNew();
    while (true)
    {
        if (Console.KeyAvailable &&
            Console.ReadKey().Key == ConsoleKey.C)
        {
            break;
        }

        var message = ReadData(networkStreamer, buffer);
        if (string.IsNullOrEmpty(message) == false)
        {
            Console.Write(message);
            stopwatch.Restart();
        }
        else if (stopwatch.ElapsedMilliseconds > 2000)
        {
            Console.WriteLine("Noting received for 2 s");
            stopwatch.Restart();
        }
    }
}
finally
{
    networkStreamer.Flush();
    networkStreamer.Close();
    tcpClient.Close();
}

string ReadData(NetworkStream networkStreamer, byte[] buffer)
{
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
            return string.Empty;
        }
    }

    var bytesLeft = packetHeader.PayloadSize;
    if (bytesLeft == 0)
    {
        return string.Empty;
    }

    var message = new StringBuilder();
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

                case ChannelTypes.Gps:
                    var gpsChannelHeader = new GpsChannelHeader(payloadReader);
                    bytesLeft -= gpsChannelHeader.GetBinarySize();

                    var gpsDataPacket = new GpsDataPacket(genericChannelHeader, gpsChannelHeader, payloadReader);
                    bytesLeft -= gpsDataPacket.GetBinarySize();
                    message.AppendLine($"{DateTime.Now:G} - Timestamp: {gpsDataPacket.GpsChannelHeader.Timestamp}");
                    message.AppendLine($"{gpsDataPacket.Message}");
                    break;

                default:
                    throw new InvalidOperationException($"The channel type received in the stream is not supported by this example.");
            }
        }
    }

    return message.ToString();
}
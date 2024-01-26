using QClient.RestfulClient;
using QProtocol;
using QProtocol.DataStreaming.DataPackets;
using QProtocol.DataStreaming.Headers;
using QProtocol.GenericDefines;
using System.Net.Sockets;

namespace StreamAndSaveData;

public class DataStreamer
{
    private byte[] buffer = new byte[2048];
    private bool _timestampAlligned = false;
    private TcpClient _tcpClient;
    private NetworkStream _stream;
    private Task _streamingThread;

    private readonly Dictionary<int, List<AnalogDataPacket>> _analogDataPackets = new();
    private readonly Dictionary<int, List<GpsDataPacket>> _gpsDataPackets = new();

    private readonly List<int> _channelIds = new();
    private readonly DataStreamSetup _streamingSetup;
    private readonly CancellationTokenSource _tokenSource = new();
    private readonly CancellationToken _token;

    private readonly SemaphoreSlim _semaphore = new(1);

    public DataStreamer(RestfulInterface restInterface, List<int> channelIds)
    {
        _channelIds = channelIds;
        _streamingSetup = restInterface.Get<DataStreamSetup>(EndPoints.DataStreamSetup);
        _token = _tokenSource.Token;
    }

    public void StartStreaming()
    {
        // This will initiate the data stream. After this we need to gather data fast.
        _streamingThread = Task.Factory.StartNew(ReadData, _token);
    }

    public void StopStreaming()
    {
        _tokenSource.Cancel();
        while (!_streamingThread.IsCanceled && !_streamingThread.IsCompleted)
        {
            Thread.Sleep(1);
        }

        _stream.Flush();
        _stream.Close();
        _tcpClient.Close();
    }

    private void ReadData()
    {
        _tcpClient = new TcpClient(_streamingSetup.IPAddresses[0], _streamingSetup.TCPPort);
        _stream = _tcpClient.GetStream();

        while (_token.IsCancellationRequested == false)
        {
            if (_stream.DataAvailable == false)
            {
                Thread.Sleep(1);
                continue;
            }

            _stream.ReadExactly(buffer, 0, (int)PacketHeader.BinarySize);

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
                    _stream.ReadExactly(buffer, 0, (int)packetHeader.PayloadSize);
                    return;
                }
            }

            var bytesLeft = packetHeader.PayloadSize;
            if (bytesLeft == 0)
            {
                return;
            }

            _stream.ReadExactly(buffer, 0, (int)bytesLeft);
            using (var memoryStream = new MemoryStream(buffer))
            using (var payloadReader = new BinaryReader(memoryStream))
            {
                lock (_semaphore)
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
                                if (_channelIds.Any(id => id == genericChannelHeader.ChannelId))
                                {
                                    if (_analogDataPackets.ContainsKey(genericChannelHeader.ChannelId) == false)
                                    {
                                        _analogDataPackets.Add(genericChannelHeader.ChannelId, new List<AnalogDataPacket>());
                                    }

                                    _analogDataPackets[genericChannelHeader.ChannelId].Add(analogDataPacket);
                                }
                                
                                break;

                            case ChannelTypes.Gps:
                                var gpsChannelHeader = new GpsChannelHeader(payloadReader);
                                bytesLeft -= gpsChannelHeader.GetBinarySize();

                                var gpsDataPacket = new GpsDataPacket(genericChannelHeader, gpsChannelHeader, payloadReader);
                                bytesLeft -= gpsDataPacket.GetBinarySize();

                                if (_gpsDataPackets.ContainsKey(genericChannelHeader.ChannelId) == false)
                                {
                                    _gpsDataPackets.Add(genericChannelHeader.ChannelId, new List<GpsDataPacket>());
                                }

                                _gpsDataPackets[genericChannelHeader.ChannelId].Add(gpsDataPacket);
                                break;

                            // These data packets might be in the stream, just discard it.
                            case ChannelTypes.CanFd:
                                var canFdChannelHeader = new CanFdChannelHeader(payloadReader);
                                bytesLeft -= canFdChannelHeader.GetBinarySize();

                                var canFdDataPacket = new CanFdDataPacket(genericChannelHeader, canFdChannelHeader, payloadReader);
                                bytesLeft -= canFdDataPacket.GetBinarySize();
                                // discard
                                break;

                            case ChannelTypes.Tacho:
                                var tachoDataPacket = new TachoDataPacket(genericChannelHeader, payloadReader);
                                bytesLeft -= tachoDataPacket.GetBinarySize();
                                // discard
                                break;

                            default:
                                throw new InvalidOperationException($"The channel type received in the stream is not supported by this example.");
                        }
                    }
                }
            }
        }
    }

    public bool IsDataReady()
    {
        lock (_semaphore)
        {
            if (_timestampAlligned)
            {
                return _analogDataPackets.All(pair => pair.Value.Count > 0) || _gpsDataPackets.All(pair => pair.Value.Count > 0);
            }

            foreach (var itemId in _channelIds)
            {
                if (_analogDataPackets.ContainsKey(itemId) == false)
                {
                    return false;
                }
            }

            if (_analogDataPackets.Any(pair => pair.Value.Count() == 0))
            {
                return false;
            }

            do
            {
                var smallestTimeValue = _analogDataPackets.Min(pair => pair.Value.First().GenericChannelHeader.Timestamp);
                var existForAllChannels = _analogDataPackets.All(pair => pair.Value.Any(packet => packet.GenericChannelHeader.Timestamp == smallestTimeValue));
                if (existForAllChannels)
                {
                    _timestampAlligned = true;
                    return true;
                }

                foreach (var packet in _analogDataPackets)
                {
                    packet.Value.RemoveAll(packet => packet.GenericChannelHeader.Timestamp == smallestTimeValue);
                }
            } while (_analogDataPackets.Any(pair => pair.Value.Count > 0));
        }

        return false;
    }

    public void GetAnalogData(Action<int, List<AnalogDataPacket>> getDataForChannelId)
    {
        lock (_semaphore)
        {
            foreach (var channelBuffer in _analogDataPackets)
            {
                getDataForChannelId(channelBuffer.Key, channelBuffer.Value);
                channelBuffer.Value.Clear();
            }
        }
    }

    public void GetGpsData(Action<int, List<GpsDataPacket>> getDataForChannelId)
    {
        lock (_semaphore)
        {
            foreach (var channelBuffer in _gpsDataPackets)
            {
                getDataForChannelId(channelBuffer.Key, channelBuffer.Value);
                channelBuffer.Value.Clear();
            }
        }
    }

    public void Close()
    {
        _stream.Flush();
        _stream.Close();
        _tcpClient.Close();
    }
}

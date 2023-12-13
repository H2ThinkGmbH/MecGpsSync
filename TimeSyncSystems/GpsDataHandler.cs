using QServerValidation.DataStreaming.GpsHelper;

namespace TimeSyncSystems;

public class GpsDataHandler
{
    private int _referenceChannelId = -1;
    private int _syncChannelId = -1;
    private ulong _referenceSystemDelay = 0;
    private ulong _syncSystemDelay = 0;

    public GpsDataHandler(int referenceChannelId, int syncChannelId)
    {
        _referenceChannelId = referenceChannelId;
        _syncChannelId = syncChannelId;
    }

    public (List<float> referenceSampleList, List<float> syncSampleList) GetDataBlock(DataStreamer referenceSystem, DataStreamer syncSystem)
    {
        if (_referenceSystemDelay == 0 && _syncSystemDelay == 0)
        {
            // With GPS sync we need to establish an offset from the system's internal timestamp and the GPS packet.
            // We can then correct the "sync time" with this offset and align the packets.
            // We only have to do this once, after which we trust the system sync and this delay remains constant.
            var referenceGpsPacket = referenceSystem.GpsDataPackets.First();
            var referenceGpGgaMessage = new GpGgaMessage(referenceGpsPacket.Message);
            _referenceSystemDelay = (ulong)referenceGpsPacket.GpsChannelHeader.Timestamp * 1000000; // ms to ns

            var syncGpsPacket = syncSystem.GpsDataPackets.First();
            var syncGpGgaMessage = new GpGgaMessage(syncGpsPacket.Message);
            _syncSystemDelay = (ulong)syncGpsPacket.GpsChannelHeader.Timestamp * 1000000;

            if (referenceGpGgaMessage.Time.CompareTo(syncGpGgaMessage.Time) != 0)
            {
                throw new InvalidOperationException("The time received from the two GPS messages are different hence we cannot align the data.");
            }
        }

        // First determine the time stamps for both systems, also adjust it with the GPS absolute delay.
        var referenceDataTimestamp = referenceSystem.AnalogDataPackets
                                                    .Where(packet => packet.GenericChannelHeader.ChannelId == _referenceChannelId)
                                                    .First()
                                                    .GenericChannelHeader.Timestamp;
        referenceDataTimestamp -= _referenceSystemDelay;

        var referenceSampleBuffer = referenceSystem.AnalogDataPackets
                                                   .Where(packet => packet.GenericChannelHeader.ChannelId == _referenceChannelId)
                                                   .SelectMany(packet => packet.SampleList)
                                                   .ToList();

        var syncDataTimestamp = syncSystem.AnalogDataPackets
                                          .Where(packet => packet.GenericChannelHeader.ChannelId == _syncChannelId)
                                          .First()
                                          .GenericChannelHeader.Timestamp;
        syncDataTimestamp -= _syncSystemDelay;

        var syncSampleBuffer = syncSystem.AnalogDataPackets
                                         .Where(packet => packet.GenericChannelHeader.ChannelId == _syncChannelId)
                                         .SelectMany(packet => packet.SampleList)
                                         .ToList();

        var absoluteDelta = (long)syncDataTimestamp - (long)referenceDataTimestamp; // in ns
        var sampleCount = (int)(65536 * Math.Abs(absoluteDelta) / 1000000000); // SR * delta / ns

        // Now here is the tricky bit, since QServer sends data as soon as it is ready, we must ensure the payloads we 
        // receive are adjusted for the absolute delta as calculated. Hence, align the sample array by trimming the 
        // data form the system who is leading.
        if (absoluteDelta > 0)
        {
            // Meaning my sync system's time is lagging behind the reference system
            referenceSampleBuffer.RemoveRange(0, sampleCount);
        }
        else
        {
            syncSampleBuffer.RemoveRange(0, sampleCount);
        }

        return (referenceSampleBuffer, syncSampleBuffer);
    }
}

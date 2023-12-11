using QServerValidation.DataStreaming.GpsHelper;
using System.Linq;

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
        // Grab the data packets saved.
        var referencePackets = referenceSystem.AnalogDataPackets
                                              .Where(packet => packet.GenericChannelHeader.ChannelId == _referenceChannelId)
                                              .ToList();

        var syncPackets = syncSystem.AnalogDataPackets
                                    .Where(packet => packet.GenericChannelHeader.ChannelId == _syncChannelId)
                                    .ToList();

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

        // Next find the impulse on both channels, save a block before and block after the impulse too.
        var referenceDataTimestamp = 0ul;
        var referenceSamples = new List<float>();
        var pulseCount = 1;
        for (int index = 1; index < referencePackets.Count - 1; index++)
        {
            if (referencePackets[index].AnalogChannelHeader.Max < 0.1)
            {
                continue;
            }

            // Ignore the first pulse since we could have started a sample block in the middle of it.
            if (pulseCount != 0)
            {
                pulseCount = 0;
                continue;
            }

            referenceSamples.AddRange(referencePackets[index - 1].SampleList);
            referenceSamples.AddRange(referencePackets[index].SampleList);
            referenceSamples.AddRange(referencePackets[index + 1].SampleList);
            referenceDataTimestamp = referencePackets[index - 1].GenericChannelHeader.Timestamp;
            break;
        }

        var syncDataTimestamp = 0ul;
        var syncSamples = new List<float>();
        pulseCount = 1;
        for (int index = 1; index < syncPackets.Count - 1; index++)
        {
            if (syncPackets[index].AnalogChannelHeader.Max < 0.1)
            {
                continue;
            }

            if (pulseCount != 0)
            {
                pulseCount = 0;
                continue;
            }

            syncSamples.AddRange(syncPackets[index - 1].SampleList);
            syncSamples.AddRange(syncPackets[index].SampleList);
            syncSamples.AddRange(syncPackets[index + 1].SampleList);
            syncDataTimestamp = syncPackets[index - 1].GenericChannelHeader.Timestamp;
            break;
        }

        if (referenceDataTimestamp == 0 || syncDataTimestamp == 0)
        {
            throw new IndexOutOfRangeException("Could not find the impulse for both streams of data.");
        }

        // Now correct for the time delay between the two systems.
        var referenceDelta = (long)(referenceDataTimestamp - _referenceSystemDelay);
        var syncDelta = (long)(syncDataTimestamp - _syncSystemDelay);

        var absoluteDelta = syncDelta - referenceDelta; // in ns

        // Discard the amount of samples to align the data.
        var sampleCount = (int)(65536 * Math.Abs(absoluteDelta) / 1000000000); // SR * delta
        if (absoluteDelta < 0) 
        {
            syncSamples.RemoveRange(0, sampleCount);
            referenceSamples.RemoveRange(referenceSamples.Count - sampleCount, sampleCount);
        }
        else
        {
            syncSamples.RemoveRange(syncSamples.Count - sampleCount, sampleCount);
            referenceSamples.RemoveRange(0, sampleCount);
        }

        return (referenceSamples, syncSamples);
    }
}

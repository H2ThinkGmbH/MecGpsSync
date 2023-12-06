using QServerValidation.DataStreaming.GpsHelper;
using System.Linq;

namespace TimeSyncSystems;

public class GpsDataHandler
{
    public static (List<float> referenceSampleList, List<float> syncSampleList) GetDataBlock(DataStreamer referenceSystem, DataStreamer syncSystem)
    {
        // Find the first channel. It is not necessarily the first packet in the list, but it will have the lowest assigned ID.
        var referenceChannelId = referenceSystem.AnalogDataPackets
                                                .Select(packet => packet.GenericChannelHeader.ChannelId)
                                                .Min();

        // Next, grab the data packets saved.
        var referencePackets = referenceSystem.AnalogDataPackets
                                              .Where(packet => packet.GenericChannelHeader.ChannelId == referenceChannelId)
                                              .ToList();


        // Repeat for sync system.
        var syncChannelId = syncSystem.AnalogDataPackets
                                      .Select(packet => packet.GenericChannelHeader.ChannelId)
                                      .Min();

        var syncPackets = syncSystem.AnalogDataPackets
                                    .Where(packet => packet.GenericChannelHeader.ChannelId == syncChannelId)
                                    .ToList();

        // With GPS sync we need to establish an offset from the system's internal timestamp and the GPS packet.
        // We can then correct the "sync time" with this offset and align the packets.
        var referenceGpsPacket = referenceSystem.GpsDataPackets.First();
        var referenceGpGgaMessage = new GpGgaMessage(referenceGpsPacket.Message);
        var referenceGpsTimestamp = (ulong)referenceGpsPacket.GpsChannelHeader.Timestamp * 1000000; // ms to ns

        var syncGpsPacket = syncSystem.GpsDataPackets.First();
        var syncGpGgaMessage = new GpGgaMessage(syncGpsPacket.Message);
        var syncGpsTimestamp = (ulong)syncGpsPacket.GpsChannelHeader.Timestamp * 1000000;

        if (referenceGpGgaMessage.Time.CompareTo(syncGpGgaMessage.Time) != 0)
        {
            throw new InvalidOperationException("The time received from the two GPS messages are different hence we cannot align the data.");
        }

        // Next find the impulse on both channels, save a block before and block after the impulse too.
        var referenceDataTimestamp = 0ul;
        var referenceSamples = new List<float>();
        for (int index = 1; index < referencePackets.Count - 1; index++)
        {
            if (referencePackets[index].AnalogChannelHeader.Max < 0.1)
            {
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
        for (int index = 1; index < syncPackets.Count - 1; index++)
        {
            if (syncPackets[index].AnalogChannelHeader.Max < 0.1)
            {
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
        var referenceDelta = (long)(referenceDataTimestamp - referenceGpsTimestamp);
        var syncDelta = (long)(syncDataTimestamp - syncGpsTimestamp);

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

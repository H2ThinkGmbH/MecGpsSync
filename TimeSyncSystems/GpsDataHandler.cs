namespace TimeSyncSystems;

public class GpsDataHandler
{
    public static (float[] referenceSampleList, float[] syncSampleList) GetDataBlock(DataStreamer referenceSystem, DataStreamer syncSystem)
    {
        // Find the first channel. It is not necessarily the first packet in the list, but it will have the lowest assigned ID.
        var referenceChannelId = referenceSystem.AnalogDataPackets
                                                .Select(packet => packet.GenericChannelHeader.ChannelId)
                                                .Min();

        // Next, grab the data packets saved.
        var referencePackets = referenceSystem.AnalogDataPackets
                                              .Where(packet => packet.GenericChannelHeader.ChannelId == referenceChannelId)
                                              .ToList();

        // And the timestamps (for reference)
        var referenceTimestamps = referencePackets.Select(packet => packet.GenericChannelHeader.Timestamp)
                                                  .ToList();

        // Repeat for sync system.
        var syncChannelId = syncSystem.AnalogDataPackets
                                      .Select(packet => packet.GenericChannelHeader.ChannelId)
                                      .Min();

        var syncPackets = syncSystem.AnalogDataPackets
                                    .Where(packet => packet.GenericChannelHeader.ChannelId == syncChannelId)
                                    .ToList();

        var syncTimestamps = syncPackets.Select(packet => packet.GenericChannelHeader.Timestamp)
                                        .ToList();

        // With GPS sync we need to establish an offset from the system's internal timestamp and the GPS packet.
        // We can then correct the "sync time" with this offset and align the packets.
        var referecenceGpsTimestamps = referenceSystem.GpsDataPackets
                                                      .Select(packet => packet.GpsChannelHeader.Timestamp)
                                                      .ToList();

        var syncGpsTimestamps = syncSystem.GpsDataPackets
                                          .Select(packet => packet.GpsChannelHeader.Timestamp)
                                          .ToList();

        // Dump the timestamps to a file for reference.
        using var fileWriter = new StreamWriter("GpsTimeStamp_System_1.csv");
        fileWriter.WriteLine("referenceTimestamp,syncTimestamp,referenceGpsTimestamp,syncGpsTimestamp,");
        for (int index = 0; index < referenceTimestamps.Count && index < syncTimestamps.Count; index++)
        {
            var gpsTimestamp = (index < referecenceGpsTimestamps.Count
                ? $"{referecenceGpsTimestamps[index]},"
                : ",");

            gpsTimestamp += (index < syncGpsTimestamps.Count
                ? $"{syncGpsTimestamps[index]},"
                : ",");

            fileWriter.WriteLine($"{referenceTimestamps[index]},{syncTimestamps[index]},{gpsTimestamp}");
        }

        fileWriter.Close();

        // Next find the impulse on both channels.
        var referenceTimestamp = 0ul;
        for (int index = 0; index < referencePackets.Count; index++)
        {
            var packet = referencePackets[index];
            if (packet.AnalogChannelHeader.Max < 0.1)
            {
                continue;
            }

            referenceTimestamp = packet.GenericChannelHeader.Timestamp;
            break;
        }

        var syncTimestamp = 0ul;
        for (int index = 0; index < syncPackets.Count; index++)
        {
            var packet = syncPackets[index];
            if (packet.AnalogChannelHeader.Max < 0.1)
            {
                continue;
            }

            syncTimestamp = packet.GenericChannelHeader.Timestamp;
            break;
        }

        var startTimestamp = referenceTimestamp < syncTimestamp ? referenceTimestamp : syncTimestamp;
        var stopTimestamp = referenceTimestamp > syncTimestamp ? referenceTimestamp : syncTimestamp;

        if (referencePackets.First().GenericChannelHeader.Timestamp > startTimestamp
            || syncPackets.First().GenericChannelHeader.Timestamp > startTimestamp)
        {
            throw new IndexOutOfRangeException("The start timestamp does not exist for both packet lists");
        }

        if (referencePackets.Last().GenericChannelHeader.Timestamp < stopTimestamp
            || syncPackets.Last().GenericChannelHeader.Timestamp < stopTimestamp)
        {
            throw new IndexOutOfRangeException("The stop timestamp does not exist for both packet lists");
        }

        // Now select the block of data that exist in both streams which had the same time stamps.
        var referenceBlock = referencePackets.Where(packet => packet.GenericChannelHeader.Timestamp >= startTimestamp)
                                             .Where(packet => packet.GenericChannelHeader.Timestamp <= stopTimestamp)
                                             .SelectMany(packet => packet.SampleList)
                                             .ToArray();

        var syncBlock = syncPackets.Where(packet => packet.GenericChannelHeader.Timestamp >= startTimestamp)
                                   .Where(packet => packet.GenericChannelHeader.Timestamp <= stopTimestamp)
                                   .SelectMany(packet => packet.SampleList)
                                   .ToArray();

        return (referenceBlock, syncBlock);
    }
}

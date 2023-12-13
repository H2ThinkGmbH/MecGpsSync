namespace TimeSyncSystems;

public class PtpDataHandler
{
    public static (List<float> referenceSampleList, List<float> syncSampleList) GetDataBlock(DataStreamer referenceSystem, DataStreamer syncSystem) 
    {
        // Find the first channel. It is not necessarily the first packet in the list, but it will have the lowest assigned ID.
        var referenceChannelId = referenceSystem.AnalogDataPackets
                                                .Select(packet => packet.GenericChannelHeader.ChannelId)
                                                .Min();

        var referencePackets = referenceSystem.AnalogDataPackets
                                              .Where(packet => packet.GenericChannelHeader.ChannelId == referenceChannelId)
                                              .ToList();

        var syncChannelId = syncSystem.AnalogDataPackets
                                      .Select(packet => packet.GenericChannelHeader.ChannelId)
                                      .Min();

        var syncPackets = syncSystem.AnalogDataPackets
                                    .Where(packet => packet.GenericChannelHeader.ChannelId == syncChannelId)
                                    .ToList();

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
                                             .ToList();

        var syncBlock = syncPackets.Where(packet => packet.GenericChannelHeader.Timestamp >= startTimestamp)
                                   .Where(packet => packet.GenericChannelHeader.Timestamp <= stopTimestamp)
                                   .SelectMany(packet => packet.SampleList)
                                   .ToList();

        return (referenceBlock, syncBlock);
    }
}

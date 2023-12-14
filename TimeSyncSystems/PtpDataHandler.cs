namespace TimeSyncSystems;

public class PtpDataHandler
{
    private int _referenceChannelId = -1;
    private int _syncChannelId = -1;

    public PtpDataHandler(int referenceChannelId, int syncChannelId)
    {
        _referenceChannelId = referenceChannelId;
        _syncChannelId = syncChannelId;
    }

    public (List<float> referenceSampleList, List<float> syncSampleList) GetDataBlock(DataStreamer referenceSystem, DataStreamer syncSystem) 
    {
        var referencePackets = referenceSystem.AnalogDataPackets
                                              .Where(packet => packet.GenericChannelHeader.ChannelId == _referenceChannelId)
                                              .ToList();

        var syncPackets = syncSystem.AnalogDataPackets
                                    .Where(packet => packet.GenericChannelHeader.ChannelId == _syncChannelId)
                                    .ToList();

        // Find the start and stop timestamp which exist in both streams.
        var referenceTimestamp = referencePackets.First().GenericChannelHeader.Timestamp;
        var syncTimestamp = syncPackets.First().GenericChannelHeader.Timestamp;
        var startTimestamp = referenceTimestamp > syncTimestamp ? referenceTimestamp : syncTimestamp;

        referenceTimestamp = referencePackets.Last().GenericChannelHeader.Timestamp;
        syncTimestamp = syncPackets.Last().GenericChannelHeader.Timestamp;
        var stopTimestamp = referenceTimestamp < syncTimestamp ? referenceTimestamp : syncTimestamp;

        if (referencePackets.Any(packet => packet.GenericChannelHeader.Timestamp >= startTimestamp) == false
            || syncPackets.Any(packet => packet.GenericChannelHeader.Timestamp >= startTimestamp) == false)
        {
            throw new IndexOutOfRangeException("The start timestamp does not exist for both packet lists");
        }

        if (referencePackets.Any(packet => packet.GenericChannelHeader.Timestamp <= stopTimestamp) == false
            || syncPackets.Any(packet => packet.GenericChannelHeader.Timestamp <= stopTimestamp) == false)
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

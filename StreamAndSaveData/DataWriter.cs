using QProtocol.DataStreaming.DataPackets;
using System.Text;

namespace StreamAndSaveData;

public class DataWriter
{
    private readonly string _basePath = string.Empty;
    private readonly double _timestampDelta;

    public DataWriter(string path, double sampleRate)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }

        Directory.CreateDirectory(path);
        _basePath = path;
        _timestampDelta = 1 / sampleRate;
    }

    public void SaveAnalogData(int channelId, List<AnalogDataPacket> packets)
    {
        using var fileWriter = new StreamWriter(Path.Combine(_basePath, $"Analog_Channel_{channelId:00}.csv"), true);
        var stringBuilder = new StringBuilder();
        foreach (var packet in packets)
        {
            var startTimestamp = packet.GenericChannelHeader.Timestamp / 1000000000.0;
            packet.SampleList.ForEach(sample => stringBuilder.AppendLine($"{startTimestamp += _timestampDelta:f6},{sample}"));
        }

        fileWriter.Write(stringBuilder.ToString());
    }

    public void SaveGpsData(int channelId, List<GpsDataPacket> packets)
    {
        using var fileWriter = new StreamWriter(Path.Combine(_basePath, $"Gps_Channel_{channelId:00}.csv"), true);
        var stringBuilder = new StringBuilder();
        foreach (var packet in packets)
        {
            var gpsSplit = packet.Message.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            stringBuilder.Append($"{packet.GpsChannelHeader.Timestamp},");
            for (int index = 0; index < gpsSplit.Length; index++)
            {
                stringBuilder.Append($"\"{gpsSplit[index]}\",");
            }

            stringBuilder.Append(Environment.NewLine);
        }

        fileWriter.Write(stringBuilder.ToString());
    }
}

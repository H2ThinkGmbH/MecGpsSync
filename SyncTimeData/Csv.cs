namespace SyncTimeData;

public static class Csv
{
    public static (List<double> timestamps, List<double> samples) ReadFromFile(string filePath)
    {
        var timestamps = new List<double>();
        var samples = new List<double>();

        foreach (var line in File.ReadLines(filePath))
        {
            var splitLine = line.Split(',');
            timestamps.Add(double.Parse(splitLine[0]));
            samples.Add(double.Parse(splitLine[1]));
        }

        return (timestamps, samples);
    }
}

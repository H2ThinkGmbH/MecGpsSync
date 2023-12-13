namespace PlotDrift;

public static class Csv
{
    public static (double[] timestamps, double[] channel1, double[] channel2) Parse(string filePath)
    {
        var timestamps = new List<double>();
        var channel1 = new List<double>();
        var channel2 = new List<double>();

        foreach (var line in File.ReadLines(filePath))
        {
            var splitLine = line.Split(',');
            timestamps.Add(double.Parse(splitLine[0]));
            channel1.Add(double.Parse(splitLine[1]));
            channel2.Add(double.Parse(splitLine[2]));
        }

        return (timestamps.ToArray(), channel1.ToArray(), channel2.ToArray());
    }
}

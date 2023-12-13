// See https://aka.ms/new-console-template for more information
using PlotDrift;
using ScottPlot;
using System.Globalization;

string rootFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
Console.WriteLine("Searching for data folders...");

// Define the culture for parsing dates in the folder name
var cultureInfo = new CultureInfo("en-US");
var dateTimeFormat = "yyyyMMdd HHmm";

var delayList = new List<DataPoint>();
foreach (var folder in Directory.EnumerateDirectories(rootFolder))
{
    if (DateTime.TryParseExact(Path.GetFileName(folder), dateTimeFormat, cultureInfo, DateTimeStyles.None, out DateTime folderDateTime))
    {
        // Process each CSV file and calculate delays
        var csvFilePath = Path.Combine(folder, "raw_data.csv");
        if (File.Exists(csvFilePath))
        {
            var (_, channel1, channel2) = Csv.Parse(csvFilePath);
            var delay = CalculateDelay.UsingCrossCorrelation(channel1, channel2, 65536, 256);
            delayList.Add(new DataPoint() { Time = folderDateTime, Value = delay });
        }
        else
        {
            Console.WriteLine($"No 'raw_data.csv' file found in folder '{folder}'.");
        }
    }
}

// Output the summary information
Console.WriteLine($"Number of files found and processed: {delayList.Count()}");
if (delayList.Any())
{
    Console.WriteLine($"Minimum delay: {delayList.Min(point => point.Value)} seconds");
    Console.WriteLine($"Maximum delay: {delayList.Max(point => point.Value)} seconds");
}

// Plot the delay
PlotAndSave(delayList);
Console.ReadKey();

static void PlotAndSave(List<DataPoint> dataPoints)
{
    // Create a new ScottPlot plot
    var plot = new Plot(1200, 800);

    // Add both the original time series and delay time series to the plot
    var firstTimeSample = dataPoints.First().Time;
    var timeValues = dataPoints.Select(point => (point.Time - firstTimeSample).TotalMinutes)
                               .ToArray();
    var delayValues = dataPoints.Select(point => point.Value).ToArray();
    plot.AddScatter(timeValues, delayValues);

    // Customize the plot style
    plot.Title("Delay Over Time");
    plot.XLabel("Time in minutes");
    plot.YLabel("Delay (s)");
    plot.Legend(location: Alignment.UpperRight);

    // Save the plot as an image file within the specified folder
    var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Result");
    string fileFullPath = Path.Combine(outputDirectory, $"DelayPlot.png");
    if (Directory.Exists(outputDirectory) == false)
    {
        Directory.CreateDirectory(outputDirectory);
    }

    plot.SaveFig(fileFullPath);

    // Notify the user where the plot has been saved
    Console.WriteLine($"Plot saved to: {fileFullPath}");
}

class DataPoint
{
    public DateTime Time { get; set; }
    public double Value { get; set; }
}
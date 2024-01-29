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
            var delay = CalculateDelay.UsingCrossCorrelation(channel1, channel2, 131072/2, 131072/2);
            delayList.Add(new DataPoint() { Time = folderDateTime, Value = delay });
        }
        else
        {
            Console.WriteLine($"No 'raw_data.csv' file found in folder '{folder}'.");
        }
    }
}

// Normalize the data
for (int index = 0; index < delayList.Count; index++)
{
    delayList[index].Value *= 1000000; // scale it to micro seconds
}

// Calculate stats
var average = delayList.Select(point => point.Value).Average();
var sumOfSquareDifferences = delayList.Select(point => (point.Value - average) * (point.Value - average)).Sum();
var standardDeviation = Math.Sqrt(sumOfSquareDifferences / delayList.Count());

// Output the summary information
Console.WriteLine($"Number of files found and processed: {delayList.Count()}");
if (delayList.Any())
{
    Console.WriteLine($"Average: {average} µs, STDEV: {standardDeviation} µs");
    Console.WriteLine($"Minimum delay: {delayList.Min(point => point.Value)} µs");
    Console.WriteLine($"Maximum delay: {delayList.Max(point => point.Value)} µs");
}

// Plot the delay
PlotAndSave(delayList);
Console.ReadKey();

static void PlotAndSave(List<DataPoint> dataPoints)
{
    // Create a new ScottPlot plot
    var plot = new Plot();

    // Add both the original time series and delay time series to the plot
    var firstTimeSample = dataPoints.First().Time;
    var timeValues = dataPoints.Select(point => (point.Time - firstTimeSample).TotalMinutes)
                               .ToArray();
    var delayValues = dataPoints.Select(point => point.Value).ToArray();
    plot.Add.Scatter(timeValues, delayValues);

    // Customize the plot style
    plot.Title("System time delay over time period measured with GPS enabled", size: 24);
    plot.XLabel("Time in minutes", size: 20);
    plot.YLabel("Delay (µs)", size: 20);
    plot.Legend.Location = Alignment.LowerRight;

    // Save the plot as an image file within the specified folder
    var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Result");
    string fileFullPath = Path.Combine(outputDirectory, $"DelayPlot.svg");
    if (Directory.Exists(outputDirectory) == false)
    {
        Directory.CreateDirectory(outputDirectory);
    }

    plot.SaveSvg(fileFullPath, 1200, 800);

    // Notify the user where the plot has been saved
    Console.WriteLine($"Plot saved to: {fileFullPath}");
}

class DataPoint
{
    public DateTime Time { get; set; }
    public double Value { get; set; }
}
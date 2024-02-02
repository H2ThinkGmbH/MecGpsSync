using FftSharp;
using ScottPlot;
using SyncTimeData;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

var timer = Stopwatch.StartNew();
var rootDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Data");
var systemList = new List<string>();
foreach (var folder in Directory.EnumerateDirectories(rootDirectory))
{
    var measurementCount = Directory.GetDirectories(folder).Length;
    if (measurementCount < 2)
    {
        throw new InvalidOperationException($"At least 2 measurements are need for sub-folder {folder}");
    }

    systemList.Add(folder);
}

var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Modified Data");
if (Directory.Exists(outputDirectory) == false)
{
    Directory.CreateDirectory(outputDirectory);
}

// Read or calculate the delay's between the systems based on the pulse.
var delayCalculationFilePath = Path.Combine(outputDirectory, "saved_delays.json");
List<Delay> delayList;
if (File.Exists(delayCalculationFilePath))
{
    using var fileReader = new StreamReader(delayCalculationFilePath);
    delayList = JsonSerializer.Deserialize<List<Delay>>(fileReader.ReadToEnd());
}
else
{
    delayList = CalculateDelayBetweenSystems(systemList, outputDirectory);
    using var fileWriter = new StreamWriter(delayCalculationFilePath, false);
    fileWriter.WriteLine(JsonSerializer.Serialize(delayList));
    fileWriter.Close();
}

foreach (var folder in Directory.EnumerateDirectories(outputDirectory))
{
    Directory.Delete(folder, true);
}

// Itterate over all the files saved and adjust for the calculated delays.
var numberOfMeasurements = Directory.GetDirectories(systemList.First()).Length;
for (int measurementIndex = 0; measurementIndex < numberOfMeasurements; measurementIndex++)
{
    for (int systemIndex = 0; systemIndex < systemList.Count; systemIndex++)
    {
        var measurementFolder = Directory.GetDirectories(systemList[systemIndex])[measurementIndex];
        Parallel.ForEach(Directory.EnumerateFiles(measurementFolder, "Analog*"), channelFile =>
        {
            var channelSamples = Csv.ReadFromFile(channelFile);
            var timeDelta = channelSamples.timestamps.First() - delayList[systemIndex].timeZero;
            var inSyncIndex = channelSamples.timestamps.FindIndex(timestamp => timestamp >= timeDelta) - delayList[systemIndex].sampleDelay;

            var outputFile = channelFile.Replace("\\Data\\", "\\Modified Data\\");
            var dirName = Path.GetDirectoryName(outputFile);
            if (Directory.Exists(dirName) == false)
            {
                Directory.CreateDirectory(dirName);
            }

            using var fileWriter = new StreamWriter(outputFile, false);
            for (int sampleIndex = inSyncIndex; sampleIndex < channelSamples.timestamps.Count; sampleIndex++)
            {
                fileWriter.WriteLine($"{channelSamples.timestamps[sampleIndex]},{channelSamples.samples[sampleIndex]}");
            }

            fileWriter.Close();
            channelSamples.timestamps.Clear();
            channelSamples.samples.Clear();
        });
    }
}

// first trim the data sets to equal length.
Console.WriteLine($"Runtime: {timer.Elapsed}");
Environment.Exit(0);

static void PlotSignal(string filePath,
                       List<(List<double> timestamps, List<double> samples)> firstChannelData,
                       bool showPopup)
{
    var plot = new Plot();

    // Add both the original time series and delay time series to the plot
    var count = 0;
    var period = firstChannelData[0].timestamps[1] - firstChannelData[0].timestamps[0];
    foreach (var sampleCollection in firstChannelData)
    {
        var signal = plot.Add.Signal(sampleCollection.samples, period);
        signal.Label = $"Channel {count++}";
        plot.Legend.IsVisible = true;
    }

    // Customize the plot style
    plot.Title("Signal measured by two indipendant systems", size: 24);
    plot.XLabel("Time in seconds", size: 20);
    plot.YLabel("Voltage (V)", size: 20);
    plot.Legend.Location = Alignment.LowerRight;

    // Save the plot as an image file within the specified folder
    var fileFullPath = $"{filePath}.svg";
    plot.SaveSvg(fileFullPath, 1200, 800);
    Console.WriteLine($"Plot saved to: {fileFullPath}");

    if (showPopup)
    {
        var thread = new Thread(() =>
        {
            ScottPlot.WinForms.FormsPlotViewer.Launch(plot);
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        while (thread.IsAlive)
        {
            Thread.Sleep(25);
        }
    }
}

static void PlotSignalBlock(string filePath,
                            List<List<double>> sampleBlock,
                            bool showPopup)
{
    var plot = new Plot();

    // Add both the original time series and delay time series to the plot
    var count = 0;
    foreach (var sampleCollection in sampleBlock)
    {
        var signal = plot.Add.Signal(sampleCollection);
        signal.Label = $"Channel {count++}";
        plot.Legend.IsVisible = true;
    }

    // Customize the plot style
    plot.Title("Signal measured by two indipendant systems", size: 24);
    plot.XLabel("Time in seconds", size: 20);
    plot.YLabel("Voltage (V)", size: 20);
    plot.Legend.Location = Alignment.LowerRight;

    // Save the plot as an image file within the specified folder
    var fileFullPath = $"{filePath}.svg";
    plot.SaveSvg(fileFullPath, 1200, 800);
    Console.WriteLine($"Plot saved to: {fileFullPath}");

    if (showPopup)
    {
        var thread = new Thread(() =>
        {
            ScottPlot.WinForms.FormsPlotViewer.Launch(plot);
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        while (thread.IsAlive)
        {
            Thread.Sleep(25);
        }
    }
}

static void TrimDataSet(int maxNumberOfSamples, List<(List<double> timestamps, List<double> samples)> firstChannelData)
{
    for (int index = 0; index < firstChannelData.Count(); index++)
    {
        if (firstChannelData[index].samples.Count() > maxNumberOfSamples)
        {
            var listSize = firstChannelData[index].samples.Count;
            firstChannelData[index].timestamps.RemoveRange(maxNumberOfSamples, listSize - maxNumberOfSamples);
            firstChannelData[index].samples.RemoveRange(maxNumberOfSamples, listSize - maxNumberOfSamples);
        }
    }
}

static List<Delay> CalculateDelayBetweenSystems(List<string> systemList, string outputDirectory)
{
    // Some asumptions:.
    // 1) The first measurement has a sync pulse of some sort.
    // 2) Only the first channel will be used for the sync pulse.
    // 3) The first system always started to measure before the rest - i.e only positive delays can be considered.
    var maxNumberOfSamples = int.MaxValue;
    var firstChannelData = new List<(List<double> timestamps, List<double> samples)>();
    foreach (var systemFolder in systemList)
    {
        var syncMeasurementFile = Path.Combine(Directory.GetDirectories(systemFolder).First(), "Analog_Channel_13.csv");
        var channelSamples = Csv.ReadFromFile(syncMeasurementFile);
        firstChannelData.Add(channelSamples);
        maxNumberOfSamples = maxNumberOfSamples > channelSamples.samples.Count
            ? channelSamples.samples.Count
            : maxNumberOfSamples;
    }

    // first trim the data sets to equal length.
    TrimDataSet(maxNumberOfSamples, firstChannelData);
    PlotSignal(Path.Combine(outputDirectory, "Raw Plot"), firstChannelData, false);

    // Now determine the index of the delay between the time data.
    var delayCount = new List<Delay>() { new() { sampleDelay = 0, timeZero = 0 } };
    var comparePeriod = 65536;
    for (int referenceIndex = 0; referenceIndex < firstChannelData.First().samples.Count; referenceIndex += comparePeriod) // 1 second
    {
        // First look for a block of data where the pulse exist.
        // This will be a time instance where both positive and negative values exist which is greater than 0.2 V.
        var referenceSamples = firstChannelData.First().samples.Skip(referenceIndex).Take(comparePeriod);
        if (referenceSamples.Any(value => value > 0.2)
            && referenceSamples.Any(value => value < -0.02))
        {
            var referenceTimeEpoch = firstChannelData.First().timestamps[referenceIndex];
            for (int systemIndex = 1; systemIndex < firstChannelData.Count; systemIndex++)
            {
                // Pulse found, now compare with the other stream to find the delay!
                for (int syncIndex = 0; syncIndex < firstChannelData[systemIndex].samples.Count; syncIndex += comparePeriod)
                {
                    var syncSamples = firstChannelData[systemIndex].samples.Skip(syncIndex).Take(comparePeriod);
                    if (syncSamples.Any(sample => sample > 0.2)
                        && syncSamples.Any(sample => sample < 0.2))
                    {
                        PlotSignalBlock(Path.Combine(outputDirectory, "Sync Pulse Plot"), new List<List<double>>() { referenceSamples.ToList(), syncSamples.ToList() }, false);
                        var sampleDelay = CalculateDelay.UsingCrossCorrelation(referenceSamples, syncSamples, comparePeriod);
                        delayCount.Add(new() { timeZero = firstChannelData[systemIndex].timestamps[syncIndex] - referenceTimeEpoch, sampleDelay = sampleDelay });
                        break;
                    }
                }
            }

            break;
        }

        referenceIndex -= comparePeriod / 2; // overlap blocks to cover all posibilities.
    }
    
    return delayCount;
}
using ScottPlot;
using SyncTimeData;

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

var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Result");
if (Directory.Exists(outputDirectory) == false)
{
    Directory.CreateDirectory(outputDirectory);
}

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
var delayCount = new List<(double timeZero, int sampleDelay)>() { (0, 0) };
var comparePeriod = 65536;
for (int referenceIndex = 0; referenceIndex < firstChannelData.First().samples.Count; referenceIndex+=comparePeriod) // 1 second
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
                    delayCount.Add((firstChannelData[systemIndex].timestamps[syncIndex] - referenceTimeEpoch, sampleDelay));
                    break;
                }
            }
        }

        break;
    }

    referenceIndex -= comparePeriod / 2; // overlap blocks to cover all posibilities.
}

// Save the delay for next use:

// Read the delay info

// This needs to include all channels!
for (int systemIndex = 1; systemIndex < firstChannelData.Count; systemIndex++)
{
    // first determine the Time Zero index of the new delay between this system and system 1.
    var timeDelta = firstChannelData[systemIndex].timestamps.First() - delayCount[systemIndex].timeZero;
    var inSyncIndex = firstChannelData.First().timestamps.FindIndex(timestamp => timestamp >= timeDelta);
    var numberOfSyncSamples = firstChannelData.First().samples.Count - inSyncIndex;

    var maxSampleCount = firstChannelData[systemIndex].samples.Count > numberOfSyncSamples
        ? numberOfSyncSamples
        : firstChannelData[systemIndex].samples.Count;

    var referenceSamples = firstChannelData.First().samples.Skip(inSyncIndex - delayCount[systemIndex].sampleDelay).Take(maxSampleCount).ToList();
    var syncSamples = firstChannelData[systemIndex].samples.Take(maxSampleCount).ToList();
    PlotSignalBlock(Path.Combine(outputDirectory, "Signal In Sync Plot"), new List<List<double>>() { referenceSamples, syncSamples }, true);
}

// first trim the data sets to equal length.
TrimDataSet(maxNumberOfSamples, firstChannelData);
PlotSignal(Path.Combine(outputDirectory, "Synced Plot"), firstChannelData, true);
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
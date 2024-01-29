using PlotDrift;
using ScottPlot;

string rootFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

// Define the culture for parsing dates in the folder name
var sampleRate = 131072 / 2;

var delay = 0.0;
double[] refernceSamples, syncSamples;

// Process each CSV file and calculate delays
var csvFilePath = Path.Combine(rootFolder, "raw_data PTP.csv");
if (File.Exists(csvFilePath))
{
    (_, refernceSamples, syncSamples) = Csv.Parse(csvFilePath);
    delay = CalculateDelay.UsingCrossCorrelation(refernceSamples, syncSamples, sampleRate, sampleRate);
}
else
{
    throw new FileNotFoundException($"No file named raw_data.csv found in folder {rootFolder}");
}

// Output the summary information
Console.WriteLine($"Delay calculated: {delay * 1000000} us");

// Plot the delay
PlotAndSave(refernceSamples, syncSamples, sampleRate);
Console.ReadKey();

static void PlotAndSave(double[] referenceSamples, double[] syncSamples, double sampleRate)
{
    // Create a new ScottPlot plot
    var plot = new Plot();
    var referenceSignal = plot.Add.Signal(referenceSamples, 1000 / sampleRate);
    referenceSignal.Label = "Reference Channel";
    referenceSignal.MaximumMarkerSize = 20;
    referenceSignal.Marker.IsVisible = true;
    referenceSignal.Marker.Shape = MarkerShape.FilledCircle;

    var syncSignal = plot.Add.Signal(syncSamples, 1000 / sampleRate);
    syncSignal.Label = "Synchronized Channel";
    syncSignal.MaximumMarkerSize = 20;
    syncSignal.Marker.IsVisible = true;
    syncSignal.Marker.Shape = MarkerShape.FilledCircle;

    // Customize the plot style
    plot.Title("Time delay measured between two DAQ systems with PTP enabled", size: 24);
    plot.XLabel("Time in milliseconds (ms)", size: 20);
    plot.YLabel("Voltage (V)", size: 20);

    plot.Legend.IsVisible = true;
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

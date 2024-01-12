// H2Think gGmbH - In collaboration with Mecalc Technologies GmbH
// This example will illustrate how to connect to multiple Mecalc systems, acquire data and display if the data is in sync
// The requirements for this example are:
// * 2x DecaQ / MicroQ systems running QServer Q2.3.0 or newer
// * 1x ICS425 in each system

using QClient.RestfulClient;
using QProtocol;
using QProtocol.Advanced;
using QProtocol.Controllers;
using QProtocol.GenericDefines;
using QProtocol.InternalModules.ICS;
using System;
using System.Diagnostics;
using System.Reflection;
using TimeSyncSystems;

Console.WriteLine($"Mecalc Time Sync Systems {Assembly.GetExecutingAssembly().GetName().Version}");

var checkSystemTime = false;
var ptpSync = true;
var scopePulse = true;
var repeat = true;

var sampleRate = 131072 / 2;
var pulseFrequency = 2048.0;

// First connect to the separate systems
var ipSystem1 = "192.168.100.52";
var system1RestfulInterface = new RestfulInterface($"http://{ipSystem1}:8080");
var pingResponse = system1RestfulInterface.Get<InfoPing>(EndPoints.InfoPing);
if (pingResponse == null || pingResponse.Code < 0)
{
    Console.WriteLine("Unable to ping system 1");
}

var ipSystem2 = "192.168.100.44"; //"169.254.28.242";
var system2RestfulInterface = new RestfulInterface($"http://{ipSystem2}:8080");
pingResponse = system2RestfulInterface.Get<InfoPing>(EndPoints.InfoPing);
if (pingResponse == null || pingResponse.Code < 0)
{
    Console.WriteLine("Unable to ping system 2");
}

// The following section is used to set the system clocks.
// After the command has been set, the systems have to reboot and then the test can start.
// This is only needed for the non PTP and non GPS test case.
var system1Time = system1RestfulInterface.Get<SystemTime>(EndPoints.SystemTime);
var system2Time = system2RestfulInterface.Get<SystemTime>(EndPoints.SystemTime);
var timeDifference = CheckTimeDifference(system1Time, system2Time);
if (checkSystemTime && timeDifference > 0)
{
    var time = SystemTime.GetPresentTime();
    var setTimeTask1 = Task.Factory.StartNew(() => system1RestfulInterface.Put(EndPoints.SystemTime, time));
    var setTimeTask2 = Task.Factory.StartNew(() => system2RestfulInterface.Put(EndPoints.SystemTime, time));
    Console.WriteLine($"Time difference was {timeDifference} s, it was reset hence reboot both system and run the application again.");
    Console.ReadKey();

    while (setTimeTask1.IsCompleted == false && setTimeTask2.IsCompleted == false)  
    {
        Thread.Sleep(1);
    }

    Environment.Exit(0);
}

// Do a standard configuration.
// As explained before, this example is dependent on ICS425 Modules. If other Module types are used then this section.
// needs to be updated with the appropriate setup.
var system1Items = Item.CreateList(system1RestfulInterface);
var controller = (Controller)system1Items.First();
var controllerSettings = controller.GetItemSettings<Controller.EnabledSettings>();
controllerSettings.Settings.MasterSamplingRate = Controller.MasterSamplingRate._131072Hz;
controllerSettings.Settings.AnalogDataStreamingFormat = Controller.AnalogDataStreamingFormat.Raw;
controller.PutItemSettings(controllerSettings);

var system1Modules = system1Items.OfType<ICS425Module>().ToList();
var system1Channels = system1Items.OfType<ICS425Channel>().ToList();
if (system1Modules == null || system1Channels == null
    || system1Modules.Count < 1 || system1Channels.Count < 1)
{
    throw new InvalidOperationException("Unable to find the required Modules or Channels in system 1");
}

foreach (var module in system1Modules)
{
    module.PutItemOperationMode(ICS425Module.OperationMode.Enabled);
    var moduleSettings = module.GetItemSettings<ICS425Module.EnabledSettings>();
    moduleSettings.Settings.SampleRate = ICS425Module.SampleRate.MsrDivideBy2;
    module.PutItemSettings(moduleSettings);
}

foreach (var channel in system1Channels)
{
    channel.PutItemOperationMode(ICS425Channel.OperationMode.VoltageInput);
    var channelSettings = channel.GetItemSettings<ICS425Channel.VoltageInputSettings>();
    channelSettings.Settings.VoltageRange = ICS425Channel.VoltageRange._1V;
    channelSettings.Settings.VoltageInputCoupling = ICS425Channel.VoltageInputCoupling.Dc;
    channelSettings.Settings.InputBiasing = ICS425Channel.InputBiasing.SingleEnded;
    channelSettings.Data.StreamingState = Generic.Status.Enabled;
    if (channelSettings.Data.LocalStorage != null)
    {
        channelSettings.Data.LocalStorage = Generic.Status.Disabled;
    }

    channel.PutItemSettings(channelSettings);
}

// Repeat for system 2.
var system2Items = Item.CreateList(system2RestfulInterface);
var controller2 = (Controller)system2Items.First();
var controller2Settings = controller2.GetItemSettings<Controller.EnabledSettings>();
controller2Settings.Settings.MasterSamplingRate = Controller.MasterSamplingRate._131072Hz;
controller2Settings.Settings.AnalogDataStreamingFormat = Controller.AnalogDataStreamingFormat.Raw;
controller2.PutItemSettings(controller2Settings);

var system2Modules = system2Items.OfType<ICS425Module>().ToList();
var system2Channels = system2Items.OfType<ICS425Channel>().ToList();
if (system2Modules == null || system2Channels == null
    || system2Modules.Count < 1 || system2Channels.Count < 1)
{
    throw new InvalidOperationException("Unable to find the required Modules or Channels in system 1");
}

foreach (var module in system2Modules)
{
    module.PutItemOperationMode(ICS425Module.OperationMode.Enabled);
    var moduleSettings = module.GetItemSettings<ICS425Module.EnabledSettings>();
    moduleSettings.Settings.SampleRate = ICS425Module.SampleRate.MsrDivideBy2;
    module.PutItemSettings(moduleSettings);
}

foreach (var channel in system2Channels)
{
    channel.PutItemOperationMode(ICS425Channel.OperationMode.VoltageInput);
    var channelSettings = channel.GetItemSettings<ICS425Channel.VoltageInputSettings>();
    channelSettings.Settings.VoltageRange = ICS425Channel.VoltageRange._1V;
    channelSettings.Settings.VoltageInputCoupling = ICS425Channel.VoltageInputCoupling.Dc;
    channelSettings.Settings.InputBiasing = ICS425Channel.InputBiasing.SingleEnded;
    channelSettings.Data.StreamingState = Generic.Status.Enabled;
    if (channelSettings.Data.LocalStorage != null)
    {
        channelSettings.Data.LocalStorage = Generic.Status.Disabled;
    }

    channel.PutItemSettings(channelSettings);
}

var applyTask1 = Task.Factory.StartNew(() => system1RestfulInterface.Put(EndPoints.SystemSettingsApply));
var applyTask2 = Task.Factory.StartNew(() => system2RestfulInterface.Put(EndPoints.SystemSettingsApply));
while (applyTask1.IsCompleted == false && applyTask2.IsCompleted == false)
{
    Thread.Sleep(1);
}

// Save the channel Id's which is sampling the signal
var referenceChannelId = system1Channels.First().ItemId;
var syncChannelId = system2Channels.First().ItemId;

var system1StreamingSetup = system1RestfulInterface.Get<DataStreamSetup>(EndPoints.DataStreamSetup);
var system2StreamingSetup = system1RestfulInterface.Get<DataStreamSetup>(EndPoints.DataStreamSetup);

var timer = Stopwatch.StartNew();
var timestamp = timer.ElapsedMilliseconds;
do
{
    // Open a socket to each system
    // This will initiate the data transfer from the system to our application.
    var system1Streamer = new DataStreamer(ipSystem1, system1StreamingSetup.TCPPort, sampleRate);
    var system2Streamer = new DataStreamer(ipSystem2, system2StreamingSetup.TCPPort, sampleRate);

    // Create a results directory
    var timeSaved = DateTime.Now;
    var path = Path.Combine(Directory.GetCurrentDirectory(), "Results", timeSaved.ToString("yyyyMMdd HHmm"));

    // Save some data.
    system1Streamer.StartStreaming();
    system2Streamer.StartStreaming();
    Thread.Sleep(2500);
    system1Streamer.StopStreaming();
    system2Streamer.StopStreaming();

    // Find the impulse, and save the block of data.
    List<float> referenceSampleList = null;
    List<float> syncSampleList = null;

    if (ptpSync)
    {
        var ptpDataHandler = new PtpDataHandler(referenceChannelId, syncChannelId);
        (referenceSampleList, syncSampleList) = ptpDataHandler.GetDataBlock(system1Streamer, system2Streamer);
    }
    else
    {
        var gpsDataHandler = new GpsDataHandler(referenceChannelId, syncChannelId);
        (referenceSampleList, syncSampleList) = gpsDataHandler.GetDataBlock(system1Streamer, system2Streamer);
    }

    float[] referenceSampleArray = null;
    float[] syncSampleArray = null;
    if (scopePulse)
    {
        (referenceSampleArray, syncSampleArray) = FindPulse(sampleRate, pulseFrequency, ref path, referenceSampleList, syncSampleList);
    }
    else
    {
        referenceSampleArray = referenceSampleList.ToArray();
        syncSampleArray = syncSampleList.ToArray();
    }

    if (Directory.Exists(path) == false)
    {
        Directory.CreateDirectory(path);
    }

    var fileName = Path.Combine(path, "raw_data.csv");
    using var fileWriter = new StreamWriter(fileName, false);
    {
        for (int index = 0; index < referenceSampleArray.Length && index < syncSampleArray.Length; index++)
        {
            fileWriter.WriteLine($"{index / (double)sampleRate},{referenceSampleArray[index]},{syncSampleArray[index]}");
        }

        fileWriter.Close();
    }

    // Display the data block
    Console.WriteLine($"Sample taken at {timeSaved}");
    if (repeat == false)
    {

        var thread = new Thread(() =>
        {
            var plot = new ScottPlot.Plot(1200, 800);
            plot.AddSignal(referenceSampleArray, sampleRate, label: "Reference Channel");
            plot.AddSignal(syncSampleArray, sampleRate, label: "Synchronized Channel");
            plot.Legend();

            plot.Title($"A common input signal measured by two systems with {(ptpSync ? "PTP" : "GPS")} synchronization enabled");
            plot.XLabel("Time in seconds (s)");
            plot.YLabel("Voltage (V)");

            var fileName = Path.Combine(path, "Plot.png");
            plot.SaveFig(fileName);
            new ScottPlot.FormsPlotViewer(plot).ShowDialog();
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    // Continue to save data.
    while ((timer.ElapsedMilliseconds - timestamp) < 5 * 60 * 1000)
    {
        Thread.Sleep(250);
        if (Console.KeyAvailable && Console.ReadKey().Key == ConsoleKey.C)
        {
            Environment.Exit(0);
        }
    }

    timestamp = timer.ElapsedMilliseconds;
} while (repeat);

int CheckTimeDifference(SystemTime system1Time, SystemTime system2Time)
{
    var dateTimeSystem1 = new DateTime(system1Time.Year, system1Time.Month, system1Time.Day, system1Time.Hour, system1Time.Minutes, system1Time.Seconds);
    var dateTimeSystem2 = new DateTime(system2Time.Year, system2Time.Month, system2Time.Day, system2Time.Hour, system2Time.Minutes, system2Time.Seconds);
    return Math.Abs((int)(dateTimeSystem1 - dateTimeSystem2).TotalSeconds);
}

static (float[] referenceSampleArray, float[] syncSampleArray) FindPulse(
    int sampleRate,
    double pulseFrequency,
    ref string path,
    List<float> referenceSampleList,
    List<float> syncSampleList)
{
    var startIndex = 40;

    // Find the first slope which is at least 32 samples delayed from the start.
    // This makes pretty charts.
    var referenceSlopeLower = referenceSampleList.FindIndex(startIndex, sample => sample > 0.1 && sample < 0.2); // Should be a few in this range
    var referenceSlopeUpper = referenceSampleList.FindIndex(startIndex, sample => sample > 0.4);

    var syncSlopeLower = syncSampleList.FindIndex(startIndex, sample => sample > 0.1 && sample < 0.2);
    var syncSlopeUpper = syncSampleList.FindIndex(startIndex, sample => sample > 0.4);

    // No peaks found, save data and exit.
    if (referenceSlopeUpper < 0 || syncSlopeUpper < 0)
    {
        path += " suspect";
        return (referenceSampleList.ToArray(), syncSampleList.ToArray());
    }

    var firstSampleIndex = -32 + (syncSlopeLower < referenceSlopeLower
        ? syncSlopeLower
        : referenceSlopeLower);

    // Add enough samples to cover both.
    var lastSampleIndex = (int)(sampleRate / pulseFrequency) + 32 + (referenceSlopeLower > syncSlopeLower
        ? referenceSlopeLower
        : syncSlopeLower);

    // Some checks
    var sampleCount = lastSampleIndex - firstSampleIndex;
    var minimumSampleCount = (int)(sampleRate * 0.0015);
    if (sampleCount < minimumSampleCount) // = 5 ms
    {
        sampleCount = minimumSampleCount;
    }

    if (firstSampleIndex + sampleCount >= referenceSampleList.Count())
    {
        sampleCount = referenceSampleList.Count() - firstSampleIndex;
    }

    if (firstSampleIndex + sampleCount >= syncSampleList.Count())
    {
        sampleCount = syncSampleList.Count() - firstSampleIndex;
    }

    var referenceSampleArray = new float[sampleCount];
    var syncSampleArray = new float[sampleCount];

    referenceSampleList.CopyTo(firstSampleIndex, referenceSampleArray, 0, referenceSampleArray.Length);
    syncSampleList.CopyTo(firstSampleIndex, syncSampleArray, 0, syncSampleArray.Length);
    return (referenceSampleArray, syncSampleArray);
}
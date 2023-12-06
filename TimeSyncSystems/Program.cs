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
using System.Reflection;
using TimeSyncSystems;

Console.WriteLine($"Mecalc Time Sync Systems {Assembly.GetExecutingAssembly().GetName().Version}");

var checkSystemTime = false;
var ptpSync = false;
var scopePulse = true;

// First connect to the separate systems
var ipSystem1 = "192.168.100.45";
var system1RestfulInterface = new RestfulInterface($"http://{ipSystem1}:8080");
var pingResponse = system1RestfulInterface.Get<InfoPing>(EndPoints.InfoPing);
if (pingResponse == null || pingResponse.Code < 0)
{
    Console.WriteLine("Unable to ping system 1");
}

var ipSystem2 = "192.168.100.52"; //"169.254.28.242";
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

// Open a socket to each system
// This will initiate the data transfer from the system to our application.
var sampleRate = 131072 / 2; // This should be manually updated.
var system1StreamingSetup = system1RestfulInterface.Get<DataStreamSetup>(EndPoints.DataStreamSetup);
var system1Streamer = new DataStreamer(ipSystem1, system1StreamingSetup.TCPPort, sampleRate);

var system2StreamingSetup = system1RestfulInterface.Get<DataStreamSetup>(EndPoints.DataStreamSetup);
var system2Streamer = new DataStreamer(ipSystem2, system2StreamingSetup.TCPPort, sampleRate);

Console.WriteLine($"Ready to stream data. Press S to start and C to stop.");
var startKey = Console.ReadKey();
if (startKey.Key != ConsoleKey.S)
{
    Environment.Exit(0);
}

// Save some data.
system1Streamer.StartStreaming();
system2Streamer.StartStreaming();
while (Console.KeyAvailable == false
       && Console.ReadKey().Key != ConsoleKey.C)
{
    Thread.Sleep(10);
}

system1Streamer.StopStreaming();
system2Streamer.StopStreaming();

// Look for the impulse and select a block of data around it.
// We will only use the first channel of each system. System 1 will be our reference.
List<float> referenceSampleList = null;
List<float> syncSampleList = null;

if (ptpSync)
{
    (referenceSampleList, syncSampleList) = PtpDataHandler.GetDataBlock(system1Streamer, system2Streamer);
}
else
{
    (referenceSampleList, syncSampleList) = GpsDataHandler.GetDataBlock(system1Streamer, system2Streamer);
}


float[] referenceSampleArray = null;
float[] syncSampleArray = null;
if (scopePulse)
{
    // Trim the data
    var referenceFirstPeak = referenceSampleList.FindIndex(sample => sample > 0.1);
    var syncFirstPeak = syncSampleList.FindIndex(sample => sample > 0.1);
    var firstSampleIndex = -30 + (referenceFirstPeak > syncFirstPeak
        ? syncFirstPeak
        : referenceFirstPeak);

    var referenceLastPeak = referenceSampleList.FindLastIndex(sample => sample < -0.1);
    var syncLastPeak = syncSampleList.FindLastIndex(sample => sample < -0.1);
    var lastSampleIndex = 30 + (referenceLastPeak > syncLastPeak
        ? referenceLastPeak
        : syncLastPeak);

    // Some checks
    if (firstSampleIndex < 0)
    {
        firstSampleIndex = 0;
    }

    if (lastSampleIndex >= syncSampleList.Count())
    {
        lastSampleIndex = syncSampleList.Count() - 1;
    }

    referenceSampleArray = new float[lastSampleIndex - firstSampleIndex + 1];
    syncSampleArray = new float[lastSampleIndex - firstSampleIndex + 1];

    referenceSampleList.CopyTo(firstSampleIndex, referenceSampleArray, 0, referenceSampleArray.Length);
    syncSampleList.CopyTo(firstSampleIndex, syncSampleArray, 0, syncSampleArray.Length);
}
else 
{
    referenceSampleArray = referenceSampleList.ToArray();
    syncSampleArray = syncSampleList.ToArray();
}

// Display the data block
var thread = new Thread(() =>
{
    var plot = new ScottPlot.Plot(1200, 800);
    plot.AddSignal(referenceSampleArray, sampleRate, label: "Reference Channel");
    plot.AddSignal(syncSampleArray, sampleRate, label: "Synchronized Channel");
    plot.Legend();

    plot.Title("A common input signal measured by two systems with a GPS synchronization enabled");
    plot.XLabel("Time in seconds (s)");
    plot.YLabel("Voltage (V)");

    plot.SaveFig("result.png");
    new ScottPlot.FormsPlotViewer(plot).ShowDialog();
});

thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();

int CheckTimeDifference(SystemTime system1Time, SystemTime system2Time)
{
    var dateTimeSystem1 = new DateTime(system1Time.Year, system1Time.Month, system1Time.Day, system1Time.Hour, system1Time.Minutes, system1Time.Seconds);
    var dateTimeSystem2 = new DateTime(system2Time.Year, system2Time.Month, system2Time.Day, system2Time.Hour, system2Time.Minutes, system2Time.Seconds);
    return Math.Abs((int)(dateTimeSystem1 - dateTimeSystem2).TotalSeconds);
}

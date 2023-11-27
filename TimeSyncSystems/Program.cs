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
using QProtocol.InternalModules.WSB;
using System.Reflection;
using TimeSyncSystems;

Console.WriteLine($"Mecalc Time Sync Systems {Assembly.GetExecutingAssembly().GetName().Version}");

// First connect to the separate systems
var ipSystem1 = "192.168.100.26";
var system1RestfulInterface = new RestfulInterface($"http://{ipSystem1}:8080");
var pingResponse = system1RestfulInterface.Get<InfoPing>(EndPoints.InfoPing);
if (pingResponse == null || pingResponse.Code < 0)
{
    Console.WriteLine("Unable to ping system 1");
}

var ipSystem2 = "";
var system2RestfulInterface = new RestfulInterface($"http://{ipSystem2}:8080");
pingResponse = system2RestfulInterface.Get<InfoPing>(EndPoints.InfoPing);
if (pingResponse == null || pingResponse.Code < 0)
{
    Console.WriteLine("Unable to ping system 2");
}

// The following section is used to set the system clocks.
// After the command has been set, the systems have to reboot and then the test can start.
var setTime = true;
if (setTime)
{
    var time = SystemTime.GetPresentTime();
    system1RestfulInterface.Put(EndPoints.SystemTime, time);
    system2RestfulInterface.Put(EndPoints.SystemTime, time);
    Console.WriteLine("Please reboot both system and wait for the booting process to complete. Press any key to continue.");
    Console.ReadKey();
}

// Do a standard configuration.
// As explained before, this example is dependent on WSB42X6 Modules. If other Module types are used then this section.
// needs to be updated with the appropriate setup.
var system1Items = Item.CreateList(system1RestfulInterface);
var controller = (Controller)system1Items.First();
var controllerSettings = controller.GetItemSettings<Controller.EnabledSettings>();
controllerSettings.Settings.MasterSamplingRate = Controller.MasterSamplingRate._131072Hz;
controllerSettings.Settings.AnalogDataStreamingFormat = Controller.AnalogDataStreamingFormat.Raw;
controller.PutItemSettings(controllerSettings);

var system1Modules = system1Items.OfType<WSB42X6Module>().ToList();
var system1Channels = system1Items.OfType<WSB42X6Channel>().ToList();
if (system1Modules == null || system1Channels == null
    || system1Modules.Count < 1 || system1Channels.Count < 1)
{
    throw new InvalidOperationException("Unable to find the required Modules or Channels in system 1");
}

foreach (var module in system1Modules)
{
    module.PutItemOperationMode(WSB42X6Module.OperationMode.Enabled);
    var moduleSettings = module.GetItemSettings<WSB42X6Module.EnabledSettings>();
    moduleSettings.Settings.SampleRate = WSB42X6Module.SampleRate.MsrDivideBy2;
    module.PutItemSettings(moduleSettings);
}

foreach (var channel in system1Channels)
{
    channel.PutItemOperationMode(WSB42X6Channel.OperationMode.VoltageInput);
    var channelSettings = channel.GetItemSettings<WSB42X6Channel.VoltageInputSettings>();
    channelSettings.Settings.VoltageRange = WSB42X6Channel.VoltageRange._1V;
    channelSettings.Settings.VoltageInputCoupling = WSB42X6Channel.VoltageInputCoupling.AcWith1HzFilter;
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

var system2Modules = system2Items.OfType<WSB42X6Module>().ToList();
var system2Channels = system2Items.OfType<WSB42X6Channel>().ToList();
if (system2Modules == null || system2Channels == null
    || system2Modules.Count < 1 || system2Channels.Count < 1)
{
    throw new InvalidOperationException("Unable to find the required Modules or Channels in system 1");
}

foreach (var module in system2Modules)
{
    module.PutItemOperationMode(WSB42X6Module.OperationMode.Enabled);
    var moduleSettings = module.GetItemSettings<WSB42X6Module.EnabledSettings>();
    moduleSettings.Settings.SampleRate = WSB42X6Module.SampleRate.MsrDivideBy2;
    module.PutItemSettings(moduleSettings);
}

foreach (var channel in system2Channels)
{
    channel.PutItemOperationMode(WSB42X6Channel.OperationMode.VoltageInput);
    var channelSettings = channel.GetItemSettings<WSB42X6Channel.VoltageInputSettings>();
    channelSettings.Settings.VoltageRange = WSB42X6Channel.VoltageRange._1V;
    channelSettings.Settings.VoltageInputCoupling = WSB42X6Channel.VoltageInputCoupling.AcWith1HzFilter;
    channelSettings.Data.StreamingState = Generic.Status.Enabled;
    if (channelSettings.Data.LocalStorage != null)
    {
        channelSettings.Data.LocalStorage = Generic.Status.Disabled;
    }

    channel.PutItemSettings(channelSettings);
}

system1RestfulInterface.Put(EndPoints.SystemSettingsApply);
system2RestfulInterface.Put(EndPoints.SystemSettingsApply);

// Open a socket to each system
// This will initiate the data transfer from the system to our application.
var system1StreamingSetup = system1RestfulInterface.Get<DataStreamSetup>(EndPoints.DataStreamSetup);
var system1Streamer = new DataStreamer(ipSystem1, system1StreamingSetup.TCPPort);

var system2StreamingSetup = system1RestfulInterface.Get<DataStreamSetup>(EndPoints.DataStreamSetup);
var system2Streamer = new DataStreamer(ipSystem2, system2StreamingSetup.TCPPort);

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
var referenceChannelId = system1Channels.First();
var referencePackets = system1Streamer.AnalogDataPackets
                                      .Where(packet => packet.GenericChannelHeader.ChannelId == referenceChannelId.ItemId)
                                      .ToList();

var syncChannelId = system2Channels.First();
var syncPackets = system2Streamer.AnalogDataPackets
                                 .Where(packet => packet.GenericChannelHeader.ChannelId == syncChannelId.ItemId)
                                 .ToList();

var activityTimeStamp = 0ul;
var referenceSampleList = new List<float>();
for (int index = 0; index < referencePackets.Count; index++)
{
    var packet = referencePackets[index];
    if (packet.AnalogChannelHeader.Max < 0.5)
    {
        continue;
    }

    // Found a peak, same a few blocks of data.
    activityTimeStamp = packet.GenericChannelHeader.Timestamp;
    for (; index < index + 32; index++)
    {
        referenceSampleList.AddRange(referencePackets[index].SampleList);
    }

    break;
}

// Now find the same timestamp in system 2, and save the block of data.
var syncSampleList = new List<float>();
for (int index = 0; index < syncPackets.Count; index++)
{
    if (syncPackets[index].GenericChannelHeader.Timestamp == activityTimeStamp)
    {
        for (; index < index + 32; index++)
        {
            syncSampleList.AddRange(syncPackets[index].SampleList);
        }
    }
}

// Display the data block
var plot = new ScottPlot.Plot(600, 400);
plot.AddSignal(referenceSampleList.ToArray(), 131072 / 2, label: "Reference Channel");
plot.AddSignal(syncSampleList.ToArray(), 131072 / 2, label: "Synchronized Channel");
plot.Legend();

new ScottPlot.WpfPlotViewer(plot).ShowDialog();

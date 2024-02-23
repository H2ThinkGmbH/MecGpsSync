using ConfigureSystem;
using QClient.RestfulClient;
using QProtocol.Advanced;
using QProtocol.Controllers;
using QProtocol.GenericDefines;
using QProtocol.InternalChannels.XMC237;
using QProtocol.InternalModules.ICS;

Console.WriteLine("Looking for QServer Devices");
var ipList = GetIpAddresses.UsingQDeviceDiscovery();
if (ipList.Count > 0)
{
    Console.WriteLine($"Found {ipList.Count} devices");
}
else
{
    Console.WriteLine("No devices found!");
    Console.WriteLine("Press any key to exit.");
    Console.ReadKey();
    Environment.Exit(-1);
}

foreach (var ipAddress in ipList)
{
    Console.WriteLine($"Configuring {ipAddress}");
    var restfulInterface = new RestfulInterface($"http://{ipAddress}:8080");
    var itemList = Item.CreateList(restfulInterface);
    var controller = (Controller)itemList.First();
    var controllerSettings = controller.GetItemSettings<Controller.EnabledSettings>();
    controllerSettings.Settings.MasterSamplingRate = Controller.MasterSamplingRate._131072Hz;
    controllerSettings.Settings.AnalogDataStreamingFormat = Controller.AnalogDataStreamingFormat.Raw;
    controller.PutItemSettings(controllerSettings);

    var modules = itemList.OfType<ICS425Module>().ToList();
    var channels = itemList.OfType<ICS425Channel>().ToList();
    if (modules == null || channels == null
        || modules.Count < 1 || channels.Count < 1)
    {
        throw new InvalidOperationException("Unable to find the required Modules or Channels in system 1");
    }

    foreach (var module in modules)
    {
        module.PutItemOperationMode(ICS425Module.OperationMode.Enabled);
        var moduleSettings = module.GetItemSettings<ICS425Module.EnabledSettings>();
        moduleSettings.Settings.SampleRate = ICS425Module.SampleRate.MsrDivideBy2;
        module.PutItemSettings(moduleSettings);
    }

    foreach (var channel in channels)
    {
        channel.PutItemOperationMode(ICS425Channel.OperationMode.IcpInput);
        var channelSettings = channel.GetItemSettings<ICS425Channel.IcpInputSettings>();
        channelSettings.Settings.VoltageRange = ICS425Channel.VoltageRange._1V;
        channelSettings.Settings.IcpInputCoupling = ICS425Channel.IcpInputCoupling.AcWith1HzFilter;
        channelSettings.Settings.InputBiasing = ICS425Channel.InputBiasing.SingleEnded;
        channelSettings.Data.StreamingState = Generic.Status.Enabled;
        if (channelSettings.Data.LocalStorage != null)
        {
            channelSettings.Data.LocalStorage = Generic.Status.Disabled;
        }

        channel.PutItemSettings(channelSettings);
    }

    var xmc237Module = itemList.OfType<XMC237Module>().First();
    xmc237Module.PutItemOperationMode(XMC237Module.OperationMode.Enabled);

    var xmc237GpsChannel = xmc237Module.Children.OfType<XMC237GpsChannel>().First();
    xmc237GpsChannel.PutItemOperationMode(XMC237GpsChannel.OperationMode.Enabled);
    var gpsChannelSettings = xmc237GpsChannel.GetItemSettings<XMC237GpsChannel.EnabledSettings>();
    gpsChannelSettings.Settings.MessageRate = XMC237GpsChannel.MessageRate._1Hz;
    gpsChannelSettings.Data.StreamingState = Generic.Status.Enabled;
    xmc237GpsChannel.PutItemSettings(gpsChannelSettings);

    if (xmc237Module.Children.Any(item => item.ItemNameIdentifier == (int)Types.ChannelType.XMC237Icp))
    {
        var xmc237IcpChannel = xmc237Module.Children.OfType<XMC237IcpChannel>().First();
        xmc237IcpChannel.PutItemOperationMode(XMC237IcpChannel.OperationMode.Disabled);
    }

    Console.WriteLine($"Appling settings");
    restfulInterface.Put(EndPoints.SystemSettingsApply);
}

Console.WriteLine("Done");
Console.WriteLine("Press any key to exit.");
Console.ReadKey();
using QClient.RestfulClient;
using QProtocol.Advanced;
using QProtocol.GenericDefines;
using QProtocol.InternalModules.ICS;
using StreamAndSaveData;
using System.Diagnostics;

var ipAddress = args[0];
var sampleRate = 131072 / 2;
var systemRestfulInterface = new RestfulInterface($"http://{ipAddress}:8080");
Console.WriteLine($"Connecting to {ipAddress}");

var channelList = Item.CreateList(systemRestfulInterface)
                      .OfType<ICS425Channel>()
                      .ToList();

var streamingChannels = new List<int>();
foreach (var channel in channelList)
{
    if (channel.GetItemOperationMode() == ICS425Channel.OperationMode.Disabled)
    {
        continue;
    }

    var settings = channel.GetItemSettings().ConvertToData();
    if (settings.StreamingState == Generic.Status.Enabled)
    {
        streamingChannels.Add(channel.ItemId);
    }
}

WriteTextToTop($"Start streaming...");
var dataWriter = new DataWriter(Path.Combine(Directory.GetCurrentDirectory(), $"{DateTime.Now:yyyyMMdd HHmmss}"), sampleRate);
var dataStreamer = new DataStreamer(systemRestfulInterface, streamingChannels);
dataStreamer.StartStreaming();

try
{
    var stopwatch = Stopwatch.StartNew();
    var timer = Stopwatch.StartNew();
    while (true)
    {
        if (Console.KeyAvailable &&
            Console.ReadKey().Key == ConsoleKey.C)
        {
            break;
        }

        if (stopwatch.ElapsedMilliseconds > 1000)
        {
            stopwatch.Restart();
            if (dataStreamer.IsDataReady())
            {
                dataStreamer.GetAnalogData(dataWriter.SaveAnalogData);
                dataStreamer.GetGpsData(dataWriter.SaveGpsData);
            }
            
            WriteTextToTop($"Runtime: {timer.ElapsedMilliseconds / 1000.0} s");
        }
    }
}
finally
{
    dataStreamer.StopStreaming();
}

void WriteTextToTop(string text)
{
    Console.Clear();
    Console.SetCursorPosition(0, 0);
    Console.WriteLine(text);
}
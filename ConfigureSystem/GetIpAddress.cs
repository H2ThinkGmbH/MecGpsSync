using System.Diagnostics;
using System.Text.Json;

namespace ConfigureSystem;

public class GetIpAddresses
{
    public static List<string> UsingQDeviceDiscovery()
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine("Libraries", "QDeviceDiscovery.exe"),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        var ipList = new List<string>();
        var jsonData = JsonSerializer.Deserialize<DeviceInfo>(output);
        for (int i = 0; i < jsonData.numberOfentries; i++) 
        {
            ipList.Add(jsonData.entries[i].deviceIp.ToString());
        }

        return ipList;
    }
}

public class DeviceEntry
{
    public string deviceName { get; set; }

    public string deviceIp { get; set; }
}

public class DeviceInfo
{
    public int numberOfentries { get; set; }

    public List<DeviceEntry> entries { get; set; }
}

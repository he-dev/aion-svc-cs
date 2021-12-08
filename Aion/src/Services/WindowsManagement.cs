using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.Versioning;

namespace Aion.Services;

internal class WindowsManagement
{
    [SupportedOSPlatform("windows")]
    public static IEnumerable<string> GetCommandLines(string processName)
    {
        if (!processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }
        
        var query = $"SELECT CommandLine FROM Win32_Process WHERE Name = '{processName}'";
        using var searcher = new ManagementObjectSearcher(query);
        using var results = searcher.Get();
        foreach (var instance in results)
        {
            if (instance["CommandLine"] is string commandLine)
            {
                yield return commandLine;
            }
        }
    }
    
    [SupportedOSPlatform("windows")]
    public static bool IsRunning(string processName, string arguments)
    {
        return GetCommandLines(processName).Any(cl => cl.EndsWith(arguments, StringComparison.OrdinalIgnoreCase));
    }
}
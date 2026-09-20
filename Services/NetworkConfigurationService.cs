using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace IpForge.Services;

public sealed class NetworkConfigurationService
{
    public async Task SetDhcpAsync(string adapterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterName);

        await RunNetshAsync(
            "interface",
            "ipv4",
            "set",
            "address",
            $"name={adapterName}",
            "source=dhcp");

        await RunNetshAsync(
            "interface",
            "ipv4",
            "set",
            "dnsservers",
            $"name={adapterName}",
            "source=dhcp");
    }

    public async Task SetStaticAsync(
        string adapterName,
        string ipAddress,
        string subnetMask,
        string gateway,
        string primaryDns,
        string secondaryDns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterName);
        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(subnetMask);

        string gatewayArgument = string.IsNullOrWhiteSpace(gateway)
            ? "gateway=none"
            : $"gateway={gateway}";

        await RunNetshAsync(
            "interface",
            "ipv4",
            "set",
            "address",
            $"name={adapterName}",
            "source=static",
            $"address={ipAddress}",
            $"mask={subnetMask}",
            gatewayArgument,
            "store=persistent");

        if (string.IsNullOrWhiteSpace(primaryDns))
        {
            return;
        }

        await RunNetshAsync(
            "interface",
            "ipv4",
            "set",
            "dnsservers",
            $"name={adapterName}",
            "source=static",
            $"address={primaryDns}",
            "validate=no");

        if (string.IsNullOrWhiteSpace(secondaryDns))
        {
            return;
        }

        await RunNetshAsync(
            "interface",
            "ipv4",
            "add",
            "dnsservers",
            $"name={adapterName}",
            $"address={secondaryDns}",
            "index=2",
            "validate=no");
    }

    private static async Task RunNetshAsync(params string[] arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "netsh.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new()
        {
            StartInfo = startInfo
        };

        process.Start();

        Task<string> outputTask =
            process.StandardOutput.ReadToEndAsync();

        Task<string> errorTask =
            process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        string output = await outputTask;
        string error = await errorTask;

        if (process.ExitCode == 0)
        {
            return;
        }

        string message = string.IsNullOrWhiteSpace(error)
            ? output
            : error;

        if (string.IsNullOrWhiteSpace(message))
        {
            message = "Windows could not apply the network configuration.";
        }

        throw new InvalidOperationException(message.Trim());
    }
}
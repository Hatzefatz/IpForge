using IpForge.Models;
using IpForge.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace IpForge.Views;

public sealed partial class AdapterPage : Page
{
    private readonly PresetService _presetService = new();

    private readonly NetworkConfigurationService
        _networkConfigurationService = new();

    private NetworkInterface[] _adapters = [];
    private List<NetworkPreset> _presets = [];

    private bool _presetsLoaded;

    public AdapterPage()
    {
        InitializeComponent();
        LoadAdapters();
    }

    private async void AdapterPage_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_presetsLoaded)
        {
            return;
        }

        _presetsLoaded = true;

        try
        {
            _presets = await _presetService.LoadAsync();
            PresetComboBox.ItemsSource = _presets;
        }
        catch
        {
            await ShowMessageDialogAsync(
                "Could not load presets",
                "The preset file could not be read.",
                InfoBarSeverity.Error);
        }
    }

    private void LoadAdapters(
    string? selectedAdapterId = null)
    {
        _adapters = NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(adapter =>
                adapter.NetworkInterfaceType !=
                    NetworkInterfaceType.Loopback &&
                adapter.NetworkInterfaceType !=
                    NetworkInterfaceType.Tunnel)
            .OrderByDescending(adapter =>
                adapter.OperationalStatus ==
                    OperationalStatus.Up)
            .ThenBy(adapter => adapter.Name)
            .ToArray();

        AdapterComboBox.ItemsSource = _adapters;

        if (selectedAdapterId is null)
        {
            return;
        }

        NetworkInterface? selectedAdapter =
            _adapters.FirstOrDefault(adapter =>
                adapter.Id == selectedAdapterId);

        if (selectedAdapter is not null)
        {
            AdapterComboBox.SelectedItem = selectedAdapter;
        }
    }
    private void RefreshButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        string? selectedAdapterId =
            (AdapterComboBox.SelectedItem as NetworkInterface)?.Id;

        LoadAdapters(selectedAdapterId);
    }
    private void AdapterComboBox_SelectionChanged(
    object sender,
    SelectionChangedEventArgs e)
    {
        NotificationInfoBar.IsOpen = false;

        if (AdapterComboBox.SelectedItem is not
            NetworkInterface adapter)
        {
            ClearConfiguration();
            return;
        }

        DisplayAdapterConfiguration(adapter);

        ResetButton.IsEnabled = true;
        ApplyConfigurationButton.IsEnabled = true;
    }

    private void DisplayAdapterConfiguration(
        NetworkInterface adapter)
    {
        PresetComboBox.SelectedItem = null;

        IPInterfaceProperties properties =
            adapter.GetIPProperties();

        IPv4InterfaceProperties? ipv4Properties =
            properties.GetIPv4Properties();

        UnicastIPAddressInformation? ipv4Address =
            properties.UnicastAddresses.FirstOrDefault(address =>
                address.Address.AddressFamily ==
                    AddressFamily.InterNetwork);

        GatewayIPAddressInformation? gateway =
            properties.GatewayAddresses.FirstOrDefault(address =>
                address.Address.AddressFamily ==
                    AddressFamily.InterNetwork);

        string[] dnsServers = properties
            .DnsAddresses
            .Where(address =>
                address.AddressFamily ==
                    AddressFamily.InterNetwork)
            .Select(address => address.ToString())
            .ToArray();

        bool dhcpEnabled =
            ipv4Properties?.IsDhcpEnabled ?? false;

        StatusTextBlock.Text =
            adapter.OperationalStatus.ToString();

        ModeTextBlock.Text =
            dhcpEnabled ? "DHCP" : "Static";

        IpAddressTextBlock.Text =
            ipv4Address?.Address.ToString() ?? "—";

        SubnetMaskTextBlock.Text =
            ipv4Address?.IPv4Mask.ToString() ?? "—";

        GatewayTextBlock.Text =
            gateway?.Address.ToString() ?? "—";

        DnsTextBlock.Text = dnsServers.Length > 0
            ? string.Join(", ", dnsServers)
            : "—";

        DhcpToggleSwitch.IsOn = dhcpEnabled;

        IpAddressTextBox.Text =
            ipv4Address?.Address.ToString() ??
            string.Empty;

        SubnetMaskTextBox.Text =
            ipv4Address?.IPv4Mask.ToString() ??
            string.Empty;

        GatewayTextBox.Text =
            gateway?.Address.ToString() ??
            string.Empty;

        PrimaryDnsTextBox.Text =
            dnsServers.ElementAtOrDefault(0) ??
            string.Empty;

        SecondaryDnsTextBox.Text =
            dnsServers.ElementAtOrDefault(1) ??
            string.Empty;

        UpdateConfigurationMode();
    }

    private void PresetComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (PresetComboBox.SelectedItem is not
            NetworkPreset preset)
        {
            return;
        }

        DhcpToggleSwitch.IsOn = false;

        IpAddressTextBox.Text = preset.IpAddress;
        SubnetMaskTextBox.Text = preset.SubnetMask;
        GatewayTextBox.Text = preset.Gateway;
        PrimaryDnsTextBox.Text = preset.PrimaryDns;
        SecondaryDnsTextBox.Text = preset.SecondaryDns;

        UpdateConfigurationMode();
    }

    private void DhcpToggleSwitch_Toggled(
        object sender,
        RoutedEventArgs e)
    {
        UpdateConfigurationMode();
    }

    private void UpdateConfigurationMode()
    {
        bool staticModeEnabled =
            !DhcpToggleSwitch.IsOn;

        PresetComboBox.IsEnabled =
            staticModeEnabled;

        IpAddressTextBox.IsEnabled =
            staticModeEnabled;

        SubnetMaskTextBox.IsEnabled =
            staticModeEnabled;

        GatewayTextBox.IsEnabled =
            staticModeEnabled;

        PrimaryDnsTextBox.IsEnabled =
            staticModeEnabled;

        SecondaryDnsTextBox.IsEnabled =
            staticModeEnabled;
    }

    private void ResetButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (AdapterComboBox.SelectedItem is
            NetworkInterface adapter)
        {
            DisplayAdapterConfiguration(adapter);
        }
        else
        {
            ClearConfiguration();
        }
    }

    private async void ApplyConfigurationButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        if (AdapterComboBox.SelectedItem is not
            NetworkInterface adapter)
        {
            await ShowMessageDialogAsync(
                "No adapter selected",
                "Select a network adapter first.");

            return;
        }

        if(!await ValidateConfigurationAsync())
{
            return;
        }

        bool confirmed =
            await ShowConfirmationDialogAsync(adapter);

        if (!confirmed)
        {
            return;
        }

        string adapterId = adapter.Id;
        string adapterName = adapter.Name;
        bool useDhcp = DhcpToggleSwitch.IsOn;

        SetApplyingState(true);

        try
        {
            if (useDhcp)
            {
                await _networkConfigurationService
                    .SetDhcpAsync(adapterName);
            }
            else
            {
                await _networkConfigurationService
                    .SetStaticAsync(
                        adapterName,
                        IpAddressTextBox.Text.Trim(),
                        SubnetMaskTextBox.Text.Trim(),
                        GatewayTextBox.Text.Trim(),
                        PrimaryDnsTextBox.Text.Trim(),
                        SecondaryDnsTextBox.Text.Trim());
            }

            // Give Windows a moment to refresh the adapter state.
            await Task.Delay(1000);

            LoadAdapters(adapterId);

            string mode = useDhcp
                ? "DHCP"
                : "The static IPv4 configuration";

            await ShowMessageDialogAsync(
                "Configuration applied",
                $"{mode} was applied to {adapterName}.",
                InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            await ShowMessageDialogAsync(
                "Configuration failed",
                exception.Message,
                InfoBarSeverity.Error);
        }
        finally
        {
            SetApplyingState(false);
        }
    }
    private static bool AreInSameSubnet(
    string firstAddress,
    string secondAddress,
    string subnetMask)
    {
        uint first = ConvertIpv4ToUInt32(
            IPAddress.Parse(firstAddress));

        uint second = ConvertIpv4ToUInt32(
            IPAddress.Parse(secondAddress));

        uint mask = ConvertIpv4ToUInt32(
            IPAddress.Parse(subnetMask));

        return (first & mask) == (second & mask);
    }

    private static bool IsNetworkOrBroadcastAddress(
        string ipAddress,
        string subnetMask)
    {
        uint address = ConvertIpv4ToUInt32(
            IPAddress.Parse(ipAddress));

        uint mask = ConvertIpv4ToUInt32(
            IPAddress.Parse(subnetMask));

        int prefixLength = CountMaskBits(mask);

        // /31 and /32 networks do not use the traditional
        // network and broadcast address rules.
        if (prefixLength >= 31)
        {
            return false;
        }

        uint networkAddress = address & mask;
        uint broadcastAddress =
            networkAddress | ~mask;

        return address == networkAddress ||
               address == broadcastAddress;
    }

    private static int CountMaskBits(uint mask)
    {
        int count = 0;

        while (mask != 0)
        {
            count += (int)(mask & 1);
            mask >>= 1;
        }

        return count;
    }

    private static uint ConvertIpv4ToUInt32(
        IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();

        return ((uint)bytes[0] << 24) |
               ((uint)bytes[1] << 16) |
               ((uint)bytes[2] << 8) |
               bytes[3];
    }
    private async Task<bool> ValidateConfigurationAsync()
    {
        if (DhcpToggleSwitch.IsOn)
        {
            return true;
        }

        string ipAddress = IpAddressTextBox.Text.Trim();
        string subnetMask = SubnetMaskTextBox.Text.Trim();
        string gateway = GatewayTextBox.Text.Trim();
        string primaryDns = PrimaryDnsTextBox.Text.Trim();
        string secondaryDns = SecondaryDnsTextBox.Text.Trim();

        if (!IsValidIpv4(ipAddress))
        {
            await ShowMessageDialogAsync(
                "Invalid IP address",
                "Enter a valid IPv4 address.");

            return false;
        }

        if (!IsValidSubnetMask(subnetMask))
        {
            await ShowMessageDialogAsync(
                "Invalid subnet mask",
                "Enter a valid IPv4 subnet mask such as 255.255.255.0.");

            return false;
        }

        if (!IsValidOptionalIpv4(gateway))
        {
            await ShowMessageDialogAsync(
                "Invalid gateway",
                "Enter a valid IPv4 gateway or leave it empty.");

            return false;
        }

        if (!IsValidOptionalIpv4(primaryDns))
        {
            await ShowMessageDialogAsync(
                "Invalid primary DNS",
                "Enter a valid IPv4 DNS server or leave it empty.");

            return false;
        }

        if (!IsValidOptionalIpv4(secondaryDns))
        {
            await ShowMessageDialogAsync(
                "Invalid secondary DNS",
                "Enter a valid IPv4 DNS server or leave it empty.");

            return false;
        }

        if (string.IsNullOrWhiteSpace(primaryDns) &&
            !string.IsNullOrWhiteSpace(secondaryDns))
        {
            await ShowMessageDialogAsync(
                "Primary DNS required",
                "Enter a primary DNS server before adding a secondary DNS server.");

            return false;
        }

        if (IsNetworkOrBroadcastAddress(
                ipAddress,
                subnetMask))
        {
            await ShowMessageDialogAsync(
                "Unusable IP address",
                "The IP address is the network or broadcast address for this subnet.");

            return false;
        }

        if (!string.IsNullOrWhiteSpace(gateway) &&
            !AreInSameSubnet(
                ipAddress,
                gateway,
                subnetMask))
        {
            await ShowMessageDialogAsync(
                "Gateway outside subnet",
                "The gateway must be in the same subnet as the IP address.");

            return false;
        }

        if (!string.IsNullOrWhiteSpace(gateway) &&
            IsNetworkOrBroadcastAddress(
                gateway,
                subnetMask))
        {
            await ShowMessageDialogAsync(
                "Invalid gateway",
                "The gateway cannot be the network or broadcast address.");

            return false;
        }

        if (ipAddress == gateway)
        {
            await ShowMessageDialogAsync(
                "Invalid gateway",
                "The gateway cannot be the same as the adapter IP address.");

            return false;
        }

        return true;
    }

    private async Task<bool> ShowConfirmationDialogAsync(
        NetworkInterface adapter)
    {
        string configuration;

        if (DhcpToggleSwitch.IsOn)
        {
            configuration =
                "Mode: DHCP\n" +
                "IP address: Automatic\n" +
                "DNS servers: Automatic";
        }
        else
        {
            configuration =
                $"Mode: Static\n" +
                $"IP address: {IpAddressTextBox.Text.Trim()}\n" +
                $"Subnet mask: {SubnetMaskTextBox.Text.Trim()}\n" +
                $"Gateway: {GetDisplayValue(GatewayTextBox.Text)}\n" +
                $"Primary DNS: {GetDisplayValue(PrimaryDnsTextBox.Text)}\n" +
                $"Secondary DNS: {GetDisplayValue(SecondaryDnsTextBox.Text)}";
        }

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "Apply network configuration?",
            Content =
                $"Adapter: {adapter.Name}\n\n" +
                $"{configuration}\n\n" +
                "The network connection may be interrupted.",
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result =
            await dialog.ShowAsync();

        return result ==
            ContentDialogResult.Primary;
    }

    private static string GetDisplayValue(
        string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Not set"
            : value.Trim();
    }

    private static bool IsValidIpv4(
        string value)
    {
        return IPAddress.TryParse(
                   value.Trim(),
                   out IPAddress? address) &&
               address.AddressFamily ==
                   AddressFamily.InterNetwork;
    }

    private static bool IsValidOptionalIpv4(
        string value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               IsValidIpv4(value);
    }

    private static bool IsValidSubnetMask(
        string value)
    {
        if (!IPAddress.TryParse(
                value.Trim(),
                out IPAddress? address) ||
            address.AddressFamily !=
                AddressFamily.InterNetwork)
        {
            return false;
        }

        byte[] bytes = address.GetAddressBytes();
        bool zeroFound = false;

        foreach (byte currentByte in bytes)
        {
            for (int bit = 7; bit >= 0; bit--)
            {
                bool bitIsSet =
                    (currentByte & (1 << bit)) != 0;

                if (!bitIsSet)
                {
                    zeroFound = true;
                }
                else if (zeroFound)
                {
                    // A valid mask cannot contain a 1 after a 0.
                    return false;
                }
            }
        }

        return true;
    }

    private Task ShowMessageDialogAsync(
    string title,
    string message,
    InfoBarSeverity severity = InfoBarSeverity.Warning)
    {
        NotificationInfoBar.Title = title;
        NotificationInfoBar.Message = message;
        NotificationInfoBar.Severity = severity;
        NotificationInfoBar.IsOpen = true;

        return Task.CompletedTask;
    }
    private void SetApplyingState(bool isApplying)
    {
        bool adapterSelected =
            AdapterComboBox.SelectedItem is NetworkInterface;

        AdapterComboBox.IsEnabled = !isApplying;
        RefreshButton.IsEnabled = !isApplying;

        ResetButton.IsEnabled =
            !isApplying && adapterSelected;

        ApplyConfigurationButton.IsEnabled =
            !isApplying && adapterSelected;

        DhcpToggleSwitch.IsEnabled = !isApplying;
        PresetComboBox.IsEnabled = !isApplying;
        IpAddressTextBox.IsEnabled = !isApplying;
        SubnetMaskTextBox.IsEnabled = !isApplying;
        GatewayTextBox.IsEnabled = !isApplying;
        PrimaryDnsTextBox.IsEnabled = !isApplying;
        SecondaryDnsTextBox.IsEnabled = !isApplying;

        ApplyingProgressRing.IsActive = isApplying;

        ApplyingProgressRing.Visibility = isApplying
            ? Visibility.Visible
            : Visibility.Collapsed;

        ApplyingStatusTextBlock.Visibility = isApplying
            ? Visibility.Visible
            : Visibility.Collapsed;

        ApplyConfigurationButton.Content = isApplying
            ? "Applying..."
            : "Apply configuration";

        if (!isApplying)
        {
            UpdateConfigurationMode();
        }
    }
    private void ClearConfiguration()
    {
        PresetComboBox.SelectedItem = null;

        StatusTextBlock.Text = "—";
        ModeTextBlock.Text = "—";
        IpAddressTextBlock.Text = "—";
        SubnetMaskTextBlock.Text = "—";
        GatewayTextBlock.Text = "—";
        DnsTextBlock.Text = "—";

        DhcpToggleSwitch.IsOn = false;

        IpAddressTextBox.Text = string.Empty;
        SubnetMaskTextBox.Text = string.Empty;
        GatewayTextBox.Text = string.Empty;
        PrimaryDnsTextBox.Text = string.Empty;
        SecondaryDnsTextBox.Text = string.Empty;
        ResetButton.IsEnabled = false;
        ApplyConfigurationButton.IsEnabled = false;
        UpdateConfigurationMode();
    }
}
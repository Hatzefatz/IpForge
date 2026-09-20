using IpForge.Models;
using IpForge.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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
                "The preset file could not be read.");
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
        AdapterComboBox.DisplayMemberPath =
            nameof(NetworkInterface.Name);

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
        if (AdapterComboBox.SelectedItem is not
            NetworkInterface adapter)
        {
            ClearConfiguration();
            return;
        }

        DisplayAdapterConfiguration(adapter);
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

        if (!ValidateConfiguration())
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
                $"{mode} was applied to {adapterName}.");
        }
        catch (Exception exception)
        {
            await ShowMessageDialogAsync(
                "Configuration failed",
                exception.Message);
        }
        finally
        {
            SetApplyingState(false);
        }
    }

    private bool ValidateConfiguration()
    {
        if (DhcpToggleSwitch.IsOn)
        {
            return true;
        }

        if (!IsValidIpv4(IpAddressTextBox.Text))
        {
            _ = ShowMessageDialogAsync(
                "Invalid IP address",
                "Enter a valid IPv4 address.");

            return false;
        }

        if (!IsValidSubnetMask(SubnetMaskTextBox.Text))
        {
            _ = ShowMessageDialogAsync(
                "Invalid subnet mask",
                "Enter a valid IPv4 subnet mask.");

            return false;
        }

        if (!IsValidOptionalIpv4(GatewayTextBox.Text))
        {
            _ = ShowMessageDialogAsync(
                "Invalid gateway",
                "Enter a valid IPv4 gateway or leave it empty.");

            return false;
        }

        if (!IsValidOptionalIpv4(PrimaryDnsTextBox.Text))
        {
            _ = ShowMessageDialogAsync(
                "Invalid primary DNS",
                "Enter a valid IPv4 DNS server or leave it empty.");

            return false;
        }

        if (!IsValidOptionalIpv4(SecondaryDnsTextBox.Text))
        {
            _ = ShowMessageDialogAsync(
                "Invalid secondary DNS",
                "Enter a valid IPv4 DNS server or leave it empty.");

            return false;
        }

        if (string.IsNullOrWhiteSpace(
                PrimaryDnsTextBox.Text) &&
            !string.IsNullOrWhiteSpace(
                SecondaryDnsTextBox.Text))
        {
            _ = ShowMessageDialogAsync(
                "Primary DNS required",
                "Enter a primary DNS server before adding a secondary DNS server.");

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

    private async Task ShowMessageDialogAsync(
        string title,
        string message)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "OK"
        };

        await dialog.ShowAsync();
    }
    private void SetApplyingState(bool isApplying)
    {
        AdapterComboBox.IsEnabled = !isApplying;
        RefreshButton.IsEnabled = !isApplying;
        ResetButton.IsEnabled = !isApplying;
        ApplyConfigurationButton.IsEnabled = !isApplying;

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

        UpdateConfigurationMode();
    }
}
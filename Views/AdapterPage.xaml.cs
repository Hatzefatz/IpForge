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
            await ShowDialogAsync(
                "Could not load presets",
                "The preset file could not be read.");
        }
    }

    private void LoadAdapters()
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
    }

    private void AdapterComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (AdapterComboBox.SelectedItem is not NetworkInterface adapter)
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

        UnicastIPAddressInformation? ipv4Address = properties
            .UnicastAddresses
            .FirstOrDefault(address =>
                address.Address.AddressFamily ==
                    AddressFamily.InterNetwork);

        GatewayIPAddressInformation? gateway = properties
            .GatewayAddresses
            .FirstOrDefault(address =>
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
        if (PresetComboBox.SelectedItem is not NetworkPreset preset)
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
        if (AdapterComboBox.SelectedItem is NetworkInterface adapter)
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
        if (AdapterComboBox.SelectedItem is not NetworkInterface)
        {
            await ShowDialogAsync(
                "No adapter selected",
                "Select a network adapter first.");

            return;
        }

        if (!DhcpToggleSwitch.IsOn)
        {
            if (!IsValidIpv4(IpAddressTextBox.Text))
            {
                await ShowDialogAsync(
                    "Invalid IP address",
                    "Enter a valid IPv4 address.");

                return;
            }

            if (!IsValidIpv4(SubnetMaskTextBox.Text))
            {
                await ShowDialogAsync(
                    "Invalid subnet mask",
                    "Enter a valid IPv4 subnet mask.");

                return;
            }

            if (!IsValidOptionalIpv4(GatewayTextBox.Text))
            {
                await ShowDialogAsync(
                    "Invalid gateway",
                    "Enter a valid IPv4 gateway or leave it empty.");

                return;
            }

            if (!IsValidOptionalIpv4(PrimaryDnsTextBox.Text))
            {
                await ShowDialogAsync(
                    "Invalid primary DNS",
                    "Enter a valid IPv4 DNS server or leave it empty.");

                return;
            }

            if (!IsValidOptionalIpv4(SecondaryDnsTextBox.Text))
            {
                await ShowDialogAsync(
                    "Invalid secondary DNS",
                    "Enter a valid IPv4 DNS server or leave it empty.");

                return;
            }
        }

        await ShowDialogAsync(
            "Configuration valid",
            "The configuration is valid. Applying changes will be added next.");
    }

    private static bool IsValidIpv4(string value)
    {
        return IPAddress.TryParse(
                   value,
                   out IPAddress? address) &&
               address.AddressFamily ==
                   AddressFamily.InterNetwork;
    }

    private static bool IsValidOptionalIpv4(string value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               IsValidIpv4(value);
    }

    private async Task ShowDialogAsync(
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
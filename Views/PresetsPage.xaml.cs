using IpForge.Models;
using IpForge.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace IpForge.Views;

public sealed partial class PresetsPage : Page
{
    private readonly PresetService _presetService = new();
    private readonly ObservableCollection<NetworkPreset> _presets = [];

    private NetworkPreset? _selectedPreset;
    private bool _isLoaded;

    public PresetsPage()
    {
        InitializeComponent();
        PresetListView.ItemsSource = _presets;
    }

    private async void PresetsPage_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;

        try
        {
            var savedPresets = await _presetService.LoadAsync();

            foreach (NetworkPreset preset in savedPresets)
            {
                _presets.Add(preset);
            }
        }
        catch
        {
            await ShowMessageAsync(
                "Could not load presets",
                "The preset file could not be read.");
        }
    }

    private void PresetListView_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (PresetListView.SelectedItem is not NetworkPreset preset)
        {
            return;
        }

        _selectedPreset = preset;

        EditorTitleTextBlock.Text = "Edit preset";
        DeletePresetButton.IsEnabled = true;

        PresetNameTextBox.Text = preset.Name;
        IpAddressTextBox.Text = preset.IpAddress;
        SubnetMaskTextBox.Text = preset.SubnetMask;
        GatewayTextBox.Text = preset.Gateway;
        PrimaryDnsTextBox.Text = preset.PrimaryDns;
        SecondaryDnsTextBox.Text = preset.SecondaryDns;
    }

    private void NewPresetButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        PresetListView.SelectedItem = null;
        _selectedPreset = null;

        EditorTitleTextBlock.Text = "New preset";
        DeletePresetButton.IsEnabled = false;

        ClearEditor();
        PresetNameTextBox.Focus(FocusState.Programmatic);
    }

    private async void SavePresetButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PresetNameTextBox.Text))
        {
            await ShowMessageAsync(
                "Preset name required",
                "Enter a name for the preset.");

            return;
        }

        if (!IsValidIpv4(IpAddressTextBox.Text))
        {
            await ShowMessageAsync(
                "Invalid IP address",
                "Enter a valid IPv4 address.");

            return;
        }

        if (!IsValidIpv4(SubnetMaskTextBox.Text))
        {
            await ShowMessageAsync(
                "Invalid subnet mask",
                "Enter a valid IPv4 subnet mask.");

            return;
        }

        if (!IsValidOptionalIpv4(GatewayTextBox.Text))
        {
            await ShowMessageAsync(
                "Invalid gateway",
                "Enter a valid IPv4 gateway or leave it empty.");

            return;
        }

        if (!IsValidOptionalIpv4(PrimaryDnsTextBox.Text))
        {
            await ShowMessageAsync(
                "Invalid primary DNS",
                "Enter a valid IPv4 DNS server or leave it empty.");

            return;
        }

        if (!IsValidOptionalIpv4(SecondaryDnsTextBox.Text))
        {
            await ShowMessageAsync(
                "Invalid secondary DNS",
                "Enter a valid IPv4 DNS server or leave it empty.");

            return;
        }

        NetworkPreset preset = new()
        {
            Id = _selectedPreset?.Id ?? System.Guid.NewGuid(),
            Name = PresetNameTextBox.Text.Trim(),
            IpAddress = IpAddressTextBox.Text.Trim(),
            SubnetMask = SubnetMaskTextBox.Text.Trim(),
            Gateway = GatewayTextBox.Text.Trim(),
            PrimaryDns = PrimaryDnsTextBox.Text.Trim(),
            SecondaryDns = SecondaryDnsTextBox.Text.Trim()
        };

        if (_selectedPreset is null)
        {
            _presets.Add(preset);
        }
        else
        {
            int selectedIndex = _presets.IndexOf(_selectedPreset);

            if (selectedIndex >= 0)
            {
                _presets[selectedIndex] = preset;
            }
        }

        _selectedPreset = preset;
        PresetListView.SelectedItem = preset;

        try
        {
            await _presetService.SaveAsync(_presets);

            await ShowMessageAsync(
                "Preset saved",
                $"The preset \"{preset.Name}\" was saved.");
        }
        catch
        {
            await ShowMessageAsync(
                "Could not save preset",
                "The preset file could not be written.");
        }
    }

    private async void DeletePresetButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_selectedPreset is null)
        {
            return;
        }

        ContentDialog confirmationDialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "Delete preset?",
            Content = $"Delete \"{_selectedPreset.Name}\"?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };

        ContentDialogResult result =
            await confirmationDialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        _presets.Remove(_selectedPreset);
        _selectedPreset = null;
        PresetListView.SelectedItem = null;

        ClearEditor();
        EditorTitleTextBlock.Text = "New preset";
        DeletePresetButton.IsEnabled = false;

        try
        {
            await _presetService.SaveAsync(_presets);
        }
        catch
        {
            await ShowMessageAsync(
                "Could not save changes",
                "The preset file could not be written.");
        }
    }

    private void ClearEditor()
    {
        PresetNameTextBox.Text = string.Empty;
        IpAddressTextBox.Text = string.Empty;
        SubnetMaskTextBox.Text = string.Empty;
        GatewayTextBox.Text = string.Empty;
        PrimaryDnsTextBox.Text = string.Empty;
        SecondaryDnsTextBox.Text = string.Empty;
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

    private async Task ShowMessageAsync(
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
}
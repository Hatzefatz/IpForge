using IpForge.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace IpForge.Services;

public sealed class PresetService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;

    public PresetService()
    {
        string appDataDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "IpForge");

        Directory.CreateDirectory(appDataDirectory);

        _filePath = Path.Combine(
            appDataDirectory,
            "presets.json");
    }

    public async Task<List<NetworkPreset>> LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        string json = await File.ReadAllTextAsync(_filePath);

        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<NetworkPreset>>(
            json,
            JsonOptions) ?? [];
    }

    public async Task SaveAsync(
        IEnumerable<NetworkPreset> presets)
    {
        string json = JsonSerializer.Serialize(
            presets,
            JsonOptions);

        await File.WriteAllTextAsync(
            _filePath,
            json);
    }
}
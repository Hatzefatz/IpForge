using System;

namespace IpForge.Models;

public sealed class NetworkPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public string SubnetMask { get; set; } = string.Empty;

    public string Gateway { get; set; } = string.Empty;

    public string PrimaryDns { get; set; } = string.Empty;

    public string SecondaryDns { get; set; } = string.Empty;
}
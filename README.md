# IP Forge

IP Forge is a small Windows utility for viewing and managing IPv4 network adapter configurations. It provides a simple interface for switching adapters between DHCP and static IPv4 settings and supports reusable configuration presets.

## Features

- View available Windows network adapters and their current status
- Display the current IPv4 address, subnet mask, gateway, and DNS servers
- Switch an adapter between DHCP and static IPv4 configuration
- Configure an IPv4 address, subnet mask, gateway, and DNS servers
- Create, edit, delete, and reuse network presets
- Validate IPv4 addresses and subnet configuration before applying changes
- Refresh adapter information without restarting the application
- Confirm changes before modifying the selected adapter
- Display success, warning, and error notifications in the application

## Requirements

- Windows 10 or Windows 11
- Administrator rights for changing network adapter settings
- x64, x86, or ARM64 Windows device

IP Forge currently runs as an unpackaged WinUI 3 application. Windows displays a User Account Control prompt when the application starts because network configuration changes require elevated permissions.

## Preset storage

Presets are stored locally for the current Windows user:

```text
%LocalAppData%\IpForge\presets.json
```

No separate database or server is required.

## Building the project

### Prerequisites

- Visual Studio with WinUI application development support
- .NET 8 SDK
- Windows App SDK dependencies restored through NuGet

### Debug build

1. Open the solution in Visual Studio.
2. Select `Debug` and the desired platform, such as `x64`.
3. Select the `IpForge (Unpackaged)` launch profile.
4. Run Visual Studio as administrator.
5. Start the application with `F5`.

### Release build

1. Select `Release` and the desired platform, such as `x64`.
2. Clean the solution.
3. Rebuild the solution.
4. Publish the project again when using a folder-based publish profile.

Debug, Release, and published output are separate builds. Republishing is required after changing the application.

## Usage

1. Start IP Forge and accept the Windows administrator prompt.
2. Select a network adapter.
3. Choose DHCP or Static mode.
4. For Static mode, enter the IPv4 settings manually or select a preset.
5. Select **Apply configuration**.
6. Review and confirm the configuration.

Changing an active adapter can temporarily interrupt the network connection. Verify static network settings before applying them.

## Technology

- C#
- .NET 8
- WinUI 3
- Windows App SDK
- `netsh` for applying Windows IPv4 configuration changes
- JSON for local preset storage

## Project status

IP Forge is currently an early working release. The core IPv4 adapter and preset workflows are implemented and ready for testing on additional Windows systems.

# Device battery library

| Package | Info |
|:-|:-|
| DeviceLib.Battery | [![NuGet](https://img.shields.io/nuget/v/DeviceLib.Battery.svg)](https://www.nuget.org/packages/DeviceLib.Battery) |

## Supported

Windows only.

| Provider | Device | Method |
|:-|:-|:-|
| Logitech | HID++ 2.0 devices on Bolt / Unifying receivers, USB and Bluetooth | Feature `0x1004`, `0x1000`, `0x1001` |
| Bluetooth | Connected Bluetooth devices paired with Windows | Device property `{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2` |

## Usage

```csharp
using DeviceLib.Battery;

var reader = new BatteryReader();
foreach (var battery in await reader.ReadAsync())
{
    Console.WriteLine($"{battery.Name} : {battery.Percent}% {battery.Status}");
}
```

Select providers.

```csharp
using DeviceLib.Battery;
using DeviceLib.Battery.Logitech;

var reader = new BatteryReader(new LogitechProvider());
```

Include disconnected Bluetooth devices.

```csharp
using DeviceLib.Battery;
using DeviceLib.Battery.Bluetooth;
using DeviceLib.Battery.Logitech;

var reader = new BatteryReader(new LogitechProvider(), new BluetoothProvider { IncludeDisconnected = true });
```

## Global tool

### Install

```
> dotnet tool install -g DeviceTool.Battery
```

### Commands

| Command | Description |
|:-|:-|
| `list` | List device batteries |
| `watch` | Watch device batteries |
| `logitech` | Show Logitech HID++ details |

### Usage

```
devbat list
devbat list -a
devbat watch -i 60
devbat logitech -f -t
```

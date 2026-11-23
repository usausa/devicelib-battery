namespace DeviceLib.Battery.Bluetooth;

using System.Runtime.Versioning;

[SupportedOSPlatform("windows")]
public sealed class BluetoothProvider : IBatteryProvider
{
    private const string ProviderName = "Bluetooth";

    public string Name => ProviderName;

    public ValueTask<IReadOnlyList<BatteryInfo>> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var batteries = new List<BatteryInfo>();
        foreach (var battery in BluetoothBatteryReader.Read())
        {
            batteries.Add(new BatteryInfo
            {
                Id = $"bluetooth:{battery.ContainerId}",
                Name = battery.Name,
                Source = ProviderName,
                Kind = ResolveKind(battery.ClassOfDevice, battery.Name),
                Connection = DeviceConnection.Bluetooth,
                Connected = battery.Connected,
                Percent = battery.Percent,
                ContainerId = battery.ContainerId
            });
        }

        return ValueTask.FromResult<IReadOnlyList<BatteryInfo>>(batteries);
    }

    private static DeviceKind ResolveKind(uint? classOfDevice, string name)
    {
        var kind = FromClassOfDevice(classOfDevice);
        return kind == DeviceKind.Unknown ? KindGuess.FromName(name) : kind;
    }

    private static DeviceKind FromClassOfDevice(uint? classOfDevice)
    {
        if (classOfDevice is not { } value)
        {
            return DeviceKind.Unknown;
        }

        var major = (value >> 8) & 0x1F;
        var minor = (value >> 2) & 0x3F;
        return major switch
        {
            0x02 => DeviceKind.Phone,
            0x04 => DeviceKind.Headset,
            0x05 when (minor & 0x0F) is 0x01 or 0x02 => DeviceKind.Gamepad,
            0x05 when (minor >> 4) == 0x01 => DeviceKind.Keyboard,
            0x05 when (minor >> 4) == 0x02 => DeviceKind.Mouse,
            _ => DeviceKind.Unknown
        };
    }
}

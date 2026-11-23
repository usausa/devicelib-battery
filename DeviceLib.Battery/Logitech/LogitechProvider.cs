namespace DeviceLib.Battery.Logitech;

using System.Runtime.Versioning;

using DeviceLib.Battery.Hid;
using DeviceLib.Battery.Pnp;

[SupportedOSPlatform("windows")]
public sealed class LogitechProvider : IBatteryProvider
{
    private const string ProviderName = "Logitech";

    public string Name => ProviderName;

    public async ValueTask<IReadOnlyList<BatteryInfo>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var batteries = new List<BatteryInfo>();
        foreach (var hidpp in HidppInterface.Find(HidEnumerator.Enumerate()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var channel = hidpp.Open(null);
            if (!channel.IsOpen)
            {
                continue;
            }

            foreach (var target in await HidppScanner.ResolveAsync(hidpp, channel).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var battery = await ReadDeviceAsync(hidpp, channel, target).ConfigureAwait(false);
                if (battery is not null)
                {
                    batteries.Add(battery);
                }
            }
        }

        return batteries;
    }

    private static async ValueTask<BatteryInfo?> ReadDeviceAsync(HidppInterface hidpp, HidppChannel channel, HidppTarget target)
    {
        var device = new HidppDevice(channel, target.Index);
        var ping = await device.PingAsync().ConfigureAwait(false);
        if (!ping.IsSuccess || (ping.Payload.Length == 0) || (ping.Payload[0] < 2))
        {
            return null;
        }

        var battery = await device.ReadBatteryAsync().ConfigureAwait(false);
        if (battery is null)
        {
            return null;
        }

        var identity = await device.ReadIdentityAsync().ConfigureAwait(false);
        var name = identity.Name ?? (hidpp.Product.Length > 0 ? hidpp.Product : "Logitech device");
        return new BatteryInfo
        {
            Id = identity.UnitId is null ? $"logitech:{hidpp.ProductId:X4}:{target.Index:X2}" : $"logitech:{identity.UnitId}",
            Name = name,
            Source = ProviderName,
            Kind = identity.Kind == DeviceKind.Unknown ? KindGuess.FromName(name) : identity.Kind,
            Connection = target.Connection,
            Percent = battery.Percent,
            Level = battery.Level,
            Status = battery.Status,
            Voltage = battery.Voltage,
            ContainerId = target.Connection == DeviceConnection.Receiver ? null : PnpDevices.ReadInterfaceContainerId(hidpp.Primary.Path)
        };
    }
}

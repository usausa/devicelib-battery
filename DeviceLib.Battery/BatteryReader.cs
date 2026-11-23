namespace DeviceLib.Battery;

using System.Runtime.Versioning;

using DeviceLib.Battery.Bluetooth;
using DeviceLib.Battery.Logitech;

[SupportedOSPlatform("windows")]
public sealed class BatteryReader
{
    private readonly IBatteryProvider[] providers;

    public BatteryReader()
        : this(new LogitechProvider(), new BluetoothProvider())
    {
    }

    public BatteryReader(params IEnumerable<IBatteryProvider> providers)
    {
        this.providers = [.. providers];
    }

    public IReadOnlyList<IBatteryProvider> Providers => providers;

    public async ValueTask<IReadOnlyList<BatteryInfo>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var batteries = new List<BatteryInfo>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var containers = new HashSet<Guid>();
        foreach (var provider in providers)
        {
            foreach (var battery in await provider.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!ids.Add(battery.Id) || ((battery.ContainerId is { } containerId) && !containers.Add(containerId)))
                {
                    continue;
                }

                batteries.Add(battery);
            }
        }

        return batteries;
    }
}

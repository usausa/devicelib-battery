namespace DeviceLib.Battery.Bluetooth;

using DeviceLib.Battery.Pnp;

internal sealed record BluetoothBattery(Guid ContainerId, string Name, byte Percent, bool Connected, uint? ClassOfDevice);

internal static class BluetoothBatteryReader
{
    private static readonly string[] Enumerators = ["BTHENUM", "BTHLE", "BTHLEDEVICE"];

    private static readonly string[] MainNodePrefixes = [@"BTHENUM\DEV_", @"BTHLE\DEV_"];

    public static IReadOnlyList<BluetoothBattery> Read()
    {
        var containers = new Dictionary<Guid, Container>();
        foreach (var enumerator in Enumerators)
        {
            foreach (var instanceId in PnpDevices.ListPresentInstanceIds(enumerator))
            {
                if ((PnpDevices.Locate(instanceId) is not { } devInst) || (PnpDevices.ReadGuid(devInst, PnpDevices.ContainerId) is not { } containerId))
                {
                    continue;
                }

                if (!containers.TryGetValue(containerId, out var container))
                {
                    container = new Container();
                    containers.Add(containerId, container);
                }

                Collect(container, instanceId, devInst);
            }
        }

        var batteries = new List<BluetoothBattery>();
        foreach (var (containerId, container) in containers)
        {
            if (container.Battery is { } battery)
            {
                batteries.Add(new BluetoothBattery(
                    containerId,
                    container.Name ?? container.ServiceName ?? "Bluetooth device",
                    battery,
                    container.MainConnected ?? container.ServiceConnected,
                    container.ClassOfDevice));
            }
        }

        return batteries;
    }

    private static void Collect(Container container, string instanceId, uint devInst)
    {
        var name = PnpDevices.ReadString(devInst, PnpDevices.FriendlyName) ?? PnpDevices.ReadString(devInst, PnpDevices.DeviceDescription);
        var connected = PnpDevices.ReadBoolean(devInst, PnpDevices.BluetoothConnected);
        if (MainNodePrefixes.Any(prefix => instanceId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            container.Name ??= name;
            container.MainConnected = (container.MainConnected ?? false) || (connected ?? false);
            container.ClassOfDevice ??= PnpDevices.ReadUInt32(devInst, PnpDevices.BluetoothClassOfDevice);
        }

        if ((PnpDevices.ReadByte(devInst, PnpDevices.BluetoothBatteryLevel) is { } level) && (level <= 100) && !PnpDevices.HasProblem(devInst))
        {
            if ((container.Battery is null) || (connected ?? false))
            {
                container.Battery = level;
                container.ServiceName ??= name;
            }

            container.ServiceConnected |= connected ?? true;
        }
    }

    private sealed class Container
    {
        public string? Name { get; set; }

        public string? ServiceName { get; set; }

        public bool? MainConnected { get; set; }

        public bool ServiceConnected { get; set; }

        public uint? ClassOfDevice { get; set; }

        public byte? Battery { get; set; }
    }
}

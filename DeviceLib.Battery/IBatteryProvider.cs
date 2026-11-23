namespace DeviceLib.Battery;

public interface IBatteryProvider
{
    string Name { get; }

    ValueTask<IReadOnlyList<BatteryInfo>> ReadAsync(CancellationToken cancellationToken = default);
}

namespace DeviceLib.Battery;

public sealed record BatteryInfo
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Source { get; init; }

    public DeviceKind Kind { get; init; }

    public DeviceConnection Connection { get; init; }

    public bool Connected { get; init; } = true;

    public int? Percent { get; init; }

    public BatteryLevel Level { get; init; }

    public BatteryStatus Status { get; init; }

    public int? Voltage { get; init; }

    public Guid? ContainerId { get; init; }
}

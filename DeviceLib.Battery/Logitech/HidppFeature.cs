namespace DeviceLib.Battery.Logitech;

internal static class HidppFeature
{
    public const ushort Root = 0x0000;
    public const ushort FeatureSet = 0x0001;
    public const ushort DeviceInformation = 0x0003;
    public const ushort DeviceTypeAndName = 0x0005;
    public const ushort BatteryLevelStatus = 0x1000;
    public const ushort BatteryVoltage = 0x1001;
    public const ushort UnifiedBattery = 0x1004;

    public static string? GetName(ushort featureId) => featureId switch
    {
        Root => "Root",
        FeatureSet => "FeatureSet",
        DeviceInformation => "DeviceInformation",
        DeviceTypeAndName => "DeviceTypeAndName",
        BatteryLevelStatus => "BatteryLevelStatus",
        BatteryVoltage => "BatteryVoltage",
        UnifiedBattery => "UnifiedBattery",
        _ => null
    };
}

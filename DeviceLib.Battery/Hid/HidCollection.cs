namespace DeviceLib.Battery.Hid;

internal sealed record HidCollection(
    string Path,
    ushort VendorId,
    ushort ProductId,
    ushort UsagePage,
    ushort Usage,
    ushort InputReportLength,
    ushort OutputReportLength)
{
    public bool IsBluetooth =>
        Path.Contains("{00001812-", StringComparison.OrdinalIgnoreCase) ||
        Path.Contains("{00001124-", StringComparison.OrdinalIgnoreCase);
}

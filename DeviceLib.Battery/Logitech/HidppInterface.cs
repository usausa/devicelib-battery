namespace DeviceLib.Battery.Logitech;

using DeviceLib.Battery.Hid;

internal sealed record HidppInterface(HidCollection Primary, HidCollection? ShortCollection, HidCollection? LongCollection, string Product)
{
    public const ushort VendorId = 0x046D;

    private const ushort HidppUsagePage = 0xFF00;

    private const ushort VendorUsagePage = 0xFF43;

    private const ushort ShortUsage = 0x0001;

    private const ushort LongUsage = 0x0002;

    private const int ShortReportLength = 7;

    private const int LongReportLength = 20;

    public ushort ProductId => Primary.ProductId;

    public bool IsBluetooth => Primary.IsBluetooth;

    public bool IsReceiver => Product.Contains("Receiver", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<HidppInterface> Find(IEnumerable<HidCollection> collections) =>
        collections
            .Where(static x => (x.VendorId == VendorId) && (IsShort(x) || IsLong(x)))
            .GroupBy(static x => (x.ProductId, Key: PhysicalKey(x.Path)))
            .Select(static x => Create([.. x]))
            .ToArray();

    public HidppChannel Open(Action<string>? trace) => new(ShortCollection, LongCollection, trace);

    private static HidppInterface Create(HidCollection[] group)
    {
        var shortCollection = group.FirstOrDefault(IsShort);
        var longCollection = group.FirstOrDefault(IsLong);
        var primary = longCollection ?? shortCollection ?? group[0];
        return new HidppInterface(primary, shortCollection, longCollection, HidEnumerator.ReadProductString(primary.Path));
    }

    private static bool IsShort(HidCollection collection) =>
        (collection.OutputReportLength == ShortReportLength) && IsHidppUsage(collection, ShortUsage);

    private static bool IsLong(HidCollection collection) =>
        (collection.OutputReportLength == LongReportLength) && IsHidppUsage(collection, LongUsage);

    private static bool IsHidppUsage(HidCollection collection, ushort usage) =>
        ((collection.UsagePage == HidppUsagePage) && (collection.Usage == usage)) ||
        ((collection.UsagePage == VendorUsagePage) && ((collection.Usage & 0xFF) == usage));

    private static string PhysicalKey(string path)
    {
        var parts = path.ToUpperInvariant().Split('#');
        if (parts.Length < 4)
        {
            return String.Join('#', parts);
        }

        var hardware = parts[1];
        var instance = parts[2];
        var collection = hardware.IndexOf("&COL", StringComparison.Ordinal);
        if (collection >= 0)
        {
            hardware = hardware.Remove(collection, Math.Min(6, hardware.Length - collection));
        }

        var separator = instance.AsSpan().LastIndexOf('&');
        return $"{hardware}#{(separator >= 0 ? instance[..separator] : instance)}";
    }
}

namespace DeviceLib.Battery.Interop;

using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct DevPropKey(Guid formatId, uint propertyId)
{
    public readonly Guid FormatId = formatId;
    public readonly uint PropertyId = propertyId;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HiddAttributes
{
    public uint Size;
    public ushort VendorId;
    public ushort ProductId;
    public ushort VersionNumber;
}

[StructLayout(LayoutKind.Explicit, Size = 64)]
internal struct HidpCaps
{
    [FieldOffset(0)]
    public ushort Usage;

    [FieldOffset(2)]
    public ushort UsagePage;

    [FieldOffset(4)]
    public ushort InputReportByteLength;

    [FieldOffset(6)]
    public ushort OutputReportByteLength;
}

internal sealed class SafePreparsedDataHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafePreparsedDataHandle()
        : base(true)
    {
    }

    protected override bool ReleaseHandle() => NativeMethods.HidDFreePreparsedData(handle);
}

namespace DeviceLib.Battery.Pnp;

using System.Runtime.InteropServices;

using DeviceLib.Battery.Interop;

internal static class PnpDevices
{
    public static readonly DevPropKey FriendlyName = new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);

    public static readonly DevPropKey DeviceDescription = new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 2);

    public static readonly DevPropKey ContainerId = new(new Guid("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"), 2);

    public static readonly DevPropKey InstanceId = new(new Guid("78C34FC8-104A-4ACA-9EA4-524D52996E57"), 256);

    public static readonly DevPropKey BluetoothBatteryLevel = new(new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"), 2);

    public static readonly DevPropKey BluetoothConnected = new(new Guid("83DA6326-97A6-4088-9453-A1923F573B29"), 15);

    public static readonly DevPropKey BluetoothClassOfDevice = new(new Guid("2BD67D8B-8BEB-48D5-87E0-6CDA3428040A"), 10);

    private const int PropertyBufferLength = 512;

    public static string[] ListPresentInstanceIds(string enumerator)
    {
        const uint flags = NativeMethods.CmGetIdListFilterEnumerator | NativeMethods.CmGetIdListFilterPresent;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if ((NativeMethods.CmGetDeviceIdListSize(out var length, enumerator, flags) != NativeMethods.CrSuccess) || (length <= 1))
            {
                return [];
            }

            var buffer = new ushort[length];
            var result = NativeMethods.CmGetDeviceIdList(enumerator, buffer, length, flags);
            if (result == NativeMethods.CrSuccess)
            {
                return new string(MemoryMarshal.Cast<ushort, char>(buffer)).Split('\0', StringSplitOptions.RemoveEmptyEntries);
            }

            if (result != NativeMethods.CrBufferSmall)
            {
                return [];
            }
        }

        return [];
    }

    public static uint? Locate(string instanceId) =>
        NativeMethods.CmLocateDevNode(out var devInst, instanceId, NativeMethods.CmLocateDevNodeNormal) == NativeMethods.CrSuccess ? devInst : null;

    public static bool HasProblem(uint devInst) =>
        (NativeMethods.CmGetDevNodeStatus(out var status, out _, devInst, 0) == NativeMethods.CrSuccess) && ((status & NativeMethods.DnHasProblem) != 0);

    public static byte? ReadByte(uint devInst, DevPropKey key)
    {
        Span<byte> buffer = stackalloc byte[PropertyBufferLength];
        return TryRead(devInst, key, NativeMethods.DevPropTypeByte, buffer, out var length) && (length >= 1) ? buffer[0] : null;
    }

    public static bool? ReadBoolean(uint devInst, DevPropKey key)
    {
        Span<byte> buffer = stackalloc byte[PropertyBufferLength];
        return TryRead(devInst, key, NativeMethods.DevPropTypeBoolean, buffer, out var length) && (length >= 1) ? buffer[0] != 0 : null;
    }

    public static uint? ReadUInt32(uint devInst, DevPropKey key)
    {
        Span<byte> buffer = stackalloc byte[PropertyBufferLength];
        return TryRead(devInst, key, NativeMethods.DevPropTypeUInt32, buffer, out var length) && (length >= 4) ? MemoryMarshal.Read<uint>(buffer) : null;
    }

    public static Guid? ReadGuid(uint devInst, DevPropKey key)
    {
        Span<byte> buffer = stackalloc byte[PropertyBufferLength];
        return TryRead(devInst, key, NativeMethods.DevPropTypeGuid, buffer, out var length) && (length >= 16) ? new Guid(buffer[..16]) : null;
    }

    public static string? ReadString(uint devInst, DevPropKey key)
    {
        Span<byte> buffer = stackalloc byte[PropertyBufferLength];
        return TryRead(devInst, key, NativeMethods.DevPropTypeString, buffer, out var length) ? DecodeString(buffer[..length]) : null;
    }

    public static Guid? ReadInterfaceContainerId(string interfacePath)
    {
        Span<byte> buffer = stackalloc byte[PropertyBufferLength];
        var size = (uint)buffer.Length;
        var result = NativeMethods.CmGetDeviceInterfaceProperty(interfacePath, in InstanceId, out var type, buffer, ref size, 0);
        if ((result != NativeMethods.CrSuccess) || (type != NativeMethods.DevPropTypeString))
        {
            return null;
        }

        return Locate(DecodeString(buffer[..(int)Math.Min(size, (uint)buffer.Length)])) is { } devInst ? ReadGuid(devInst, ContainerId) : null;
    }

    private static bool TryRead(uint devInst, in DevPropKey key, uint expectedType, Span<byte> buffer, out int length)
    {
        var size = (uint)buffer.Length;
        var result = NativeMethods.CmGetDevNodeProperty(devInst, in key, out var type, buffer, ref size, 0);
        length = (int)Math.Min(size, (uint)buffer.Length);
        return (result == NativeMethods.CrSuccess) && (type == expectedType);
    }

    private static string DecodeString(ReadOnlySpan<byte> bytes)
    {
        var text = MemoryMarshal.Cast<byte, char>(bytes);
        var end = text.IndexOf('\0');
        return new string(end >= 0 ? text[..end] : text);
    }
}

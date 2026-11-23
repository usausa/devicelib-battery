namespace DeviceLib.Battery.Hid;

using System.Runtime.InteropServices;

using DeviceLib.Battery.Interop;

using Microsoft.Win32.SafeHandles;

internal static class HidEnumerator
{
    private static readonly Guid HidInterfaceClass = new("4D1E55B2-F16F-11CF-88CB-001111000030");

    public static IReadOnlyList<HidCollection> Enumerate()
    {
        var collections = new List<HidCollection>();
        foreach (var path in ReadInterfacePaths())
        {
            var collection = Describe(path);
            if (collection is not null)
            {
                collections.Add(collection);
            }
        }

        return collections;
    }

    public static string ReadProductString(string path)
    {
        using var handle = OpenMetadataHandle(path);
        if (handle.IsInvalid)
        {
            return string.Empty;
        }

        Span<byte> buffer = stackalloc byte[256];
        buffer.Clear();
        if (!NativeMethods.HidDGetProductString(handle, buffer, (uint)buffer.Length))
        {
            return string.Empty;
        }

        var text = MemoryMarshal.Cast<byte, char>(buffer);
        var end = text.IndexOf('\0');
        return new string(end >= 0 ? text[..end] : text).Trim();
    }

    private static SafeFileHandle OpenMetadataHandle(string path) =>
        NativeMethods.CreateFile(path, 0, NativeMethods.FileShareReadWrite, 0, NativeMethods.OpenExisting, 0, 0);

    private static string[] ReadInterfacePaths()
    {
        var guid = HidInterfaceClass;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if ((NativeMethods.CmGetDeviceInterfaceListSize(out var length, in guid, 0, 0) != NativeMethods.CrSuccess) || (length <= 1))
            {
                return [];
            }

            var buffer = new ushort[length];
            var result = NativeMethods.CmGetDeviceInterfaceList(in guid, 0, buffer, length, 0);
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

    private static HidCollection? Describe(string path)
    {
        using var handle = OpenMetadataHandle(path);
        if (handle.IsInvalid)
        {
            return null;
        }

        var attributes = new HiddAttributes { Size = (uint)Marshal.SizeOf<HiddAttributes>() };
        if (!NativeMethods.HidDGetAttributes(handle, ref attributes) || !NativeMethods.HidDGetPreparsedData(handle, out var preparsed))
        {
            return null;
        }

        using (preparsed)
        {
            if (NativeMethods.HidPGetCaps(preparsed, out var caps) != NativeMethods.HidpStatusSuccess)
            {
                return null;
            }

            return new HidCollection(
                path,
                attributes.VendorId,
                attributes.ProductId,
                caps.UsagePage,
                caps.Usage,
                caps.InputReportByteLength,
                caps.OutputReportByteLength);
        }
    }
}

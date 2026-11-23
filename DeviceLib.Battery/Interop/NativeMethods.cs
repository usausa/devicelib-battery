namespace DeviceLib.Battery.Interop;

using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

internal static partial class NativeMethods
{
    public const uint GenericRead = 0x80000000;
    public const uint GenericWrite = 0x40000000;
    public const uint FileShareReadWrite = 0x00000003;
    public const uint OpenExisting = 3;
    public const uint FileFlagOverlapped = 0x40000000;

    public const uint CrSuccess = 0x00000000;
    public const uint CrBufferSmall = 0x0000001A;

    public const uint CmGetIdListFilterEnumerator = 0x00000001;
    public const uint CmGetIdListFilterPresent = 0x00000100;
    public const uint CmLocateDevNodeNormal = 0x00000000;
    public const uint DnHasProblem = 0x00000400;

    public const uint DevPropTypeByte = 0x00000003;
    public const uint DevPropTypeUInt32 = 0x00000007;
    public const uint DevPropTypeGuid = 0x0000000D;
    public const uint DevPropTypeBoolean = 0x00000011;
    public const uint DevPropTypeString = 0x00000012;

    public const int HidpStatusSuccess = 0x00110000;

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, nint securityAttributes, uint creationDisposition, uint flagsAndAttributes, nint templateFile);

    [LibraryImport("cfgmgr32.dll", EntryPoint = "CM_Get_Device_Interface_List_SizeW")]
    public static partial uint CmGetDeviceInterfaceListSize(out uint length, in Guid interfaceClassGuid, nint deviceId, uint flags);

    [LibraryImport("cfgmgr32.dll", EntryPoint = "CM_Get_Device_Interface_ListW")]
    public static partial uint CmGetDeviceInterfaceList(in Guid interfaceClassGuid, nint deviceId, Span<ushort> buffer, uint bufferLength, uint flags);

    [LibraryImport("cfgmgr32.dll", EntryPoint = "CM_Get_Device_ID_List_SizeW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint CmGetDeviceIdListSize(out uint length, string filter, uint flags);

    [LibraryImport("cfgmgr32.dll", EntryPoint = "CM_Get_Device_ID_ListW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint CmGetDeviceIdList(string filter, Span<ushort> buffer, uint bufferLength, uint flags);

    [LibraryImport("cfgmgr32.dll", EntryPoint = "CM_Locate_DevNodeW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint CmLocateDevNode(out uint devInst, string deviceId, uint flags);

    [LibraryImport("cfgmgr32.dll", EntryPoint = "CM_Get_DevNode_PropertyW")]
    public static partial uint CmGetDevNodeProperty(uint devInst, in DevPropKey propertyKey, out uint propertyType, Span<byte> propertyBuffer, ref uint propertyBufferSize, uint flags);

    [LibraryImport("cfgmgr32.dll", EntryPoint = "CM_Get_DevNode_Status")]
    public static partial uint CmGetDevNodeStatus(out uint status, out uint problemNumber, uint devInst, uint flags);

    [LibraryImport("cfgmgr32.dll", EntryPoint = "CM_Get_Device_Interface_PropertyW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint CmGetDeviceInterfaceProperty(string deviceInterface, in DevPropKey propertyKey, out uint propertyType, Span<byte> propertyBuffer, ref uint propertyBufferSize, uint flags);

    [LibraryImport("hid.dll", EntryPoint = "HidD_GetAttributes")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool HidDGetAttributes(SafeFileHandle hidDeviceObject, ref HiddAttributes attributes);

    [LibraryImport("hid.dll", EntryPoint = "HidD_GetPreparsedData")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool HidDGetPreparsedData(SafeFileHandle hidDeviceObject, out SafePreparsedDataHandle preparsedData);

    [LibraryImport("hid.dll", EntryPoint = "HidD_FreePreparsedData")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool HidDFreePreparsedData(nint preparsedData);

    [LibraryImport("hid.dll", EntryPoint = "HidP_GetCaps")]
    public static partial int HidPGetCaps(SafePreparsedDataHandle preparsedData, out HidpCaps capabilities);

    [LibraryImport("hid.dll", EntryPoint = "HidD_GetProductString")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool HidDGetProductString(SafeFileHandle hidDeviceObject, Span<byte> buffer, uint bufferLength);
}

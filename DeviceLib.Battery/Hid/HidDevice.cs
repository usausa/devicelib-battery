namespace DeviceLib.Battery.Hid;

using DeviceLib.Battery.Interop;

using Microsoft.Win32.SafeHandles;

internal sealed class HidDevice : IDisposable
{
    private readonly SafeFileHandle handle;

    private readonly FileStream stream;

    private HidDevice(HidCollection collection, SafeFileHandle fileHandle)
    {
        Collection = collection;
        handle = fileHandle;
        stream = new FileStream(fileHandle, FileAccess.ReadWrite, 0, true);
    }

    public HidCollection Collection { get; }

    public static HidDevice? OpenReadWrite(HidCollection collection)
    {
        var fileHandle = NativeMethods.CreateFile(
            collection.Path,
            NativeMethods.GenericRead | NativeMethods.GenericWrite,
            NativeMethods.FileShareReadWrite,
            0,
            NativeMethods.OpenExisting,
            NativeMethods.FileFlagOverlapped,
            0);
        if (fileHandle.IsInvalid)
        {
            fileHandle.Dispose();
            return null;
        }

        try
        {
            return new HidDevice(collection, fileHandle);
        }
        catch (IOException)
        {
            fileHandle.Dispose();
            return null;
        }
        catch (ArgumentException)
        {
            fileHandle.Dispose();
            return null;
        }
    }

    public void Dispose()
    {
        stream.Dispose();
        handle.Dispose();
    }

    public async ValueTask<byte[]?> ReadAsync(CancellationToken cancellationToken)
    {
        if (Collection.InputReportLength == 0)
        {
            return null;
        }

        var buffer = new byte[Collection.InputReportLength];
        try
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            return read > 0 ? buffer[..read] : null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }

    public async ValueTask<bool> WriteAsync(ReadOnlyMemory<byte> data, TimeSpan timeout)
    {
        if ((Collection.OutputReportLength == 0) || data.IsEmpty)
        {
            return false;
        }

        var report = new byte[Collection.OutputReportLength];
        data.Span[..Math.Min(data.Length, report.Length)].CopyTo(report);
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await stream.WriteAsync(report, cts.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}

namespace DeviceLib.Battery.Hid;

using System.Threading.Channels;

internal sealed class HidInputPump : IDisposable
{
    private readonly Channel<byte[]> channel = Channel.CreateUnbounded<byte[]>();

    private readonly CancellationTokenSource stop = new();

    private readonly Task[] pumps;

    public HidInputPump(IEnumerable<HidDevice> devices)
    {
        pumps = [.. devices.Select(device => PumpAsync(device, channel.Writer, stop.Token))];
    }

    public void Dispose()
    {
        stop.Cancel();
        Task.WaitAll(pumps);
        stop.Dispose();
    }

    public void Clear()
    {
        while (channel.Reader.TryRead(out _))
        {
        }
    }

    public async ValueTask<byte[]?> ReadAsync(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            return null;
        }

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            return await channel.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private static async Task PumpAsync(HidDevice device, ChannelWriter<byte[]> writer, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var report = await device.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (report is null)
            {
                return;
            }

            _ = writer.TryWrite(report);
        }
    }
}

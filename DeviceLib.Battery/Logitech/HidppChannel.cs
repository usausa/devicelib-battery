namespace DeviceLib.Battery.Logitech;

using System.Diagnostics;

using DeviceLib.Battery.Hid;

internal enum HidppReplyStatus
{
    Timeout,
    Success,
    Error
}

internal sealed record HidppReply(HidppReplyStatus Status, byte[] Payload, byte ErrorCode, byte ErrorType)
{
    public static HidppReply NoReply { get; } = new(HidppReplyStatus.Timeout, [], 0, 0);

    public bool IsSuccess => Status == HidppReplyStatus.Success;
}

internal sealed class HidppChannel : IDisposable
{
    public const byte ReceiverIndex = 0xFF;

    public const byte Hidpp10Error = 0x8F;

    public const byte Hidpp20Error = 0xFF;

    private const byte SoftwareId = 0x0D;

    private const byte ShortReportId = 0x10;

    private const byte LongReportId = 0x11;

    private const int ShortReportLength = 7;

    private const int LongReportLength = 20;

    private const byte GetLongRegister = 0x83;

    private static readonly byte[] NoParameters = [0x00, 0x00, 0x00];

    private readonly HidDevice? shortDevice;

    private readonly HidDevice? longDevice;

    private readonly HidInputPump pump;

    private readonly Action<string>? trace;

    public HidppChannel(HidCollection? shortCollection, HidCollection? longCollection, Action<string>? trace)
    {
        shortDevice = shortCollection is null ? null : HidDevice.OpenReadWrite(shortCollection);
        longDevice = longCollection is null ? null : HidDevice.OpenReadWrite(longCollection);
        pump = new HidInputPump(new[] { shortDevice, longDevice }.OfType<HidDevice>());
        this.trace = trace;
    }

    public bool IsOpen => (shortDevice is not null) || (longDevice is not null);

    public void Dispose()
    {
        pump.Dispose();
        shortDevice?.Dispose();
        longDevice?.Dispose();
    }

    public ValueTask<HidppReply> PingAsync(byte deviceIndex, TimeSpan timeout) =>
        SendAsync(deviceIndex, 0x00, 0x10 | SoftwareId, NoParameters, null, false, timeout);

    public ValueTask<HidppReply> CallAsync(byte deviceIndex, byte featureIndex, byte function, ReadOnlyMemory<byte> parameters, TimeSpan timeout) =>
        SendAsync(deviceIndex, featureIndex, (byte)((function << 4) | SoftwareId), parameters, null, false, timeout);

    public ValueTask<HidppReply> ReadLongRegisterAsync(byte deviceIndex, byte register, byte subRegister, TimeSpan timeout) =>
        SendAsync(deviceIndex, GetLongRegister, register, new byte[] { subRegister, 0x00, 0x00 }, subRegister, true, timeout);

    private async ValueTask<HidppReply> SendAsync(byte deviceIndex, byte subId, byte address, ReadOnlyMemory<byte> parameters, byte? echo, bool shortOnly, TimeSpan timeout)
    {
        var useLong = !shortOnly && (longDevice is not null);
        var device = useLong ? longDevice : shortDevice;
        if (device is null)
        {
            return HidppReply.NoReply;
        }

        var message = new byte[useLong ? LongReportLength : ShortReportLength];
        message[0] = useLong ? LongReportId : ShortReportId;
        message[1] = deviceIndex;
        message[2] = subId;
        message[3] = address;
        parameters.Span[..Math.Min(parameters.Length, message.Length - 4)].CopyTo(message.AsSpan(4));

        pump.Clear();
        var started = Stopwatch.GetTimestamp();
        trace?.Invoke($"tx {Convert.ToHexString(message)}");
        if (!await device.WriteAsync(message, timeout).ConfigureAwait(false))
        {
            trace?.Invoke("   write failed");
            return HidppReply.NoReply;
        }

        while (true)
        {
            var report = await pump.ReadAsync(timeout - Stopwatch.GetElapsedTime(started)).ConfigureAwait(false);
            if (report is null)
            {
                trace?.Invoke($"   no reply within {timeout.TotalMilliseconds:F0} ms");
                return HidppReply.NoReply;
            }

            trace?.Invoke($"rx {Convert.ToHexString(report)}");
            if ((report.Length < ShortReportLength) || ((report[0] != ShortReportId) && (report[0] != LongReportId)) || (report[1] != deviceIndex))
            {
                continue;
            }

            if ((report[2] == subId) && (report[3] == address) && ((echo is null) || (report[4] == echo)))
            {
                return new HidppReply(HidppReplyStatus.Success, report[4..], 0, 0);
            }

            if (((report[2] == Hidpp20Error) || (report[2] == Hidpp10Error)) && (report[3] == subId) && (report[4] == address))
            {
                return new HidppReply(HidppReplyStatus.Error, [], report[5], report[2]);
            }
        }
    }
}

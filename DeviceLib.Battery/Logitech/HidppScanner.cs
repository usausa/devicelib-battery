namespace DeviceLib.Battery.Logitech;

internal sealed record HidppTarget(byte Index, DeviceConnection Connection);

internal static class HidppScanner
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    private static readonly byte[] ReceiverIndices = [1, 2, 3, 4, 5, 6];

    public static async ValueTask<IReadOnlyList<HidppTarget>> ResolveAsync(HidppInterface hidpp, HidppChannel channel)
    {
        if (!hidpp.IsReceiver)
        {
            var ping = await channel.PingAsync(HidppChannel.ReceiverIndex, Timeout).ConfigureAwait(false);
            if (ping.IsSuccess)
            {
                return [new HidppTarget(HidppChannel.ReceiverIndex, hidpp.IsBluetooth ? DeviceConnection.Bluetooth : DeviceConnection.Usb)];
            }

            if (ping.Status == HidppReplyStatus.Timeout)
            {
                return [];
            }
        }

        var targets = new List<HidppTarget>();
        foreach (var index in ReceiverIndices)
        {
            if (hidpp.ProductId == BoltReceiver.ProductId)
            {
                var pairing = await BoltReceiver.ReadPairingAsync(channel, index, Timeout).ConfigureAwait(false);
                if (pairing.State is BoltSlotState.Unpaired or BoltSlotState.Offline)
                {
                    continue;
                }
            }

            targets.Add(new HidppTarget(index, DeviceConnection.Receiver));
        }

        return targets;
    }
}

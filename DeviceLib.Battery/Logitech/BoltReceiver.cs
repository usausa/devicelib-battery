namespace DeviceLib.Battery.Logitech;

internal enum BoltSlotState
{
    Unknown,
    Unpaired,
    Offline,
    Online
}

internal sealed record BoltPairing(byte Index, BoltSlotState State, ushort WirelessProductId, string? UnitId, byte ErrorCode);

internal static class BoltReceiver
{
    public const ushort ProductId = 0xC548;

    private const byte InformationRegister = 0xB5;

    private const byte PairingInformation = 0x50;

    private const byte UnknownDevice = 0x08;

    private const byte OfflineFlag = 0x40;

    public static async ValueTask<BoltPairing> ReadPairingAsync(HidppChannel channel, byte index, TimeSpan timeout)
    {
        var subRegister = (byte)(PairingInformation + (index & 0x0F));
        var reply = await channel.ReadLongRegisterAsync(HidppChannel.ReceiverIndex, InformationRegister, subRegister, timeout).ConfigureAwait(false);
        if (reply.Status == HidppReplyStatus.Error)
        {
            return new BoltPairing(index, reply.ErrorCode == UnknownDevice ? BoltSlotState.Unpaired : BoltSlotState.Unknown, 0, null, reply.ErrorCode);
        }

        if (!reply.IsSuccess || (reply.Payload.Length < 8))
        {
            return new BoltPairing(index, BoltSlotState.Unknown, 0, null, 0);
        }

        var payload = reply.Payload;
        var unitId = payload.AsSpan(4, 4);
        return new BoltPairing(
            index,
            (payload[1] & OfflineFlag) == 0 ? BoltSlotState.Online : BoltSlotState.Offline,
            (ushort)(payload[2] | (payload[3] << 8)),
            unitId.ContainsAnyExcept((byte)0) ? Convert.ToHexString(unitId) : null,
            0);
    }
}

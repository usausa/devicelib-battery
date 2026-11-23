namespace DeviceLib.Battery.Logitech;

using System.Text;

internal sealed record HidppFeatureInfo(ushort Id, byte Index, byte Type, byte Version);

internal sealed record HidppIdentity(string? Name, DeviceKind Kind, string? UnitId);

internal sealed record HidppBatteryReading(ushort FeatureId, int? Percent, BatteryLevel Level, BatteryStatus Status, int? Voltage, byte[] Payload);

internal sealed class HidppDevice
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(1);

    private static readonly byte[] NoParameters = [0x00, 0x00, 0x00];

    private static readonly ushort[] BatteryFeatures = [HidppFeature.UnifiedBattery, HidppFeature.BatteryLevelStatus, HidppFeature.BatteryVoltage];

    private readonly HidppChannel channel;

    public HidppDevice(HidppChannel channel, byte index)
    {
        this.channel = channel;
        Index = index;
    }

    public byte Index { get; }

    public ValueTask<HidppReply> PingAsync() => channel.PingAsync(Index, RequestTimeout);

    public async ValueTask<HidppFeatureInfo?> FindFeatureAsync(ushort featureId)
    {
        var reply = await CallAsync(0x00, 0, [(byte)(featureId >> 8), (byte)featureId, 0x00]).ConfigureAwait(false);
        return reply.IsSuccess && (reply.Payload.Length >= 3) && (reply.Payload[0] != 0)
            ? new HidppFeatureInfo(featureId, reply.Payload[0], reply.Payload[1], reply.Payload[2])
            : null;
    }

    public async ValueTask<IReadOnlyList<HidppFeatureInfo>> ReadFeatureTableAsync()
    {
        var featureSet = await FindFeatureAsync(HidppFeature.FeatureSet).ConfigureAwait(false);
        if (featureSet is null)
        {
            return [];
        }

        var count = await CallAsync(featureSet.Index, 0, NoParameters).ConfigureAwait(false);
        if (!count.IsSuccess || (count.Payload.Length == 0))
        {
            return [];
        }

        var features = new List<HidppFeatureInfo>();
        for (var index = 0; index <= count.Payload[0]; index++)
        {
            var entry = await CallAsync(featureSet.Index, 1, [(byte)index, 0x00, 0x00]).ConfigureAwait(false);
            if (entry.IsSuccess && (entry.Payload.Length >= 4))
            {
                features.Add(new HidppFeatureInfo((ushort)((entry.Payload[0] << 8) | entry.Payload[1]), (byte)index, entry.Payload[2], entry.Payload[3]));
            }
        }

        return features;
    }

    public async ValueTask<HidppIdentity> ReadIdentityAsync()
    {
        string? name = null;
        var kind = DeviceKind.Unknown;
        var nameFeature = await FindFeatureAsync(HidppFeature.DeviceTypeAndName).ConfigureAwait(false);
        if (nameFeature is not null)
        {
            name = await ReadNameAsync(nameFeature.Index).ConfigureAwait(false);
            kind = KindFromType(await ReadTypeAsync(nameFeature.Index).ConfigureAwait(false));
        }

        var infoFeature = await FindFeatureAsync(HidppFeature.DeviceInformation).ConfigureAwait(false);
        var unitId = infoFeature is null ? null : await ReadUnitIdAsync(infoFeature.Index).ConfigureAwait(false);
        return new HidppIdentity(String.IsNullOrEmpty(name) ? null : name, kind, unitId);
    }

    public async ValueTask<HidppBatteryReading?> ReadBatteryAsync()
    {
        foreach (var featureId in BatteryFeatures)
        {
            var feature = await FindFeatureAsync(featureId).ConfigureAwait(false);
            if (feature is not null)
            {
                return await ReadBatteryAsync(feature).ConfigureAwait(false);
            }
        }

        return null;
    }

    public ValueTask<HidppBatteryReading?> ReadBatteryAsync(HidppFeatureInfo feature) => feature.Id switch
    {
        HidppFeature.UnifiedBattery => ReadUnifiedAsync(feature.Index),
        HidppFeature.BatteryLevelStatus => ReadLevelStatusAsync(feature.Index),
        HidppFeature.BatteryVoltage => ReadVoltageAsync(feature.Index),
        _ => ValueTask.FromResult<HidppBatteryReading?>(null)
    };

    private static DeviceKind KindFromType(byte? type) => type switch
    {
        null => DeviceKind.Unknown,
        0x00 or 0x02 => DeviceKind.Keyboard,
        0x03 or 0x04 or 0x05 => DeviceKind.Mouse,
        0x08 => DeviceKind.Headset,
        0x0B or 0x0C => DeviceKind.Gamepad,
        _ => DeviceKind.Other
    };

    private static BatteryLevel LevelFromFlags(byte flags) => flags switch
    {
        0x01 => BatteryLevel.Critical,
        0x02 => BatteryLevel.Low,
        0x04 => BatteryLevel.Good,
        0x08 => BatteryLevel.Full,
        _ => BatteryLevel.Unknown
    };

    private static BatteryStatus StatusFromChargingState(byte state) => state switch
    {
        0x00 => BatteryStatus.Discharging,
        0x01 or 0x02 or 0x04 => BatteryStatus.Charging,
        0x03 => BatteryStatus.Full,
        0x05 or 0x06 or 0x07 => BatteryStatus.Error,
        _ => BatteryStatus.Unknown
    };

    private static BatteryStatus StatusFromVoltageFlags(byte flags)
    {
        if ((flags & 0x80) == 0)
        {
            return BatteryStatus.Discharging;
        }

        return (flags & 0x07) switch
        {
            0x00 => BatteryStatus.Charging,
            0x01 => BatteryStatus.Full,
            0x02 => BatteryStatus.Error,
            _ => BatteryStatus.Unknown
        };
    }

    private ValueTask<HidppReply> CallAsync(byte featureIndex, byte function, byte[] parameters) =>
        channel.CallAsync(Index, featureIndex, function, parameters, RequestTimeout);

    private async ValueTask<string?> ReadNameAsync(byte featureIndex)
    {
        var count = await CallAsync(featureIndex, 0, NoParameters).ConfigureAwait(false);
        if (!count.IsSuccess || (count.Payload.Length == 0))
        {
            return null;
        }

        var name = new byte[count.Payload[0]];
        var offset = 0;
        while (offset < name.Length)
        {
            var chunk = await CallAsync(featureIndex, 1, [(byte)offset, 0x00, 0x00]).ConfigureAwait(false);
            if (!chunk.IsSuccess || (chunk.Payload.Length == 0))
            {
                return null;
            }

            var size = Math.Min(chunk.Payload.Length, name.Length - offset);
            chunk.Payload.AsSpan(0, size).CopyTo(name.AsSpan(offset));
            offset += size;
        }

        return Encoding.UTF8.GetString(name).TrimEnd('\0').Trim();
    }

    private async ValueTask<byte?> ReadTypeAsync(byte featureIndex)
    {
        var reply = await CallAsync(featureIndex, 2, NoParameters).ConfigureAwait(false);
        return reply.IsSuccess && (reply.Payload.Length > 0) ? reply.Payload[0] : null;
    }

    private async ValueTask<string?> ReadUnitIdAsync(byte featureIndex)
    {
        var reply = await CallAsync(featureIndex, 0, NoParameters).ConfigureAwait(false);
        if (!reply.IsSuccess || (reply.Payload.Length < 5))
        {
            return null;
        }

        var unitId = reply.Payload.AsSpan(1, 4);
        return unitId.ContainsAnyExcept((byte)0) ? Convert.ToHexString(unitId) : null;
    }

    private async ValueTask<HidppBatteryReading?> ReadUnifiedAsync(byte featureIndex)
    {
        var capabilities = await CallAsync(featureIndex, 0, NoParameters).ConfigureAwait(false);
        var status = await CallAsync(featureIndex, 1, NoParameters).ConfigureAwait(false);
        if (!status.IsSuccess || (status.Payload.Length < 3))
        {
            return null;
        }

        var hasPercent = capabilities.IsSuccess && (capabilities.Payload.Length >= 2)
            ? (capabilities.Payload[1] & 0x02) != 0
            : status.Payload[0] > 0;
        return new HidppBatteryReading(
            HidppFeature.UnifiedBattery,
            hasPercent ? Math.Min((int)status.Payload[0], 100) : null,
            LevelFromFlags(status.Payload[1]),
            StatusFromChargingState(status.Payload[2]),
            null,
            status.Payload);
    }

    private async ValueTask<HidppBatteryReading?> ReadLevelStatusAsync(byte featureIndex)
    {
        var reply = await CallAsync(featureIndex, 0, NoParameters).ConfigureAwait(false);
        if (!reply.IsSuccess || (reply.Payload.Length < 3))
        {
            return null;
        }

        var percent = reply.Payload[0];
        return new HidppBatteryReading(
            HidppFeature.BatteryLevelStatus,
            percent is > 0 and <= 100 ? percent : null,
            BatteryLevel.Unknown,
            StatusFromChargingState(reply.Payload[2]),
            null,
            reply.Payload);
    }

    private async ValueTask<HidppBatteryReading?> ReadVoltageAsync(byte featureIndex)
    {
        var reply = await CallAsync(featureIndex, 0, NoParameters).ConfigureAwait(false);
        if (!reply.IsSuccess || (reply.Payload.Length < 3))
        {
            return null;
        }

        var flags = reply.Payload[2];
        return new HidppBatteryReading(
            HidppFeature.BatteryVoltage,
            null,
            (flags & 0x20) != 0 ? BatteryLevel.Critical : BatteryLevel.Unknown,
            StatusFromVoltageFlags(flags),
            (reply.Payload[0] << 8) | reply.Payload[1],
            reply.Payload);
    }
}

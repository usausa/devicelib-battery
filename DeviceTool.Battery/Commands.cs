namespace DeviceTool.Battery;

using DeviceLib.Battery;
using DeviceLib.Battery.Hid;
using DeviceLib.Battery.Logitech;

using Smart.CommandLine.Hosting;

internal static class CommandBuilderExtensions
{
    public static void AddCommands(this ICommandBuilder commands)
    {
        commands.AddCommand<ListCommand>();
        commands.AddCommand<WatchCommand>();
        commands.AddCommand<LogitechCommand>();
    }
}

[Command("list", "List device batteries")]
public sealed class ListCommand : ICommandHandler
{
    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var batteries = await new BatteryReader().ReadAsync(context.CancellationToken).ConfigureAwait(false);
        await BatteryTable.WriteAsync(Console.Out, batteries).ConfigureAwait(false);
    }
}

[Command("watch", "Watch device batteries")]
public sealed class WatchCommand : ICommandHandler
{
    [Option<int>("--interval", "-i", Description = "Interval in seconds", DefaultValue = 60)]
    public int Interval { get; set; }

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var reader = new BatteryReader();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(Interval, 1)));
        try
        {
            do
            {
                var batteries = await reader.ReadAsync(context.CancellationToken).ConfigureAwait(false);
                await Console.Out.WriteLineAsync($"[{DateTime.Now:HH:mm:ss}]").ConfigureAwait(false);
                await BatteryTable.WriteAsync(Console.Out, batteries).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(context.CancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            context.ExitCode = 0;
        }
    }
}

[Command("logitech", "Show Logitech HID++ details")]
public sealed class LogitechCommand : ICommandHandler
{
    [Option<bool>("--features", "-f", Description = "Show feature table")]
    public bool Features { get; set; }

    [Option<bool>("--trace", "-t", Description = "Show HID++ traffic")]
    public bool Trace { get; set; }

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var output = Console.Out;
        var interfaces = HidppInterface.Find(HidEnumerator.Enumerate());
        if (interfaces.Count == 0)
        {
            await output.WriteLineAsync("No Logitech HID++ interface found.").ConfigureAwait(false);
            return;
        }

        foreach (var hidpp in interfaces)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            await InspectAsync(output, hidpp).ConfigureAwait(false);
        }
    }

    private static string Describe(HidppReply reply) => reply.Status switch
    {
        HidppReplyStatus.Success => "ok",
        HidppReplyStatus.Error => $"error 0x{reply.ErrorCode:X2} ({(reply.ErrorType == HidppChannel.Hidpp10Error ? "HID++ 1.0" : "HID++ 2.0")})",
        _ => "no reply"
    };

    private static string DescribeFeature(HidppFeatureInfo feature) =>
        $"0x{feature.Id:X4} {HidppFeature.GetName(feature.Id) ?? string.Empty}".TrimEnd();

    private async ValueTask InspectAsync(TextWriter output, HidppInterface hidpp)
    {
        var connection = hidpp.IsBluetooth ? "Bluetooth" : (hidpp.IsReceiver ? "Receiver" : "USB");
        await output.WriteLineAsync($"[{hidpp.Product}] PID={hidpp.ProductId:X4} {connection} short={hidpp.ShortCollection is not null} long={hidpp.LongCollection is not null}").ConfigureAwait(false);
        await output.WriteLineAsync($"  path: {hidpp.Primary.Path}").ConfigureAwait(false);

        using var channel = hidpp.Open(Trace ? static line => Console.Out.WriteLine($"      {line}") : null);
        if (!channel.IsOpen)
        {
            await output.WriteLineAsync("  cannot open").ConfigureAwait(false);
            return;
        }

        if (!hidpp.IsReceiver)
        {
            var ping = await channel.PingAsync(HidppChannel.ReceiverIndex, HidppScanner.Timeout).ConfigureAwait(false);
            await output.WriteLineAsync($"  index 0xFF ping: {Describe(ping)}").ConfigureAwait(false);
            if (ping.IsSuccess)
            {
                await InspectDeviceAsync(output, new HidppDevice(channel, HidppChannel.ReceiverIndex)).ConfigureAwait(false);
                return;
            }

            if (ping.Status == HidppReplyStatus.Timeout)
            {
                return;
            }
        }

        for (byte index = 1; index <= 6; index++)
        {
            if (hidpp.ProductId == BoltReceiver.ProductId)
            {
                var pairing = await BoltReceiver.ReadPairingAsync(channel, index, HidppScanner.Timeout).ConfigureAwait(false);
                await output.WriteLineAsync($"  slot {index}: {pairing.State} wpid={pairing.WirelessProductId:X4} unit={pairing.UnitId ?? "-"} error=0x{pairing.ErrorCode:X2}").ConfigureAwait(false);
                if (pairing.State is BoltSlotState.Unpaired or BoltSlotState.Offline)
                {
                    continue;
                }
            }

            var device = new HidppDevice(channel, index);
            var reply = await device.PingAsync().ConfigureAwait(false);
            await output.WriteLineAsync($"  slot {index} ping: {Describe(reply)}").ConfigureAwait(false);
            if (reply.IsSuccess)
            {
                await InspectDeviceAsync(output, device).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask InspectDeviceAsync(TextWriter output, HidppDevice device)
    {
        var ping = await device.PingAsync().ConfigureAwait(false);
        if (!ping.IsSuccess || (ping.Payload.Length < 2))
        {
            await output.WriteLineAsync($"    protocol: {Describe(ping)}").ConfigureAwait(false);
            return;
        }

        await output.WriteLineAsync($"    protocol: {ping.Payload[0]}.{ping.Payload[1]}").ConfigureAwait(false);
        if (ping.Payload[0] < 2)
        {
            return;
        }

        var identity = await device.ReadIdentityAsync().ConfigureAwait(false);
        await output.WriteLineAsync($"    name: {identity.Name ?? "-"}  kind: {identity.Kind}  unit: {identity.UnitId ?? "-"}").ConfigureAwait(false);

        var battery = await device.ReadBatteryAsync().ConfigureAwait(false);
        if (battery is null)
        {
            await output.WriteLineAsync("    battery: not supported").ConfigureAwait(false);
        }
        else
        {
            var percent = battery.Percent is { } value ? $"{value}%" : "-";
            var voltage = battery.Voltage is { } millivolts ? $"{millivolts}mV" : "-";
            await output.WriteLineAsync($"    battery: 0x{battery.FeatureId:X4} percent={percent} level={battery.Level} status={battery.Status} voltage={voltage}").ConfigureAwait(false);
            await output.WriteLineAsync($"    payload: {Convert.ToHexString(battery.Payload)}").ConfigureAwait(false);
        }

        if (Features)
        {
            foreach (var feature in await device.ReadFeatureTableAsync().ConfigureAwait(false))
            {
                await output.WriteLineAsync($"    feature [{feature.Index,2}] {DescribeFeature(feature)} type=0x{feature.Type:X2} version={feature.Version}").ConfigureAwait(false);
            }
        }
    }
}

internal static class BatteryTable
{
    public static async ValueTask WriteAsync(TextWriter output, IReadOnlyList<BatteryInfo> batteries)
    {
        if (batteries.Count == 0)
        {
            await output.WriteLineAsync("No battery found.").ConfigureAwait(false);
            return;
        }

        await output.WriteLineAsync($"{"Name",-28} {"Kind",-8} {"Connection",-10} {"Source",-9} {"Battery",7} {"Level",-8} {"Status",-11} {"Voltage",7} {"Online",-6} Id").ConfigureAwait(false);
        foreach (var battery in batteries)
        {
            var name = battery.Name.Length > 28 ? battery.Name[..28] : battery.Name;
            var percent = battery.Percent is { } value ? $"{value}%" : "-";
            var voltage = battery.Voltage is { } millivolts ? $"{millivolts}mV" : "-";
            var online = battery.Connected ? "yes" : "no";
            await output.WriteLineAsync($"{name,-28} {battery.Kind,-8} {battery.Connection,-10} {battery.Source,-9} {percent,7} {battery.Level,-8} {battery.Status,-11} {voltage,7} {online,-6} {battery.Id}").ConfigureAwait(false);
        }
    }
}

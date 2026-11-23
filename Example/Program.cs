using DeviceLib.Battery;

var reader = new BatteryReader();
foreach (var battery in await reader.ReadAsync().ConfigureAwait(false))
{
    var percent = battery.Percent is { } value ? $"{value}%" : "-";
    Console.WriteLine($"{battery.Name} : {percent} {battery.Status} ({battery.Connection}, {battery.Source})");
}

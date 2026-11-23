using DeviceTool.Battery;

using Smart.CommandLine.Hosting;

var builder = CommandHost.CreateBuilder(args);
builder.ConfigureCommands(commands =>
{
    commands.ConfigureRootCommand(root =>
    {
        root.WithDescription("Battery tool for wireless devices");
    });

    commands.AddCommands();
});

var host = builder.Build();
return await host.RunAsync().ConfigureAwait(false);

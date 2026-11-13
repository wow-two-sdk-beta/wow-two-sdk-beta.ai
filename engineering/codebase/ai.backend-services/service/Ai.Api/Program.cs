using Ai.Api.Configurations;
using WoW.Two.Sdk.Beta.Be.Data;

// Build and configure the host.
var builder = WebApplication.CreateBuilder(args);
builder.Configure();

// Build and configure the app.
var app = builder.Build();
if (MigrateCommand.IsCommand(args))
    return await app.RunMigrateCommandAsync(args);
await app.ConfigureAsync();

// Run.
await app.RunAsync();
return 0;

public partial class Program;

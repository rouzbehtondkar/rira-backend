using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using ViraTask.Server.Grpc;
using ViraTask.Server.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Use HTTP/2 without TLS (h2c) for local development.
// Do not expose this development endpoint directly to the public internet.
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5000, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "The SQL Server connection string 'DefaultConnection' is missing. Configure it with .NET User Secrets.");
}

builder.Services.AddGrpc();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();

app.MapGrpcService<EmployeeGrpcService>();
app.MapGet("/", () => Results.Ok("ViraTask gRPC server is running."));

app.Run();

public partial class Program
{
}

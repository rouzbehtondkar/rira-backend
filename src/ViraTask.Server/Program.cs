var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGet("/", () =>
    Results.Ok("ViraTask gRPC server is running."));

app.Run();

public partial class Program
{
}

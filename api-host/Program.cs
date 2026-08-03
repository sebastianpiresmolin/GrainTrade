using GrainTrade.ApiHost.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Orleans client, not a silo: reaches grains through their interfaces only.
builder.UseOrleansClient(client =>
{
    client.UseLocalhostClustering();
});

const string DevCors = "dev-cors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCors, policy => policy
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors(DevCors);
app.MapAccountEndpoints();
app.MapTickerEndpoints();

app.Run();

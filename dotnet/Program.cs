using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using dotnet.Data;
using MudBlazor.Services;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Formatting.Json;



var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.File(new CompactJsonFormatter(), "./logs/log.txt", rollingInterval: RollingInterval.Hour)
    .WriteTo.Console(new JsonFormatter())
    .CreateLogger();

builder.Services.AddHttpClient();
StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
builder.Logging.ClearProviders();
builder.Logging.Configure(op => op.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);
builder.Host.UseSerilog();

var greeterMeter = new Meter("OtPrGrYa.Example", "1.0.0");
var countGreetings = greeterMeter.CreateCounter<int>("greetings.count", description: "Counts the number of greetings");

// Custom ActivitySource for the application
var greeterActivitySource = new ActivitySource("dotnet.test.otel");
Action<ResourceBuilder> configureResource = r => r.AddService(
    serviceName: "otel-test-dotnet",
    serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
    serviceInstanceId: Environment.MachineName);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<WeatherForecastService>();
builder.Services.AddMudServices();
var otel = builder.Services.AddOpenTelemetry();

// AddSource needs to be the same as new ActivitySource name
otel.WithTracing(b => b.AddSource("dotnet.test.otel")
        .SetSampler(new AlwaysOnSampler())
        .AddOtlpExporter());
otel.ConfigureResource(resource => resource
    .AddService(serviceName: builder.Environment.ApplicationName));
otel.WithMetrics(metrics => metrics
    // Metrics provider from OpenTelemetry
    .AddAspNetCoreInstrumentation()
    .AddMeter(greeterMeter.Name)
    // Metrics provides by ASP.NET Core in .NET 8
    .AddMeter("Microsoft.AspNetCore.Hosting")
    .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
    .AddOtlpExporter());

var app = builder.Build();
var logger = app.Logger;

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

var rand = new Random();

async Task<String> SendGreeting()
{
    // Create a new Activity scoped to the method

    // Create a new root span
    Activity.Current = null;
    using var activity = greeterActivitySource.StartActivity("GreeterActivity");
    activity?.Start();
    activity?.AddEvent(new("Starting new Greeting Event"));

    Thread.Sleep(rand.Next(50, 100));
    // Increment the custom counter
    countGreetings.Add(1);

    logger.LogInformation("Sending greeting");
    for (var i = 0; i < rand.Next(1, 10); i++)
    {
        NestedGreeting();
    }

    activity?.AddEvent(new("Finish Greeting Event"));
    // Add a tag to the Activity
    activity?.SetTag("greeting", "Hello World!");
    activity?.SetEndTime(DateTime.Now);

    return "Hello World!";
}

void NestedGreeting()
{
    using var nestedActivity = greeterActivitySource.StartActivity("NestedActivity");
    Thread.Sleep(rand.Next(1, 10));
    nestedActivity?.AddEvent(new("Did a nested greeting"));
    logger.LogInformation("Hello from nested call!");
    nestedActivity?.Stop();
}

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.MapGet("/test", () => "Hello from otel test");
app.MapGet("/greeting", SendGreeting);

app.Run();

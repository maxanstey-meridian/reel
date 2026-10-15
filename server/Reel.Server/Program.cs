using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Reel.Server;
using Reel.Server.Library;
using Reel.Server.Media;
using Reel.Server.Pairing;
using Reel.Server.Playback;
using Reel.Server.Storage;
using Reel.Server.Web;
using Rivet;

var builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddOptions<ReelOptions>()
    .BindConfiguration(ReelOptions.Section)
    .ValidateDataAnnotations()
    .Validate(
        options => options.Compression == "off" || options.TranscodeHeight is not null,
        "Reel:Compression must be \"off\" or an even video height such as \"720p\"."
    )
    .ValidateOnStart();

builder.Services.AddSingleton<MediaPathResolver>();
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<PlaybackSessionService>();
builder.Services.AddSingleton<NextEpisodeService>();
builder.Services.AddSingleton<FileSystemScanner>();
builder.Services.AddSingleton<LibraryIndexReconciler>();
builder.Services.AddHostedService<StartupLibraryIndex>();
builder.Services.AddHostedService<MdnsAdvertiser>();
builder.Services.AddSingleton<SqliteMediaStore>();
builder.Services.AddSingleton<SqliteProgressStore>();
builder.Services.AddSingleton<SqliteFavouritesStore>();
builder.Services.AddSingleton<FfprobeSubtitleDiscovery>();
builder.Services.AddSingleton<FfmpegSubtitleExtractor>();
builder.Services.AddSingleton<FfmpegTrickplayGenerator>();
builder.Services.AddSingleton<FfmpegThumbnailGenerator>();

// Undeclared failures: a Rivet contract violation keeps Rivet's envelope; anything else becomes a ProblemDetails 500.
// Both carry the code and message every declared ErrorResponse has, and both are logged.
builder.Services.AddExceptionHandler<RivetContractViolationHandler>();
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
    {
        var problem = context.ProblemDetails;
        problem.Extensions["code"] = problem.Status >= 500 ? "server_error" : "invalid_request";
        problem.Extensions["message"] = problem.Detail ?? problem.Title;
    }
);
builder.Services.Configure<ExceptionHandlerOptions>(options =>
    options.SuppressDiagnosticsCallback = _ => false
);

// Development otherwise throws on unreadable requests instead of answering 400 as production does.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
    )
);

var app = builder.Build();

await app.Services.GetRequiredService<DatabaseService>().MigrateAsync();

app.UseExceptionHandler();
app.UseMiddleware<PrivateNetworkMiddleware>();
app.UseMiddleware<SecretKeyMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthEndpoints();
app.MapConnectEndpoints();
app.MapPairEndpoints();
app.MapLibraryEndpoints();
app.MapTagEndpoints();
app.MapThumbnailEndpoints();
app.MapStreamEndpoints();
app.MapProgressEndpoints();
app.MapFavouritesEndpoints();
app.MapSearchEndpoints();
app.MapSubtitleEndpoints();
app.MapTrickplayEndpoints();
app.MapPlaybackSessionEndpoints();
app.MapWebAppEndpoints();

var reel = app.Services.GetRequiredService<IOptions<ReelOptions>>().Value;
app.Urls.Clear();
app.Urls.Add($"http://0.0.0.0:{reel.Port}");

await app.RunAsync();

public partial class Program { }

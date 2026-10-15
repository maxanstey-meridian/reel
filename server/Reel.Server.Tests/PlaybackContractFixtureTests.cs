using System.Text.Json;
using FluentAssertions;
using Reel.Server.Contracts.Playback;

namespace Reel.Server.Tests;

// The TV parses this checked-in JSON in its own unit tests, so a server-side wire change
// fails here first instead of on the television.
public sealed class PlaybackContractFixtureTests : IDisposable
{
    private readonly TempMediaRoot _media = new();

    public void Dispose() => _media.Dispose();

    [Fact]
    public void PlaybackSessionEnvelope_MatchesTheTvFixture()
    {
        using var factory = _media.CreateFactory();
        var options = new JsonSerializerOptions(factory.JsonOptions()) { WriteIndented = true };
        var created = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var envelope = new PlaybackSessionEnvelope(
            new PlaybackSessionResponse(
                Guid.Parse("7d3f7a8e-2b1c-4d5e-9f60-1a2b3c4d5e6f"),
                4,
                created,
                created.AddSeconds(42),
                new PlaybackIntentResponse(
                    "Shows/Series/Series.S01E02.mkv",
                    "Series S01E02",
                    PlaybackDesiredStateDto.Paused,
                    1,
                    new SeekRequestResponse(2, 612.5, 4)
                ),
                new PlaybackObservationResponse(
                    9,
                    3,
                    1,
                    PlaybackObservationStateDto.Playing,
                    "Shows/Series/Series.S01E02.mkv",
                    600.25,
                    1440,
                    1,
                    null,
                    null,
                    created.AddSeconds(41)
                ),
                TvConnected: true
            )
        );
        var json = JsonSerializer.Serialize(envelope, options) + "\n";
        var fixture = Path.Combine(
            RepositoryRoot(),
            "tv",
            "app",
            "src",
            "test",
            "resources",
            "contract",
            "playback-session.json"
        );

        // After a deliberate wire change, rerun with REEL_UPDATE_CONTRACT_FIXTURE=1 to rewrite it.
        if (Environment.GetEnvironmentVariable("REEL_UPDATE_CONTRACT_FIXTURE") == "1")
        {
            File.WriteAllText(fixture, json);
        }

        File.Exists(fixture)
            .Should()
            .BeTrue("the TV's contract fixture is checked in at {0}", fixture);
        json.Should()
            .Be(
                File.ReadAllText(fixture),
                "the TV contract fixture must match the server's serialized session"
            );
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (
            directory is not null
            && !Directory.Exists(Path.Combine(directory.FullName, "tv", "app"))
        )
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}

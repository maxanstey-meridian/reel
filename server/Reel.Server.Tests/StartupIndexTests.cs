using System.Net;
using Dapper;
using FluentAssertions;

namespace Reel.Server.Tests;

public sealed class StartupIndexTests : IDisposable
{
    private readonly TempMediaRoot _media = new();

    public void Dispose() => _media.Dispose();

    [Fact]
    public async Task Startup_WithUnreadableFolder_StillServesAndKeepsExistingIndex()
    {
        _media.Write("existing.mp4");
        await _media.Reconciler.ReconcileAsync(CancellationToken.None);
        _media.Write("Locked/hidden.mp4");
        File.SetUnixFileMode(Path.Combine(_media.Root, "Locked"), UnixFileMode.None);

        using var factory = _media.CreateFactory();
        await factory.WaitForStartupIndexAsync();
        using var client = factory.CreateAuthenticatedClient();

        (await client.GetAsync("/api/health")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/library")).StatusCode.Should().Be(HttpStatusCode.OK);
        using var connection = _media.Database.GetConnection();
        connection.ExecuteScalar<int>("SELECT COUNT(*) FROM media").Should().Be(1);
    }
}

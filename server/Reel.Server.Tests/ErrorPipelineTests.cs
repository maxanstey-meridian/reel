using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Reel.Server.Common;

namespace Reel.Server.Tests;

// Changing PATH affects the whole process, so these run alone after the parallel tests.
[CollectionDefinition(nameof(ProcessEnvironment), DisableParallelization = true)]
public sealed class ProcessEnvironment;

[Collection(nameof(ProcessEnvironment))]
public sealed class ErrorPipelineTests : IDisposable
{
    private readonly TempMediaRoot _media = new();

    public void Dispose() => _media.Dispose();

    [Fact]
    public async Task MissingFfmpeg_IsALoggedServerError()
    {
        _media.Write("a.mp4");
        using var factory = _media.CreateFactory(builder =>
            builder.ConfigureLogging(logging => logging.AddFakeLogging())
        );
        using var client = factory.CreateAuthenticatedClient();
        var path = Environment.GetEnvironmentVariable("PATH");

        HttpResponseMessage response;
        Environment.SetEnvironmentVariable("PATH", _media.Root);
        try
        {
            response = await client.GetAsync("/api/files/stream/info?path=a.mp4");
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", path);
        }

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await response.Content.ReadFromJsonAsync<ErrorResponse>())!
            .Code.Should()
            .Be("server_error");
        factory
            .Services.GetFakeLogCollector()
            .GetSnapshot()
            .Should()
            .Contain(log => log.Level == LogLevel.Error && log.Exception is Win32Exception);
    }
}

using FluentAssertions;
using Reel.Server.Media;

namespace Reel.Server.Tests;

public sealed class SingleFlightTests
{
    private readonly SingleFlight<int> _flights = new();

    [Fact]
    public async Task ConcurrentCallersShareOneRun()
    {
        var runs = 0;
        var release = new TaskCompletionSource();
        Func<Task<int>> work = async () =>
        {
            Interlocked.Increment(ref runs);
            await release.Task;
            return 7;
        };

        var first = _flights.RunAsync("a", work, CancellationToken.None);
        var second = _flights.RunAsync("a", work, CancellationToken.None);
        release.SetResult();

        (await Task.WhenAll(first, second)).Should().Equal(7, 7);
        runs.Should().Be(1);
    }

    [Fact]
    public async Task RunKeepsGoingWhenItsCallerGivesUp()
    {
        var release = new TaskCompletionSource();
        using var giveUp = new CancellationTokenSource();
        var abandoned = _flights.RunAsync(
            "a",
            async () =>
            {
                await release.Task;
                return 7;
            },
            giveUp.Token
        );

        giveUp.Cancel();
        await abandoned.Invoking(task => task).Should().ThrowAsync<OperationCanceledException>();
        var joined = _flights.RunAsync("a", () => Task.FromResult(0), CancellationToken.None);
        release.SetResult();

        (await joined).Should().Be(7);
    }

    [Fact]
    public async Task FinishedRunIsForgottenSoTheNextCallRunsAgain()
    {
        var runs = 0;
        Func<Task<int>> work = () => Task.FromResult(Interlocked.Increment(ref runs));

        await _flights.RunAsync("a", work, CancellationToken.None);
        await _flights.RunAsync("a", work, CancellationToken.None);

        runs.Should().Be(2);
    }
}

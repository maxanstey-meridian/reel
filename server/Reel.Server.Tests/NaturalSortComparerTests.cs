using FluentAssertions;
using Reel.Server.Infrastructure.FileSystem;

namespace Reel.Server.Tests;

public sealed class NaturalSortComparerTests
{
    private readonly NaturalSortComparer _comparer = NaturalSortComparer.Instance;

    [Fact]
    public void SortsNumericSegmentsAsIntegers()
    {
        var input = new List<string> { "Episode 1", "Episode 10", "Episode 2" };

        input.Sort(_comparer);

        input.Should().ContainInOrder("Episode 1", "Episode 2", "Episode 10");
    }

    [Fact]
    public void SortsMixedAlphaNumeric()
    {
        var input = new List<string> { "s1e10", "s2e1", "s1e1", "s1e2" };

        input.Sort(_comparer);

        input.Should().ContainInOrder("s1e1", "s1e2", "s1e10", "s2e1");
    }

    [Fact]
    public void IdenticalStringsCompareEqual()
    {
        _comparer.Compare("abc123", "abc123").Should().Be(0);
    }

    [Fact]
    public void PureTextStringsSortLexically()
    {
        var input = new List<string> { "cherry", "apple", "banana" };

        input.Sort(_comparer);

        input.Should().ContainInOrder("apple", "banana", "cherry");
    }

    [Fact]
    public void CaseInsensitive()
    {
        _comparer.Compare("ABC", "abc").Should().Be(0);
    }

    [Fact]
    public void HandlesNulls()
    {
        _comparer.Compare(null, "a").Should().BeNegative();
        _comparer.Compare("a", null).Should().BePositive();
        _comparer.Compare(null, null).Should().Be(0);
    }

    [Fact]
    public void SortsRealEpisodeFilenames()
    {
        var input = new List<string>
        {
            "Breaking Bad S01E03.mkv",
            "Breaking Bad S01E01.mkv",
            "Breaking Bad S01E10.mkv",
            "Breaking Bad S01E02.mkv",
        };

        input.Sort(_comparer);

        input.Should().ContainInOrder(
            "Breaking Bad S01E01.mkv",
            "Breaking Bad S01E02.mkv",
            "Breaking Bad S01E03.mkv",
            "Breaking Bad S01E10.mkv"
        );
    }

    [Fact]
    public void SortsPrefixSubstringsCorrectly()
    {
        var input = new List<string> { "file10", "file1", "file2" };

        input.Sort(_comparer);

        input.Should().ContainInOrder("file1", "file2", "file10");
    }
}

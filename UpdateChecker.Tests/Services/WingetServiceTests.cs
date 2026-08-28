using UpdateChecker.Services;
using Xunit;

namespace UpdateChecker.Tests.Services;

public sealed class WingetServiceTests
{
    [Fact]
    public async Task ReadBoundedAsync_ReturnsCompleteTextWithinLimit()
    {
        using var reader = new StringReader("available updates");

        BoundedText result = await WingetService.ReadBoundedAsync(
            reader,
            maximumCharacters: 64
        );

        Assert.Equal("available updates", result.Value);
        Assert.False(result.WasTruncated);
    }

    [Fact]
    public async Task ReadBoundedAsync_CapsMemoryButStillDrainsReader()
    {
        using var reader = new StringReader(new string('x', 10_000));

        BoundedText result = await WingetService.ReadBoundedAsync(
            reader,
            maximumCharacters: 128
        );

        Assert.Equal(128, result.Value.Length);
        Assert.True(result.WasTruncated);
        Assert.Equal(-1, reader.Read());
    }
}

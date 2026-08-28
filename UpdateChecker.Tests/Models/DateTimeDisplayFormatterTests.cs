using UpdateChecker.Models;
using Xunit;

namespace UpdateChecker.Tests.Models;

public sealed class DateTimeDisplayFormatterTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(-30, "just now")]
    [InlineData(-480, "8 minutes ago")]
    [InlineData(-7200, "2 hours ago")]
    [InlineData(300, "in 5 minutes")]
    public void FormatRelative_DescribesPastAndFuture(
        int offsetSeconds,
        string expected)
    {
        string result = DateTimeDisplayFormatter.FormatRelative(
            Now.AddSeconds(offsetSeconds),
            Now
        );

        Assert.Equal(expected, result);
    }
}

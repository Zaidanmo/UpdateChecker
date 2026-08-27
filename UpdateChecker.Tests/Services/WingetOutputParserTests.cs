using UpdateChecker.Services;
using Xunit;

namespace UpdateChecker.Tests.Services;

public sealed class WingetOutputParserTests
{
    [Fact]
    public void Parse_ReturnsUpdatesFromStandardTable()
    {
        string output = CreateTable(
            Row("Example App", "Vendor.Example", "1.2.0", "2.0.0", "winget"),
            Row("Another App", "Vendor.Another", "5.1", "5.2", "winget")
        );

        var updates = WingetOutputParser.Parse(output);

        Assert.Collection(
            updates,
            first =>
            {
                Assert.Equal("Example App", first.Name);
                Assert.Equal("Vendor.Example", first.Id);
                Assert.Equal("1.2.0", first.InstalledVersion);
                Assert.Equal("2.0.0", first.AvailableVersion);
            },
            second => Assert.Equal("Another App", second.Name)
        );
    }

    [Fact]
    public void Parse_RemovesAnsiSequences()
    {
        string header = Row("Name", "Id", "Version", "Available", "Source");
        string output = string.Join(
            '\n',
            $"\u001b[32m{header}\u001b[0m",
            new string('-', header.Length),
            Row("Example", "Vendor.Example", "1.0", "1.1", "winget")
        );

        var update = Assert.Single(WingetOutputParser.Parse(output));

        Assert.Equal("Example", update.Name);
    }

    [Fact]
    public void Parse_AcceptsTableWithoutSourceColumn()
    {
        string header = RowWithoutSource("Name", "Id", "Version", "Available");
        string output = string.Join(
            '\n',
            header,
            new string('-', header.Length),
            RowWithoutSource("Example", "Vendor.Example", "1.0", "1.1")
        );

        var update = Assert.Single(WingetOutputParser.Parse(output));

        Assert.Equal("1.1", update.AvailableVersion);
    }

    [Theory]
    [InlineData("No installed package found matching input criteria.")]
    [InlineData("No applicable upgrade found.")]
    [InlineData("No package found matching input criteria.")]
    public void Parse_ReturnsEmptyForRecognizedNoUpgradeMessage(string output)
    {
        Assert.Empty(WingetOutputParser.Parse(output));
    }

    [Fact]
    public void Parse_ReturnsEmptyForRecognizedEmptyTable()
    {
        string header = Row("Name", "Id", "Version", "Available", "Source");
        string output = string.Join('\n', header, new string('-', header.Length));

        Assert.Empty(WingetOutputParser.Parse(output));
    }

    [Fact]
    public void Parse_ThrowsForUnsupportedOutputInsteadOfReportingNoUpdates()
    {
        Assert.Throws<WingetOutputParseException>(
            () => WingetOutputParser.Parse("Unexpected localized output")
        );
    }

    private static string CreateTable(params string[] rows)
    {
        string header = Row("Name", "Id", "Version", "Available", "Source");

        return string.Join(
            '\n',
            new[] { header, new string('-', header.Length) }
                .Concat(rows)
                .Append($"{rows.Length} upgrades available.")
        );
    }

    private static string Row(
        string name,
        string id,
        string version,
        string available,
        string source)
    {
        return string.Concat(
            name.PadRight(30),
            id.PadRight(35),
            version.PadRight(18),
            available.PadRight(18),
            source
        );
    }

    private static string RowWithoutSource(
        string name,
        string id,
        string version,
        string available)
    {
        return string.Concat(
            name.PadRight(30),
            id.PadRight(35),
            version.PadRight(18),
            available
        );
    }
}

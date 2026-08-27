using UpdateChecker.Models;
using Xunit;

namespace UpdateChecker.Tests.Models;

public sealed class VersionClassifierTests
{
    [Theory]
    [InlineData("1.2.3", "1.9.0", false)]
    [InlineData("1.9.0", "2.0.0", true)]
    [InlineData("v3.4", "v4.0", true)]
    [InlineData("2024.3", "2025.1", true)]
    [InlineData("release", "preview", false)]
    [InlineData("1.0", "1.0.1", false)]
    public void HasMajorVersionChange_ClassifiesLeadingNumericComponent(
        string installedVersion,
        string availableVersion,
        bool expected)
    {
        bool actual = VersionClassifier.HasMajorVersionChange(
            installedVersion,
            availableVersion
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AppUpdateInfo_ExposesClassifierResult()
    {
        var update = new AppUpdateInfo(
            "Example",
            "Vendor.Example",
            "1.8.0",
            "2.0.0"
        );

        Assert.True(update.HasMajorVersionChange);
    }
}

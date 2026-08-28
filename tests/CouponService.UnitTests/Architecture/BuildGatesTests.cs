using System.Xml.Linq;

namespace CouponService.UnitTests.Architecture;

public sealed class BuildGatesTests
{
    [Fact]
    public void Directory_Build_props_enables_analyzers_and_treats_warnings_as_errors()
    {
        var buildPropsPath = Path.Combine(RepositoryRoot.Find(), "Directory.Build.props");
        Assert.True(File.Exists(buildPropsPath), $"Expected {buildPropsPath} to exist.");

        var document = XDocument.Load(buildPropsPath);
        var propertyGroups = document.Root?
            .Elements("PropertyGroup")
            .SelectMany(group => group.Elements())
            .ToDictionary(element => element.Name.LocalName, element => element.Value, StringComparer.OrdinalIgnoreCase);

        Assert.NotNull(propertyGroups);
        Assert.True(propertyGroups.TryGetValue("EnableNETAnalyzers", out var analyzersEnabled));
        Assert.Equal("true", analyzersEnabled, ignoreCase: true);
        Assert.True(propertyGroups.TryGetValue("TreatWarningsAsErrors", out var warningsAsErrors));
        Assert.Equal("true", warningsAsErrors, ignoreCase: true);
    }
}

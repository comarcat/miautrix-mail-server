namespace Miautrix.Mail.UnitTests;

public sealed class UnitTestProjectSmokeTests
{
    [Fact]
    public void Repository_targets_net10_and_projects_do_not_override_it()
    {
        var root = FindRepositoryRoot();
        var props = System.Xml.Linq.XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        Assert.Equal("net10.0", props.Root?.Element("PropertyGroup")?.Element("TargetFramework")?.Value);

        using var global = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "global.json")));
        Assert.StartsWith("10.", global.RootElement.GetProperty("sdk").GetProperty("version").GetString());

        foreach (var project in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
                     .Concat(Directory.EnumerateFiles(Path.Combine(root, "tests"), "*.csproj", SearchOption.AllDirectories)))
        {
            var xml = System.Xml.Linq.XDocument.Load(project);
            Assert.DoesNotContain(xml.Descendants().Where(e => e.Name.LocalName is "TargetFramework" or "TargetFrameworks"),
                element => !string.IsNullOrWhiteSpace(element.Value));
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find repository root.");
    }

    [Fact]
    public void Test_project_contains_at_least_one_discoverable_test()
    {
        Assert.True(true);
    }

    [Fact]
    public void Application_assembly_does_not_reference_MimeKit()
    {
        var references = typeof(Miautrix.Mail.Application.Mail.Mime.IMimeMessageParser).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, reference => reference.Name?.StartsWith("MimeKit", StringComparison.OrdinalIgnoreCase) == true);
    }
}

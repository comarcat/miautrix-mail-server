using Miautrix.Mail.Infrastructure.Deployment;
using Xunit;

namespace Miautrix.Mail.IntegrationTests.BlueGreen;

[Trait("Category", "BlueGreen")]
public class BlueGreenUpdateTests
{
    private readonly string _tempRoot;
    private readonly string _currentDeploymentDir;
    private readonly string _newDeploymentDir;
    private readonly DeploymentManager _deploymentManager;

    public BlueGreenUpdateTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"miautrix_deploy_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);

        _currentDeploymentDir = Path.Combine(_tempRoot, "releases", "v1.0.0");
        _newDeploymentDir = Path.Combine(_tempRoot, "releases", "v1.1.0");

        Directory.CreateDirectory(_currentDeploymentDir);
        Directory.CreateDirectory(_newDeploymentDir);

        File.WriteAllText(Path.Combine(_currentDeploymentDir, "version.txt"), "1.0.0");
        File.WriteAllText(Path.Combine(_newDeploymentDir, "version.txt"), "1.1.0");

        _deploymentManager = new DeploymentManager(_tempRoot);

        // Initial release switch (v1.0.0 is active)
        _deploymentManager.StageAndSwitchAsync(_currentDeploymentDir, _ => Task.FromResult(true)).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task StagedReleaseFailsHealthGate_LeavesPreviousReleaseActive()
    {
        // Arrange
        Assert.Equal(_currentDeploymentDir, _deploymentManager.GetActiveReleasePath());

        // Act: Stage new release with failing health gate
        var switched = await _deploymentManager.StageAndSwitchAsync(
            _newDeploymentDir,
            _ => Task.FromResult(false) // health check fails
        );

        // Assert: Switched is false, and active release remains v1.0.0
        Assert.False(switched);
        Assert.Equal(_currentDeploymentDir, _deploymentManager.GetActiveReleasePath());
    }

    [Fact]
    public async Task StagedReleasePassesHealthGate_FlipsToNewRelease()
    {
        // Arrange
        Assert.Equal(_currentDeploymentDir, _deploymentManager.GetActiveReleasePath());

        // Act: Stage new release with passing health gate
        var switched = await _deploymentManager.StageAndSwitchAsync(
            _newDeploymentDir,
            _ => Task.FromResult(true) // health check passes
        );

        // Assert: Switched is true, and active release points to v1.1.0
        Assert.True(switched);
        Assert.Equal(_newDeploymentDir, _deploymentManager.GetActiveReleasePath());
    }
}

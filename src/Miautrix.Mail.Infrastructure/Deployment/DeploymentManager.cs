namespace Miautrix.Mail.Infrastructure.Deployment;

public interface IDeploymentManager
{
    string GetActiveReleasePath();
    Task<bool> StageAndSwitchAsync(string newReleasePath, Func<string, Task<bool>> healthCheck, CancellationToken cancellationToken = default);
}

public class DeploymentManager : IDeploymentManager
{
    private readonly string _baseDir;
    private readonly string _symlinkPath;

    public DeploymentManager(string baseDir)
    {
        _baseDir = baseDir;
        _symlinkPath = Path.Combine(baseDir, "current");
    }

    public string GetActiveReleasePath()
    {
        if (!File.Exists(_symlinkPath)) return string.Empty;

        // On Windows, read file content as pointer; on Linux, resolve symlink
        if (OperatingSystem.IsWindows())
            return File.ReadAllText(_symlinkPath).Trim();

        return File.ResolveLinkTarget(_symlinkPath, true)?.FullName ?? string.Empty;
    }

    public async Task<bool> StageAndSwitchAsync(string newReleasePath, Func<string, Task<bool>> healthCheck, CancellationToken cancellationToken = default)
    {
        if (await healthCheck(newReleasePath))
        {
            Switch(newReleasePath);
            return true;
        }
        return false;
    }

    private void Switch(string targetPath)
    {
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(_symlinkPath, targetPath);
        }
        else
        {
            if (File.Exists(_symlinkPath)) File.Delete(_symlinkPath);
            File.CreateSymbolicLink(_symlinkPath, targetPath);
        }
    }
}

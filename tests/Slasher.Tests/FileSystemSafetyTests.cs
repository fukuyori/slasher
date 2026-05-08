using Slasher.Api;
using Slasher.Files;
using Xunit;

namespace Slasher.Tests;

public sealed class FileSystemSafetyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "slasher-file-safety-tests", Guid.NewGuid().ToString("N"));
    private readonly FileSystemAutomationService _files = new();

    public FileSystemSafetyTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void DeleteFile_RequiresDestructiveApproval()
    {
        var path = Path.Combine(_root, "delete.txt");
        File.WriteAllText(path, "delete me");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _files.DeleteFile(new FileOperationRequest(path)));

        Assert.Contains("allowDestructive=true", ex.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void DeleteFile_DryRunReturnsPlanWithoutDeleting()
    {
        var path = Path.Combine(_root, "delete.txt");
        File.WriteAllText(path, "delete me");

        var plan = Assert.IsType<FileOperationPlan>(
            _files.DeleteFile(new FileOperationRequest(path, DryRun: true)));

        Assert.Equal("file.delete", plan.Operation);
        Assert.True(plan.DryRun);
        Assert.True(plan.Destructive);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void DeleteFile_AllowsExplicitDestructiveApproval()
    {
        var path = Path.Combine(_root, "delete.txt");
        File.WriteAllText(path, "delete me");

        _files.DeleteFile(new FileOperationRequest(path, AllowDestructive: true));

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void DeleteFolder_RequiresDestructiveApproval()
    {
        var path = Path.Combine(_root, "folder");
        Directory.CreateDirectory(path);

        Assert.Throws<InvalidOperationException>(() =>
            _files.DeleteFolder(new FileOperationRequest(path)));

        Assert.True(Directory.Exists(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

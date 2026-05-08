using Slasher.Files;
using Xunit;

namespace Slasher.Tests;

public sealed class FileWatcherServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "slasher-watcher-tests", Guid.NewGuid().ToString("N"));
    private readonly FileWatcherService _watchers = new();

    public FileWatcherServiceTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task Watcher_CapturesCreatedFile()
    {
        var watcher = _watchers.Start(new FileWatcherStartRequest(_root, Filter: "*.txt", Name: "test"));

        var file = Path.Combine(_root, "created.txt");
        await File.WriteAllTextAsync(file, "hello");
        var events = await WaitForEventsAsync(watcher.WatcherId);

        Assert.Equal("test", watcher.Name);
        Assert.Contains(events, item => item.ChangeType == "created" && item.FullPath == file);
    }

    [Fact]
    public void Stop_RemovesWatcher()
    {
        var watcher = _watchers.Start(new FileWatcherStartRequest(_root));

        Assert.True(_watchers.Stop(watcher.WatcherId));
        Assert.False(_watchers.Stop(watcher.WatcherId));
    }

    [Fact]
    public void Start_RejectsMissingDirectory()
    {
        Assert.Throws<DirectoryNotFoundException>(() =>
            _watchers.Start(new FileWatcherStartRequest(Path.Combine(_root, "missing"))));
    }

    public void Dispose()
    {
        _watchers.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<IReadOnlyList<FileWatcherEvent>> WaitForEventsAsync(string watcherId)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var events = _watchers.GetEvents(watcherId);
            if (events.Count > 0)
            {
                return events;
            }

            await Task.Delay(100);
        }

        return _watchers.GetEvents(watcherId);
    }
}

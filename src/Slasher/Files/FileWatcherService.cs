using System.Collections.Concurrent;

namespace Slasher.Files;

public sealed class FileWatcherService : IDisposable
{
    private const int MaxEventsPerWatcher = 500;
    private readonly ConcurrentDictionary<string, WatcherState> _watchers = new(StringComparer.OrdinalIgnoreCase);

    public FileWatcherInfo Start(FileWatcherStartRequest request)
    {
        if (!Directory.Exists(request.Path))
        {
            throw new DirectoryNotFoundException($"Watch path '{request.Path}' was not found.");
        }

        var id = $"watch_{Guid.NewGuid():N}";
        var fullPath = Path.GetFullPath(request.Path);
        var filter = string.IsNullOrWhiteSpace(request.Filter) ? "*.*" : request.Filter!;
        var watcher = new FileSystemWatcher(fullPath, filter)
        {
            IncludeSubdirectories = request.IncludeSubdirectories,
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.DirectoryName
                | NotifyFilters.LastWrite
                | NotifyFilters.CreationTime
                | NotifyFilters.Size
        };

        var state = new WatcherState(
            id,
            string.IsNullOrWhiteSpace(request.Name) ? id : request.Name!,
            fullPath,
            filter,
            request.IncludeSubdirectories,
            DateTimeOffset.UtcNow,
            watcher);

        watcher.Created += (_, args) => state.Add("created", args.FullPath, null);
        watcher.Changed += (_, args) => state.Add("changed", args.FullPath, null);
        watcher.Deleted += (_, args) => state.Add("deleted", args.FullPath, null);
        watcher.Renamed += (_, args) => state.Add("renamed", args.FullPath, args.OldFullPath);
        watcher.Error += (_, _) => state.Add("error", fullPath, null);

        if (!_watchers.TryAdd(id, state))
        {
            watcher.Dispose();
            throw new InvalidOperationException("Could not register file watcher.");
        }

        watcher.EnableRaisingEvents = true;
        return state.ToInfo();
    }

    public IReadOnlyList<FileWatcherInfo> List()
    {
        return _watchers.Values
            .OrderBy(state => state.StartedAt)
            .Select(state => state.ToInfo())
            .ToArray();
    }

    public IReadOnlyList<FileWatcherEvent> GetEvents(string watcherId, int? limit = null)
    {
        var state = GetState(watcherId);
        return state.GetEvents(limit ?? MaxEventsPerWatcher);
    }

    public bool Stop(string watcherId)
    {
        if (!_watchers.TryRemove(watcherId, out var state))
        {
            return false;
        }

        state.Dispose();
        return true;
    }

    public void Dispose()
    {
        foreach (var watcherId in _watchers.Keys.ToArray())
        {
            Stop(watcherId);
        }
    }

    private WatcherState GetState(string watcherId)
    {
        return _watchers.TryGetValue(watcherId, out var state)
            ? state
            : throw new KeyNotFoundException($"Watcher '{watcherId}' was not found.");
    }

    private sealed class WatcherState : IDisposable
    {
        private readonly object _lock = new();
        private readonly Queue<FileWatcherEvent> _events = new();
        private readonly FileSystemWatcher _watcher;

        public WatcherState(
            string watcherId,
            string name,
            string path,
            string filter,
            bool includeSubdirectories,
            DateTimeOffset startedAt,
            FileSystemWatcher watcher)
        {
            WatcherId = watcherId;
            Name = name;
            Path = path;
            Filter = filter;
            IncludeSubdirectories = includeSubdirectories;
            StartedAt = startedAt;
            _watcher = watcher;
        }

        public string WatcherId { get; }

        public string Name { get; }

        public string Path { get; }

        public string Filter { get; }

        public bool IncludeSubdirectories { get; }

        public DateTimeOffset StartedAt { get; }

        public void Add(string changeType, string fullPath, string? oldFullPath)
        {
            lock (_lock)
            {
                _events.Enqueue(new FileWatcherEvent(WatcherId, changeType, fullPath, oldFullPath, DateTimeOffset.UtcNow));
                while (_events.Count > MaxEventsPerWatcher)
                {
                    _events.Dequeue();
                }
            }
        }

        public IReadOnlyList<FileWatcherEvent> GetEvents(int limit)
        {
            lock (_lock)
            {
                var safeLimit = Math.Clamp(limit, 1, MaxEventsPerWatcher);
                return _events.TakeLast(safeLimit).ToArray();
            }
        }

        public FileWatcherInfo ToInfo()
        {
            lock (_lock)
            {
                return new FileWatcherInfo(
                    WatcherId,
                    Name,
                    Path,
                    Filter,
                    IncludeSubdirectories,
                    StartedAt,
                    _watcher.EnableRaisingEvents,
                    _events.Count);
            }
        }

        public void Dispose()
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }
    }
}

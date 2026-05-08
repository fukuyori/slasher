namespace Slasher.Files;

public sealed record FileWatcherStartRequest(
    string Path,
    string? Filter = null,
    bool IncludeSubdirectories = false,
    string? Name = null);

public sealed record FileWatcherInfo(
    string WatcherId,
    string Name,
    string Path,
    string Filter,
    bool IncludeSubdirectories,
    DateTimeOffset StartedAt,
    bool Running,
    int EventCount);

public sealed record FileWatcherEvent(
    string WatcherId,
    string ChangeType,
    string FullPath,
    string? OldFullPath,
    DateTimeOffset Timestamp);

public sealed record FileWatcherStartResponse(FileWatcherInfo Watcher);

public sealed record FileWatcherListResponse(IReadOnlyList<FileWatcherInfo> Watchers);

public sealed record FileWatcherEventsResponse(
    string WatcherId,
    IReadOnlyList<FileWatcherEvent> Events);

public sealed record FileWatcherStopResponse(string WatcherId, bool Stopped);

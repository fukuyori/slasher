namespace Slasher.Files;

public sealed record FileOperationPlan(
    string Operation,
    bool DryRun,
    bool Destructive,
    bool Allowed,
    IReadOnlyList<string> Targets,
    string? Destination = null,
    bool Recursive = false,
    bool Overwrite = false);

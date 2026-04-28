using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private async Task<int> ExecuteIfBlockAsync(
        IReadOnlyList<ScriptLine> lines,
        int index,
        int end,
        ScriptExecutionState state,
        ScriptRunRequest request,
        CancellationToken cancellationToken)
    {
        var line = lines[index];
        var block = FindBlockEnd(
            lines,
            index,
            end,
            "if",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "endif" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "else" });
        var condition = line.Command[2..].Trim();
        if (EvaluateCondition(condition, state.ResolveVariables(line)))
        {
            await ExecuteBlockAsync(lines, index + 1, block.ElseIndex ?? block.End, state, request, cancellationToken);
        }
        else if (block.ElseIndex is not null)
        {
            await ExecuteBlockAsync(lines, block.ElseIndex.Value + 1, block.End, state, request, cancellationToken);
        }

        return block.End + 1;
    }
}


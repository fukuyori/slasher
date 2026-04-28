using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private async Task<int> ExecuteRepeatBlockAsync(
        IReadOnlyList<ScriptLine> lines,
        int index,
        int end,
        ScriptExecutionState state,
        ScriptRunRequest request,
        CancellationToken cancellationToken)
    {
        var line = lines[index];
        var block = FindBlockEnd(lines, index, end, "repeat", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "endrepeat" });
        var countText = ExpandVariables(line.Command["repeat".Length..].Trim(), state.ResolveVariables(line));
        var count = ParseInt(countText, "repeat count");
        if (count < 0)
        {
            throw new ScriptCommandException("invalid_repeat_count", "repeat count must be zero or positive.");
        }

        for (var i = 0; i < count; i++)
        {
            state.AssignVariable(line, "index", i, ScriptVariableScope.Local);
            state.AssignVariable(line, "iteration", i + 1, ScriptVariableScope.Local);
            await ExecuteBlockAsync(lines, index + 1, block.End, state, request, cancellationToken);
            if (ShouldStopAfterNestedBlock(state, request))
            {
                return block.End + 1;
            }
        }

        return block.End + 1;
    }

    private async Task<int> ExecuteForEachBlockAsync(
        IReadOnlyList<ScriptLine> lines,
        int index,
        int end,
        ScriptExecutionState state,
        ScriptRunRequest request,
        CancellationToken cancellationToken)
    {
        var line = lines[index];
        var block = FindBlockEnd(lines, index, end, "foreach", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "endforeach" });
        var tokens = ParseCommandLine(ExpandVariables(line.Command, state.ResolveVariables(line)));
        if (tokens.Count < 4 || !tokens[2].Equals("in", StringComparison.OrdinalIgnoreCase))
        {
            throw new ScriptCommandException("invalid_foreach", "foreach syntax is: foreach item in arrayName");
        }

        var itemName = tokens[1];
        var arrayName = tokens[3];
        var items = RequireArray(state.ResolveVariables(line), arrayName).ToArray();
        for (var i = 0; i < items.Length; i++)
        {
            state.AssignVariable(line, itemName, items[i], ScriptVariableScope.Local);
            state.AssignVariable(line, "index", i, ScriptVariableScope.Local);
            state.AssignVariable(line, "iteration", i + 1, ScriptVariableScope.Local);
            await ExecuteBlockAsync(lines, index + 1, block.End, state, request, cancellationToken);
            if (ShouldStopAfterNestedBlock(state, request))
            {
                return block.End + 1;
            }
        }

        return block.End + 1;
    }

    private async Task<int> ExecuteWhileBlockAsync(
        IReadOnlyList<ScriptLine> lines,
        int index,
        int end,
        ScriptExecutionState state,
        ScriptRunRequest request,
        CancellationToken cancellationToken)
    {
        var line = lines[index];
        var block = FindBlockEnd(lines, index, end, "while", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "endwhile" });
        var condition = line.Command["while".Length..].Trim();
        for (var i = 0; EvaluateCondition(condition, state.ResolveVariables(line)); i++)
        {
            if (i >= 1000)
            {
                throw new ScriptCommandException("while_limit_exceeded", "while loop exceeded 1000 iterations.");
            }

            state.AssignVariable(line, "index", i, ScriptVariableScope.Local);
            state.AssignVariable(line, "iteration", i + 1, ScriptVariableScope.Local);
            await ExecuteBlockAsync(lines, index + 1, block.End, state, request, cancellationToken);
            if (ShouldStopAfterNestedBlock(state, request))
            {
                return block.End + 1;
            }
        }

        return block.End + 1;
    }
}


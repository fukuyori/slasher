using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private async Task<int> ExecuteTryBlockAsync(
        IReadOnlyList<ScriptLine> lines,
        int index,
        int end,
        ScriptExecutionState state,
        ScriptRunRequest request,
        CancellationToken cancellationToken)
    {
        var block = FindBlockEnd(
            lines,
            index,
            end,
            "try",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "endtry" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "catch", "finally" });

        int? catchIndex = null;
        int? finallyIndex = null;
        if (block.MiddleIndexes.Count > 0)
        {
            foreach (var middleIndex in block.MiddleIndexes)
            {
                var middleWord = FirstWord(lines[middleIndex].Command);
                if (middleWord == "catch" && catchIndex is null && finallyIndex is null)
                {
                    catchIndex = middleIndex;
                }
                else if (middleWord == "finally" && finallyIndex is null)
                {
                    finallyIndex = middleIndex;
                }
                else
                {
                    throw new ScriptCommandException("invalid_try_block", "try supports at most one catch followed by at most one finally.");
                }
            }
        }

        var tryEnd = catchIndex ?? finallyIndex ?? block.End;
        await ExecuteBlockAsync(lines, index + 1, tryEnd, state, request, cancellationToken);
        var tryError = state.FinalError;

        if (tryError is not null && catchIndex is not null)
        {
            var catchVariable = CatchVariableName(lines[catchIndex.Value].Command);
            state.AssignVariable(lines[catchIndex.Value], catchVariable, tryError, ScriptVariableScope.Local);
            state.AssignVariable(lines[catchIndex.Value], "error", tryError, ScriptVariableScope.Local);
            state.FinalError = null;

            var catchEnd = finallyIndex ?? block.End;
            await ExecuteBlockAsync(lines, catchIndex.Value + 1, catchEnd, state, request, cancellationToken);
        }

        if (finallyIndex is not null)
        {
            var preservedError = state.FinalError;
            await ExecuteBlockAsync(lines, finallyIndex.Value + 1, block.End, state, request, cancellationToken);
            if (preservedError is not null && state.FinalError is null)
            {
                state.FinalError = preservedError;
            }
        }

        return block.End + 1;
    }
}


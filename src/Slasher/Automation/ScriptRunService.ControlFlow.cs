using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private async Task<int> ExecuteBlockAsync(
        IReadOnlyList<ScriptLine> lines,
        int start,
        int end,
        ScriptExecutionState state,
        ScriptRunRequest request,
        CancellationToken cancellationToken)
    {
        var index = start;
        while (index < end)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = lines[index];
            var word = FirstWord(line.Command);
            if (IsBlockTerminator(word))
            {
                return index;
            }

            if (state.ReturnRequested)
            {
                return index;
            }

            if (word == "function")
            {
                var block = FindBlockEnd(lines, index, end, "function", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "endfunction" });
                index = block.End + 1;
                continue;
            }

            if (word == "call")
            {
                await ExecuteFunctionCallAsync(lines, line, state, request, cancellationToken);
                if (state.FinalError is not null && request.StopOnError)
                {
                    return index + 1;
                }

                index++;
                continue;
            }

            if (word == "if")
            {
                var next = await ExecuteIfBlockAsync(lines, index, end, state, request, cancellationToken);
                if (ShouldStopAfterNestedBlock(state, request))
                {
                    return next;
                }

                index = next;
                continue;
            }

            if (word == "repeat")
            {
                var next = await ExecuteRepeatBlockAsync(lines, index, end, state, request, cancellationToken);
                if (ShouldStopAfterNestedBlock(state, request))
                {
                    return next;
                }

                index = next;
                continue;
            }

            if (word == "foreach")
            {
                var next = await ExecuteForEachBlockAsync(lines, index, end, state, request, cancellationToken);
                if (ShouldStopAfterNestedBlock(state, request))
                {
                    return next;
                }

                index = next;
                continue;
            }

            if (word == "while")
            {
                var next = await ExecuteWhileBlockAsync(lines, index, end, state, request, cancellationToken);
                if (ShouldStopAfterNestedBlock(state, request))
                {
                    return next;
                }

                index = next;
                continue;
            }

            if (word == "try")
            {
                var next = await ExecuteTryBlockAsync(lines, index, end, state, request, cancellationToken);
                if (ShouldStopAfterNestedBlock(state, request))
                {
                    return next;
                }

                index = next;
                continue;
            }

            await ExecuteStepAsync(line, state, request, cancellationToken);
            if (state.FinalError is not null && request.StopOnError)
            {
                return index + 1;
            }

            if (state.ReturnRequested)
            {
                return index + 1;
            }

            index++;
        }

        return index;
    }

    private static bool IsBlockTerminator(string word)
    {
        return word is "else" or "endif" or "endrepeat" or "endforeach" or "endwhile" or "catch" or "finally" or "endtry" or "endfunction";
    }

    private static bool ShouldStopAfterNestedBlock(ScriptExecutionState state, ScriptRunRequest request)
    {
        return state.ReturnRequested || (state.FinalError is not null && request.StopOnError);
    }
}

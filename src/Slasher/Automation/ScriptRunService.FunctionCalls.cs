using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private async Task ExecuteFunctionCallAsync(
        IReadOnlyList<ScriptLine> lines,
        ScriptLine callLine,
        ScriptExecutionState state,
        ScriptRunRequest request,
        CancellationToken cancellationToken)
    {
        var callAssignment = SplitAssignmentSuffix(ParseCommandLine(ExpandVariables(callLine.Command, state.ResolveVariables(callLine))));
        var callTokens = callAssignment.Tokens;
        if (callTokens.Count < 2)
        {
            throw new ScriptCommandException("invalid_call", "call syntax is: call <functionName> [args...]");
        }

        var functionName = callTokens[1];
        var functionIndex = FindFunction(lines, functionName);
        if (functionIndex is null)
        {
            throw new ScriptCommandException("function_not_found", $"Function '{functionName}' was not found.");
        }

        var functionLine = lines[functionIndex.Value];
        var functionTokens = ParseCommandLine(functionLine.Command);
        var parameterNames = functionTokens.Skip(2).ToArray();
        foreach (var parameterName in parameterNames)
        {
            if (!IsValidVariableName(parameterName))
            {
                throw new ScriptCommandException("invalid_function_parameter", $"Invalid function parameter name '{parameterName}'.");
            }
        }

        var argumentValues = callTokens.Skip(2).Cast<object?>().ToArray();
        if (argumentValues.Length > parameterNames.Length)
        {
            throw new ScriptCommandException(
                "too_many_arguments",
                $"Function '{functionName}' expects {parameterNames.Length} arguments, but received {argumentValues.Length}.",
                Expected: new { arguments = parameterNames.Length },
                Actual: new { arguments = argumentValues.Length });
        }

        var block = FindBlockEnd(lines, functionIndex.Value, lines.Count, "function", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "endfunction" });
        var frame = state.PushCallFrame(functionLine, functionName, callLine);
        try
        {
            for (var i = 0; i < parameterNames.Length; i++)
            {
                var value = i < argumentValues.Length ? argumentValues[i] : string.Empty;
                state.AssignVariable(functionLine, parameterNames[i], value, ScriptVariableScope.Local);
            }

            await ExecuteBlockAsync(lines, functionIndex.Value + 1, block.End, state, request, cancellationToken);
            if (state.ReturnRequested)
            {
                if (callAssignment.VariableName is not null)
                {
                    state.AssignVariable(callLine, callAssignment.VariableName, state.ReturnValue, ScriptVariableScope.Global);
                }

                state.ClearReturn();
            }
        }
        finally
        {
            state.PopCallFrame(frame);
        }
    }
}


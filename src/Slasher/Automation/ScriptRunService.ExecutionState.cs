using Slasher.Api;
using Slasher.Windows;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private sealed partial class ScriptExecutionState
    {
        public ScriptExecutionState(AutomationRunReport report)
        {
            Report = report;
        }

        public AutomationRunReport Report { get; set; }

        public List<AutomationEvent> Events { get; } = [];

        public Dictionary<string, object?> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, Dictionary<string, object?>> FileVariables { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, Dictionary<string, object?>> LocalVariables { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<ScriptCallFrame> CallStack { get; } = [];

        public bool ReturnRequested { get; private set; }

        public object? ReturnValue { get; private set; }

        public string? SelectedHandle { get; set; }

        public WindowInfo? SelectedWindow { get; set; }

        public AutomationError? FinalError { get; set; }

        public string? CurrentStep { get; set; }

        public int NextSequence { get; set; } = 1;

        public int NextCallId { get; set; } = 1;

        public bool IsInsideFunction => CallStack.Count > 0;

        public void SetReturn(object? value)
        {
            ReturnRequested = true;
            ReturnValue = value;
        }

        public void ClearReturn()
        {
            ReturnRequested = false;
            ReturnValue = null;
        }

        public ScriptCallFrame PushCallFrame(ScriptLine functionLine, string functionName, ScriptLine callLine)
        {
            var callId = NextCallId++;
            var frame = new ScriptCallFrame(
                callId,
                functionName,
                functionLine.SourceFile,
                $"{functionLine.SourceFile}::{functionName}::{callId}",
                new AutomationSourceFrame(callLine.SourceFile, callLine.Line, 1, callLine.Function, callLine.Command),
                CurrentStep);
            CallStack.Add(frame);
            CurrentStep = functionName;
            return frame;
        }

        public void PopCallFrame(ScriptCallFrame frame)
        {
            if (CallStack.Count > 0 && ReferenceEquals(CallStack[^1], frame))
            {
                CallStack.RemoveAt(CallStack.Count - 1);
            }
            else
            {
                CallStack.Remove(frame);
            }

            CurrentStep = CallStack.Count == 0 ? frame.PreviousStep : CallStack[^1].Name;
        }
    }
}


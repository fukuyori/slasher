using Slasher.Api;
using Slasher.Windows;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private sealed class ScriptCommandException : Exception
    {
        public ScriptCommandException(
            string code,
            string message,
            IReadOnlyDictionary<string, object?>? details = null,
            bool Recoverable = true,
            object? Expected = null,
            object? Actual = null)
            : base(message)
        {
            Code = code;
            Details = details;
            this.Recoverable = Recoverable;
            this.Expected = Expected;
            this.Actual = Actual;
        }

        public string Code { get; }

        public IReadOnlyDictionary<string, object?>? Details { get; }

        public bool Recoverable { get; }

        public object? Expected { get; }

        public object? Actual { get; }
    }
}


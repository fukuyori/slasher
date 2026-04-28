using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private ScriptCommandResult ExecuteElementAssert(IReadOnlyList<string> args, string? selectedHandle)
    {
        RequireArgs(args, 1, "assert element supports exists, not exists, and text.");
        var negate = args[0].Equals("not", StringComparison.OrdinalIgnoreCase);
        var subjectIndex = negate ? 1 : 0;
        RequireArgs(args.Skip(subjectIndex).ToArray(), 1, "assert element requires a subject.");

        var subject = args[subjectIndex].ToLowerInvariant();
        return subject switch
        {
            "exists" => ExecuteElementExistsAssert(args.Skip(subjectIndex + 1).ToArray(), selectedHandle, negate),
            "text" => ExecuteElementTextAssert(args.Skip(subjectIndex + 1).ToArray(), selectedHandle),
            _ => throw new ScriptCommandException(
                "unsupported_assertion",
                "assert element supports exists, not exists, and text.")
        };
    }

    private ScriptCommandResult ExecuteElementExistsAssert(
        IReadOnlyList<string> args,
        string? selectedHandle,
        bool negate)
    {
        var query = ParseElementQuery(args, selectedHandle);
        var request = ToElementRequest(query);
        if (!_automation.ElementExists(request, out var response, out var error) || response is null)
        {
            throw FromError(error, "element_exists_assert_failed");
        }

        if (response.Exists == negate)
        {
            throw new ScriptCommandException(
                "assertion_failed",
                negate
                    ? "Expected matching element not to exist, but it was found."
                    : "Expected matching element to exist, but none was found.",
                Expected: new { exists = !negate, query },
                Actual: new { response.Exists, response.Element, response.TotalScanned });
        }

        return new ScriptCommandResult(new { asserted = true, exists = response.Exists, response.Element });
    }

    private ScriptCommandResult ExecuteElementTextAssert(
        IReadOnlyList<string> args,
        string? selectedHandle)
    {
        var split = FindTextAssertionOperator(args);
        if (split.OperatorIndex < 0)
        {
            throw new ScriptCommandException(
                "invalid_element_text_assertion",
                "assert element text syntax is: assert element text <element query> <operator> <expected>.");
        }

        var query = ParseElementQuery(args.Take(split.OperatorIndex).ToArray(), selectedHandle);
        var expected = string.Join(' ', args.Skip(split.OperatorIndex + 1));
        if (string.IsNullOrEmpty(expected))
        {
            throw new ScriptCommandException("invalid_element_text_assertion", "assert element text requires expected text.");
        }

        var request = ToElementRequest(query);
        if (!_automation.GetElementText(request, out var response, out var error) || response is null)
        {
            throw FromError(error, "element_text_assert_failed");
        }

        if (!CompareValues(response.Text, split.Operator, expected))
        {
            throw new ScriptCommandException(
                "assertion_failed",
                $"Element text assertion failed. Expected text {split.Operator} '{expected}', actual '{response.Text}'.",
                Expected: new { text = expected, op = split.Operator, query },
                Actual: new { text = response.Text, response.Element });
        }

        return new ScriptCommandResult(new { asserted = true, text = response.Text, op = split.Operator, expected });
    }

    private static ElementClickRequest ToElementRequest(ElementQuery query)
    {
        return new ElementClickRequest(
            query.Handle,
            query.Title,
            query.ClassName,
            query.ControlId,
            query.Match,
            query.MaxDepth,
            query.Button);
    }

    private static (int OperatorIndex, string Operator) FindTextAssertionOperator(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            var token = args[i].ToLowerInvariant();
            if (token is "==" or "=" or "eq" or "!=" or "<>" or "ne" or "contains" or "startswith" or "starts-with" or "endswith" or "ends-with")
            {
                return (i, args[i]);
            }
        }

        return (-1, string.Empty);
    }
}

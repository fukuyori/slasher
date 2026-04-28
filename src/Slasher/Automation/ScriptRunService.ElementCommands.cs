using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private ScriptCommandResult ExecuteElementCommand(IReadOnlyList<string> args, string? selectedHandle)
    {
        RequireArgs(args, 1, "element requires an action.");
        var action = args[0].ToLowerInvariant();
        return action switch
        {
            "tree" => ExecuteElementTreeCommand(args.Skip(1).ToArray(), selectedHandle),
            "find" => ExecuteElementFindCommand(args.Skip(1).ToArray(), selectedHandle),
            "exists" => ExecuteElementExistsCommand(args.Skip(1).ToArray(), selectedHandle),
            "text" => ExecuteElementTextCommand(args.Skip(1).ToArray(), selectedHandle),
            "click" => ExecuteElementClickCommand(args.Skip(1).ToArray(), selectedHandle),
            _ => throw new ScriptCommandException(
                "unsupported_element_command",
                "element supports tree, find, exists, text, and click.")
        };
    }

    private ScriptCommandResult ExecuteElementTreeCommand(IReadOnlyList<string> args, string? selectedHandle)
    {
        var handle = selectedHandle;
        var maxDepth = 3;
        var maxChildren = 200;
        for (var i = 0; i < args.Count; i++)
        {
            var token = args[i].ToLowerInvariant();
            switch (token)
            {
                case "selected":
                    handle = RequireSelected(selectedHandle);
                    break;
                case "foreground":
                    handle = null;
                    break;
                case "handle":
                    RequireArgs(args.Skip(i + 1).ToArray(), 1, "element tree handle requires a value.");
                    handle = args[++i];
                    break;
                case "depth":
                case "maxdepth":
                    RequireArgs(args.Skip(i + 1).ToArray(), 1, "element tree depth requires a value.");
                    maxDepth = ParseInt(args[++i], "maxDepth");
                    break;
                case "children":
                case "maxchildren":
                    RequireArgs(args.Skip(i + 1).ToArray(), 1, "element tree children requires a value.");
                    maxChildren = ParseInt(args[++i], "maxChildren");
                    break;
                default:
                    if (i == 0)
                    {
                        handle = token == "selected" ? RequireSelected(selectedHandle) : args[i];
                    }
                    else
                    {
                        throw new ScriptCommandException("invalid_element_tree_argument", $"Unknown element tree argument '{args[i]}'.");
                    }

                    break;
            }
        }

        if (!_automation.GetElementTree(handle, maxDepth, maxChildren, out var tree, out var error) || tree is null)
        {
            throw FromError(error, "element_tree_failed");
        }

        return new ScriptCommandResult(tree, AssignmentValue: tree);
    }

    private ScriptCommandResult ExecuteElementFindCommand(IReadOnlyList<string> args, string? selectedHandle)
    {
        var query = ParseElementQuery(args, selectedHandle);
        if (!_automation.FindElements(
            query.Handle,
            query.Title,
            query.ClassName,
            query.ControlId,
            query.Match,
            query.MaxDepth,
            query.MaxResults,
            out var response,
            out var error)
            || response is null)
        {
            throw FromError(error, "element_find_failed");
        }

        object assignmentValue = response.Elements.FirstOrDefault() is { } first
            ? first
            : response;
        return new ScriptCommandResult(response, AssignmentValue: assignmentValue);
    }

    private ScriptCommandResult ExecuteElementClickCommand(IReadOnlyList<string> args, string? selectedHandle)
    {
        var query = ParseElementQuery(args, selectedHandle);
        var request = new ElementClickRequest(
            query.Handle,
            query.Title,
            query.ClassName,
            query.ControlId,
            query.Match,
            query.MaxDepth,
            query.Button);
        if (!_automation.ClickElement(request, out var element, out var error) || element is null)
        {
            throw FromError(error, "element_click_failed");
        }

        return new ScriptCommandResult(new { clicked = true, element }, AssignmentValue: element);
    }

    private ScriptCommandResult ExecuteElementExistsCommand(IReadOnlyList<string> args, string? selectedHandle)
    {
        var query = ParseElementQuery(args, selectedHandle);
        var request = new ElementClickRequest(
            query.Handle,
            query.Title,
            query.ClassName,
            query.ControlId,
            query.Match,
            query.MaxDepth,
            query.Button);
        if (!_automation.ElementExists(request, out var response, out var error) || response is null)
        {
            throw FromError(error, "element_exists_failed");
        }

        return new ScriptCommandResult(response, AssignmentValue: response);
    }

    private ScriptCommandResult ExecuteElementTextCommand(IReadOnlyList<string> args, string? selectedHandle)
    {
        var query = ParseElementQuery(args, selectedHandle);
        var request = new ElementClickRequest(
            query.Handle,
            query.Title,
            query.ClassName,
            query.ControlId,
            query.Match,
            query.MaxDepth,
            query.Button);
        if (!_automation.GetElementText(request, out var response, out var error) || response is null)
        {
            throw FromError(error, "element_text_failed");
        }

        return new ScriptCommandResult(response, AssignmentValue: response.Text);
    }

    private ElementQuery ParseElementQuery(IReadOnlyList<string> args, string? selectedHandle)
    {
        RequireArgs(args, 2, "element query syntax is: element <find|exists|text|click> title <text> [class <className>] [controlId <id>] [match exact|contains] [in selected|foreground|<handle>] [depth n] [limit n] [button left|right|middle].");
        var query = new ElementQuery(Handle: selectedHandle);
        for (var i = 0; i < args.Count; i++)
        {
            var token = args[i].ToLowerInvariant();
            switch (token)
            {
                case "title":
                case "name":
                case "text":
                    query = query with { Title = ReadOptionValue(args, ref i, token) };
                    break;
                case "class":
                case "classname":
                    query = query with { ClassName = ReadOptionValue(args, ref i, token) };
                    break;
                case "control":
                case "controlid":
                case "id":
                    query = query with { ControlId = ParseInt(ReadOptionValue(args, ref i, token), "controlId") };
                    break;
                case "match":
                    query = query with { Match = ReadOptionValue(args, ref i, token).ToLowerInvariant() };
                    break;
                case "in":
                case "within":
                    query = query with { Handle = ReadElementScope(args, ref i, selectedHandle) };
                    break;
                case "handle":
                    query = query with { Handle = ReadOptionValue(args, ref i, token) };
                    break;
                case "selected":
                    query = query with { Handle = RequireSelected(selectedHandle) };
                    break;
                case "foreground":
                    query = query with { Handle = null };
                    break;
                case "depth":
                case "maxdepth":
                    query = query with { MaxDepth = ParseInt(ReadOptionValue(args, ref i, token), "maxDepth") };
                    break;
                case "limit":
                case "maxresults":
                    query = query with { MaxResults = ParseInt(ReadOptionValue(args, ref i, token), "maxResults") };
                    break;
                case "button":
                    query = query with { Button = NormalizeMouseButton(ReadOptionValue(args, ref i, token)) };
                    break;
                default:
                    throw new ScriptCommandException("invalid_element_argument", $"Unknown element argument '{args[i]}'.");
            }
        }

        if (query.Match is not "contains" and not "exact")
        {
            throw new ScriptCommandException("invalid_element_match", "element match must be contains or exact.");
        }

        return query;
    }

    private static string ReadOptionValue(IReadOnlyList<string> args, ref int index, string option)
    {
        if (index + 1 >= args.Count)
        {
            throw new ScriptCommandException("invalid_element_argument", $"element {option} requires a value.");
        }

        index++;
        return args[index];
    }

    private static string? ReadElementScope(IReadOnlyList<string> args, ref int index, string? selectedHandle)
    {
        var value = ReadOptionValue(args, ref index, "in");
        return value.ToLowerInvariant() switch
        {
            "selected" => RequireSelected(selectedHandle),
            "foreground" => null,
            _ => value
        };
    }

    private sealed record ElementQuery(
        string? Handle = null,
        string? Title = null,
        string? ClassName = null,
        int? ControlId = null,
        string Match = "contains",
        int MaxDepth = 8,
        int MaxResults = 20,
        string Button = "left");
}

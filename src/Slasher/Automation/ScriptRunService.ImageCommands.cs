using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private ScriptCommandResult ExecuteImageCommand(IReadOnlyList<string> args, string? selectedHandle, ScriptLine line)
    {
        RequireArgs(args, 1, "image requires an action.");
        return args[0].ToLowerInvariant() switch
        {
            "match" or "find" => ExecuteImageMatchCommand(args.Skip(1).ToArray(), selectedHandle, line),
            _ => throw new ScriptCommandException("unsupported_image_command", "image supports: image match <templatePath> [selected|full] [threshold n] [maxWidth n] [maxHeight n] [step n].")
        };
    }

    private ScriptCommandResult ExecuteImageMatchCommand(IReadOnlyList<string> args, string? selectedHandle, ScriptLine line)
    {
        var request = ParseImageMatchRequest(args, selectedHandle, line);
        if (!_automation.MatchImage(request, out var response, out var error) || response is null)
        {
            throw FromError(error, "image_match_failed");
        }

        return new ScriptCommandResult(response, AssignmentValue: response);
    }

    private ScriptCommandResult ExecuteImageAssert(IReadOnlyList<string> args, string? selectedHandle, ScriptLine line)
    {
        RequireArgs(args, 1, "assert image supports: assert image match <templatePath> [selected|full].");
        var negate = args[0].Equals("not", StringComparison.OrdinalIgnoreCase);
        var subjectIndex = negate ? 1 : 0;
        RequireArgs(args.Skip(subjectIndex).ToArray(), 1, "assert image requires a subject.");
        if (!args[subjectIndex].Equals("match", StringComparison.OrdinalIgnoreCase)
            && !args[subjectIndex].Equals("find", StringComparison.OrdinalIgnoreCase))
        {
            throw new ScriptCommandException("unsupported_assertion", "assert image supports match and not match.");
        }

        var request = ParseImageMatchRequest(args.Skip(subjectIndex + 1).ToArray(), selectedHandle, line);
        if (!_automation.MatchImage(request, out var response, out var error) || response is null)
        {
            throw FromError(error, "image_match_assert_failed");
        }

        if (response.Found == negate)
        {
            throw new ScriptCommandException(
                "assertion_failed",
                negate
                    ? "Expected template image not to match the screen, but it was found."
                    : "Expected template image to match the screen, but it was not found.",
                Expected: new { found = !negate, request.TemplatePath, request.Threshold },
                Actual: new { response.Found, response.Score, response.Bounds });
        }

        return new ScriptCommandResult(new { asserted = true, imageMatched = response.Found, response.Score, response.Bounds }, AssignmentValue: response);
    }

    private ImageMatchRequest ParseImageMatchRequest(IReadOnlyList<string> args, string? selectedHandle, ScriptLine line)
    {
        RequireArgs(args, 1, "image match syntax is: image match <templatePath> [selected|full] [threshold n] [maxWidth n] [maxHeight n] [step n].");
        var templatePath = ResolveRuntimeFilePath(args[0], line, "image");
        string? handle = selectedHandle;
        double threshold = 0.98;
        int? maxWidth = PreviewMaxWidth;
        int? maxHeight = PreviewMaxHeight;
        var step = 1;

        for (var i = 1; i < args.Count; i++)
        {
            var token = args[i].ToLowerInvariant();
            switch (token)
            {
                case "selected":
                    handle = RequireSelected(selectedHandle);
                    break;
                case "full":
                case "screen":
                    handle = null;
                    break;
                case "handle":
                    RequireArgs(args.Skip(i + 1).ToArray(), 1, "image match handle requires a value.");
                    handle = args[++i];
                    break;
                case "threshold":
                case "score":
                    RequireArgs(args.Skip(i + 1).ToArray(), 1, "image match threshold requires a value.");
                    if (!double.TryParse(args[++i], out threshold))
                    {
                        throw new ScriptCommandException("invalid_threshold", "threshold must be a number between 0 and 1.");
                    }

                    break;
                case "maxwidth":
                case "width":
                    RequireArgs(args.Skip(i + 1).ToArray(), 1, "image match maxWidth requires a value.");
                    maxWidth = ParseInt(args[++i], "maxWidth");
                    break;
                case "maxheight":
                case "height":
                    RequireArgs(args.Skip(i + 1).ToArray(), 1, "image match maxHeight requires a value.");
                    maxHeight = ParseInt(args[++i], "maxHeight");
                    break;
                case "step":
                    RequireArgs(args.Skip(i + 1).ToArray(), 1, "image match step requires a value.");
                    step = ParseInt(args[++i], "step");
                    break;
                default:
                    throw new ScriptCommandException("invalid_image_match_argument", $"Unknown image match argument '{args[i]}'.");
            }
        }

        return new ImageMatchRequest(templatePath, handle, threshold, maxWidth, maxHeight, step);
    }
}

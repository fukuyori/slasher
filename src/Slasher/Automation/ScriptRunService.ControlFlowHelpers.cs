using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private static int? FindFunction(IReadOnlyList<ScriptLine> lines, string name)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var tokens = ParseCommandLine(lines[i].Command);
            if (tokens.Count >= 2
                && tokens[0].Equals("function", StringComparison.OrdinalIgnoreCase)
                && tokens[1].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return null;
    }

    private static ScriptBlockMatch FindBlockEnd(
        IReadOnlyList<ScriptLine> lines,
        int start,
        int end,
        string opener,
        IReadOnlySet<string> closers,
        IReadOnlySet<string>? middle = null)
    {
        var depth = 0;
        var middleIndexes = new List<int>();
        for (var index = start + 1; index < end; index++)
        {
            var word = FirstWord(lines[index].Command);
            if (word == opener)
            {
                depth++;
                continue;
            }

            if (closers.Contains(word))
            {
                if (depth == 0)
                {
                    return new ScriptBlockMatch(index, middleIndexes.Count == 0 ? null : middleIndexes[0], middleIndexes);
                }

                depth--;
                continue;
            }

            if (depth == 0 && middle?.Contains(word) == true)
            {
                middleIndexes.Add(index);
            }
        }

        throw new ScriptCommandException("block_not_closed", $"{opener} block is missing its closing command.");
    }
}


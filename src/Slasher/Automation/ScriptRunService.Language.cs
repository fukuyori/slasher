using System.Text;
using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private static IReadOnlyList<string> ParseCommandLine(string input)
    {
        var tokens = new List<string>();
        var current = new List<char>();
        char? quote = null;
        var escaping = false;
        var tokenStarted = false;

        foreach (var ch in input.Trim())
        {
            if (escaping)
            {
                if (ch == quote || ch == '\\')
                {
                    current.Add(ch);
                }
                else
                {
                    current.Add('\\');
                    current.Add(ch);
                }

                escaping = false;
                tokenStarted = true;
            }
            else if (ch == '\\' && quote is not null)
            {
                escaping = true;
                tokenStarted = true;
            }
            else if (quote is not null)
            {
                if (ch == quote)
                {
                    quote = null;
                }
                else
                {
                    current.Add(ch);
                    tokenStarted = true;
                }
            }
            else if (ch is '"' or '\'')
            {
                quote = ch;
                tokenStarted = true;
            }
            else if (char.IsWhiteSpace(ch))
            {
                if (tokenStarted)
                {
                    tokens.Add(new string(current.ToArray()));
                    current.Clear();
                    tokenStarted = false;
                }
            }
            else
            {
                current.Add(ch);
                tokenStarted = true;
            }
        }

        if (escaping)
        {
            current.Add('\\');
            tokenStarted = true;
        }

        if (quote is not null)
        {
            throw new ScriptCommandException("unclosed_quote", "Unclosed quote in command.");
        }

        if (tokenStarted)
        {
            tokens.Add(new string(current.ToArray()));
        }

        return tokens;
    }

    private static ScriptAssignment SplitAssignmentSuffix(IReadOnlyList<string> tokens)
    {
        if (tokens.Count >= 3 && tokens[^2].Equals("as", StringComparison.OrdinalIgnoreCase))
        {
            return new ScriptAssignment(tokens.Take(tokens.Count - 2).ToArray(), tokens[^1]);
        }

        return new ScriptAssignment(tokens, null);
    }

    private static string FirstWord(string text)
    {
        return ParseCommandLine(text).FirstOrDefault()?.ToLowerInvariant() ?? string.Empty;
    }
}


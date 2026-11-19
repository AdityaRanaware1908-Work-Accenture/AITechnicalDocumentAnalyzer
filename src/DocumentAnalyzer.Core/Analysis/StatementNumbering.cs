using System.Text.RegularExpressions;

namespace DocumentAnalyzer.Core.Analysis;

public sealed record NumberingResult(string Text, bool WasNumbered, int LinesWithoutIds);

/// <summary>
/// Prepares pasted text for the analyzer. Evidence lookup works on identifiers such as REQ-001 at the start of a line,
/// so pasted text without any identifier is numbered automatically (REQ-001, REQ-002, ...).
/// </summary>
public static class StatementNumbering
{
    private static readonly Regex IdPattern = new(@"^\s*[A-Za-z][A-Za-z0-9_]*-\d+\b", RegexOptions.Compiled);

    public static NumberingResult Prepare(string? text, string idPrefix)
    {
        var lines = (text ?? string.Empty)
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        var linesWithIds = lines.Count(line => IdPattern.IsMatch(line));

        if (lines.Count > 0 && linesWithIds == 0)
        {
            var numbered = lines.Select((line, index) => $"{idPrefix}-{index + 1:000}: {line}");

            return new NumberingResult(string.Join(Environment.NewLine, numbered), true, 0);
        }

        return new NumberingResult(string.Join(Environment.NewLine, lines), false, lines.Count - linesWithIds);
    }
}

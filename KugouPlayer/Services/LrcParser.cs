using KugouPlayer.Models;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace KugouPlayer.Services;

public static partial class LrcParser
{
    [GeneratedRegex(@"\[(?<minute>\d{1,3}):(?<second>\d{1,2})(?:[\.:](?<fraction>\d{1,3}))?\]")]
    private static partial Regex TimestampRegex();

    public static IReadOnlyList<LyricLine> ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        return ParseLines(File.ReadLines(filePath));
    }

    public static IReadOnlyList<LyricLine> ParseText(string content) =>
        ParseLines(content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'));

    private static IReadOnlyList<LyricLine> ParseLines(IEnumerable<string> rawLines)
    {
        var lines = new List<LyricLine>();
        foreach (var rawLine in rawLines)
        {
            var matches = TimestampRegex().Matches(rawLine);
            if (matches.Count == 0)
            {
                continue;
            }

            var text = TimestampRegex().Replace(rawLine, string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            foreach (Match match in matches)
            {
                var minutes = int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture);
                var seconds = int.Parse(match.Groups["second"].Value, CultureInfo.InvariantCulture);
                var fractionText = match.Groups["fraction"].Value;
                var milliseconds = fractionText.Length switch
                {
                    1 => int.Parse(fractionText, CultureInfo.InvariantCulture) * 100,
                    2 => int.Parse(fractionText, CultureInfo.InvariantCulture) * 10,
                    3 => int.Parse(fractionText, CultureInfo.InvariantCulture),
                    _ => 0
                };

                lines.Add(new LyricLine
                {
                    Timestamp = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds) + TimeSpan.FromMilliseconds(milliseconds),
                    Text = text
                });
            }
        }

        return lines.OrderBy(line => line.Timestamp).ToArray();
    }
}

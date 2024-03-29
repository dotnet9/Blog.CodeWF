﻿namespace CodeWF.MetaWeblog;
// Borrowed from: https://github.com/snielsson/XmlRpcLight/blob/master/XmlRpcLight/DataTypes/DateTime8601.cs

internal static class DateTime8601
{
    private static readonly Regex DateTime8601Regex = new(
        @"(((?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2}))|((?<year>\d{4})(?<month>\d{2})(?<day>\d{2})))"
        + @"T"
        + @"(((?<hour>\d{2}):(?<minute>\d{2}):(?<second>\d{2}))|((?<hour>\d{2})(?<minute>\d{2})(?<second>\d{2})))"
        + @"(?<tz>$|Z|([+-]\d{2}:?(\d{2})?))");

    public static bool TryParseDateTime8601(string date, out DateTime result)
    {
        result = DateTime.MinValue;
        Match m = DateTime8601Regex.Match(date);
        string normalized = m.Groups["year"].Value + m.Groups["month"].Value + m.Groups["day"].Value
                            + "T"
                            + m.Groups["hour"].Value + m.Groups["minute"].Value + m.Groups["second"].Value
                            + m.Groups["tz"].Value;
        string[] formats = new[]
        {
            "yyyyMMdd'T'HHmmss", "yyyyMMdd'T'HHmmss'Z'", "yyyyMMdd'T'HHmmsszzz", "yyyyMMdd'T'HHmmsszz"
        };

        try
        {
            result = DateTime.ParseExact(normalized, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
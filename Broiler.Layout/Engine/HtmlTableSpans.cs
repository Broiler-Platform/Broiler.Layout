using System;

namespace Broiler.Layout.Engine;

/// <summary>
/// A table cell's <c>colspan</c> and <c>rowspan</c> (HTML §4.9.11), read as HTML reads them: as a
/// non-negative integer (§2.3.4.2), so leading ASCII whitespace is skipped, a <c>+</c> is allowed and
/// digits are read up to the first other character, then clamped as the <c>colSpan</c> and
/// <c>rowSpan</c> IDL attributes clamp them.
/// </summary>
/// <remarks>
/// The table layout read both with <c>int.TryParse</c>: <c>colspan="2px"</c> spanned one column, a
/// negative <c>rowspan</c> was returned as it stood, and nothing bounded either, so one cell with
/// <c>colspan="3000000"</c> made the layout allocate 137 MiB of column state. A <c>rowspan</c> of 0, which
/// HTML spans to the end of its row group, is a single row here: this layout has no such span.
/// </remarks>
internal static class HtmlTableSpans
{
    /// <summary>The largest <c>colspan</c> HTML honours.</summary>
    internal const int MaximumColSpan = 1000;

    /// <summary>The largest <c>rowspan</c> HTML honours.</summary>
    internal const int MaximumRowSpan = 65534;

    public static int GetColSpan(CssBox cell) => ParseColSpan(cell.GetAttribute("colspan"));

    public static int GetRowSpan(CssBox cell) => ParseRowSpan(cell.GetAttribute("rowspan"));

    /// <summary>A <c>colspan</c> value: 1 when it is missing, invalid or 0, and at most <see cref="MaximumColSpan"/>.</summary>
    internal static int ParseColSpan(string? value) =>
        ParseNonNegativeInteger(value) is { } span and > 0 ? Math.Min(span, MaximumColSpan) : 1;

    /// <summary>A <c>rowspan</c> value: 1 when it is missing, invalid or 0, and at most <see cref="MaximumRowSpan"/>.</summary>
    internal static int ParseRowSpan(string? value) =>
        ParseNonNegativeInteger(value) is { } span and > 0 ? Math.Min(span, MaximumRowSpan) : 1;

    /// <summary>
    /// HTML §2.3.4.2, the rules for parsing non-negative integers: <see langword="null"/> for an error,
    /// and a value too large for an <see cref="int"/> as <see cref="int.MaxValue"/>.
    /// </summary>
    internal static int? ParseNonNegativeInteger(string? value)
    {
        if (value is null)
            return null;

        var position = 0;
        while (position < value.Length && value[position] is '\t' or '\n' or '\f' or '\r' or ' ')
            position++;

        var negative = false;
        if (position < value.Length && value[position] == '-')
        {
            negative = true;
            position++;
        }
        else if (position < value.Length && value[position] == '+')
        {
            position++;
        }

        if (position >= value.Length || !char.IsAsciiDigit(value[position]))
            return null;

        long result = 0;
        while (position < value.Length && char.IsAsciiDigit(value[position]))
        {
            if (result < int.MaxValue)
                result = Math.Min(result * 10 + (value[position] - '0'), int.MaxValue);
            position++;
        }

        // "-0" is zero, which is not negative.
        return negative && result != 0 ? null : (int)result;
    }
}

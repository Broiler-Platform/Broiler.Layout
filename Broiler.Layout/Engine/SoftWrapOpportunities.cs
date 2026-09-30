using System;
using System.Globalization;
using System.Runtime.CompilerServices;

using Broiler.CSS;

namespace Broiler.Layout.Engine;

/// <summary>
/// Where a line may break between the words of different boxes: the soft wrap opportunities at the
/// edges of inline boxes (CSS Text 3 §5.1, §5.2).
/// </summary>
/// <remarks>
/// <para>
/// Soft wrap opportunities come from the text, its spaces and the characters the line breaking rules
/// allow a break after, not from the elements around it: the edge of an inline box inside a word is
/// not one, and "aaa&lt;b&gt;bbb&lt;/b&gt;" is one word, which overflows a line too narrow for it
/// rather than break. For web compatibility there is one before and after an atomic inline, an image
/// or an inline-block, and a <c>&lt;wbr&gt;</c> makes one.
/// </para>
/// <para>
/// A box's text is split into words where a line may break (<see cref="CssBox.ParseToWords"/>): at
/// white space, which a word carries as <see cref="CssRect.HasSpaceBefore"/> and
/// <see cref="CssRect.HasSpaceAfter"/>, after a hyphen or an ideograph, and after every character
/// under <c>word-break: break-all</c> or <c>line-break: anywhere</c>. So a line may always break
/// between two words of one box, and between two words of different boxes where either word says
/// so.
/// </para>
/// <para>
/// The line breaking rules (UAX #14) let a line break beside many more characters than that split
/// does: between kana, around CJK punctuation and fullwidth forms, after a dash, a soft hyphen or a
/// zero width space, between the words of Thai. The split keeps those inside a word, but the edge of
/// an element beside one is a break (<see cref="BreaksAfter"/>, <see cref="BreaksBefore"/>). Taken
/// for none, they joined the kana on either side of a link into one word: Japanese text with links
/// ran 184px past the side of a 200px paragraph, where browsers wrap it.
/// </para>
/// <para>
/// Some breaks are for a pair of characters, neither of which breaks on its own side, as before an
/// opening bracket after a full stop (<see cref="Between"/>).
/// </para>
/// </remarks>
internal static class SoftWrapOpportunities
{
    /// <summary>Whether a line may break after <paramref name="word"/>, whatever follows it.</summary>
    internal static bool After(CssRect word)
    {
        if (word.HasSpaceAfter || word.IsImage || word.IsLineBreak || BreaksAfterEveryCharacter(word.OwnerBox))
            return true;

        var text = word.Text;
        if (string.IsNullOrEmpty(text))
            return false;

        // A variation selector takes the class of the character it follows (UAX #14 LB9): "❤"
        // with the one that asks for its emoji form breaks as "❤" does.
        int last = text.Length - 1;
        while (last > 0 && text[last] is >= '\uFE00' and <= '\uFE0F')
            last--;

        return BreaksAfter(text[last]);
    }

    /// <summary>Whether a line may break before <paramref name="word"/>, whatever precedes it.</summary>
    private static bool Before(CssRect word)
    {
        if (word.HasSpaceBefore || word.IsImage || word.IsLineBreak)
            return true;

        var text = word.Text;
        return !string.IsNullOrEmpty(text) && BreaksBefore(text[0]);
    }

    /// <summary>
    /// Whether a line may break after <paramref name="c"/> (UAX #14): after a space or a tab that
    /// <c>pre-wrap</c> keeps, a word of its own; a hyphen, a dash from U+2010 to U+2014 but the
    /// non-breaking hyphen, the two- and three-em dashes, or a soft hyphen; '?', "‼", "⁇", "⁉" and
    /// "❣"; an ellipsis; the spaces from U+2000 to the zero width space but the figure space, the
    /// Ogham, Tibetan and Ethiopic word separators; a line or paragraph separator; and beside the
    /// characters of <see cref="BreaksAround"/>.
    /// </summary>
    private static bool BreaksAfter(char c) =>
        c is ' ' or '\t' or '-' or '\u00AD' or '\u2010' or (>= '\u2012' and <= '\u2014') or '\u2E3A' or '\u2E3B'
            or '?' or '\u203C' or '\u2047' or '\u2049' or '\u2763' or '\u2026'
            or (>= '\u2000' and <= '\u200B' and not '\u2007') or '\u205F' or '\u1680' or '\u0F0B' or '\u1361'
            or '\u2028' or '\u2029'
        || BreaksAround(c);

    /// <summary>
    /// Whether a line may break before <paramref name="c"/>: before an em dash, a two- or three-em
    /// dash, before a zero width space, which lets it break just after, and beside the characters of
    /// <see cref="BreaksAround"/>.
    /// </summary>
    private static bool BreaksBefore(char c) => c is '\u2014' or '\u2E3A' or '\u2E3B' or '\u200B' || BreaksAround(c);

    /// <summary>
    /// Whether a line may break on either side of <paramref name="c"/>: the ideographs the text is
    /// split after (<see cref="CommonUtils.IsAsianCharecter"/>), and the CJK radicals, symbols and
    /// punctuation, kana, bopomofo, Hangul jamo, compatibility forms and fullwidth and halfwidth forms
    /// (UAX #14 classes ID, CL, OP and their kin), and the pictographs it puts with them
    /// (<see cref="IsIdeographicPictograph"/>). And the scripts of Southeast Asia, Thai, Lao,
    /// Myanmar, Khmer and the Tai scripts, which browsers break by dictionary between words: the edge
    /// of an element is the only break the engine can find in them.
    /// </summary>
    /// <remarks>
    /// A line may not break before closing punctuation such as "。", only after it, but the text is
    /// not split there, so a break before it is the nearest there is: "aaa&lt;b&gt;。bbb&lt;/b&gt;" in
    /// a narrow block goes on two lines, as in browsers, with "。" starting the second.
    /// </remarks>
    private static bool BreaksAround(char c) =>
        CommonUtils.IsAsianCharecter(c)
        || c is (>= '\u2E80' and <= '\u2FFF') or (>= '\u3000' and <= '\u4DBF') or (>= '\uFA2E' and <= '\uFAFF')
            or (>= '\uFE30' and <= '\uFE4F') or (>= '\uFF00' and <= '\uFFEF')
            or (>= '\u0E00' and <= '\u0EFF') or (>= '\u1000' and <= '\u109F') or (>= '\u1780' and <= '\u17FF')
            or (>= '\u1950' and <= '\u19FF') or (>= '\u1A20' and <= '\u1AAF')
        || IsIdeographicPictograph(c);

    /// <summary>
    /// Whether <paramref name="c"/> is one of the pictographs below U+2800 that UAX #14 puts with the
    /// ideographs (classes ID and EB), as browsers do: "☀", "☺", "✂", "✈" and "❤" among them, but
    /// not "★", "✓" or "✅". Those past U+FFFF, as "😀", are pairs of surrogates, which the text is
    /// split after, and <see cref="CommonUtils.IsAsianCharecter"/> takes for ideographs.
    /// </summary>
    private static bool IsIdeographicPictograph(char c) =>
        c is '\u231A' or '\u231B' or (>= '\u23F0' and <= '\u23F3') or (>= '\u2600' and <= '\u2603') or '\u2614'
            or '\u2615' or '\u2618' or (>= '\u261A' and <= '\u261F') or (>= '\u2639' and <= '\u263B') or '\u2668'
            or '\u267F' or (>= '\u26BD' and <= '\u26C8') or '\u26CD' or (>= '\u26CF' and <= '\u26D1') or '\u26D3'
            or '\u26D4' or '\u26D8' or '\u26D9' or '\u26DC' or (>= '\u26DF' and <= '\u26E1') or '\u26EA'
            or (>= '\u26F1' and <= '\u26F5') or (>= '\u26F7' and <= '\u26FA') or (>= '\u26FD' and <= '\u2704')
            or (>= '\u2708' and <= '\u270D') or '\u2764';

    /// <summary>
    /// Whether a line may break between <paramref name="previous"/> and <paramref name="next"/>, the
    /// words on either side of an element's edge, where neither allows it on its own side: for the
    /// pair of characters that meet there, as before an opening bracket after a full stop.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Browsers break between two ASCII characters by a table of their own (Chromium's
    /// <c>kAsciiLineBreakTable</c>): before "(", "[", "{" and "&lt;" after one of
    /// <c>! " # % &amp; ) * + , . : ; = &gt; \ ] | } ~</c>, and otherwise only after "-" and "?"
    /// (<see cref="BreaksAfter"/>). Beside any other character they follow UAX #14 (LB31 and the
    /// rules before it): after closing punctuation, "!" and its kin, the separators "," "." ":" ";",
    /// "/", a currency or percent sign or "|" (classes CL, CP, EX, IS, SY, PR, PO, BA) before an
    /// opening bracket or a currency or percent sign (OP, PR, PO), and after CL, EX, SY and BA before
    /// a letter or a digit too. Not after a currency or percent sign before an opening bracket and a
    /// digit (LB25), but for two characters of Latin-1, which Chromium breaks between whatever
    /// follows: "€[1]" is one word there, and "°[1]" two.
    /// </para>
    /// <para>
    /// The edge of every element broke a line before, and so matched browsers here. Taken for none,
    /// these pairs joined a citation to the word it follows: "aaa." then a sup holding "[1]" stayed
    /// together, past the side of a 40px block, where browsers put "[1]" on the next line.
    /// </para>
    /// </remarks>
    private static bool Between(CssRect previous, CssRect next)
    {
        var before = previous.Text;
        var after = next.Text;
        if (previous.IsImage || next.IsImage || string.IsNullOrEmpty(before) || string.IsNullOrEmpty(after))
            return false;

        char x = before[^1], y = after[0];
        if (x < 0x80 && y < 0x80)
            return y is '(' or '[' or '{' or '<' && x is '!' or '"' or '#' or '%' or '&' or ')' or '*' or '+' or ','
                or '.' or ':' or ';' or '=' or '>' or '\\' or ']' or '|' or '}' or '~';

        // A combining mark or a variation selector breaks as the character it follows (LB9).
        int last = before.Length - 1;
        while (last > 0 && char.GetUnicodeCategory(before[last]) is UnicodeCategory.NonSpacingMark
                   or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
            last--;

        var nextClass = ClassOf(y);
        return ClassOf(before[last]) switch
        {
            LineBreakClass.Close or LineBreakClass.Exclamation =>
                nextClass is LineBreakClass.Open or LineBreakClass.Prefix or LineBreakClass.Postfix
                    or LineBreakClass.Alphabetic or LineBreakClass.Hebrew or LineBreakClass.Numeric,
            LineBreakClass.CloseParenthesis or LineBreakClass.Infix =>
                nextClass is LineBreakClass.Open or LineBreakClass.Prefix or LineBreakClass.Postfix,
            LineBreakClass.Solidus =>
                nextClass is LineBreakClass.Open or LineBreakClass.Prefix or LineBreakClass.Postfix
                    or LineBreakClass.Alphabetic or LineBreakClass.Numeric,
            LineBreakClass.BreakAfter =>
                nextClass is LineBreakClass.Open or LineBreakClass.Prefix or LineBreakClass.Postfix
                    or LineBreakClass.Alphabetic or LineBreakClass.Hebrew or LineBreakClass.Numeric
                    or LineBreakClass.Glue,
            LineBreakClass.Prefix or LineBreakClass.Postfix =>
                nextClass is LineBreakClass.Prefix or LineBreakClass.Postfix
                || (nextClass == LineBreakClass.Open
                    && !(after.Length > 1 && char.IsDigit(after[1]) && (x > 0xFF || y > 0xFF))),
            _ => false,
        };
    }

    /// <summary>
    /// The line breaking classes of UAX #14 that <see cref="Between"/> tells apart: CL, CP, EX, IS,
    /// SY, PR, PO and BA before, OP, PR, PO, AL, HL, NU and GL after; any other is
    /// <see cref="LineBreakClass.Other"/>.
    /// </summary>
    private enum LineBreakClass
    {
        Other,
        Open,
        Close,
        CloseParenthesis,
        Exclamation,
        Infix,
        Solidus,
        Prefix,
        Postfix,
        BreakAfter,
        Glue,
        Alphabetic,
        Hebrew,
        Numeric,
    }

    /// <summary>
    /// The line breaking class of <paramref name="c"/> (UAX #14, LineBreak.txt), as far as
    /// <see cref="Between"/> needs it: letters and symbols are alphabetic, and quotation marks, dashes
    /// and the characters the other rules decide are none of the classes it tells apart.
    /// </summary>
    private static LineBreakClass ClassOf(char c)
    {
        switch (c)
        {
            case '(' or '[' or '{' or '\u00A1' or '\u00BF' or '\u2E18':
                return LineBreakClass.Open;
            case ')' or ']':
                return LineBreakClass.CloseParenthesis;
            case '!' or '?' or '\u05C6' or '\u061B' or (>= '\u061D' and <= '\u061F') or '\u06D4' or '\u07F9'
                or (>= '\u0F0D' and <= '\u0F11') or '\u0F14' or '\u1802' or '\u1803' or '\u1808' or '\u1809'
                or '\u1944' or '\u1945' or '\u2762' or '\u2763' or '\u2CF9' or '\u2CFE' or '\u2E2E' or '\uA60E'
                or '\uA876' or '\uA877' or '\uFE15' or '\uFE16' or '\uFE56' or '\uFE57' or '\uFF01' or '\uFF1F':
                return LineBreakClass.Exclamation;
            case ',' or '.' or ':' or ';' or '\u037E' or '\u0589' or '\u060C' or '\u060D' or '\u07F8' or '\u2044'
                or '\uFE10' or '\uFE13' or '\uFE14':
                return LineBreakClass.Infix;
            case '/':
                return LineBreakClass.Solidus;
            case '$' or '+' or '\\' or '\u00B1' or '\u2116' or '\u2212' or '\u2213':
                return LineBreakClass.Prefix;
            case '%' or '\u00A2' or '\u00B0' or (>= '\u0609' and <= '\u060B') or '\u066A' or '\u09F2' or '\u09F3'
                or '\u09F9' or '\u0D79' or (>= '\u2030' and <= '\u2037') or '\u20A7' or '\u20B6' or '\u20BB'
                or '\u20BE' or '\u20C0' or '\u2103' or '\u2109' or '\uA838' or '\uFDFC' or '\uFE6A' or '\uFF05'
                or '\uFFE0':
                return LineBreakClass.Postfix;
            case '|' or '\u0964' or '\u0965' or '\u2027' or '\u2056' or (>= '\u2058' and <= '\u205B') or '\u205D'
                or '\u205E':
                return LineBreakClass.BreakAfter;
            case '\u00A0' or '\u202F' or '\u2007' or '\u2011' or '\u180E' or '\u0F08' or '\u0F0C' or '\u0F12'
                or '\u034F':
                return LineBreakClass.Glue;
            case '"' or '\'' or '-':
                return LineBreakClass.Other;
            case '#' or '&' or '*' or '<' or '=' or '>' or '@' or '^' or '_' or '`' or '~' or '\u00A7' or '\u00B6'
                or '\u00B7' or '\u2020' or '\u2021' or '\u2022':
                return LineBreakClass.Alphabetic;
        }

        return char.GetUnicodeCategory(c) switch
        {
            UnicodeCategory.OpenPunctuation => LineBreakClass.Open,
            UnicodeCategory.ClosePunctuation => LineBreakClass.Close,
            UnicodeCategory.CurrencySymbol => LineBreakClass.Prefix,
            UnicodeCategory.DecimalDigitNumber => LineBreakClass.Numeric,
            UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
                or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter =>
                c is (>= '\u05D0' and <= '\u05F2') or (>= '\uFB1D' and <= '\uFB4F')
                    ? LineBreakClass.Hebrew
                    : LineBreakClass.Alphabetic,
            UnicodeCategory.MathSymbol or UnicodeCategory.OtherSymbol or UnicodeCategory.ModifierSymbol
                or UnicodeCategory.OtherNumber => LineBreakClass.Alphabetic,
            _ => LineBreakClass.Other,
        };
    }

    /// <summary>
    /// Whether a line may break before the first word of <paramref name="box"/>, after the in-flow
    /// content before it in its inline formatting context. With none before it, the word is first
    /// on its line and the answer does not matter.
    /// </summary>
    internal static bool BeforeFirstWord(CssBox box)
    {
        var first = box.Words.Count > 0 ? box.Words[0] : null;
        if (first != null && Before(first))
            return true;

        for (var node = box; SharesLines(node) && node.ParentBox is { } parent; node = parent)
        {
            // The words are inside a box that is a break before and after itself.
            if (MakesOpportunity(node))
                return true;

            for (int i = IndexIn(parent, node) - 1; i >= 0; i--)
            {
                if (AfterContentOf(parent.Boxes[i], first) is { } breaks)
                    return breaks;
            }
        }

        return true;
    }

    /// <summary>
    /// How far the content after <paramref name="box"/>'s last word reaches before a line may break
    /// in it, or its inline formatting context ends: the right margin, border and padding of each
    /// inline box that ends there, the box's own first, the left ones of each that begins there, and
    /// the words between. The walk stops as soon as it is past <paramref name="limit"/>.
    /// </summary>
    /// <remarks>
    /// The flow measures a box's words as it reaches the box, so the walk measures those of the
    /// boxes after it with <paramref name="g"/>. Without it, as when measuring intrinsic widths, the
    /// words are taken to be measured already.
    /// </remarks>
    internal static double RunAfterLastWord(ILayoutEnvironment? g, CssBox box, double limit)
    {
        double width = 0;
        var previous = box.Words[^1];

        for (var node = box; SharesLines(node) && node.ParentBox is { } parent; node = parent)
        {
            width += node.ActualMarginRight + node.ActualBorderRightWidth + node.ActualPaddingRight;

            if (width > limit || MakesOpportunity(node))
                return width;

            for (int i = IndexIn(parent, node) + 1; i < parent.Boxes.Count; i++)
            {
                if (RunInto(g, parent.Boxes[i], ref width, limit, ref previous))
                    return width;
            }
        }

        return width;
    }

    /// <summary>
    /// The border and padding of <paramref name="box"/> and of each inline box around it that
    /// <see cref="RunAfterLastWord"/> walks out of: on their right sides, and on their left sides
    /// where a run from the box's last word does not start next to them: on all of them with
    /// <paramref name="left"/>, and otherwise on those around a box with content before it.
    /// </summary>
    /// <remarks>
    /// A run from a box's only word starts next to the left edge of each box around it only as long
    /// as that box is the first content of the box around it. A span with 30px of left padding
    /// holding "aaa ", an i holding "bbb", then "ccc", had its padding counted before "bbbccc" as
    /// well as before "aaa": a float holding it came out 80.7px wide, where browsers make it 56.7px.
    /// A float, a box that is not displayed and white space that collapses are no content there:
    /// taken for some, a display:none span before the i dropped the padding, and the float came out
    /// 56.7px wide, where browsers make it 80.7px.
    /// </remarks>
    internal static double InlinePathEdges(CssBox box, bool left)
    {
        double edges = 0;

        for (var node = box; SharesLines(node) && node.ParentBox is { } parent; node = parent)
        {
            edges += node.ActualBorderRightWidth + node.ActualPaddingRight;

            if (left)
                edges += node.ActualBorderLeftWidth + node.ActualPaddingLeft;

            if (MakesOpportunity(node))
                break;

            left = left || HasContentBefore(parent, node);
        }

        return edges;
    }

    /// <summary>Whether in-flow content comes before <paramref name="node"/> in <paramref name="parent"/>.</summary>
    private static bool HasContentBefore(CssBox parent, CssBox node)
    {
        for (int i = IndexIn(parent, node) - 1; i >= 0; i--)
        {
            if (IsContent(parent.Boxes[i]))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether <paramref name="box"/> is or holds content on the line: not a box out of the flow or
    /// not displayed, nor white space that collapses.
    /// </summary>
    private static bool IsContent(CssBox box)
    {
        if (IsPositioned(box) || box.Display == CssConstants.None || box.Float != CssConstants.None)
            return false;

        if (MakesOpportunity(box) || box.Words.Count > 0)
            return true;

        foreach (var child in box.Boxes)
        {
            if (IsContent(child))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Adds to <paramref name="width"/> how far <paramref name="box"/>'s content reaches before a
    /// line may break in it, and says whether one may: false when the run goes on past its end.
    /// <paramref name="previous"/> is the last word of the run so far, and becomes the box's.
    /// </summary>
    private static bool RunInto(ILayoutEnvironment? g, CssBox box, ref double width, double limit, ref CssRect previous)
    {
        if (IsPositioned(box))
            return false;

        if (MakesOpportunity(box))
            return true;

        if (box.Words.Count == 0 && IsCollapsedSpace(box))
            return true;

        // Where the line breaks before the first content of inline boxes, their left margins,
        // borders and padding go to the next line with it: browsers carry the start of an inline box
        // that would end a line over to the next one. Counted in the run before them, they moved it
        // on too. In a 60px block, "x aaa" then a span with 25px of left padding holding a 10px
        // inline-block took three lines, "aaa" moved to the second and the inline-block to the
        // third, where browsers keep "x aaa" on the first; and so did a span holding " bbb", or one
        // holding "[1]" after "aaa.", and one holding a float or a hidden box before the inline-block.
        if (BreaksBeforeContentOf(box, previous) == true)
            return true;

        width += box.ActualMarginLeft + box.ActualBorderLeftWidth + box.ActualPaddingLeft;

        if (box.Words.Count > 0)
        {
            if (g != null)
                box.MeasureWordsSize(g);

            var first = box.Words[0];
            if (Before(first))
                return true;

            // CSS Text 3 §4.1.3: a space or a tab that `pre-wrap` keeps is a word of its own, the
            // line may break after it, and at the end of a line it hangs, so it is no part of what
            // has to fit. Counted, it moved a word that ends an element to the next line before a
            // box starting with a space, as syntax highlighters put spaces between their tokens:
            // "x ", a span holding "aaa", then a span holding " bbb" went on three lines of a 41px
            // pre-wrap block, where browsers keep "x aaa" on the first.
            if (box.WhiteSpace == CssConstants.PreWrap && first.Text is " " or "\t")
                return true;

            // A box that does not wrap has no break between its words, and a line break that `pre`
            // keeps ends the word. The words after one were measured with it: "aaa" then a pre span
            // holding "b", a line break and "cccccc" did not fit after "x " in a 60px block, and
            // "aaa" went to the next line, where browsers keep "aaab" beside "x".
            if (box.WhiteSpace is CssConstants.NoWrap or CssConstants.Pre)
            {
                for (int i = 0; i < box.Words.Count; i++)
                {
                    var word = box.Words[i];
                    if (word.IsLineBreak)
                        return true;

                    width += i < box.Words.Count - 1 ? word.FullWidth : word.Width;
                }
            }
            else
            {
                width += first.Width;

                if (box.Words.Count > 1)
                    return true;
            }

            previous = box.Words[^1];

            if (width > limit || After(previous))
                return true;
        }
        else
        {
            foreach (var child in box.Boxes)
            {
                if (RunInto(g, child, ref width, limit, ref previous))
                    return true;
            }
        }

        width += box.ActualMarginRight + box.ActualBorderRightWidth + box.ActualPaddingRight;
        return width > limit;
    }

    /// <summary>
    /// Whether a line may break after the in-flow content of <paramref name="box"/>, before
    /// <paramref name="next"/> where that is given: null when it has none, and the line may break
    /// there or not as though the box were not there.
    /// </summary>
    private static bool? AfterContentOf(CssBox box, CssRect? next)
    {
        if (IsPositioned(box))
            return null;

        if (MakesOpportunity(box))
            return true;

        if (box.Words.Count > 0)
            return After(box.Words[^1]) || (next != null && Between(box.Words[^1], next));

        if (IsCollapsedSpace(box))
            return true;

        for (int i = box.Boxes.Count - 1; i >= 0; i--)
        {
            if (AfterContentOf(box.Boxes[i], next) is { } breaks)
                return breaks;
        }

        return null;
    }

    /// <summary>
    /// Whether a line may break before the first in-flow content of <paramref name="box"/>, an
    /// inline box, after <paramref name="previous"/>: before an atomic inline, a float or a box that
    /// is not displayed (<see cref="MakesOpportunity"/>), white space that collapses, or a word the
    /// line may break before by its text, or with <paramref name="previous"/>. Not before a
    /// <c>&lt;wbr&gt;</c> or a line break, after which browsers keep the start of the box on the line
    /// before. Null when the box holds no in-flow content.
    /// </summary>
    private static bool? BreaksBeforeContentOf(CssBox box, CssRect previous)
    {
        if (box.Words.Count > 0)
        {
            var first = box.Words[0];
            return first.IsImage
                || (first.HasSpaceBefore && box.WhiteSpace is CssConstants.Normal or CssConstants.PreLine)
                || (!string.IsNullOrEmpty(first.Text) && BreaksBefore(first.Text[0]))
                || Between(previous, first);
        }

        foreach (var child in box.Boxes)
        {
            if (IsPositioned(child))
                continue;

            if (IsAtomicInline(child) || child.Display == CssConstants.None || child.Float != CssConstants.None
                || (child.Words.Count == 0 && IsCollapsedSpace(child)))
                return true;

            if (MakesOpportunity(child))
                return false;

            if (BreaksBeforeContentOf(child, previous) is { } breaks)
                return breaks;
        }

        return null;
    }

    /// <summary>
    /// Whether <paramref name="box"/> is an atomic inline in the flow: an inline-block or another box
    /// laid out whole on the line, a replaced element, or a <c>&lt;math&gt;</c> element.
    /// </summary>
    private static bool IsAtomicInline(CssBox box) =>
        box.Float == CssConstants.None
        && box.Position is not (CssConstants.Absolute or CssConstants.Fixed)
        && (CssBoxHelper.IsAtomicInlineLevel(box.Display)
            || (box.Display == CssConstants.Inline
                && (box.IsReplaced || box.HtmlTag?.Name.Equals("math", StringComparison.OrdinalIgnoreCase) == true)));

    /// <summary>
    /// Whether a line may break before and after <paramref name="box"/> whatever is around it: a
    /// <c>&lt;wbr&gt;</c>, an atomic inline or a replaced element, a <c>&lt;math&gt;</c> element,
    /// which browsers lay out whole as an atomic inline (MathML Core, <c>display: math</c>), or a
    /// block-level box, which takes lines of its own, as a <c>&lt;br&gt;</c> does here.
    /// </summary>
    /// <remarks>
    /// A float, block-level, and a box that is not displayed are taken for one too. They are no
    /// break in themselves, but the white space beside them does not reach the layout: a text box
    /// holding only white space is dropped when the tree is built where a block-level box or a box
    /// that is not displayed is its neighbour. Taken for none, they joined the words around them
    /// into one: "gggg&lt;i style=float:left&gt;&lt;/i&gt; &lt;span&gt;after&lt;/span&gt;" kept
    /// "after" on the line after "gggg", past the block's edge, where browsers break at the space.
    /// </remarks>
    private static bool MakesOpportunity(CssBox box) =>
        box.Display != CssConstants.Inline
        || box.IsReplaced
        || (box.HtmlTag?.Name is { } name
            && (name.Equals("wbr", StringComparison.OrdinalIgnoreCase) || name.Equals("math", StringComparison.OrdinalIgnoreCase)));

    /// <summary>A text box that held only white space, collapsed to the space between its neighbours.</summary>
    private static bool IsCollapsedSpace(CssBox box) =>
        box.Boxes.Count == 0 && box.HtmlTag == null && !box.Text.IsEmpty && box.Text.Span.IsWhiteSpace();

    /// <summary>
    /// Whether <paramref name="box"/> is an inline box that is absolutely or fixed positioned: out
    /// of the flow, and no break (CSS 2.1 §9.6). The white space beside it is kept.
    /// </summary>
    private static bool IsPositioned(CssBox box) =>
        box.Display == CssConstants.Inline
        && box.Float == CssConstants.None
        && box.Position is CssConstants.Absolute or CssConstants.Fixed;

    /// <summary>
    /// Whether the content of <paramref name="box"/> goes on the lines of the box around it: it is an
    /// inline box, and not one that is absolutely or fixed positioned, which keeps its display here
    /// but lays its content out on lines of its own. The walks went on out of one into the text after
    /// it: "x aaa" in a 60px positioned span, followed by "bbbbbbbb" outside it, went on two lines.
    /// </summary>
    private static bool SharesLines(CssBox box) => box.Display == CssConstants.Inline && !IsPositioned(box);

    /// <summary>
    /// Where <paramref name="node"/> is among <paramref name="parent"/>'s boxes. The flow and the
    /// intrinsic widths reach the boxes of a parent in order, so each lookup in a parent with many
    /// starts from where the last one found its box. <c>IndexOf</c> made a paragraph of thousands of
    /// elements with spaces between them take time that grows with the square of their number.
    /// </summary>
    private static int IndexIn(CssBox parent, CssBox node)
    {
        var boxes = parent.Boxes;
        if (boxes.Count <= 8)
            return boxes.IndexOf(node);

        var last = LastIndexIn.GetValue(parent, static _ => new StrongBox<int>());
        int hint = Math.Min(last.Value, boxes.Count - 1);

        for (int d = 0; d < boxes.Count; d++)
        {
            if (hint + d < boxes.Count && boxes[hint + d] == node)
                return last.Value = hint + d;

            if (hint - d - 1 >= 0 && boxes[hint - d - 1] == node)
                return last.Value = hint - d - 1;
        }

        return -1;
    }

    /// <summary>Where <see cref="IndexIn"/> last found a box among each parent's boxes.</summary>
    private static readonly ConditionalWeakTable<CssBox, StrongBox<int>> LastIndexIn = new();

    private static bool BreaksAfterEveryCharacter(CssBox? box) =>
        box != null
        && (box.WordBreak == CssConstants.BreakAll
            || string.Equals(box.LineBreak, "anywhere", StringComparison.OrdinalIgnoreCase));
}

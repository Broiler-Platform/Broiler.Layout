using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Broiler.Layout.IR;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An empty inline box is as tall as its font's content area, around its baseline, and as wide as
/// its horizontal padding and border, where the flow put it on its line.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §9.4.2 and §10.8, CSS Inline 3: an empty inline element generates an empty inline box,
/// which still has margins, padding, borders and a line height. Its content area is as tall as its
/// font and 0 wide, and its padding and border reach out from it as they do from any inline box's.
/// The engine gave such a box no rectangle on its line, since one would have made a line holding
/// nothing else count as content and take the strut's height, only a location: the left of its
/// content at the top of its line. Script read it there, 0 × 0, and nothing of its background or
/// border was painted: in 16px/20px text, an empty span with <c>padding: 0 2px</c> was 0 × 0 at the
/// top of its line, 2px right of where it starts, where browsers make it 4 × 17 around its baseline.
/// </para>
/// <para>
/// Each block here is 200px wide with 20px lines of a 16px font, whose glyphs stand 2px down their
/// line, with the baseline 14.8px down. Words are 8px wide a letter, and a space is 4px wide. The
/// rectangles are what script reads for the box (<c>getBoundingClientRect</c>), from the block's
/// top-left corner.
/// </para>
/// </remarks>
public sealed class EmptyInlineBoxRectangleTests
{
    private static readonly Uri BaseUrl = new("file:///empty-inline-box.html");

    /// <summary>
    /// "a", an empty span, then "b": the span is 0px wide at "b", 8px in, and as tall as its 16px
    /// font, 2px down, where the glyphs beside it are. It was 0 × 0 at the top of the line.
    /// </summary>
    [Fact]
    public void An_Empty_Span_Is_As_Tall_As_Its_Font()
    {
        var block = Block();
        Text(block, "a");
        var span = Span(block);
        Text(block, "b");
        Layout(block);

        AssertRectangle(8, 2, 0, 16, ScriptRectangle(span, block));
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// "a", an empty span with <c>padding: 0 2px</c>, then "b": the span is 4px wide from 8px in,
    /// 16px tall from 2px down, and "b" follows it 12px in. It was 0 × 0 at the top of the line,
    /// 10px in.
    /// </summary>
    [Fact]
    public void An_Empty_Span_Is_As_Wide_As_Its_Horizontal_Padding()
    {
        var block = Block();
        Text(block, "a");
        var span = Span(block);
        span.PaddingLeft = span.PaddingRight = "2px";
        Text(block, "b");
        Layout(block);

        AssertRectangle(8, 2, 4, 16, ScriptRectangle(span, block));
        Assert.Equal(12, Word(block, "b").Left - block.Location.X, 1);
    }

    /// <summary>
    /// "a", an empty span with <c>padding: 3px</c> and a 1px border, then "b": the span's border box
    /// reaches 4px beyond its content area on every side, 8 × 24 from 8px in and 2px above the
    /// block. The line stays 20px tall, as vertical padding and border take no part in it, and "b"
    /// stands 16px in, 2px down. The span was 0 × 0 at the top of the line, 12px in.
    /// </summary>
    [Fact]
    public void An_Empty_Spans_Vertical_Padding_And_Border_Reach_Beyond_Its_Line()
    {
        var block = Block();
        Text(block, "a");
        var span = Span(block);
        span.PaddingLeft = span.PaddingRight = span.PaddingTop = span.PaddingBottom = "3px";
        span.BorderLeftWidth = span.BorderRightWidth = span.BorderTopWidth = span.BorderBottomWidth = "1px";
        span.BorderLeftStyle = span.BorderRightStyle = span.BorderTopStyle = span.BorderBottomStyle = "solid";
        Text(block, "b");
        Layout(block);

        AssertRectangle(8, -2, 8, 24, ScriptRectangle(span, block));
        Assert.Equal(20, block.Size.Height, 1);
        Assert.Equal(16, Word(block, "b").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "b").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// "a", an empty span in an 8px font with <c>line-height: 10px</c>, then "b": the span stands on
    /// the line's baseline, 14.8px down, as tall as its 8px font, so from 8.4px down. It was 0 × 0 at
    /// the top of the line.
    /// </summary>
    [Fact]
    public void An_Empty_Span_Is_As_Tall_As_Its_Own_Font_On_The_Baseline()
    {
        var block = Block();
        Text(block, "a");
        var span = Span(block, "8px");
        span.LineHeight = "10px";
        Text(block, "b");
        Layout(block);

        AssertRectangle(8, 8.4, 0, 8, ScriptRectangle(span, block));
    }

    /// <summary>
    /// "a", an empty span aligned <c>sub</c> with <c>line-height: 10px</c>, then "b": the span's
    /// baseline is 4.2px below the line's (a fifth of the parent's 16px font and a pixel), 19px
    /// down, and its 16px font reaches from 6.2px down. It was 0 × 0 at the top of the line.
    /// </summary>
    [Fact]
    public void An_Empty_Span_Stands_Where_Its_Vertical_Alignment_Puts_It()
    {
        var block = Block();
        Text(block, "a");
        var span = Span(block);
        span.LineHeight = "10px";
        span.VerticalAlign = CssConstants.Sub;
        Text(block, "b");
        Layout(block);

        AssertRectangle(8, 6.2, 0, 16, ScriptRectangle(span, block));
    }

    /// <summary>
    /// "a", an empty span with <c>padding: 0 2px</c>, then "b", centred: the 20px of content moves
    /// 90px along the 200px line, and the span with it, 98px in. It stayed 0 × 0 where the flow had
    /// put its content, 10px in.
    /// </summary>
    [Fact]
    public void An_Empty_Span_Moves_With_Its_Centred_Line()
    {
        var block = Block();
        block.TextAlign = CssConstants.Center;
        Text(block, "a");
        var span = Span(block);
        span.PaddingLeft = span.PaddingRight = "2px";
        Text(block, "b");
        Layout(block);

        AssertRectangle(98, 2, 4, 16, ScriptRectangle(span, block));
        Assert.Equal(102, Word(block, "b").Left - block.Location.X, 1);
    }

    /// <summary>
    /// "abc", then an empty span with <c>padding: 0 2px</c>, right-aligned: the line's content ends
    /// where the span's padding does, so "abc" starts 172px in and the span ends at the line's end,
    /// 4px wide from 196px in. "abc" started 176px in, as though the line ended with it, and the
    /// span was 0 × 0 at the top of the line, 26px in.
    /// </summary>
    [Fact]
    public void An_Empty_Span_Ending_A_Right_Aligned_Line_Ends_It()
    {
        var block = Block();
        block.TextAlign = CssConstants.Right;
        Text(block, "abc");
        var span = Span(block);
        span.PaddingLeft = span.PaddingRight = "2px";
        Layout(block);

        Assert.Equal(172, Word(block, "abc").Left - block.Location.X, 1);
        AssertRectangle(196, 2, 4, 16, ScriptRectangle(span, block));
    }

    /// <summary>
    /// "a", a link holding only an empty span with <c>padding: 0 3px</c>, then "b": the link wraps
    /// the span, 6 × 16 from 8px in and 2px down. It had no place at all: 0 × 0 at the top-left of
    /// the page.
    /// </summary>
    [Fact]
    public void An_Inline_Box_Around_An_Empty_Span_Wraps_It()
    {
        var block = Block();
        Text(block, "a");
        var link = Span(block, tag: "a");
        var span = Span(link);
        span.PaddingLeft = span.PaddingRight = "3px";
        Text(block, "b");
        Layout(block);

        AssertRectangle(8, 2, 6, 16, ScriptRectangle(link, block));
    }

    /// <summary>
    /// "a", then a link with <c>padding: 0 1px</c> holding an empty span with <c>padding: 0 3px</c>
    /// and "b": the link starts where it opens, 8px in, and is 1 + 6 + 8 + 1 = 16px wide. It started
    /// at "b", 1px before it, and was 10px wide.
    /// </summary>
    [Fact]
    public void An_Inline_Box_Starting_With_An_Empty_Span_Starts_Before_It()
    {
        var block = Block();
        Text(block, "a");
        var link = Span(block, tag: "a");
        link.PaddingLeft = link.PaddingRight = "1px";
        var span = Span(link);
        span.PaddingLeft = span.PaddingRight = "3px";
        Text(link, "b");
        Layout(block);

        AssertRectangle(8, 2, 16, 16, ScriptRectangle(link, block));
    }

    /// <summary>
    /// "aaaa bbbb", an empty span with <c>padding: 0 2px</c>, then " cc", in a 40px block: "bbbb"
    /// and the span wrap to the second line, where the span is 4px wide from 32px in, 22px down.
    /// It was 0 × 0 at the top of that line, 34px in.
    /// </summary>
    [Fact]
    public void An_Empty_Span_On_A_Wrapped_Line_Stands_On_That_Line()
    {
        var block = Block("40px");
        Text(block, "aaaa bbbb");
        var span = Span(block);
        span.PaddingLeft = span.PaddingRight = "2px";
        Text(block, " cc");
        Layout(block);

        AssertRectangle(32, 22, 4, 16, ScriptRectangle(span, block));
        Assert.Equal(60, block.Size.Height, 1);
    }

    /// <summary>
    /// "a", an empty span with <c>padding: 0 2px</c> positioned relatively 3px right and 5px down,
    /// then "b": the span moves from where the flow put it, to 11px in and 7px down, and "b" stays
    /// 12px in. It was 0 × 0 at 13px in and 5px down.
    /// </summary>
    [Fact]
    public void An_Empty_Span_Takes_Its_Relative_Offset()
    {
        var block = Block();
        Text(block, "a");
        var span = Span(block);
        span.PaddingLeft = span.PaddingRight = "2px";
        span.Position = CssConstants.Relative;
        span.Left = "3px";
        span.Top = "5px";
        Text(block, "b");
        Layout(block);

        AssertRectangle(11, 7, 4, 16, ScriptRectangle(span, block));
        Assert.Equal(12, Word(block, "b").Left - block.Location.X, 1);
    }

    /// <summary>
    /// "a", an empty relatively positioned span holding a 5 × 5px box positioned absolutely at
    /// <c>top: 0; left: 0</c>, then "b": the span is the box's containing block, and the box stands
    /// at its top-left corner, 8px in and 2px down. It stood at the top of the line.
    /// </summary>
    [Fact]
    public void A_Box_Positioned_In_An_Empty_Span_Stands_At_Its_Top()
    {
        var block = Block();
        Text(block, "a");
        var span = Span(block);
        span.Position = CssConstants.Relative;
        var positioned = AbsoluteBox(span);
        Text(block, "b");
        Layout(block);

        Assert.Equal(8, positioned.Location.X - block.Location.X, 1);
        Assert.Equal(2, positioned.Location.Y - block.Location.Y, 1);
    }

    /// <summary>
    /// "a", an empty span with <c>padding: 0 2px</c>, then "b": the fragment painted for the span
    /// carries its rectangle on the line, so its background and border are drawn there, 4 × 16 from
    /// 8px in and 2px down. It carried none, and nothing was drawn.
    /// </summary>
    [Fact]
    public void An_Empty_Spans_Background_Is_Painted_Where_It_Is()
    {
        var block = Block();
        Text(block, "a");
        var span = Span(block);
        span.PaddingLeft = span.PaddingRight = "2px";
        Text(block, "b");
        Layout(block);

        var fragment = Fragments(FragmentTreeBuilder.Build(block.ParentBox!.ParentBox!))
            .Single(f => f.Padding.Left == 2 && f.Padding.Right == 2);

        Assert.NotNull(fragment.InlineRects);
        var rect = Assert.Single(fragment.InlineRects!);
        AssertRectangle(8, 2, 4, 16, Relative(rect, block));
    }

    /// <summary>
    /// "abc ", an empty span with <c>padding: 0 2px</c>, a space, and another such span, ending the
    /// line: the spaces before the spans are at the end of the line and removed (CSS Text 3 §4.1.3),
    /// so the first span stands against "abc", 4px wide from 24px in, and the second against the
    /// first, from 28px in. They were 0 × 0 at the top of the line, 30px and 38px in.
    /// </summary>
    [Fact]
    public void Empty_Spans_Ending_A_Line_Stand_Against_Its_Last_Word()
    {
        var block = Block();
        Text(block, "abc ");
        var first = Span(block);
        first.PaddingLeft = first.PaddingRight = "2px";
        Text(block, " ");
        var second = Span(block);
        second.PaddingLeft = second.PaddingRight = "2px";
        Layout(block);

        AssertRectangle(24, 2, 4, 16, ScriptRectangle(first, block));
        AssertRectangle(28, 2, 4, 16, ScriptRectangle(second, block));
    }

    /// <summary>
    /// "abc ", then an empty span with <c>padding: 0 2px</c>, right-aligned: the space before the
    /// span is removed at the end of the line, which ends with the span, so "abc" starts 172px in
    /// and the span stands against it, 4px wide from 196px in. "abc" started 176px in, and the span
    /// was 0 × 0 at the top of the line, 30px in.
    /// </summary>
    [Fact]
    public void An_Empty_Span_After_A_Space_Ends_A_Right_Aligned_Line()
    {
        var block = Block();
        block.TextAlign = CssConstants.Right;
        Text(block, "abc ");
        var span = Span(block);
        span.PaddingLeft = span.PaddingRight = "2px";
        Layout(block);

        Assert.Equal(172, Word(block, "abc").Left - block.Location.X, 1);
        AssertRectangle(196, 2, 4, 16, ScriptRectangle(span, block));
    }

    /// <summary>
    /// "abc", then a link with <c>padding-right: 10px</c> holding only an empty span, right-aligned:
    /// the line ends where the link's padding does, so "abc" starts 166px in and the link stands
    /// against it, 10 × 16 from 190px in. "abc" started 176px in, as though the line ended with it,
    /// and the link had no place at all, 0 × 0 at the top-left of the page.
    /// </summary>
    [Fact]
    public void An_Inline_Box_Closing_After_An_Empty_Span_Ends_A_Right_Aligned_Line()
    {
        var block = Block();
        block.TextAlign = CssConstants.Right;
        Text(block, "abc");
        var link = Span(block, tag: "a");
        link.PaddingRight = "10px";
        Span(link);
        Layout(block);

        Assert.Equal(166, Word(block, "abc").Left - block.Location.X, 1);
        AssertRectangle(190, 2, 10, 16, ScriptRectangle(link, block));
    }

    /// <summary>
    /// The same, right to left: the line is mirrored, "abc" starting it at its right end, 176px in,
    /// and the link ending it on its left, against "abc", 10 × 16 from 166px in, its padding on the
    /// right of the empty span in it. The link had no place at all, 0 × 0 at the top-left of the
    /// page.
    /// </summary>
    [Fact]
    public void An_Inline_Box_Closing_After_An_Empty_Span_Ends_A_Right_To_Left_Line()
    {
        var block = Block();
        block.Direction = CssConstants.Rtl;
        Text(block, "abc");
        var link = Span(block, tag: "a");
        link.PaddingRight = "10px";
        Span(link);
        Layout(block);

        Assert.Equal(176, Word(block, "abc").Left - block.Location.X, 1);
        AssertRectangle(166, 2, 10, 16, ScriptRectangle(link, block));
    }

    /// <summary>
    /// "aaaa ", an empty span with <c>padding: 0 2px</c>, then "bbbb", in a 40px block: "bbbb" wraps
    /// to the second line, and the span stays at the end of the first, against "aaaa", 4px wide from
    /// 32px in, as browsers keep it. It was 0 × 0 at the top of the line, 38px in.
    /// </summary>
    [Fact]
    public void An_Empty_Span_After_A_Space_Where_The_Line_Wraps_Stands_Against_The_Word()
    {
        var block = Block("40px");
        Text(block, "aaaa ");
        var span = Span(block);
        span.PaddingLeft = span.PaddingRight = "2px";
        Text(block, "bbbb");
        Layout(block);

        AssertRectangle(32, 2, 4, 16, ScriptRectangle(span, block));
        Assert.Equal(22, Word(block, "bbbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// "aa ", an empty span with <c>padding: 0 2px</c>, "bb cc", another such span, " dd", then a word
    /// too long for the line, justified in 200px: the words of the first line move apart, keeping
    /// the room the spans take beside them, 4px each, out of what the spaces share (CSS Text 3 §7.3).
    /// "bb" goes 52px in and "cc" 100px in. The first span, after the space, stays against "bb", 4px
    /// wide from 48px in, and the second, with no space before it, stays against "cc", from 116px
    /// in. They were 0 × 0 at the top of the line, 22px and 62px in, where the flow had put them
    /// before the words moved, and "bb" went 50px in.
    /// </summary>
    [Fact]
    public void Empty_Spans_On_A_Justified_Line_Keep_To_Their_Words()
    {
        var block = Block();
        block.TextAlign = CssConstants.Justify;
        Text(block, "aa ");
        var first = Span(block);
        first.PaddingLeft = first.PaddingRight = "2px";
        Text(block, "bb cc");
        var second = Span(block);
        second.PaddingLeft = second.PaddingRight = "2px";
        Text(block, " dd eeeeeeeeeeeeeeeeeeeeeeeee");
        Layout(block);

        Assert.Equal(52, Word(block, "bb").Left - block.Location.X, 1);
        Assert.Equal(100, Word(block, "cc").Left - block.Location.X, 1);
        AssertRectangle(48, 2, 4, 16, ScriptRectangle(first, block));
        AssertRectangle(116, 2, 4, 16, ScriptRectangle(second, block));
    }

    /// <summary>
    /// An empty span with <c>padding: 0 8px</c>, then "aa bb cc " and a word too long for the line,
    /// justified in 200px: the span starts the line, 16px wide from its start, and "aa" follows it,
    /// 16px in, while "cc" ends the line. Justification left the span out: "aa" went to the start
    /// of the line, over the span, and the span was 0 × 0 at the top of the line, 8px in.
    /// </summary>
    [Fact]
    public void An_Empty_Span_Starting_A_Justified_Line_Starts_It()
    {
        var block = Block();
        block.TextAlign = CssConstants.Justify;
        var span = Span(block);
        span.PaddingLeft = span.PaddingRight = "8px";
        Text(block, "aa bb cc ");
        Text(block, "eeeeeeeeeeeeeeeeeeeeeeeee");
        Layout(block);

        AssertRectangle(0, 2, 16, 16, ScriptRectangle(span, block));
        Assert.Equal(16, Word(block, "aa").Left - block.Location.X, 1);
        Assert.Equal(184, Word(block, "cc").Left - block.Location.X, 1);
    }

    /// <summary>
    /// "aa bb cc ", an empty span with <c>padding: 0 8px</c>, then a word too long for the line,
    /// justified in 200px: the span ends the line, against "cc", 16px wide from 184px in, and "cc"
    /// ends 16px before the line does, 168px in. The span was 0 × 0 at the top of the line, 68px in,
    /// and "cc" ended the line, 184px in.
    /// </summary>
    [Fact]
    public void An_Empty_Span_Ending_A_Justified_Line_Ends_It_Inside_The_Block()
    {
        var block = Block();
        block.TextAlign = CssConstants.Justify;
        Text(block, "aa bb cc ");
        var span = Span(block);
        span.PaddingLeft = span.PaddingRight = "8px";
        Text(block, "eeeeeeeeeeeeeeeeeeeeeeeee");
        Layout(block);

        AssertRectangle(184, 2, 16, 16, ScriptRectangle(span, block));
        Assert.Equal(168, Word(block, "cc").Left - block.Location.X, 1);
    }

    /// <summary>
    /// An empty span, then " abc def": the space after the span starts the line and is removed
    /// (CSS Text 3 §4.1.3), so "abc" starts the line with the span, both at its start. "abc" stood
    /// a space in, 4px, and the span was 0 × 0 at the top of the line.
    /// </summary>
    [Fact]
    public void A_Space_After_An_Empty_Span_Starting_A_Line_Is_Removed()
    {
        var block = Block();
        var span = Span(block);
        Text(block, " abc def");
        Layout(block);

        Assert.Equal(0, Word(block, "abc").Left - block.Location.X, 1);
        AssertRectangle(0, 2, 0, 16, ScriptRectangle(span, block));
    }

    /// <summary>
    /// The same right to left: the line is mirrored, and the span starts it on the right, 200px
    /// in, with "abc" against it, from 176px in. The span was 0 × 0 at the top-left of the line.
    /// </summary>
    [Fact]
    public void An_Empty_Span_And_A_Space_Starting_A_Right_To_Left_Line_Stand_At_Its_Right()
    {
        var block = Block();
        block.Direction = CssConstants.Rtl;
        var span = Span(block);
        Text(block, " abc def");
        Layout(block);

        Assert.Equal(176, Word(block, "abc").Left - block.Location.X, 1);
        AssertRectangle(200, 2, 0, 16, ScriptRectangle(span, block));
    }

    /// <summary>
    /// <c>white-space: pre-line</c>: "x" and a preserved newline, an empty span, then " abc": the
    /// newline is a forced break, and the space after the span starts the second line and is
    /// removed there: "abc" stands at its start, 22px down. It stood a space in, 4px.
    /// </summary>
    [Fact]
    public void A_Space_After_An_Empty_Span_After_A_Forced_Break_Is_Removed()
    {
        var block = Block();
        block.WhiteSpace = CssConstants.PreLine;
        Text(block, "x\n");
        Span(block);
        Text(block, " abc");
        Layout(block);

        Assert.Equal(0, Word(block, "abc").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "abc").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// "aaaa bbbb ", a link with <c>padding-left: 4px</c> holding an empty span with
    /// <c>padding-left: 16px</c> (an icon) and "link", then " x", in a 100px block: "link" does not
    /// fit, and the line breaks at the space before the link, which starts the second line whole
    /// with its icon: the link is 4 + 16 + 32 = 52px wide from its start, 22px down, the icon 16px
    /// wide from 4px in, and "link" 20px in. The icon was 0 × 0 at the top of the first line, 92px
    /// in, and the link held "link" alone, without its padding, at the start of the second line.
    /// </summary>
    [Fact]
    public void A_Link_Starting_With_An_Empty_Span_Wraps_Whole()
    {
        var block = Block("100px");
        Text(block, "aaaa bbbb ");
        var link = Span(block, tag: "a");
        link.PaddingLeft = "4px";
        var icon = Span(link);
        icon.PaddingLeft = "16px";
        Text(link, "link");
        Text(block, " x");
        Layout(block);

        AssertRectangle(0, 22, 52, 16, ScriptRectangle(link, block));
        AssertRectangle(4, 22, 16, 16, ScriptRectangle(icon, block));
        Assert.Equal(20, Word(block, "link").Left - block.Location.X, 1);
    }

    /// <summary>
    /// The same with the link's markup indented: a newline before the icon in the link. The newline
    /// follows the space before the link and collapses (CSS Text 3 §4.1.1): the link starts the
    /// second line with its icon, 16px wide at its start, and "link" 16px in. The icon was 0 × 0 at
    /// the top of the first line, 92px in, and "link" started the second line.
    /// </summary>
    [Fact]
    public void A_Link_Starting_With_White_Space_And_An_Empty_Span_Wraps_Whole()
    {
        var block = Block("100px");
        Text(block, "aaaa bbbb ");
        var link = Span(block, tag: "a");
        Text(link, "\n");
        var icon = Span(link);
        icon.PaddingLeft = "16px";
        Text(link, "link");
        Text(block, " x");
        Layout(block);

        AssertRectangle(0, 22, 16, 16, ScriptRectangle(icon, block));
        Assert.Equal(16, Word(block, "link").Left - block.Location.X, 1);
    }

    /// <summary>
    /// "aaaa bbbb ", then a link holding an empty span with <c>padding-left: 16px</c> and a 30 × 10px
    /// inline-block, in a 100px block: the inline-block does not fit, and the link starts the
    /// second line with its icon, 16px wide at its start, 22px down, and the inline-block 16px in.
    /// The icon was 0 × 0 at the top of the first line, 88px in, and the inline-block started the
    /// second line.
    /// </summary>
    [Fact]
    public void A_Link_Starting_With_An_Empty_Span_Wraps_Whole_With_Its_Inline_Block()
    {
        var block = Block("100px");
        Text(block, "aaaa bbbb ");
        var link = Span(block, tag: "a");
        var icon = Span(link);
        icon.PaddingLeft = "16px";
        var inlineBlock = new CssBox(link, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = "30px",
            Height = "10px",
        };
        Text(block, " x");
        Layout(block);

        AssertRectangle(0, 22, 16, 16, ScriptRectangle(icon, block));
        Assert.Equal(16, inlineBlock.Location.X - block.Location.X, 1);
    }

    /// <summary>
    /// "aaaa ", then a link holding an empty span with <c>padding: 0 2px</c> and "bbbb", in a 34px
    /// block: "aaaa" and the space after it already fill the line, and browsers leave the link's
    /// start and the span there, against "aaaa", 4px wide from 32px in, and put "bbbb" on the next
    /// line. The span was 0 × 0 at the top of the line, 38px in.
    /// </summary>
    [Fact]
    public void An_Empty_Span_Starting_A_Link_Stays_On_A_Line_Its_Content_And_Space_Fill()
    {
        var block = Block("34px");
        Text(block, "aaaa ");
        var link = Span(block, tag: "a");
        var span = Span(link);
        span.PaddingLeft = span.PaddingRight = "2px";
        Text(link, "bbbb");
        Layout(block);

        AssertRectangle(32, 2, 4, 16, ScriptRectangle(span, block));
        Assert.Equal(22, Word(block, "bbbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// "aaaa bbbb", then a link holding a space, an empty span with <c>padding-left: 4px</c> and
    /// "link", then " x", in a 100px block: the space in the link is where the line breaks, so the
    /// link starts on the first line, and the span after the space stays there too, at the end of
    /// the line against "bbbb", 4px wide from 68px in, as browsers keep it. It was 0 × 0 at the top
    /// of the line, 76px in.
    /// </summary>
    [Fact]
    public void An_Empty_Span_After_White_Space_Inside_A_Link_Stays_Where_The_Line_Breaks()
    {
        var block = Block("100px");
        Text(block, "aaaa bbbb");
        var link = Span(block, tag: "a");
        Text(link, " ");
        var span = Span(link);
        span.PaddingLeft = "4px";
        Text(link, "link");
        Text(block, " x");
        Layout(block);

        AssertRectangle(68, 2, 4, 16, ScriptRectangle(span, block));
        Assert.Equal(22, Word(block, "link").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// "abc", an absolutely positioned span holding "x " (its text ends in a space), then an empty
    /// span with <c>padding: 0 2px</c>: the positioned span is out of the flow, and no space lies
    /// between "abc" and the empty span, which stands against "abc", 4px wide from 24px in. It was
    /// 0 × 0 at the top of the line, 26px in.
    /// </summary>
    [Fact]
    public void A_Positioned_Spans_White_Space_Leaves_An_Empty_Span_Against_The_Word_Before()
    {
        var block = Block();
        Text(block, "abc");
        Positioned(block, "x ");
        var span = Span(block);
        span.PaddingLeft = span.PaddingRight = "2px";
        Layout(block);

        AssertRectangle(24, 2, 4, 16, ScriptRectangle(span, block));
    }

    /// <summary>
    /// A justified line in 200px: "aa bb ", an empty span with <c>padding: 0 2px</c>, an absolutely
    /// positioned span holding "tip" 400px along, then "cc dd" and a word too long for the line:
    /// the empty span keeps to "cc", the next word in the flow, and ends where "cc" starts. It was
    /// 0 × 0 at the top of the line, 42px in.
    /// </summary>
    [Fact]
    public void An_Empty_Span_Before_A_Positioned_Box_On_A_Justified_Line_Keeps_To_The_Next_Word()
    {
        var block = Block();
        block.TextAlign = CssConstants.Justify;
        Text(block, "aa bb ");
        var span = Span(block);
        span.PaddingLeft = span.PaddingRight = "2px";
        Positioned(block, "tip");
        Text(block, "cc dd eeeeeeeeeeeeeeeeeeeeeeeee");
        Layout(block);

        double cc = Word(block, "cc").Left - block.Location.X;
        AssertRectangle(cc - 4, 2, 4, 16, ScriptRectangle(span, block));
    }

    /// <summary>
    /// Right to left, in a 40px block: "abcd ", an empty span with <c>padding-left: 16px</c>, then
    /// "efgh", which wraps. The span ends the first line and overflows it at its left, its end,
    /// 16px wide from 8px left of the block, with "abcd" against the line's right edge. The span
    /// was 0 × 0 at the top of the line, 52px in.
    /// </summary>
    [Fact]
    public void An_Empty_Span_Overflowing_A_Right_To_Left_Line_Overflows_It_On_The_Left()
    {
        var block = Block("40px");
        block.Direction = CssConstants.Rtl;
        Text(block, "abcd ");
        var span = Span(block);
        span.PaddingLeft = "16px";
        Text(block, "efgh");
        Layout(block);

        AssertRectangle(-8, 2, 16, 16, ScriptRectangle(span, block));
    }

    /// <summary>
    /// An empty span with <c>padding: 0 2px</c>, then "abcdefgh xyz", right to left: the line is
    /// mirrored, and the span with it, to its right end, 4px wide from 196px in, where it starts the
    /// line; "abcdefgh" stands against it, from 132px in, and "xyz" from 104px in. The span was
    /// 0 × 0 at the top of the line, 2px in, at the left end where the flow had put it, and
    /// "abcdefgh" and "xyz" stood 136px and 108px in, as though the span took no room.
    /// </summary>
    [Fact]
    public void An_Empty_Span_On_A_Right_To_Left_Line_Is_Mirrored_With_It()
    {
        var block = Block();
        block.Direction = CssConstants.Rtl;
        var span = Span(block);
        span.PaddingLeft = span.PaddingRight = "2px";
        Text(block, "abcdefgh xyz");
        Layout(block);

        AssertRectangle(196, 2, 4, 16, ScriptRectangle(span, block));
        Assert.Equal(132, Word(block, "abcdefgh").Left - block.Location.X, 1);
        Assert.Equal(104, Word(block, "xyz").Left - block.Location.X, 1);
    }

    /// <summary>
    /// "abc xyz ", then an empty span with <c>padding: 0 2px</c>, right to left: the space before
    /// the span is removed at the end of the line, and the span ends the line on its left, against
    /// "xyz", 4px wide from 144px in; "abc" stays 176px in and "xyz" 148px in. The span was 0 × 0 at
    /// the top of the line, 58px in, where the flow had put it, left to right.
    /// </summary>
    [Fact]
    public void An_Empty_Span_Ending_A_Right_To_Left_Line_Stands_Left_Of_Its_Last_Word()
    {
        var block = Block();
        block.Direction = CssConstants.Rtl;
        Text(block, "abc xyz ");
        var span = Span(block);
        span.PaddingLeft = span.PaddingRight = "2px";
        Layout(block);

        Assert.Equal(176, Word(block, "abc").Left - block.Location.X, 1);
        Assert.Equal(148, Word(block, "xyz").Left - block.Location.X, 1);
        AssertRectangle(144, 2, 4, 16, ScriptRectangle(span, block));
    }

    /// <summary>
    /// "a", a span holding only an empty span in a 40px font with <c>padding: 0 2px</c>, then "b":
    /// the inner span is as tall as its 40px font on the line's baseline, from 17.2px above the
    /// line, and the outer span takes it in along the line but is as tall as its own 16px font,
    /// 4 × 16 from 8px in and 2px down (CSS 2.1 §10.6.1). The outer span had no place at all, 0 × 0
    /// at the top-left of the page, and the inner one was 0 × 0 at the top of the line, 10px in.
    /// </summary>
    [Fact]
    public void An_Inline_Box_Around_An_Empty_Span_In_A_Larger_Font_Is_As_Tall_As_Its_Own_Font()
    {
        var block = Block();
        Text(block, "a");
        var outer = Span(block);
        var inner = Span(outer, "40px");
        inner.PaddingLeft = inner.PaddingRight = "2px";
        Text(block, "b");
        Layout(block);

        AssertRectangle(8, -17.2, 4, 40, ScriptRectangle(inner, block));
        AssertRectangle(8, 2, 4, 16, ScriptRectangle(outer, block));
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: an empty span alone in its block. Its line holds
    /// nothing, and is not a line at all (CSS 2.1 §9.4.2): the block is 0px tall, and the span is
    /// 0 × 0 at its top-left corner, as browsers have it.
    /// </summary>
    [Fact]
    public void Control_An_Empty_Span_Alone_Makes_No_Line()
    {
        var block = Block();
        var span = Span(block);
        Layout(block);

        Assert.Equal(0, block.Size.Height, 1);
        Assert.Empty(span.Rectangles);
        AssertRectangle(0, 0, 0, 0, ScriptRectangle(span, block));
    }

    /// <summary>
    /// Control, which passes before and after: an empty relatively positioned span alone on its
    /// line, holding a box positioned absolutely at <c>top: 0; left: 0</c> with "z" in it, then a
    /// block holding "a". The line holds nothing in the flow and takes no room, so the block below
    /// starts at the top, and the positioned box stands at the span's place there, not at the top
    /// of the page (WPT css/css-inline/empty-span-scroll).
    /// </summary>
    [Fact]
    public void Control_A_Box_Positioned_In_An_Empty_Span_Alone_On_Its_Line()
    {
        var block = Block();
        block.PaddingTop = "7px";
        var span = Span(block);
        span.Position = CssConstants.Relative;
        var positioned = AbsoluteBox(span);
        Text(positioned, "z");
        var below = new CssBox(block, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        below.InheritStyle();
        Text(below, "a");
        Layout(block);

        Assert.Equal(7, below.Location.Y - block.Location.Y, 1);
        Assert.Equal(7, positioned.Location.Y - block.Location.Y, 1);
        Assert.Equal(27, block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "abc", then an absolutely positioned span holding an
    /// empty span with <c>padding: 0 10px</c>, right-aligned. The empty span is laid out on the line
    /// only for the positioned span's static position, and is out of the flow with it: "abc" ends
    /// the line, 176px in.
    /// </summary>
    [Fact]
    public void Control_An_Empty_Span_In_A_Positioned_Box_Takes_No_Room_On_The_Line()
    {
        var block = Block();
        block.TextAlign = CssConstants.Right;
        Text(block, "abc");
        var positioned = Span(block);
        positioned.Position = CssConstants.Absolute;
        var span = Span(positioned);
        span.PaddingLeft = span.PaddingRight = "10px";
        Layout(block);

        Assert.Equal(176, Word(block, "abc").Left - block.Location.X, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "a", an 8 × 40px inline-block, a span aligned
    /// <c>top</c> holding "x" and an empty span, then "b". The span and its text stand at the top
    /// of the 40px line, and the span is 16px tall from 2px down, around its text. The empty span in
    /// it is aligned to the top of the line box with it (CSS 2.1 §10.8.1), not on the line's
    /// baseline, where empty boxes are placed: it is left where the flow put it, 0 × 0 at the top
    /// of the line, 24px in, and not given a rectangle on the baseline, 25px below its text.
    /// </summary>
    [Fact]
    public void Control_A_Span_Aligned_Top_Holding_An_Empty_Span_Stays_Around_Its_Text()
    {
        var block = Block();
        Text(block, "a");
        new CssBox(block, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = "8px",
            Height = "40px",
        };
        var top = Span(block);
        top.VerticalAlign = CssConstants.Top;
        Text(top, "x");
        var empty = Span(top);
        Text(block, "b");
        Layout(block);

        Assert.Equal(2, Word(block, "x").Top - block.Location.Y, 1);
        AssertRectangle(16, 2, 8, 16, ScriptRectangle(top, block));
        AssertRectangle(24, 0, 0, 0, ScriptRectangle(empty, block));
    }

    /// <summary>
    /// Control, which passes before and after: "abc" and a space, then an empty span with no
    /// padding, ending a line aligned right, and centred. The space is removed at the end of the
    /// line, and the span takes no room: "abc" ends the right-aligned line, 176px in, whether the
    /// space is in its text or a text of its own, and stands 88px in on the centred one.
    /// </summary>
    [Fact]
    public void Control_A_Space_Before_An_Empty_Span_Ending_An_Aligned_Line_Takes_No_Room()
    {
        var right = Block();
        right.TextAlign = CssConstants.Right;
        Text(right, "abc ");
        Span(right);
        Layout(right);

        var separate = Block();
        separate.TextAlign = CssConstants.Right;
        Text(separate, "abc");
        Text(separate, " ");
        Span(separate);
        Layout(separate);

        var centred = Block();
        centred.TextAlign = CssConstants.Center;
        Text(centred, "abc ");
        Span(centred);
        Layout(centred);

        Assert.Equal(176, Word(right, "abc").Left - right.Location.X, 1);
        Assert.Equal(176, Word(separate, "abc").Left - separate.Location.X, 1);
        Assert.Equal(88, Word(centred, "abc").Left - centred.Location.X, 1);
    }

    /// <summary>
    /// Control, which passes before and after: on justified lines in 200px, "aa " then a span
    /// holding an empty <c>&lt;i&gt;</c> and "bb", and "aaa bbb " then a link holding an empty span
    /// and "ccc", each before a word too long for the line. The span and the link stay around their
    /// words where justification moves them: the span 16px wide from 184px in, where "bb" ends the
    /// line, and the link starting at "ccc".
    /// </summary>
    [Fact]
    public void Control_Inline_Boxes_On_A_Justified_Line_Stay_Around_Their_Words()
    {
        var block = Block();
        block.TextAlign = CssConstants.Justify;
        Text(block, "aa ");
        var span = Span(block);
        Span(span, tag: "i");
        Text(span, "bb");
        Text(block, " cccccccccccccccccccccccccccc");
        Layout(block);

        var other = Block();
        other.TextAlign = CssConstants.Justify;
        Text(other, "aaa bbb ");
        var link = Span(other, tag: "a");
        Span(link);
        Text(link, "ccc");
        Text(other, " ddd eee fff ggg hhh iii jjj kkk lll mmm nnn");
        Layout(other);

        AssertRectangle(184, 2, 16, 16, ScriptRectangle(span, block));
        Assert.Equal(Word(other, "ccc").Left - other.Location.X, ScriptRectangle(link, other).X, 1);
        Assert.Equal(24, ScriptRectangle(link, other).Width, 1);
    }

    /// <summary>
    /// Control, which passes before and after: right to left, a span holding an empty
    /// <c>&lt;i&gt;</c> and "abcdefgh" before " xyz"; a link holding an empty span and "abc" before
    /// " def ghi"; and a span holding white space, a <c>&lt;b&gt;</c> with "abc", and white space
    /// before " def ghi". Each stays around its own word where mirroring the line puts it.
    /// </summary>
    [Fact]
    public void Control_Inline_Boxes_On_A_Right_To_Left_Line_Stay_Around_Their_Words()
    {
        var first = Block();
        first.Direction = CssConstants.Rtl;
        var span = Span(first);
        Span(span, tag: "i");
        Text(span, "abcdefgh");
        Text(first, " xyz");
        Layout(first);

        var second = Block();
        second.Direction = CssConstants.Rtl;
        var link = Span(second, tag: "a");
        Span(link);
        Text(link, "abc");
        Text(second, " def ghi");
        Layout(second);

        var third = Block();
        third.Direction = CssConstants.Rtl;
        var spaced = Span(third);
        Text(spaced, " ");
        var bold = Span(spaced, tag: "b");
        Text(bold, "abc");
        Text(spaced, " ");
        Text(third, " def ghi");
        Layout(third);

        AssertRectangle(Word(first, "abcdefgh").Left - first.Location.X, 2, 64, 16, ScriptRectangle(span, first));
        AssertRectangle(Word(second, "abc").Left - second.Location.X, 2, 24, 16, ScriptRectangle(link, second));
        AssertRectangle(Word(third, "abc").Left - third.Location.X, 2, 24, 16, ScriptRectangle(spaced, third));
    }

    /// <summary>
    /// Control, which passes before and after: a span holding "b", an empty span, and "c", once with
    /// the empty span in a 40px font and once with it aligned <c>super</c>. The span around it is
    /// as tall as its own 16px font, 2px down its line (CSS 2.1 §10.6.1): what it holds does not
    /// make its content area taller.
    /// </summary>
    [Fact]
    public void Control_An_Empty_Span_Does_Not_Make_The_Inline_Box_Around_It_Taller()
    {
        var large = Block();
        Text(large, "a");
        var aroundLarge = Span(large);
        Text(aroundLarge, "b");
        Span(aroundLarge, "40px");
        Text(aroundLarge, "c");
        Text(large, "d");
        Layout(large);

        var raised = Block();
        Text(raised, "a");
        var aroundRaised = Span(raised);
        Text(aroundRaised, "b");
        Span(aroundRaised).VerticalAlign = CssConstants.Super;
        Text(aroundRaised, "c");
        Text(raised, "d");
        Layout(raised);

        AssertRectangle(8, 2, 16, 16, ScriptRectangle(aroundLarge, large));
        AssertRectangle(8, 2, 16, 16, ScriptRectangle(aroundRaised, raised));
    }

    /// <summary>
    /// Control, which passes before and after: "x ", then a link holding a space and a
    /// <c>&lt;b&gt;</c> with "abc", then " d". The space the link starts with follows another and
    /// collapses (CSS Text 3 §4.1.1); it is text, not a box of its own, and the link starts where
    /// its word does.
    /// </summary>
    [Fact]
    public void Control_A_Link_Starting_With_White_Space_Starts_At_Its_Word()
    {
        var block = Block();
        Text(block, "x ");
        var link = Span(block, tag: "a");
        Text(link, " ");
        var bold = Span(link, tag: "b");
        Text(bold, "abc");
        Text(block, " d");
        Layout(block);

        AssertRectangle(Word(block, "abc").Left - block.Location.X, 2, 24, 16, ScriptRectangle(link, block));
    }

    /// <summary>
    /// Control, which passes before and after: right to left, an empty named anchor, then " abc
    /// def"; the anchor, a newline, then "abc def"; and, justified, an empty span, then " aaa bbb ccc"
    /// and a word too long for the line. The white space after the empty box starts the line and
    /// takes no room (CSS Text 3 §4.1.3): "abc" ends the lines at their right edge, 176px in, and
    /// the justified line fills the block, "aaa" from 176px in and "ccc" at its left edge.
    /// </summary>
    [Fact]
    public void Control_A_Space_After_An_Empty_Box_Starting_A_Right_To_Left_Line_Takes_No_Room()
    {
        var anchor = Block();
        anchor.Direction = CssConstants.Rtl;
        Span(anchor, tag: "a");
        Text(anchor, " abc def");
        Layout(anchor);

        var newline = Block();
        newline.Direction = CssConstants.Rtl;
        Span(newline, tag: "a");
        Text(newline, "\n");
        Text(newline, "abc def");
        Layout(newline);

        var justified = Block();
        justified.Direction = CssConstants.Rtl;
        justified.TextAlign = CssConstants.Justify;
        Span(justified);
        Text(justified, " aaa bbb ccc dddddddddddddddddddddddd");
        Layout(justified);

        Assert.Equal(176, Word(anchor, "abc").Left - anchor.Location.X, 1);
        Assert.Equal(148, Word(anchor, "def").Left - anchor.Location.X, 1);
        Assert.Equal(176, Word(newline, "abc").Left - newline.Location.X, 1);
        Assert.Equal(176, Word(justified, "aaa").Left - justified.Location.X, 1);
        Assert.Equal(0, Word(justified, "ccc").Left - justified.Location.X, 1);
    }

    /// <summary>
    /// Control, which passes before and after: in 100px blocks, "aaaa bbbb ", a link starting with an
    /// empty box and holding "link", then " x", where "link" wraps: right-aligned, with an empty span
    /// with <c>padding-left: 16px</c> (an icon) in the link, and with an empty <c>&lt;b&gt;</c>. The
    /// link goes to the second line whole: the first line ends with "bbbb", right-aligned from 32px
    /// in, and the link is "link" alone on the second line, 32 × 16 at its start.
    /// </summary>
    [Fact]
    public void Control_A_Line_Before_A_Wrapped_Link_Starting_With_An_Empty_Box_Keeps_Its_Place()
    {
        var right = Block("100px");
        right.TextAlign = CssConstants.Right;
        Text(right, "aaaa bbbb ");
        var iconLink = Span(right, tag: "a");
        Span(iconLink).PaddingLeft = "16px";
        Text(iconLink, "link");
        Text(right, " x");
        Layout(right);

        var plain = Block("100px");
        Text(plain, "aaaa bbbb ");
        var link = Span(plain, tag: "a");
        Span(link, tag: "b");
        Text(link, "link");
        Text(plain, " x");
        Layout(plain);

        Assert.Equal(32, Word(right, "aaaa").Left - right.Location.X, 1);
        Assert.Equal(68, Word(right, "bbbb").Left - right.Location.X, 1);
        AssertRectangle(0, 22, 32, 16, ScriptRectangle(link, plain));
    }

    /// <summary>
    /// Control, which passes before and after: "aaaa ", then a link holding an empty span and
    /// "bbbb", in a 34px block. "aaaa" and the space after it fill the first line, where the span
    /// stays, taking no room; the link is "bbbb" alone on the second line, 32 × 16 at its start, as
    /// browsers find it (they leave the empty piece of it on the first line out).
    /// </summary>
    [Fact]
    public void Control_A_Link_Starting_With_An_Empty_Span_Left_On_A_Full_Line_Is_Its_Text_Alone()
    {
        var block = Block("34px");
        Text(block, "aaaa ");
        var link = Span(block, tag: "a");
        Span(link);
        Text(link, "bbbb");
        Layout(block);

        AssertRectangle(0, 22, 32, 16, ScriptRectangle(link, block));
    }

    /// <summary>
    /// Control, which passes before and after: "aaaa bbbb ", a link holding an empty span and a
    /// <c>&lt;b&gt;</c> with <c>padding-left: 4px</c> holding "link", then " x", in a 100px block:
    /// "link" wraps with the <c>&lt;b&gt;</c>, which starts the second line with its padding, 36px
    /// wide from its start, and "link" 4px in.
    /// </summary>
    [Fact]
    public void Control_An_Inline_Box_Starting_A_Wrapped_Line_Has_Its_Padding_There_Once()
    {
        var block = Block("100px");
        Text(block, "aaaa bbbb ");
        var link = Span(block, tag: "a");
        Span(link);
        var bold = Span(link, tag: "b");
        bold.PaddingLeft = "4px";
        Text(bold, "link");
        Text(block, " x");
        Layout(block);

        AssertRectangle(0, 22, 36, 16, ScriptRectangle(bold, block));
        Assert.Equal(4, Word(block, "link").Left - block.Location.X, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a justified line in 200px, "aa ", then a link holding
    /// "bb ", an empty span, an absolutely positioned span holding "tip" 400px along, and "cc"; then
    /// " dd" and a word too long for the line. The link stays around its words, from where "bb"
    /// starts to where "cc" ends, wherever justification puts them.
    /// </summary>
    [Fact]
    public void Control_A_Link_Around_An_Empty_Span_And_A_Positioned_Box_Stays_Around_Its_Words()
    {
        var block = Block();
        block.TextAlign = CssConstants.Justify;
        Text(block, "aa ");
        var link = Span(block, tag: "a");
        Text(link, "bb ");
        Span(link);
        Positioned(link, "tip");
        Text(link, "cc");
        Text(block, " dd eeeeeeeeeeeeeeeeeeeeeeeee");
        Layout(block);

        var r = ScriptRectangle(link, block);
        Assert.Equal(Word(block, "bb").Left - block.Location.X, r.Left, 1);
        Assert.Equal(Word(block, "cc").Right - block.Location.X, r.Right, 1);
    }

    /// <summary>
    /// Control, which passes before and after: right to left, in a 40px block, "abcd ", an empty span
    /// with <c>padding-left: 16px</c>, then "efgh", which wraps. "abcd" stands against the first
    /// line's right edge, 8px in, and the span overflows the line at its left.
    /// </summary>
    [Fact]
    public void Control_An_Empty_Span_Overflowing_A_Right_To_Left_Line_Leaves_Its_Word_At_The_Edge()
    {
        var block = Block("40px");
        block.Direction = CssConstants.Rtl;
        Text(block, "abcd ");
        Span(block).PaddingLeft = "16px";
        Text(block, "efgh");
        Layout(block);

        Assert.Equal(8, Word(block, "abcd").Left - block.Location.X, 1);
    }

    /// <summary>
    /// Control, which passes before and after: right to left, "abc ", then a link with
    /// <c>padding-right: 10px</c> holding "def" and an empty span. The link is as wide as its word
    /// and its padding, 34px.
    /// </summary>
    [Fact]
    public void Control_A_Right_To_Left_Link_Closing_After_An_Empty_Span_Is_As_Wide_As_Its_Word_And_Padding()
    {
        var block = Block();
        block.Direction = CssConstants.Rtl;
        Text(block, "abc ");
        var link = Span(block, tag: "a");
        link.PaddingRight = "10px";
        Text(link, "def");
        Span(link);
        Layout(block);

        Assert.Equal(34, ScriptRectangle(link, block).Width, 1);
    }

    /// <summary>
    /// "abc ", then a link with <c>padding-right: 10px</c> holding "def" and an empty span with
    /// <c>padding: 0 8px</c>, right-aligned: the link closes after the span, and its padding ends the
    /// line, so "def" starts 150px in, the span stands against it, 16 × 16 from 174px in, and the
    /// link reaches from "def" to the end of the line, 50px wide. "def" stood 166px in, and the
    /// span was 0 × 0 at the top of the line, 60px in; then, the link taken to close after "def",
    /// the line was aligned as though the span ended it, "def" 160px in, and the link was drawn to
    /// 210px, past the end of the line.
    /// </summary>
    [Fact]
    public void An_Inline_Box_Closing_After_A_Word_And_An_Empty_Span_Ends_A_Right_Aligned_Line()
    {
        var block = Block();
        block.TextAlign = CssConstants.Right;
        Text(block, "abc ");
        var link = Span(block, tag: "a");
        link.PaddingRight = "10px";
        Text(link, "def");
        var span = Span(link);
        span.PaddingLeft = span.PaddingRight = "8px";
        Layout(block);

        Assert.Equal(150, Word(block, "def").Left - block.Location.X, 1);
        AssertRectangle(174, 2, 16, 16, ScriptRectangle(span, block));
        AssertRectangle(150, 2, 50, 16, ScriptRectangle(link, block));
    }

    /// <summary>
    /// "aa bb cc ", a link with <c>padding-right: 10px</c> holding "def" and an empty span with
    /// <c>padding: 0 8px</c>, then a word too long for the line, justified in 200px: the span and the
    /// link's padding after it end the line, so "def" ends 26px before it, 174px in, the span stands
    /// from there, and the link ends where the line does. "def" ended the line, the span was 0 × 0
    /// at the top of the line, 92px in, and the link's padding was drawn past the line's end, to
    /// 210px; then, the link taken to close after "def", the span ended the line, from 184px in,
    /// and the link's padding still reached 210px.
    /// </summary>
    [Fact]
    public void An_Inline_Box_Closing_After_A_Word_And_An_Empty_Span_Ends_A_Justified_Line()
    {
        var block = Block();
        block.TextAlign = CssConstants.Justify;
        Text(block, "aa bb cc ");
        var link = Span(block, tag: "a");
        link.PaddingRight = "10px";
        Text(link, "def");
        var span = Span(link);
        span.PaddingLeft = span.PaddingRight = "8px";
        Text(block, " eeeeeeeeeeeeeeeeeeeeeeeee");
        Layout(block);

        Assert.Equal(174, Word(block, "def").Right - block.Location.X, 1);
        AssertRectangle(174, 2, 16, 16, ScriptRectangle(span, block));
        Assert.Equal(200, ScriptRectangle(link, block).Right, 1);
    }

    /// <summary>
    /// "aa ", an empty span with <c>padding: 0 3px</c>, then " bb cc dd ee ff gg hh ii jj", justified
    /// in 200px: the space after the span follows the one before it and collapses (CSS Text 3
    /// §4.1.1), so the span stands against "bb", 6px wide from 21.56px in, "bb" 27.56px in, and the
    /// gap before the span is as wide as the gaps between the words, 5.56px. The span was 0 × 0 at
    /// the top of the line, 23px in, with "bb" 22.22px in; then, kept a space from "bb", the span
    /// took that space for room of its own, and "bb" stood 31.11px in, 4px after the span.
    /// </summary>
    [Fact]
    public void An_Empty_Span_Between_Spaces_On_A_Justified_Line_Stands_Against_The_Word_After_It()
    {
        var block = Block();
        block.TextAlign = CssConstants.Justify;
        Text(block, "aa ");
        var span = Span(block);
        span.PaddingLeft = span.PaddingRight = "3px";
        Text(block, " bb cc dd ee ff gg hh ii jj");
        Layout(block);

        var rectangle = ScriptRectangle(span, block);
        AssertRectangle(21.56, 2, 6, 16, rectangle);
        Assert.Equal(27.56, Word(block, "bb").Left - block.Location.X, 1);
        Assert.Equal(
            Word(block, "cc").Left - Word(block, "bb").Right,
            rectangle.Left - (Word(block, "aa").Right - block.Location.X), 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aa ", an empty named anchor, then
    /// " bb cc dd ee ff gg hh ii jj kk", justified in 200px: the space after the anchor collapses
    /// into the one before it, and the gap before "bb" is as wide as the one after it.
    /// </summary>
    [Fact]
    public void Control_An_Empty_Anchor_Between_Spaces_Leaves_A_Justified_Lines_Gaps_Even()
    {
        var block = Block();
        block.TextAlign = CssConstants.Justify;
        Text(block, "aa ");
        Span(block, tag: "a");
        Text(block, " bb cc dd ee ff gg hh ii jj kk");
        Layout(block);

        Assert.Equal(
            Word(block, "cc").Left - Word(block, "bb").Right,
            Word(block, "bb").Left - Word(block, "aa").Right, 1);
    }

    /// <summary>
    /// A 300px flex row: an item growing to take the room left, then an item holding an empty span,
    /// a space and "abc". The space starts the item's line, and is removed there (CSS Text 3 §4.1.3),
    /// so the item is as wide as "abc", 24px, and ends the row, "abc" with it, 276px in. It was
    /// measured with the space, 28px wide, from 272px in.
    /// </summary>
    [Fact]
    public void A_Flex_Item_Starting_With_An_Empty_Span_And_A_Space_Is_As_Wide_As_Its_Text()
    {
        var row = Row();
        var item = Item(row);
        Span(item);
        Text(item, " ");
        Text(Span(item, tag: "b"), "abc");
        Layout(row);

        Assert.Equal(24, item.Size.Width, 1);
        Assert.Equal(276, item.Location.X - row.Location.X, 1);
        Assert.Equal(276, Word(row, "abc").Left - row.Location.X, 1);
    }

    /// <summary>
    /// A 300px flex row: an item growing to take the room left, then an item holding an absolutely
    /// positioned 10px inline-block, a space and an inline-block with "abc" (the language button of
    /// Wikipedia's article pages). The space starts the item's line, the positioned box being out of
    /// the flow, so the item is as wide as the inline-block, 24px, and puts it at the end of the row,
    /// 276px in. The item was 28px wide.
    /// </summary>
    [Fact]
    public void A_Flex_Item_Starting_With_A_Positioned_Box_And_A_Space_Is_As_Wide_As_Its_Label()
    {
        var row = Row();
        var item = Item(row);
        item.Position = CssConstants.Relative;
        var checkbox = Span(item, tag: "i");
        checkbox.Display = "inline-block";
        checkbox.Position = CssConstants.Absolute;
        checkbox.Width = checkbox.Height = "10px";
        Text(item, " ");
        var label = Span(item, tag: "label");
        label.Display = "inline-block";
        Text(label, "abc");
        Layout(row);

        Assert.Equal(24, item.Size.Width, 1);
        Assert.Equal(276, label.Location.X - row.Location.X, 1);
    }

    /// <summary>
    /// An inline-block holding an absolutely positioned span with "tip", then " abc", right-aligned
    /// in 200px: the space starts the inline-block's line and is removed there, so the inline-block
    /// is as wide as "abc", 24px, and "abc" ends the line, 176px in. The inline-block was 28px wide.
    /// </summary>
    [Fact]
    public void An_Inline_Block_Starting_With_A_Positioned_Span_And_A_Space_Is_As_Wide_As_Its_Text()
    {
        var block = Block();
        block.TextAlign = CssConstants.Right;
        var inlineBlock = Span(block);
        inlineBlock.Display = "inline-block";
        Positioned(inlineBlock, "tip");
        Text(inlineBlock, " abc");
        Layout(block);

        Assert.Equal(24, inlineBlock.Size.Width, 1);
        Assert.Equal(176, Word(block, "abc").Left - block.Location.X, 1);
    }

    /// <summary>
    /// A 300px flex row: an item holding an empty span with <c>padding-left: 16px</c>, a space and
    /// "abc", then an item with "next". The space starts the first item's line and is removed there,
    /// so "abc" follows the span, 16px in, the item is 40px wide, and "next" follows it, 40px in.
    /// "abc" stood 20px in, and the item was 44px wide.
    /// </summary>
    [Fact]
    public void A_Flex_Item_Starting_With_A_Padded_Empty_Span_And_A_Space_Ends_At_Its_Text()
    {
        var row = Block("300px");
        row.Display = "flex";
        var item = Item(row);
        Span(item).PaddingLeft = "16px";
        Text(item, " ");
        Text(Span(item, tag: "b"), "abc");
        Text(Item(row), "next");
        Layout(row);

        Assert.Equal(16, Word(row, "abc").Left - row.Location.X, 1);
        Assert.Equal(40, item.Size.Width, 1);
        Assert.Equal(40, Word(row, "next").Left - row.Location.X, 1);
    }

    /// <summary>
    /// An inline-block holding "xy", a line break, an empty span, a space and "abc", right-aligned
    /// in 200px: the space starts the inline-block's second line and is removed there, so the
    /// inline-block is as wide as that line, "abc", 24px, and "abc" ends the line, 176px in. The
    /// inline-block was measured with the space, 28px wide.
    /// </summary>
    [Fact]
    public void An_Inline_Block_With_A_Line_Starting_With_An_Empty_Span_And_A_Space_Is_As_Wide_As_Its_Text()
    {
        var block = Block();
        block.TextAlign = CssConstants.Right;
        var inlineBlock = Span(block);
        inlineBlock.Display = "inline-block";
        Text(inlineBlock, "xy");
        _ = new CssBox(inlineBlock, new HtmlTag("br", false, null), BaseUrl) { Display = "block" };
        Span(inlineBlock);
        Text(inlineBlock, " ");
        Text(Span(inlineBlock, tag: "b"), "abc");
        Layout(block);

        Assert.Equal(24, inlineBlock.Size.Width, 1);
        Assert.Equal(176, Word(block, "abc").Left - block.Location.X, 1);
    }

    /// <summary>
    /// Control, which passes before and after: an inline-block holding " abc def ", right-aligned
    /// in 200px: the spaces at the start and the end of its line are removed, so it is as wide as
    /// "abc def", 52px, and holds it on one line, "abc" 148px in and "def" 176px in.
    /// </summary>
    [Fact]
    public void Control_An_Inline_Block_Holding_Words_Between_Spaces_Is_As_Wide_As_The_Words()
    {
        var block = Block();
        block.TextAlign = CssConstants.Right;
        var inlineBlock = Span(block);
        inlineBlock.Display = "inline-block";
        Text(inlineBlock, " abc def ");
        Layout(block);

        Assert.Equal(52, inlineBlock.Size.Width, 1);
        Assert.Equal(148, Word(block, "abc").Left - block.Location.X, 1);
        Assert.Equal(176, Word(block, "def").Left - block.Location.X, 1);
        Assert.Equal(Word(block, "abc").Top, Word(block, "def").Top, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a 300px flex row: an item growing to take the room
    /// left, then an item holding a 10px inline-block, a space and "abc". The space follows the
    /// inline-block on the item's line and takes room there, so the item is 38px wide, and "abc"
    /// ends the row, 276px in.
    /// </summary>
    [Fact]
    public void Control_A_Space_After_An_Inline_Block_Starting_A_Flex_Item_Takes_Room()
    {
        var row = Row();
        var item = Item(row);
        var inlineBlock = Span(item);
        inlineBlock.Display = "inline-block";
        inlineBlock.Width = "10px";
        Text(item, " ");
        Text(Span(item, tag: "b"), "abc");
        Layout(row);

        Assert.Equal(38, item.Size.Width, 1);
        Assert.Equal(276, Word(row, "abc").Left - row.Location.X, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a 300px flex row: an item growing to take the room
    /// left, then an item holding "abc" in a <c>&lt;b&gt;</c>, a space and "def" in another. The space
    /// is between the words on the item's line and takes room there, so the item is 52px wide, and
    /// "def" ends the row, 276px in.
    /// </summary>
    [Fact]
    public void Control_A_Space_Between_Words_Of_Two_Boxes_In_A_Flex_Item_Takes_Room()
    {
        var row = Row();
        var item = Item(row);
        Text(Span(item, tag: "b"), "abc");
        Text(item, " ");
        Text(Span(item, tag: "b"), "def");
        Layout(row);

        Assert.Equal(52, item.Size.Width, 1);
        Assert.Equal(276, Word(row, "def").Left - row.Location.X, 1);
    }

    /// <summary>
    /// "x ", a link holding an empty span with <c>margin-left: 5px</c> and <c>padding-left: 20px</c>,
    /// and "lll", then " y": the link holds the span's margin, so it starts where the span's margin
    /// does, 12px in, and is 49px wide, to the end of "lll"; the span is 20px wide from 17px in. The
    /// link started at "lll", 37px in, 24px wide, and the span was 0 × 0 at the top of the line;
    /// then, around the span, the link started at the span's border, 17px in.
    /// </summary>
    [Fact]
    public void An_Inline_Box_Starting_With_An_Empty_Span_Holds_Its_Margin()
    {
        var block = Block();
        Text(block, "x ");
        var link = Span(block, tag: "a");
        var span = Span(link);
        span.MarginLeft = "5px";
        span.PaddingLeft = "20px";
        Text(link, "lll");
        Text(block, " y");
        Layout(block);

        AssertRectangle(17, 2, 20, 16, ScriptRectangle(span, block));
        AssertRectangle(12, 2, 49, 16, ScriptRectangle(link, block));
    }

    /// <summary>
    /// "aaaa bbbb ", a link holding an empty span with <c>margin-left: 5px</c> and
    /// <c>padding-left: 20px</c>, and "lll", then " y", in a 100px block: the link wraps whole, and
    /// starts the second line with the span's margin, 49px wide from its start, and "lll" 25px in.
    /// The span stayed at the end of the first line, and the link was "lll" alone, 24px wide; then,
    /// wrapped with the span, the link started at the span's border, 5px in.
    /// </summary>
    [Fact]
    public void A_Link_Starting_With_A_Margined_Empty_Span_Wraps_Whole_With_The_Margin()
    {
        var block = Block("100px");
        Text(block, "aaaa bbbb ");
        var link = Span(block, tag: "a");
        var span = Span(link);
        span.MarginLeft = "5px";
        span.PaddingLeft = "20px";
        Text(link, "lll");
        Text(block, " y");
        Layout(block);

        AssertRectangle(5, 22, 20, 16, ScriptRectangle(span, block));
        AssertRectangle(0, 22, 49, 16, ScriptRectangle(link, block));
        Assert.Equal(25, Word(block, "lll").Left - block.Location.X, 1);
    }

    /// <summary>
    /// "aaaa ", then a link with <c>padding-left: 5px</c> holding a <c>&lt;b&gt;</c> with an empty
    /// span and "bbbb", in a 34px block: "bbbb" wraps, and the span stays on the first line, which
    /// "aaaa" and the space after it fill. The link starts there, its padding after "aaaa", 5px wide
    /// from 32px in, and has a rectangle on each line, as browsers give it. It had none on the
    /// first line, and its padding was not painted.
    /// </summary>
    [Fact]
    public void An_Inline_Box_Starting_On_A_Full_Line_Keeps_Its_Padding_There()
    {
        var block = Block("34px");
        Text(block, "aaaa ");
        var link = Span(block, tag: "a");
        link.PaddingLeft = "5px";
        var bold = Span(link, tag: "b");
        Span(bold);
        Text(bold, "bbbb");
        Layout(block);

        Assert.Equal(2, link.Rectangles.Count);
        AssertRectangle(32, 2, 5, 16, Relative(link.Rectangles.Values.OrderBy(r => r.Top).First(), block));
    }

    /// <summary>A 200px block (or as wide as given) with 20px lines of a 16px font, in a block in the root.</summary>
    private static CssBox Block(string width = "200px")
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        return new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Width = width,
            FontSize = "16px",
            LineHeight = "20px",
        };
    }

    private static void Layout(CssBox block)
    {
        var root = block.ParentBox!.ParentBox!;
        root.PerformLayout(root.LayoutEnvironment);
    }

    /// <summary>
    /// A 300px flex row with 20px lines of a 16px font, in a block in the root, holding an item with
    /// "T" that grows to take the room the items after it leave.
    /// </summary>
    private static CssBox Row()
    {
        var row = Block("300px");
        row.Display = "flex";
        var title = Item(row);
        title.FlexGrow = "1";
        Text(title, "T");
        return row;
    }

    /// <summary>A block in <paramref name="parent"/>, inheriting its style, as a flex item is.</summary>
    private static CssBox Item(CssBox parent)
    {
        var item = new CssBox(parent, new HtmlTag("div", false, null), BaseUrl);
        item.InheritStyle();
        item.Display = "block";
        return item;
    }

    /// <summary>An element's inline box in <paramref name="parent"/>, inheriting its style, in the font size given.</summary>
    private static CssBox Span(CssBox parent, string? fontSize = null, string tag = "span")
    {
        var span = new CssBox(parent, new HtmlTag(tag, false, null), BaseUrl);
        span.InheritStyle();
        span.Display = CssConstants.Inline;

        if (fontSize != null)
            span.FontSize = fontSize;

        return span;
    }

    /// <summary>
    /// An absolutely positioned span in <paramref name="parent"/>, 400px from the left and at the top,
    /// holding the text.
    /// </summary>
    private static CssBox Positioned(CssBox parent, string text)
    {
        var positioned = Span(parent);
        positioned.Position = CssConstants.Absolute;
        positioned.Left = "400px";
        positioned.Top = "0";
        Text(positioned, text);
        return positioned;
    }

    /// <summary>A 5 × 5px box in <paramref name="parent"/>, positioned absolutely at its top-left corner.</summary>
    private static CssBox AbsoluteBox(CssBox parent)
    {
        var box = new CssBox(parent, new HtmlTag("i", false, null), BaseUrl);
        box.InheritStyle();
        box.Display = "block";
        box.Position = CssConstants.Absolute;
        box.Top = "0";
        box.Left = "0";
        box.Width = "5px";
        box.Height = "5px";
        return box;
    }

    /// <summary>
    /// An anonymous inline box in <paramref name="parent"/> holding the text, inheriting the
    /// parent's style as the box a text node makes does.
    /// </summary>
    private static void Text(CssBox parent, string text)
    {
        var box = new CssBox(parent, null, BaseUrl);
        box.InheritStyle();
        box.Display = CssConstants.Inline;
        box.Text = text.AsMemory();
        box.ParseToWords();
    }

    private static CssRect Word(CssBox block, string text) =>
        Descendants(block).SelectMany(b => b.Words).Single(w => w.Text == text);

    private static IEnumerable<CssBox> Descendants(CssBox box)
    {
        foreach (var child in box.Boxes)
        {
            yield return child;

            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static IEnumerable<Fragment> Fragments(Fragment fragment)
    {
        yield return fragment;

        foreach (var child in fragment.Children)
        {
            foreach (var descendant in Fragments(child))
                yield return descendant;
        }
    }

    /// <summary>
    /// The border box script reads for the box (<c>getBoundingClientRect</c>), from the block's
    /// top-left corner, as <c>HtmlContainerInt.CollectLayoutGeometry</c> in Broiler.HTML takes it:
    /// the box's own bounds, unless they are empty and it has line rectangles, and then the union of
    /// those that are not empty.
    /// </summary>
    private static RectangleF ScriptRectangle(CssBox box, CssBox block)
    {
        var bounds = box.Bounds;

        if (bounds.Width == 0 && bounds.Height == 0 && box.Rectangles.Count > 0)
        {
            float left = float.MaxValue, top = float.MaxValue, right = float.MinValue, bottom = float.MinValue;

            foreach (var rectangle in box.Rectangles.Values)
            {
                if (rectangle.Width == 0 && rectangle.Height == 0)
                    continue;

                left = Math.Min(left, rectangle.Left);
                top = Math.Min(top, rectangle.Top);
                right = Math.Max(right, rectangle.Right);
                bottom = Math.Max(bottom, rectangle.Bottom);
            }

            bounds = right < left || bottom < top ? RectangleF.Empty : RectangleF.FromLTRB(left, top, right, bottom);
        }

        return Relative(bounds, block);
    }

    private static RectangleF Relative(RectangleF rectangle, CssBox block) =>
        new(rectangle.X - block.Location.X, rectangle.Y - block.Location.Y, rectangle.Width, rectangle.Height);

    private static void AssertRectangle(double x, double y, double width, double height, RectangleF actual)
    {
        Assert.Equal(x, actual.X, 1);
        Assert.Equal(y, actual.Y, 1);
        Assert.Equal(width, actual.Width, 1);
        Assert.Equal(height, actual.Height, 1);
    }

    private sealed class FakeLayoutEnvironment : ILayoutEnvironment
    {
        private static readonly ConcurrentDictionary<double, FakeFont> Fonts = new();
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => Fonts.GetOrAdd(size, s => new FakeFont(s));
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => new(8 * text.Length, (float)font.Height);
        public void MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = text.Length; charFitWidth = 8 * text.Length; }
        public double GetWhitespaceWidth(Broiler.Graphics.Text.ILayoutFont font) => 4;
        public ImageIntrinsics GetImageIntrinsics(object imageHandle) => new(300, 150, true);
        public Broiler.Graphics.Color.BColor ParseColor(string value) => default;
        public void RequestRefresh(bool relayout) { }
        public SizeF ViewportSize => new(1024, 768);
        public PointF RootLocation => PointF.Empty;
        public SizeF ActualSize { get; set; }
        public bool AvoidGeometryAntialias => false;
        public SizeF PageSize => new(1024, 768);
        public int MarginTop => 0;
        public void ReportLayoutError(string message, Exception? exception = null) { }
        public bool AvoidAsyncImagesLoading => true;
        public bool AvoidImagesLateLoading => true;
        public ILayoutImageLoader CreateImageLoader(Action<object?, RectangleF, bool> onComplete) => new ImageLoader(onComplete);
        public string FormatListMarker(int number, string style) => string.Empty;
    }

    private sealed class ImageLoader(Action<object?, RectangleF, bool> onComplete) : ILayoutImageLoader
    {
        private static readonly object TheImage = new();

        public object? Image { get; private set; }
        public RectangleF Rectangle => RectangleF.Empty;

        public void LoadImage(string src, IReadOnlyDictionary<string, string>? attributes, Uri baseUrl)
        {
            Image = TheImage;
            onComplete(TheImage, RectangleF.Empty, false);
        }

        public void Dispose() { }
    }

    /// <summary>A font of the given size in points, as tall in pixels as its size in pixels.</summary>
    private sealed class FakeFont(double size) : Broiler.Graphics.Text.ILayoutFont
    {
        public double Size => size;
        public double Height => size * 4 / 3;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}

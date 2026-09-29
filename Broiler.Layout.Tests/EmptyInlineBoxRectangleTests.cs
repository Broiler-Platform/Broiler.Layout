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
    /// too long for the line, justified in 200px: the words of the first line move apart, "bb" to
    /// 50px in and "cc" to 100px in. The first span, after the space, stays against "bb", 4px wide
    /// from 46px in, and the second, with no space before it, stays against "cc", from 116px in.
    /// They were 0 × 0 at the top of the line, 22px and 62px in, where the flow had put them before
    /// the words moved.
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

        Assert.Equal(50, Word(block, "bb").Left - block.Location.X, 1);
        Assert.Equal(100, Word(block, "cc").Left - block.Location.X, 1);
        AssertRectangle(46, 2, 4, 16, ScriptRectangle(first, block));
        AssertRectangle(116, 2, 4, 16, ScriptRectangle(second, block));
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

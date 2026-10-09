using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A line does not break at the edge of an inline element inside a word: it breaks only where the
/// text lets it, and a word too wide for the room left on a line goes to the next line whole.
/// </summary>
/// <remarks>
/// <para>
/// CSS Text 3 §5.1, §5.2: soft wrap opportunities come from the text, its spaces and the characters
/// line breaking rules allow a break after, not from the elements around it; for web compatibility
/// there is one before and after an atomic inline. The flow broke the line before any word that did
/// not fit once the line held something, whatever came before the word: in a 30px block,
/// "aaa&lt;b&gt;bbb&lt;/b&gt;" went on two lines, where browsers keep "aaabbb" on one and let it
/// overflow. And it only ever measured the word itself, so "x aaa&lt;b&gt;bbb&lt;/b&gt;" in a 50px
/// block kept "aaa" on the first line beside "x" and put "bbb" on the second, where browsers break
/// at the space and put "aaabbb" on the second line together.
/// </para>
/// <para>
/// Each block here has 20px lines of a 16px font, whose glyphs stand 2px down their line. Words are
/// 8px wide a letter, and a space is 4px wide.
/// </para>
/// </remarks>
public sealed class BreakAtElementEdgeTests
{
    private static readonly Uri BaseUrl = new("file:///break-at-element-edge.html");

    [Fact]
    public void OverflowWrapBreakWordWrapsAndRestoresOnResize()
    {
        var block = Block(32);
        block.OverflowWrap = "break-word";
        Text(block, "abcdefgh");
        Layout(block);
        Assert.Equal(20, Word(block, "efgh").Top - Word(block, "abcd").Top, 1);
        block.Width = "80px";
        Layout(block);
        Assert.Equal(64, Word(block, "abcdefgh").Width, 1);
        Assert.Equal(20, block.Size.Height, 1);
    }

    [Theory]
    [InlineData("normal", "normal")]
    [InlineData("break-word", "nowrap")]
    [InlineData("break-word", "pre")]
    public void OverflowWrapPreservesUnbreakableTextWhenDisabled(string wrap, string whiteSpace)
    {
        var block = Block(32, whiteSpace);
        block.OverflowWrap = wrap;
        Text(block, "abcdefgh");
        Layout(block);
        Assert.Equal(64, Word(block, "abcdefgh").Width, 1);
    }

    [Fact]
    public void OverflowWrapKeepsCombiningSequencesTogether()
    {
        var block = Block(16);
        block.OverflowWrap = "break-word";
        Text(block, "e\u0301o\u0308");
        Layout(block);
        Assert.Equal(20, Word(block, "o\u0308").Top - Word(block, "e\u0301").Top, 1);
    }

    /// <summary>
    /// "aaa", then a span holding "bbb", in a 30px block: "aaabbb" is one word, so "bbb" follows
    /// "aaa" on the first line, 24px along and 2px down, and the block is a line tall. "bbb" went
    /// to the second line, 0px along and 22px down, and the block was 40px tall.
    /// </summary>
    [Fact]
    public void Text_Joined_To_An_Inline_Element_Stays_On_Its_Line()
    {
        var block = Block(30);
        Text(block, "aaa");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(24, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "bbb").Top - block.Location.Y, 1);
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// "aa", a span holding "bb" and a span in it holding "cc", then "dd", in a 30px block: all one
    /// word on one line, "dd" 48px along and 2px down. Each part went to a line of its own: "dd"
    /// was 0px along and 62px down, and the block 80px tall.
    /// </summary>
    [Fact]
    public void Text_Joined_Across_Nested_Inline_Elements_Stays_On_Its_Line()
    {
        var block = Block(30);
        Text(block, "aa");
        var span = Span(block);
        Text(span, "bb");
        Text(Span(span), "cc");
        Text(block, "dd");
        Layout(block);

        Assert.Equal(48, Word(block, "dd").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "dd").Top - block.Location.Y, 1);
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// "a", then "B" in a span in a 32px font, then " cc", in a 12px block: "aB" is one word, so "B"
    /// stays beside "a", 8px along, and "cc" wraps below the 24.8px line the 32px glyph makes, 26.8px
    /// down; the block is 44.8px tall. "B" went to a line of its own, 0px along, "cc" to a third
    /// line 46.8px down, and the block was 64.8px tall.
    /// </summary>
    [Fact]
    public void Text_In_A_Larger_Font_Joined_To_Text_Stays_On_Its_Line()
    {
        var block = Block(12);
        Text(block, "a");
        Text(Span(block, "32px"), "B");
        Text(block, " cc");
        Layout(block);

        Assert.Equal(8, Word(block, "B").Left - block.Location.X, 1);
        Assert.Equal(26.8, Word(block, "cc").Top - block.Location.Y, 1);
        Assert.Equal(44.8, block.Size.Height, 1);
    }

    /// <summary>
    /// "aaa", an empty span, then a span holding "bbb", in a 30px block: the empty span is no break,
    /// and "bbb" stays on the first line, 24px along. It went to the second line, 0px along.
    /// </summary>
    [Fact]
    public void An_Empty_Inline_Element_Inside_A_Word_Is_No_Break()
    {
        var block = Block(30);
        Text(block, "aaa");
        Span(block);
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(24, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// "aaa", then a span with <c>white-space: nowrap</c> holding "bbb", in a 30px block: nothing
    /// separates them, so "bbb" stays on the first line, 24px along. The span went to the second
    /// line whole, 0px along, and the block was 40px tall.
    /// </summary>
    [Fact]
    public void A_Span_That_Does_Not_Wrap_Joined_To_Text_Stays_On_Its_Line()
    {
        var block = Block(30);
        Text(block, "aaa");
        Text(Span(block, whiteSpace: CssConstants.NoWrap), "bbb");
        Layout(block);

        Assert.Equal(24, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// "aaa", then a span holding "bbb", in a 30px block with <c>white-space: pre-wrap</c>: "bbb"
    /// stays on the first line, 24px along. It went to the second line, 0px along.
    /// </summary>
    [Fact]
    public void Text_Joined_To_An_Inline_Element_Stays_On_Its_Line_In_Pre_Wrap()
    {
        var block = Block(30, CssConstants.PreWrap);
        Text(block, "aaa");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(24, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// "x aaa", then a span holding "bbb", in a 50px block: "aaabbb" does not fit beside "x", so the
    /// line breaks at the space, and "aaa" starts the second line, 0px along and 22px down, with
    /// "bbb" 24px along it. "aaa" stayed beside "x", 12px along and 2px down, and "bbb" went to the
    /// second line alone, 0px along.
    /// </summary>
    [Fact]
    public void A_Word_Across_An_Element_Edge_Moves_To_The_Next_Line_Whole()
    {
        var block = Block(50);
        Text(block, "x aaa");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(24, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
        Assert.Equal(40, block.Size.Height, 1);
    }

    /// <summary>
    /// Ten empty spans, then "x aaa" and a span holding "bbb", in a 50px block: the same as above,
    /// with the boxes looked up among many, "aaa" 0px along and 22px down, "bbb" 24px along and 22px
    /// down. "aaa" stayed beside "x", 12px along and 2px down, and "bbb" went to the second line
    /// alone, 0px along.
    /// </summary>
    [Fact]
    public void A_Word_Across_An_Element_Edge_After_Many_Elements_Moves_To_The_Next_Line_Whole()
    {
        var block = Block(50);
        for (int i = 0; i < 10; i++)
            Span(block);

        Text(block, "x aaa");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(24, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// "x ", then a span holding "aaa", then "bbb", in a 50px block: the word starts in the span,
    /// after the space, and goes to the second line whole: "aaa" 0px along and 22px down, "bbb" 24px
    /// along. "aaa" stayed beside "x", 12px along and 2px down, and "bbb" went to the second line
    /// alone, 0px along.
    /// </summary>
    [Fact]
    public void A_Word_Starting_In_An_Element_Moves_To_The_Next_Line_Whole()
    {
        var block = Block(50);
        Text(block, "x ");
        Text(Span(block), "aaa");
        Text(block, "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(24, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(40, block.Size.Height, 1);
    }

    /// <summary>
    /// "x aaa", then a span with 14px of right padding holding "b", in a 50px block: the padding
    /// ends the word too, which with it is 46px wide and does not fit beside "x", so "aaa" starts
    /// the second line, 0px along and 22px down. The padding was not measured, and "aaa" and "b"
    /// stayed beside "x", "aaa" 12px along and 2px down, running past the block's edge.
    /// </summary>
    [Fact]
    public void The_Padding_Of_An_Element_Ending_A_Word_Is_Part_Of_It()
    {
        var block = Block(50);
        Text(block, "x aaa");
        var span = Span(block);
        span.PaddingRight = "14px";
        Text(span, "b");
        Layout(block);

        Assert.Equal(0, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(24, Word(block, "b").Left - block.Location.X, 1);
    }

    /// <summary>
    /// "x aaa", then a span with <c>white-space: nowrap</c> holding "bbb", in a 50px block: the word
    /// runs into the span and goes to the second line whole, "aaa" 0px along and 22px down, "bbb"
    /// 24px along. "aaa" stayed beside "x", 12px along and 2px down.
    /// </summary>
    [Fact]
    public void A_Word_Running_Into_A_Span_That_Does_Not_Wrap_Moves_Whole()
    {
        var block = Block(50);
        Text(block, "x aaa");
        Text(Span(block, whiteSpace: CssConstants.NoWrap), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(24, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// "x ", a span with <c>white-space: nowrap</c> holding "aaa", then a span holding "bbb", in a
    /// 50px block: the word the span starts goes to the second line whole, "aaa" 0px along and 22px
    /// down, "bbb" 24px along. "aaa" stayed beside "x", 12px along and 2px down.
    /// </summary>
    [Fact]
    public void A_Span_That_Does_Not_Wrap_Starting_A_Word_Moves_With_It()
    {
        var block = Block(50);
        Text(block, "x ");
        Text(Span(block, whiteSpace: CssConstants.NoWrap), "aaa");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(24, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// A 60px × 30px left float, then "aaa" and a span holding "bbb", in a 100px block: the 48px word
    /// does not fit in the 40px beside the float, so its line goes below the float, "aaa" 0px along
    /// and 32px down, "bbb" 24px along. "aaa" stood beside the float, 60px along and 2px down, and
    /// "bbb" on the line below it, 60px along and 22px down.
    /// </summary>
    [Fact]
    public void A_Word_Across_An_Element_Edge_Goes_Below_A_Float_It_Does_Not_Fit_Beside()
    {
        var block = Block(100);
        _ = new CssBox(block, new HtmlTag("i", false, null), BaseUrl)
        {
            Display = "block",
            Float = "left",
            Width = "60px",
            Height = "30px",
        };
        Text(block, "aaa");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(32, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(24, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(32, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// A 60px × 40px left float, then "x aaa" and a span holding "bbb", in a 100px block: "aaabbb"
    /// does not fit beside "x", and the 48px word does not fit in the 40px beside the float on the
    /// next line either, so it goes below the float, "aaa" 0px along and 42px down, "bbb" 24px along.
    /// "aaa" stayed beside "x", 72px along and 2px down, and "bbb" went to the second line, beside the
    /// float, 60px along and 22px down.
    /// </summary>
    [Fact]
    public void A_Word_Moved_To_The_Next_Line_Goes_Below_A_Float_It_Does_Not_Fit_Beside()
    {
        var block = Block(100);
        _ = new CssBox(block, new HtmlTag("i", false, null), BaseUrl)
        {
            Display = "block",
            Float = "left",
            Width = "60px",
            Height = "40px",
        };
        Text(block, "x aaa");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(42, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(24, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(42, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// The same with "x aaa", a span holding "b" and a span holding "bbbbb": the 72px word goes
    /// below the float whole, "aaa" 0px along and 42px down, "bbbbb" 32px along. It is measured to
    /// its end, not only as far as the 28px left beside "x": "aaa" and "b" alone fit in the 40px
    /// beside the float. "aaa" stayed beside "x", 72px along and 2px down, and "bbbbb" went to the
    /// third line, 0px along and 42px down.
    /// </summary>
    [Fact]
    public void A_Word_Across_Three_Elements_Goes_Below_A_Float_It_Does_Not_Fit_Beside()
    {
        var block = Block(100);
        _ = new CssBox(block, new HtmlTag("i", false, null), BaseUrl)
        {
            Display = "block",
            Float = "left",
            Width = "60px",
            Height = "40px",
        };
        Text(block, "x aaa");
        Text(Span(block), "b");
        Text(Span(block), "bbbbb");
        Layout(block);

        Assert.Equal(0, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(42, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(32, Word(block, "bbbbb").Left - block.Location.X, 1);
        Assert.Equal(42, Word(block, "bbbbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// "aaa", an absolutely positioned span, then a span holding "bbb", in a 30px block: the
    /// positioned span is out of the flow and no break, and "bbb" stays on the first line, 24px
    /// along and 2px down, in a block a line tall. It went to the second line, 0px along and 22px
    /// down, and the block was 40px tall.
    /// </summary>
    [Fact]
    public void An_Absolutely_Positioned_Box_Inside_A_Word_Is_No_Break()
    {
        var block = Block(30);
        Text(block, "aaa");
        Span(block).Position = CssConstants.Absolute;
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(24, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "bbb").Top - block.Location.Y, 1);
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// A float holding "aaa", a span holding "bbb", then " cc", in a block with no width: the float
    /// is as wide as its widest word, "aaabbb", 48px, and holds it on its first line, "cc" 22px down
    /// on its second. It was as wide as "aaa", 24px, "bbb" went to a second line, 0px along, and
    /// "cc" to a third, 42px down.
    /// </summary>
    [Fact]
    public void The_Min_Content_Width_Holds_A_Word_Across_An_Element_Edge()
    {
        var block = Block(0);
        var floated = Float(block);
        Text(floated, "aaa");
        Text(Span(floated), "bbb");
        Text(floated, " cc");
        Layout(block);

        Assert.Equal(48, floated.Size.Width, 1);
        Assert.Equal(24, Word(block, "bbb").Left - floated.Location.X, 1);
        Assert.Equal(22, Word(block, "cc").Top - floated.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa ", then a span holding "bbb", in a 30px block:
    /// the line breaks at the space, and "bbb" starts the second line, 0px along and 22px down.
    /// </summary>
    [Fact]
    public void Control_A_Space_Before_An_Element_Is_A_Break()
    {
        var block = Block(30);
        Text(block, "aaa ");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa", then a span holding " bbb", in a 30px block:
    /// the line breaks at the space in the span, and "bbb" starts the second line, 0px along.
    /// </summary>
    [Fact]
    public void Control_A_Space_At_The_Start_Of_An_Element_Is_A_Break()
    {
        var block = Block(30);
        Text(block, "aaa");
        Text(Span(block), " bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a span holding "aaa", a space, then a span holding
    /// "bbb", in a 30px block: the space, a text box of its own, is a break, and "bbb" starts the
    /// second line, 0px along and 22px down.
    /// </summary>
    [Fact]
    public void Control_A_Space_Between_Two_Elements_Is_A_Break()
    {
        var block = Block(30);
        Text(Span(block), "aaa");
        Text(block, " ");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa-", then a span holding "bbb", in a 40px block:
    /// the line breaks after the hyphen, and "bbb" starts the second line, 0px along and 22px down.
    /// </summary>
    [Fact]
    public void Control_A_Hyphen_Before_An_Element_Is_A_Break()
    {
        var block = Block(40);
        Text(block, "aaa-");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: two ideographs, then a span holding a third, in a
    /// 20px block: a line may break between ideographs, and the third starts the second line, 0px
    /// along and 22px down.
    /// </summary>
    [Fact]
    public void Control_Ideographs_Break_At_An_Element_Edge()
    {
        var block = Block(20);
        Text(block, "漢字");
        Text(Span(block), "語");
        Layout(block);

        Assert.Equal(0, Word(block, "語").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "語").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a span with <c>word-break: break-all</c> holding
    /// "aaa", then a span holding "bbb", in a 30px block: the line may break after any letter of
    /// the first span, and "bbb" starts the second line, 0px along and 22px down.
    /// </summary>
    [Fact]
    public void Control_Text_That_Breaks_Anywhere_Before_An_Element_Is_A_Break()
    {
        var block = Block(30);
        var span = Span(block);
        span.WordBreak = CssConstants.BreakAll;
        Text(span, "aaa");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa", a 5px right float, then a span holding "bbb",
    /// in a 40px block: the line breaks at the float, and "bbb" starts the second line, 0px along
    /// and 22px down. A float is no break in itself, but the white space beside one is dropped when
    /// the tree is built, so the line may break there as it may at the space.
    /// </summary>
    [Fact]
    public void Control_A_Float_Between_Two_Words_Is_A_Break()
    {
        var block = Block(40);
        Text(block, "aaa");
        _ = new CssBox(block, new HtmlTag("i", false, null), BaseUrl)
        {
            Display = "block",
            Float = "right",
            Width = "5px",
            Height = "5px",
        };
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa", a span that is not displayed, then a span
    /// holding "bbb", in a 30px block: the line breaks at the hidden span, as at the white space
    /// dropped beside it, and "bbb" starts the second line, 0px along and 22px down.
    /// </summary>
    [Fact]
    public void Control_A_Box_That_Is_Not_Displayed_Between_Two_Words_Is_A_Break()
    {
        var block = Block(30);
        Text(block, "aaa");
        var hidden = Span(block);
        hidden.Display = CssConstants.None;
        Text(hidden, "x");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa ", then a span holding "bbb", in a 30px block
    /// with <c>white-space: pre-wrap</c>: the line breaks after the preserved space, a word of its
    /// own, and "bbb" starts the second line, 0px along and 22px down.
    /// </summary>
    [Fact]
    public void Control_A_Preserved_Space_Before_An_Element_Is_A_Break()
    {
        var block = Block(30, CssConstants.PreWrap);
        Text(block, "aaa ");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa", a 20px inline <c>&lt;svg&gt;</c>, then a span
    /// holding "bbb", in a 50px block: a line may break after a replaced element, which holds no
    /// words, and "bbb" starts the second line, 0px along and 22px down.
    /// </summary>
    [Fact]
    public void Control_Text_After_An_Inline_Svg_Wraps()
    {
        var block = Block(50);
        Text(block, "aaa");
        _ = new CssBox(block, new HtmlTag("svg", false, null), BaseUrl)
        {
            Display = CssConstants.Inline,
            Width = "20px",
            Height = "10px",
        };
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa", a &lt;wbr&gt;, then a span holding "bbb", in a
    /// 30px block: the &lt;wbr&gt; is a break, and "bbb" starts the second line, 0px along.
    /// </summary>
    [Fact]
    public void Control_A_Wbr_Is_A_Break()
    {
        var block = Block(30);
        Text(block, "aaa");
        new CssBox(block, new HtmlTag("wbr", true, null), BaseUrl).Display = CssConstants.Inline;
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa", then a <c>&lt;math&gt;</c> element holding
    /// "b", in a 30px block: browsers lay the element out whole, as an atomic inline, so a line may
    /// break before it, and "b" starts the second line, 0px along and 22px down. Taken for an inline
    /// box, the element would keep "b" on the first line, 24px along.
    /// </summary>
    [Fact]
    public void Control_A_Math_Element_After_Text_Wraps()
    {
        var block = Block(30);
        Text(block, "aaa");
        var math = new CssBox(block, new HtmlTag("math", false, null), BaseUrl);
        math.InheritStyle();
        math.Display = CssConstants.Inline;
        Text(math, "b");
        Layout(block);

        Assert.Equal(0, Word(block, "b").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "b").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa", then a 10px inline-block, in a 30px block: a
    /// line may break before an atomic inline, and the box starts the second line, 0px along.
    /// </summary>
    [Fact]
    public void Control_An_Inline_Block_After_Text_Wraps()
    {
        var block = Block(30);
        Text(block, "aaa");
        var box = new CssBox(block, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = "10px",
            Height = "10px",
        };
        Layout(block);

        Assert.Equal(0, box.Location.X - block.Location.X, 1);
        Assert.Equal(40, block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a 24px image, then a span holding "bbb", in a 30px
    /// block: a line may break after an atomic inline, and "bbb" starts the second line, 0px along.
    /// </summary>
    [Fact]
    public void Control_Text_After_An_Image_Wraps()
    {
        var block = Block(30);
        var image = new CssBoxImage(block, new HtmlTag("img", true, new System.Collections.Generic.Dictionary<string, string> { ["src"] = "i.png" }), BaseUrl)
        {
            Display = CssConstants.Inline,
            Width = "24px",
            Height = "10px",
        };
        image.InheritStyle();
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.True(Word(block, "bbb").Top - block.Location.Y > 20);
    }

    /// <summary>
    /// Control, which passes before and after: "x aaa", then a span holding "b", in a 50px block:
    /// "aaab" fits beside "x", "b" 36px along and 2px down, and the block is a line tall.
    /// </summary>
    [Fact]
    public void Control_A_Word_Across_An_Element_Edge_That_Fits_Stays()
    {
        var block = Block(50);
        Text(block, "x aaa");
        Text(Span(block), "b");
        Layout(block);

        Assert.Equal(36, Word(block, "b").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "b").Top - block.Location.Y, 1);
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// "aaa" and a no-break space, then a span holding "bbb", in a 50px block: a no-break space is
    /// no break, and "bbb" stays on the first line, 32px along and 2px down. It went to the second
    /// line, 0px along and 22px down.
    /// </summary>
    [Fact]
    public void A_No_Break_Space_Before_An_Element_Is_No_Break()
    {
        var block = Block(50);
        Text(block, "aaa\u00A0");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(32, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// "aaa/", then a span holding "bbb", in a 40px block: a line does not break after a slash, and
    /// "bbb" stays on the first line, 32px along and 2px down. It went to the second line, 0px along
    /// and 22px down.
    /// </summary>
    [Fact]
    public void A_Slash_Before_An_Element_Is_No_Break()
    {
        var block = Block(40);
        Text(block, "aaa/");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(32, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "あああ", then a span holding "いい", in a 30px block:
    /// a line may break between kana, and "いい" starts the second line, 0px along and 22px down. Were
    /// only ideographs taken for breaks, "いい" would stay on the first line, 24px along and 2px down.
    /// </summary>
    [Fact]
    public void Control_Kana_Before_An_Element_Is_A_Break()
    {
        var block = Block(30);
        Text(block, "あああ");
        Text(Span(block), "いい");
        Layout(block);

        Assert.Equal(0, Word(block, "いい").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "いい").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa", then a span holding "いい", in a 30px block: a
    /// line may break before kana, and "いい" starts the second line, 0px along and 22px down. Were
    /// only ideographs taken for breaks, it would stay on the first line, 24px along and 2px down.
    /// </summary>
    [Fact]
    public void Control_Kana_At_The_Start_Of_An_Element_Is_A_Break()
    {
        var block = Block(30);
        Text(block, "aaa");
        Text(Span(block), "いい");
        Layout(block);

        Assert.Equal(0, Word(block, "いい").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "いい").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa。", then a span holding "bbb", in a 40px block: a
    /// line may break after an ideographic full stop, and "bbb" starts the second line, 0px along and
    /// 22px down. Were only ideographs taken for breaks, it would stay on the first line, 32px along.
    /// </summary>
    [Fact]
    public void Control_Ideographic_Punctuation_Before_An_Element_Is_A_Break()
    {
        var block = Block(40);
        Text(block, "aaa。");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "กขค", then a span holding "งจ", in a 30px block:
    /// browsers break Thai between its words, which the engine cannot find, so an element's edge in
    /// it is a break, and "งจ" starts the second line, 0px along and 22px down. Taken for no break,
    /// the edge would keep it on the first line, 24px along and 2px down.
    /// </summary>
    [Fact]
    public void Control_Thai_Before_An_Element_Is_A_Break()
    {
        var block = Block(30);
        Text(block, "กขค");
        Text(Span(block), "งจ");
        Layout(block);

        Assert.Equal(0, Word(block, "งจ").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "งจ").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa" and an em dash, then a span holding "bbb", in a
    /// 40px block: a line may break after a dash, and "bbb" starts the second line, 0px along and
    /// 22px down. Taken for no break, the dash would keep it on the first line, 32px along.
    /// </summary>
    [Fact]
    public void Control_A_Dash_Before_An_Element_Is_A_Break()
    {
        var block = Block(40);
        Text(block, "aaa\u2014");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa", then a span holding an em dash and "bbb", in a
    /// 40px block: a line may break before an em dash, and the span's word starts the second line,
    /// 0px along and 22px down. Taken for no break, the dash would keep it on the first line, 24px
    /// along.
    /// </summary>
    [Fact]
    public void Control_An_Em_Dash_At_The_Start_Of_An_Element_Is_A_Break()
    {
        var block = Block(40);
        Text(block, "aaa");
        Text(Span(block), "\u2014bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "\u2014bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "\u2014bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa" and a zero width space, then a span holding
    /// "bbb", in a 40px block: the zero width space is a break, and "bbb" starts the second line, 0px
    /// along and 22px down. Taken for no break, it would keep "bbb" on the first line, 32px along.
    /// </summary>
    [Fact]
    public void Control_A_Zero_Width_Space_Before_An_Element_Is_A_Break()
    {
        var block = Block(40);
        Text(block, "aaa\u200B");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa?", then a span holding "bbb", in a 40px block: a
    /// line may break after a question mark, and "bbb" starts the second line, 0px along and 22px
    /// down. Taken for no break, it would keep "bbb" on the first line, 32px along.
    /// </summary>
    [Fact]
    public void Control_A_Question_Mark_Before_An_Element_Is_A_Break()
    {
        var block = Block(40);
        Text(block, "aaa?");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "a" and a heart, then a span holding "bbb", in a 30px
    /// block: a line may break beside the pictographs UAX #14 puts with the ideographs, and "bbb"
    /// starts the second line, 0px along and 22px down. Taken for no break, the heart would keep it on
    /// the first line, 16px along.
    /// </summary>
    [Fact]
    public void Control_A_Pictograph_Before_An_Element_Is_A_Break()
    {
        var block = Block(30);
        Text(block, "a\u2764");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "a", a heart and the variation selector that asks for
    /// its emoji form, then a span holding "bbb", in a 30px block: the selector breaks as the heart
    /// does, and "bbb" starts the second line, 0px along and 22px down. Taken for a character of its
    /// own, the selector would keep "bbb" on the first line, 24px along.
    /// </summary>
    [Fact]
    public void Control_A_Pictograph_In_Its_Emoji_Form_Before_An_Element_Is_A_Break()
    {
        var block = Block(30);
        Text(block, "a\u2764\uFE0F");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// A float holding a span with 20px of padding on each side holding "a", then "b", in a block
    /// with no width: the float is as wide as the word "ab" with the span's padding once, 20 + 8 + 20
    /// + 8 = 56px. It was as wide as "a" in the span, 48px; counted twice, the right padding would
    /// make it 76px.
    /// </summary>
    [Fact]
    public void The_Min_Content_Width_Counts_The_Padding_Of_An_Element_In_A_Word_Once()
    {
        var block = Block(0);
        var floated = Float(block);
        var span = Span(floated);
        span.PaddingLeft = span.PaddingRight = "20px";
        Text(span, "a");
        Text(floated, "b");
        Layout(block);

        Assert.Equal(56, floated.Size.Width, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a float holding a span with 20px of right padding
    /// holding "aaa", in a block with no width: the float is 24 + 20 = 44px wide. Counted twice, the
    /// padding would make it 64px.
    /// </summary>
    [Fact]
    public void Control_The_Min_Content_Width_Counts_The_Right_Padding_Of_An_Element_Once()
    {
        var block = Block(0);
        var floated = Float(block);
        var span = Span(floated);
        span.PaddingRight = "20px";
        Text(span, "aaa");
        Layout(block);

        Assert.Equal(44, floated.Size.Width, 1);
    }

    /// <summary>
    /// A float holding a span with 10px of left padding holding "a bbb", then "cccccc", in a block
    /// with no width: the word "bbbcccccc" is not the span's first, so the padding is not before it on
    /// its line, and the float is as wide as it, 72px. It was as wide as "cccccc", 48px; measured with
    /// the padding, the word would make it 82px.
    /// </summary>
    [Fact]
    public void The_Min_Content_Width_Leaves_Out_The_Left_Padding_Before_Other_Words()
    {
        var block = Block(0);
        var floated = Float(block);
        var span = Span(floated);
        span.PaddingLeft = "10px";
        Text(span, "a bbb");
        Text(floated, "cccccc");
        Layout(block);

        Assert.Equal(72, floated.Size.Width, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a float holding a span with 30px of right padding,
    /// holding a span holding "aaa" and then "bbb ccc", in a block with no width: the word "aaabbb"
    /// ends inside the span, before its padding, and is 48px wide, so the float is as wide as "ccc"
    /// and the padding, 54px. Counted twice after "ccc", the padding would make it 84px.
    /// </summary>
    [Fact]
    public void Control_The_Min_Content_Width_Of_A_Word_Ending_Inside_An_Element()
    {
        var block = Block(0);
        var floated = Float(block);
        var span = Span(floated);
        span.PaddingRight = "30px";
        Text(Span(span), "aaa");
        Text(span, "bbb ccc");
        Layout(block);

        Assert.Equal(54, floated.Size.Width, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "zz", an absolutely positioned span 60px wide holding
    /// "x " and a span holding "aaa", then "bbbbbbbb", in a 200px block: the span lays its text out
    /// on lines of its own, so "aaa" fits beside "x" in it, 12px along, whatever follows the span.
    /// Measured on into "bbbbbbbb", "aaa" would go on the span's second line, 0px along and 20px
    /// down.
    /// </summary>
    [Fact]
    public void Control_A_Word_In_An_Absolutely_Positioned_Span_Ends_At_Its_Edge()
    {
        var block = Block(200);
        block.Position = CssConstants.Relative;
        Text(block, "zz");
        var positioned = Span(block);
        positioned.Position = CssConstants.Absolute;
        positioned.Width = "60px";
        Text(positioned, "x ");
        Text(Span(positioned), "aaa");
        Text(block, "bbbbbbbb");
        Layout(block);

        Assert.Equal(12, Word(block, "aaa").Left - Word(block, "x").Left, 1);
        Assert.Equal(0, Word(block, "aaa").Top - Word(block, "x").Top, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "zz", an absolutely positioned span with no width
    /// holding "x " and a span holding "aaa", then "bbbbbbbb", in a 200px block: the span is no wider
    /// than its own text, "x aaa", 36px; the engine makes it 24px, before and after, where browsers
    /// make it 36px. Measured on into "bbbbbbbb", "aaa" would make it 88px wide.
    /// </summary>
    [Fact]
    public void Control_An_Absolutely_Positioned_Span_Is_No_Wider_Than_Its_Own_Text()
    {
        var block = Block(200);
        block.Position = CssConstants.Relative;
        Text(block, "zz");
        var positioned = Span(block);
        positioned.Position = CssConstants.Absolute;
        Text(positioned, "x ");
        Text(Span(positioned), "aaa");
        Text(block, "bbbbbbbb");
        Layout(block);

        Assert.True(positioned.Size.Width <= 36);
    }

    /// <summary>
    /// Control, which passes before and after: "x ", a span holding "aaa", then a span with
    /// <c>white-space: pre</c> holding "b", a line break and "cccccc", in a 60px block: the line break
    /// ends the word, and "aaab" fits beside "x", "aaa" 12px along and 2px down. Measured on past the
    /// line break, the word would put "aaa" on the second line, 0px along and 22px down.
    /// </summary>
    [Fact]
    public void Control_A_Line_Break_In_A_Pre_Span_Ends_A_Word()
    {
        var block = Block(60);
        Text(block, "x ");
        Text(Span(block), "aaa");
        Text(Span(block, whiteSpace: CssConstants.Pre), "b\ncccccc");
        Layout(block);

        Assert.Equal(12, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "aaa").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// "x ", a span with <c>white-space: pre</c> holding "aaa", then a span holding "bbb", in a 50px
    /// block: the word the pre span starts goes to the second line whole, "aaa" 0px along and 22px
    /// down, "bbb" 24px along. "aaa" stayed beside "x", 12px along and 2px down, and "bbb" went to the
    /// second line alone, 0px along.
    /// </summary>
    [Fact]
    public void A_Word_Starting_In_A_Pre_Span_Moves_To_The_Next_Line_Whole()
    {
        var block = Block(50);
        Text(block, "x ");
        Text(Span(block, whiteSpace: CssConstants.Pre), "aaa");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(24, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// "x ", then a span with <c>white-space: pre</c> holding "aaaaaa", in a 50px block: the span does
    /// not fit beside "x" and goes to the second line whole, 0px along and 22px down. It stayed beside
    /// "x", 12px along and 2px down, past the block's edge.
    /// </summary>
    [Fact]
    public void A_Pre_Span_That_Does_Not_Fit_After_A_Space_Goes_To_The_Next_Line()
    {
        var block = Block(50);
        Text(block, "x ");
        Text(Span(block, whiteSpace: CssConstants.Pre), "aaaaaa");
        Layout(block);

        Assert.Equal(0, Word(block, "aaaaaa").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "aaaaaa").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "x ", then a span with <c>white-space: pre</c> holding
    /// "aa", a line break and "aaaaaaaaa", in a 50px block: the span's first line fits beside "x", and
    /// "aa" stays there, 12px along and 2px down.
    /// </summary>
    [Fact]
    public void Control_A_Pre_Span_Fits_Up_To_Its_First_Line_Break()
    {
        var block = Block(50);
        Text(block, "x ");
        Text(Span(block, whiteSpace: CssConstants.Pre), "aa\naaaaaaaaa");
        Layout(block);

        Assert.Equal(12, Word(block, "aa").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "aa").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a span with 4px of left padding holding a span with
    /// <c>white-space: pre</c> holding "aaaaaaaaa", in a 50px block: nothing precedes the pre span on
    /// the line, so it stays on the first line, 2px down, and overflows it.
    /// </summary>
    [Fact]
    public void Control_A_Pre_Span_Starting_A_Line_Stays_On_It()
    {
        var block = Block(50);
        var span = Span(block);
        span.PaddingLeft = "4px";
        Text(Span(span, whiteSpace: CssConstants.Pre), "aaaaaaaaa");
        Layout(block);

        Assert.Equal(4, Word(block, "aaaaaaaaa").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "aaaaaaaaa").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "x aaa", then a span holding " bbb", in a 44px block
    /// with <c>white-space: pre-wrap</c>, where a space is a word of its own, 8px wide here. The line
    /// may break after the space that starts the span, and that space hangs at the end of the line
    /// (CSS Text 3 §4.1.3), so only "x aaa" has to fit, and it does: "aaa" stays on the first line,
    /// 16px along and 2px down, "bbb" starts the second, 22px down, and the block is 40px tall.
    /// Counted in the word "aaa" ends, the space would move "aaa" to the second line, 0px along and
    /// 22px down, "bbb" to a third, and make the block 60px tall.
    /// </summary>
    [Fact]
    public void Control_A_Preserved_Space_Starting_An_Element_Hangs()
    {
        var block = Block(44, CssConstants.PreWrap);
        Text(block, "x aaa");
        Text(Span(block), " bbb");
        Layout(block);

        Assert.Equal(16, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
        Assert.Equal(40, block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: the same with a tab starting the span, "\tbbb": the
    /// tab hangs as the space does, and "aaa" stays on the first line, 16px along and 2px down.
    /// Counted in the word, the tab would move "aaa" to the second line, 0px along and 22px down.
    /// </summary>
    [Fact]
    public void Control_A_Preserved_Tab_Starting_An_Element_Hangs()
    {
        var block = Block(44, CssConstants.PreWrap);
        Text(block, "x aaa");
        Text(Span(block), "\tbbb");
        Layout(block);

        Assert.Equal(16, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "aaa").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a float with <c>white-space: pre-wrap</c> in a 0px
    /// block, holding a span holding "aaa", then a span holding " bbb": the line may break after the
    /// space, which hangs, so the widest thing that cannot be broken is "aaa", and the float is 24px
    /// wide. Counted with "aaa", the space would make it 32px wide.
    /// </summary>
    [Fact]
    public void Control_The_Min_Content_Width_Leaves_Out_A_Preserved_Space_Starting_An_Element()
    {
        var block = Block(0, CssConstants.PreWrap);
        var floated = Float(block);
        Text(Span(floated), "aaa");
        Text(Span(floated), " bbb");
        Layout(block);

        Assert.Equal(24, floated.Size.Width, 1);
    }

    /// <summary>
    /// "x ", a span holding "aaa", a span holding a no-break space, then a span holding "bbb", in a
    /// 44px block with <c>white-space: pre-wrap</c>: a no-break space is no break, and does not hang,
    /// so "aaa", the space and "bbb" are one word, too wide for the room after "x ", and go to the
    /// second line together, "aaa" 0px along and 22px down, "bbb" 32px along. "aaa" stayed on the
    /// first line, 16px along and 2px down, and "bbb" went to the second, 0px along.
    /// </summary>
    [Fact]
    public void A_No_Break_Space_Between_Elements_Is_No_Break_In_Pre_Wrap()
    {
        var block = Block(44, CssConstants.PreWrap);
        Text(block, "x ");
        Text(Span(block), "aaa");
        Text(Span(block), " ");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(32, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// "aaa", a span holding "bbb", then " ccc", in a 30px block with <c>text-align: justify</c>:
    /// "aaabbb" is one word, which starts the first line and overflows it, and justification only
    /// stretches a line (CSS Text 3 §7.1), so "bbb" stays right after "aaa", 24px along and 2px
    /// down. "bbb" went to the second line, 0px along; spread over the line it overflows, "bbb"
    /// would stand 6px along, over "aaa".
    /// </summary>
    [Fact]
    public void A_Justified_Line_A_Word_Overflows_Is_Not_Squeezed()
    {
        var block = Block(30);
        block.TextAlign = CssConstants.Justify;
        Text(block, "aaa");
        Text(Span(block), "bbb");
        Text(block, " ccc");
        Layout(block);

        Assert.Equal(24, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// "aaa", a span with 10px of left padding holding "bbb", then " ccc", in a 50px justified
    /// block: "aaabbb" with the padding in it, 58px, overflows the line, though the two words alone
    /// would fit it, and the line is left as the flow laid it out: "bbb" 34px along and 2px down.
    /// "bbb" went to the second line, 0px along and 22px down; spread over the line, "bbb" would end
    /// at its end, 26px along, over the padding.
    /// </summary>
    [Fact]
    public void A_Justified_Line_Padding_Makes_Overflow_Is_Not_Squeezed()
    {
        var block = Block(50);
        block.TextAlign = CssConstants.Justify;
        Text(block, "aaa");
        var span = Span(block);
        span.PaddingLeft = "10px";
        Text(span, "bbb");
        Text(block, " ccc");
        Layout(block);

        Assert.Equal(34, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aa bb ccc" in a 60px justified block: "aa bb" fits
    /// the first line and is stretched across it, "bb" 44px along, flush with its end.
    /// </summary>
    [Fact]
    public void Control_A_Justified_Line_That_Fits_Is_Stretched()
    {
        var block = Block(60);
        block.TextAlign = CssConstants.Justify;
        Text(block, "aa bb ccc");
        Layout(block);

        Assert.Equal(44, Word(block, "bb").Left - block.Location.X, 1);
    }

    /// <summary>
    /// A span with <c>white-space: nowrap</c> and 4px of left padding holding "aaa", then
    /// "bbbbbbb", in a 50px block: "aaabbbbbbb" is one word, which starts the line, so it stays on
    /// it and overflows: "aaa" 4px along and 2px down, "bbbbbbb" 28px along and 2px down, and the
    /// block is a line tall. "bbbbbbb" went to the second line, 0px along and 22px down, and the
    /// block was 40px tall. Measured from past the padding, where the line holds nothing, the word
    /// would move to the second line whole and leave the first empty: "aaa" 22px down.
    /// </summary>
    [Fact]
    public void A_Span_That_Does_Not_Wrap_Starting_A_Line_After_Its_Padding_Stays_On_It()
    {
        var block = Block(50);
        var span = Span(block, whiteSpace: CssConstants.NoWrap);
        span.PaddingLeft = "4px";
        Text(span, "aaa");
        Text(block, "bbbbbbb");
        Layout(block);

        Assert.Equal(4, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(28, Word(block, "bbbbbbb").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "bbbbbbb").Top - block.Location.Y, 1);
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// A span with <c>white-space: nowrap</c> holding "aaa", then "bbbbbbb", in a 50px block with a
    /// 4px <c>text-indent</c>: the word stays on the first line, "aaa" 4px along and 2px down,
    /// "bbbbbbb" 28px along and 2px down. "bbbbbbb" went to the second line, 0px along and 22px
    /// down. Measured from the indent, the word would move to the second line whole, "aaa" 0px along
    /// and 22px down.
    /// </summary>
    [Fact]
    public void A_Span_That_Does_Not_Wrap_Starting_An_Indented_Line_Stays_On_It()
    {
        var block = Block(50);
        block.TextIndent = "4px";
        Text(Span(block, whiteSpace: CssConstants.NoWrap), "aaa");
        Text(block, "bbbbbbb");
        Layout(block);

        Assert.Equal(4, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(28, Word(block, "bbbbbbb").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "bbbbbbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// A span with 4px of left padding holding a span with <c>white-space: nowrap</c> holding "aaa",
    /// then "bbbbbbb", in a 50px block: the word stays on the first line, "aaa" 4px along and 2px
    /// down, "bbbbbbb" 28px along and 2px down. "bbbbbbb" went to the second line, 0px along and
    /// 22px down. Measured from past the padding, the word would move to the second line whole,
    /// "aaa" 0px along and 22px down.
    /// </summary>
    [Fact]
    public void A_Span_That_Does_Not_Wrap_Starting_A_Line_In_A_Padded_Element_Stays_On_It()
    {
        var block = Block(50);
        var outer = Span(block);
        outer.PaddingLeft = "4px";
        Text(Span(outer, whiteSpace: CssConstants.NoWrap), "aaa");
        Text(block, "bbbbbbb");
        Layout(block);

        Assert.Equal(4, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(28, Word(block, "bbbbbbb").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "bbbbbbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a float in a 0px block holding a span with 30px of
    /// left padding, holding "aaa ", an i holding "bbb", then "ccc": the padding comes before "aaa",
    /// not before "bbbccc", so the widest things that cannot be broken are the padding with "aaa",
    /// 54px, and "bbbccc", 48px, and the float is 54px wide. Counted before "bbbccc" too, the
    /// padding would make it 78px wide.
    /// </summary>
    [Fact]
    public void Control_The_Min_Content_Width_Leaves_Out_The_Left_Padding_Of_An_Element_Before_Other_Content()
    {
        var block = Block(0);
        var floated = Float(block);
        var span = Span(floated);
        span.PaddingLeft = "30px";
        Text(span, "aaa ");
        Text(Span(span), "bbb");
        Text(span, "ccc");
        Layout(block);

        Assert.Equal(54, floated.Size.Width, 1);
    }

    /// <summary>
    /// A float in a 0px block holding a span with a 12px left border, holding "aa ", an i holding
    /// "bbb", then "cc": the widest thing that cannot be broken is "bbbcc", 40px, the border coming
    /// before "aa", and the float is 40px wide. It was 36px wide, "bbb" with the border, too narrow
    /// for "bbbcc"; counted before "bbbcc", the border would make it 52px wide.
    /// </summary>
    [Fact]
    public void The_Min_Content_Width_Leaves_Out_The_Left_Border_Of_An_Element_Before_Other_Content()
    {
        var block = Block(0);
        var floated = Float(block);
        var span = Span(floated);
        span.BorderLeftWidth = "12px";
        span.BorderLeftStyle = "solid";
        Text(span, "aa ");
        Text(Span(span), "bbb");
        Text(span, "cc");
        Layout(block);

        Assert.Equal(40, floated.Size.Width, 1);
    }

    /// <summary>
    /// A float in a 0px block holding a span with 30px of left padding, holding "x " and a span with
    /// 5px of left padding, which holds an i holding "aaa", an i holding "bbb", then "ccc": the inner
    /// padding is next to "aaabbbccc" and the outer one before "x", so the widest things that cannot
    /// be broken are the outer padding with "x", 38px, and the inner padding with "aaabbbccc", 77px,
    /// and the float is 77px wide. It was 59px wide, both paddings with "aaa"; counting both before
    /// "aaabbbccc" would make it 107px wide.
    /// </summary>
    [Fact]
    public void The_Min_Content_Width_Keeps_Only_The_Left_Padding_Next_To_A_Word()
    {
        var block = Block(0);
        var floated = Float(block);
        var outer = Span(floated);
        outer.PaddingLeft = "30px";
        Text(outer, "x ");
        var inner = Span(outer);
        inner.PaddingLeft = "5px";
        Text(Span(inner), "aaa");
        Text(Span(inner), "bbb");
        Text(inner, "ccc");
        Layout(block);

        Assert.Equal(77, floated.Size.Width, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "x aaa", then a span with 25px of left padding
    /// holding a 10px inline-block, in a 60px block: the line may break before the inline-block,
    /// and it breaks before the span that starts with it, whose padding goes to the next line with
    /// it, so only "x aaa" has to fit the first line, and "aaa" stays on it, 12px along and 2px
    /// down. Counted in the word before it, the padding would move "aaa" to the second line, 0px
    /// along and 22px down.
    /// </summary>
    [Fact]
    public void Control_The_Padding_Before_An_Inline_Block_Is_Not_Part_Of_The_Word_Before_It()
    {
        var block = Block(60);
        Text(block, "x aaa");
        var span = Span(block);
        span.PaddingLeft = "25px";
        _ = new CssBox(span, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = "10px",
            Height = "10px",
        };
        Layout(block);

        Assert.Equal(12, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "aaa").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "x aaa", then a span with a 25px left margin holding
    /// a 10px image, in a 60px block: as with the inline-block, "aaa" stays on the first line, 12px
    /// along and 2px down. Counted in the word before it, the margin would move "aaa" to the second
    /// line, 0px along and 22px down.
    /// </summary>
    [Fact]
    public void Control_The_Margin_Before_An_Image_Is_Not_Part_Of_The_Word_Before_It()
    {
        var block = Block(60);
        Text(block, "x aaa");
        var span = Span(block);
        span.MarginLeft = "25px";
        var image = new CssBoxImage(span, new HtmlTag("img", true, new System.Collections.Generic.Dictionary<string, string> { ["src"] = "i.png" }), BaseUrl)
        {
            Display = CssConstants.Inline,
            Width = "10px",
            Height = "10px",
        };
        image.InheritStyle();
        Layout(block);

        Assert.Equal(12, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "aaa").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a float in a 0px block holding a span holding "aaa",
    /// then a span with 25px of left padding holding a 10px inline-block: the line may break before
    /// the padded span, so the widest things that cannot be broken are "aaa", 24px, and the padding
    /// with the inline-block, 35px, and the float is 35px wide. Counted with "aaa", the padding
    /// would make it 49px wide.
    /// </summary>
    [Fact]
    public void Control_The_Min_Content_Width_Leaves_Out_The_Padding_Before_An_Inline_Block()
    {
        var block = Block(0);
        var floated = Float(block);
        Text(Span(floated), "aaa");
        var span = Span(floated);
        span.PaddingLeft = "25px";
        _ = new CssBox(span, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = "10px",
            Height = "10px",
        };
        Layout(block);

        Assert.Equal(35, floated.Size.Width, 1);
    }

    /// <summary>
    /// "x aaa", then a span with 25px of left padding holding a &lt;wbr&gt; and "bbb", in a 60px
    /// block: the line may break at the &lt;wbr&gt;, inside the span, so the padding is part of the
    /// word "aaa" ends, 49px wide, which does not fit after "x " and goes to the second line, "aaa"
    /// 0px along and 22px down, as in browsers. "aaa" stayed on the first line, 12px along and 2px
    /// down.
    /// </summary>
    [Fact]
    public void The_Padding_Before_A_Wbr_Is_Part_Of_The_Word_Before_It()
    {
        var block = Block(60);
        Text(block, "x aaa");
        var span = Span(block);
        span.PaddingLeft = "25px";
        new CssBox(span, new HtmlTag("wbr", true, null), BaseUrl).Display = CssConstants.Inline;
        Text(span, "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "aaa").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa." then a span holding "[1]", as a citation
    /// follows a sentence, in a 40px block: the line may break between a full stop and an opening
    /// bracket, and "[1]" starts the second line, 0px along and 22px down, as in browsers. Taken
    /// for one word with "aaa.", it would stay on the first line, 32px along and 2px down.
    /// </summary>
    [Fact]
    public void Control_A_Full_Stop_Before_A_Bracket_In_An_Element_Is_A_Break()
    {
        var block = Block(40);
        Text(block, "aaa.");
        Text(Span(block), "[1]");
        Layout(block);

        Assert.Equal(0, Word(block, "[1]").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "[1]").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa" and a quotation mark, then a span holding
    /// "[1]", in a 40px block: browsers break between two ASCII characters by a table of their
    /// own, which has a break there, where UAX #14 has none, and "[1]" starts the second line, 0px
    /// along and 22px down. Taken for one word, it would stay 32px along and 2px down.
    /// </summary>
    [Fact]
    public void Control_A_Quotation_Mark_Before_A_Bracket_In_An_Element_Is_A_Break()
    {
        var block = Block(40);
        Text(block, "aaa\"");
        Text(Span(block), "[1]");
        Layout(block);

        Assert.Equal(0, Word(block, "[1]").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "[1]").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa," then a span holding "¿b", in a 40px block:
    /// beside a character outside ASCII browsers follow UAX #14, which has a break between a comma
    /// and an opening mark (IS and OP), and "¿b" starts the second line, 0px along and 22px down.
    /// Taken for one word, it would stay 32px along and 2px down.
    /// </summary>
    [Fact]
    public void Control_A_Comma_Before_An_Inverted_Question_Mark_In_An_Element_Is_A_Break()
    {
        var block = Block(40);
        Text(block, "aaa,");
        Text(Span(block), "¿b");
        Layout(block);

        Assert.Equal(0, Word(block, "¿b").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "¿b").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa!" then a span holding "éé", in a 40px block:
    /// UAX #14 has a break after "!" before a letter (EX and AL), which browsers take beside a
    /// letter outside ASCII, and "éé" starts the second line, 0px along and 22px down. Taken for
    /// one word, it would stay 32px along and 2px down.
    /// </summary>
    [Fact]
    public void Control_An_Exclamation_Mark_Before_An_Accented_Letter_In_An_Element_Is_A_Break()
    {
        var block = Block(40);
        Text(block, "aaa!");
        Text(Span(block), "éé");
        Layout(block);

        Assert.Equal(0, Word(block, "éé").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "éé").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa°" then a span holding "[1]", in a 40px block:
    /// browsers break between a degree sign and an opening bracket, two characters of Latin-1,
    /// whatever follows, and "[1]" starts the second line, 0px along and 22px down. Taken for one
    /// word, it would stay 32px along and 2px down.
    /// </summary>
    [Fact]
    public void Control_A_Degree_Sign_Before_A_Bracket_And_A_Digit_In_An_Element_Is_A_Break()
    {
        var block = Block(40);
        Text(block, "aaa°");
        Text(Span(block), "[1]");
        Layout(block);

        Assert.Equal(0, Word(block, "[1]").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "[1]").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// "aaa" then a span holding "[1]", in a 40px block: the line may not break between a letter
    /// and an opening bracket, so "[1]" follows "aaa" on the first line, 24px along and 2px down,
    /// and overflows, as in browsers. It went to the second line, 0px along and 22px down.
    /// </summary>
    [Fact]
    public void A_Letter_Before_A_Bracket_In_An_Element_Is_No_Break()
    {
        var block = Block(40);
        Text(block, "aaa");
        Text(Span(block), "[1]");
        Layout(block);

        Assert.Equal(24, Word(block, "[1]").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "[1]").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// "aaa/" then a span holding "[1]", in a 40px block: UAX #14 has a break between "/" and "[",
    /// but the table browsers break two ASCII characters by has none, so "[1]" stays on the first
    /// line, 32px along and 2px down. It went to the second line, 0px along and 22px down.
    /// </summary>
    [Fact]
    public void A_Slash_Before_A_Bracket_In_An_Element_Is_No_Break()
    {
        var block = Block(40);
        Text(block, "aaa/");
        Text(Span(block), "[1]");
        Layout(block);

        Assert.Equal(32, Word(block, "[1]").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "[1]").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// "aaa!" then a span holding "bb", in a 40px block: the table browsers break two ASCII
    /// characters by has no break after "!" before a letter, so "bb" stays on the first line, 32px
    /// along and 2px down. It went to the second line, 0px along and 22px down.
    /// </summary>
    [Fact]
    public void An_Exclamation_Mark_Before_An_ASCII_Letter_In_An_Element_Is_No_Break()
    {
        var block = Block(40);
        Text(block, "aaa!");
        Text(Span(block), "bb");
        Layout(block);

        Assert.Equal(32, Word(block, "bb").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "bb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// "aaa€" then a span holding "[1]", in a 40px block: UAX #14 keeps a currency sign with an
    /// opening bracket that a digit follows (LB25), as browsers do beside a character outside
    /// Latin-1, so "[1]" stays on the first line, 32px along and 2px down. It went to the second
    /// line, 0px along and 22px down.
    /// </summary>
    [Fact]
    public void A_Euro_Sign_Before_A_Bracket_And_A_Digit_In_An_Element_Is_No_Break()
    {
        var block = Block(40);
        Text(block, "aaa€");
        Text(Span(block), "[1]");
        Layout(block);

        Assert.Equal(32, Word(block, "[1]").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "[1]").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a float in a 0px block holding "aaa." then a span
    /// holding "[1]": the line may break between them, so the widest thing that cannot be broken
    /// is "aaa.", and the float is 32px wide. Taken for one word, "aaa.[1]" would make it 56px wide.
    /// </summary>
    [Fact]
    public void Control_The_Min_Content_Width_Breaks_Before_A_Citation()
    {
        var block = Block(0);
        var floated = Float(block);
        Text(floated, "aaa.");
        Text(Span(floated), "[1]");
        Layout(block);

        Assert.Equal(32, floated.Size.Width, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "x aaa." then a span with 25px of left padding
    /// holding "[1]", in a 60px block: the line breaks between "aaa." and "[1]", and the padding
    /// goes to the next line with "[1]", so "aaa." stays on the first line, 12px along and 2px down,
    /// and "[1]" starts the second, 25px along and 22px down, as in browsers. Counted in the word
    /// "aaa." ends, the padding would move "aaa." to the second line too, 0px along and 22px down,
    /// and "[1]" 57px along.
    /// </summary>
    [Fact]
    public void Control_The_Padding_Before_A_Citation_Is_Not_Part_Of_The_Word_Before_It()
    {
        var block = Block(60);
        Text(block, "x aaa.");
        var span = Span(block);
        span.PaddingLeft = "25px";
        Text(span, "[1]");
        Layout(block);

        Assert.Equal(12, Word(block, "aaa.").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "aaa.").Top - block.Location.Y, 1);
        Assert.Equal(25, Word(block, "[1]").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "[1]").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "x aaa" then a span with 25px of left padding
    /// holding " bbb", in a 60px block: the line breaks at the space, and the padding before it goes
    /// to the next line with "bbb", so "aaa" stays on the first line, 12px along and 2px down, and
    /// "bbb" starts the second, 25px along and 22px down, as in browsers. Counted in the word "aaa"
    /// ends, the padding would move "aaa" to the second line, 0px along and 22px down, and "bbb" to
    /// a third, 42px down.
    /// </summary>
    [Fact]
    public void Control_The_Padding_Before_A_Space_Starting_An_Element_Is_Not_Part_Of_The_Word_Before_It()
    {
        var block = Block(60);
        Text(block, "x aaa");
        var span = Span(block);
        span.PaddingLeft = "25px";
        Text(span, " bbb");
        Layout(block);

        Assert.Equal(12, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(25, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "x aaa" then a span with 25px of left padding
    /// holding "—b", in a 60px block: the line may break before an em dash, and the padding goes to
    /// the next line with it, so "aaa" stays on the first line, 12px along and 2px down, as in
    /// browsers. Counted in the word "aaa" ends, the padding would move "aaa" to the second line,
    /// 0px along and 22px down.
    /// </summary>
    [Fact]
    public void Control_The_Padding_Before_An_Em_Dash_Starting_An_Element_Is_Not_Part_Of_The_Word_Before_It()
    {
        var block = Block(60);
        Text(block, "x aaa");
        var span = Span(block);
        span.PaddingLeft = "25px";
        Text(span, "—b");
        Layout(block);

        Assert.Equal(12, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "aaa").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "x aaa", then a span with 25px of left padding
    /// holding a 4px right float and a 10px inline-block, in a 60px block: the float is no content,
    /// so the line breaks before the span, as before one the inline-block starts, and "aaa" stays on
    /// the first line, 12px along and 2px down, as in browsers. Counted in the word "aaa" ends, the
    /// padding would move "aaa" to the second line, 0px along and 22px down.
    /// </summary>
    [Fact]
    public void Control_The_Padding_Before_A_Float_And_An_Inline_Block_Is_Not_Part_Of_The_Word_Before_It()
    {
        var block = Block(60);
        Text(block, "x aaa");
        var span = Span(block);
        span.PaddingLeft = "25px";
        SizedFloat(span, "right", 4, 4);
        _ = new CssBox(span, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = "10px",
            Height = "10px",
        };
        Layout(block);

        Assert.Equal(12, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "aaa").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: the same with a span that is not displayed in
    /// place of the float: "aaa" stays on the first line, 12px along and 2px down, as in browsers,
    /// and would move to the second, 0px along and 22px down.
    /// </summary>
    [Fact]
    public void Control_The_Padding_Before_A_Hidden_Box_And_An_Inline_Block_Is_Not_Part_Of_The_Word_Before_It()
    {
        var block = Block(60);
        Text(block, "x aaa");
        var span = Span(block);
        span.PaddingLeft = "25px";
        var hidden = Span(span);
        hidden.Display = CssConstants.None;
        Text(hidden, "q");
        _ = new CssBox(span, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = "10px",
            Height = "10px",
        };
        Layout(block);

        Assert.Equal(12, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "aaa").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "x ", a span holding "aaa", then a span with 25px
    /// of left padding holding a 10px right float and a span holding "bbb", in a 60px block: the
    /// line may break at the float, as at the white space dropped beside one, and before the span,
    /// so "aaa" stays on the first line, 12px along and 2px down, "bbb" goes to the second, and the
    /// block is 40px tall, as in browsers. Counted in the word "aaa" ends, the padding would move
    /// "aaa" to the second line, 0px along and 22px down, and "bbb" to a third, making the block
    /// 60px tall.
    /// </summary>
    [Fact]
    public void Control_A_Float_In_A_Padded_Element_After_A_Word_Adds_No_Line()
    {
        var block = Block(60);
        Text(block, "x ");
        Text(Span(block), "aaa");
        var span = Span(block);
        span.PaddingLeft = "25px";
        SizedFloat(span, "right", 10, 10);
        Text(Span(span), "bbb");
        Layout(block);

        Assert.Equal(12, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(40, block.Size.Height, 1);
    }

    /// <summary>
    /// A float in a 0px block holding a span with 30px of left padding holding a span that is not
    /// displayed, an i holding "bbb", then "ccc": nothing displayed comes before "bbbccc", so the
    /// padding is next to it, and the float is 30 + 48 = 78px wide, as browsers make it. The hidden
    /// span was taken for content before the word, and the padding for none of it: the float was
    /// 54px wide.
    /// </summary>
    [Fact]
    public void The_Min_Content_Width_Keeps_The_Left_Padding_Next_To_A_Word_After_A_Hidden_Box()
    {
        var block = Block(0);
        var floated = Float(block);
        var span = Span(floated);
        span.PaddingLeft = "30px";
        var hidden = Span(span);
        hidden.Display = CssConstants.None;
        Text(hidden, "q");
        Text(Span(span), "bbb");
        Text(span, "ccc");
        Layout(block);

        Assert.Equal(78, floated.Size.Width, 1);
    }

    /// <summary>
    /// As above with a 4px right float in place of the hidden span: the float is no content either,
    /// and the float around them is 78px wide, as browsers make it. It was 54px wide.
    /// </summary>
    [Fact]
    public void The_Min_Content_Width_Keeps_The_Left_Padding_Next_To_A_Word_After_A_Float()
    {
        var block = Block(0);
        var floated = Float(block);
        var span = Span(floated);
        span.PaddingLeft = "30px";
        SizedFloat(span, "right", 4, 4);
        Text(Span(span), "bbb");
        Text(span, "ccc");
        Layout(block);

        Assert.Equal(78, floated.Size.Width, 1);
    }

    /// <summary>
    /// A float in a 0px block holding a span with 30px of left padding holding a space, an i
    /// holding "bbb", then "ccc": the space collapses at the start of the line, and the padding goes
    /// with "bbbccc", so the float is 78px wide, as browsers make it. It was 54px wide.
    /// </summary>
    [Fact]
    public void The_Min_Content_Width_Keeps_The_Left_Padding_Next_To_A_Word_After_A_Space()
    {
        var block = Block(0);
        var floated = Float(block);
        var span = Span(floated);
        span.PaddingLeft = "30px";
        Text(span, " ");
        Text(Span(span), "bbb");
        Text(span, "ccc");
        Layout(block);

        Assert.Equal(78, floated.Size.Width, 1);
    }

    /// <summary>
    /// A 100px block with a 50px by 40px right float, then a nowrap span holding "aaa" and a span
    /// holding "bbbbbbb": "aaabbbbbbb", 80px, is one word, which does not fit in the 50px beside the
    /// float, so its line goes below it: "aaa" 0px along and 42px down, and "bbbbbbb" 24px along on
    /// the same line, as in browsers. "aaa" was 2px down beside the float, and "bbbbbbb" 0px along
    /// and 42px down, the word broken at the span's edge; measured without the text joined to it,
    /// the nowrap span would stay beside the float, and "bbbbbbb" be drawn over it, 24px along and
    /// 2px down.
    /// </summary>
    [Fact]
    public void A_Span_That_Does_Not_Wrap_Joined_To_Text_Goes_Below_A_Right_Float_It_Does_Not_Fit_Beside()
    {
        var block = Block(100);
        SizedFloat(block, "right", 50, 40);
        Text(Span(block, whiteSpace: CssConstants.NoWrap), "aaa");
        Text(Span(block), "bbbbbbb");
        Layout(block);

        Assert.Equal(0, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(42, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(24, Word(block, "bbbbbbb").Left - block.Location.X, 1);
        Assert.Equal(42, Word(block, "bbbbbbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// As above with a left float: "aaa" goes below the float, 0px along and 42px down, and
    /// "bbbbbbb" 24px along, as in browsers. "aaa" was 50px along beside the float and "bbbbbbb"
    /// 0px along on the next line; measured without it, "bbbbbbb" would follow "aaa" beside the
    /// float, 74px along, and run 30px past the block's side.
    /// </summary>
    [Fact]
    public void A_Span_That_Does_Not_Wrap_Joined_To_Text_Goes_Below_A_Left_Float_It_Does_Not_Fit_Beside()
    {
        var block = Block(100);
        SizedFloat(block, "left", 50, 40);
        Text(Span(block, whiteSpace: CssConstants.NoWrap), "aaa");
        Text(Span(block), "bbbbbbb");
        Layout(block);

        Assert.Equal(0, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(42, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(24, Word(block, "bbbbbbb").Left - block.Location.X, 1);
    }

    /// <summary>
    /// A 100px block with a 50px by 40px right float, then "x ", a nowrap span holding "aaa" and a
    /// span holding "bbb": "aaabbb", 48px, does not fit after "x " in the 50px beside the float, so
    /// the line breaks at the space, and it goes on the second line, which it fits: "aaa" 0px along
    /// and 22px down, "bbb" 24px along and 22px down. "aaa" stayed beside "x", 12px along and 2px
    /// down, and "bbb" went to the second line, 0px along; measured against the side of the block
    /// only, "bbb" would stay on the first line, 36px along, over the float.
    /// </summary>
    [Fact]
    public void A_Span_That_Does_Not_Wrap_Joined_To_Text_After_A_Space_Goes_To_The_Next_Line_Beside_A_Float()
    {
        var block = Block(100);
        SizedFloat(block, "right", 50, 40);
        Text(block, "x ");
        Text(Span(block, whiteSpace: CssConstants.NoWrap), "aaa");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(24, Word(block, "bbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// As above with a pre span in place of the nowrap one: "aaa" 0px along and 22px down, "bbb"
    /// 24px along. "aaa" was 12px along and 2px down, and "bbb" 0px along; against the side of the
    /// block only, "bbb" would be drawn over the float, 36px along.
    /// </summary>
    [Fact]
    public void A_Pre_Span_Joined_To_Text_After_A_Space_Goes_To_The_Next_Line_Beside_A_Float()
    {
        var block = Block(100);
        SizedFloat(block, "right", 50, 40);
        Text(block, "x ");
        Text(Span(block, whiteSpace: CssConstants.Pre), "aaa");
        Text(Span(block), "bbb");
        Layout(block);

        Assert.Equal(0, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(24, Word(block, "bbb").Left - block.Location.X, 1);
    }

    /// <summary>
    /// A 100px block with a 60px by 40px left float, then "x ", a nowrap span holding "aaa" and a
    /// span holding "bbbb": "aaabbbb", 56px, does not fit beside "x", nor in the 40px beside the
    /// float on the second line, so it goes below the float: "aaa" 0px along and 42px down, "bbbb"
    /// 24px along, as in browsers. "aaa" was 72px along and 2px down, and "bbbb" 60px along on the
    /// second line; with the width of "aaa" alone for the float, "aaa" would go to the second line
    /// beside the float, 60px along and 22px down, and "bbbb" run past the block's side, 84px along.
    /// </summary>
    [Fact]
    public void A_Span_That_Does_Not_Wrap_Joined_To_Text_Moved_To_The_Next_Line_Goes_Below_A_Float()
    {
        var block = Block(100);
        SizedFloat(block, "left", 60, 40);
        Text(block, "x ");
        Text(Span(block, whiteSpace: CssConstants.NoWrap), "aaa");
        Text(Span(block), "bbbb");
        Layout(block);

        Assert.Equal(0, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(42, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(24, Word(block, "bbbb").Left - block.Location.X, 1);
    }

    /// <summary>
    /// As above with "aaa" followed by a span holding "b" and a span holding "bbbbbb": the text
    /// joined to the nowrap span is measured as far as a whole line, 80px with "aaa", which does not
    /// fit beside the float, so "aaa" goes below it, 0px along and 42px down, and "bbbbbb" 32px
    /// along. "aaa" was 72px along and 2px down, and "bbbbbb" 0px along and 42px down; measured
    /// only as far as the room left beside "x", the text came to "aaab", which fits beside the
    /// float on the second line, and "aaa" would go there, 60px along and 22px down, with "bbbbbb"
    /// past the block's side, 92px along.
    /// </summary>
    [Fact]
    public void A_Span_That_Does_Not_Wrap_Joined_To_Text_Across_Elements_Goes_Below_A_Float()
    {
        var block = Block(100);
        SizedFloat(block, "left", 60, 40);
        Text(block, "x ");
        Text(Span(block, whiteSpace: CssConstants.NoWrap), "aaa");
        Text(Span(block), "b");
        Text(Span(block), "bbbbbb");
        Layout(block);

        Assert.Equal(0, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(42, Word(block, "aaa").Top - block.Location.Y, 1);
        Assert.Equal(32, Word(block, "bbbbbb").Left - block.Location.X, 1);
        Assert.Equal(42, Word(block, "bbbbbb").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// A 200px block with a 120px by 50px right float, then "On ", a nowrap span holding "aaaaaa",
    /// a span holding "bb", and " cc": "On aaaaaa" fits in the 80px beside the float, "On aaaaaabb"
    /// does not, so "aaaaaabb" goes to the second line, "aaaaaa" 0px along and 22px down and "bb"
    /// 48px along. "aaaaaa" stayed on the first line, 20px along, and "bb" went to the second, 0px
    /// along; against the side of the block only, "bb" would stay on the first line, 68px along,
    /// over the float.
    /// </summary>
    [Fact]
    public void A_Span_That_Does_Not_Wrap_Joined_To_Text_Is_Not_Drawn_Over_A_Float_Beside_Its_Line()
    {
        var block = Block(200);
        SizedFloat(block, "right", 120, 50);
        Text(block, "On ");
        Text(Span(block, whiteSpace: CssConstants.NoWrap), "aaaaaa");
        Text(Span(block), "bb");
        Text(block, " cc");
        Layout(block);

        Assert.Equal(0, Word(block, "aaaaaa").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "aaaaaa").Top - block.Location.Y, 1);
        Assert.Equal(48, Word(block, "bb").Left - block.Location.X, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a 100px block with a 50px by 40px right float, then
    /// a nowrap span holding "aaa" and a span holding " bbbbbbb": the line may break at the space,
    /// so only "aaa" has to fit beside the float, and it stays on the first line, 0px along and 2px
    /// down.
    /// </summary>
    [Fact]
    public void Control_A_Span_That_Does_Not_Wrap_Before_A_Space_Stays_Beside_A_Float()
    {
        var block = Block(100);
        SizedFloat(block, "right", 50, 40);
        Text(Span(block, whiteSpace: CssConstants.NoWrap), "aaa");
        Text(Span(block), " bbbbbbb");
        Layout(block);

        Assert.Equal(0, Word(block, "aaa").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "aaa").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// A 200px block with a 50px by 40px right float, then a nowrap span holding "aaaa bbbb cccc
    /// dddd eeee", 176px, which does not fit in the 150px beside the float: the block wraps, so its
    /// line goes below the float, "aaaa" 0px along and 42px down, as in browsers. "aaaa" stood
    /// beside the float, 2px down, and the span ran on over it.
    /// </summary>
    [Fact]
    public void A_Span_That_Does_Not_Wrap_Starting_A_Line_Goes_Below_A_Float_It_Does_Not_Fit_Beside()
    {
        var block = Block(200);
        SizedFloat(block, "right", 50, 40);
        Text(Span(block, whiteSpace: CssConstants.NoWrap), "aaaa bbbb cccc dddd eeee");
        Layout(block);

        Assert.Equal(0, Word(block, "aaaa").Left - block.Location.X, 1);
        Assert.Equal(42, Word(block, "aaaa").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a 200px nowrap block with a 50px by 40px right
    /// float, then "aaaa bbbb cccc dddd eeee", 176px. A block that does not wrap keeps its line
    /// beside the float and lets it overflow, as browsers do: "aaaa" 0px along and 2px down.
    /// Measured whole, as a nowrap span starting a line of a block that wraps is (above), the line
    /// would go below the float, 42px down.
    /// </summary>
    [Fact]
    public void Control_A_Block_That_Does_Not_Wrap_Keeps_Its_First_Line_Beside_A_Float()
    {
        var block = Block(200, CssConstants.NoWrap);
        SizedFloat(block, "right", 50, 40);
        Text(block, "aaaa bbbb cccc dddd eeee");
        Layout(block);

        Assert.Equal(0, Word(block, "aaaa").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "aaaa").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: as above in a pre block, "aaaa" 2px down.
    /// </summary>
    [Fact]
    public void Control_A_Pre_Block_Keeps_Its_First_Line_Beside_A_Float()
    {
        var block = Block(200, CssConstants.Pre);
        SizedFloat(block, "right", 50, 40);
        Text(block, "aaaa bbbb cccc dddd eeee");
        Layout(block);

        Assert.Equal(2, Word(block, "aaaa").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a 200px nowrap block with a 50px by 20px right
    /// float, then a span holding "aaaa bbbb cccc dddd eeee", as a one-line row holds its title
    /// after a floated badge: the span stays on the first line, "aaaa" 2px down. Below the float,
    /// 22px down, it would be clipped out of a 20px row with `overflow: hidden`.
    /// </summary>
    [Fact]
    public void Control_A_Block_That_Does_Not_Wrap_Keeps_A_Span_Starting_Its_Line_Beside_A_Float()
    {
        var block = Block(200, CssConstants.NoWrap);
        SizedFloat(block, "right", 50, 20);
        Text(Span(block), "aaaa bbbb cccc dddd eeee");
        Layout(block);

        Assert.Equal(2, Word(block, "aaaa").Top - block.Location.Y, 1);
    }

    /// <summary>A float of the given side and size in <paramref name="parent"/>.</summary>
    private static CssBox SizedFloat(CssBox parent, string side, int width, int height) =>
        new(parent, new HtmlTag("i", false, null), BaseUrl)
        {
            Display = "block",
            Float = side,
            Width = width + "px",
            Height = height + "px",
        };

    /// <summary>A left float in <paramref name="block"/>, inheriting its style, with no width.</summary>
    private static CssBox Float(CssBox block)
    {
        var floated = new CssBox(block, new HtmlTag("div", false, null), BaseUrl);
        floated.InheritStyle();
        floated.Display = "block";
        floated.Float = "left";
        return floated;
    }

    /// <summary>
    /// A block of the given width, with 20px lines of a 16px font and the given <c>white-space</c>,
    /// in a block in the root.
    /// </summary>
    private static CssBox Block(int width, string whiteSpace = CssConstants.Normal)
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
            Width = width + "px",
            FontSize = "16px",
            LineHeight = "20px",
            WhiteSpace = whiteSpace,
        };
    }

    private static void Layout(CssBox block)
    {
        var root = block.ParentBox!.ParentBox!;
        root.PerformLayout(root.LayoutEnvironment);
    }

    /// <summary>
    /// A span in <paramref name="parent"/> inheriting its style, in the font size and with the
    /// <c>white-space</c> given.
    /// </summary>
    private static CssBox Span(CssBox parent, string? fontSize = null, string? whiteSpace = null)
    {
        var span = new CssBox(parent, new HtmlTag("span", false, null), BaseUrl);
        span.InheritStyle();
        span.Display = CssConstants.Inline;

        if (fontSize != null)
            span.FontSize = fontSize;

        if (whiteSpace != null)
            span.WhiteSpace = whiteSpace;

        return span;
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

    private static System.Collections.Generic.IEnumerable<CssBox> Descendants(CssBox box)
    {
        foreach (var child in box.Boxes)
        {
            yield return child;

            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
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

        public void LoadImage(string src, System.Collections.Generic.IReadOnlyDictionary<string, string>? attributes, Uri baseUrl)
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

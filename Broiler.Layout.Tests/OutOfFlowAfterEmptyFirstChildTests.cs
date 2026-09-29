using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A float or an absolutely positioned box between an empty first child and the block after it
/// does not keep their margins from collapsing through the parent's top: the parent moves, and the
/// box goes where the margins end, at the parent's top.
/// </summary>
/// <remarks>
/// <para>
/// CSS2.1 §8.3.1: an empty block's top and bottom margins adjoin, and adjoin its parent's top margin
/// when it is the first child, and the next in-flow block's top margin. Floats and absolutely
/// positioned boxes are out of flow (§9.5, §9.6), so one between the two separates nothing, and
/// browsers place it where the collapsed margins before it end. <c>MarginTopCollapse</c> collapsed
/// the margins of the block after the empty one through the parent only when nothing but
/// <c>display: none</c> boxes stood between them: after a 10px block, <c>&lt;div&gt;&lt;div
/// style="margin-bottom: 16px"&gt;&lt;/div&gt;&lt;div style="float: left"&gt;&lt;/div&gt;&lt;p&gt;</c>
/// began right below the 10px block, 36px tall with the float and the paragraph 16px down it, where
/// browsers begin it 16px lower, 20px tall with both at its top. A float between is placed again
/// there by the float rules (§9.5.1), beside or below the floats before it as they are then. A block
/// whose <c>clear</c> takes it past a float the set places, or past one outside that reaches below
/// where the block would begin, has clearance (§9.5.2): the set ends without its margin, and its top
/// margin does not collapse with the parent's. An empty first child with clearance keeps the set
/// inside the parent. A table, or a block that establishes a formatting context, that the floats
/// leave no room for where its margin puts it goes below them (§9.5), and its margin leaves the set
/// as a margin with clearance does, after an empty first child or as the first child itself.
/// </para>
/// <para>
/// Whenever margins move the parent, across out-of-flow boxes or not, what it holds already goes
/// with it but for the out-of-flow boxes whose place does not follow its own: a float the set
/// places below a part of it that an empty block handed on is placed again where the set ends, and
/// so is a float that the move takes out of the reach of a float outside the parent, which stays
/// where it is, that held it down or stood beside it; a float in a box that <c>position:
/// relative</c> has shifted goes with it; and an absolutely positioned box that its offsets place
/// in a containing block outside the parent stays where they put it (§10.6.4), unless they place it
/// against a height that what the parent holds sizes.
/// </para>
/// <para>
/// Each run of blocks here is in a 320px block in a block in the root, after a 10px block. The
/// empty first child has <c>margin-bottom: 16px</c> unless a test says otherwise, floats are 30 ×
/// 12px, paragraphs have no bottom margin, and words are 8 × 16px.
/// </para>
/// </remarks>
public sealed class OutOfFlowAfterEmptyFirstChildTests
{
    private static readonly Uri BaseUrl = new("file:///out-of-flow-after-empty-first-child.html");

    /// <summary>
    /// An empty first child, then a float, an absolutely positioned box or a fixed one with auto
    /// offsets, then a paragraph: the outer block begins 16px below the 10px block, 16px tall, with
    /// the out-of-flow box and the paragraph at its top. It began right below the 10px block, 32px
    /// tall, with the box and the paragraph 16px down it.
    /// </summary>
    [Theory]
    [InlineData("float")]
    [InlineData("absolute")]
    [InlineData("fixed")]
    public void The_Parent_Moves_And_The_Box_Goes_To_Its_Top(string kind)
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var box = OutOfFlow(outer, kind);
        var paragraph = Paragraph(outer, "0");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 16, outer.Location.Y, 1);
        Assert.Equal(16, outer.Size.Height, 1);
        Assert.Equal(outer.Location.Y, box.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// The paragraph's own <c>margin-top: 20px</c> joins the set, and the float goes where the set
    /// ends: the outer block, the float and the paragraph begin 20px below the 10px block. The outer
    /// block began right below it, the float 16px and the paragraph 20px below it.
    /// </summary>
    [Fact]
    public void The_Box_Goes_Where_The_Whole_Set_Ends()
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var floated = OutOfFlow(outer, "float");
        var paragraph = Paragraph(outer, "20px");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 20, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, floated.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// With <c>margin-bottom: -6px</c> on the empty first child, the outer block, the float or the
    /// absolutely positioned box, and the paragraph begin 6px above the 10px block's bottom: the
    /// float goes no higher than the outer block's top, which is there now. The paragraph and the
    /// absolutely positioned box were there already, but the outer block began right below the 10px
    /// block, 10px tall, and the float at its top.
    /// </summary>
    [Theory]
    [InlineData("float")]
    [InlineData("absolute")]
    public void A_Negative_Margin_Moves_The_Parent_Up(string kind)
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "-6px";
        var box = OutOfFlow(outer, kind);
        var paragraph = Paragraph(outer, "0");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom - 6, outer.Location.Y, 1);
        Assert.Equal(16, outer.Size.Height, 1);
        Assert.Equal(outer.Location.Y, box.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// An empty first child, a float, an empty block with <c>margin-bottom: 20px</c> and a
    /// paragraph: the outer block, the float and the paragraph begin 20px below the 10px block. The
    /// outer block began right below it, and the float 16px below it.
    /// </summary>
    [Fact]
    public void An_Empty_Block_After_The_Box_Joins_The_Set_Too()
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var floated = OutOfFlow(outer, "float");
        Block(outer).MarginBottom = "20px";
        var paragraph = Paragraph(outer, "0");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 20, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, floated.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// A paragraph with <c>clear: both</c> or <c>clear: left</c> and <c>margin-top: 20px</c> after
    /// the float has clearance past it, so the set ends before the paragraph's margin joins it: the
    /// outer block and the float begin 16px below the 10px block, and the paragraph below the float,
    /// 28px below. The outer block began right below the 10px block, 44px tall.
    /// </summary>
    [Theory]
    [InlineData("both")]
    [InlineData("left")]
    public void A_Paragraph_With_Clearance_Leaves_Its_Margin_Out(string clear)
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var floated = OutOfFlow(outer, "float");
        var paragraph = Paragraph(outer, "20px");
        paragraph.Clear = clear;
        page.Layout();

        double below = page.Before.ActualBottom;

        Assert.Equal(below + 16, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, floated.Location.Y, 1);
        Assert.Equal(below + 28, paragraph.Location.Y, 1);
        Assert.Equal(28, outer.Size.Height, 1);
    }

    /// <summary>
    /// After the float, an empty block with <c>clear: both</c> and then a paragraph with
    /// <c>margin-top: 20px</c>, or a block with <c>clear: both</c> holding a paragraph with
    /// <c>margin-top: 30px</c>: the cleared block has clearance past the float, so its top margin
    /// does not collapse with the outer block's, and the margins after it or in it leave the outer
    /// block and the float 16px below the 10px block. The outer block began right below the 10px
    /// block, and the float 16px below it.
    /// </summary>
    [Theory]
    [InlineData("after")]
    [InlineData("inside")]
    public void Margins_Past_A_Cleared_Block_Do_Not_Move_The_Parent(string where)
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var floated = OutOfFlow(outer, "float");
        var cleared = Block(outer);
        cleared.Clear = "both";

        if (where == "after")
            Paragraph(outer, "20px");
        else
            Paragraph(cleared, "30px");

        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 16, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, floated.Location.Y, 1);
    }

    /// <summary>
    /// A paragraph with <c>clear: right</c> and <c>margin-top: 20px</c> after a left float clears
    /// nothing, so its margin joins the set: the outer block, the float and the paragraph begin
    /// 20px below the 10px block. The outer block began right below it, the float 16px below.
    /// </summary>
    [Fact]
    public void A_Paragraph_That_Clears_The_Other_Side_Keeps_Its_Margin_In()
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var floated = OutOfFlow(outer, "float");
        var paragraph = Paragraph(outer, "20px");
        paragraph.Clear = CssConstants.Right;
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 20, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, floated.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// An absolutely positioned box with <c>top: 5px</c> between is placed in its containing block,
    /// the root, and stays 5px down it when the outer block begins 16px below the 10px block. The
    /// outer block began right below the 10px block.
    /// </summary>
    [Fact]
    public void A_Box_Placed_By_Its_Offsets_Stays_In_Its_Containing_Block()
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var positioned = OutOfFlow(outer, "absolute");
        positioned.Top = "5px";
        var paragraph = Paragraph(outer, "0");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 16, outer.Location.Y, 1);
        Assert.Equal(page.Root.Location.Y + 5, positioned.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// When the outer block is <c>position: relative</c>, it is the containing block of an
    /// absolutely positioned box with <c>top: 5px</c> between, which moves with it: the outer block
    /// begins 16px below the 10px block, and the box 5px down it. The outer block began right below
    /// the 10px block.
    /// </summary>
    [Fact]
    public void A_Box_Placed_By_Its_Offsets_Moves_With_A_Containing_Block_That_Moves()
    {
        var page = new Page();
        var outer = Block(page.Content);
        outer.Position = CssConstants.Relative;
        Block(outer).MarginBottom = "16px";
        var positioned = OutOfFlow(outer, "absolute");
        positioned.Top = "5px";
        Paragraph(outer, "0");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 16, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y + 5, positioned.Location.Y, 1);
    }

    /// <summary>
    /// Two 200px floats between, which do not fit side by side in 320px: the outer block begins 16px
    /// below the 10px block with the first float at its top and the second 12px down it, below the
    /// first. The outer block began right below the 10px block, and the floats 16px and 28px down it.
    /// </summary>
    [Fact]
    public void Floats_Between_Keep_Their_Places_Beside_Each_Other()
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var first = OutOfFlow(outer, "float");
        first.Width = "200px";
        var second = OutOfFlow(outer, "float");
        second.Width = "200px";
        Paragraph(outer, "0");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 16, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, first.Location.Y, 1);
        Assert.Equal(outer.Location.Y + 12, second.Location.Y, 1);
    }

    /// <summary>
    /// A float before the empty first child goes down with the outer block, 16px below the 10px
    /// block, and a float between is placed again beside it, 30px across, at the same top; with
    /// <c>clear: left</c>, or 300px wide, which does not fit beside it, below it, 12px lower. The
    /// outer block began right below the 10px block with the first float at its top and the second
    /// 16px down it at the left.
    /// </summary>
    [Theory]
    [InlineData("beside")]
    [InlineData("clear")]
    [InlineData("wide")]
    public void A_Float_Between_Is_Placed_Again_Beside_A_Float_Before(string kind)
    {
        var page = new Page();
        var outer = Block(page.Content);
        var first = OutOfFlow(outer, "float");
        Block(outer).MarginBottom = "16px";
        var second = OutOfFlow(outer, "float");
        Paragraph(outer, "0");

        if (kind == "clear")
            second.Clear = CssConstants.Left;
        else if (kind == "wide")
            second.Width = "300px";

        page.Layout();

        double left = page.Content.Location.X;
        bool beside = kind == "beside";

        Assert.Equal(page.Before.ActualBottom + 16, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, first.Location.Y, 1);
        Assert.Equal(left, first.Location.X, 1);
        Assert.Equal(outer.Location.Y + (beside ? 0 : 12), second.Location.Y, 1);
        Assert.Equal(left + (beside ? 30 : 0), second.Location.X, 1);
    }

    /// <summary>
    /// A 320 × 40px float after the 10px block pushes the float between below it, 50px down the
    /// 320px block, which the outer block moving does not change: with <c>margin-top: 30px</c>,
    /// <c>-6px</c> or <c>16px</c> on the paragraph, the outer block begins 30px, 10px or 16px below
    /// the 10px block. The float was 50px down in each, the outer block right below the 10px block.
    /// </summary>
    [Theory]
    [InlineData("30px", 30)]
    [InlineData("-6px", 10)]
    [InlineData("16px", 16)]
    public void A_Float_Between_Stays_Below_A_Float_Outside(string marginTop, double set)
    {
        var page = new Page();
        var wide = OutOfFlow(page.Content, "float");
        wide.Width = "320px";
        wide.Height = "40px";
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var floated = OutOfFlow(outer, "float");
        Paragraph(outer, marginTop);
        page.Layout();

        double top = page.Content.Location.Y;

        Assert.Equal(page.Before.ActualBottom + set, outer.Location.Y, 1);
        Assert.Equal(top + 50, floated.Location.Y, 1);
    }

    /// <summary>
    /// With <c>margin-top: -16px</c> on the paragraph the set comes to nothing, and the outer block
    /// stays right below the 10px block, with the float or the absolutely positioned box at its
    /// top. The box was 16px down it.
    /// </summary>
    [Theory]
    [InlineData("float")]
    [InlineData("absolute")]
    public void The_Box_Goes_To_The_Top_When_The_Set_Comes_To_Nothing(string kind)
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var box = OutOfFlow(outer, kind);
        var paragraph = Paragraph(outer, "-16px");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, box.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// A float with <c>margin-top: 5px</c> between keeps its margin, which does not collapse
    /// (CSS2.1 §8.3.1): the outer block begins 16px below the 10px block with the float 5px down it.
    /// The outer block began right below the 10px block, with the float 16px down it.
    /// </summary>
    [Fact]
    public void A_Float_Between_Keeps_Its_Own_Top_Margin()
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var floated = OutOfFlow(outer, "float");
        floated.MarginTop = "5px";
        Paragraph(outer, "0");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 16, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y + 5, floated.Location.Y, 1);
    }

    /// <summary>
    /// A paragraph with <c>clear: left</c> and <c>margin-top: 20px</c> after a left float before
    /// the empty first child, with a right float between, has clearance past the first float, which
    /// the set places too: the outer block and both floats begin 16px below the 10px block, and the
    /// paragraph below the left float, 28px below. The outer block began right below the 10px block
    /// with the left float at its top and the right float 16px down it.
    /// </summary>
    [Fact]
    public void Clearance_Past_A_Float_Before_The_Empty_Child_Leaves_The_Margin_Out()
    {
        var page = new Page();
        var outer = Block(page.Content);
        var first = OutOfFlow(outer, "float");
        Block(outer).MarginBottom = "16px";
        var second = OutOfFlow(outer, "float");
        second.Float = CssConstants.Right;
        var paragraph = Paragraph(outer, "20px");
        paragraph.Clear = CssConstants.Left;
        page.Layout();

        double below = page.Before.ActualBottom;

        Assert.Equal(below + 16, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, first.Location.Y, 1);
        Assert.Equal(outer.Location.Y, second.Location.Y, 1);
        Assert.Equal(below + 28, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// An <c>&lt;hr&gt;</c> with 8px margins after the absolutely positioned box: its margin joins
    /// the set, and it goes to the top of the outer block, 16px below the 10px block. The outer
    /// block began right below the 10px block with the <c>&lt;hr&gt;</c> 16px down it.
    /// </summary>
    [Fact]
    public void An_Hr_After_The_Box_Goes_To_The_Top()
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        OutOfFlow(outer, "absolute");
        var hr = new CssBoxHr(outer, new HtmlTag("hr", false, null), BaseUrl) { MarginTop = "8px", MarginBottom = "8px" };
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 16, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, hr.Location.Y, 1);
    }

    /// <summary>
    /// Laid out a second time, the float case gives the same places: the outer block, the float and
    /// the paragraph 16px below the 10px block. The outer block began right below it both times.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Places()
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var floated = OutOfFlow(outer, "float");
        var paragraph = Paragraph(outer, "0");
        page.Layout();
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 16, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, floated.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// Controls, which pass before and after, each an outer block whose first child is an empty
    /// block with <c>margin-bottom: 16px</c>, then a float:
    /// <list type="bullet">
    /// <item>with 1px of top padding and a paragraph, the outer block begins right below the 10px
    /// block and holds the float and the paragraph 17px down it;</item>
    /// <item>with <c>overflow: hidden</c> and a paragraph, it holds them 16px down;</item>
    /// <item>with nothing after the float, the float is 16px below the 10px block.</item>
    /// </list>
    /// </summary>
    [Theory]
    [InlineData("padding")]
    [InlineData("overflow")]
    [InlineData("last")]
    public void Control_A_Set_That_Stays_Inside_Or_Ends_With_The_Float(string kind)
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var floated = OutOfFlow(outer, "float");
        CssBox? paragraph = kind == "last" ? null : Paragraph(outer, "0");

        if (kind == "padding")
            outer.PaddingTop = "1px";
        else if (kind == "overflow")
            outer.Overflow = "hidden";

        page.Layout();

        double below = page.Before.ActualBottom;
        double inside = kind == "padding" ? 17 : 16;

        Assert.Equal(below + inside, floated.Location.Y, 1);

        if (paragraph != null)
        {
            Assert.Equal(below, outer.Location.Y, 1);
            Assert.Equal(below + inside, paragraph.Location.Y, 1);
        }
    }

    /// <summary>
    /// Control, which passes before and after: after a 50 × 60px float that follows the 10px block,
    /// the outer block's first child is an empty block with <c>clear: both</c>, then a float and a
    /// paragraph with <c>margin-top: 20px</c>. The empty block has clearance past the big float, so
    /// its margins do not collapse with the outer block's (CSS2.1 §8.3.1): the outer block stays
    /// right below the 10px block, the float goes below the big float, 70px down the 320px block,
    /// and the paragraph 20px lower.
    /// </summary>
    [Fact]
    public void Control_An_Empty_First_Child_With_Clearance_Keeps_The_Set_Inside()
    {
        var page = new Page();
        var big = OutOfFlow(page.Content, "float");
        big.Width = "50px";
        big.Height = "60px";
        var outer = Block(page.Content);
        Block(outer).Clear = "both";
        var floated = OutOfFlow(outer, "float");
        var paragraph = Paragraph(outer, "20px");
        page.Layout();

        double top = page.Content.Location.Y;

        Assert.Equal(page.Before.ActualBottom, outer.Location.Y, 1);
        Assert.Equal(top + 70, floated.Location.Y, 1);
        Assert.Equal(top + 90, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// After a 100 × 50px left float that follows the 10px block, a paragraph with <c>clear:
    /// both</c> and <c>margin-top: 30px</c> after an absolutely positioned box has clearance past
    /// that float, which reaches below where the paragraph would begin if its margin joined the set,
    /// 30px below the 10px block: the set ends without it, the outer block and the box begin 16px
    /// below the 10px block, the paragraph below the float, 50px below the 10px block, and the outer
    /// block is 50px tall. After a 100 × 80px right float, a left float between and a paragraph with
    /// <c>clear: right</c> and <c>margin-top: 50px</c>: the outer block and the left float 16px
    /// below the 10px block, the paragraph 80px below it, the outer block 80px tall. The outer block
    /// began right below the 10px block, 66px and 96px tall, with the box or the float 16px down it
    /// and the paragraph where it is now.
    /// </summary>
    [Theory]
    [InlineData("absolute")]
    [InlineData("float")]
    public void Clearance_Past_A_Float_Outside_Leaves_The_Margin_Out(string kind)
    {
        var page = new Page();
        bool absolute = kind == "absolute";
        Float(page.Content, "100px", absolute ? "50px" : "80px", absolute ? CssConstants.Left : CssConstants.Right);
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var box = OutOfFlow(outer, kind);
        var paragraph = Paragraph(outer, absolute ? "30px" : "50px");
        paragraph.Clear = absolute ? "both" : CssConstants.Right;
        page.Layout();

        double below = page.Before.ActualBottom;
        double floatHeight = absolute ? 50 : 80;

        Assert.Equal(below + 16, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, box.Location.Y, 1);
        Assert.Equal(below + floatHeight, paragraph.Location.Y, 1);
        Assert.Equal(floatHeight, outer.Size.Height, 1);
    }

    /// <summary>
    /// After a 100 × 20px left float, which ends 20px below the 10px block, the paragraph with
    /// <c>clear: both</c> and <c>margin-top: 30px</c> after an absolutely positioned box would begin
    /// 30px below the 10px block if its margin joined the set, past the float, so it has no
    /// clearance and its margin joins: the outer block, the box and the paragraph begin 30px below
    /// the 10px block. The outer block began right below it, 46px tall, with the box 16px and the
    /// paragraph 30px down it.
    /// </summary>
    [Fact]
    public void No_Clearance_Past_A_Float_Outside_That_Ends_Above()
    {
        var page = new Page();
        Float(page.Content, "100px", "20px", CssConstants.Left);
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var box = OutOfFlow(outer, "absolute");
        var paragraph = Paragraph(outer, "30px");
        paragraph.Clear = "both";
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 30, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, box.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: after a 150 × 120px left float, standing for an
    /// infobox, and a paragraph with <c>margin-bottom: 16px</c>, the outer block's first child is
    /// empty, then come a 60 × 30px right float and a paragraph, standing for a heading, with
    /// <c>clear: left</c> and 24px and 8px margins. The heading has clearance past the infobox, so
    /// the set is the paragraph's 16px: the outer block and the right float begin 16px below the
    /// paragraph, and the heading below the infobox, 120px below the 10px block.
    /// </summary>
    [Fact]
    public void Control_A_Heading_With_Clearance_Past_An_Infobox_Leaves_Its_Margin_Out()
    {
        var page = new Page();
        Float(page.Content, "150px", "120px", CssConstants.Left);
        var intro = Paragraph(page.Content, "0");
        intro.MarginBottom = "16px";
        var outer = Block(page.Content);
        Block(outer);
        var thumbnail = Float(outer, "60px", "30px", CssConstants.Right);
        var heading = Paragraph(outer, "24px");
        heading.MarginBottom = "8px";
        heading.Clear = CssConstants.Left;
        page.Layout();

        Assert.Equal(intro.ActualBottom + 16, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, thumbnail.Location.Y, 1);
        Assert.Equal(page.Before.ActualBottom + 120, heading.Location.Y, 1);
    }

    /// <summary>
    /// An absolutely positioned box with <c>top: 5px</c> in the root stays 5px down the root when
    /// the box it is in moves with the outer block, 16px below the 10px block: in the empty first
    /// child, with a float between or with nothing between; in the float between, with
    /// <c>margin-top: 20px</c> on the paragraph and the outer block 20px below; in a float before the
    /// empty first child; or in the outer block itself, before the empty first child. With nothing
    /// between it went 16px down with the outer block, 21px down the root; otherwise it was 5px down
    /// the root, and the outer block right below the 10px block.
    /// </summary>
    [Theory]
    [InlineData("empty")]
    [InlineData("empty, nothing between")]
    [InlineData("float between")]
    [InlineData("float before")]
    [InlineData("before")]
    public void A_Box_Placed_By_Its_Offsets_Stays_When_The_Box_It_Is_In_Moves(string where)
    {
        var page = new Page();
        var outer = Block(page.Content);
        var holder = outer;
        string marginTop = "0";

        if (where is "float before" or "before")
        {
            if (where == "float before")
                holder = OutOfFlow(outer, "float");

            var early = OutOfFlow(holder, "absolute");
            early.Top = "5px";
            Block(outer).MarginBottom = "16px";
            OutOfFlow(outer, "float");
            Paragraph(outer, marginTop);
            page.Layout();

            Assert.Equal(page.Before.ActualBottom + 16, outer.Location.Y, 1);
            Assert.Equal(page.Root.Location.Y + 5, early.Location.Y, 1);
            return;
        }

        var empty = Block(outer);
        empty.MarginBottom = "16px";
        holder = empty;

        if (where == "float between")
        {
            holder = OutOfFlow(outer, "float");
            marginTop = "20px";
        }
        else if (where == "empty")
        {
            OutOfFlow(outer, "float");
        }

        var positioned = OutOfFlow(holder, "absolute");
        positioned.Top = "5px";
        Paragraph(outer, marginTop);
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + (where == "float between" ? 20 : 16), outer.Location.Y, 1);
        Assert.Equal(page.Root.Location.Y + 5, positioned.Location.Y, 1);
    }

    /// <summary>
    /// An absolutely positioned box in the empty first child that its <c>top</c> places in a
    /// containing block outside the outer block stays where the offset puts it there. With the
    /// 320px block <c>position: relative</c>, <c>top: 3px</c>, a float between and a paragraph: 3px
    /// down the 320px block, while the outer block begins 16px below the 10px block. In a card with
    /// <c>position: relative</c> and 10px of padding, holding an empty block with the box, with
    /// <c>top: 8px</c>, in it, a 60 × 40px float and a paragraph with <c>margin-top: 12px</c>: 8px
    /// down the card, while the card's content and the float begin 12px down its content top. The
    /// box was where it is now, and the outer block right below the 10px block or at the card's
    /// content top.
    /// </summary>
    [Theory]
    [InlineData("relative block")]
    [InlineData("card")]
    public void A_Box_Placed_By_Its_Offsets_In_A_Containing_Block_Outside_Stays(string kind)
    {
        var page = new Page();
        bool card = kind == "card";
        var containing = page.Content;

        if (card)
        {
            containing = Block(page.Content);
            containing.PaddingTop = containing.PaddingBottom = "10px";
        }

        containing.Position = CssConstants.Relative;
        var outer = Block(containing);
        var empty = Block(outer);
        var positioned = OutOfFlow(empty, "absolute");
        positioned.Top = card ? "8px" : "3px";
        CssBox floated;

        if (card)
        {
            floated = Float(outer, "60px", "40px", CssConstants.Left);
            Paragraph(outer, "12px");
        }
        else
        {
            empty.MarginBottom = "16px";
            floated = OutOfFlow(outer, "float");
            Paragraph(outer, "0");
        }

        page.Layout();

        double contentTop = card ? containing.ClientTop + 12 : page.Before.ActualBottom + 16;

        Assert.Equal(contentTop, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, floated.Location.Y, 1);
        Assert.Equal(containing.Location.Y + (card ? 8 : 3), positioned.Location.Y, 1);
    }

    /// <summary>
    /// After a 320 × 40px float, below which the float between goes, the outer block's margins move
    /// it again once the block after the empty one has joined the set: that block's first child's
    /// <c>margin-top: 30px</c>, laid out once or twice; a second empty block with
    /// <c>margin-bottom: 20px</c>, a second float and a paragraph with <c>margin-top: 30px</c>; or a
    /// second empty block and the paragraph. The outer block begins 30px below the 10px block, and
    /// the floats stay right below the big one, 50px down the 320px block, the second beside the
    /// first. The outer block began right below the 10px block; the floats were where they are now.
    /// </summary>
    [Theory]
    [InlineData("nested")]
    [InlineData("nested, laid out twice")]
    [InlineData("second join")]
    [InlineData("empty after")]
    public void A_Later_Move_Of_The_Parent_Places_The_Floats_Between_Again(string kind)
    {
        var page = new Page();
        Float(page.Content, "320px", "40px", CssConstants.Left);
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var first = OutOfFlow(outer, "float");
        CssBox? second = null;

        if (kind.StartsWith("nested", StringComparison.Ordinal))
        {
            Paragraph(Block(outer), "30px");
        }
        else
        {
            Block(outer).MarginBottom = "20px";

            if (kind == "second join")
                second = OutOfFlow(outer, "float");

            Paragraph(outer, "30px");
        }

        page.Layout();

        if (kind.EndsWith("twice", StringComparison.Ordinal))
            page.Layout();

        double top = page.Content.Location.Y;

        Assert.Equal(page.Before.ActualBottom + 30, outer.Location.Y, 1);
        Assert.Equal(top + 50, first.Location.Y, 1);

        if (second != null)
        {
            Assert.Equal(top + 50, second.Location.Y, 1);
            Assert.Equal(page.Content.Location.X + 30, second.Location.X, 1);
        }
    }

    /// <summary>
    /// An absolutely positioned or fixed box between, then a block holding a paragraph with
    /// <c>margin-top: 30px</c>, which moves the outer block again, 30px below the 10px block: an
    /// absolutely positioned box with <c>top: 5px</c> stays 5px down the root; one with auto
    /// offsets, or a fixed one, goes with the end of the set, to the outer block's top. The outer
    /// block began right below the 10px block, and the box 5px down the root or 16px below the 10px
    /// block.
    /// </summary>
    [Theory]
    [InlineData("offset")]
    [InlineData("static")]
    [InlineData("fixed")]
    public void A_Later_Move_Of_The_Parent_Places_A_Box_Between_As_Its_Offsets_Say(string kind)
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var positioned = OutOfFlow(outer, kind == "fixed" ? "fixed" : "absolute");

        if (kind == "offset")
            positioned.Top = "5px";

        Paragraph(Block(outer), "30px");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 30, outer.Location.Y, 1);
        Assert.Equal(kind == "offset" ? page.Root.Location.Y + 5 : outer.Location.Y, positioned.Location.Y, 1);
    }

    /// <summary>
    /// After a 120 × 80px right float, standing for an infobox, the outer block's first child is
    /// empty, then come a 100 × 40px right float with <c>clear: right</c>, standing for a thumbnail,
    /// and a block holding a paragraph with 8px margins: the outer block begins 8px below the 10px
    /// block, and the thumbnail below the infobox, 80px below the 10px block, at the right edge,
    /// 220px across. The outer block began right below the 10px block, and the thumbnail 100px
    /// across, where it would have gone beside the infobox.
    /// </summary>
    [Fact]
    public void A_Thumbnail_Below_An_Infobox_Stays_Below_It()
    {
        var page = new Page();
        Float(page.Content, "120px", "80px", CssConstants.Right);
        var outer = Block(page.Content);
        Block(outer);
        var thumbnail = Float(outer, "100px", "40px", CssConstants.Right);
        thumbnail.Clear = CssConstants.Right;
        var paragraph = Paragraph(Block(outer), "8px");
        paragraph.MarginBottom = "8px";
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 8, outer.Location.Y, 1);
        Assert.Equal(page.Before.ActualBottom + 80, thumbnail.Location.Y, 1);
        Assert.Equal(page.Content.Location.X + 220, thumbnail.Location.X, 1);
    }

    /// <summary>
    /// A float before the empty first child and one between, with the outer block 16px below the
    /// 10px block. After a 320 × 40px float both stay right below it, 50px down the 320px block, the
    /// second beside the first. After a 100 × 12px float, which ends above the outer block's new top,
    /// the first goes to the left at the outer block's top, and the second beside it, 30px across.
    /// The outer block began right below the 10px block; after the big float the floats were where
    /// they are now, and after the small float the first float was beside it, 100px across at the
    /// outer block's top, and the second 16px down, at the left.
    /// </summary>
    [Theory]
    [InlineData("320px", "40px")]
    [InlineData("100px", "12px")]
    public void A_Float_Before_The_Empty_Child_Is_Placed_Again(string width, string height)
    {
        var page = new Page();
        Float(page.Content, width, height, CssConstants.Left);
        var outer = Block(page.Content);
        var first = OutOfFlow(outer, "float");
        Block(outer).MarginBottom = "16px";
        var second = OutOfFlow(outer, "float");
        Paragraph(outer, "0");
        page.Layout();

        double left = page.Content.Location.X;
        double top = width == "320px" ? page.Content.Location.Y + 50 : page.Before.ActualBottom + 16;

        Assert.Equal(page.Before.ActualBottom + 16, outer.Location.Y, 1);
        Assert.Equal(top, first.Location.Y, 1);
        Assert.Equal(left, first.Location.X, 1);
        Assert.Equal(top, second.Location.Y, 1);
        Assert.Equal(left + 30, second.Location.X, 1);
    }

    /// <summary>
    /// A first child's <c>margin-top: 30px</c> moves the outer block 30px below the 10px block, with
    /// nothing empty before it, and an out-of-flow box before it stays where it belongs: one with
    /// <c>top: 5px</c> 5px down the root; after a 320 × 40px float, a float right below that, 50px
    /// down the 320px block, with the paragraph or a block holding it after the float. The box was
    /// 35px down the root, and the float 80px down, 30px below the big float.
    /// </summary>
    [Theory]
    [InlineData("absolute")]
    [InlineData("float")]
    [InlineData("float, nested")]
    public void A_First_Child_Margin_Leaves_An_Out_Of_Flow_Box_Before_It_In_Place(string kind)
    {
        var page = new Page();
        bool absolute = kind == "absolute";

        if (!absolute)
            Float(page.Content, "320px", "40px", CssConstants.Left);

        var outer = Block(page.Content);
        var box = OutOfFlow(outer, absolute ? "absolute" : "float");

        if (absolute)
            box.Top = "5px";

        Paragraph(kind == "float, nested" ? Block(outer) : outer, "30px");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 30, outer.Location.Y, 1);
        Assert.Equal(absolute ? page.Root.Location.Y + 5 : page.Content.Location.Y + 50, box.Location.Y, 1);
    }

    /// <summary>
    /// A float before the empty first child, and one between with <c>clear: left</c> and
    /// <c>margin-top: 5px</c>: the second float's top margin edge goes below the first, so it is 5px
    /// below it, 17px down the outer block, which begins 16px below the 10px block. The outer block
    /// began right below the 10px block, with the second float 16px down it, 4px below the first.
    /// </summary>
    [Fact]
    public void A_Float_Between_With_Clear_Keeps_Its_Top_Margin_Below_The_Float_It_Clears()
    {
        var page = new Page();
        var outer = Block(page.Content);
        OutOfFlow(outer, "float");
        Block(outer).MarginBottom = "16px";
        var second = OutOfFlow(outer, "float");
        second.Clear = CssConstants.Left;
        second.MarginTop = "5px";
        Paragraph(outer, "0");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 16, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y + 17, second.Location.Y, 1);
    }

    /// <summary>
    /// A float between with <c>float: inline-start</c>, <c>position: relative</c> and <c>left:
    /// 3px</c> keeps its offset once: 3px across, at the top of the outer block, which begins 16px
    /// below the 10px block. The outer block began right below the 10px block, with the float 3px
    /// across and 16px down it.
    /// </summary>
    [Fact]
    public void A_Relatively_Positioned_Float_Between_Keeps_Its_Offset_Once()
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var floated = OutOfFlow(outer, "float");
        floated.Float = "inline-start";
        floated.Position = CssConstants.Relative;
        floated.Left = "3px";
        Paragraph(outer, "0");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 16, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, floated.Location.Y, 1);
        Assert.Equal(page.Content.Location.X + 3, floated.Location.X, 1);
    }

    /// <summary>
    /// A float in an empty block before the empty first child, or in the empty first child itself,
    /// is one the set places too: a paragraph with <c>clear: left</c> and <c>margin-top: 20px</c>
    /// after an absolutely positioned box has clearance past it, so the outer block, the float and
    /// the box begin 16px below the 10px block, and the paragraph below the float, 12px lower. The
    /// outer block began right below the 10px block with the float at its top, the box 16px and the
    /// paragraph 20px down it.
    /// </summary>
    [Theory]
    [InlineData("before")]
    [InlineData("inside")]
    public void A_Float_In_An_Empty_Block_Is_One_The_Set_Places(string where)
    {
        var page = new Page();
        var outer = Block(page.Content);
        var holder = Block(outer);
        CssBox floated;

        if (where == "before")
        {
            floated = OutOfFlow(holder, "float");
            Block(outer).MarginBottom = "16px";
        }
        else
        {
            holder.MarginBottom = "16px";
            floated = OutOfFlow(holder, "float");
        }

        var box = OutOfFlow(outer, "absolute");
        var paragraph = Paragraph(outer, "20px");
        paragraph.Clear = CssConstants.Left;
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 16, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, floated.Location.Y, 1);
        Assert.Equal(outer.Location.Y, box.Location.Y, 1);
        Assert.Equal(outer.Location.Y + 12, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// After a 320 × 40px float, the outer block's first child is an empty block holding an empty
    /// block and a float, then comes a paragraph with <c>margin-top: 30px</c>, which moves the outer
    /// block 30px below the 10px block. The float is one the set places, and it is placed again,
    /// right below the big float, 50px down the 320px block. It went down with the outer block, 80px
    /// down, 30px below the big float.
    /// </summary>
    [Fact]
    public void A_Float_In_An_Empty_Block_Is_Placed_Again()
    {
        var page = new Page();
        Float(page.Content, "320px", "40px", CssConstants.Left);
        var outer = Block(page.Content);
        var holder = Block(outer);
        Block(holder);
        var floated = OutOfFlow(holder, "float");
        Paragraph(outer, "30px");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 30, outer.Location.Y, 1);
        Assert.Equal(page.Content.Location.Y + 50, floated.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: after a 100 × 50px float, which reaches into the
    /// outer block, the outer block's first child holds nothing but a float, which it places on a
    /// line of its own, and a paragraph with <c>margin-top: 30px</c> moves the outer block 30px below
    /// the 10px block. The float goes down with its line, beside the big float, 100px across at the
    /// outer block's top.
    /// </summary>
    [Fact]
    public void Control_A_Float_On_A_Line_Goes_Down_With_It()
    {
        var page = new Page();
        Float(page.Content, "100px", "50px", CssConstants.Left);
        var outer = Block(page.Content);
        var floated = OutOfFlow(Block(outer), "float");
        Paragraph(outer, "30px");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 30, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, floated.Location.Y, 1);
        Assert.Equal(page.Content.Location.X + 100, floated.Location.X, 1);
    }

    /// <summary>
    /// Control, which passes before and after: the same after a 100 × 20px float, which the
    /// paragraph's margin takes the outer block past. The float goes down with its line, to the
    /// outer block's top, 30px below the 10px block. It stays 100px across, where browsers put it
    /// back at the left edge: the top its line had when it was placed, from which the float rules
    /// place it, does not move with the line (a separate issue), and placed again from there, it
    /// would go back up to the 10px block's bottom.
    /// </summary>
    [Fact]
    public void Control_A_Float_On_A_Line_Goes_Down_With_It_Past_A_Float_Outside()
    {
        var page = new Page();
        Float(page.Content, "100px", "20px", CssConstants.Left);
        var outer = Block(page.Content);
        var floated = OutOfFlow(Block(outer), "float");
        Paragraph(outer, "30px");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 30, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, floated.Location.Y, 1);
    }

    /// <summary>
    /// An absolutely positioned box between with <c>margin-top: 20px</c> or <c>-10px</c> keeps its
    /// own margin, which does not collapse with the set (CSS2.1 §8.3.1): it is 20px down the outer
    /// block, which begins 16px below the 10px block, or 10px above its top. The outer block began
    /// right below the 10px block, with the box 20px or 6px down it, where its margin had collapsed
    /// with the set.
    /// </summary>
    [Theory]
    [InlineData("20px", 20)]
    [InlineData("-10px", -10)]
    public void An_Absolutely_Positioned_Box_Between_Keeps_Its_Own_Top_Margin(string marginTop, double margin)
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var positioned = OutOfFlow(outer, "absolute");
        positioned.MarginTop = marginTop;
        Paragraph(outer, "0");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 16, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y + margin, positioned.Location.Y, 1);
    }

    /// <summary>
    /// Controls, which pass before and after, each a box that moves with the outer block: an
    /// absolutely positioned box with <c>top: 5px</c> in the empty first child, which is
    /// <c>position: relative</c>, 5px down it at the top of the outer block, which begins 16px below
    /// the 10px block; a float, or an absolutely positioned box with auto offsets, before a
    /// paragraph with <c>margin-top: 20px</c>, at the top of the outer block, 20px below the 10px
    /// block.
    /// </summary>
    [Theory]
    [InlineData("relative")]
    [InlineData("float")]
    [InlineData("absolute")]
    public void Control_Boxes_That_Move_With_The_Parent(string kind)
    {
        var page = new Page();
        var outer = Block(page.Content);
        CssBox box;

        if (kind == "relative")
        {
            var empty = Block(outer);
            empty.MarginBottom = "16px";
            empty.Position = CssConstants.Relative;
            box = OutOfFlow(empty, "absolute");
            box.Top = "5px";
            Paragraph(outer, "0");
        }
        else
        {
            box = OutOfFlow(outer, kind);
            Paragraph(outer, "20px");
        }

        page.Layout();

        bool relative = kind == "relative";

        Assert.Equal(page.Before.ActualBottom + (relative ? 16 : 20), outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y + (relative ? 5 : 0), box.Location.Y, 1);
    }

    /// <summary>
    /// An empty first child and a float, then a block that establishes a formatting context, as
    /// wide as the 320px block and so with no room beside the float: a flow root or a block with
    /// <c>overflow: hidden</c> holding a word, with <c>margin-top: 20px</c>, <c>50px</c> or
    /// <c>-6px</c>; a table with <c>width: 100%</c>; or a flow root in a block. CSS2.1 §9.5 puts it
    /// below the float, and its margin leaves the set, as a margin with clearance does: the outer
    /// block and the float begin 16px below the 10px block, and the block 12px lower, below the
    /// float, the outer block 28px tall. The outer block began right below the 10px block, 44px
    /// tall, with the float and the block where they are now; with the 50px margin, 66px tall, with
    /// the block 50px below the 10px block.
    /// </summary>
    [Theory]
    [InlineData("flow-root", "20px")]
    [InlineData("hidden", "20px")]
    [InlineData("hidden", "50px")]
    [InlineData("hidden", "-6px")]
    [InlineData("table", "20px")]
    [InlineData("nested", "20px")]
    public void A_Formatting_Context_Root_The_Float_Pushes_Down_Leaves_Its_Margin_Out(string kind, string marginTop)
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var floated = OutOfFlow(outer, "float");
        var root = FormattingContextRoot(kind == "nested" ? Block(outer) : outer, kind, "320px", marginTop);
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 16, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, floated.Location.Y, 1);
        Assert.Equal(outer.Location.Y + 12, root.Location.Y, 1);
        Assert.Equal(28, outer.Size.Height, 1);
    }

    /// <summary>
    /// A 30 × 40px float and then a 300px block with <c>overflow: hidden</c> and <c>margin-top:
    /// 20px</c>, the outer block's first child in the flow, laid out once or twice: the block has
    /// no room beside the float, so its margin leaves the set above the outer block, which begins
    /// right below the 10px block with the float at its top, and the block below the float, 40px
    /// lower. The outer block and the float began 20px below the 10px block, and the block 60px
    /// below it.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_First_Formatting_Context_Root_Below_A_Float_Before_It_Leaves_Its_Margin_Out(bool twice)
    {
        var page = new Page();
        var outer = Block(page.Content);
        var floated = Float(outer, "30px", "40px", CssConstants.Left);
        var root = FormattingContextRoot(outer, "hidden", "300px", "20px");
        page.Layout();

        if (twice)
            page.Layout();

        Assert.Equal(page.Before.ActualBottom, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, floated.Location.Y, 1);
        Assert.Equal(outer.Location.Y + 40, root.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: an empty first child with no margins, a 30 × 40px
    /// float and a 300px block with <c>overflow: hidden</c> and <c>margin-top: 20px</c>, laid out
    /// once or twice. The block has no room beside the float, so its margin leaves the set, which
    /// comes to nothing: the outer block and the float begin right below the 10px block, and the
    /// block below the float, 40px lower.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Control_A_Formatting_Context_Root_Below_A_Float_After_An_Empty_Block_Without_Margins(bool twice)
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer);
        var floated = Float(outer, "30px", "40px", CssConstants.Left);
        var root = FormattingContextRoot(outer, "hidden", "300px", "20px");
        page.Layout();

        if (twice)
            page.Layout();

        Assert.Equal(page.Before.ActualBottom, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, floated.Location.Y, 1);
        Assert.Equal(outer.Location.Y + 40, root.Location.Y, 1);
    }

    /// <summary>
    /// After a 100 × 40px float, which leaves 220px of room beside it, a 300px flow root with
    /// <c>margin-top: 30px</c> goes below it, 40px below the 10px block, and its margin leaves the
    /// set: after an empty first child and an absolutely positioned box, or after the empty first
    /// child alone, the outer block begins 16px below the 10px block, with the box at its top; as
    /// the first child, right below the 10px block. The outer block began right below the 10px
    /// block, with the box 16px below it, or 30px below the 10px block.
    /// </summary>
    [Theory]
    [InlineData("absolute")]
    [InlineData("nothing between")]
    [InlineData("first child")]
    public void A_Formatting_Context_Root_A_Float_Outside_Pushes_Down_Leaves_Its_Margin_Out(string kind)
    {
        var page = new Page();
        Float(page.Content, "100px", "40px", CssConstants.Left);
        var outer = Block(page.Content);
        CssBox? positioned = null;

        if (kind != "first child")
            Block(outer).MarginBottom = "16px";

        if (kind == "absolute")
            positioned = OutOfFlow(outer, "absolute");

        var root = FormattingContextRoot(outer, "flow-root", "300px", "30px");
        page.Layout();

        double set = kind == "first child" ? 0 : 16;

        Assert.Equal(page.Before.ActualBottom + set, outer.Location.Y, 1);
        Assert.Equal(page.Before.ActualBottom + 40, root.Location.Y, 1);

        if (positioned != null)
            Assert.Equal(outer.Location.Y, positioned.Location.Y, 1);
    }

    /// <summary>
    /// A flow root with <c>margin-top: 20px</c> that has room beside the float, 200px wide, or
    /// after an absolutely positioned box, 320px wide: its margin joins the set, and the outer
    /// block, the float or the box and the flow root begin 20px below the 10px block. The outer
    /// block began right below the 10px block, with the float or the box 16px and the flow root 20px
    /// below it.
    /// </summary>
    [Theory]
    [InlineData("float")]
    [InlineData("absolute")]
    public void A_Formatting_Context_Root_That_Fits_Beside_The_Floats_Joins_The_Set(string kind)
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        var box = OutOfFlow(outer, kind);
        var root = FormattingContextRoot(outer, "flow-root", kind == "float" ? "200px" : "320px", "20px");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 20, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, box.Location.Y, 1);
        Assert.Equal(outer.Location.Y, root.Location.Y, 1);
    }

    /// <summary>
    /// Controls, which pass before and after, each a flow root whose margin stays where it was:
    /// with <c>padding-top: 1px</c> on the outer block, after an empty first child and a float, a
    /// 320px flow root with <c>margin-top: 20px</c> is 29px down the outer block, below the float,
    /// which is 17px down it, and the outer block right below the 10px block. After a 100 × 40px
    /// left float and a 250 × 40px right float, which has no room beside it and goes below it, a
    /// 150px flow root with <c>clear: left</c> and <c>margin-top: 30px</c> goes below the right
    /// float, 80px below the 10px block; placed from where the set ends without its margin, it
    /// would go beside the left float, which it clears.
    /// </summary>
    [Theory]
    [InlineData("padding")]
    [InlineData("clear")]
    public void Control_A_Formatting_Context_Root_Whose_Margin_Stays_Where_It_Is(string kind)
    {
        var page = new Page();

        if (kind == "padding")
        {
            var outer = Block(page.Content);
            outer.PaddingTop = "1px";
            Block(outer).MarginBottom = "16px";
            var floated = OutOfFlow(outer, "float");
            var root = FormattingContextRoot(outer, "flow-root", "320px", "20px");
            page.Layout();

            Assert.Equal(page.Before.ActualBottom, outer.Location.Y, 1);
            Assert.Equal(outer.Location.Y + 17, floated.Location.Y, 1);
            Assert.Equal(outer.Location.Y + 29, root.Location.Y, 1);
        }
        else
        {
            Float(page.Content, "100px", "40px", CssConstants.Left);
            Float(page.Content, "250px", "40px", CssConstants.Right);
            var outer = Block(page.Content);
            var root = FormattingContextRoot(outer, "flow-root", "150px", "30px");
            root.Clear = CssConstants.Left;
            page.Layout();

            Assert.Equal(page.Before.ActualBottom + 80, root.Location.Y, 1);
        }
    }

    /// <summary>
    /// After a 100 × 50px float, a block with <c>position: relative</c> and <c>top: 20px</c> holds
    /// an empty block and a float, and an absolutely positioned box and a paragraph with
    /// <c>margin-top: 30px</c> follow it: the outer block begins 30px below the 10px block, and the
    /// float is 20px down it, 100px across, beside the big float. The outer block began right below
    /// the 10px block, with the float 20px down it.
    /// </summary>
    [Fact]
    public void A_Float_In_A_Shifted_Box_Goes_With_The_Run_Across_A_Box_Between()
    {
        var (page, outer, floated) = LayOutFloatInShiftedBox("20px", null, between: true);

        Assert.Equal(page.Before.ActualBottom + 30, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y + 20, floated.Location.Y, 1);
        Assert.Equal(page.Content.Location.X + 100, floated.Location.X, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: after a 100 × 50px float, a block with <c>position:
    /// relative</c> and <c>top: 20px</c> or <c>left: 50px</c> holds an empty block and a float,
    /// and a paragraph with <c>margin-top: 30px</c> after it moves the outer block 30px below the
    /// 10px block. The float goes down with the outer block, beside the big float, 100px across and
    /// 20px down the outer block, or 150px across at its top.
    /// </summary>
    [Theory]
    [InlineData("20px", null)]
    [InlineData(null, "50px")]
    public void Control_A_Float_In_A_Shifted_Box_Goes_With_The_Run(string? top, string? left)
    {
        var (page, outer, floated) = LayOutFloatInShiftedBox(top, left, between: false);

        Assert.Equal(page.Before.ActualBottom + 30, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y + (top == null ? 0 : 20), floated.Location.Y, 1);
        Assert.Equal(page.Content.Location.X + (left == null ? 100 : 150), floated.Location.X, 1);
    }

    /// <summary>
    /// After a 100 × 50px float, the outer block's first child is a block with <c>position:
    /// relative</c> and the given <paramref name="top"/> or <paramref name="left"/>, holding an empty
    /// block and a float; then, if <paramref name="between"/>, an absolutely positioned box; then a
    /// paragraph with <c>margin-top: 30px</c>.
    /// </summary>
    private static (Page Page, CssBox Outer, CssBox Floated) LayOutFloatInShiftedBox(string? top, string? left,
        bool between)
    {
        var page = new Page();
        Float(page.Content, "100px", "50px", CssConstants.Left);
        var outer = Block(page.Content);
        var shifted = Block(outer);
        shifted.Position = CssConstants.Relative;
        shifted.Top = top ?? CssConstants.Auto;
        shifted.Left = left ?? CssConstants.Auto;
        Block(shifted);
        var floated = OutOfFlow(shifted, "float");

        if (between)
            OutOfFlow(outer, "absolute");

        Paragraph(outer, "30px");
        page.Layout();

        return (page, outer, floated);
    }

    /// <summary>
    /// The outer block's first child holds an empty block with <c>margin-bottom: 10px</c> and a
    /// float, and a paragraph follows it: the set comes to 10px, and the outer block begins 10px
    /// below the 10px block with the float at its top; 5px down it with <c>margin-top: 5px</c> on
    /// the float; a second float beside the first, 30px across; or, with <c>margin-top: 30px</c> on
    /// the paragraph, 30px below the 10px block with the float at its top. The float, and the
    /// second one, went 10px down the outer block, where the part of the set that the empty block
    /// handed on had put them.
    /// </summary>
    [Theory]
    [InlineData("one")]
    [InlineData("margin")]
    [InlineData("two")]
    [InlineData("paragraph margin")]
    public void A_Float_After_An_Empty_Block_In_An_Empty_Block_Goes_Where_The_Set_Ends(string kind)
    {
        var page = new Page();
        var outer = Block(page.Content);
        var holder = Block(outer);
        Block(holder).MarginBottom = "10px";
        var first = OutOfFlow(holder, "float");
        var second = kind == "two" ? OutOfFlow(holder, "float") : null;

        if (kind == "margin")
            first.MarginTop = "5px";

        Paragraph(outer, kind == "paragraph margin" ? "30px" : "0");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + (kind == "paragraph margin" ? 30 : 10), outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y + (kind == "margin" ? 5 : 0), first.Location.Y, 1);

        if (second != null)
        {
            Assert.Equal(outer.Location.Y, second.Location.Y, 1);
            Assert.Equal(page.Content.Location.X + 30, second.Location.X, 1);
        }
    }

    /// <summary>
    /// After a 100 × 5px float with <c>margin-bottom: 30px</c> and a second 10px block, the outer
    /// block's first child is a float with <c>clear: left</c>, and a paragraph with <c>margin-top:
    /// 30px</c> follows: the outer block begins 30px below the second block, 40px below the first,
    /// and the float at its top, below the big float's margin, which ends 35px below the first
    /// block. Placed right below that margin first, the float went 30px further down with the outer
    /// block, 25px down it.
    /// </summary>
    [Fact]
    public void A_Float_With_Clear_Is_Placed_Again_Below_The_Margin_Of_A_Float_Outside()
    {
        var (page, outer, floated) = LayOutFloatBelowMargin(CssConstants.Left);

        Assert.Equal(page.Before.ActualBottom + 40, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, floated.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: the same float without <c>clear</c>, which the
    /// margin does not hold, is at the outer block's top, at the left edge.
    /// </summary>
    [Fact]
    public void Control_A_Float_Without_Clear_Goes_With_The_Run_Past_The_Margin_Of_A_Float_Outside()
    {
        var (page, outer, floated) = LayOutFloatBelowMargin(CssConstants.None);

        Assert.Equal(page.Before.ActualBottom + 40, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, floated.Location.Y, 1);
        Assert.Equal(page.Content.Location.X, floated.Location.X, 1);
    }

    /// <summary>
    /// After a 100 × 5px float with <c>margin-bottom: 30px</c> and a second 10px block, the outer
    /// block's first child is a float with the given <paramref name="clear"/>, then comes a
    /// paragraph with <c>margin-top: 30px</c>.
    /// </summary>
    private static (Page Page, CssBox Outer, CssBox Floated) LayOutFloatBelowMargin(string clear)
    {
        var page = new Page();
        Float(page.Content, "100px", "5px", CssConstants.Left).MarginBottom = "30px";
        new CssBox(page.Content, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Height = "10px" };
        var outer = Block(page.Content);
        var floated = OutOfFlow(outer, "float");
        floated.Clear = clear;
        Paragraph(outer, "30px");
        page.Layout();

        return (page, outer, floated);
    }

    /// <summary>
    /// After a 100 × 20px float, the outer block's first child is a float, or two, and a paragraph
    /// with <c>margin-top: 30px</c> moves the outer block 30px below the 10px block, past the big
    /// float, which ends 20px below it: the floats go back to the left edge at the outer block's
    /// top, the second 30px across. Placed beside the big float first, they went down with the outer
    /// block, 100px and 130px across.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void A_Float_That_The_Move_Takes_Past_A_Float_Outside_Goes_Back_Across(int count)
    {
        var page = new Page();
        Float(page.Content, "100px", "20px", CssConstants.Left);
        var outer = Block(page.Content);
        var first = OutOfFlow(outer, "float");
        var second = count == 2 ? OutOfFlow(outer, "float") : null;
        Paragraph(outer, "30px");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 30, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, first.Location.Y, 1);
        Assert.Equal(page.Content.Location.X, first.Location.X, 1);

        if (second != null)
        {
            Assert.Equal(outer.Location.Y, second.Location.Y, 1);
            Assert.Equal(page.Content.Location.X + 30, second.Location.X, 1);
        }
    }

    /// <summary>
    /// After a 100 × 20px float and a 30px block, the outer block's first child is a float, and a
    /// paragraph with <c>margin-top: -20px</c> moves the outer block 20px up, 10px below the 10px
    /// block, beside the big float, which reaches 20px below it: the float goes beside the big one,
    /// 100px across at the outer block's top. Placed below the big float first, at the left edge, it
    /// went up with the outer block and stayed at the left edge, over the big float.
    /// </summary>
    [Fact]
    public void A_Float_That_A_Negative_Margin_Takes_Up_Beside_A_Float_Outside_Goes_Aside()
    {
        var page = new Page();
        Float(page.Content, "100px", "20px", CssConstants.Left);
        new CssBox(page.Content, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Height = "30px" };
        var outer = Block(page.Content);
        var floated = OutOfFlow(outer, "float");
        Paragraph(outer, "-20px");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 10, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, floated.Location.Y, 1);
        Assert.Equal(page.Content.Location.X + 100, floated.Location.X, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: after a 100 × 200px float on the left or the right,
    /// which reaches far below the outer block, the outer block's first child is a float, and a
    /// paragraph with <c>margin-top: 30px</c> moves the outer block 30px below the 10px block. The
    /// float goes down with it, still beside the big float: 100px across, or at the left edge.
    /// </summary>
    [Theory]
    [InlineData("left")]
    [InlineData("right")]
    public void Control_A_Float_Beside_A_Tall_Float_Outside_Stays_Beside_It(string side)
    {
        bool left = side == "left";
        var page = new Page();
        Float(page.Content, "100px", "200px", left ? CssConstants.Left : CssConstants.Right);
        var outer = Block(page.Content);
        var floated = OutOfFlow(outer, "float");
        Paragraph(outer, "30px");
        page.Layout();

        Assert.Equal(page.Before.ActualBottom + 30, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, floated.Location.Y, 1);
        Assert.Equal(page.Content.Location.X + (left ? 100 : 0), floated.Location.X, 1);
    }

    /// <summary>
    /// The outer block's first child is an absolutely positioned 30 × 12px box with <c>bottom:
    /// 5px</c> in a containing block whose height is known, the 320px block with <c>position:
    /// relative</c> and <c>height: 100px</c> or, with nothing positioned, the 1024 × 768px root, and
    /// a paragraph with <c>margin-top: 30px</c> moves the outer block 30px down. The box stays where
    /// its offset places it, 83px down the 320px block or 751px down the root. It went 30px down
    /// with the outer block, 113px and 781px down.
    /// </summary>
    [Theory]
    [InlineData("100px")]
    [InlineData(null)]
    public void A_Box_Placed_By_Its_Bottom_Stays_In_A_Containing_Block_Of_A_Set_Height(string? height)
    {
        Assert.Equal(height == null ? 751 : 83, PlaceByOffset("absolute", "bottom", height, "30px"), 1);
    }

    /// <summary>
    /// Controls, which pass before and after: with the 320px block <c>position: relative</c> and
    /// no height, or <c>height: 50%</c>, which comes to <c>auto</c> in the block around it (CSS2.1
    /// §10.5), the outer block's first child an absolutely positioned 30 × 12px box with
    /// <c>bottom: 5px</c> or <c>top: 50%</c>, and a paragraph with <c>margin-top: 30px</c> after it,
    /// which moves the outer block 30px down, the box is 30px further down the 320px block than with
    /// no margin on the paragraph. The engine places it against the height the 320px block has so
    /// far (a separate issue), which grows as what the block holds goes down; browsers put it 30px
    /// lower too with <c>bottom: 5px</c>, and 15px lower with <c>top: 50%</c>.
    /// </summary>
    [Theory]
    [InlineData("bottom", "auto")]
    [InlineData("top", "auto")]
    [InlineData("bottom", "50%")]
    public void Control_A_Box_Placed_Against_The_Height_Of_Its_Containing_Block_Goes_Down_With_What_Sizes_It(
        string offset, string height)
    {
        double moved = PlaceByOffset("absolute", offset, height, "30px");
        double unmoved = PlaceByOffset("absolute", offset, height, "0");

        Assert.Equal(30, moved - unmoved, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a fixed 30 × 12px box with <c>bottom: 5px</c> in
    /// the same place stays at the bottom of the 1024 × 768px viewport, 751px down, while the
    /// paragraph's margin moves the outer block.
    /// </summary>
    [Fact]
    public void Control_A_Fixed_Box_Placed_By_Its_Bottom_Stays_At_The_Bottom_Of_The_Viewport()
    {
        Assert.Equal(751, PlaceByOffset("fixed", "bottom", "auto", "30px"), 1);
    }

    /// <summary>
    /// How far down the 320px block a 30 × 12px box, absolutely positioned or fixed as <paramref
    /// name="kind"/> says, goes, the outer block's first child, before a paragraph with the given
    /// <paramref name="marginTop"/>: placed by <c>bottom: 5px</c> or, as <paramref name="offset"/>
    /// says, by <c>top: 50%</c>, with the 320px block <c>position: relative</c> with the given
    /// <paramref name="height"/>, or not positioned when that is <c>null</c>.
    /// </summary>
    private static double PlaceByOffset(string kind, string offset, string? height, string marginTop)
    {
        var page = new Page();

        if (height != null)
        {
            page.Content.Position = CssConstants.Relative;
            page.Content.Height = height;
        }

        var outer = Block(page.Content);
        var positioned = OutOfFlow(outer, kind);

        if (offset == "bottom")
            positioned.Bottom = "5px";
        else
            positioned.Top = "50%";

        Paragraph(outer, marginTop);
        page.Layout();

        return positioned.Location.Y - page.Content.Location.Y;
    }

    /// <summary>
    /// A block that establishes a formatting context, <paramref name="width"/> wide with the given
    /// top margin and no bottom margin, in <paramref name="parent"/>: a <c>flow-root</c> or a block
    /// with <c>overflow: hidden</c>, holding a word; a table with <c>width: 100%</c> holding one cell
    /// with a word; or, <c>"nested"</c>, a <c>flow-root</c>.
    /// </summary>
    private static CssBox FormattingContextRoot(CssBox parent, string kind, string width, string marginTop)
    {
        CssBox box;

        if (kind == "table")
        {
            box = new CssBox(parent, new HtmlTag("table", false, null), BaseUrl)
            {
                Display = "table",
                Width = "100%",
            };

            var body = new CssBox(box, new HtmlTag("tbody", false, null), BaseUrl) { Display = "table-row-group" };
            var row = new CssBox(body, new HtmlTag("tr", false, null), BaseUrl) { Display = "table-row" };
            parent = new CssBox(row, new HtmlTag("td", false, null), BaseUrl) { Display = "table-cell" };
        }
        else
        {
            box = new CssBox(parent, new HtmlTag("div", false, null), BaseUrl)
            {
                Display = "flow-root",
                Width = width,
            };

            if (kind == "hidden")
            {
                box.Display = "block";
                box.Overflow = "hidden";
            }

            parent = box;
        }

        var text = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
        text.ParseToWords();

        box.MarginTop = marginTop;
        box.MarginBottom = "0";
        return box;
    }

    /// <summary>
    /// The root, the block in it, the 320px block in that and the 10px block in that.
    /// </summary>
    private sealed class Page
    {
        public Page()
        {
            Root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
            {
                Display = "block",
                Location = new PointF(0, 0),
                Size = new SizeF(1024, 768),
                LayoutEnvironment = new FakeLayoutEnvironment(),
            };

            Body = new CssBox(Root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
            Content = new CssBox(Body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "320px" };
            Before = new CssBox(Content, new HtmlTag("b", false, null), BaseUrl) { Display = "block", Height = "10px" };
        }

        public CssBox Root { get; }

        public CssBox Body { get; }

        public CssBox Content { get; }

        public CssBox Before { get; }

        public void Layout()
        {
            FlexGridItemBlockification.Generate(Root);
            Root.PerformLayout(Root.LayoutEnvironment);
        }
    }

    private static CssBox Block(CssBox parent, string marginTop = "0") =>
        new(parent, new HtmlTag("div", false, null), BaseUrl) { Display = "block", MarginTop = marginTop };

    /// <summary>
    /// A float of the given size in <paramref name="parent"/>, on the given side.
    /// </summary>
    private static CssBox Float(CssBox parent, string width, string height, string side) =>
        new(parent, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Width = width,
            Height = height,
            Float = side,
        };

    /// <summary>
    /// A 30 × 12px box in <paramref name="parent"/>: a left float, or an absolutely positioned or
    /// fixed box with auto offsets.
    /// </summary>
    private static CssBox OutOfFlow(CssBox parent, string kind)
    {
        var box = new CssBox(parent, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Width = "30px",
            Height = "12px",
        };

        if (kind == "float")
            box.Float = CssConstants.Left;
        else
            box.Position = kind;

        return box;
    }

    /// <summary>
    /// A paragraph with the given top margin and no bottom margin, holding a word.
    /// </summary>
    private static CssBox Paragraph(CssBox parent, string marginTop)
    {
        var paragraph = new CssBox(parent, new HtmlTag("p", false, null), BaseUrl)
        {
            Display = "block",
            MarginTop = marginTop,
            MarginBottom = "0",
        };

        var text = new CssBox(paragraph, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
        text.ParseToWords();
        return paragraph;
    }

    private sealed class FakeLayoutEnvironment : ILayoutEnvironment
    {
        private static readonly Broiler.Graphics.Text.ILayoutFont TheFont = new FakeFont();
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => TheFont;
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => new(8, 16);
        public void MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = text.Length; charFitWidth = 8; }
        public double GetWhitespaceWidth(Broiler.Graphics.Text.ILayoutFont font) => 4;
        public ImageIntrinsics GetImageIntrinsics(object imageHandle) => default;
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
        public ILayoutImageLoader CreateImageLoader(Action<object?, RectangleF, bool> onComplete) => null!;
        public string FormatListMarker(int number, string style) => string.Empty;
    }

    private sealed class FakeFont : Broiler.Graphics.Text.ILayoutFont
    {
        public double Size => 16;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}

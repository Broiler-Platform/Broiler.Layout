using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A block's auto height ends at its last in-flow child, not at its lowest one (CSS2.1 §10.6.3).
/// </summary>
/// <remarks>
/// <para>
/// The height ends at the bottom edge of the last in-flow child's bottom margin, or, where that
/// margin collapses through the block, at the bottom border edge of the last in-flow child whose
/// top margin does not collapse with the block's bottom margin. The engine took the lowest bottom
/// among the children, so a last child that a negative margin pulls up over the one before it
/// left the block as tall as that one, and an empty last child's margins ended up inside it.
/// </para>
/// <para>
/// Each block here follows a 10px block in a wrapper, in a body in a root, and is followed by a
/// 10px block. Its children are 100px wide.
/// </para>
/// </remarks>
public sealed class LastInFlowChildHeightTests
{
    private static readonly Uri BaseUrl = new("file:///last-in-flow-child-height.html");

    /// <summary>
    /// After a 30px child, a 10px one with <c>margin-top: -25px</c> ends the block 15px down, in a
    /// plain block and in one that establishes a formatting context. It ended 30px down.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData(null)]
    [InlineData("hidden")]
    public void A_Raised_Last_Child_Ends_The_Block(string? overflow)
    {
        var (block, after) = Lay(b => { if (overflow != null) b.Overflow = overflow; },
            c => c.Height = "30px",
            c => { c.Height = "10px"; c.MarginTop = "-25px"; });

        Assert.Equal(15, block.Size.Height, 2);
        Assert.Equal(block.ActualBottom, after.Location.Y, 2);
    }

    /// <summary>
    /// The same in an inline-block, laid out on a line of the block it is in: it ends 15px below
    /// its top. It ended 30px below.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Raised_Last_Child_Ends_An_Inline_Block()
    {
        var root = Root();
        var line = Block(Block(root));
        var inlineBlock = new CssBox(line, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = "100px",
        };
        Block(inlineBlock).Height = "30px";
        var raised = Block(inlineBlock);
        raised.Height = "10px";
        raised.MarginTop = "-25px";

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(15, inlineBlock.Size.Height, 2);
    }

    /// <summary>
    /// With 1px of bottom padding, the raised child's 10px bottom margin stays inside: the block is
    /// 26px tall. It was 41px.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Raised_Child_Margin_Stays_Inside_A_Padded_Block()
    {
        var (block, _) = Lay(b => b.PaddingBottom = "1px",
            c => c.Height = "30px",
            c => { c.Height = "10px"; c.MarginTop = "-25px"; c.MarginBottom = "10px"; });

        Assert.Equal(26, block.Size.Height, 2);
    }

    /// <summary>
    /// Without the padding, that margin collapses through the block: it is 15px tall, and the block
    /// after it begins 10px below it. It was 30px tall, the block after 10px below.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Raised_Child_Margin_Collapses_Through_The_Block()
    {
        var (block, after) = Lay(_ => { },
            c => c.Height = "30px",
            c => { c.Height = "10px"; c.MarginTop = "-25px"; c.MarginBottom = "10px"; });

        Assert.Equal(15, block.Size.Height, 2);
        Assert.Equal(block.ActualBottom + 10, after.Location.Y, 2);
    }

    /// <summary>
    /// A last child pulled up above the block's content by <c>margin-top: -50px</c> leaves the
    /// content zero tall: the block is as tall as its 1px padding. It was 31px.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Last_Child_Above_The_Content_Leaves_It_Empty()
    {
        var (block, _) = Lay(b => b.PaddingBottom = "1px",
            c => c.Height = "30px",
            c => { c.Height = "10px"; c.MarginTop = "-50px"; });

        Assert.Equal(1, block.Size.Height, 2);
    }

    /// <summary>
    /// A last child that is not generated (<c>display: none</c>) adds nothing: after a 10px child,
    /// one with <c>margin-bottom: 20px</c> leaves the block with 1px of padding 11px tall. It was
    /// 31px.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Display_None_Last_Child_Adds_No_Margin()
    {
        var (block, _) = Lay(b => b.PaddingBottom = "1px",
            c => c.Height = "10px",
            c => { c.Display = CssConstants.None; c.MarginBottom = "20px"; });

        Assert.Equal(11, block.Size.Height, 2);
    }

    /// <summary>
    /// In a block with <c>overflow: hidden</c>, a 50px float and a 10px child with
    /// <c>margin-bottom: 10px</c> after it: the block ends at the float's bottom, 50px down, below
    /// the child's margin. It was 60px tall, the child's margin added below the float.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Float_Below_The_Last_Child_Margin_Ends_A_Formatting_Context_Root()
    {
        var (block, _) = Lay(b => b.Overflow = "hidden",
            c => { c.Float = CssConstants.Left; c.Height = "50px"; c.Width = "10px"; },
            c => { c.Height = "10px"; c.MarginBottom = "10px"; });

        Assert.Equal(50, block.Size.Height, 2);
    }

    /// <summary>
    /// After a 30px child, an empty one whose margins collapse through it and through the block
    /// ends the block where they begin, 30px down; they come to 20px below it with
    /// <c>margin-top: 20px</c>, to 25px above its bottom with <c>margin-top: -25px</c>, and to
    /// 15px below it with two empty children of <c>margin-top: 20px</c> and <c>-5px</c>. The block
    /// ended 50px, 30px and 50px down, the block after right below it.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("20px", null, 20)]
    [InlineData("-25px", null, -25)]
    [InlineData("20px", "-5px", 15)]
    public void Empty_Last_Children_Hand_Their_Margins_On(string marginTop, string? secondMarginTop, float below)
    {
        var children = secondMarginTop == null
            ? new Action<CssBox>[] { c => c.Height = "30px", c => c.MarginTop = marginTop }
            : new Action<CssBox>[] { c => c.Height = "30px", c => c.MarginTop = marginTop, c => c.MarginTop = secondMarginTop };

        var (block, after) = Lay(_ => { }, children);

        Assert.Equal(30, block.Size.Height, 2);
        Assert.Equal(block.ActualBottom + below, after.Location.Y, 2);
    }

    /// <summary>
    /// In a block with 1px of bottom padding, an empty last child with 10px margins, after a 30px
    /// child, ends the content where the margins do, 10px below the 30px child: the block is 41px
    /// tall. It was 51px, the empty child's bottom margin added below its top one.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Empty_Last_Child_Margins_Collapse_Inside_A_Padded_Block()
    {
        var (block, _) = Lay(b => b.PaddingBottom = "1px",
            c => c.Height = "30px",
            c => { c.MarginTop = "10px"; c.MarginBottom = "10px"; });

        Assert.Equal(41, block.Size.Height, 2);
    }

    /// <summary>
    /// A clearfix: after a 50px float and a 10px child, an empty child with <c>clear: both</c>
    /// and <c>margin-bottom: 10px</c> is moved down past the float, and its margin stays inside
    /// the block, which is 60px tall, with the block after right below it. It was 50px tall, the
    /// block after 10px below.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Empty_Cleared_Last_Child_Keeps_Its_Margin_Inside()
    {
        var (block, after) = Lay(_ => { },
            c => { c.Float = CssConstants.Left; c.Height = "50px"; c.Width = "10px"; },
            c => c.Height = "10px",
            c => { c.Clear = "both"; c.MarginBottom = "10px"; });

        Assert.Equal(60, block.Size.Height, 2);
        Assert.Equal(block.ActualBottom, after.Location.Y, 2);
    }

    /// <summary>
    /// A clearfix whose float ends above it: after a 5px float and a 10px child with
    /// <c>margin-bottom: 16px</c>, an empty child with <c>clear: both</c> keeps the margin above it
    /// inside the block, which is 26px tall, with the block after right below it, as Chromium has
    /// it. It passes before and after; with only the boxes <c>clear</c> moves down kept apart, the
    /// block was 10px tall and the block after 16px below it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Empty_Last_Child_With_A_Float_To_Clear_Keeps_The_Margin_Above_It_Inside()
    {
        var (block, after) = Lay(_ => { },
            c => { c.Float = CssConstants.Left; c.Height = "5px"; c.Width = "10px"; },
            c => { c.Height = "10px"; c.MarginBottom = "16px"; },
            c => c.Clear = "both");

        Assert.Equal(26, block.Size.Height, 2);
        Assert.Equal(block.ActualBottom, after.Location.Y, 2);
    }

    /// <summary>
    /// A float that ends above the block is not one its last child keeps the margin inside for:
    /// after a 5px float in the 10px block before it, a block holding a 30px child with
    /// <c>margin-bottom: 10px</c> and an empty child with <c>clear: both</c> is 30px tall, and the
    /// block after begins 10px below it, as Chromium has it. It was 40px tall, the block after
    /// right below it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Empty_Last_Child_Whose_Floats_End_Above_The_Block_Hands_Its_Margins_On()
    {
        var root = Root();
        var wrapper = Block(Block(root));
        var before = Block(wrapper);
        before.Height = "10px";
        var floated = Block(before);
        floated.Float = CssConstants.Left;
        floated.Width = "10px";
        floated.Height = "5px";
        var block = Block(wrapper);
        var child = Block(block);
        child.Width = "100px";
        child.Height = "30px";
        child.MarginBottom = "10px";
        Block(block).Clear = "both";
        var after = Block(wrapper);
        after.Height = "10px";

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(30, block.Size.Height, 2);
        Assert.Equal(block.ActualBottom + 10, after.Location.Y, 2);
    }

    /// <summary>
    /// An empty child with <c>clear: both</c> and no float to clear has no clearance, and its
    /// margins collapse through as any empty child's do: after a 30px child with
    /// <c>margin-bottom: 10px</c>, the block is 30px tall and the block after begins 10px below it.
    /// It was 40px tall, the block after right below it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Empty_Last_Child_With_Nothing_To_Clear_Hands_Its_Margins_On()
    {
        var (block, after) = Lay(_ => { },
            c => { c.Height = "30px"; c.MarginBottom = "10px"; },
            c => c.Clear = "both");

        Assert.Equal(30, block.Size.Height, 2);
        Assert.Equal(block.ActualBottom + 10, after.Location.Y, 2);
    }

    /// <summary>
    /// Controls, which pass before and after: a clearfix with no margin ends the block at the
    /// float's bottom, 50px down; a relatively positioned last child moved up by <c>top: -25px</c>
    /// ends the padded block where it ends in the flow, 41px down; two 30px and 10px children end
    /// it 40px down.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Controls_Clearfix_Relative_And_Plain_Children()
    {
        var (clearfix, _) = Lay(_ => { },
            c => { c.Float = CssConstants.Left; c.Height = "50px"; c.Width = "10px"; },
            c => c.Height = "10px",
            c => c.Clear = "both");
        var (relative, _) = Lay(b => b.PaddingBottom = "1px",
            c => c.Height = "30px",
            c => { c.Height = "10px"; c.Position = CssConstants.Relative; c.Top = "-25px"; });
        var (plain, _) = Lay(_ => { },
            c => c.Height = "30px",
            c => c.Height = "10px");

        Assert.Equal(50, clearfix.Size.Height, 2);
        Assert.Equal(41, relative.Size.Height, 2);
        Assert.Equal(40, plain.Size.Height, 2);
    }

    /// <summary>
    /// A root holding a body holding a wrapper, which holds a 10px block, a block styled by
    /// <paramref name="styleBlock"/> holding a 100px wide child styled by each of
    /// <paramref name="children"/>, and a 10px block after, laid out. Returns the styled block and
    /// the block after.
    /// </summary>
    private static (CssBox Block, CssBox After) Lay(Action<CssBox> styleBlock, params Action<CssBox>[] children)
    {
        var root = Root();
        var body = Block(root);
        var wrapper = Block(body);
        var before = Block(wrapper);
        before.Height = "10px";
        var block = Block(wrapper);
        styleBlock(block);
        foreach (var style in children)
        {
            var child = Block(block);
            child.Width = "100px";
            style(child);
        }
        var after = Block(wrapper);
        after.Height = "10px";

        root.PerformLayout(root.LayoutEnvironment);
        return (block, after);
    }

    /// <summary>A root element box on a 1024 × 768 page.</summary>
    private static CssBox Root() => new(null, new HtmlTag("html", false, null), BaseUrl)
    {
        Display = "block",
        Location = new PointF(0, 0),
        Size = new SizeF(1024, 768),
        LayoutEnvironment = new FakeLayoutEnvironment(),
    };

    private static CssBox Block(CssBox parent) =>
        new(parent, new HtmlTag("div", false, null), BaseUrl) { Display = CssConstants.Block };

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

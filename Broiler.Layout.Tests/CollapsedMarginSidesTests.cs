using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// The margins collapsed above a box are kept as the largest positive one and the most negative
/// one, and the box after an empty block, a first child and the root element's first child are each
/// placed by that whole set.
/// </summary>
/// <remarks>
/// <para>
/// CSS2.1 §8.3.1 collapses adjoining margins to the largest positive one plus the most negative
/// one, and an empty block's top and bottom margins adjoin, so the margins above it, its own and
/// the next block's top margin are one set. <c>MarginTopCollapse</c> kept one number for what was
/// collapsed above a box, and it meant what the set came to after a sibling, its positive side for
/// a first child, the larger of the two after an empty block, and nothing in a block with top
/// padding. So the block after an empty one collapsed only the empty block's margins with its own
/// and then took away everything spent above the empty block: after a 10px block with
/// <c>margin-bottom: 20px</c> and an empty block, it began right below the 10px block. A first child
/// compared its margin with what a parent's set came to rather than its positive side, and the root
/// element's first child dropped a negative margin of its own.
/// </para>
/// <para>
/// Each run of blocks here is in a 320px block in a block in the root, as a page's are in its
/// body, after a 10px block unless it is the first child there. Paragraphs have no bottom margin,
/// and words are 8×16px.
/// </para>
/// </remarks>
public sealed class CollapsedMarginSidesTests
{
    private static readonly Uri BaseUrl = new("file:///collapsed-margin-sides.html");

    /// <summary>
    /// After the 10px block with the given bottom margin and an empty block with the given top
    /// margin, a paragraph begins as far below the 10px block as the three margins collapse to: 20px
    /// below it, 6px above its bottom, and 20px below it with the empty block's own 5px. It began
    /// right below it, right below it, and 5px below it.
    /// </summary>
    [Theory]
    [InlineData("20px", "0", 20)]
    [InlineData("-6px", "0", -6)]
    [InlineData("20px", "5px", 20)]
    public void The_Margins_Above_An_Empty_Block_Reach_The_Block_After_It(string beforeMarginBottom, string emptyMarginTop, float offset)
    {
        var page = new Page(beforeMarginBottom: beforeMarginBottom);
        var empty = Block(page.Content, emptyMarginTop);
        var paragraph = Paragraph(page.Content, "0");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom + offset, empty.Location.Y, 1);
        Assert.Equal(page.Before.ActualBottom + offset, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// After the 10px block with <c>margin-bottom: 20px</c> and two empty blocks, the paragraph
    /// begins 20px below the 10px block. It began 10px above its bottom, over it, each empty block
    /// taking the margin away again.
    /// </summary>
    [Fact]
    public void Two_Empty_Blocks_Hand_The_Margin_On_Whole()
    {
        var page = new Page(beforeMarginBottom: "20px");
        Block(page.Content);
        Block(page.Content);
        var paragraph = Paragraph(page.Content, "0");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom + 20, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// After the 10px block with <c>margin-bottom: 20px</c> and an empty block holding an empty
    /// block with <c>margin-top: 5px</c>, the paragraph begins 20px below the 10px block. It began
    /// 5px below it.
    /// </summary>
    [Fact]
    public void An_Empty_Block_Holding_One_Hands_The_Margin_On()
    {
        var page = new Page(beforeMarginBottom: "20px");
        var empty = Block(page.Content);
        Block(empty, "5px");
        var paragraph = Paragraph(page.Content, "0");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom + 20, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// After the 10px block with <c>margin-bottom: 20px</c> and an empty block, a paragraph with
    /// <c>margin-top: -30px</c> begins 10px above the 10px block's bottom, the margins collapsing to
    /// 20px less 30px. It began 30px above it.
    /// </summary>
    [Fact]
    public void A_Negative_Margin_After_The_Empty_Block_Takes_From_The_Margin_Above()
    {
        var page = new Page(beforeMarginBottom: "20px");
        Block(page.Content);
        var paragraph = Paragraph(page.Content, "-30px");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom - 10, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// In a block with 1px of top padding, an empty first child with <c>margin-top: 10px</c> and
    /// then a paragraph: the paragraph begins 11px down the block, the margin spent once, and the
    /// block is 27px tall. It began 21px down, the margin spent again, in a block 37px tall.
    /// </summary>
    [Fact]
    public void An_Empty_First_Child_Of_A_Padded_Block_Spends_Its_Margin_Once()
    {
        var page = new Page();
        var outer = Block(page.Content);
        outer.PaddingTop = "1px";
        Block(outer, "10px");
        var paragraph = Paragraph(outer, "0");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y + 11, paragraph.Location.Y, 1);
        Assert.Equal(27, outer.Size.Height, 1);
    }

    /// <summary>
    /// After the 10px block with <c>margin-bottom: 10px</c>, a block with <c>margin-top: -4px</c>
    /// holding a paragraph with <c>margin-top: 8px</c> begins 6px below the 10px block, the three
    /// margins collapsing to 10px less 4px, with the paragraph at its top. It began 8px below it:
    /// the 6px the first two came to stood for the positive side, and the paragraph's 8px moved the
    /// block down by the 2px it is larger.
    /// </summary>
    [Fact]
    public void A_First_Childs_Margin_Is_Compared_With_The_Positive_Side_Above_Its_Parent()
    {
        var page = new Page(beforeMarginBottom: "10px");
        var outer = Block(page.Content, "-4px");
        var paragraph = Paragraph(outer, "8px");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom + 6, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// The 320px block, the first child of the block in the root, with <c>margin-top: -4px</c> of
    /// its own, begins 4px above the top of the block in the root, which keeps its place as a page's
    /// root element does, with the paragraph in it at its top. Its margin was dropped.
    /// </summary>
    [Fact]
    public void The_Root_Elements_First_Child_Keeps_A_Negative_Margin()
    {
        var page = new Page(afterBlock: false);
        page.Content.MarginTop = "-4px";
        var paragraph = Paragraph(page.Content, "0");
        page.Layout();

        Assert.Equal(page.Body.ClientTop - 4, page.Content.Location.Y, 1);
        Assert.Equal(page.Content.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// Laid out a second time, the two empty blocks' page gives the same places: what a pass records
    /// of the margins it collapsed is not carried into the next.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Places()
    {
        var page = new Page(beforeMarginBottom: "20px");
        var first = Block(page.Content);
        Block(first, "5px");
        Block(page.Content, "-2px");
        var paragraph = Paragraph(page.Content, "0");
        page.Layout();

        float top = paragraph.Location.Y;
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom + 18, top, 1);
        Assert.Equal(top, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// Controls, which pass before and after:
    /// <list type="bullet">
    /// <item>after the 10px block with no margin and an empty block, the paragraph is right below
    /// the 10px block;</item>
    /// <item>after an empty block with <c>margin-bottom: 16px</c>, it is 16px below;</item>
    /// <item>after the 10px block with <c>margin-bottom: 20px</c> and no empty block, 20px
    /// below;</item>
    /// <item>in a block with <c>margin-top: 24px</c> whose first child is an empty block with
    /// margins of 8px and 16px, at the top of that block, which is 24px below.</item>
    /// </list>
    /// </summary>
    [Theory]
    [InlineData("none", 0)]
    [InlineData("empty-bottom", 16)]
    [InlineData("no-empty", 20)]
    [InlineData("container", 24)]
    public void Control_Margins_That_Were_Handed_On_Already(string kind, float offset)
    {
        var page = new Page(beforeMarginBottom: kind == "no-empty" ? "20px" : "0");
        var holder = page.Content;

        switch (kind)
        {
            case "none":
                Block(page.Content);
                break;
            case "empty-bottom":
                Block(page.Content).MarginBottom = "16px";
                break;
            case "container":
                holder = Block(page.Content, "24px");
                var empty = Block(holder, "8px");
                empty.MarginBottom = "16px";
                break;
        }

        var paragraph = Paragraph(holder, "0");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom + offset, paragraph.Location.Y, 1);

        if (kind == "container")
            Assert.Equal(holder.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// The root, the block in it, the 320px block in that and, unless it is left out, the 10px
    /// block in that, with the given bottom margin.
    /// </summary>
    private sealed class Page
    {
        public Page(bool afterBlock = true, string beforeMarginBottom = "0")
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

            if (afterBlock)
            {
                Before = new CssBox(Content, new HtmlTag("b", false, null), BaseUrl)
                {
                    Display = "block",
                    Height = "10px",
                    MarginBottom = beforeMarginBottom,
                };
            }
        }

        public CssBox Root { get; }

        public CssBox Body { get; }

        public CssBox Content { get; }

        public CssBox? Before { get; }

        public void Layout()
        {
            FlexGridItemBlockification.Generate(Root);
            Root.PerformLayout(Root.LayoutEnvironment);
        }
    }

    private static CssBox Block(CssBox parent, string marginTop = "0") =>
        new(parent, new HtmlTag("div", false, null), BaseUrl) { Display = "block", MarginTop = marginTop };

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

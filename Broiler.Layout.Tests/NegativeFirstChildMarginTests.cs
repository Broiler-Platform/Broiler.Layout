using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A first child's negative top margin collapses with its parent's, and with the margins above
/// that, as the most negative of them, and is not dropped.
/// </summary>
/// <remarks>
/// <para>
/// CSS2.1 §8.3.1: adjoining margins collapse to the largest positive one plus the most negative
/// one, and a box's top margin adjoins its first in-flow child's when no border, padding or
/// clearance separates them. <c>MarginTopCollapse</c> only ever moved a first child's parent down,
/// by the part of a positive margin the margins above did not already give, and dropped a negative
/// one: after a 10px block, a block holding a paragraph with <c>margin-top: -4px</c> began right
/// below the 10px block, where browsers begin it 4px higher, over the block. It also took a
/// parent's own negative margin for the positive side of the set, so a first child with no margin
/// moved a parent with <c>margin-top: -2px</c> back down by 2px.
/// </para>
/// <para>
/// Each run of blocks here is in a 320px block in a block in the root, as a page's are in its
/// body, after a 10px block unless it is the first child there. Paragraphs have no bottom margin,
/// and words are 8×16px.
/// </para>
/// </remarks>
public sealed class NegativeFirstChildMarginTests
{
    private static readonly Uri BaseUrl = new("file:///negative-first-child-margin.html");

    /// <summary>
    /// A block holding a paragraph with <c>margin-top: -4px</c> begins 4px above the 10px block's
    /// bottom, 16px tall, with the paragraph at its top. It began right below the 10px block.
    /// </summary>
    [Fact]
    public void A_Negative_Margin_Pulls_Its_Parent_Up()
    {
        var page = new Page();
        var outer = Block(page.Content);
        var paragraph = Paragraph(outer, "-4px");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom - 4, outer.Location.Y, 1);
        Assert.Equal(16, outer.Size.Height, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// With <c>margin-top: 6px</c> of its own, the block begins 2px below the 10px block, the two
    /// margins collapsing to 6px less 4px. It began 6px below it.
    /// </summary>
    [Fact]
    public void The_Parents_Positive_Margin_Collapses_With_It()
    {
        var page = new Page();
        var outer = Block(page.Content, "6px");
        var paragraph = Paragraph(outer, "-4px");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom + 2, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// After a 10px block with <c>margin-bottom: 20px</c>, the block begins 16px below it, the two
    /// margins collapsing to 20px less 4px. It began 20px below it.
    /// </summary>
    [Fact]
    public void The_Margin_Above_Collapses_With_It()
    {
        var page = new Page(beforeMarginBottom: "20px");
        var outer = Block(page.Content);
        var paragraph = Paragraph(outer, "-4px");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom + 16, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// A block holding an unpadded block holding the paragraph begins 4px above the 10px block's
    /// bottom, with the inner block and the paragraph at its top. It began right below it.
    /// </summary>
    [Fact]
    public void The_Margin_Collapses_Through_Two_Blocks()
    {
        var page = new Page();
        var outer = Block(page.Content);
        var inner = Block(outer);
        var paragraph = Paragraph(inner, "-4px");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom - 4, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, inner.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// The two margins collapse to the more negative of them, either way round: with -2px on the
    /// block and -4px on the paragraph, or -4px on the block and -2px on the paragraph, the block
    /// begins 4px above the 10px block's bottom, with the paragraph at its top. It began 2px above
    /// it both ways: the paragraph's margin was dropped, or moved the block back down by the 2px
    /// between the two.
    /// </summary>
    [Theory]
    [InlineData("-2px", "-4px")]
    [InlineData("-4px", "-2px")]
    public void The_Most_Negative_Margin_Is_The_One_They_Collapse_To(string outerMargin, string paragraphMargin)
    {
        var page = new Page();
        var outer = Block(page.Content, outerMargin);
        var paragraph = Paragraph(outer, paragraphMargin);
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom - 4, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// An inner block with <c>margin-top: -4px</c> holding a paragraph with <c>margin-top: 10px</c>:
    /// the three margins collapse to 10px less 4px, and the outer block begins 6px below the 10px
    /// block, with the inner block and the paragraph at its top. It began 10px below it.
    /// </summary>
    [Fact]
    public void A_Negative_Margin_Above_A_Positive_One_Takes_From_It()
    {
        var page = new Page();
        var outer = Block(page.Content);
        var inner = Block(outer, "-4px");
        var paragraph = Paragraph(inner, "10px");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom + 6, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, inner.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// A block with <c>margin-top: -2px</c> whose paragraph has no margin keeps its own: it begins
    /// 2px above the 10px block's bottom, with the paragraph at its top. It began right below it,
    /// moved back down by its own margin, which was taken for the positive side of the set.
    /// </summary>
    [Fact]
    public void A_Parents_Negative_Margin_Stays_Over_A_Child_With_None()
    {
        var page = new Page();
        var outer = Block(page.Content, "-2px");
        var paragraph = Paragraph(outer, "0");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom - 2, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// When the block's first child is an empty paragraph with <c>margin-top: -4px</c> and the
    /// paragraph after it holds a word, the block begins 4px above the 10px block's bottom, 16px
    /// tall, with the second paragraph at its top: the empty one hands its margin on to it, and
    /// what it already spent pulling the block up is not spent again.
    /// </summary>
    [Fact]
    public void An_Empty_First_Child_Hands_Its_Margin_On_Once()
    {
        var page = new Page();
        var outer = Block(page.Content);
        Paragraph(outer, "-4px", empty: true);
        var second = Paragraph(outer, "0");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom - 4, outer.Location.Y, 1);
        Assert.Equal(16, outer.Size.Height, 1);
        Assert.Equal(outer.Location.Y, second.Location.Y, 1);
    }

    /// <summary>
    /// The same with the empty paragraph in an empty block of its own, the paragraph holding the
    /// word after that block: the outer block begins 4px above the 10px block's bottom, with the
    /// paragraph at its top. It began right below it, with the paragraph 4px above its top.
    /// </summary>
    [Fact]
    public void An_Empty_Block_Holding_It_Hands_It_On_Once()
    {
        var page = new Page();
        var outer = Block(page.Content);
        var empty = Block(outer);
        Paragraph(empty, "-4px", empty: true);
        var paragraph = Paragraph(outer, "0");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom - 4, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// With <c>margin-top: -2px</c> on the block and an empty first paragraph with none, the
    /// paragraph holding the word after it begins at the block's top, which is 2px above the 10px
    /// block's bottom. It began right below it.
    /// </summary>
    [Fact]
    public void A_Parents_Negative_Margin_Stays_Over_An_Empty_Child()
    {
        var page = new Page();
        var outer = Block(page.Content, "-2px");
        Paragraph(outer, "0", empty: true);
        var second = Paragraph(outer, "0");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom - 2, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, second.Location.Y, 1);
    }

    /// <summary>
    /// When the block is the first child of the 320px block, that block begins 4px above the top of
    /// the block in the root, which keeps its place as a page's root element does, with the block
    /// and the paragraph at its top. It began at the top.
    /// </summary>
    [Fact]
    public void The_Margin_Pulls_Up_To_The_Block_In_The_Root()
    {
        var page = new Page(afterBlock: false);
        var outer = Block(page.Content);
        var paragraph = Paragraph(outer, "-4px");
        page.Layout();

        Assert.Equal(page.Body.ClientTop - 4, page.Content.Location.Y, 1);
        Assert.Equal(page.Content.Location.Y, outer.Location.Y, 1);
        Assert.Equal(page.Content.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// A block holding a block with <c>margin-top: -2px</c>, which holds an empty paragraph with
    /// <c>margin-top: -4px</c> and a paragraph holding a word, begins 4px above the 10px block's
    /// bottom, and laid out a second time gives the same places: what a pass records of the margins
    /// it collapsed is not carried into the next.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Places()
    {
        var page = new Page();
        var outer = Block(page.Content);
        var inner = Block(outer, "-2px");
        Paragraph(inner, "-4px", empty: true);
        var second = Paragraph(inner, "0");
        page.Layout();

        float outerTop = outer.Location.Y, secondTop = second.Location.Y, height = outer.Size.Height;
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom - 4, outerTop, 1);
        Assert.Equal(outerTop, outer.Location.Y, 1);
        Assert.Equal(secondTop, second.Location.Y, 1);
        Assert.Equal(height, outer.Size.Height, 1);
    }

    /// <summary>
    /// Controls, which pass before and after, each a block holding a paragraph:
    /// <list type="bullet">
    /// <item>with 1px of top padding, the block begins right below the 10px block and keeps the
    /// paragraph's -4px margin inside, the paragraph 3px above its top, and is 13px tall;</item>
    /// <item>with <c>overflow: hidden</c>, it keeps it too, the paragraph 4px above its top;</item>
    /// <item>a row flex container's and a grid container's item keeps it, the paragraph 4px above
    /// the item's top, which is at the container's, right below the 10px block;</item>
    /// <item>a paragraph with <c>margin-top: 10px</c> collapses through, the block 10px below the
    /// 10px block with the paragraph at its top.</item>
    /// </list>
    /// </summary>
    [Theory]
    [InlineData("padding", 0, -3)]
    [InlineData("overflow", 0, -4)]
    [InlineData("flex", 0, -4)]
    [InlineData("grid", 0, -4)]
    [InlineData("positive", 10, 0)]
    public void Control_A_Margin_Kept_Inside_Or_Positive(string kind, float outerTop, float paragraphTop)
    {
        var page = new Page();
        var outer = Block(page.Content);
        var holder = outer;

        switch (kind)
        {
            case "padding":
                outer.PaddingTop = "1px";
                break;
            case "overflow":
                outer.Overflow = "hidden";
                break;
            case "flex":
                outer.Display = "flex";
                holder = Block(outer);
                break;
            case "grid":
                outer.Display = "grid";
                outer.GridTemplateColumns = "100px";
                holder = Block(outer);
                break;
        }

        var paragraph = Paragraph(holder, kind == "positive" ? "10px" : "-4px");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom + outerTop, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, holder.Location.Y, 1);
        Assert.Equal(holder.Location.Y + paragraphTop, paragraph.Location.Y, 1);

        if (kind == "padding")
            Assert.Equal(13, outer.Size.Height, 1);
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
    /// A paragraph with the given top margin and no bottom margin, holding a word unless
    /// <paramref name="empty"/>.
    /// </summary>
    private static CssBox Paragraph(CssBox parent, string marginTop, bool empty = false)
    {
        var paragraph = new CssBox(parent, new HtmlTag("p", false, null), BaseUrl)
        {
            Display = "block",
            MarginTop = marginTop,
            MarginBottom = "0",
        };

        if (!empty)
        {
            var text = new CssBox(paragraph, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
            text.ParseToWords();
        }

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

using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// The margins an empty first child hands on to the block after it collapse through their parent's
/// top, as the first child's own do, and move the parent rather than the block inside it.
/// </summary>
/// <remarks>
/// <para>
/// CSS2.1 §8.3.1: a box's top margin adjoins its first in-flow child's, an empty block's top and
/// bottom margins adjoin, and so do a block's bottom margin and the next block's top margin. So when
/// a block's first child is empty, the child's margins and the next block's top margin are all in
/// the set above the parent's top. <c>MarginTopCollapse</c> placed the next block by that set inside
/// the parent: in <c>&lt;div&gt;&lt;div style="margin-bottom: 16px"&gt;&lt;/div&gt;&lt;p&gt;</c>,
/// the outer block kept its place and held the paragraph 16px down, where browsers begin the outer
/// block 16px lower with the paragraph at its top.
/// </para>
/// <para>
/// Each run of blocks here is in a 320px block in a block in the root, as a page's are in its
/// body, after a 10px block unless it is the first child there. Paragraphs have no bottom margin,
/// and words are 8×16px.
/// </para>
/// </remarks>
public sealed class EmptyFirstChildMarginTests
{
    private static readonly Uri BaseUrl = new("file:///empty-first-child-margin.html");

    /// <summary>
    /// A block whose first child is empty with the given bottom margin, and then a paragraph, begins
    /// that far below the 10px block's bottom, 16px tall, with the paragraph at its top: 16px below,
    /// or 6px above. It began right below it, with the paragraph 16px down it or 6px above its top.
    /// </summary>
    [Theory]
    [InlineData("16px", 16)]
    [InlineData("-6px", -6)]
    public void The_Empty_First_Childs_Margins_Move_Its_Parent(string emptyMarginBottom, float offset)
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = emptyMarginBottom;
        var paragraph = Paragraph(outer, "0");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom + offset, outer.Location.Y, 1);
        Assert.Equal(16, outer.Size.Height, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// The block after the empty first child brings its own margins into the set: a paragraph with
    /// <c>margin-top: 12px</c>, or an empty block with <c>margin-top: 10px</c> before a paragraph,
    /// begins the outer block 12px or 10px below the 10px block, with the paragraph at its top. It
    /// began right below it, with the paragraph 12px or 10px down it.
    /// </summary>
    [Theory]
    [InlineData(false, 12)]
    [InlineData(true, 10)]
    public void The_Next_Blocks_Margin_Joins_The_Set(bool secondEmpty, float offset)
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer);
        CssBox paragraph;

        if (secondEmpty)
        {
            Block(outer, "10px");
            paragraph = Paragraph(outer, "0");
        }
        else
        {
            paragraph = Paragraph(outer, "12px");
        }

        page.Layout();

        Assert.Equal(page.Before!.ActualBottom + offset, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// With <c>margin-top: 8px</c> on the outer block, an empty first child with
    /// <c>margin-top: 4px</c> and a paragraph with <c>margin-top: 12px</c>, the set comes to 12px:
    /// the outer block begins 12px below the 10px block, with the paragraph at its top. It began 8px
    /// below it, with the paragraph 4px down it.
    /// </summary>
    [Fact]
    public void The_Set_Includes_The_Parents_Own_Margin()
    {
        var page = new Page();
        var outer = Block(page.Content, "8px");
        Block(outer, "4px");
        var paragraph = Paragraph(outer, "12px");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom + 12, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// When the empty block and the paragraph are in an unpadded block in the outer one, the margin
    /// collapses through both: the outer block begins 16px below the 10px block, with the inner block
    /// and the paragraph at its top. It began right below it, with the paragraph 16px down.
    /// </summary>
    [Fact]
    public void The_Set_Collapses_Through_Two_Blocks()
    {
        var page = new Page();
        var outer = Block(page.Content);
        var inner = Block(outer);
        Block(inner).MarginBottom = "16px";
        var paragraph = Paragraph(inner, "0");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom + 16, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, inner.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// A <c>display: none</c> box between the empty block and the paragraph, as a
    /// <c>&lt;script&gt;</c> is, changes nothing: the outer block begins 16px below the 10px block,
    /// with the paragraph at its top.
    /// </summary>
    [Fact]
    public void A_Hidden_Box_Between_Changes_Nothing()
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "16px";
        Block(outer).Display = CssConstants.None;
        var paragraph = Paragraph(outer, "0");
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom + 16, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// When the empty block is the 320px block's first child, that block begins 16px down the block
    /// in the root, which keeps its place as a page's root element does, with the paragraph at its
    /// top. It began at the top, with the paragraph 16px down.
    /// </summary>
    [Fact]
    public void The_Set_Moves_Up_To_The_Block_In_The_Root()
    {
        var page = new Page(afterBlock: false);
        Block(page.Content).MarginBottom = "16px";
        var paragraph = Paragraph(page.Content, "0");
        page.Layout();

        Assert.Equal(page.Body.ClientTop + 16, page.Content.Location.Y, 1);
        Assert.Equal(page.Content.Location.Y, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// An empty first child with <c>margin-bottom: -6px</c>, then an empty block with
    /// <c>margin-top: 10px</c> and a paragraph, begin the outer block 4px below the 10px block, and
    /// laid out a second time, the same: what a pass records of the margins it collapsed is not
    /// carried into the next.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Places()
    {
        var page = new Page();
        var outer = Block(page.Content);
        Block(outer).MarginBottom = "-6px";
        Block(outer, "10px");
        var paragraph = Paragraph(outer, "0");
        page.Layout();

        float outerTop = outer.Location.Y, paragraphTop = paragraph.Location.Y;
        page.Layout();

        Assert.Equal(page.Before!.ActualBottom + 4, outerTop, 1);
        Assert.Equal(outerTop, paragraphTop, 1);
        Assert.Equal(outerTop, outer.Location.Y, 1);
        Assert.Equal(paragraphTop, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// Controls, which pass before and after, each an outer block whose first child is an empty
    /// block with <c>margin-bottom: 16px</c>, then a paragraph:
    /// <list type="bullet">
    /// <item>with 1px of top padding, the outer block begins right below the 10px block and holds
    /// the paragraph 17px down;</item>
    /// <item>with <c>overflow: hidden</c>, it holds it 16px down;</item>
    /// <item>with a 12px float between the empty block and the paragraph, the float and the
    /// paragraph are 16px below the 10px block;</item>
    /// <item>with <c>margin-top: 24px</c> and margins of 8px and 16px on the empty block, the outer
    /// block begins 24px below with the paragraph at its top;</item>
    /// <item>with a block holding a word instead of the empty one, and a paragraph with
    /// <c>margin-top: 12px</c>, the outer block begins right below and the paragraph is 12px below
    /// that block.</item>
    /// </list>
    /// </summary>
    [Theory]
    [InlineData("padding")]
    [InlineData("overflow")]
    [InlineData("float")]
    [InlineData("margin")]
    [InlineData("not-empty")]
    public void Control_A_Set_That_Stays_Inside_Or_Does_Not_Grow(string kind)
    {
        var page = new Page();
        var outer = Block(page.Content, kind == "margin" ? "24px" : "0");
        var first = Block(outer, kind == "margin" ? "8px" : "0");
        first.MarginBottom = "16px";
        CssBox? floated = null;

        switch (kind)
        {
            case "padding":
                outer.PaddingTop = "1px";
                break;
            case "overflow":
                outer.Overflow = "hidden";
                break;
            case "float":
                floated = new CssBox(outer, new HtmlTag("div", false, null), BaseUrl)
                {
                    Display = "block",
                    Float = CssConstants.Left,
                    Width = "30px",
                    Height = "12px",
                };
                break;
            case "not-empty":
                first.MarginBottom = "0";
                Word(first);
                break;
        }

        var paragraph = Paragraph(outer, kind == "not-empty" ? "12px" : "0");
        page.Layout();

        double below = page.Before!.ActualBottom;

        switch (kind)
        {
            case "padding":
                Assert.Equal(below, outer.Location.Y, 1);
                Assert.Equal(outer.Location.Y + 17, paragraph.Location.Y, 1);
                break;
            case "overflow":
                Assert.Equal(below, outer.Location.Y, 1);
                Assert.Equal(outer.Location.Y + 16, paragraph.Location.Y, 1);
                break;
            case "float":
                Assert.Equal(below + 16, floated!.Location.Y, 1);
                Assert.Equal(below + 16, paragraph.Location.Y, 1);
                break;
            case "margin":
                Assert.Equal(below + 24, outer.Location.Y, 1);
                Assert.Equal(outer.Location.Y, paragraph.Location.Y, 1);
                break;
            case "not-empty":
                Assert.Equal(below, outer.Location.Y, 1);
                Assert.Equal(first.ActualBottom + 12, paragraph.Location.Y, 1);
                break;
        }
    }

    /// <summary>
    /// The root, the block in it, the 320px block in that and, unless it is left out, the 10px
    /// block in that.
    /// </summary>
    private sealed class Page
    {
        public Page(bool afterBlock = true)
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
                Before = new CssBox(Content, new HtmlTag("b", false, null), BaseUrl) { Display = "block", Height = "10px" };
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

        Word(paragraph);
        return paragraph;
    }

    private static void Word(CssBox parent)
    {
        var text = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
        text.ParseToWords();
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

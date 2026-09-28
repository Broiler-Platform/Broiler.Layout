using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A block after a sibling that a negative margin pulled above its parent's content top begins
/// right below that sibling, not at the content top.
/// </summary>
/// <remarks>
/// <para>
/// CSS2.1 §9.4.1: in a block formatting context, boxes are laid out one after the other from the
/// top, the distance between them given by the margins. <c>PerformLayoutImp</c> placed a block at
/// the parent's content top plus its advance past the previous sibling, and clamped that advance at
/// zero, for an inline sibling, which records no bottom of its own. A block records a real bottom,
/// and a negative margin can put it above the content top: in a block with 1px of top padding, a
/// 5px block with <c>margin-top: -20px</c> ends 14px above the block's top, and the paragraph after
/// it began at the content top, where browsers begin it right below that block, 15px higher.
/// </para>
/// <para>
/// Each block here is in a 320px block after a 10px block, as a page's are, and holds a 5px block
/// with <c>margin-top: -20px</c> and then a paragraph with no bottom margin, around one 8×16px word.
/// </para>
/// </remarks>
public sealed class BlockAfterPulledSiblingTests
{
    private static readonly Uri BaseUrl = new("file:///block-after-pulled-sibling.html");

    /// <summary>
    /// In a block with 1px of top padding, 1px of top border or <c>overflow: hidden</c>, which each
    /// keep the 5px block's margin inside, the paragraph begins right below the 5px block, 15px above
    /// the content top, and the block is as tall as its top edge and the 1px of the paragraph below
    /// the content top: 2px, 2px and 1px. It began at the content top, in a block 17, 17 and 16px
    /// tall.
    /// </summary>
    [Theory]
    [InlineData("padding", 2)]
    [InlineData("border", 2)]
    [InlineData("overflow", 1)]
    public void The_Next_Block_Begins_Right_Below_The_Pulled_One(string kind, float height)
    {
        var tree = Build(kind);
        var paragraph = Paragraph(tree.Outer, "0");
        Layout(tree);

        Assert.Equal(tree.Outer.ClientTop - 15, tree.Pulled.ActualBottom, 1);
        Assert.Equal(tree.Pulled.ActualBottom, paragraph.Location.Y, 1);
        Assert.Equal(height, tree.Outer.Size.Height, 1);
    }

    /// <summary>
    /// A paragraph with <c>margin-top: 4px</c> begins 4px below the pulled block, 11px above the
    /// content top. It began 4px below the content top.
    /// </summary>
    [Fact]
    public void The_Next_Blocks_Margin_Counts_From_The_Pulled_Ones_Bottom()
    {
        var tree = Build("padding");
        var paragraph = Paragraph(tree.Outer, "4px");
        Layout(tree);

        Assert.Equal(tree.Pulled.ActualBottom + 4, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// Laid out a second time, the padded block gives the same places.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Places()
    {
        var tree = Build("padding");
        var paragraph = Paragraph(tree.Outer, "0");
        Layout(tree);

        float top = paragraph.Location.Y, height = tree.Outer.Size.Height;
        Layout(tree);

        Assert.Equal(tree.Outer.ClientTop - 15, top, 1);
        Assert.Equal(top, paragraph.Location.Y, 1);
        Assert.Equal(height, tree.Outer.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: in the padded block, after a 20px block with
    /// <c>margin-top: -10px</c>, which ends 10px below the content top, the paragraph begins right
    /// below it.
    /// </summary>
    [Fact]
    public void Control_A_Sibling_That_Ends_Below_The_Content_Top()
    {
        var tree = Build("padding", "20px", "-10px");
        var paragraph = Paragraph(tree.Outer, "0");
        Layout(tree);

        Assert.Equal(tree.Outer.ClientTop + 10, paragraph.Location.Y, 1);
    }

    /// <summary>
    /// The root, the block under test and the block pulled up in it.
    /// </summary>
    private sealed record Tree(CssBox Root, CssBox Outer, CssBox Pulled);

    /// <summary>
    /// In a block in the root, a 320px block holding a 10px block and then the block under test,
    /// kept from collapsing margins through its top by <paramref name="kind"/>, holding a block with
    /// the given height and top margin.
    /// </summary>
    private static Tree Build(string kind, string pulledHeight = "5px", string pulledMarginTop = "-20px")
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var page = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "320px" };
        _ = new CssBox(page, new HtmlTag("b", false, null), BaseUrl) { Display = "block", Height = "10px" };

        var outer = new CssBox(page, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };

        switch (kind)
        {
            case "padding":
                outer.PaddingTop = "1px";
                break;
            case "border":
                outer.BorderTopStyle = CssConstants.Solid;
                outer.BorderTopWidth = "1px";
                break;
            case "overflow":
                outer.Overflow = "hidden";
                break;
        }

        var pulled = new CssBox(outer, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Height = pulledHeight,
            MarginTop = pulledMarginTop,
        };

        return new Tree(root, outer, pulled);
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

        Word(paragraph);
        return paragraph;
    }

    private static void Word(CssBox parent)
    {
        var text = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
        text.ParseToWords();
    }

    private static void Layout(Tree tree)
    {
        FlexGridItemBlockification.Generate(tree.Root);
        tree.Root.PerformLayout(tree.Root.LayoutEnvironment);
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

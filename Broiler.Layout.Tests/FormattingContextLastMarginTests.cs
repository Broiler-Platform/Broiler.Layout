using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A box that establishes a block formatting context keeps its last child's bottom margin inside
/// itself, as it keeps its first child's top margin.
/// </summary>
/// <remarks>
/// <para>
/// CSS2.1 §8.3.1: the margins of a box that establishes a new block formatting context do not
/// collapse with its in-flow children's. <c>MarginBottomCollapse</c> and
/// <c>GetPropagatedMarginBottom</c> let the last child's bottom margin collapse out of such a box
/// all the same, so an <c>overflow: hidden</c> or <c>flow-root</c> block, a float and a flex
/// container ended at the last child's border box, 16px short, with the margin outside or, for a
/// float, lost.
/// </para>
/// <para>
/// Each box here is in a block in the root, as a page's are in its body, after a 5px block. It
/// holds a paragraph with 16px margins around one word, 8×16px, and is followed by a 5px block that
/// clears floats.
/// </para>
/// </remarks>
public sealed class FormattingContextLastMarginTests
{
    private static readonly Uri BaseUrl = new("file:///formatting-context-last-margin.html");

    /// <summary>
    /// An <c>overflow: hidden</c> block, a <c>flow-root</c> block, a 100px float and a column flex
    /// container are each 48px tall, the paragraph 16px down them and their bottom 16px below the
    /// paragraph, with the next block right after. They were 32px tall, and the float's next block
    /// started 16px higher.
    /// </summary>
    [Theory]
    [InlineData("overflow")]
    [InlineData("flow-root")]
    [InlineData("float")]
    [InlineData("column")]
    public void The_Box_Keeps_Its_Last_Childs_Bottom_Margin(string kind)
    {
        var (box, paragraph, next) = Lay(Styled(kind), wrapped: false);

        Assert.Equal(48, box.Size.Height, 1);
        Assert.Equal(box.Location.Y + 16, paragraph.Location.Y, 1);
        Assert.Equal(box.ActualBottom, next.Location.Y, 1);
    }

    /// <summary>
    /// With an unpadded block between them, an <c>overflow: hidden</c> block and a column flex
    /// container keep the paragraph's margin, which has collapsed through that block, inside
    /// themselves: 48px tall, with the next block right after. They were 32px tall, the margin
    /// outside.
    /// </summary>
    [Theory]
    [InlineData("overflow")]
    [InlineData("column")]
    public void The_Box_Keeps_A_Margin_That_Collapsed_Through_Its_Last_Child(string kind)
    {
        var (box, _, next) = Lay(Styled(kind), wrapped: true);

        Assert.Equal(48, box.Size.Height, 1);
        Assert.Equal(box.ActualBottom, next.Location.Y, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: a plain block lets the paragraph's margins collapse
    /// through it, 16px tall with the paragraph at its top and the next block 16px below; and one
    /// with 1px of padding above and below keeps both margins, 50px tall with the paragraph 17px
    /// down and the next block right after.
    /// </summary>
    [Theory]
    [InlineData("plain", 16, 0, 16)]
    [InlineData("padded", 50, 17, 0)]
    public void Control_A_Block_That_Is_Not_A_Formatting_Context(string kind, float height, float paragraphTop, float gap)
    {
        var (box, paragraph, next) = Lay(kind == "padded" ? box => box.PaddingTop = box.PaddingBottom = "1px" : _ => { }, wrapped: false);

        Assert.Equal(height, box.Size.Height, 1);
        Assert.Equal(box.Location.Y + paragraphTop, paragraph.Location.Y, 1);
        Assert.Equal(box.ActualBottom + gap, next.Location.Y, 1);
    }

    private static Action<CssBox> Styled(string kind) => kind switch
    {
        "overflow" => box => box.Overflow = "hidden",
        "flow-root" => box => box.Display = "flow-root",
        "float" => box => { box.Float = "left"; box.Width = "100px"; },
        "column" => box => { box.Display = "flex"; box.FlexDirection = "column"; box.AlignItems = "flex-start"; },
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>
    /// In a block in the root: a 5px block, then a 320px block, styled by <paramref name="style"/>,
    /// holding a paragraph with 16px margins around a word, or, when <paramref name="wrapped"/>, an
    /// unpadded block holding it, and then a 5px block that clears floats.
    /// </summary>
    private static (CssBox Box, CssBox Paragraph, CssBox Next) Lay(Action<CssBox> style, bool wrapped)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        _ = new CssBox(body, new HtmlTag("b", false, null), BaseUrl) { Display = "block", Height = "5px" };
        var box = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "320px" };
        style(box);

        var holder = wrapped ? new CssBox(box, new HtmlTag("div", false, null), BaseUrl) { Display = "block" } : box;
        var paragraph = new CssBox(holder, new HtmlTag("p", false, null), BaseUrl)
        {
            Display = "block",
            MarginTop = "16px",
            MarginBottom = "16px",
        };
        var text = new CssBox(paragraph, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
        text.ParseToWords();

        var next = new CssBox(body, new HtmlTag("b", false, null), BaseUrl)
        {
            Display = "block",
            Clear = "both",
            Height = "5px",
        };

        FlexGridItemBlockification.Generate(root);
        root.PerformLayout(root.LayoutEnvironment);
        return (box, paragraph, next);
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

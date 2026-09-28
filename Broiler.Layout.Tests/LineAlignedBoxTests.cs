using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A box aligned <c>top</c> or <c>bottom</c> is aligned to the top or bottom of its line box, which
/// reaches the leading above and below the text on it and the strut's descent below the baseline.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §10.8.1: <c>top</c> aligns the top of the box with the top of the line box, and
/// <c>bottom</c> its bottom with the bottom of it; the line box reaches from the top of the highest
/// box on it to the bottom of the lowest, the inline boxes of its text and its strut among them, each
/// as tall as its line height. The engine aligned the box to the glyphs of the text instead: a box
/// aligned <c>bottom</c> ended the leading and the strut's descent above the line's bottom, and one
/// aligned <c>top</c> started half the leading below its top.
/// </para>
/// <para>
/// Each line here is in a 320px block with a 16px font: "a", then an empty 8px wide inline-block
/// aligned as given. Words are 8px wide a letter and 16px tall, and stand on a baseline 12.8px below
/// their top.
/// </para>
/// </remarks>
public sealed class LineAlignedBoxTests
{
    private static readonly Uri BaseUrl = new("file:///line-aligned-box.html");

    /// <summary>
    /// In 20px lines, a 30px box aligned <c>bottom</c> ends at the bottom of the strut, 2px of
    /// leading and a 3.2px descent below the baseline, and starts the 30px line; "a" is 12px down it.
    /// The box ended where the glyphs do, 2px higher, and "a" was 14px down.
    /// </summary>
    [Fact]
    public void A_Box_Aligned_Bottom_Ends_At_The_Struts_Bottom()
    {
        var (block, word, box) = Build("20px", 30, CssConstants.Bottom);
        Layout(block);

        Assert.Equal(0, box.Location.Y - block.Location.Y, 1);
        Assert.Equal(12, word.Words[0].Top - block.Location.Y, 1);
        Assert.Equal(30, block.Size.Height, 1);
    }

    /// <summary>
    /// In 30px lines, a 40px box aligned <c>top</c> starts at the top of the strut, 7px of leading
    /// above the glyphs of "a", and the line is 40px tall, "a" 7px down it. The box started where the
    /// glyphs do, 7px down, and the line was 47px tall.
    /// </summary>
    [Fact]
    public void A_Box_Aligned_Top_Starts_At_The_Struts_Top()
    {
        var (block, word, box) = Build("30px", 40, CssConstants.Top);
        Layout(block);

        Assert.Equal(0, box.Location.Y - block.Location.Y, 1);
        Assert.Equal(7, word.Words[0].Top - block.Location.Y, 1);
        Assert.Equal(40, block.Size.Height, 1);
    }

    /// <summary>
    /// A 10px box aligned <c>bottom</c> in 20px lines ends at the bottom of the 20px line, 10px down
    /// it. It ended where the glyphs do, 2px higher.
    /// </summary>
    [Fact]
    public void A_Shorter_Box_Aligned_Bottom_Ends_At_The_Lines_Bottom()
    {
        var (block, _, box) = Build("20px", 10, CssConstants.Bottom);
        Layout(block);

        Assert.Equal(10, box.Location.Y - block.Location.Y, 1);
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: in 16px lines, which have no leading, a 30px box
    /// aligned <c>top</c> starts at the top of the 30px line, with "a" at the top too.
    /// </summary>
    [Fact]
    public void Control_A_Box_Aligned_Top_Beside_Text_With_No_Leading()
    {
        var (block, word, box) = Build("16px", 30, CssConstants.Top);
        Layout(block);

        Assert.Equal(0, box.Location.Y - block.Location.Y, 1);
        Assert.Equal(0, word.Words[0].Top - block.Location.Y, 1);
        Assert.Equal(30, block.Size.Height, 1);
    }

    /// <summary>
    /// In a block in the root with the given line height, "a" in an inline box of its own, and an
    /// empty inline-block of the given height aligned as given.
    /// </summary>
    private static (CssBox Block, CssBox Word, CssBox Box) Build(string lineHeight, int height, string verticalAlign)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var block = new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Width = "320px",
            LineHeight = lineHeight,
        };

        var word = new CssBox(block, null, BaseUrl);
        word.InheritStyle();
        word.Display = "inline";
        word.Text = "a".AsMemory();
        word.ParseToWords();

        var box = new CssBox(block, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = "8px",
            Height = height + "px",
            VerticalAlign = verticalAlign,
        };

        return (block, word, box);
    }

    private static void Layout(CssBox block)
    {
        var root = block.ParentBox!.ParentBox!;
        root.PerformLayout(root.LayoutEnvironment);
    }

    private sealed class FakeLayoutEnvironment : ILayoutEnvironment
    {
        private static readonly Broiler.Graphics.Text.ILayoutFont TheFont = new FakeFont();
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => TheFont;
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => new(8 * text.Length, 16);
        public void MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = text.Length; charFitWidth = 8 * text.Length; }
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
        public double Size => 12;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}

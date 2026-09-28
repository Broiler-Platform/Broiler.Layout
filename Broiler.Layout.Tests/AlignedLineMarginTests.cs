using System;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A centred or right-aligned line is aligned with the right margin of what ends it.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §16.2: <c>text-align</c> aligns the line's inline-level boxes within the line box, each
/// with its margins, as the flow placed them; CSS Text 3 §7.1: a line they overflow is not aligned.
/// The right margin of the box ending the line was left out, so a centred line stood half that
/// margin too far to the right and a right-aligned one the whole of it: a 100px inline-block with
/// <c>margin-right: 50px</c> stood 200px along a 300px right-aligned line, where browsers put it
/// 150px along.
/// </para>
/// <para>
/// Each block here is 300px wide with 20px lines. Words are 8px wide a letter and 16px tall.
/// </para>
/// </remarks>
public sealed class AlignedLineMarginTests
{
    private static readonly Uri BaseUrl = new("file:///aligned-line-margin.html");

    /// <summary>
    /// A 100px inline-block with <c>margin-right: 50px</c> on a centred line: the box and its margin
    /// are centred, and the box starts 75px along. It started 100px along, as if it had no margin.
    /// </summary>
    [Fact]
    public void A_Centred_Inline_Block_Is_Centred_With_Its_Right_Margin()
    {
        var block = Block(CssConstants.Center);
        var box = Box(block, "50px");
        Layout(block);

        Assert.Equal(75, box.Location.X - block.Location.X, 1);
    }

    /// <summary>
    /// The same box on a right-aligned line ends its margin at the line's right, starting 150px
    /// along. It started 200px along.
    /// </summary>
    [Fact]
    public void A_Right_Aligned_Inline_Block_Ends_Its_Right_Margin_At_The_Lines_Right()
    {
        var block = Block(CssConstants.Right);
        var box = Box(block, "50px");
        Layout(block);

        Assert.Equal(150, box.Location.X - block.Location.X, 1);
    }

    /// <summary>
    /// "aaa", then a span with <c>margin-right: 20px</c> holding "bbb", on a right-aligned line:
    /// the span's margin ends at the line's right, and "bbb" starts 256px along. It started 276px
    /// along.
    /// </summary>
    [Fact]
    public void A_Right_Aligned_Line_Ends_With_The_Right_Margin_Of_Its_Last_Span()
    {
        var block = Block(CssConstants.Right);
        Text(block, "aaa");
        var span = new CssBox(block, new HtmlTag("span", false, null), BaseUrl);
        span.InheritStyle();
        span.Display = CssConstants.Inline;
        span.MarginRight = "20px";
        Text(span, "bbb");
        Layout(block);

        Assert.Equal(256, Word(block, "bbb").Left - block.Location.X, 1);
    }

    /// <summary>
    /// A 322px image with 3px margins and a 1px border, 330px with them, on a centred line 328px
    /// wide: it overflows the line and starts at the line's start, its border 3px along. It was
    /// centred on its border box, 3.5px along.
    /// </summary>
    [Fact]
    public void An_Image_Whose_Margins_Overflow_A_Centred_Line_Starts_At_Its_Start()
    {
        var block = Block(CssConstants.Center, "328px");
        var image = new CssBoxImage(block, new HtmlTag("img", true, new System.Collections.Generic.Dictionary<string, string> { ["src"] = "i.png" }), BaseUrl)
        {
            Display = CssConstants.Inline,
            Width = "322px",
            Height = "10px",
            MarginLeft = "3px",
            MarginRight = "3px",
            BorderLeftWidth = "1px",
            BorderRightWidth = "1px",
            BorderLeftStyle = "solid",
            BorderRightStyle = "solid",
        };
        Layout(block);

        Assert.Equal(3, image.Location.X - block.Location.X, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a 100px inline-block with no margin on a centred line
    /// starts 100px along.
    /// </summary>
    [Fact]
    public void Control_A_Centred_Inline_Block_With_No_Margin()
    {
        var block = Block(CssConstants.Center);
        var box = Box(block, null);
        Layout(block);

        Assert.Equal(100, box.Location.X - block.Location.X, 1);
    }

    /// <summary>A block of the given width and alignment with 20px lines, in a block in the root.</summary>
    private static CssBox Block(string textAlign, string width = "300px")
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
            LineHeight = "20px",
            TextAlign = textAlign,
        };
    }

    private static void Layout(CssBox block)
    {
        var root = block.ParentBox!.ParentBox!;
        root.PerformLayout(root.LayoutEnvironment);
    }

    /// <summary>An empty 100 × 10px inline-block with the given right margin, if any.</summary>
    private static CssBox Box(CssBox parent, string? marginRight)
    {
        var box = new CssBox(parent, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = "100px",
            Height = "10px",
        };

        if (marginRight != null)
            box.MarginRight = marginRight;

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
        private static readonly Broiler.Graphics.Text.ILayoutFont TheFont = new FakeFont();
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => TheFont;
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => new(8 * text.Length, 16);
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

    private sealed class FakeFont : Broiler.Graphics.Text.ILayoutFont
    {
        public double Size => 12;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}

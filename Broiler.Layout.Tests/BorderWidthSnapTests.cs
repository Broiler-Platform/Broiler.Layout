using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A border is snapped to whole pixels: one thinner than a pixel is a pixel wide, and a wider one
/// is rounded down.
/// </summary>
/// <remarks>
/// <para>
/// CSS Values 4 §6.1.1, "snap as a border width": a width between 0 and 1 device pixel is rounded
/// up to 1, and one wider than 1 device pixel is rounded down to whole device pixels; a CSS pixel
/// is a device pixel here. Borders were as wide as given: a <c>0.5px</c> border made a 100px box
/// 101px wide, where browsers make it 102px with a 1px border.
/// </para>
/// <para>
/// Each box here is 100 × 10px, with a solid border of the given width on every side, in a block
/// in the root. Its font is 16px.
/// </para>
/// </remarks>
public sealed class BorderWidthSnapTests
{
    private static readonly Uri BaseUrl = new("file:///border-width-snap.html");

    /// <summary>
    /// A border thinner than a pixel, 0.5px, 0.1px or 0.05px, is 1px wide, and the box 102 × 12px.
    /// It was as wide as given.
    /// </summary>
    [Theory]
    [InlineData("0.5px")]
    [InlineData("0.1px")]
    [InlineData("0.05px")]
    public void A_Border_Thinner_Than_A_Pixel_Is_A_Pixel_Wide(string width)
    {
        var box = Lay(width);

        Assert.Equal(1, box.ActualBorderTopWidth, 2);
        Assert.Equal(1, box.ActualBorderLeftWidth, 2);
        Assert.Equal(102, box.Size.Width, 1);
        Assert.Equal(12, box.Size.Height, 1);
    }

    /// <summary>
    /// A border wider than a pixel is rounded down to whole pixels: 1.5px to 1, 2.7px to 2, 3.9px
    /// to 3, and 0.1em of the 16px font, 1.6px, to 1. They were as wide as given.
    /// </summary>
    [Theory]
    [InlineData("1.5px", 1)]
    [InlineData("2.7px", 2)]
    [InlineData("3.9px", 3)]
    [InlineData("0.1em", 1)]
    public void A_Wider_Border_Is_Rounded_Down_To_Whole_Pixels(string width, double snapped)
    {
        var box = Lay(width);

        Assert.Equal(snapped, box.ActualBorderBottomWidth, 2);
        Assert.Equal(snapped, box.ActualBorderRightWidth, 2);
        Assert.Equal(100 + 2 * snapped, box.Size.Width, 1);
        Assert.Equal(10 + 2 * snapped, box.Size.Height, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: a 1px, 3px or 0px border keeps its width, and a
    /// border with <c>border-style: none</c> has none.
    /// </summary>
    [Theory]
    [InlineData("1px", "solid", 1)]
    [InlineData("3px", "solid", 3)]
    [InlineData("0px", "solid", 0)]
    [InlineData("0.5px", "none", 0)]
    public void Control_Whole_Or_Absent_Borders(string width, string style, double expected)
    {
        var box = Lay(width, style);

        Assert.Equal(expected, box.ActualBorderTopWidth, 2);
        Assert.Equal(100 + 2 * expected, box.Size.Width, 1);
    }

    /// <summary>
    /// A 100 × 10px block with a border of <paramref name="width"/> and <paramref name="style"/> on
    /// every side, in a block in the root, laid out.
    /// </summary>
    private static CssBox Lay(string width, string style = "solid")
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var box = new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Width = "100px",
            Height = "10px",
            BorderTopWidth = width,
            BorderRightWidth = width,
            BorderBottomWidth = width,
            BorderLeftWidth = width,
            BorderTopStyle = style,
            BorderRightStyle = style,
            BorderBottomStyle = style,
            BorderLeftStyle = style,
        };

        root.PerformLayout(root.LayoutEnvironment);
        return box;
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

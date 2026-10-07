using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// Text whose only family is the generic <c>monospace</c>, sized by a keyword, is 13/16 of the size
/// that keyword gives other text, as browsers make it: <c>kbd</c> and <c>code</c> in 16px text are
/// 13px. A second family, or a size given as a length, leaves it alone. Measured in Chromium.
/// </summary>
/// <remarks>
/// It was 16px, a quarter larger than browsers draw it: reCAPTCHA's demo index sets its link to the
/// demo's sources in a <c>&lt;kbd&gt;</c>.
/// </remarks>
public sealed class MonospaceFontSizeTests
{
    private static readonly Uri BaseUrl = new("file:///monospace.html");

    [Theory(Timeout = 600000)]
    [InlineData("monospace", null, 9.75)]
    [InlineData("monospace", "16px", 12)]
    [InlineData("monospace, monospace", null, 12)]
    [InlineData("sans-serif", null, 12)]
    public void A_Keyword_Size_Is_Smaller_For_Monospace_Alone(string family, string? size, double points)
    {
        var root = new CssBox(null, new HtmlTag("html", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new SizedFontEnvironment(),
        };
        var body = new CssBox(root, new HtmlTag("body", false, null), BaseUrl) { Display = CssConstants.Block };
        var code = new CssBox(body, new HtmlTag("code", false, null), BaseUrl) { Display = CssConstants.Inline };
        body.InheritStyle();
        code.InheritStyle();
        code.FontFamily = family;
        if (size is not null)
            code.FontSize = size;

        Assert.Equal(points, code.ActualFont.Size, 3);
    }

    // Hands back a font of the size asked for, so the size layout asks for can be read off it.
    private sealed class SizedFontEnvironment : ILayoutEnvironment
    {
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => new SizedFont(size);
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

    private sealed class SizedFont(double size) : Broiler.Graphics.Text.ILayoutFont
    {
        public double Size => size;
        public double Height => size * 96 / 72;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}

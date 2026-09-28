using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// Inline-blocks that stand on a line's baseline stand on it together, and an inline-block aligned
/// any other way on the same line does not move them (CSS2.1 §10.8.1).
/// </summary>
/// <remarks>
/// <para>
/// Empty inline-blocks have their bottom margin edge for a baseline, and this engine stands the ones
/// aligned to the baseline on the lowest bottom among them. It took that bottom from every such box
/// not aligned <c>top</c> or <c>bottom</c>, so one aligned <c>middle</c>, <c>text-top</c>,
/// <c>super</c> or by a length set it with the bottom the flow left it, and pushed the boxes on the
/// baseline down to it.
/// </para>
/// <para>
/// Each line here holds an empty 8×60px inline-block aligned as given, then an 8×20px and an 8×30px
/// one on the baseline, in a 320px block; words are 8×16px.
/// </para>
/// </remarks>
public sealed class InlineBlockSharedBaselineTests
{
    private static readonly Uri BaseUrl = new("file:///inline-block-shared-baseline.html");

    /// <summary>
    /// Beside a 60px box aligned <c>middle</c>, <c>text-top</c>, <c>text-bottom</c>, <c>super</c>,
    /// <c>sub</c> or 10px up, the 20px and 30px boxes stand flush on the bottom of the 30px one: the
    /// 20px one is 10px lower than the 30px one, where the alignment of the 60px box leaves the line.
    /// Each was 30px lower, on the 60px box's bottom instead.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("middle", 28.3)]
    [InlineData("text-top", 10)]
    [InlineData("text-bottom", 48.7)]
    [InlineData("super", 64.9)]
    [InlineData("sub", 22.9)]
    [InlineData("10px", 62.9)]
    public void Boxes_On_The_Baseline_Stand_On_Their_Own_Bottom(string verticalAlign, float shortTop)
    {
        var (block, shortBox, other) = Lay(verticalAlign);

        Assert.Equal(shortTop, shortBox.Location.Y - block.Location.Y, 1);
        Assert.Equal(other.Location.Y + 10, shortBox.Location.Y, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: beside a 60px box on the baseline too, the 20px and
    /// 30px boxes stand on its bottom, 40px and 30px down; beside one aligned <c>top</c> or
    /// <c>bottom</c>, which were never counted, 10px and 0px down, or 40px and 30px.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("baseline", 40)]
    [InlineData("top", 10)]
    [InlineData("bottom", 40)]
    public void Control_A_Box_On_The_Baseline_Or_Aligned_To_The_Line(string verticalAlign, float shortTop)
    {
        var (block, shortBox, other) = Lay(verticalAlign);

        Assert.Equal(shortTop, shortBox.Location.Y - block.Location.Y, 1);
        Assert.Equal(other.Location.Y + 10, shortBox.Location.Y, 1);
    }

    /// <summary>
    /// A 320px block holding an 8×60px inline-block aligned by <paramref name="verticalAlign"/>, then
    /// an 8×20px and an 8×30px one on the baseline, laid out. Returns the block and the two boxes on
    /// the baseline.
    /// </summary>
    private static (CssBox Block, CssBox Short, CssBox Other) Lay(string verticalAlign)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };
        var block = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "320px" };
        var tall = InlineBlock(block, "60px");
        tall.VerticalAlign = verticalAlign;
        var shortBox = InlineBlock(block, "20px");
        var other = InlineBlock(block, "30px");

        root.PerformLayout(root.LayoutEnvironment);
        return (block, shortBox, other);
    }

    private static CssBox InlineBlock(CssBox parent, string height) =>
        new(parent, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = "8px",
            Height = height,
        };

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

using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A percentage <c>padding-top</c> or <c>padding-bottom</c> resolves against the width a box is laid
/// out at (CSS2.1 §8.4), and so follows it when the box is laid out again at another.
/// </summary>
/// <remarks>
/// The used value was cached on its first read and never resolved again. A column flex container
/// lays its items out at their shrink-to-fit widths first and stretches them to its own width
/// afterwards, so a <c>padding-top: 50%</c> box in an empty item, the intrinsic-ratio pattern of
/// responsive embeds, kept the 0px its zero-width first layout gave it. Words are 8px wide and 16px
/// tall.
/// </remarks>
public sealed class PercentagePaddingRelayoutTests
{
    private static readonly Uri BaseUrl = new("file:///percentage-padding-relayout.html");

    /// <summary>
    /// In an item a 320px column stretches, a box with <c>padding-top: 50%</c>, or
    /// <c>padding-bottom: 50%</c>, or 25% of each, is 160px tall, and so is the item. Each was 0.
    /// </summary>
    [Theory]
    [InlineData("50%", "0")]
    [InlineData("0", "50%")]
    [InlineData("25%", "25%")]
    public void A_Percentage_Padding_Follows_The_Stretch(string top, string bottom)
    {
        var root = Root();
        var column = new CssBox(root, Div(), BaseUrl)
        {
            Display = "flex",
            FlexDirection = "column",
            Width = "320px",
        };
        var item = new CssBox(column, Div(), BaseUrl) { Display = CssConstants.Block };
        var box = new CssBox(item, Div(), BaseUrl)
        {
            Display = CssConstants.Block,
            PaddingTop = top,
            PaddingBottom = bottom,
        };

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(320, box.Size.Width, 1);
        Assert.Equal(160, Height(box), 1);
        Assert.Equal(160, Height(item), 1);
    }

    /// <summary>
    /// A tree laid out again after its container's width changes, as on a resize: a box with
    /// <c>padding-top: 50%</c> is 50px tall at 100px and 160px at 320px. It stayed at 50.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Percentage_Padding_Follows_A_Second_Layout_At_Another_Width()
    {
        var root = Root();
        var container = new CssBox(root, Div(), BaseUrl) { Display = CssConstants.Block, Width = "100px" };
        var box = new CssBox(container, Div(), BaseUrl) { Display = CssConstants.Block, PaddingTop = "50%" };

        root.PerformLayout(root.LayoutEnvironment);
        Assert.Equal(50, Height(box), 1);

        container.Width = "320px";
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(160, Height(box), 1);
    }

    /// <summary>
    /// Controls, which pass before and after: a length keeps its value at either width, 20px, and
    /// a percentage resolved on a box's first layout is right already, 160px of a 320px width.
    /// </summary>
    [Theory]
    [InlineData("20px", 20)]
    [InlineData("50%", 160)]
    public void Control_A_Padding_Resolved_Once(string top, float height)
    {
        var root = Root();
        var container = new CssBox(root, Div(), BaseUrl) { Display = CssConstants.Block, Width = "320px" };
        var box = new CssBox(container, Div(), BaseUrl) { Display = CssConstants.Block, PaddingTop = top };

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(height, Height(box), 1);
    }

    private static CssBox Root() =>
        new(null, Div(), BaseUrl)
        {
            Display = CssConstants.Block,
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

    private static HtmlTag Div() => new("div", false, null);

    private static double Height(CssBox box) => box.ActualBottom - box.Location.Y;

    // Minimal ILayoutEnvironment: every word 8px wide and 16px tall, a space 4px, a 1024×768 viewport.
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

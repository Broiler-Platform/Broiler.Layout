using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A percentage <c>left</c> or <c>right</c> of a relatively positioned box refers to the width of its
/// containing block, and a percentage <c>top</c> or <c>bottom</c> to its height (CSS2.1 §9.3.2): its
/// content box's. A percentage <c>top</c> or <c>bottom</c> in a containing block of <c>auto</c> height
/// is <c>auto</c>, as Chromium has it. Each expectation was measured in Chromium.
/// </summary>
/// <remarks>
/// Each referred to the box's own size. reCAPTCHA's image challenge clips an image four times as large
/// as each tile and shifts it with <c>left: -100%</c> and <c>top: -100%</c> to show its part: shifted
/// by the image's own size instead, every tile but the first, which is not shifted, showed nothing.
/// Words are 8px wide and 16px tall, in a 1024 × 768 viewport.
/// </remarks>
public sealed class RelativePercentageOffsetTests
{
    private static readonly Uri BaseUrl = new("file:///relative-percentage-offset.html");

    /// <summary>
    /// A 50 × 20 box in a 200 × 100 containing block, at <c>left: 50%; top: 50%</c>, is 100px across and
    /// 50px down from where the flow puts it; at <c>right: 25%; bottom: 50%</c> in a 60px-tall one, 50px
    /// back and 30px up.
    /// </summary>
    [Theory]
    [InlineData("100px", "50%", null, "50%", null, 100, 50)]
    [InlineData("60px", null, "25%", null, "50%", -50, -30)]
    public void A_Percentage_Offset_Refers_To_The_Containing_Block(
        string containerHeight, string? left, string? right, string? top, string? bottom, float x, float y)
    {
        var root = Root();
        var container = Block(root, "200px", containerHeight);
        var box = Relative(container, left, right, top, bottom);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(x, box.Location.X - container.Location.X, 1);
        Assert.Equal(y, box.Location.Y - container.Location.Y, 1);
    }

    /// <summary>
    /// The percentages refer to the containing block's content box: in a 200 × 60 one with 10px of
    /// padding and a 5px border, <c>left: 100%; top: 100%</c> puts the box 215px across and 75px down
    /// from the containing block's border edge, 200 and 60 past where the flow puts it.
    /// </summary>
    [Fact]
    public void A_Percentage_Offset_Refers_To_The_Content_Box()
    {
        var root = Root();
        var container = Block(root, "200px", "60px");
        container.PaddingLeft = container.PaddingTop = container.PaddingRight = container.PaddingBottom = "10px";
        container.BorderLeftWidth = container.BorderTopWidth = container.BorderRightWidth = container.BorderBottomWidth = "5px";
        container.BorderLeftStyle = container.BorderTopStyle = container.BorderRightStyle = container.BorderBottomStyle = "solid";
        var box = Relative(container, "100%", null, "100%", null);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(215, box.Location.X - container.Location.X, 1);
        Assert.Equal(75, box.Location.Y - container.Location.Y, 1);
    }

    /// <summary>
    /// In a containing block of <c>auto</c> height, <c>top: 50%</c> is <c>auto</c> and the box stays
    /// where the flow puts it, while <c>left: -100%</c> of the 200px width moves it 200px back.
    /// </summary>
    [Fact]
    public void A_Percentage_Top_In_A_Containing_Block_Of_Auto_Height_Is_Auto()
    {
        var root = Root();
        var container = Block(root, "200px", null);
        var box = Relative(container, "-100%", null, "50%", null);
        new CssBox(container, Div(), BaseUrl) { Display = CssConstants.Block, Height = "80px" };

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(-200, box.Location.X - container.Location.X, 1);
        Assert.Equal(0, box.Location.Y - container.Location.Y, 1);
    }

    /// <summary>
    /// The box after one moved by <c>top: 50%</c> goes where the flow puts it, right below the moved
    /// box's place in the flow: the offset taken back off to find that place is the one that was given.
    /// </summary>
    [Fact]
    public void The_Next_Box_Follows_The_Moved_Box_Place_In_The_Flow()
    {
        var root = Root();
        var container = Block(root, "200px", "100px");
        var moved = Relative(container, null, null, "50%", null);
        var next = new CssBox(container, Div(), BaseUrl) { Display = CssConstants.Block, Height = "10px" };

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(50, moved.Location.Y - container.Location.Y, 1);
        Assert.Equal(20, next.Location.Y - container.Location.Y, 1);
    }

    private static CssBox Relative(CssBox parent, string? left, string? right, string? top, string? bottom)
    {
        var box = new CssBox(parent, Div(), BaseUrl)
        {
            Display = CssConstants.Block,
            Position = CssConstants.Relative,
            Width = "50px",
            Height = "20px",
        };

        if (left is not null) box.Left = left;
        if (right is not null) box.Right = right;
        if (top is not null) box.Top = top;
        if (bottom is not null) box.Bottom = bottom;
        return box;
    }

    private static CssBox Block(CssBox parent, string width, string? height)
    {
        var box = new CssBox(parent, Div(), BaseUrl) { Display = CssConstants.Block, Width = width };
        if (height is not null)
            box.Height = height;
        return box;
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

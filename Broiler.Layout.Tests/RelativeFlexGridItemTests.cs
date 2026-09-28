using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A relatively positioned flex or grid item is shifted by its offset from where the flex or grid
/// layout places it, and moves nothing around it (CSS2.1 §9.4.3).
/// </summary>
/// <remarks>
/// <para>
/// An item's own layout applies its relative offset. A row flex container, and a grid container,
/// then move each item to its place by the distance from where that layout left it, which took the
/// offset away again: an item with <c>top: 10px; left: 20px</c> sat where it would have sat without
/// them. A column flex container and a block keep the offset.
/// </para>
/// <para>
/// Each container here is 320px wide in a root, and each item 50×20px, the one shifted first.
/// </para>
/// </remarks>
public sealed class RelativeFlexGridItemTests
{
    private static readonly Uri BaseUrl = new("file:///relative-flex-grid-item.html");

    /// <summary>
    /// In a row flex container, an item with <c>top: 10px; left: 20px</c> is 20px across and 10px
    /// down from its place, or 20px back and 10px up with <c>right: 20px; bottom: 10px</c>; the item
    /// after it stays at its own place, 50px across, and the container 20px tall. The item stayed at
    /// its place.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData(false, 20, 10)]
    [InlineData(true, -20, -10)]
    public void A_Row_Flex_Item_Is_Shifted_By_Its_Offset(bool fromRightAndBottom, float x, float y)
    {
        var (root, container) = Container(c => c.Display = "flex");
        var item = Item(container, fromRightAndBottom);
        var next = Item(container);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(x, item.Location.X - container.Location.X, 1);
        Assert.Equal(y, item.Location.Y - container.Location.Y, 1);
        Assert.Equal(50, next.Location.X - container.Location.X, 1);
        Assert.Equal(0, next.Location.Y - container.Location.Y, 1);
        Assert.Equal(20, container.Size.Height, 1);
    }

    /// <summary>
    /// In a grid container with two 100px columns, an item with <c>top: 10px; left: 20px</c> is
    /// 20px across and 10px down from its area's corner; the item after it stays at its own, 100px
    /// across, and the container 20px tall. The item stayed at its area's corner.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Grid_Item_Is_Shifted_By_Its_Offset()
    {
        var (root, container) = Container(c =>
        {
            c.Display = "grid";
            c.GridTemplateColumns = "100px 100px";
        });
        var item = Item(container, fromRightAndBottom: false);
        var next = Item(container);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(20, item.Location.X - container.Location.X, 1);
        Assert.Equal(10, item.Location.Y - container.Location.Y, 1);
        Assert.Equal(100, next.Location.X - container.Location.X, 1);
        Assert.Equal(0, next.Location.Y - container.Location.Y, 1);
        Assert.Equal(20, container.Size.Height, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: in a column flex container, or a block, an item with
    /// <c>top: 10px; left: 20px</c> is 20px across and 10px down from its place, and the item after
    /// it 20px down, below the first item's place.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("column")]
    [InlineData(null)]
    public void Control_A_Column_Flex_Item_Or_A_Block(string? flexDirection)
    {
        var (root, container) = Container(c =>
        {
            if (flexDirection != null)
            {
                c.Display = "flex";
                c.FlexDirection = flexDirection;
            }
        });
        var item = Item(container, fromRightAndBottom: false);
        var next = Item(container);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(20, item.Location.X - container.Location.X, 1);
        Assert.Equal(10, item.Location.Y - container.Location.Y, 1);
        Assert.Equal(20, next.Location.Y - container.Location.Y, 1);
    }

    /// <summary>A 320px block in a root, styled by <paramref name="style"/>.</summary>
    private static (CssBox Root, CssBox Container) Container(Action<CssBox> style)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };
        var container = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "320px" };
        style(container);
        return (root, container);
    }

    /// <summary>
    /// A 50×20px block in <paramref name="parent"/>; relatively positioned when
    /// <paramref name="fromRightAndBottom"/> is given, by <c>top: 10px; left: 20px</c>, or by
    /// <c>bottom: 10px; right: 20px</c> when it is true.
    /// </summary>
    private static CssBox Item(CssBox parent, bool? fromRightAndBottom = null)
    {
        var item = new CssBox(parent, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Width = "50px",
            Height = "20px",
        };

        if (fromRightAndBottom is bool opposite)
        {
            item.Position = CssConstants.Relative;
            if (opposite)
            {
                item.Right = "20px";
                item.Bottom = "10px";
            }
            else
            {
                item.Left = "20px";
                item.Top = "10px";
            }
        }

        return item;
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

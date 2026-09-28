using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A percentage margin or padding refers to the width of the box's containing block (CSS2.1 §8.3,
/// §8.4): the content box of the nearest block container for a box in flow or a float (§10.1),
/// the padding box of the positioned ancestor for an absolutely positioned box, and the viewport
/// for a fixed one.
/// </summary>
/// <remarks>
/// Every percentage margin and padding resolved against the box's own width, which is the
/// containing block's only for a box that fills it. A 100px wide box with <c>padding-top: 50%</c>
/// in a 320px block, the intrinsic-ratio pattern of responsive embeds given a width, was 50px tall
/// where browsers make it 160. Words are 8px wide and 16px tall, in a 1024 × 768 viewport.
/// </remarks>
public sealed class PercentageContainingBlockTests
{
    private static readonly Uri BaseUrl = new("file:///percentage-containing-block.html");

    /// <summary>
    /// A 100px wide block, or inline-block, with <c>padding-top: 50%</c> in a 320px block is 160px
    /// tall. Each was 50.
    /// </summary>
    [Theory]
    [InlineData("block")]
    [InlineData("inline-block")]
    public void A_Percentage_Padding_Refers_To_The_Containing_Block(string display)
    {
        var root = Root();
        var container = Block(root, "320px");
        var box = new CssBox(container, Div(), BaseUrl) { Display = display, Width = "100px", PaddingTop = "50%" };

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(100, box.Size.Width, 1);
        Assert.Equal(160, Height(box), 1);
    }

    /// <summary>
    /// A 100px wide block with <c>margin-left: 50%</c> in a 320px block starts 160px in, and a
    /// 100px wide float with <c>margin-left: 10%</c> 32px in. They started 50px and 10px in.
    /// </summary>
    [Theory]
    [InlineData("none", "50%", 160)]
    [InlineData("left", "10%", 32)]
    public void A_Percentage_Margin_Refers_To_The_Containing_Block(string @float, string marginLeft, float left)
    {
        var root = Root();
        var container = Block(root, "320px");
        var box = new CssBox(container, Div(), BaseUrl)
        {
            Display = CssConstants.Block,
            Float = @float,
            Width = "100px",
            Height = "10px",
            MarginLeft = marginLeft,
        };

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(left, box.Location.X - container.Location.X, 1);
    }

    /// <summary>
    /// A block with 20px side margins in a 320px block is 280px wide, and its
    /// <c>padding-left: 10%</c> is 32px, a tenth of the 320, so its content starts 32px in. It
    /// was 28, a tenth of the box's own width.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Box_Narrower_Than_Its_Container_Takes_A_Tenth_Of_The_Container()
    {
        var root = Root();
        var container = Block(root, "320px");
        var box = new CssBox(container, Div(), BaseUrl)
        {
            Display = CssConstants.Block,
            MarginLeft = "20px",
            MarginRight = "20px",
            PaddingLeft = "10%",
        };
        var content = new CssBox(box, Div(), BaseUrl) { Display = CssConstants.Block, Height = "5px" };

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(280, box.Size.Width, 1);
        Assert.Equal(32, content.Location.X - box.Location.X, 1);
    }

    /// <summary>
    /// A 100px wide item with <c>padding-top: 50%</c> in a 320px row flex container is 160px tall.
    /// It was 50, half the item's own width.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Flex_Item_Refers_To_The_Flex_Container()
    {
        var root = Root();
        var row = new CssBox(root, Div(), BaseUrl) { Display = "flex", Width = "320px" };
        var item = new CssBox(row, Div(), BaseUrl) { Display = CssConstants.Block, Width = "100px", PaddingTop = "50%" };

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(100, item.Size.Width, 1);
        Assert.Equal(160, Height(item), 1);
    }

    /// <summary>
    /// In a block 300px wide with 10px side padding, a 100px wide box in flow with
    /// <c>padding-top: 50%</c> is 150px tall, half the content box, and an absolutely positioned
    /// one, when the block is positioned, 160px, half the padding box. Each was 50.
    /// </summary>
    [Theory]
    [InlineData("static", 150)]
    [InlineData("absolute", 160)]
    public void The_Containing_Block_Is_The_Content_Box_In_Flow_And_The_Padding_Box_Out_Of_It(string position, float height)
    {
        var root = Root();
        var container = new CssBox(root, Div(), BaseUrl)
        {
            Display = CssConstants.Block,
            Position = CssConstants.Relative,
            Width = "300px",
            Height = "200px",
            PaddingLeft = "10px",
            PaddingRight = "10px",
        };
        var box = new CssBox(container, Div(), BaseUrl)
        {
            Display = CssConstants.Block,
            Position = position,
            Top = "0",
            Left = "0",
            Width = "100px",
            PaddingTop = "50%",
        };

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(100, box.Size.Width, 1);
        Assert.Equal(height, Height(box), 1);
    }

    /// <summary>
    /// A fixed 100px wide box with <c>padding-top: 10%</c> is 102.4px tall, a tenth of the 1024px
    /// viewport, however narrow its parent. It was 10.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Fixed_Box_Refers_To_The_Viewport()
    {
        var root = Root();
        var container = Block(root, "320px");
        var box = new CssBox(container, Div(), BaseUrl)
        {
            Display = CssConstants.Block,
            Position = CssConstants.Fixed,
            Top = "0",
            Left = "0",
            Width = "100px",
            PaddingTop = "10%",
        };

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(102.4, Height(box), 1);
    }

    /// <summary>
    /// A float, an absolutely positioned box and an inline-block, each of auto width, holding a
    /// block with <c>padding-left: 10%</c>, or <c>margin-left: 10%</c>, around a 100px wide box,
    /// are 100px wide, as wide as that box, and it starts 10px in, a tenth of that. Measured for
    /// its width, a box's content counts no percentage margin or padding, which would refer to the
    /// width being measured (CSS Sizing 3 §5.2.1). The float and the absolutely positioned box take
    /// the 320px they have room for before they measure their content, and would each be 132px
    /// wide, the box 13.2px in. The padding was 0, and the margin 9px, a tenth of the block's own
    /// width.
    /// </summary>
    [Theory]
    [InlineData("float", "padding")]
    [InlineData("float", "margin")]
    [InlineData("absolute", "padding")]
    [InlineData("inline-block", "padding")]
    public void A_Shrink_To_Fit_Box_Measures_Its_Content_Without_The_Percentage(string kind, string edge)
    {
        var root = Root();
        var container = new CssBox(root, Div(), BaseUrl)
        {
            Display = CssConstants.Block,
            Position = CssConstants.Relative,
            Width = "320px",
            Height = "50px",
        };
        var box = new CssBox(container, Div(), BaseUrl)
        {
            Display = kind == "inline-block" ? "inline-block" : CssConstants.Block,
            Float = kind == "float" ? "left" : CssConstants.None,
            Position = kind == "absolute" ? "absolute" : "static",
            Top = "0",
            Left = "0",
        };
        var block = new CssBox(box, Div(), BaseUrl) { Display = CssConstants.Block };
        if (edge == "padding")
            block.PaddingLeft = "10%";
        else
            block.MarginLeft = "10%";
        var content = new CssBox(block, Div(), BaseUrl) { Display = CssConstants.Block, Width = "100px", Height = "5px" };

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(100, box.Size.Width, 1);
        Assert.Equal(10, content.Location.X - box.Location.X, 1);
    }

    /// <summary>
    /// A tree laid out again after its container's width changes, as on a resize: a box's
    /// <c>padding-left: 10%</c> is 10px in a 100px block and 32px in a 320px one. It stayed at 10.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Percentage_Side_Padding_Follows_A_Second_Layout_At_Another_Width()
    {
        var root = Root();
        var container = Block(root, "100px");
        var box = new CssBox(container, Div(), BaseUrl) { Display = CssConstants.Block, PaddingLeft = "10%" };
        var content = new CssBox(box, Div(), BaseUrl) { Display = CssConstants.Block, Height = "5px" };

        root.PerformLayout(root.LayoutEnvironment);
        Assert.Equal(10, content.Location.X - box.Location.X, 1);

        container.Width = "320px";
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(32, content.Location.X - box.Location.X, 1);
    }

    /// <summary>
    /// The same for a margin with a percentage in a <c>calc()</c>: a 50px wide box with
    /// <c>margin-left: calc(10% + 2px)</c> starts 12px in in a 100px block and 34px in in a 320px
    /// one. It started 7px in in both, from a tenth of its own width, and a margin cached like a
    /// length would stay at 12.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Calc_Margin_Follows_A_Second_Layout_At_Another_Width()
    {
        var root = Root();
        var container = Block(root, "100px");
        var box = new CssBox(container, Div(), BaseUrl)
        {
            Display = CssConstants.Block,
            Width = "50px",
            Height = "5px",
            MarginLeft = "calc(10% + 2px)",
        };

        root.PerformLayout(root.LayoutEnvironment);
        Assert.Equal(12, box.Location.X - container.Location.X, 1);

        container.Width = "320px";
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(34, box.Location.X - container.Location.X, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: a box that fills its 320px container, where its own
    /// width is the container's, is 160px tall with <c>padding-top: 50%</c>; and a length keeps its
    /// value, 20px, on a 100px wide box.
    /// </summary>
    [Theory]
    [InlineData("auto", "50%", 160)]
    [InlineData("100px", "20px", 20)]
    public void Control_A_Box_That_Fills_Its_Container_Or_A_Length(string width, string paddingTop, float height)
    {
        var root = Root();
        var container = Block(root, "320px");
        var box = new CssBox(container, Div(), BaseUrl) { Display = CssConstants.Block, Width = width, PaddingTop = paddingTop };

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(height, Height(box), 1);
    }

    private static CssBox Block(CssBox parent, string width) =>
        new(parent, Div(), BaseUrl) { Display = CssConstants.Block, Width = width };

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

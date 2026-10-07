using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An absolutely positioned box whose height or place depends on its containing block's height --
/// both <c>top</c> and <c>bottom</c>, <c>bottom</c> alone, a percentage <c>top</c> or <c>height</c> --
/// gets them from that height when the containing block's height comes from its content.
/// </summary>
/// <remarks>
/// <para>
/// An absolutely positioned box is laid out where it stands among its containing block's children,
/// before the containing block has a height when its height comes from its content. With
/// <c>top: 0; bottom: 0</c> such a box was as tall as nothing; with <c>bottom: 0</c> it stood above
/// its containing block; <c>top: 50%</c> did not move it and <c>height: 50%</c> was nothing. reCAPTCHA
/// marks a selected image tile with a tick drawn by a box with all four insets 0 over the tile, and
/// no tick was drawn.
/// </para>
/// <para>
/// Each box here is 20px wide at <c>left: 0</c> in a 200px block with <c>position: relative</c> and
/// an auto height, after an 80px tall block, in a body in a root. Every expected answer is Chromium's,
/// measured on the same arrangement.
/// </para>
/// </remarks>
public sealed class AbsposContentHeightContainingBlockTests
{
    private static readonly Uri BaseUrl = new("file:///abspos-content-height.html");

    /// <summary>With <c>top: 0; bottom: 0</c> the box is 80px tall, as its containing block is. It was 0px tall.</summary>
    [Fact]
    public void Top_And_Bottom_Insets_Make_It_As_Tall_As_The_Containing_Block()
    {
        var (root, box) = Build(b => b.Top = b.Bottom = "0");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(0, box.Location.Y, 1);
        Assert.Equal(80, box.Size.Height, 1);
    }

    /// <summary>
    /// With <c>top: 10px; bottom: 10px</c> and 5px of padding, its border box is 60px tall, 10px down.
    /// It was 10px tall, its padding.
    /// </summary>
    [Fact]
    public void The_Insets_And_Padding_Come_Off_The_Containing_Blocks_Height()
    {
        var (root, box) = Build(b =>
        {
            b.Top = b.Bottom = "10px";
            b.PaddingTop = b.PaddingBottom = "5px";
        });
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(10, box.Location.Y, 1);
        Assert.Equal(60, box.Size.Height, 1);
    }

    /// <summary>With <c>bottom: 0; height: 20px</c> the box stands at the containing block's foot, 60px down. It stood 20px above its top.</summary>
    [Fact]
    public void A_Bottom_Inset_Puts_It_At_The_Containing_Blocks_Foot()
    {
        var (root, box) = Build(b =>
        {
            b.Bottom = "0";
            b.Height = "20px";
        });
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(60, box.Location.Y, 1);
        Assert.Equal(20, box.Size.Height, 1);
    }

    /// <summary>With <c>top: 50%</c> the box is 40px down. It was at the top.</summary>
    [Fact]
    public void A_Percentage_Top_Is_Of_The_Containing_Blocks_Height()
    {
        var (root, box) = Build(b =>
        {
            b.Top = "50%";
            b.Height = "10px";
        });
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(40, box.Location.Y, 1);
    }

    /// <summary>With <c>top: 0; height: 50%</c> the box is 40px tall. It was 0px tall.</summary>
    [Fact]
    public void A_Percentage_Height_Is_Of_The_Containing_Blocks_Height()
    {
        var (root, box) = Build(b =>
        {
            b.Top = "0";
            b.Height = "50%";
        });
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(40, box.Size.Height, 1);
    }

    /// <summary>Laid out a second time, the box with both insets is 80px tall still.</summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Height()
    {
        var (root, box) = Build(b => b.Top = b.Bottom = "0");
        root.PerformLayout(root.LayoutEnvironment);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(0, box.Location.Y, 1);
        Assert.Equal(80, box.Size.Height, 1);
    }

    /// <summary>
    /// Inside a block with no position between it and its containing block, the box is as tall as its
    /// containing block still.
    /// </summary>
    [Fact]
    public void A_Box_Further_Down_The_Tree_Is_Sized_Too()
    {
        var (root, box) = Build(b => b.Top = b.Bottom = "0", wrapped: true);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(80, box.Size.Height, 1);
    }

    /// <summary>A control, which passes before and after: in a containing block 80px tall by its own height, the box is 80px tall.</summary>
    [Fact]
    public void Control_A_Containing_Block_With_A_Height()
    {
        var (root, box) = Build(b => b.Top = b.Bottom = "0", containingHeight: "80px");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(80, box.Size.Height, 1);
    }

    /// <summary>
    /// In a 200px block with <c>position: relative</c> and the given height, auto when none is given,
    /// after an 80px tall block, an empty 20px wide box with <c>position: absolute; left: 0</c>, styled
    /// by <paramref name="style"/> -- inside a block with no position of its own when
    /// <paramref name="wrapped"/>. Returns the root and the box.
    /// </summary>
    private static (CssBox Root, CssBox Box) Build(Action<CssBox> style, string? containingHeight = null, bool wrapped = false)
    {
        var root = new CssBox(null, new HtmlTag("html", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("body", false, null), BaseUrl) { Display = "block" };
        var containing = new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Position = "relative",
            Width = "200px",
            Height = containingHeight ?? CssConstants.Auto,
        };

        _ = new CssBox(containing, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Height = "80px" };

        var parent = wrapped
            ? new CssBox(containing, new HtmlTag("div", false, null), BaseUrl) { Display = "block" }
            : containing;

        var box = new CssBox(parent, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Position = CssConstants.Absolute,
            Left = "0",
            Width = "20px",
        };
        style(box);

        return (root, box);
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
        public double Size => 16;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}

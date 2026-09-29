using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An absolutely or fixed positioned box with <c>top</c>, <c>bottom</c> and an auto height, and an
/// <c>align-self</c> other than <c>normal</c> or <c>stretch</c>, is as tall as its content, and its
/// margin box is aligned between the insets.
/// </summary>
/// <remarks>
/// <para>
/// CSS Box Alignment 3 §6.1: such a box is sized as <c>fit-content</c> in the space between its
/// insets, which in the block axis is its content height, and aligned there. The engine stretched
/// it between the insets, then measured it again from its children's bottom edges, which says
/// nothing of its lines: "x" with <c>top: 0; bottom: 0; align-self: center</c> stayed 100px tall at
/// the top of a 100px containing block, where browsers make it a line tall, in the middle. It
/// aligned the border box and then added the top margin.
/// </para>
/// <para>
/// Each box here is in a 500px wide, 100px tall block with <c>position: relative</c>, with
/// <c>left: 0</c>. Words are 16px tall and 8px wide a letter, and a line is 16px tall.
/// </para>
/// </remarks>
public sealed class AbsposAlignSelfTests
{
    private static readonly Uri BaseUrl = new("file:///abspos-align-self.html");

    /// <summary>
    /// With <c>top: 0; bottom: 0</c>, an x is a line tall, 16px: 42px down for <c>center</c>, 84px
    /// for <c>end</c>, and at the top for <c>start</c>. It was 100px tall at the top.
    /// </summary>
    [Theory]
    [InlineData("center", 42)]
    [InlineData("end", 84)]
    [InlineData("start", 0)]
    public void An_X_Is_Aligned_Between_The_Insets(string alignSelf, float y)
    {
        var (root, containing, box) = Build(alignSelf);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(y, box.Location.Y - containing.Location.Y, 1);
        Assert.Equal(16, box.Size.Height, 1);
    }

    /// <summary>
    /// With <c>top: 10px; bottom: 30px</c>, a centred x is 32px down, in the middle of the 60px
    /// between them. It was 60px tall, 10px down.
    /// </summary>
    [Fact]
    public void It_Is_Centred_Between_The_Insets_Not_In_The_Block()
    {
        var (root, containing, box) = Build("center", b =>
        {
            b.Top = "10px";
            b.Bottom = "30px";
        });
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(32, box.Location.Y - containing.Location.Y, 1);
        Assert.Equal(16, box.Size.Height, 1);
    }

    /// <summary>
    /// Its margin box is aligned. With <c>margin-top: 20px</c>, the centred margin box, 36px tall,
    /// is 32px down, so the x is 52px down; it was 80px tall, 20px down. With
    /// <c>margin-bottom: 20px</c> and <c>end</c>, the x is 64px down; it was 80px tall at the top.
    /// </summary>
    [Theory]
    [InlineData("center", "20px", "0", 52)]
    [InlineData("end", "0", "20px", 64)]
    public void Its_Margin_Box_Is_Aligned(string alignSelf, string marginTop, string marginBottom, float y)
    {
        var (root, containing, box) = Build(alignSelf, b =>
        {
            b.MarginTop = marginTop;
            b.MarginBottom = marginBottom;
        });
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(y, box.Location.Y - containing.Location.Y, 1);
        Assert.Equal(16, box.Size.Height, 1);
    }

    /// <summary>
    /// With 5px of padding above and below, the centred box is 26px tall, 37px down. It was 100px
    /// tall at the top.
    /// </summary>
    [Fact]
    public void Its_Padding_Counts()
    {
        var (root, containing, box) = Build("center", b => b.PaddingTop = b.PaddingBottom = "5px");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(37, box.Location.Y - containing.Location.Y, 1);
        Assert.Equal(26, box.Size.Height, 1);
    }

    /// <summary>
    /// A positioned child 60px tall does not count: the centred box around an x and that child is
    /// 16px tall, 42px down. It was 100px tall at the top.
    /// </summary>
    [Fact]
    public void A_Positioned_Child_Does_Not_Count()
    {
        var (root, containing, box) = Build("center");
        _ = new CssBox(box, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Position = CssConstants.Absolute,
            Top = "0",
            Left = "0",
            Width = "10px",
            Height = "60px",
        };
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(42, box.Location.Y - containing.Location.Y, 1);
        Assert.Equal(16, box.Size.Height, 1);
    }

    /// <summary>
    /// Two lines, "x" and "y" in an 8px wide box, make it 32px tall, 34px down. It was 100px tall
    /// at the top.
    /// </summary>
    [Fact]
    public void Its_Lines_Count()
    {
        var (root, containing, box) = Build("center", b => b.Width = "8px", "x y");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(34, box.Location.Y - containing.Location.Y, 1);
        Assert.Equal(32, box.Size.Height, 1);
    }

    /// <summary>
    /// <c>min-height: 50px</c> makes the centred box 50px tall, 25px down. It was 100px tall at
    /// the top.
    /// </summary>
    [Fact]
    public void Min_Height_Counts()
    {
        var (root, containing, box) = Build("center", b => b.MinHeight = "50px");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(25, box.Location.Y - containing.Location.Y, 1);
        Assert.Equal(50, box.Size.Height, 1);
    }

    /// <summary>
    /// A fixed box with <c>top: 0; bottom: 0</c> is centred in the 768px tall viewport, 376px
    /// down. It was 768px tall.
    /// </summary>
    [Fact]
    public void A_Fixed_Box_Is_Centred_In_The_Viewport()
    {
        var (root, _, box) = Build("center", b => b.Position = CssConstants.Fixed);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(376, box.Location.Y, 1);
        Assert.Equal(16, box.Size.Height, 1);
    }

    /// <summary>
    /// Laid out a second time, the centred x is 42px down, 16px tall still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Box()
    {
        var (root, containing, box) = Build("center");
        root.PerformLayout(root.LayoutEnvironment);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(42, box.Location.Y - containing.Location.Y, 1);
        Assert.Equal(16, box.Size.Height, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: <c>stretch</c> and <c>normal</c> stretch the box
    /// between the insets, 100px; a centred box with <c>height: 30px</c> is 35px down.
    /// </summary>
    [Theory]
    [InlineData("stretch", null, 0, 100)]
    [InlineData("normal", null, 0, 100)]
    [InlineData("center", "30px", 35, 30)]
    public void Control_Stretched_Or_With_A_Height(string alignSelf, string? height, float y, float boxHeight)
    {
        var (root, containing, box) = Build(alignSelf, b =>
        {
            if (height != null)
                b.Height = height;
        });
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(y, box.Location.Y - containing.Location.Y, 1);
        Assert.Equal(boxHeight, box.Size.Height, 1);
    }

    /// <summary>
    /// In a 500px wide, 100px tall block with <c>position: relative</c>, a box with
    /// <c>position: absolute; top: 0; bottom: 0; left: 0</c> and the given <c>align-self</c>,
    /// styled by <paramref name="style"/>, holding <paramref name="text"/>. Returns the root, the
    /// block and the box.
    /// </summary>
    private static (CssBox Root, CssBox Containing, CssBox Box) Build(string alignSelf, Action<CssBox>? style = null, string text = "x")
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
            Width = "500px",
            Height = "100px",
        };

        var box = new CssBox(containing, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Position = CssConstants.Absolute,
            Top = "0",
            Bottom = "0",
            Left = "0",
            AlignSelf = alignSelf,
        };
        style?.Invoke(box);

        var word = new CssBox(box, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        word.ParseToWords();

        return (root, containing, box);
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

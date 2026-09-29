using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An absolutely or fixed positioned box with <c>left</c>, <c>right</c> and an auto width, and a
/// <c>justify-self</c> other than <c>normal</c> or <c>stretch</c>, is as wide as its content fits,
/// and its margin box is aligned between the insets.
/// </summary>
/// <remarks>
/// <para>
/// CSS Box Alignment 3 §6.1: such a box is sized as <c>fit-content</c> in the space between its
/// insets, and aligned there. The engine stretched it across that space, then shrank it to the
/// right edge of its widest child, which says nothing of the text in it: "x" with
/// <c>left: 0; right: 0; justify-self: center</c> stayed 500px wide at the left of a 500px
/// containing block, where browsers make it 8px wide, 246px in. It aligned the border box and then
/// added the left margin.
/// </para>
/// <para>
/// Each box here is in a 500px block with <c>position: relative</c>, with <c>top: 0</c>. Words are
/// 16px tall and 8px wide a letter, and a space is 4px wide.
/// </para>
/// </remarks>
public sealed class AbsposJustifySelfTests
{
    private static readonly Uri BaseUrl = new("file:///abspos-justify-self.html");

    /// <summary>
    /// With <c>left: 0; right: 0</c>, an x is 8px wide, 246px in for <c>center</c>, 492px in for
    /// <c>end</c> and <c>right</c>, and at 0 for <c>start</c>. It was 500px wide at 0.
    /// </summary>
    [Theory]
    [InlineData("center", 246)]
    [InlineData("end", 492)]
    [InlineData("right", 492)]
    [InlineData("start", 0)]
    public void An_X_Is_Aligned_Between_The_Insets(string justifySelf, float x)
    {
        var (root, box) = Build(justifySelf);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(x, box.Location.X, 1);
        Assert.Equal(8, box.Size.Width, 1);
    }

    /// <summary>
    /// With <c>left: 50px; right: 100px</c>, a centred x is 221px in, in the middle of the 350px
    /// between them. It was 350px wide, 50px in.
    /// </summary>
    [Fact]
    public void It_Is_Centred_Between_The_Insets_Not_In_The_Block()
    {
        var (root, box) = Build("center", b =>
        {
            b.Left = "50px";
            b.Right = "100px";
        });
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(221, box.Location.X, 1);
        Assert.Equal(8, box.Size.Width, 1);
    }

    /// <summary>
    /// Its margin box is aligned. With <c>margin-left: 40px</c>, the centred margin box, 48px
    /// wide, is 226px in, so the x is 266px in; it was 420px wide, 80px in. With
    /// <c>margin-right: 30px</c> and <c>end</c>, the x is 462px in; it was 440px wide, 60px in.
    /// Auto margins count as nothing: with <c>margin: 0 auto</c> the centred x is 246px in; it was
    /// 500px wide at 0.
    /// </summary>
    [Theory]
    [InlineData("center", "40px", "0", 266)]
    [InlineData("end", "0", "30px", 462)]
    [InlineData("center", "auto", "auto", 246)]
    public void Its_Margin_Box_Is_Aligned(string justifySelf, string marginLeft, string marginRight, float x)
    {
        var (root, box) = Build(justifySelf, b =>
        {
            b.MarginLeft = marginLeft;
            b.MarginRight = marginRight;
        });
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(x, box.Location.X, 1);
        Assert.Equal(8, box.Size.Width, 1);
    }

    /// <summary>
    /// With 10px of padding either side, the centred box is 28px wide, 236px in. It was 520px
    /// wide at 0.
    /// </summary>
    [Fact]
    public void Its_Padding_Counts()
    {
        var (root, box) = Build("center", b => b.PaddingLeft = b.PaddingRight = "10px");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(236, box.Location.X, 1);
        Assert.Equal(28, box.Size.Width, 1);
    }

    /// <summary>
    /// A positioned child 300px wide does not count: the centred box around an x and that child
    /// is 8px wide, 246px in. It was 500px wide at 0.
    /// </summary>
    [Fact]
    public void A_Positioned_Child_Does_Not_Count()
    {
        var (root, box) = Build("center");
        _ = new CssBox(box, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Position = CssConstants.Absolute,
            Top = "0",
            Left = "0",
            Width = "300px",
            Height = "10px",
        };
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(246, box.Location.X, 1);
        Assert.Equal(8, box.Size.Width, 1);
    }

    /// <summary>
    /// "x y", 20px, centred with <c>text-align: right</c>, is 20px wide, 240px in. It was 500px
    /// wide at 0, the text against its right edge.
    /// </summary>
    [Fact]
    public void Text_Is_Measured_On_Its_Line()
    {
        var (root, box) = Build("center", b => b.TextAlign = "right", "x y");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(240, box.Location.X, 1);
        Assert.Equal(20, box.Size.Width, 1);
    }

    /// <summary>
    /// <c>min-width: 100px</c> makes the centred box 100px wide, 200px in. It was 500px wide at 0.
    /// </summary>
    [Fact]
    public void Min_Width_Counts()
    {
        var (root, box) = Build("center", b => b.MinWidth = "100px");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(200, box.Location.X, 1);
        Assert.Equal(100, box.Size.Width, 1);
    }

    /// <summary>
    /// A fixed box with <c>left: 0; right: 0</c> is centred in the 1024px viewport, 508px in. It
    /// was 1024px wide at 0.
    /// </summary>
    [Fact]
    public void A_Fixed_Box_Is_Centred_In_The_Viewport()
    {
        var (root, box) = Build("center", b => b.Position = CssConstants.Fixed);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(508, box.Location.X, 1);
        Assert.Equal(8, box.Size.Width, 1);
    }

    /// <summary>
    /// Laid out a second time, the centred x is 246px in, 8px wide still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Box()
    {
        var (root, box) = Build("center");
        root.PerformLayout(root.LayoutEnvironment);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(246, box.Location.X, 1);
        Assert.Equal(8, box.Size.Width, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: <c>stretch</c> and <c>normal</c> stretch the box
    /// across the 500px; a centred box with <c>max-width: 4px</c> is 4px wide, 248px in; one around
    /// a 100px block is 100px wide, 200px in; and one around seven 10-letter words, 584px of them,
    /// is 500px wide at 0.
    /// </summary>
    [Theory]
    [InlineData("stretch", "none", 0, 500)]
    [InlineData("normal", "none", 0, 500)]
    [InlineData("center", "max-width", 248, 4)]
    [InlineData("center", "block", 200, 100)]
    [InlineData("center", "long", 0, 500)]
    public void Control_Stretched_Or_Already_Right(string justifySelf, string content, float x, float width)
    {
        string text = content == "long" ? string.Join(" ", new string('x', 10), new string('x', 10), new string('x', 10),
            new string('x', 10), new string('x', 10), new string('x', 10), new string('x', 10)) : "x";
        var (root, box) = Build(justifySelf, b =>
        {
            if (content == "max-width")
                b.MaxWidth = "4px";
        }, content == "block" ? "" : text);

        if (content == "block")
            _ = new CssBox(box, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "100px", Height = "20px" };

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(x, box.Location.X, 1);
        Assert.Equal(width, box.Size.Width, 1);
    }

    /// <summary>
    /// In a 500px block with <c>position: relative</c>, a box with
    /// <c>position: absolute; top: 0; left: 0; right: 0</c> and the given <c>justify-self</c>,
    /// styled by <paramref name="style"/>, holding <paramref name="text"/> unless it is empty.
    /// Returns the root and the box.
    /// </summary>
    private static (CssBox Root, CssBox Box) Build(string justifySelf, Action<CssBox>? style = null, string text = "x")
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
            Height = "20px",
        };

        var box = new CssBox(containing, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Position = CssConstants.Absolute,
            Top = "0",
            Left = "0",
            Right = "0",
            JustifySelf = justifySelf,
        };
        style?.Invoke(box);

        if (text.Length > 0)
        {
            var word = new CssBox(box, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
            word.ParseToWords();
        }

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

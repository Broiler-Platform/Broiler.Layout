using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An absolutely or fixed positioned box with <c>left</c> and <c>right</c> and an auto width is as
/// wide as the space between them, less its margins: its padding and border are inside that width.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §10.3.7: <c>left</c>, the margins, the borders, the padding, the width and <c>right</c>
/// add up to the containing block's width. The engine took what the insets and margins leave for
/// the content box, and under <c>box-sizing: content-box</c> added the padding and border to it
/// again: in a 500px containing block, <c>left: 0; right: 0; padding: 0 10px</c> made a box 520px
/// wide, where browsers make it 500px wide, and its text wrapped 20px too late.
/// </para>
/// <para>
/// Each box here is in a 500px block with <c>position: relative</c>, in a body in a root. Words are
/// 16px tall and 8px wide a letter, and a space is 4px wide.
/// </para>
/// </remarks>
public sealed class AbsposInsetWidthTests
{
    private static readonly Uri BaseUrl = new("file:///abspos-inset-width.html");

    /// <summary>
    /// With <c>left: 0; right: 0</c> and 10px of padding on either side, the box is 500px wide at
    /// 0, its content 480px. It was 520px wide.
    /// </summary>
    [Fact]
    public void The_Padding_Is_Inside_The_Width()
    {
        var (root, box) = Build(b => b.PaddingLeft = b.PaddingRight = "10px");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(0, box.Location.X, 1);
        Assert.Equal(500, box.Size.Width, 1);
        Assert.Equal(480, box.ClientRight - box.ClientLeft, 1);
    }

    /// <summary>
    /// With a 5px border on either side, the box is 500px wide. It was 510px wide.
    /// </summary>
    [Fact]
    public void The_Border_Is_Inside_The_Width()
    {
        var (root, box) = Build(b =>
        {
            b.BorderLeftWidth = b.BorderRightWidth = "5px";
            b.BorderLeftStyle = b.BorderRightStyle = "solid";
        });
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(500, box.Size.Width, 1);
    }

    /// <summary>
    /// With <c>left: 20px; right: 30px</c>, 5px margins and 10px of padding, the box is 440px wide,
    /// 25px in. It was 450px wide.
    /// </summary>
    [Fact]
    public void The_Insets_Margins_And_Padding_All_Count()
    {
        var (root, box) = Build(b =>
        {
            b.Left = "20px";
            b.Right = "30px";
            b.MarginLeft = b.MarginRight = "5px";
            b.PaddingLeft = b.PaddingRight = "10px";
        });
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(25, box.Location.X, 1);
        Assert.Equal(440, box.Size.Width, 1);
    }

    /// <summary>
    /// A fixed box with <c>left: 0; right: 0</c> and 10px of padding is as wide as the 1024px
    /// viewport. It was 1044px wide.
    /// </summary>
    [Fact]
    public void A_Fixed_Box_Is_As_Wide_As_The_Viewport()
    {
        var (root, box) = Build(b =>
        {
            b.Position = CssConstants.Fixed;
            b.PaddingLeft = b.PaddingRight = "10px";
        });
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(1024, box.Size.Width, 1);
    }

    /// <summary>
    /// Its text wraps at its content box: "xxxxxxxxxx xxxxxxxxxx", 164px, does not fit in the 160px
    /// a box with 10px of padding has in a 180px containing block, and takes two lines. It took
    /// one, the box being 200px wide.
    /// </summary>
    [Fact]
    public void Its_Text_Wraps_At_The_Content_Box()
    {
        var (root, box) = Build(b => b.PaddingLeft = b.PaddingRight = "10px", "xxxxxxxxxx xxxxxxxxxx", containingWidth: 180);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(180, box.Size.Width, 1);
        Assert.Equal(32, box.Size.Height, 1);
    }

    /// <summary>
    /// In a 10px containing block, a box with 10px of padding on either side has no room for its
    /// content, and is as wide as its padding, 20px. It was 30px wide.
    /// </summary>
    [Fact]
    public void The_Content_Box_Is_Never_Narrower_Than_Nothing()
    {
        var (root, box) = Build(b => b.PaddingLeft = b.PaddingRight = "10px", containingWidth: 10);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(20, box.Size.Width, 1);
    }

    /// <summary>
    /// Laid out a second time, the padded box is 500px wide still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Width()
    {
        var (root, box) = Build(b => b.PaddingLeft = b.PaddingRight = "10px");
        root.PerformLayout(root.LayoutEnvironment);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(500, box.Size.Width, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: with <c>box-sizing: border-box</c> the padded box is
    /// 500px wide, and with <c>left: 0</c> alone it shrinks to its x and its padding, 28px.
    /// </summary>
    [Theory]
    [InlineData(true, 500)]
    [InlineData(false, 28)]
    public void Control_Border_Box_Sizing_Or_One_Inset(bool borderBox, double width)
    {
        var (root, box) = Build(b =>
        {
            b.PaddingLeft = b.PaddingRight = "10px";
            if (borderBox)
                b.BoxSizing = "border-box";
            else
                b.Right = CssConstants.Auto;
        });
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(width, box.Size.Width, 1);
    }

    /// <summary>
    /// In a block with <c>position: relative</c> of the given width, a 20px tall box with
    /// <c>position: absolute; top: 0; left: 0; right: 0</c>, styled by <paramref name="style"/>,
    /// holding <paramref name="text"/>. Returns the root and the box.
    /// </summary>
    private static (CssBox Root, CssBox Box) Build(Action<CssBox> style, string text = "x", int containingWidth = 500)
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
            Width = containingWidth + "px",
            Height = "20px",
        };

        var box = new CssBox(containing, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Position = CssConstants.Absolute,
            Top = "0",
            Left = "0",
            Right = "0",
        };
        style(box);

        var word = new CssBox(box, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        word.ParseToWords();

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

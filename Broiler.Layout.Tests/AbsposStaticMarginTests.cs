using System;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An absolutely positioned box at its static position starts its text at its content edge, and
/// its margins are not added to its text a second time.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §10.3.7 and §10.6.4: a box whose <c>top</c> and <c>left</c> are <c>auto</c> is put at
/// its static position, and its margins place its border box from there; its content is inside it.
/// The engine put the box there, margins and all, and flowed its words from its content edge; then
/// <c>FlowBox</c> added its margins to the words again. With <c>margin-top: 10px</c> the text was
/// drawn 10px below the box's content top, where browsers draw it at the top.
/// </para>
/// <para>
/// Each box here is 60px wide with 20px lines, after a 10px block in a block in the root, whose
/// padding box is its containing block. Words are 8px wide a letter and 16px tall, so two
/// three-letter words fill a line.
/// </para>
/// </remarks>
public sealed class AbsposStaticMarginTests
{
    private static readonly Uri BaseUrl = new("file:///abspos-static-margin.html");

    /// <summary>
    /// With <c>margin-top: 10px</c>, the box starts 20px down, below the 10px block and its margin,
    /// and its text at its top. The text started 30px down.
    /// </summary>
    [Fact]
    public void A_Top_Margin_Is_Not_Added_To_The_Text()
    {
        var t = Build(box => box.MarginTop = "10px", "one");
        Layout(t);

        Assert.Equal(20, t.Box.Location.Y, 1);
        Assert.Equal(t.Box.ClientTop, WordTop(t, "one"), 1);
    }

    /// <summary>
    /// With <c>margin-left: 10px</c>, the text starts at the box's left content edge, 10px in. It
    /// started 20px in.
    /// </summary>
    [Fact]
    public void A_Left_Margin_Is_Not_Added_To_The_Text()
    {
        var t = Build(box => box.MarginLeft = "10px", "one");
        Layout(t);

        Assert.Equal(10, t.Box.Location.X, 1);
        Assert.Equal(t.Box.ClientLeft, t.Box.Boxes.SelectMany(b => b.Words).First().Left, 1);
    }

    /// <summary>
    /// Laid out a second time, the text is at the box's top still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Place()
    {
        var t = Build(box => box.MarginTop = "10px", "one");
        Layout(t);
        Layout(t);

        Assert.Equal(t.Box.ClientTop, WordTop(t, "one"), 1);
    }

    /// <summary>
    /// Controls, which pass before and after: with <c>margin-top: -10px</c>, the box's three lines
    /// start at its top, 20px apart, as the lines moved down together to the block's top had them;
    /// and a box placed by <c>top: 0</c>, 10px down by its margin, starts its text at its top.
    /// </summary>
    [Fact]
    public void Control_A_Negative_Margin_And_A_Box_Placed_By_Its_Top()
    {
        var negative = Build(box => box.MarginTop = "-10px", "one two six ten red sky");
        Layout(negative);

        Assert.Equal(0, negative.Box.Location.Y, 1);
        Assert.Equal(0, WordTop(negative, "one"), 1);
        Assert.Equal(20, WordTop(negative, "six"), 1);
        Assert.Equal(40, WordTop(negative, "red"), 1);

        var placed = Build(box => { box.Top = "0px"; box.MarginTop = "10px"; }, "one");
        Layout(placed);

        Assert.Equal(10, placed.Box.Location.Y, 1);
        Assert.Equal(placed.Box.ClientTop, WordTop(placed, "one"), 1);
    }

    /// <summary>The root and the absolutely positioned box.</summary>
    private sealed record Tree(CssBox Root, CssBox Box);

    private static Tree Build(Action<CssBox> style, string text)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        _ = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Height = "10px" };

        var box = new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Position = CssConstants.Absolute,
            Width = "60px",
            LineHeight = "20px",
        };

        style(box);

        var words = new CssBox(box, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        words.ParseToWords();

        return new Tree(root, box);
    }

    private static void Layout(Tree t) => t.Root.PerformLayout(t.Root.LayoutEnvironment);

    private static double WordTop(Tree t, string text) =>
        t.Box.Boxes.SelectMany(b => b.Words).Single(w => w.Text == text).Top;

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
        public double Size => 12;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}

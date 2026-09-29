using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An inline box that <c>vertical-align</c> lowers or raises from the baseline, by <c>sub</c>,
/// <c>super</c> or a length, moves the text and the boxes in it with it.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §10.8.1: each box is aligned against its parent's baseline, so an inline box moved from
/// its parent's baseline carries the baseline its content stands on. An element's text is in an
/// inline box of its own, aligned to the baseline; the engine aligned that box to the line's
/// baseline and moved only the element's box, which holds no words, so the text stayed where it
/// was. The text of every <c>&lt;sup&gt;</c> and <c>&lt;sub&gt;</c> stood on the line's baseline,
/// where browsers draw it 6.33px higher and 4.2px lower in 16px text.
/// </para>
/// <para>
/// Each line here is in a 320px block with a 16px font; words are 16px tall and 8px wide a letter,
/// and stand on a baseline 12.8px below their top. "a" comes first, then the box aligned as given
/// with "x" in it, in an inline box of its own as an element's text is.
/// </para>
/// </remarks>
public sealed class InlineBoxBaselineShiftTests
{
    private static readonly Uri BaseUrl = new("file:///inline-box-baseline-shift.html");

    /// <summary>
    /// "x" in a box aligned <c>sub</c> is drawn 4.2px lower than "a", in one aligned
    /// <c>super</c> 6.33px higher, and in one aligned 5px or -5px as much higher or lower. It was
    /// level with "a".
    /// </summary>
    [Theory]
    [InlineData(CssConstants.Sub, 4.2)]
    [InlineData(CssConstants.Super, -6.33)]
    [InlineData("5px", -5)]
    [InlineData("-5px", 5)]
    public void The_Text_Moves_With_Its_Box(string verticalAlign, double lower)
    {
        var tree = Build(verticalAlign);
        Layout(tree);

        Assert.Equal(lower, tree.Lower(tree.Text), 1);
    }

    /// <summary>
    /// The line makes room for the text it moves: with "x" lowered by <c>sub</c> the line is
    /// 20.2px tall, and with "x" raised by <c>super</c> it is 22.33px tall, "a" 6.33px down and "x"
    /// at its top. It was 16px tall either way, "a" at its top.
    /// </summary>
    [Theory]
    [InlineData(CssConstants.Sub, 20.2, 0)]
    [InlineData(CssConstants.Super, 22.33, 6.33)]
    public void The_Line_Makes_Room_For_The_Text(string verticalAlign, double height, double beforeTop)
    {
        var tree = Build(verticalAlign);
        Layout(tree);

        Assert.Equal(height, tree.Block.Size.Height, 1);
        Assert.Equal(beforeTop, tree.Before.Words[0].Top - tree.Block.Location.Y, 1);
    }

    /// <summary>
    /// The move passes through a box on the baseline between them: "x" in a box on the baseline in
    /// one aligned <c>sub</c> is 4.2px lower than "a". It was level with it.
    /// </summary>
    [Fact]
    public void The_Text_Of_A_Box_Nested_On_The_Baseline_Moves_Too()
    {
        var tree = Build(CssConstants.Sub, nest: CssConstants.Baseline);
        Layout(tree);

        Assert.Equal(4.2, tree.Lower(tree.Text), 1);
    }

    /// <summary>
    /// Moves add up: "x" in a box aligned <c>sub</c> in one aligned <c>super</c> is 6.33px raised
    /// and 4.2px lowered, 2.13px higher than "a". It was level with it.
    /// </summary>
    [Fact]
    public void Nested_Moves_Add_Up()
    {
        var tree = Build(CssConstants.Super, nest: CssConstants.Sub);
        Layout(tree);

        Assert.Equal(-2.13, tree.Lower(tree.Text), 1);
    }

    /// <summary>
    /// An empty 10px inline-block on the baseline in a box aligned <c>sub</c> ends 4.2px below the
    /// baseline of "a". It ended on it.
    /// </summary>
    [Fact]
    public void An_Inline_Block_In_The_Box_Moves_Too()
    {
        var tree = Build(CssConstants.Sub, text: false);
        var box = new CssBox(tree.Box, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = "inline-block",
            Width = "8px",
            Height = "10px",
        };
        Layout(tree);

        double baseline = tree.Before.Words[0].Top + Ascent;
        Assert.Equal(4.2, box.Location.Y + box.Size.Height - baseline, 1);
    }

    /// <summary>
    /// Laid out a second time, "x" in a box aligned <c>sub</c> is 4.2px lower than "a" still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Line()
    {
        var tree = Build(CssConstants.Sub);
        Layout(tree);
        Layout(tree);

        Assert.Equal(4.2, tree.Lower(tree.Text), 1);
    }

    /// <summary>
    /// Control, which passes before and after: "x" in a box on the baseline is level with "a".
    /// </summary>
    [Fact]
    public void Control_The_Text_Of_A_Box_On_The_Baseline_Stays_Level()
    {
        var tree = Build(CssConstants.Baseline);
        Layout(tree);

        Assert.Equal(0, tree.Lower(tree.Text), 1);
    }

    private const double Ascent = 12.8;

    /// <summary>
    /// The root, the 320px block, the word "a", the aligned box, and the box holding "x", if any.
    /// </summary>
    private sealed record Tree(CssBox Root, CssBox Block, CssBox Before, CssBox Box, CssBox? Text)
    {
        /// <summary>How much lower than "a" the first word of the box is drawn.</summary>
        public double Lower(CssBox? box) => box!.Words[0].Top - Before.Words[0].Top;
    }

    /// <summary>
    /// In a 320px block in the root, "a" and then a box aligned as given, holding "x" in an inline
    /// box of its own unless <paramref name="text"/> is false; with <paramref name="nest"/>, "x" is
    /// in a box aligned that way inside it.
    /// </summary>
    private static Tree Build(string verticalAlign, string? nest = null, bool text = true)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var block = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "320px" };
        var before = Word(block, "a");

        var box = new CssBox(block, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = "inline",
            VerticalAlign = verticalAlign,
        };

        var holder = box;
        if (nest != null)
            holder = new CssBox(box, new HtmlTag("span", false, null), BaseUrl) { Display = "inline", VerticalAlign = nest };

        var x = text ? Word(holder, "x") : null;
        return new Tree(root, block, before, box, x);
    }

    private static CssBox Word(CssBox parent, string text)
    {
        var word = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        word.ParseToWords();
        return word;
    }

    private static void Layout(Tree tree)
    {
        FlexGridItemBlockification.Generate(tree.Root);
        tree.Root.PerformLayout(tree.Root.LayoutEnvironment);
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
        public double Size => 12;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}

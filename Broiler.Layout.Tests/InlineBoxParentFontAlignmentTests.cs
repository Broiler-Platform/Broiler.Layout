using System;
using System.Collections.Concurrent;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An inline box that <c>vertical-align</c> aligns to its parent's font, by <c>middle</c>,
/// <c>text-top</c> or <c>text-bottom</c>, moves the text and the boxes in it with it, and is
/// aligned by its line height.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §10.8.1: <c>text-top</c> puts the top of the box at the top of the parent's content
/// area, <c>text-bottom</c> its bottom at the bottom of it, and <c>middle</c> its middle half the
/// parent's x-height above the parent's baseline. For an inline, non-replaced box that box is its
/// line height, half its leading above its glyphs and the rest below. An element's text is in an
/// inline box of its own, aligned to the baseline; the engine aligned that box to the line's
/// baseline and moved only the element's box, which holds no words, so the text stood where text on
/// the baseline does.
/// </para>
/// <para>
/// Each line here is in a 320px block with a 16px font and 20px lines, which leaves 2px of leading
/// above the glyphs and 2px below. A font is as tall as its size and stands on a baseline four
/// fifths of the way down; words are 8px wide a letter. "a" comes first, then the box aligned as
/// given with "x" in it, in an inline box of its own as an element's text is.
/// </para>
/// </remarks>
public sealed class InlineBoxParentFontAlignmentTests
{
    private static readonly Uri BaseUrl = new("file:///inline-box-parent-font-alignment.html");

    /// <summary>
    /// How much lower than "a" the top of "x" is drawn:
    /// <list type="bullet">
    /// <item><c>middle</c>: the middle of the box's 20px line height, 4.8px above its baseline,
    /// goes a quarter of the 16px font above "a"'s baseline, so "x" is 0.8px lower.</item>
    /// <item>A 10px font with 10px lines, which have no leading: <c>text-top</c> puts the top of
    /// "x" level with the top of "a", <c>text-bottom</c> their bottoms level, the top of "x" 6px
    /// lower.</item>
    /// <item>40px lines, 12px of leading above the glyphs and 12px below: <c>text-top</c> puts
    /// the top of that at the top of "a", "x" 12px lower, and <c>text-bottom</c> its bottom at
    /// the bottom of "a", "x" 12px higher.</item>
    /// </list>
    /// "x" stood on the baseline of "a": level with it, and 4.8px lower in the 10px font.
    /// </summary>
    [Theory]
    [InlineData(CssConstants.Middle, null, null, 0.8)]
    [InlineData(CssConstants.TextTop, "10px", "10px", 0)]
    [InlineData(CssConstants.TextBottom, "10px", "10px", 6)]
    [InlineData(CssConstants.TextTop, null, "40px", 12)]
    [InlineData(CssConstants.TextBottom, null, "40px", -12)]
    public void The_Text_Moves_With_Its_Box(string verticalAlign, string? fontSize, string? lineHeight, double lower)
    {
        var tree = Build(verticalAlign, fontSize, lineHeight);
        Layout(tree);

        Assert.Equal(lower, tree.Lower(tree.Text), 1);
    }

    /// <summary>
    /// A box holding words of its own, as a <c>::before</c> or <c>::after</c> box does, is aligned
    /// by its line height too: with 40px lines, <c>text-top</c> puts "x" 12px lower than "a" and
    /// <c>text-bottom</c> 12px higher. The box was aligned by its glyphs, and "x" was level with
    /// "a".
    /// </summary>
    [Theory]
    [InlineData(CssConstants.TextTop, 12)]
    [InlineData(CssConstants.TextBottom, -12)]
    public void A_Box_Holding_Its_Own_Words_Is_Aligned_By_Its_Line_Height(string verticalAlign, double lower)
    {
        var tree = Build(verticalAlign, lineHeight: "40px", text: false);
        tree.Box.Text = "x".AsMemory();
        tree.Box.ParseToWords();
        Layout(tree);

        Assert.Equal(lower, tree.Lower(tree.Box), 1);
    }

    /// <summary>
    /// The line makes room for the box's line height where the alignment puts it:
    /// <list type="bullet">
    /// <item><c>middle</c>: it reaches 0.8px below the strut's, and the line is 20.8px tall, "a"
    /// 2px down it. It was 20px.</item>
    /// <item><c>text-top</c> with 40px lines: it starts at the top of "a" and ends 22px below the
    /// strut's end, and the line is 42px tall, "a" 2px down it. It was 40px, "a" 12px down.</item>
    /// <item><c>text-bottom</c> with 40px lines: it ends at the bottom of "a" and starts 22px above
    /// the strut, and the line is 42px tall, "a" 24px down it. It was 40px, "a" 12px down.</item>
    /// </list>
    /// </summary>
    [Theory]
    [InlineData(CssConstants.Middle, null, 20.8, 2)]
    [InlineData(CssConstants.TextTop, "40px", 42, 2)]
    [InlineData(CssConstants.TextBottom, "40px", 42, 24)]
    public void The_Line_Makes_Room_For_The_Box(string verticalAlign, string? lineHeight, double height, double beforeTop)
    {
        var tree = Build(verticalAlign, lineHeight: lineHeight);
        Layout(tree);

        Assert.Equal(height, tree.Block.Size.Height, 1);
        Assert.Equal(beforeTop, tree.Before.Words[0].Top - tree.Block.Location.Y, 1);
    }

    /// <summary>
    /// Alone on its line, a box aligned <c>text-bottom</c> in a 10px font on the block's 20px lines
    /// starts 2px above the strut: its 10px of leading, 5px below its glyphs, end at the bottom of
    /// the parent's glyphs. The strut still ends its descent and leading below the baseline, and the
    /// line is 22px tall. It was measured 20px from its top, down to where the box ends, and was
    /// 20px tall.
    /// </summary>
    [Fact]
    public void The_Strut_Ends_Below_The_Baseline_Under_Raised_Content()
    {
        var tree = Build(CssConstants.TextBottom, fontSize: "10px", before: false);
        Layout(tree);

        Assert.Equal(22, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// A 32px "x" in a box aligned <c>middle</c> does not push the line's baseline down as text on
    /// the baseline does: its 20px line height, 5.6px below the parent's baseline, ends 0.8px below
    /// the strut, and "a" stays 2px down a 20.8px line. "a" was 6.8px down a 22.8px line.
    /// </summary>
    [Fact]
    public void A_Larger_Font_In_The_Box_Does_Not_Move_The_Baseline()
    {
        var tree = Build(CssConstants.Middle, fontSize: "32px");
        Layout(tree);

        Assert.Equal(2, tree.Before.Words[0].Top - tree.Block.Location.Y, 1);
        Assert.Equal(20.8, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// The move passes through a box on the baseline between them: "x" in a box on the baseline in
    /// one aligned <c>text-top</c> with 40px lines is 12px lower than "a". It was level with it.
    /// </summary>
    [Fact]
    public void The_Text_Of_A_Box_Nested_On_The_Baseline_Moves_Too()
    {
        var tree = Build(CssConstants.TextTop, lineHeight: "40px", nest: CssConstants.Baseline);
        Layout(tree);

        Assert.Equal(12, tree.Lower(tree.Text), 1);
    }

    /// <summary>
    /// An empty 10px inline-block on the baseline in a box aligned <c>text-top</c> with 40px lines
    /// ends 12px below the baseline of "a". It ended on it.
    /// </summary>
    [Fact]
    public void An_Inline_Block_In_The_Box_Moves_Too()
    {
        var tree = Build(CssConstants.TextTop, lineHeight: "40px", text: false);
        var box = new CssBox(tree.Box, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = "inline-block",
            Width = "8px",
            Height = "10px",
        };
        Layout(tree);

        double baseline = tree.Before.Words[0].Top + 12.8;
        Assert.Equal(12, box.Location.Y + box.Size.Height - baseline, 1);
    }

    /// <summary>
    /// A 40px block clamped to its first line, with a long word wrapped onto a second, keeps the
    /// first as tall as it lays it out:
    /// <list type="bullet">
    /// <item>With "x" in a box aligned <c>middle</c> after "a", 20.8px. The clamp measured the
    /// line to the bottom of the glyphs of "x", and the block was 20px.</item>
    /// <item>With "x" alone in a box aligned <c>text-bottom</c> in a 10px font, 22px, with or
    /// without an ellipsis. The clamp measured the line a line height below the leading above
    /// "x", and the block was 21.8px; to the ellipsis, at the top of "x" in the block's font,
    /// 22.8px. Laid out unclamped, it was 20px.</item>
    /// </list>
    /// </summary>
    [Theory]
    [InlineData(CssConstants.Middle, null, true, true, 20.8)]
    [InlineData(CssConstants.TextBottom, "10px", false, false, 22)]
    [InlineData(CssConstants.TextBottom, "10px", false, true, 22)]
    public void A_Clamped_Block_Keeps_The_Line_As_Tall(string verticalAlign, string? fontSize, bool before, bool ellipsis, double height)
    {
        var tree = Build(verticalAlign, fontSize, before: before, width: "40px");
        if (ellipsis)
            tree.Block.LineClamp = "1";
        else
            tree.Block.MaxLines = "1";

        Word(tree.Block, " bbbbbb");
        Layout(tree);

        Assert.Equal(height, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// The ellipsis a clamp puts at the end of the line stands on the line's baseline, in the
    /// block's font: after "x" in a box aligned <c>text-bottom</c> in a 10px font, level with "a".
    /// It stood at the top of "x", 4.8px lower.
    /// </summary>
    [Fact]
    public void The_Ellipsis_Stands_On_The_Baseline()
    {
        var tree = Build(CssConstants.TextBottom, fontSize: "10px", width: "40px");
        tree.Block.LineClamp = "1";
        Word(tree.Block, " bbbbbb");
        Layout(tree);

        var ellipsis = tree.Block.LineBoxes[0].Words[^1];
        Assert.Equal("…", ellipsis.Text);
        Assert.Equal(tree.Before.Words[0].Top, ellipsis.Top, 1);
    }

    /// <summary>
    /// Moving a block after its lines are laid out, as a margin it passes on or flex or grid
    /// placement does, moves where their baselines are with them: 10px down, 10px lower. They
    /// stayed where the lines had been.
    /// </summary>
    [Fact]
    public void A_Moved_Block_Takes_Its_Lines_Baselines_With_It()
    {
        var tree = Build(CssConstants.Middle);
        Layout(tree);
        double baseline = tree.Block.LineBoxes[0].Baseline!.Value;

        tree.Block.ParentBox!.OffsetTop(10);

        Assert.Equal(baseline + 10, tree.Block.LineBoxes[0].Baseline!.Value, 1);
    }

    /// <summary>
    /// Laid out a second time, "x" in a box aligned <c>text-bottom</c> with 40px lines is 12px
    /// higher than "a" still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Line()
    {
        var tree = Build(CssConstants.TextBottom, lineHeight: "40px");
        Layout(tree);
        Layout(tree);

        Assert.Equal(-12, tree.Lower(tree.Text), 1);
        Assert.Equal(42, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: "x" in a box on the baseline is level with "a", in a
    /// 20px line; and in a box aligned <c>sub</c>, 4.2px lower.
    /// </summary>
    [Theory]
    [InlineData(CssConstants.Baseline, 0)]
    [InlineData(CssConstants.Sub, 4.2)]
    public void Control_A_Box_On_Or_Moved_From_The_Baseline(string verticalAlign, double lower)
    {
        var tree = Build(verticalAlign);
        Layout(tree);

        Assert.Equal(lower, tree.Lower(tree.Text), 1);
        if (verticalAlign == CssConstants.Baseline)
            Assert.Equal(20, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// The root, the 320px block, the box holding "a", if any, the aligned box, and the box holding
    /// "x", if any.
    /// </summary>
    private sealed record Tree(CssBox Root, CssBox Block, CssBox Before, CssBox Box, CssBox? Text)
    {
        /// <summary>How much lower than "a" the first word of the box is drawn.</summary>
        public double Lower(CssBox? box) => box!.Words[0].Top - Before.Words[0].Top;
    }

    /// <summary>
    /// In a block in the root with a 16px font and 20px lines, "a" unless <paramref name="before"/>
    /// is false, and then a box aligned as given, with the font size and line height given, holding
    /// "x" in an inline box of its own unless <paramref name="text"/> is false; with
    /// <paramref name="nest"/>, "x" is in a box aligned that way inside it.
    /// </summary>
    private static Tree Build(
        string verticalAlign,
        string? fontSize = null,
        string? lineHeight = null,
        string? nest = null,
        bool text = true,
        bool before = true,
        string width = "320px")
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var block = new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Width = width,
            LineHeight = "20px",
        };

        var a = before ? Word(block, "a") : null;

        var box = new CssBox(block, new HtmlTag("span", false, null), BaseUrl);
        box.InheritStyle();
        box.Display = "inline";
        box.VerticalAlign = verticalAlign;
        if (fontSize != null)
            box.FontSize = fontSize;
        if (lineHeight != null)
            box.LineHeight = lineHeight;

        var holder = box;
        if (nest != null)
        {
            holder = new CssBox(box, new HtmlTag("span", false, null), BaseUrl);
            holder.InheritStyle();
            holder.Display = "inline";
            holder.VerticalAlign = nest;
        }

        var x = text ? Word(holder, "x") : null;
        return new Tree(root, block, a!, box, x);
    }

    /// <summary>
    /// An anonymous inline box in <paramref name="parent"/> holding the text, inheriting the
    /// parent's style as the box a text node makes does.
    /// </summary>
    private static CssBox Word(CssBox parent, string text)
    {
        var word = new CssBox(parent, null, BaseUrl);
        word.InheritStyle();
        word.Display = "inline";
        word.Text = text.AsMemory();
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
        private static readonly ConcurrentDictionary<double, FakeFont> Fonts = new();
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => Fonts.GetOrAdd(size, s => new FakeFont(s));
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => new(8 * text.Length, (float)font.Height);
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

    /// <summary>A font of the given size in points, as tall in pixels as its size in pixels.</summary>
    private sealed class FakeFont(double size) : Broiler.Graphics.Text.ILayoutFont
    {
        public double Size => size;
        public double Height => size * 4 / 3;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}

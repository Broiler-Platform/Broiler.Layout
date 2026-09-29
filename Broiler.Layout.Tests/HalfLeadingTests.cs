using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// Text stands half its inline box's leading below the top of its line, and the strut reaches its
/// font's descent and the other half of the leading below the baseline.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §10.8.1: an inline box, the strut among them, is as tall as its <c>line-height</c>,
/// with the leading, the line height less the font's height, split into half above the glyphs and
/// half below; browsers floor the half above to a whole pixel. The engine put the glyphs at the top
/// of the line, all the leading below them: a letter in a 60px line was drawn at the line's top,
/// where browsers draw it 22px down, and an image on the baseline of that line had a fifth of the
/// line height below it, 12px, where browsers leave the strut's descent and half its leading,
/// 25.2px.
/// </para>
/// <para>
/// Each block here is 320px wide, in a block in the root. Words are 8px wide a letter and 16px tall,
/// with an ascent of 12.8px and a descent of 3.2px.
/// </para>
/// </remarks>
public sealed class HalfLeadingTests
{
    private static readonly Uri BaseUrl = new("file:///half-leading.html");

    /// <summary>
    /// In a 60px line, a word stands half its 44px of leading down, 22px, and the block is 60px
    /// tall. The word stood at the top.
    /// </summary>
    [Fact]
    public void A_Word_Stands_Half_Its_Leading_Down_A_Tall_Line()
    {
        var t = Build("60px", b => Text(b, "x"));
        Layout(t);

        Assert.Equal(22, WordTop(t, "x"), 1);
        Assert.Equal(60, t.Block.Size.Height, 1);
    }

    /// <summary>
    /// In an 8px block of 20px lines, one word to a line, each word stands 2px down its line: 2, 22
    /// and 42px down, in a 60px block. They stood at 0, 20 and 40.
    /// </summary>
    [Fact]
    public void Every_Line_Has_Its_Half_Leading()
    {
        var t = Build("20px", b => Text(b, "a b c"), width: "8px");
        Layout(t);

        Assert.Equal(2, WordTop(t, "a"), 1);
        Assert.Equal(22, WordTop(t, "b"), 1);
        Assert.Equal(42, WordTop(t, "c"), 1);
        Assert.Equal(60, t.Block.Size.Height, 1);
    }

    /// <summary>
    /// A line height less than the font's leaves a negative leading, and the glyphs reach out of
    /// the line at the top as at the bottom: in 10px lines, one word to a line, the words stand 3px
    /// above their lines, at −3 and 7px, and the block is 20px tall. They stood at 0 and 10.
    /// </summary>
    [Fact]
    public void A_Line_Height_Less_Than_The_Fonts_Puts_The_Glyphs_Above_The_Line()
    {
        var t = Build("10px", b => Text(b, "a b"), width: "8px");
        Layout(t);

        Assert.Equal(-3, WordTop(t, "a"), 1);
        Assert.Equal(7, WordTop(t, "b"), 1);
        Assert.Equal(20, t.Block.Size.Height, 1);
    }

    /// <summary>
    /// A single line with a line height less than the font's is as tall as its line height, 10px,
    /// measured from its top, not from its glyphs 3px above it.
    /// </summary>
    [Fact]
    public void A_Line_Shorter_Than_Its_Font_Is_As_Tall_As_Its_Line_Height()
    {
        var t = Build("10px", b => Text(b, "a"));
        Layout(t);

        Assert.Equal(-3, WordTop(t, "a"), 1);
        Assert.Equal(10, t.Block.Size.Height, 1);
    }

    /// <summary>
    /// A 100px image on the baseline of a 60px line stands at the line's top, and the strut reaches
    /// 25.2px below it, its 3.2px descent and half its 44px of leading: the block is 125.2px tall.
    /// It was 112px, with a fifth of the line height below the image.
    /// </summary>
    [Fact]
    public void An_Image_On_The_Baseline_Of_A_Tall_Line_Has_The_Struts_Descent_Below_It()
    {
        var t = Build("60px", b => Image(b, 100));
        Layout(t);

        var image = t.Block.Boxes.OfType<CssBoxImage>().Single();
        Assert.Equal(0, image.Location.Y - t.Block.ClientTop, 1);
        Assert.Equal(125.2, t.Block.Size.Height, 1);
    }

    /// <summary>
    /// An odd leading is split as browsers split it, the half above the glyphs floored to a whole
    /// pixel and the rest below: in a 21px line, 2px above the word and 3px below, so a 100px image
    /// on the baseline has the strut's 3.2px descent and 3px below it, and the block is 106.2px
    /// tall.
    /// </summary>
    [Theory]
    [InlineData(false, 2, 21)]
    [InlineData(true, 0, 106.2)]
    public void An_Odd_Leading_Puts_The_Whole_Pixel_Below(bool image, double top, double height)
    {
        var t = Build("21px", b => { if (image) Image(b, 100); else Text(b, "x"); });
        Layout(t);

        if (image)
            Assert.Equal(top, t.Block.Boxes.OfType<CssBoxImage>().Single().Location.Y - t.Block.ClientTop, 1);
        else
            Assert.Equal(top, WordTop(t, "x"), 1);

        Assert.Equal(height, t.Block.Size.Height, 1);
    }

    /// <summary>
    /// A span with <c>line-height: 10px</c> in a 20px line stands on the strut's baseline, its word
    /// 2px down as the block's is. It stood at the top.
    /// </summary>
    [Fact]
    public void A_Shorter_Inline_Box_Stands_On_The_Struts_Baseline()
    {
        var t = Build("20px", b =>
        {
            Text(b, "a ");
            var span = new CssBox(b, new HtmlTag("span", false, null), BaseUrl) { Display = "inline", LineHeight = "10px" };
            Text(span, "b");
        });
        Layout(t);

        Assert.Equal(2, WordTop(t, "a"), 1);
        Assert.Equal(2, WordTop(t, "b"), 1);
        Assert.Equal(20, t.Block.Size.Height, 1);
    }

    /// <summary>
    /// A list item's marker stands with the item's text, half the leading down its 60px line. It
    /// stood at the top of the item, 22px above the text.
    /// </summary>
    [Fact]
    public void A_List_Items_Marker_Stands_With_Its_Text()
    {
        var t = Build("60px", b => Text(b, "x"));
        t.Block.Display = CssConstants.ListItem;
        Layout(t);

        var marker = t.Block.ListItemMarkerBox!;
        Assert.Equal(22, marker.Words[0].Top - t.Block.ClientTop, 1);
        Assert.Equal(22, WordTop(t, "x"), 1);
    }

    /// <summary>
    /// Laid out a second time, the word is 22px down its line still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Place()
    {
        var t = Build("60px", b => Text(b, "x"));
        Layout(t);
        Layout(t);

        Assert.Equal(22, WordTop(t, "x"), 1);
        Assert.Equal(60, t.Block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: with a line height as tall as the font, 16px, there
    /// is no leading, and the word stands at the top of its line.
    /// </summary>
    [Fact]
    public void Control_A_Line_As_Tall_As_The_Font()
    {
        var t = Build("16px", b => Text(b, "x"));
        Layout(t);

        Assert.Equal(0, WordTop(t, "x"), 1);
        Assert.Equal(16, t.Block.Size.Height, 1);
    }

    /// <summary>The root and the block that holds the lines.</summary>
    private sealed record Tree(CssBox Root, CssBox Block);

    private static Tree Build(string lineHeight, Action<CssBox> fill, string width = "320px")
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
            LineHeight = lineHeight,
        };

        fill(block);
        return new Tree(root, block);
    }

    private static void Layout(Tree t) => t.Root.PerformLayout(t.Root.LayoutEnvironment);

    private static void Text(CssBox parent, string text)
    {
        var box = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        box.InheritStyle();
        box.ParseToWords();
    }

    private static void Image(CssBox parent, int height) =>
        _ = new CssBoxImage(parent, new HtmlTag("img", true, new Dictionary<string, string> { ["src"] = "i.png" }), BaseUrl)
        {
            Display = CssConstants.Inline,
            Width = "100px",
            Height = height + "px",
        };

    private static double WordTop(Tree t, string text) =>
        Descendants(t.Block).SelectMany(b => b.Words).Single(w => w.Text == text).Top - t.Block.ClientTop;

    private static IEnumerable<CssBox> Descendants(CssBox box)
    {
        foreach (var child in box.Boxes)
        {
            yield return child;

            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private sealed class FakeLayoutEnvironment : ILayoutEnvironment
    {
        private static readonly Broiler.Graphics.Text.ILayoutFont TheFont = new FakeFont();
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => TheFont;
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => new(8 * text.Length, 16);
        public void MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = text.Length; charFitWidth = 8 * text.Length; }
        public double GetWhitespaceWidth(Broiler.Graphics.Text.ILayoutFont font) => 4;
        public ImageIntrinsics GetImageIntrinsics(object imageHandle) => new(300, 150, true);
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
        public ILayoutImageLoader CreateImageLoader(Action<object?, RectangleF, bool> onComplete) => new ImageLoader(onComplete);
        public string FormatListMarker(int number, string style) => string.Empty;
    }

    private sealed class ImageLoader(Action<object?, RectangleF, bool> onComplete) : ILayoutImageLoader
    {
        private static readonly object TheImage = new();

        public object? Image { get; private set; }
        public RectangleF Rectangle => RectangleF.Empty;

        public void LoadImage(string src, IReadOnlyDictionary<string, string>? attributes, Uri baseUrl)
        {
            Image = TheImage;
            onComplete(TheImage, RectangleF.Empty, false);
        }

        public void Dispose() { }
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

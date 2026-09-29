using System;
using System.Collections.Concurrent;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An inline-block aligned <c>text-top</c> or <c>text-bottom</c> is aligned to the content area of
/// its parent's font, not of the block's.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §10.8.1: <c>text-top</c> aligns the top of the box with the top of the parent's content
/// area, and <c>text-bottom</c> its bottom with the bottom of it. The engine took the ascent and
/// descent of the block's font for every box, so a box in an inline box with a larger font stood at
/// the top or bottom of the block's letters, not of the ones beside it.
/// </para>
/// <para>
/// Each line here is in a 320px block with a 16px font and 20px lines: "a", then a span in a 32px
/// font holding "X" and an empty 8×10px inline-block aligned as given. A font is as tall as its size
/// and stands on a baseline four fifths of the way down; the span's 20px lines are 12px shorter than
/// its font, so its letters reach 6px above and below them.
/// </para>
/// </remarks>
public sealed class AtomicBoxParentFontAlignmentTests
{
    private static readonly Uri BaseUrl = new("file:///atomic-box-parent-font-alignment.html");

    /// <summary>
    /// Aligned <c>text-top</c>, the box stands level with the top of "X", 25.6px above the baseline,
    /// and starts the line; "a" is 12.8px down it, and the line is 30.8px tall down to the strut's
    /// end. The box stood at the top of a 16px letter, 6.8px down, level with "a", and the line was
    /// 24.8px tall.
    /// </summary>
    [Fact]
    public void A_Box_Aligned_Text_Top_Stands_At_The_Top_Of_Its_Parents_Letters()
    {
        var (block, a, x, box) = Build(CssConstants.TextTop);
        Layout(block);

        Assert.Equal(0, box.Location.Y - block.Location.Y, 1);
        Assert.Equal(x.Words[0].Top, box.Location.Y, 1);
        Assert.Equal(12.8, a.Words[0].Top - block.Location.Y, 1);
        Assert.Equal(30.8, block.Size.Height, 1);
    }

    /// <summary>
    /// Aligned <c>text-bottom</c>, the box ends level with the bottom of "X", 6.4px below the
    /// baseline, 26px down a 26px line. It ended at the bottom of a 16px letter, 3.2px higher, in a
    /// 24.8px line.
    /// </summary>
    [Fact]
    public void A_Box_Aligned_Text_Bottom_Ends_At_The_Bottom_Of_Its_Parents_Letters()
    {
        var (block, _, x, box) = Build(CssConstants.TextBottom);
        Layout(block);

        Assert.Equal(26, box.Location.Y + box.Size.Height - block.Location.Y, 1);
        Assert.Equal(x.Words[0].Bottom, box.Location.Y + box.Size.Height, 1);
        Assert.Equal(26, block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a box aligned <c>text-top</c> in the block itself
    /// stands level with the top of "a", in a 20px line.
    /// </summary>
    [Fact]
    public void Control_A_Box_Aligned_Text_Top_In_The_Block()
    {
        var (block, a, _, box) = Build(CssConstants.TextTop, inSpan: false);
        Layout(block);

        Assert.Equal(a.Words[0].Top, box.Location.Y, 1);
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// In a block in the root with a 16px font and 20px lines, "a", then a span in a 32px font
    /// holding "X" and an empty inline-block aligned as given; or, when <paramref name="inSpan"/> is
    /// false, "a" and the inline-block in the block itself.
    /// </summary>
    private static (CssBox Block, CssBox A, CssBox X, CssBox Box) Build(string verticalAlign, bool inSpan = true)
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
            Width = "320px",
            LineHeight = "20px",
        };

        var a = Word(block, "a");
        var holder = block;
        var x = a;

        if (inSpan)
        {
            holder = new CssBox(block, new HtmlTag("span", false, null), BaseUrl);
            holder.InheritStyle();
            holder.Display = "inline";
            holder.FontSize = "32px";
            x = Word(holder, "X");
        }

        var box = new CssBox(holder, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = "8px",
            Height = "10px",
            VerticalAlign = verticalAlign,
        };

        return (block, a, x, box);
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

    private static void Layout(CssBox block)
    {
        var root = block.ParentBox!.ParentBox!;
        root.PerformLayout(root.LayoutEnvironment);
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

using System;
using System.Drawing;
using System.Linq;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An atomic inline-level box on a centred or right-aligned line moves with its line, content and
/// all; and one given exactly the room its content needs keeps that content on one line.
/// </summary>
/// <remarks>
/// Google's consent page ("Bevor Sie zu Google weitergehen") styles its buttons as
/// <c>inline-flex</c> pills with white text, centred by <c>text-align</c>. Only an inline-block was
/// moved with its line, so each pill's blue fill moved and its text and rounded clip stayed at the
/// left: a sliver of blue, and white text on white. Its "Weitere Optionen" button sits in a flex
/// column that sizes itself to the button, and came out 50px narrower than its text, which wrapped.
/// </remarks>
public sealed class AtomicInlineAlignmentTests
{
    private static readonly Uri BaseUrl = new("file:///atomic-inline.html");

    private static CssBox Container(float width, string textAlign) =>
        new(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(width, 200),
            TextAlign = textAlign,
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

    /// <summary>An atomic inline-level box, 10px of padding each side, holding an inline box of words.</summary>
    private static (CssBox Box, CssBox Text) Atomic(CssBox parent, string display, params string[] words)
    {
        var box = new CssBox(parent, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = display,
            PaddingLeft = "10px",
            PaddingRight = "10px",
        };
        var text = new CssBox(box, new HtmlTag("span", false, null), BaseUrl) { Display = "inline" };
        for (var i = 0; i < words.Length; i++)
            text.Words.Add(new CssRectWord(text, words[i], i > 0, i < words.Length - 1));
        return (box, text);
    }

    [Theory(Timeout = 600000)]
    [InlineData("inline-block", "center")]
    [InlineData("inline-flex", "center")]
    [InlineData("inline-grid", "center")]
    [InlineData("inline-block", "right")]
    [InlineData("inline-flex", "right")]
    [InlineData("inline-grid", "right")]
    public void The_Box_And_Its_Content_Move_With_The_Line(string display, string textAlign)
    {
        var container = Container(400, textAlign);
        var (box, text) = Atomic(container, display, "X");

        container.PerformLayout(container.LayoutEnvironment);

        double width = box.Size.Width;
        double expected = textAlign == "center" ? (400 - width) / 2 : 400 - width;
        Assert.True(width > 20 && width < 400, $"The box is {width}px wide.");
        Assert.Equal(expected, box.Location.X, 1);
        Assert.Equal(box.Location.X + 10, text.Words.Single().Left, 1);
    }

    /// <summary>
    /// The box's natural width is measured on a wide line first; on a line exactly that wide it
    /// must take that width and keep its words on one line.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("inline-block")]
    [InlineData("inline-flex")]
    public void An_Atomic_Inline_Box_Fits_A_Line_Exactly_As_Wide_As_It_Is(string display)
    {
        var wide = Container(1000, "left");
        var (natural, _) = Atomic(wide, display, "aa", "bb", "cc");
        wide.PerformLayout(wide.LayoutEnvironment);
        float naturalWidth = natural.Size.Width;

        var exact = Container(naturalWidth, "left");
        var (box, text) = Atomic(exact, display, "aa", "bb", "cc");
        exact.PerformLayout(exact.LayoutEnvironment);

        Assert.Equal(naturalWidth, box.Size.Width, 1);
        Assert.Single(text.Words.Select(static w => w.Top).Distinct());
    }


    // Minimal ILayoutEnvironment: every word 8px wide, a space 4px.
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
        public SizeF ViewportSize => new(1000, 1000);
        public PointF RootLocation => PointF.Empty;
        public SizeF ActualSize { get; set; }
        public bool AvoidGeometryAntialias => false;
        public SizeF PageSize => new(1000, 1000);
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

using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A float that follows text in its block is placed at the top of the block, where the text is,
/// wherever the block is on the page.
/// </summary>
/// <remarks>
/// <para>
/// <c>MarginTopCollapse</c> takes the float's previous sibling, the anonymous inline box holding
/// the text, for an empty block whose margins collapse through it, and floors the result so the
/// float does not rise above its block's content top. It measured that floor from the inline box's
/// <c>ActualBottom</c>, which stays 0: an inline box has no block position. The float came out as
/// far below its block's top as the block is below the top of the page.
/// </para>
/// <para>
/// Words here are 8×16px, and the floats 20×30px, in a 320px block 100px down the page.
/// </para>
/// </remarks>
public sealed class FloatAfterTextPlacementTests
{
    private static readonly Uri BaseUrl = new("file:///float-after-text.html");

    /// <summary>
    /// A left or right float after a word is at the top of its block. It was 100px down it.
    /// </summary>
    [Theory]
    [InlineData("left")]
    [InlineData("right")]
    public void A_Float_After_Text_Is_At_The_Top_Of_Its_Block(string side)
    {
        var (block, floated, word) = Lay(side, textFirst: true);

        Assert.Equal(block.ClientTop, floated.Location.Y, 1);
        Assert.Equal(block.ClientTop, word.Top, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a float before the text is at the top of its block.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_A_Float_Before_Text_Is_At_The_Top_Of_Its_Block()
    {
        var (block, floated, _) = Lay("left", textFirst: false);

        Assert.Equal(block.ClientTop, floated.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: after an empty block, a block keeps the margins
    /// that collapse through it: a 20px margin above the empty block and a 10px one below it put
    /// the block 20px below what comes before them.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_An_Empty_Block_Still_Collapses_Through()
    {
        var (root, body) = Page();
        var before = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Height = "10px" };
        _ = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", MarginTop = "20px", MarginBottom = "10px" };
        var after = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Height = "10px" };

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(before.ActualBottom + 20, after.Location.Y, 1);
    }

    /// <summary>A root holding a block, as a page's root holds its body.</summary>
    private static (CssBox Root, CssBox Body) Page()
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        return (root, new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" });
    }

    /// <summary>
    /// A 320px block 100px down the page holding a word and a 20×30px float to
    /// <paramref name="side"/>, the word first when <paramref name="textFirst"/>.
    /// </summary>
    private static (CssBox Block, CssBox Float, CssRect Word) Lay(string side, bool textFirst)
    {
        var (root, body) = Page();
        _ = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Height = "100px" };
        var block = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "320px" };

        CssRect? word = textFirst ? Word(block) : null;
        var floated = new CssBox(block, new HtmlTag("i", false, null), BaseUrl)
        {
            Display = "block",
            Float = side,
            Width = "20px",
            Height = "30px",
        };
        word ??= Word(block);

        root.PerformLayout(root.LayoutEnvironment);
        return (block, floated, word);
    }

    /// <summary>An anonymous inline box in <paramref name="parent"/> holding one word, and the word.</summary>
    private static CssRect Word(CssBox parent)
    {
        var text = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = "A".AsMemory() };
        text.ParseToWords();
        return Assert.Single(text.Words);
    }

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

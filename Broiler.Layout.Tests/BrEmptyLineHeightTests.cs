using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// The empty line a <c>&lt;br&gt;</c> makes is as tall as the <c>&lt;br&gt;</c>'s line height.
/// </summary>
/// <remarks>
/// <para>
/// The host's DOM parser gives a <c>&lt;br&gt;</c> that makes an empty line, after a block, at
/// the start of one or after another <c>&lt;br&gt;</c>, a height of <c>.95em</c>. That line is a
/// line box holding nothing but the <c>&lt;br&gt;</c>, as tall as the line height (CSS2.1 §10.8),
/// and <c>.95em</c> is shorter: 15.2px against a 20px line height, where browsers make the line
/// 20px.
/// </para>
/// <para>
/// Each block here is 320px wide, in a block in the root, and holds its content as the host hands
/// it over: each <c>&lt;br&gt;</c> a block with a height of <c>.95em</c> and a 30px line height,
/// and the inline content around it in anonymous blocks. Words are 8×16px, one line 16px.
/// </para>
/// </remarks>
public sealed class BrEmptyLineHeightTests
{
    private static readonly Uri BaseUrl = new("file:///br-empty-line-height.html");

    /// <summary>
    /// A <c>&lt;br&gt;</c> after a <c>&lt;div&gt;</c> holding a word, at the start of the block,
    /// or after an empty bold element is 30px tall, and the block with it and the line after it.
    /// It was as tall as its <c>.95em</c>, 20.3px in these blocks.
    /// </summary>
    [Theory]
    [InlineData("div", 62)]
    [InlineData("start", 46)]
    [InlineData("empty", 46)]
    public void The_Empty_Line_Is_As_Tall_As_The_Line_Height(string before, float height)
    {
        var (block, brs) = Lay(before, br: 1);

        Assert.Equal(30, brs[0].Size.Height, 1);
        Assert.Equal(height, block.Size.Height, 1);
    }

    /// <summary>
    /// After a link holding a word, the first <c>&lt;br&gt;</c> ends the line, 0px tall, and a
    /// second makes an empty line 30px tall; the block is 62px tall. The second was 20.3px tall.
    /// </summary>
    [Fact]
    public void A_Second_Br_Makes_An_Empty_Line_As_Tall_As_The_Line_Height()
    {
        var (block, brs) = Lay("link", br: 2);

        Assert.Equal(0, brs[0].Size.Height, 1);
        Assert.Equal(30, brs[1].Size.Height, 1);
        Assert.Equal(62, block.Size.Height, 1);
    }

    /// <summary>
    /// With a normal line height, the empty line is as tall as that line height, not the
    /// <c>.95em</c>.
    /// </summary>
    [Fact]
    public void A_Normal_Line_Height_Is_The_Empty_Lines_Height()
    {
        var (_, brs) = Lay("div", br: 1, lineHeight: null);

        Assert.Equal(brs[0].ActualLineHeight, brs[0].Size.Height, 1);
        Assert.NotEqual(Math.Round(0.95 * brs[0].GetEmHeight(), 1), Math.Round(brs[0].Size.Height, 1));
    }

    /// <summary>
    /// Controls, which pass before and after: a <c>&lt;br&gt;</c> after a link holding a word ends
    /// its line, 0px tall, and one given a height of 40px of its own keeps it.
    /// </summary>
    [Theory]
    [InlineData("link", ".95em", 0)]
    [InlineData("div", "40px", 40)]
    public void Control_A_Br_That_Ends_A_Line_Or_Has_A_Height_Of_Its_Own(string before, string height, float expected)
    {
        var (_, brs) = Lay(before, br: 1, brHeight: height);

        Assert.Equal(expected, brs[0].Size.Height, 1);
    }

    private static void Word(CssBox parent)
    {
        var text = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
        text.ParseToWords();
    }

    /// <summary>
    /// A 320px block in a block in the root holding, in turn: what <paramref name="before"/> names
    /// (a <c>&lt;div&gt;</c> with a word, an anonymous block with an empty bold element or with a
    /// link holding a word, or nothing at the start); <paramref name="br"/> <c>&lt;br&gt;</c>
    /// blocks with a height of <paramref name="brHeight"/> and a line height of
    /// <paramref name="lineHeight"/>; and an anonymous block with a word.
    /// </summary>
    private static (CssBox Block, CssBox[] Brs) Lay(string before, int br, string? lineHeight = "30px", string brHeight = ".95em")
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

        switch (before)
        {
            case "div":
                Word(new CssBox(block, new HtmlTag("div", false, null), BaseUrl) { Display = "block" });
                break;
            case "empty":
                _ = new CssBox(new CssBox(block, null, BaseUrl) { Display = "block" }, new HtmlTag("b", false, null), BaseUrl) { Display = "inline" };
                break;
            case "link":
                Word(new CssBox(new CssBox(block, null, BaseUrl) { Display = "block" }, new HtmlTag("a", false, null), BaseUrl) { Display = "inline" });
                break;
        }

        var brs = new CssBox[br];
        for (int i = 0; i < br; i++)
        {
            brs[i] = new CssBox(block, new HtmlTag("br", false, null), BaseUrl) { Display = "block", Height = brHeight };

            if (lineHeight != null)
                brs[i].LineHeight = lineHeight;
        }

        Word(new CssBox(block, null, BaseUrl) { Display = "block" });

        root.PerformLayout(root.LayoutEnvironment);
        return (block, brs);
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

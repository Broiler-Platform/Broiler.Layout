using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A line holding an inline-block is as tall as the strut at least, and the next line starts below
/// it, as it does below a line of words.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §10.8: every line box starts with a strut as tall as the block's line height. The flow
/// ended a line holding only inline-blocks below the lowest box and the strut's descent under it,
/// which for boxes shorter than the strut's ascent is above the strut's bottom: the next line started
/// that much too high, and the block was as much too short. An 84px and a 20px inline-block, each
/// 10px tall, in a 25px block of 16px/20px text, put the second on a line 15.15px down, where
/// browsers start it 20px down.
/// </para>
/// <para>
/// Each block here is 25px wide with 20px lines. Words are 8px wide a letter and 16px tall, and the
/// strut's baseline is 14.8px down its line, 5.2px above its bottom; a 10px inline-block on it
/// stands 4.8px down.
/// </para>
/// </remarks>
public sealed class InlineBlockLineStrutTests
{
    private static readonly Uri BaseUrl = new("file:///inline-block-line-strut.html");

    /// <summary>
    /// An 84px and a 20px inline-block: the second goes to the second line, 20px down, and stands
    /// 24.8px down on its baseline; the block is 40px tall. The second line started 15.2px down,
    /// the box stood 20px down, and the block was 35.2px tall.
    /// </summary>
    [Fact]
    public void The_Line_After_A_Line_Of_Inline_Blocks_Starts_Below_Its_Strut()
    {
        var block = Block();
        Box(block, 84);
        var second = Box(block, 20);
        Layout(block);

        Assert.Equal(24.8, second.Location.Y - block.Location.Y, 1);
        Assert.Equal(40, block.Size.Height, 1);
    }

    /// <summary>
    /// An 84px and two 20px inline-blocks, one to a line: the third stands 44.8px down, on the third
    /// line, and the block is 60px tall. It stood 35.2px down, and the block was 50.4px.
    /// </summary>
    [Fact]
    public void Each_Line_Of_Inline_Blocks_Is_A_Line_Height_Tall()
    {
        var block = Block();
        Box(block, 84);
        Box(block, 20);
        var third = Box(block, 20);
        Layout(block);

        Assert.Equal(44.8, third.Location.Y - block.Location.Y, 1);
        Assert.Equal(60, block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: after a line of text, a 20px inline-block goes to the
    /// second line and stands 24.8px down, and the block is 40px tall.
    /// </summary>
    [Fact]
    public void Control_An_Inline_Block_After_A_Line_Of_Text()
    {
        var block = Block();
        Text(block, "aaaaaa");
        var box = Box(block, 20);
        Layout(block);

        Assert.Equal(24.8, box.Location.Y - block.Location.Y, 1);
        Assert.Equal(40, block.Size.Height, 1);
    }

    /// <summary>A 25px block with 20px lines, in a block in the root.</summary>
    private static CssBox Block()
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        return new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Width = "25px",
            LineHeight = "20px",
        };
    }

    private static void Layout(CssBox block)
    {
        var root = block.ParentBox!.ParentBox!;
        root.PerformLayout(root.LayoutEnvironment);
    }

    /// <summary>An empty 10px tall inline-block of the given width.</summary>
    private static CssBox Box(CssBox parent, int width) =>
        new(parent, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = width + "px",
            Height = "10px",
        };

    /// <summary>
    /// An anonymous inline box in <paramref name="parent"/> holding the text, inheriting the
    /// parent's style as the box a text node makes does.
    /// </summary>
    private static void Text(CssBox parent, string text)
    {
        var box = new CssBox(parent, null, BaseUrl);
        box.InheritStyle();
        box.Display = CssConstants.Inline;
        box.Text = text.AsMemory();
        box.ParseToWords();
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

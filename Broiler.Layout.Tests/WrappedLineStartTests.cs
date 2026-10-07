using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// Where a wrapped line starts, and when a word fits on one: a block's later lines start at its
/// content edge, as its first does; an inline box's later lines start at the block's, without the
/// box's own left edges, which only the line it starts on has; and a box sized to its content keeps
/// that content on one line. Each expectation was measured in Chromium.
/// </summary>
/// <remarks>
/// Every wrapped line in a block's first child took the block's left margin, border and padding a
/// second time, so the second and later lines of a padded or bordered block, or of a blockquote,
/// were set in by that edge again. And a box sized to its content keeps its width as a float, which
/// can come out a hair short of the sum of what it was sized around: the last word broke onto a line
/// of its own, so "Submit now" in a 13.33px button was two lines.
/// </remarks>
public sealed class WrappedLineStartTests
{
    private static readonly Uri BaseUrl = new("file:///wrapped-lines.html");

    /// <summary>
    /// Six 8px words in a 50px-wide block with a 40px left margin, 10px of left padding or a 5px
    /// left border go two to a line, every line starting at the block's content edge.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("margin", 40)]
    [InlineData("padding", 10)]
    [InlineData("border", 5)]
    public void A_Blocks_Later_Lines_Start_At_Its_Content_Edge(string edge, float contentLeft)
    {
        var root = Root(new IntegerEnvironment());
        var block = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = CssConstants.Block, Width = "24px" };
        switch (edge)
        {
            case "margin": block.MarginLeft = "40px"; break;
            case "padding": block.PaddingLeft = "10px"; break;
            default: block.BorderLeftWidth = "5px"; block.BorderLeftStyle = "solid"; break;
        }

        Text(block, "a b c d e f");
        root.PerformLayout(root.LayoutEnvironment);

        var words = block.Boxes[0].Words;
        Assert.Equal(6, words.Count);
        Assert.All(words.Where((_, i) => i % 2 == 0), word => Assert.Equal(contentLeft, word.Left, 1));
        Assert.Equal(3, words.Select(static word => word.Top).Distinct().Count());
    }

    /// <summary>
    /// A span with 20px of left padding after "aa" in a 30px block: its first word, wrapped, starts
    /// 20px in; its next line starts at the block's edge, with room for two words.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Inline_Boxs_Later_Lines_Start_Without_Its_Left_Edge()
    {
        var root = Root(new IntegerEnvironment());
        var block = new CssBox(root, new HtmlTag("p", false, null), BaseUrl) { Display = CssConstants.Block, Width = "30px" };
        Text(block, "aa ");
        var span = new CssBox(block, new HtmlTag("span", false, null), BaseUrl) { Display = CssConstants.Inline, PaddingLeft = "20px" };
        Text(span, "b c d");

        root.PerformLayout(root.LayoutEnvironment);

        var words = span.Boxes[0].Words;
        Assert.Equal(20, words[0].Left, 1);
        Assert.Equal(0, words[1].Left, 1);
        Assert.Equal(12, words[2].Left, 1);
        Assert.Equal(words[1].Top, words[2].Top);
    }

    /// <summary>
    /// "Submit now" measured as a 13.33px sans-serif sets it, in a float sized to it, is one line.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Box_Sized_To_Its_Words_Keeps_Them_On_One_Line()
    {
        var root = Root(new MeasuredEnvironment());
        var container = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = CssConstants.Block, Width = "300px" };
        var box = new CssBox(container, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Float = CssConstants.Left,
            PaddingLeft = "6px",
            PaddingRight = "6px",
        };
        Text(box, "Submit now");

        root.PerformLayout(root.LayoutEnvironment);

        var words = box.Boxes[0].Words;
        Assert.Equal(words[0].Top, words[1].Top);
    }

    private static CssBox Root(ILayoutEnvironment environment) =>
        new(null, new HtmlTag("html", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = environment,
        };

    private static void Text(CssBox parent, string text)
    {
        var box = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        box.ParseToWords();
    }

    // Every word 8px wide and 16px tall, a space 4px, a 1024×768 viewport.
    private sealed class IntegerEnvironment : FakeEnvironment
    {
        protected override double WordWidth(string text) => 8;
        public override double GetWhitespaceWidth(Broiler.Graphics.Text.ILayoutFont font) => 4;
    }

    // The widths a 13.33px sans-serif gives "Submit", "now" and a space.
    private sealed class MeasuredEnvironment : FakeEnvironment
    {
        private static readonly Dictionary<string, double> Widths = new()
        {
            ["Submit"] = 41.393653869628906,
            ["now"] = 24.398487091064453,
        };

        protected override double WordWidth(string text) => Widths.GetValueOrDefault(text.Trim(), 8);
        public override double GetWhitespaceWidth(Broiler.Graphics.Text.ILayoutFont font) => 3.6951639652252197;
    }

    private abstract class FakeEnvironment : ILayoutEnvironment
    {
        private static readonly Broiler.Graphics.Text.ILayoutFont TheFont = new FakeFont();
        protected abstract double WordWidth(string text);
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => TheFont;
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => new((float)WordWidth(text), 16);
        public void MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = text.Length; charFitWidth = WordWidth(text); }
        public abstract double GetWhitespaceWidth(Broiler.Graphics.Text.ILayoutFont font);
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
        public double Size => 10;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}

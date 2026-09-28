using System;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// Content that <c>vertical-align</c> raises above the top of a line after the first pushes that
/// line, and every line after it, down, as it already did on the first line.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §10.8: a line box reaches from the top of the highest box on it to the bottom of the
/// lowest, and line boxes stack. The engine aligned each line's content where the flow put the line,
/// and moved the content down only when it reached above the block's own top, which only the first
/// line's can: content raised above a later line's top reached into the line above it, and the
/// block was as much too short. <c>alpha beta gamma delta&lt;img style="vertical-align: 10px"&gt;
/// omega</c> put the image on the second line 14.85px down its block, over the first line, where
/// browsers start it 20px down and make the block 45px tall.
/// </para>
/// <para>
/// Each block here is 150px wide with 20px lines, in a block in the root. Words are 8px wide a
/// letter and 16px tall, and stand half their 4px of leading down their line, so a line's baseline
/// is 14.8px down it; "alpha beta gamma" fills the first line and "delta" starts the second. The
/// raised box is an 8 × 10px inline-block, or image.
/// </para>
/// </remarks>
public sealed class RaisedLineRestackTests
{
    private static readonly Uri BaseUrl = new("file:///raised-line-restack.html");

    /// <summary>
    /// A box raised 10px on the second line reaches 5.2px above its top, so the line moves 5.2px
    /// down: the box starts at the line's top, 20px down, "delta" 27.2px down, and the block is
    /// 45.2px tall. The box started 14.8px down, over the first line, and the block was 40px.
    /// </summary>
    [Fact]
    public void A_Box_Raised_Above_The_Second_Lines_Top_Moves_The_Line_Down()
    {
        var t = Build(b => { Text(b, "alpha beta gamma delta"); Raised(b, "10px"); Text(b, " omega"); });
        Layout(t);

        Assert.Equal(20, t.Raised[0].Location.Y - t.Block.ClientTop, 1);
        Assert.Equal(27.2, WordTop(t, "delta"), 1);
        Assert.Equal(27.2, WordTop(t, "omega"), 1);
        Assert.Equal(45.2, t.Block.Size.Height, 1);
    }

    /// <summary>
    /// A line after the one moved down moves with it: with a third line, "zeta" is 5.2px lower too,
    /// 47.2px down, and the block 65.2px tall.
    /// </summary>
    [Fact]
    public void The_Lines_After_It_Move_Down_With_It()
    {
        var t = Build(b => { Text(b, "alpha beta gamma delta"); Raised(b, "10px"); Text(b, " omega epsilon zeta"); });
        Layout(t);

        Assert.Equal(47.2, WordTop(t, "zeta"), 1);
        Assert.Equal(65.2, t.Block.Size.Height, 1);
    }

    /// <summary>
    /// Raised content on the first line and on the second: each line moves down by as much as its
    /// own content reaches above it, 5.2px, and the second by the first's too. The second box
    /// starts 25.2px down, and the block is 50.4px tall.
    /// </summary>
    [Fact]
    public void Each_Line_Moves_Down_By_Its_Own_Content_And_The_Lines_Above()
    {
        var t = Build(b =>
        {
            Text(b, "alpha");
            Raised(b, "10px");
            Text(b, " beta gamma delta");
            Raised(b, "10px");
            Text(b, " omega");
        });
        Layout(t);

        Assert.Equal(0, t.Raised[0].Location.Y - t.Block.ClientTop, 1);
        Assert.Equal(25.2, t.Raised[1].Location.Y - t.Block.ClientTop, 1);
        Assert.Equal(50.4, t.Block.Size.Height, 1);
    }

    /// <summary>
    /// A 40px box aligned <c>middle</c> on the second line reaches above its top, and the line moves
    /// down so the box starts at it.
    /// </summary>
    [Fact]
    public void A_Tall_Middle_Aligned_Box_Moves_The_Line_Down_Too()
    {
        var t = Build(b => { Text(b, "alpha beta gamma delta"); Raised(b, CssConstants.Middle, height: 40); });
        Layout(t);

        Assert.Equal(20, t.Raised[0].Location.Y - t.Block.ClientTop, 1);
        Assert.True(WordTop(t, "delta") > 20.5);
    }

    /// <summary>
    /// A 40px box aligned <c>bottom</c> on the second line ends where the rest of the line does,
    /// 20px below its top, and so reaches 20px above it: the line moves down 20px, the box starts at
    /// its top, 20px down, "delta" stands at its bottom, 42px down, and the block is 60px tall. The
    /// box started at the block's top, over the first line, and the block was 40px.
    /// </summary>
    [Fact]
    public void A_Tall_Box_Aligned_Bottom_Moves_The_Line_Down()
    {
        var t = Build(b => { Text(b, "alpha beta gamma delta"); Raised(b, CssConstants.Bottom, height: 40); Text(b, " omega"); });
        Layout(t);

        Assert.Equal(20, t.Raised[0].Location.Y - t.Block.ClientTop, 1);
        Assert.Equal(42, WordTop(t, "delta"), 1);
        Assert.Equal(42, WordTop(t, "omega"), 1);
        Assert.Equal(60, t.Block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a 40px box aligned <c>top</c> on the second line
    /// starts at the line's top, 20px down, with "delta" 22px down beside it, and the block is 60px
    /// tall.
    /// </summary>
    [Fact]
    public void Control_A_Tall_Box_Aligned_Top_On_The_Second_Line()
    {
        var t = Build(b => { Text(b, "alpha beta gamma delta"); Raised(b, CssConstants.Top, height: 40); Text(b, " omega"); });
        Layout(t);

        Assert.Equal(20, t.Raised[0].Location.Y - t.Block.ClientTop, 1);
        Assert.Equal(22, WordTop(t, "delta"), 1);
        Assert.Equal(60, t.Block.Size.Height, 1);
    }

    /// <summary>
    /// Laid out a second time, the second line is 5.2px down still, and the block 45.2px tall.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Lines()
    {
        var t = Build(b => { Text(b, "alpha beta gamma delta"); Raised(b, "10px"); Text(b, " omega"); });
        Layout(t);
        Layout(t);

        Assert.Equal(27.2, WordTop(t, "delta"), 1);
        Assert.Equal(45.2, t.Block.Size.Height, 1);
    }

    /// <summary>
    /// An image raised 10px on the second line moves the line down as the inline-block does, and
    /// its box, which is where it is drawn and measured, moves with it: the image starts at the
    /// line's top, 20px down, and stays 10px tall. It started 14.8px down, over the first line.
    /// </summary>
    [Fact]
    public void A_Raised_Image_Moves_Down_With_Its_Line_And_Keeps_Its_Height()
    {
        var t = Build(b => { Text(b, "alpha beta gamma delta"); Image(b, "10px"); Text(b, " omega"); });
        Layout(t);

        var image = t.Block.Boxes.OfType<CssBoxImage>().Single();
        Assert.Equal(20, image.Location.Y - t.Block.ClientTop, 1);
        Assert.Equal(10, image.Size.Height, 1);
        Assert.Equal(45.2, t.Block.Size.Height, 1);
    }

    /// <summary>
    /// An image raised 10px on the first line moves every line down, and its box with them: it
    /// starts at the block's top and stays 10px tall. Its word moved and its box did not, and it was
    /// drawn and measured 5.2px above the block.
    /// </summary>
    [Fact]
    public void An_Image_Raised_On_The_First_Line_Starts_At_The_Blocks_Top()
    {
        var t = Build(b => { Text(b, "alpha"); Image(b, "10px"); Text(b, " beta gamma delta omega"); });
        Layout(t);

        var image = t.Block.Boxes.OfType<CssBoxImage>().Single();
        Assert.Equal(0, image.Location.Y - t.Block.ClientTop, 1);
        Assert.Equal(10, image.Size.Height, 1);
        Assert.Equal(27.2, WordTop(t, "delta"), 1);
    }

    /// <summary>
    /// An inline-block holding words of its own, as a <c>::before</c> with
    /// <c>display: inline-block</c> does, raised 5px on the second line beside the image, moves
    /// down with the line and stays as tall as its words, 16px: it starts 22.2px down.
    /// </summary>
    [Fact]
    public void An_Inline_Block_Holding_Words_Moves_Down_With_Its_Line_And_Keeps_Its_Height()
    {
        CssBox worded = null!;
        var t = Build(b =>
        {
            Text(b, "alpha beta gamma delta");
            Image(b, "10px");
            worded = new CssBox(b, null, BaseUrl) { Display = CssConstants.InlineBlock, Text = "xy".AsMemory(), VerticalAlign = "5px" };
            worded.ParseToWords();
        });
        Layout(t);
        Layout(t);

        Assert.Equal(22.2, worded.Location.Y - t.Block.ClientTop, 1);
        Assert.Equal(16, worded.Size.Height, 1);
    }

    /// <summary>
    /// Controls: a box raised on the first line moves every line down, and it starts at the
    /// block's top; and an inline box with 10px of top padding on the second line, which is not
    /// part of the line, moves nothing, its word standing 2px down the line as every word does.
    /// </summary>
    [Fact]
    public void Control_The_First_Line_And_An_Inline_Boxs_Padding()
    {
        var first = Build(b => { Text(b, "alpha"); Raised(b, "10px"); Text(b, " beta gamma delta omega"); });
        Layout(first);

        Assert.Equal(0, first.Raised[0].Location.Y - first.Block.ClientTop, 1);
        Assert.Equal(27.2, WordTop(first, "delta"), 1);

        var padded = Build(b =>
        {
            Text(b, "alpha beta gamma ");
            var span = new CssBox(b, new HtmlTag("span", false, null), BaseUrl) { Display = "inline", LineHeight = "20px", PaddingTop = "10px" };
            Text(span, "delta");
        });
        Layout(padded);

        Assert.Equal(22, WordTop(padded, "delta"), 1);
        Assert.Equal(40, padded.Block.Size.Height, 1);
    }

    /// <summary>The root, the 150px block and the raised boxes in it, in document order.</summary>
    private sealed record Tree(CssBox Root, CssBox Block)
    {
        public CssBox[] Raised => Block.Boxes.Where(b => b.Display == CssConstants.InlineBlock).ToArray();
    }

    private static Tree Build(Action<CssBox> fill)
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
            Width = "150px",
            LineHeight = "20px",
        };

        fill(block);
        return new Tree(root, block);
    }

    private static void Layout(Tree t) => t.Root.PerformLayout(t.Root.LayoutEnvironment);

    private static void Text(CssBox parent, string text)
    {
        var box = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        box.ParseToWords();
    }

    private static void Raised(CssBox parent, string verticalAlign, int height = 10) =>
        _ = new CssBox(parent, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = "8px",
            Height = height + "px",
            VerticalAlign = verticalAlign,
        };

    private static void Image(CssBox parent, string verticalAlign) =>
        _ = new CssBoxImage(parent, new HtmlTag("img", true, new System.Collections.Generic.Dictionary<string, string> { ["src"] = "i.png" }), BaseUrl)
        {
            Display = CssConstants.Inline,
            Width = "8px",
            Height = "10px",
            VerticalAlign = verticalAlign,
        };

    private static double WordTop(Tree t, string text) =>
        Descendants(t.Block).SelectMany(b => b.Words).Single(w => w.Text == text).Top - t.Block.ClientTop;

    private static System.Collections.Generic.IEnumerable<CssBox> Descendants(CssBox box)
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

        public void LoadImage(string src, System.Collections.Generic.IReadOnlyDictionary<string, string>? attributes, Uri baseUrl)
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

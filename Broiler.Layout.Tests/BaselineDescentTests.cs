using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A line reaches a strut's descent below an inline-block only when the inline-block stands on the
/// baseline; one aligned any other way leaves the line ending at its own bottom or the text's.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §10.8: the strut's descent lies below the baseline. An inline-block standing on the
/// baseline with its bottom edge has that descent below it, and the engine extends the line by it.
/// It extended the line by it below every such inline-block, whatever its <c>vertical-align</c>, so
/// an empty 30px inline-block aligned <c>middle</c> made a 33.2px line of 16px text, where browsers
/// make it 30px. An image aligned other than on the baseline already went without it.
/// </para>
/// <para>
/// Each line here is in a 320px block and is 16px tall; words are 16px tall and 8px wide a letter,
/// and stand on a baseline 12.8px below their top, the strut's descent 3.2px below it.
/// </para>
/// </remarks>
public sealed class BaselineDescentTests
{
    private static readonly Uri BaseUrl = new("file:///baseline-descent.html");

    /// <summary>
    /// An empty 30px inline-block aligned middle, between "a" and "b", reaches above the words and
    /// moves the line down; the line ends at its bottom, 30px down. It ended 33.2px down.
    /// </summary>
    [Fact]
    public void A_Box_Aligned_Middle_Ends_The_Line_At_Its_Bottom()
    {
        var tree = Build();
        var box = EmptyBox(tree, 30, CssConstants.Middle);
        Layout(tree);

        Assert.Equal(0, tree.Top(box), 1);
        Assert.Equal(30, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// An empty 20px inline-block aligned top makes a 20px line. It made a 23.2px one.
    /// </summary>
    [Fact]
    public void A_Box_Aligned_Top_Ends_The_Line_At_Its_Bottom()
    {
        var tree = Build();
        EmptyBox(tree, 20, CssConstants.Top);
        Layout(tree);

        Assert.Equal(20, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// An empty 10px inline-block aligned text-bottom ends where the text does, and the line is
    /// 16px tall. It was 19.2px tall.
    /// </summary>
    [Fact]
    public void A_Box_Aligned_Text_Bottom_Ends_Where_The_Text_Does()
    {
        var tree = Build();
        var box = EmptyBox(tree, 10, CssConstants.TextBottom);
        Layout(tree);

        Assert.Equal(6, tree.Top(box), 1);
        Assert.Equal(16, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// An empty 30px inline-block lowered by <c>vertical-align: -5px</c> stands 5px below the
    /// baseline, the words 12.2px down, and the line ends at the box's bottom, 30px down. It ended
    /// 33.2px down.
    /// </summary>
    [Fact]
    public void A_Box_Lowered_From_The_Baseline_Ends_The_Line_At_Its_Bottom()
    {
        var tree = Build();
        var box = EmptyBox(tree, 30, "-5px");
        Layout(tree);

        Assert.Equal(0, tree.Top(box), 1);
        Assert.Equal(12.2, tree.Top(tree.Before), 1);
        Assert.Equal(30, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// Laid out a second time, the line with the box aligned middle is 30px tall still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Line()
    {
        var tree = Build();
        EmptyBox(tree, 30, CssConstants.Middle);
        Layout(tree);
        Layout(tree);

        Assert.Equal(30, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: an empty 30px inline-block on the baseline has the
    /// strut's descent below it, and the line is 33.2px tall.
    /// </summary>
    [Fact]
    public void Control_A_Box_On_The_Baseline_Has_The_Descent_Below_It()
    {
        var tree = Build();
        var box = EmptyBox(tree, 30, CssConstants.Baseline);
        Layout(tree);

        Assert.Equal(0, tree.Top(box), 1);
        Assert.Equal(33.2, tree.Block.Size.Height, 1);
    }

    /// <summary>The root, the 320px block, and the words "a" and "b" on its line.</summary>
    private sealed record Tree(CssBox Root, CssBox Block, CssBox Before, CssBox After)
    {
        /// <summary>How far the box's first word, or the box, stands below the block's top.</summary>
        public double Top(CssBox box) =>
            (box.Words.Count > 0 ? box.Words[0].Top : box.Location.Y) - Block.Location.Y;
    }

    /// <summary>In a 320px block in the root, the words "a" and "b".</summary>
    private static Tree Build()
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
        var after = Word(block, "b");

        return new Tree(root, block, before, after);
    }

    /// <summary>Puts an empty inline-block 8px wide, as tall and aligned as given, before "b".</summary>
    private static CssBox EmptyBox(Tree tree, int height, string verticalAlign)
    {
        var box = new CssBox(tree.Block, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = "inline-block",
            Width = "8px",
            Height = $"{height}px",
            VerticalAlign = verticalAlign,
        };

        box.SetBeforeBox(tree.After);
        return box;
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

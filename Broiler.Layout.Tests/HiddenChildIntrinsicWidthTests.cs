using System;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A child with <c>display: none</c> generates no box, so its width adds nothing to the width its
/// parent shrinks to fit.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §9.2.4: <c>display: none</c> makes an element generate no box at all. The per-child
/// walks that measure a shrink-to-fit box's widths took a child's own <c>width</c> without asking
/// whether the child is displayed, so a hidden block 300px wide made a float, an absolutely
/// positioned box or a block sized <c>max-content</c> around it 300px wide, where browsers size
/// it to the rest of its content. The walk an inline-block or a table cell is measured by skips
/// such a child already.
/// </para>
/// <para>
/// Each box here is in a 500px block and holds a one-letter word, 8px wide, and a hidden block;
/// words are 16px tall and 8px wide a letter.
/// </para>
/// </remarks>
public sealed class HiddenChildIntrinsicWidthTests
{
    private static readonly Uri BaseUrl = new("file:///hidden-child-intrinsic-width.html");

    /// <summary>
    /// A float, an absolutely positioned box with an auto width, and a block whose width is
    /// <c>max-content</c>, holding the word and a hidden block 300px wide, are 8px wide, as wide as
    /// the word. Each was 300px wide.
    /// </summary>
    [Theory]
    [InlineData("float")]
    [InlineData("absolute")]
    [InlineData("max-content")]
    public void A_Hidden_Child_Takes_No_Room(string kind)
    {
        var tree = Build(kind);
        Hidden(tree.Holder, "300px");
        Layout(tree);

        Assert.Equal(8, tree.Box.Size.Width, 1);
    }

    /// <summary>
    /// A block whose width is <c>fit-content</c>, holding eighty more one-letter words, which do not
    /// fit on one line in 500px, and a hidden block 600px wide, fits the 500px block it is in:
    /// 500px wide. It was 600px wide, as wide as the hidden block.
    /// </summary>
    [Fact]
    public void A_Hidden_Child_Does_Not_Widen_The_Space_For_Fit_Content()
    {
        var tree = Build("fit-content");
        Word(tree.Holder, string.Join(' ', Enumerable.Repeat("X", 80)));
        Hidden(tree.Holder, "600px");
        Layout(tree);

        Assert.Equal(500, tree.Box.Size.Width, 1);
    }

    /// <summary>
    /// Laid out a second time, the float is 8px wide still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Width()
    {
        var tree = Build("float");
        Hidden(tree.Holder, "300px");
        Layout(tree);
        Layout(tree);

        Assert.Equal(8, tree.Box.Size.Width, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: an inline-block, a table with an auto width, and a
    /// block whose width is <c>min-content</c>, holding the word and a hidden block 300px wide, are
    /// 8px wide, as the walk they are measured by skips a hidden child already.
    /// </summary>
    [Theory]
    [InlineData("inline-block")]
    [InlineData("table")]
    [InlineData("min-content")]
    public void Control_Boxes_Measured_Without_The_Hidden_Child(string kind)
    {
        var tree = Build(kind);
        Hidden(tree.Holder, "300px");
        Layout(tree);

        Assert.Equal(8, tree.Box.Size.Width, 1);
    }

    /// <summary>
    /// Control, which passes before and after: the float holding the word and a displayed block
    /// 300px wide is 300px wide.
    /// </summary>
    [Fact]
    public void Control_A_Displayed_Child_Takes_Room()
    {
        var tree = Build("float");
        Hidden(tree.Holder, "300px", displayed: true);
        Layout(tree);

        Assert.Equal(300, tree.Box.Size.Width, 1);
    }

    /// <summary>
    /// The root, the box whose width is measured, and the box holding the word and the hidden
    /// child: the box itself, or for a table its cell.
    /// </summary>
    private sealed record Tree(CssBox Root, CssBox Box, CssBox Holder);

    /// <summary>
    /// In a 500px block in the root, the box <paramref name="kind"/> names, holding a one-letter
    /// word: an inline-block, a left float, an absolutely positioned box with an auto width, a block
    /// with the intrinsic width <paramref name="kind"/> names, or a table with one row of one cell
    /// holding it.
    /// </summary>
    private static Tree Build(string kind)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var container = new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Width = "500px",
            Position = CssConstants.Relative,
        };

        CssBox box;
        CssBox holder;

        if (kind == "table")
        {
            box = new CssBox(container, new HtmlTag("table", false, null), BaseUrl) { Display = "table" };
            var rows = new CssBox(box, new HtmlTag("tbody", false, null), BaseUrl) { Display = "table-row-group" };
            var row = new CssBox(rows, new HtmlTag("tr", false, null), BaseUrl) { Display = "table-row" };
            holder = new CssBox(row, new HtmlTag("td", false, null), BaseUrl) { Display = "table-cell", Position = CssConstants.Relative };
        }
        else
        {
            box = new CssBox(container, new HtmlTag("div", false, null), BaseUrl)
            {
                Display = kind == "inline-block" ? "inline-block" : "block",
                Position = kind == "absolute" ? CssConstants.Absolute : CssConstants.Relative,
            };

            if (kind == "float")
                box.Float = CssConstants.Left;
            else if (kind.EndsWith("-content", StringComparison.Ordinal))
                box.Width = kind;

            holder = box;
        }

        Word(holder, "X");

        return new Tree(root, box, holder);
    }

    /// <summary>
    /// Adds to <paramref name="parent"/> a block 10px tall and <paramref name="width"/> wide, with
    /// <c>display: none</c> unless <paramref name="displayed"/>.
    /// </summary>
    private static CssBox Hidden(CssBox parent, string width, bool displayed = false) =>
        new(parent, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = displayed ? "block" : CssConstants.None,
            Width = width,
            Height = "10px",
        };

    private static void Word(CssBox parent, string text)
    {
        var word = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        word.ParseToWords();
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
        public double Size => 16;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}

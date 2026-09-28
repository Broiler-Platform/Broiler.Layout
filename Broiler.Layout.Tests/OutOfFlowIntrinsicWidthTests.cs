using System;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An absolutely or fixed positioned child takes no room in its parent, so it adds nothing to the
/// width its parent shrinks to fit.
/// </summary>
/// <remarks>
/// <para>
/// CSS Sizing 3 §5: the intrinsic sizes of a box come from its in-flow content, and an absolutely
/// positioned box is out of flow. The walks that measure a box's min- and max-content width went
/// through every child, positioned or not, so a dropdown menu 300px wide, absolutely positioned
/// in an inline-block, a float or a table cell, made the box around it 300px wide, where browsers
/// size it to the rest of its content.
/// </para>
/// <para>
/// Each box here is in a 500px block and holds a one-letter word, 8px wide, and a positioned child;
/// words are 16px tall and 8px wide a letter.
/// </para>
/// </remarks>
public sealed class OutOfFlowIntrinsicWidthTests
{
    private static readonly Uri BaseUrl = new("file:///out-of-flow-intrinsic-width.html");

    /// <summary>
    /// An inline-block, a float, and a table with an auto width whose cell holds the word and an
    /// absolutely positioned block 300px wide are 8px wide, as wide as the word. Each was 300px wide.
    /// </summary>
    [Theory]
    [InlineData("inline-block")]
    [InlineData("float")]
    [InlineData("table")]
    public void An_Absolutely_Positioned_Child_Takes_No_Room(string kind)
    {
        var tree = Build(kind);
        Positioned(tree.Holder, CssConstants.Absolute, width: "300px");
        Layout(tree);

        Assert.Equal(8, tree.Box.Size.Width, 1);
    }

    /// <summary>
    /// With a fixed positioned block instead, the inline-block is 8px wide too. It was 300px wide.
    /// </summary>
    [Fact]
    public void A_Fixed_Positioned_Child_Takes_No_Room()
    {
        var tree = Build("inline-block");
        Positioned(tree.Holder, CssConstants.Fixed, width: "300px");
        Layout(tree);

        Assert.Equal(8, tree.Box.Size.Width, 1);
    }

    /// <summary>
    /// An absolutely positioned child with no width of its own, holding a 40-letter word, 320px
    /// wide, adds nothing either: the inline-block is 8px wide. It was 320px wide.
    /// </summary>
    [Fact]
    public void The_Words_Of_An_Absolutely_Positioned_Child_Take_No_Room()
    {
        var tree = Build("inline-block");
        var positioned = Positioned(tree.Holder, CssConstants.Absolute, width: null);
        Word(positioned, new string('X', 40));
        Layout(tree);

        Assert.Equal(8, tree.Box.Size.Width, 1);
    }

    /// <summary>
    /// A table 100px wide whose cell holds the word and an absolutely positioned block holding the
    /// 40-letter word stays 100px wide. It was 320px wide, as wide as that word.
    /// </summary>
    [Fact]
    public void The_Words_Of_An_Absolutely_Positioned_Child_Do_Not_Widen_A_Table()
    {
        var tree = Build("table");
        tree.Box.Width = "100px";
        var positioned = Positioned(tree.Holder, CssConstants.Absolute, width: null);
        Word(positioned, new string('X', 40));
        Layout(tree);

        Assert.Equal(100, tree.Box.Size.Width, 1);
    }

    /// <summary>
    /// A block whose width is <c>min-content</c>, <c>max-content</c> or <c>fit-content</c>, holding
    /// the word and an absolutely positioned block 300px wide, is 8px wide. Each was 300px wide.
    /// </summary>
    [Theory]
    [InlineData("min-content")]
    [InlineData("max-content")]
    [InlineData("fit-content")]
    public void An_Absolutely_Positioned_Child_Takes_No_Room_In_An_Intrinsic_Width(string width)
    {
        var tree = Build(width);
        Positioned(tree.Holder, CssConstants.Absolute, width: "300px");
        Layout(tree);

        Assert.Equal(8, tree.Box.Size.Width, 1);
    }

    /// <summary>
    /// A block whose width is <c>fit-content</c>, holding eighty more one-letter words, which do not
    /// fit on one line in 500px, and an absolutely positioned block 600px wide, fits the 500px block
    /// it is in: 500px wide. It was 600px wide, as wide as the positioned block.
    /// </summary>
    [Fact]
    public void An_Absolutely_Positioned_Child_Does_Not_Widen_The_Space_For_Fit_Content()
    {
        var tree = Build("fit-content");
        Word(tree.Holder, string.Join(' ', Enumerable.Repeat("X", 80)));
        Positioned(tree.Holder, CssConstants.Absolute, width: "600px");
        Layout(tree);

        Assert.Equal(500, tree.Box.Size.Width, 1);
    }

    /// <summary>
    /// An absolutely positioned box with an auto width, holding the word and an absolutely
    /// positioned block 300px wide, shrinks to the word, 8px. It was 300px wide.
    /// </summary>
    [Fact]
    public void An_Absolutely_Positioned_Box_Shrinks_Around_Its_Flow_Only()
    {
        var tree = Build("absolute");
        Positioned(tree.Holder, CssConstants.Absolute, width: "300px");
        Layout(tree);

        Assert.Equal(8, tree.Box.Size.Width, 1);
    }

    /// <summary>
    /// Laid out a second time, the inline-block is 8px wide still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Width()
    {
        var tree = Build("inline-block");
        Positioned(tree.Holder, CssConstants.Absolute, width: "300px");
        Layout(tree);
        Layout(tree);

        Assert.Equal(8, tree.Box.Size.Width, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: a block 300px wide in the inline-block's flow, and a
    /// relatively positioned one, which stays in flow, make it 300px wide.
    /// </summary>
    [Theory]
    [InlineData("static")]
    [InlineData("relative")]
    public void Control_A_Child_In_Flow_Takes_Room(string position)
    {
        var tree = Build("inline-block");
        Positioned(tree.Holder, position, width: "300px");
        Layout(tree);

        Assert.Equal(300, tree.Box.Size.Width, 1);
    }

    /// <summary>
    /// The root, the box whose width is measured, and the box holding the word and the positioned
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
    /// Adds to <paramref name="parent"/> a 10px tall block with the given position, and the given
    /// width, auto when null.
    /// </summary>
    private static CssBox Positioned(CssBox parent, string position, string? width)
    {
        var box = new CssBox(parent, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Position = position,
            Height = "10px",
        };

        if (width != null)
            box.Width = width;

        if (position is CssConstants.Absolute or CssConstants.Fixed)
        {
            box.Top = "0";
            box.Left = "0";
        }

        return box;
    }

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

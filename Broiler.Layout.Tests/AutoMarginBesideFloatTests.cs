using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// Auto margins place a box that establishes a formatting context in the space the floats beside
/// it leave, not in its containing block (CSS2.1 §10.3.3, §9.5).
/// </summary>
/// <remarks>
/// <para>
/// Beside floats, the border box of such a box goes where the floats leave room, and its auto
/// margins take what is left of that room. The engine resolved them against the whole containing
/// block, so after a 100px left float a 300px box with <c>margin: 0 auto</c> was centred in the
/// page, 362px in, where browsers centre it in the 924px beside the float, 412px in.
/// </para>
/// <para>
/// Each box here is 300px wide, a block with <c>overflow: hidden</c> or a table, in a 1024px
/// container in a body in a root. The floats are 50px tall.
/// </para>
/// </remarks>
public sealed class AutoMarginBesideFloatTests
{
    private static readonly Uri BaseUrl = new("file:///auto-margin-beside-float.html");

    /// <summary>
    /// After a 100px left float, the box is centred in the 924px beside it, 412px in. It was 362px
    /// in.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData(false)]
    [InlineData(true)]
    public void A_Box_Is_Centred_Beside_A_Left_Float(bool table)
    {
        var (root, container) = Container();
        Float(container, CssConstants.Left, 100);
        var box = Box(container, table, "auto", "auto");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(412, box.Location.X, 1);
    }

    /// <summary>
    /// Before a 100px right float, it is centred in the 924px left of it, 312px in. It was 362px in.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData(false)]
    [InlineData(true)]
    public void A_Box_Is_Centred_Beside_A_Right_Float(bool table)
    {
        var (root, container) = Container();
        Float(container, CssConstants.Right, 100);
        var box = Box(container, table, "auto", "auto");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(312, box.Location.X, 1);
    }

    /// <summary>
    /// Between a 100px left float and a 200px right one, it is centred in the 724px between them,
    /// 312px in. It was 362px in.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData(false)]
    [InlineData(true)]
    public void A_Box_Is_Centred_Between_Floats(bool table)
    {
        var (root, container) = Container();
        Float(container, CssConstants.Left, 100);
        Float(container, CssConstants.Right, 200);
        var box = Box(container, table, "auto", "auto");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(312, box.Location.X, 1);
    }

    /// <summary>
    /// In a container with 50px of left padding, after a 100px left float, it is centred in the
    /// 874px beside the float, 437px in. It was 387px in.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData(false)]
    [InlineData(true)]
    public void A_Box_Is_Centred_Beside_A_Float_In_A_Padded_Container(bool table)
    {
        var (root, container) = Container();
        container.PaddingLeft = "50px";
        Float(container, CssConstants.Left, 100);
        var box = Box(container, table, "auto", "auto");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(437, box.Location.X, 1);
    }

    /// <summary>
    /// Its margins are what it leaves between it and the container: 412px on the left and 312px
    /// on the right, where they were 362px each.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Used_Margins_Are_What_The_Box_Leaves()
    {
        var (root, container) = Container();
        Float(container, CssConstants.Left, 100);
        var box = Box(container, false, "auto", "auto");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(412, box.ActualMarginLeft, 1);
        Assert.Equal(312, box.ActualMarginRight, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: with <c>margin-left: auto</c> alone the box goes
    /// against the right edge, 724px in, float or not; with no float it is centred in the
    /// container, 362px in.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData(false, true, "auto", "0", 724)]
    [InlineData(true, true, "auto", "0", 724)]
    [InlineData(false, false, "auto", "auto", 362)]
    [InlineData(true, false, "auto", "auto", 362)]
    public void Control_A_Left_Auto_Margin_Or_No_Float(bool table, bool withFloat, string marginLeft, string marginRight, float x)
    {
        var (root, container) = Container();
        if (withFloat)
            Float(container, CssConstants.Left, 100);
        var box = Box(container, table, marginLeft, marginRight);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(x, box.Location.X, 1);
    }

    /// <summary>A root holding a body holding a 1024px container. Returns the root and the container.</summary>
    private static (CssBox Root, CssBox Container) Container()
    {
        var root = new CssBox(null, new HtmlTag("html", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };
        var body = Block(root);
        return (root, Block(body));
    }

    /// <summary>Adds a float to the given side, as wide as given and 50px tall.</summary>
    private static void Float(CssBox container, string side, int width)
    {
        var floated = Block(container);
        floated.Float = side;
        floated.Width = $"{width}px";
        floated.Height = "50px";
    }

    /// <summary>
    /// Adds a 300px wide box with the given margins: a table holding one cell with "X", or a
    /// block with <c>overflow: hidden</c>.
    /// </summary>
    private static CssBox Box(CssBox container, bool table, string marginLeft, string marginRight)
    {
        CssBox box;
        if (table)
        {
            box = new CssBox(container, new HtmlTag("table", false, null), BaseUrl) { Display = "table" };
            var body = new CssBox(box, new HtmlTag("tbody", false, null), BaseUrl) { Display = "table-row-group" };
            var row = new CssBox(body, new HtmlTag("tr", false, null), BaseUrl) { Display = "table-row" };
            var cell = new CssBox(row, new HtmlTag("td", false, null), BaseUrl) { Display = "table-cell" };
            cell.Words.Add(new CssRectWord(cell, "X", false, false));
        }
        else
        {
            box = Block(container);
            box.Overflow = "hidden";
            box.Height = "20px";
        }

        box.Width = "300px";
        box.MarginLeft = marginLeft;
        box.MarginRight = marginRight;
        return box;
    }

    private static CssBox Block(CssBox parent) =>
        new(parent, new HtmlTag("div", false, null), BaseUrl) { Display = CssConstants.Block };

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

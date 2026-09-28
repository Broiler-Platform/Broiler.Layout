using System;
using System.Drawing;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS Flexbox §9.7 step 4b: when the flex factors of a line's unfrozen items add up to less than
/// 1, they take only that fraction of the line's initial free space, and leave the rest.
/// </summary>
/// <remarks>
/// <para>
/// Both flex loops handed out all the free space whatever the factors added up to. A lone
/// <c>flex: 0.5 0 100px</c> item in a 400px row, which browsers grow by half the 300px left over, to
/// 250px, filled the row, and a <c>flex: 0 0.5 300px</c> one in a 200px row, which absorbs half
/// its 100px overflow and is 250px, shrank to 200.
/// </para>
/// <para>
/// Each container here holds empty items, which have no content to hold them open, with the
/// <c>flex</c> a test gives them.
/// </para>
/// </remarks>
public sealed class FlexFractionalFactorTests
{
    private static readonly Uri BaseUrl = new("file:///flex-fractional-factors.html");

    /// <summary>
    /// A line whose items' factors add up to 0.5 flexes by half its free space, and one of 0.75 by
    /// three quarters of it, in the items' proportions:
    /// <list type="bullet">
    /// <item>a <c>flex: 0.5 0 100px</c> item in a 400px row grows by 150px of the 300px, to 250px;</item>
    /// <item>two <c>flex: 0.25 0 100px</c> items in it by 50px each, to 150px;</item>
    /// <item>items growing by 0.5 and 0.25 by 100 and 50px of the 200px, to 200 and 150px;</item>
    /// <item>a <c>flex: 0 0.5 300px</c> item in a 200px row shrinks by 50px of the 100px overflow, to 250px;</item>
    /// <item>two <c>flex: 0 0.25 200px</c> items in a 300px row by 25px each, to 175px.</item>
    /// </list>
    /// They filled the line: 400, 200 each, 233.3 and 166.7, 200, and 150 each.
    /// </summary>
    [Theory]
    [InlineData(400, "0.5 0 100px", null, 250, 0)]
    [InlineData(400, "0.25 0 100px", "0.25 0 100px", 150, 150)]
    [InlineData(400, "0.5 0 100px", "0.25 0 100px", 200, 150)]
    [InlineData(200, "0 0.5 300px", null, 250, 0)]
    [InlineData(300, "0 0.25 200px", "0 0.25 200px", 175, 175)]
    public void Factors_Under_One_Flex_By_Their_Fraction_Of_The_Free_Space_In_A_Row(
        int width, string first, string? second, float firstWidth, float secondWidth)
    {
        var items = Lay("row", width, first, second);

        Assert.Equal(firstWidth, items[0].Size.Width, 1);
        if (second != null)
            Assert.Equal(secondWidth, items[1].Size.Width, 1);
    }

    /// <summary>
    /// The same in a column: a <c>flex: 0.5 0 100px</c> item in a 400px-tall column grows to 250px,
    /// and a <c>flex: 0 0.5 300px</c> one in a 200px-tall column shrinks to 250px. They were 400 and
    /// 200px.
    /// </summary>
    [Theory]
    [InlineData(400, "0.5 0 100px", 250)]
    [InlineData(200, "0 0.5 300px", 250)]
    public void Factors_Under_One_Flex_By_Their_Fraction_Of_The_Free_Space_In_A_Column(
        int height, string flex, float itemHeight)
    {
        var items = Lay("column", height, flex, null);

        Assert.Equal(itemHeight, items[0].Size.Height, 1);
    }

    /// <summary>
    /// The fraction is taken again as the loop freezes items, from the factors still unfrozen. Of
    /// two <c>flex: 0.25 0 100px</c> items in a 400px row, the first with <c>max-width: 120px</c>,
    /// the first stops at 120px, and the second then takes a quarter of the initial 200px, to 150px.
    /// The second took everything the first left, to 280px.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Fraction_Follows_The_Items_Still_Flexing()
    {
        var items = Lay("row", 400, "0.25 0 100px", "0.25 0 100px", first => first.MaxWidth = "120px");

        Assert.Equal(120, items[0].Size.Width, 1);
        Assert.Equal(150, items[1].Size.Width, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: factors adding up to 1 or more hand out all the free
    /// space. A <c>flex: 1 0 100px</c> item fills a 400px row, and two <c>flex: 0.5 0 100px</c>
    /// items are 200px each in it.
    /// </summary>
    [Theory]
    [InlineData("1 0 100px", null, 400, 0)]
    [InlineData("0.5 0 100px", "0.5 0 100px", 200, 200)]
    public void Control_Factors_Of_One_Or_More_Fill_The_Line(
        string first, string? second, float firstWidth, float secondWidth)
    {
        var items = Lay("row", 400, first, second);

        Assert.Equal(firstWidth, items[0].Size.Width, 1);
        if (second != null)
            Assert.Equal(secondWidth, items[1].Size.Width, 1);
    }

    /// <summary>
    /// A flex container of <paramref name="direction"/>, <paramref name="mainSize"/> px along its
    /// main axis, holding an empty item with <c>flex: <paramref name="first"/></c> and, unless it is
    /// <see langword="null"/>, one with <c>flex: <paramref name="second"/></c>.
    /// </summary>
    private static CssBox[] Lay(
        string direction, int mainSize, string first, string? second, Action<CssBox>? styleFirst = null)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var container = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "flex" };
        if (direction == "row")
        {
            container.Width = $"{mainSize}px";
        }
        else
        {
            container.FlexDirection = "column";
            container.Width = "320px";
            container.Height = $"{mainSize}px";
        }

        CssBox Item(string flex)
        {
            var parts = flex.Split(' ');
            return new CssBox(container, new HtmlTag("section", false, null), BaseUrl)
            {
                Display = "block",
                FlexGrow = parts[0],
                FlexShrink = parts[1],
                FlexBasis = parts[2],
            };
        }

        var items = second is null ? new[] { Item(first) } : new[] { Item(first), Item(second) };
        styleFirst?.Invoke(items[0]);

        FlexGridItemBlockification.Generate(root);
        root.PerformLayout(root.LayoutEnvironment);
        return items;
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

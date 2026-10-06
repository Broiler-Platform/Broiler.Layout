using System;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A box sized by its content -- <c>fit-content</c>, or an auto width that shrinks to fit -- takes what
/// its containing block leaves it, less its own margins, border and padding; and a percentage
/// <c>max-width</c> is of the containing block. Chromium, measured: a
/// popover of long text is 1024px wide in a 1024px viewport, wrapping at its edges; a modal dialog of
/// 400 words is held to <c>calc(100% - 6px - 2em)</c> of content, 1024px with its padding and border.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three faults made these boxes wider than their containing block.</b> The fit-content and
/// shrink-to-fit widths took the whole space for the content, so the padding and the border stuck out of
/// it; the second pass over an intrinsic keyword took the box's own width, already including them, for
/// the space, and added them again; and a percentage <c>max-width</c> resolved against the width being
/// clamped rather than the containing block, so <c>width: 500px; max-width: 50%</c> came out 250px.
/// </para>
/// <para>
/// The viewport is 1024×768, a word is 8×16px and a space 4px, and the font is 16px (12pt: Layout
/// keeps font sizes in points), so <c>2em</c> is 32px.
/// </para>
/// </remarks>
public sealed class ContentSizedWidthTests
{
    private static readonly Uri BaseUrl = new("file:///content-sized-width.html");

    /// <summary>A popover of long text wraps at the viewport. It was 1040px wide, and 1052px with a 3px border.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void A_Popover_Of_Long_Text_Wraps_At_The_Viewport(int border)
    {
        var popover = Lay(body => OutOfFlow(body, padding: 4, border: border, words: 400));

        Assert.Equal(1024, popover.Size.Width, 1);
        Assert.Equal(0, popover.Location.X, 1);
    }

    /// <summary>
    /// A modal dialog of long text is held to its <c>max-width</c>, here as Chromium's sheet gives it and as
    /// a percentage; a 2000px one is held to its <c>max-height</c>, and both stay centred.
    /// </summary>
    [Theory]
    [InlineData("calc(100% - 6px - 2em)", 1024f, 0f)]
    [InlineData("90%", 959.6f, 32.2f)]
    public void A_Modal_Dialog_Of_Long_Text_Is_Held_To_Its_Max_Width(string maxWidth, float width, float left)
    {
        var dialog = Lay(body => Dialog(body, maxWidth, box => Words(box, 400)));

        Assert.Equal(width, dialog.Size.Width, 1);
        Assert.Equal(left, dialog.Location.X, 1);
    }

    /// <summary>
    /// A 2000px modal dialog is held to its <c>max-height</c>, at the top, as wide as its content. Passes before
    /// and after: it guards the height and the content width against the width changes.
    /// </summary>
    [Fact]
    public void A_Modal_Dialog_Taller_Than_The_Viewport_Is_Held_To_Its_Max_Height()
    {
        var dialog = Lay(body => Dialog(body, "calc(100% - 6px - 2em)", box =>
            _ = new CssBox(box, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "100px", Height = "2000px" }));

        Assert.Equal(768, dialog.Size.Height, 1);
        Assert.Equal(0, dialog.Location.Y, 1);
        Assert.Equal(138, dialog.Size.Width, 1);
    }

    /// <summary>
    /// A short dialog is its words, its padding and its border wide, centred. Passes before and after: the space
    /// a content-sized box is given changed, not what a box narrower than it takes.
    /// </summary>
    [Fact]
    public void A_Short_Modal_Dialog_Is_As_Wide_As_Its_Words()
    {
        var dialog = Lay(body => Dialog(body, "calc(100% - 6px - 2em)", box => Words(box, 3)));

        Assert.Equal(70, dialog.Size.Width, 1);
        Assert.Equal(477, dialog.Location.X, 1);
    }

    /// <summary>
    /// An auto-width positioned box, and a float, of long text are as wide as the space their containing
    /// block leaves them: the positioned one less its <c>left</c>. They were wider by their padding and
    /// border, and the positioned one by its inset too.
    /// </summary>
    [Theory]
    [InlineData("absolute", 1004f)]
    [InlineData("float", 1024f)]
    public void A_Shrink_To_Fit_Box_Of_Long_Text_Wraps_Inside_Its_Space(string kind, float width)
    {
        var box = Lay(body =>
        {
            var box = new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
            {
                Display = "block",
                PaddingLeft = "10px",
                PaddingRight = "10px",
                BorderLeftStyle = "solid",
                BorderRightStyle = "solid",
                BorderLeftWidth = "3px",
                BorderRightWidth = "3px",
            };
            if (kind == "absolute")
            {
                box.Position = CssConstants.Absolute;
                box.Left = "20px";
                box.Top = "0";
            }
            else
            {
                box.Float = "left";
            }

            Words(box, 400);
            return box;
        });

        Assert.Equal(width, box.Size.Width, 1);
    }

    /// <summary>
    /// An in-flow <c>width: fit-content</c> box of long text is as wide as its containing block, its padding and
    /// border inside it, as the popover above is (CSS Sizing 3: the content is held to the stretch-fit size, the
    /// space less the box's margins, border and padding). It was 1076px: the keyword's second pass took the box's
    /// own width, padding and border included, for the space, and the padding and border went round that again.
    /// </summary>
    [Fact]
    public void An_In_Flow_Fit_Content_Box_Of_Long_Text_Fits_Its_Containing_Block()
    {
        var box = Lay(body =>
        {
            var box = new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
            {
                Display = "block",
                Width = "fit-content",
                PaddingLeft = "10px",
                PaddingRight = "10px",
                BorderLeftStyle = "solid",
                BorderRightStyle = "solid",
                BorderLeftWidth = "3px",
                BorderRightWidth = "3px",
            };
            Words(box, 400);
            return box;
        });

        Assert.Equal(1024, box.Size.Width, 1);
    }

    /// <summary>A percentage <c>max-width</c> is of the containing block, not of the width it clamps.</summary>
    [Theory]
    [InlineData("500px", 500f)]
    [InlineData("800px", 512f)]
    public void A_Percentage_Max_Width_Is_Of_The_Containing_Block(string width, float used)
    {
        var box = Lay(body =>
        {
            var box = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = width, MaxWidth = "50%" };
            Words(box, 1);
            return box;
        });

        Assert.Equal(used, box.Size.Width, 1);
    }

    /// <summary>
    /// A content-sized width is the content's, so the padding and the border go round it whatever
    /// <c>box-sizing</c> says: box-sizing says what a length measures, and a keyword is not one. Passes before
    /// and after: the padding and border now come off the space, and must still go round the content.
    /// </summary>
    [Fact]
    public void A_Keyword_Width_Holds_The_Padding_Under_Border_Box_Sizing()
    {
        var box = Lay(body =>
        {
            var box = new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
            {
                Display = "block",
                Width = "max-content",
                BoxSizing = "border-box",
                PaddingLeft = "10px",
                PaddingRight = "10px",
                BorderLeftStyle = "solid",
                BorderRightStyle = "solid",
                BorderLeftWidth = "3px",
                BorderRightWidth = "3px",
            };
            Words(box, 3);
            return box;
        });

        Assert.Equal(58, box.Size.Width, 1);
    }

    /// <summary>Control, which passes before and after: a pixel <c>max-width</c> held the dialog all along.</summary>
    [Fact]
    public void Control_A_Pixel_Max_Width_Holds_A_Dialog()
    {
        var dialog = Lay(body => Dialog(body, "986px", box => Words(box, 400)));

        Assert.Equal(1024, dialog.Size.Width, 1);
    }

    private static CssBox Dialog(CssBox body, string maxWidth, Action<CssBox> content)
    {
        var dialog = OutOfFlow(body, padding: 16, border: 3, words: 0);
        dialog.MaxWidth = maxWidth;
        dialog.MaxHeight = "calc(100% - 6px - 2em)";
        content(dialog);
        return dialog;
    }

    // A box placed as a popover and a modal dialog are: fixed, all four insets 0, auto margins,
    // fit-content in both axes, scrolling.
    private static CssBox OutOfFlow(CssBox body, int padding, int border, int words)
    {
        var box = new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Position = CssConstants.Fixed,
            Top = "0",
            Right = "0",
            Bottom = "0",
            Left = "0",
            Width = "fit-content",
            Height = "fit-content",
            MarginTop = CssConstants.Auto,
            MarginRight = CssConstants.Auto,
            MarginBottom = CssConstants.Auto,
            MarginLeft = CssConstants.Auto,
            PaddingTop = padding + "px",
            PaddingRight = padding + "px",
            PaddingBottom = padding + "px",
            PaddingLeft = padding + "px",
            Overflow = "auto",
        };
        if (border > 0)
        {
            box.BorderTopStyle = box.BorderRightStyle = box.BorderBottomStyle = box.BorderLeftStyle = "solid";
            box.BorderTopWidth = box.BorderRightWidth = box.BorderBottomWidth = box.BorderLeftWidth = border + "px";
        }

        if (words > 0)
            Words(box, words);
        return box;
    }

    private static void Words(CssBox box, int count)
    {
        var text = new CssBox(box, null, BaseUrl)
        {
            Display = "inline",
            Text = string.Join(" ", Enumerable.Repeat("X", count)).AsMemory(),
        };
        text.ParseToWords();
    }

    private static CssBox Lay(Func<CssBox, CssBox> build)
    {
        var root = new CssBox(null, null, BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var html = new CssBox(root, new HtmlTag("html", false, null), BaseUrl) { Display = "block" };
        var body = new CssBox(html, new HtmlTag("body", false, null), BaseUrl) { Display = "block", MarginTop = "0", MarginBottom = "0" };
        var box = build(body);

        root.PerformLayout(root.LayoutEnvironment);
        return box;
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

    // 12pt is 16px: Layout keeps font sizes in points (CssBoxProperties.GetEmHeight).
    private sealed class FakeFont : Broiler.Graphics.Text.ILayoutFont
    {
        public double Size => 12;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}

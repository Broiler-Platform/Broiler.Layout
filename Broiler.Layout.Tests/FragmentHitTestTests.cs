using System;
using System.Collections.Generic;
using System.Drawing;
using Broiler.Layout.Engine;
using Broiler.Layout.IR;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS 2.1 Appendix E through <see cref="FragmentHitTest"/>: which fragments cover a point, and in
/// what order.
/// </summary>
/// <remarks>
/// <para>
/// Every case below is one where reverse tree order — the walk a consumer writes when it has only
/// rectangles and the DOM — gives a different answer from painting order. They are built as
/// <see cref="CssBox"/> trees run through <see cref="FragmentTreeBuilder"/> rather than as
/// hand-written fragments, so the stacking inputs the query reads are the ones the engine really
/// projects, and a change to that projection shows up here.
/// </para>
/// <para>
/// The boxes are all placed at absolute coordinates directly, because this exercises the query over
/// a laid-out tree rather than the layout that produced it.
/// </para>
/// </remarks>
public sealed class FragmentHitTestTests
{
    private static readonly Uri BaseUrl = new("file:///hit-test.html");

    // ── stacking ──────────────────────────────────────────────────────────

    [Fact(Timeout = 600000)]
    public void A_Child_Is_Above_Its_Parent()
    {
        var root = Root();
        _ = Child(root, new RectangleF(10, 10, 50, 50), tagName: "span");

        Assert.Equal(["span", "div"], TagsOf(HitTest(root, 20, 20)));
    }

    // The case that motivated the issue: a z-index: -1 element paints in step 3 of its stacking
    // context, before the in-flow block backgrounds of step 4 — so it goes behind the background of
    // an ancestor that is not itself a stacking context. A reverse-tree-order walk reports it first,
    // which is the opposite.
    [Fact(Timeout = 600000)]
    public void A_Negative_Stack_Level_Box_Is_Behind_A_Plain_Ancestors_Background()
    {
        var root = Root();
        var section = Child(root, new RectangleF(0, 0, 100, 100), tagName: "section");
        var behind = Child(section, new RectangleF(10, 10, 50, 50), tagName: "span");
        behind.Position = "relative";
        behind.ZIndex = "-1";

        Assert.Equal(["section", "span", "div"], TagsOf(HitTest(root, 20, 20)));
    }

    // The complement, and the reason the rule is stated over stacking contexts rather than over
    // parents: a negative child is lifted only as far as its nearest ancestor stacking context,
    // whose own background is painted first of all and therefore stays below it.
    [Fact(Timeout = 600000)]
    public void A_Negative_Stack_Level_Box_Is_Still_Above_Its_Stacking_Contexts_Background()
    {
        var root = Root();
        var host = Child(root, new RectangleF(0, 0, 100, 100), tagName: "section");
        host.Position = "relative";
        host.ZIndex = "0";

        var behind = Child(host, new RectangleF(10, 10, 50, 50), tagName: "span");
        behind.Position = "relative";
        behind.ZIndex = "-1";

        Assert.Equal(["span", "section", "div"], TagsOf(HitTest(root, 20, 20)));
    }

    // Two positioned siblings where the earlier one has the higher z-index. Tree order says the
    // later sibling wins; every browser says the earlier one does.
    [Fact(Timeout = 600000)]
    public void A_Higher_Stack_Level_Beats_A_Later_Sibling()
    {
        var root = Root();
        var first = Child(root, new RectangleF(0, 0, 100, 100), tagName: "a");
        first.Position = "absolute";
        first.ZIndex = "5";

        var second = Child(root, new RectangleF(0, 0, 100, 100), tagName: "b");
        second.Position = "absolute";
        second.ZIndex = "1";

        Assert.Equal(["a", "b", "div"], TagsOf(HitTest(root, 50, 50)));
    }

    [Fact(Timeout = 600000)]
    public void Equal_Stack_Levels_Fall_Back_To_Tree_Order()
    {
        var root = Root();
        var first = Child(root, new RectangleF(0, 0, 100, 100), tagName: "a");
        first.Position = "absolute";
        first.ZIndex = "2";

        var second = Child(root, new RectangleF(0, 0, 100, 100), tagName: "b");
        second.Position = "absolute";
        second.ZIndex = "2";

        // The later sibling paints over the earlier one only because they tie.
        Assert.Equal(["b", "a", "div"], TagsOf(HitTest(root, 50, 50)));
    }

    // A positioned element with z-index: auto paints above in-flow content, which is what makes an
    // ordinary `position: relative` overlay work at all.
    [Fact(Timeout = 600000)]
    public void A_Positioned_Sibling_Is_Above_An_In_Flow_One_Declared_After_It()
    {
        var root = Root();
        var positioned = Child(root, new RectangleF(0, 0, 100, 100), tagName: "a");
        positioned.Position = "absolute";

        _ = Child(root, new RectangleF(0, 0, 100, 100), tagName: "b");

        Assert.Equal(["a", "b", "div"], TagsOf(HitTest(root, 50, 50)));
    }

    // Appendix E paints every in-flow block background (step 4) under every float (step 5) and
    // every float under every line of inline content (step 7), across the whole stacking context —
    // so a float declared first still covers a block declared after it.
    [Fact(Timeout = 600000)]
    public void A_Float_Is_Above_A_Later_In_Flow_Block()
    {
        var root = Root();
        var floated = Child(root, new RectangleF(0, 0, 100, 100), tagName: "a");
        floated.Float = "left";

        _ = Child(root, new RectangleF(0, 0, 100, 100), tagName: "b");

        Assert.Equal(["a", "b", "div"], TagsOf(HitTest(root, 50, 50)));
    }

    [Fact(Timeout = 600000)]
    public void An_Inline_Is_Above_A_Float()
    {
        var root = Root();
        var inline = Child(root, new RectangleF(0, 0, 100, 100), tagName: "a");
        inline.Display = "inline";

        var floated = Child(root, new RectangleF(0, 0, 100, 100), tagName: "b");
        floated.Float = "left";

        Assert.Equal(["a", "b", "div"], TagsOf(HitTest(root, 50, 50)));
    }

    // A stacking context is atomic: a descendant of it with a huge z-index cannot climb out over a
    // sibling of its host. This is the rule that makes z-index composable at all, and a flat walk
    // over rectangles has nothing to express it with.
    [Fact(Timeout = 600000)]
    public void A_Stacking_Context_Confines_Its_Descendants()
    {
        var root = Root();

        var host = Child(root, new RectangleF(0, 0, 100, 100), tagName: "a");
        host.Position = "absolute";
        host.ZIndex = "1";
        var inner = Child(host, new RectangleF(0, 0, 100, 100), tagName: "i");
        inner.Position = "absolute";
        inner.ZIndex = "9999";

        var over = Child(root, new RectangleF(0, 0, 100, 100), tagName: "b");
        over.Position = "absolute";
        over.ZIndex = "2";

        Assert.Equal(["b", "i", "a", "div"], TagsOf(HitTest(root, 50, 50)));
    }

    // ── the top layer ─────────────────────────────────────────────────────

    // An open dialog paints above every ordinary stacking context, wherever it sits in the tree —
    // here first among the children and under an element with a large z-index.
    [Fact(Timeout = 600000)]
    public void The_Top_Layer_Is_Above_Every_Ordinary_Stacking_Context()
    {
        var root = Root();
        _ = Child(root, new RectangleF(0, 0, 100, 100), tagName: "dialog",
            attributes: new Dictionary<string, string> { ["data-broiler-top-layer"] = "0" });

        var high = Child(root, new RectangleF(0, 0, 100, 100), tagName: "b");
        high.Position = "absolute";
        high.ZIndex = "2147483647";

        Assert.Equal(["dialog", "b", "div"], TagsOf(HitTest(root, 50, 50)));
    }

    [Fact(Timeout = 600000)]
    public void A_Later_Top_Layer_Entry_Covers_An_Earlier_One()
    {
        var root = Root();
        _ = Child(root, new RectangleF(0, 0, 100, 100), tagName: "dialog",
            attributes: new Dictionary<string, string> { ["data-broiler-top-layer"] = "7" });
        _ = Child(root, new RectangleF(0, 0, 100, 100), tagName: "aside",
            attributes: new Dictionary<string, string> { ["data-broiler-top-layer"] = "2" });

        // The order marker decides, not the tree: 7 was added after 2 and covers it.
        Assert.Equal(["dialog", "aside", "div"], TagsOf(HitTest(root, 50, 50)));
    }

    [Fact(Timeout = 600000)]
    public void A_Top_Layer_Subtree_Travels_With_Its_Host()
    {
        var root = Root();
        var dialog = Child(root, new RectangleF(0, 0, 100, 100), tagName: "dialog",
            attributes: new Dictionary<string, string> { ["data-broiler-top-layer"] = "0" });
        _ = Child(dialog, new RectangleF(10, 10, 20, 20), tagName: "p");

        var high = Child(root, new RectangleF(0, 0, 100, 100), tagName: "b");
        high.Position = "absolute";
        high.ZIndex = "99";

        // The dialog's own content is lifted with it rather than left behind at its tree position.
        Assert.Equal(["p", "dialog", "b", "div"], TagsOf(HitTest(root, 15, 15)));
    }

    // ── what covers the point ─────────────────────────────────────────────

    [Fact(Timeout = 600000)]
    public void A_Box_That_Does_Not_Cover_The_Point_Is_Not_A_Hit()
    {
        var root = Root();
        _ = Child(root, new RectangleF(10, 10, 20, 20), tagName: "span");

        Assert.Equal(["div"], TagsOf(HitTest(root, 80, 80)));
    }

    [Fact(Timeout = 600000)]
    public void A_Point_Outside_Everything_Hits_Nothing()
    {
        var root = Root();

        Assert.Empty(HitTest(root, 5000, 5000));
        Assert.Null(FragmentHitTest.TopmostAt(FragmentTreeBuilder.Build(root), new PointF(5000, 5000)));
    }

    [Fact(Timeout = 600000)]
    public void The_Topmost_Hit_Is_The_First_Of_The_List()
    {
        var root = Root();
        _ = Child(root, new RectangleF(0, 0, 50, 50), tagName: "span");

        var tree = FragmentTreeBuilder.Build(root);
        var point = new PointF(10, 10);

        Assert.Same(
            FragmentHitTest.FragmentsAt(tree, point)[0],
            FragmentHitTest.TopmostAt(tree, point));
    }

    // visibility: hidden removes the box as a target without removing its visible descendants —
    // they are separate fragments and are tested on their own.
    [Fact(Timeout = 600000)]
    public void A_Hidden_Box_Is_Not_A_Target_But_Its_Visible_Child_Is()
    {
        var root = Root();
        var hidden = Child(root, new RectangleF(0, 0, 100, 100), tagName: "a");
        hidden.Visibility = "hidden";
        var shown = Child(hidden, new RectangleF(0, 0, 50, 50), tagName: "b");
        shown.Visibility = "visible";

        Assert.Equal(["b", "div"], TagsOf(HitTest(root, 10, 10)));
    }

    [Fact(Timeout = 600000)]
    public void A_Display_None_Subtree_Is_Not_Reached()
    {
        var root = Root();
        var gone = Child(root, new RectangleF(0, 0, 100, 100), tagName: "a");
        gone.Display = "none";
        _ = Child(gone, new RectangleF(0, 0, 50, 50), tagName: "b");

        Assert.Equal(["div"], TagsOf(HitTest(root, 10, 10)));
    }

    // ── transforms ────────────────────────────────────────────────────────

    // A rotated box does not cover the corners of the rectangle that encloses it. Testing that
    // rectangle instead — which is all a caller composing getBoundingClientRect can do — hits the
    // element in four places it is not.
    [Fact(Timeout = 600000)]
    public void A_Rotated_Box_Is_Not_Hit_In_The_Corners_It_Does_Not_Cover()
    {
        var root = Root();
        var turned = Child(root, new RectangleF(20, 20, 60, 60), tagName: "span");
        turned.Transform = "rotate(45deg)";

        // The centre is covered whatever the rotation.
        Assert.Contains("span", TagsOf(HitTest(root, 50, 50)));
        // A corner of the enclosing rectangle is outside the turned square.
        Assert.DoesNotContain("span", TagsOf(HitTest(root, 22, 22)));
        // And a point on the rotated diamond's own tip is inside it, though outside the
        // untransformed box.
        Assert.Contains("span", TagsOf(HitTest(root, 50, 8)));
    }

    [Fact(Timeout = 600000)]
    public void A_Translated_Box_Is_Hit_Where_It_Paints()
    {
        var root = Root();
        var moved = Child(root, new RectangleF(0, 0, 50, 50), tagName: "span");
        moved.Transform = "translate(100px, 0)";

        Assert.DoesNotContain("span", TagsOf(HitTest(root, 25, 25)));
        Assert.Contains("span", TagsOf(HitTest(root, 125, 25)));
    }

    // The chain composes: a child of a transformed ancestor moves with it, and a caller that
    // applied only the element's own transform would look in the wrong place.
    [Fact(Timeout = 600000)]
    public void An_Ancestor_Transform_Carries_Its_Subtree()
    {
        var root = Root();
        var moved = Child(root, new RectangleF(0, 0, 100, 100), tagName: "a");
        moved.Transform = "translate(100px, 0)";
        _ = Child(moved, new RectangleF(10, 10, 20, 20), tagName: "b");

        Assert.DoesNotContain("b", TagsOf(HitTest(root, 15, 15)));
        Assert.Equal(["b", "a", "div"], TagsOf(HitTest(root, 115, 15)));
    }

    // transform-origin decides where a scale lands, so the query has to read it rather than assume
    // the box centre — the assumption that had the paint and the measured geometry disagreeing.
    [Fact(Timeout = 600000)]
    public void The_Transform_Origin_Decides_What_A_Scale_Covers()
    {
        var root = Root();
        var scaled = Child(root, new RectangleF(0, 0, 40, 40), tagName: "span");
        scaled.Transform = "scale(0.5)";
        scaled.TransformOrigin = "0 0";

        // Halved about its own corner, the box covers (0,0)-(20,20) and nothing beyond.
        Assert.Contains("span", TagsOf(HitTest(root, 15, 15)));
        Assert.DoesNotContain("span", TagsOf(HitTest(root, 25, 25)));
    }

    // A transform that collapses the box to a line leaves nothing to hit; inverting it is not
    // possible and the honest answer is that it covers no area.
    [Fact(Timeout = 600000)]
    public void A_Collapsed_Box_Covers_Nothing()
    {
        var root = Root();
        var flat = Child(root, new RectangleF(0, 0, 100, 100), tagName: "span");
        flat.Transform = "scale(0)";

        Assert.Equal(["div"], TagsOf(HitTest(root, 50, 50)));
    }

    // ── the shape ─────────────────────────────────────────────────────────

    private static IReadOnlyList<Fragment> HitTest(CssBox root, float x, float y) =>
        FragmentHitTest.FragmentsAt(FragmentTreeBuilder.Build(root), new PointF(x, y));

    private static string?[] TagsOf(IReadOnlyList<Fragment> hits)
    {
        var tags = new string?[hits.Count];
        for (var i = 0; i < hits.Count; i++)
            tags[i] = hits[i].Style.TagName;

        return tags;
    }

    private static CssBox Root()
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Location = PointF.Empty,
            Size = new SizeF(200, 200),
            Display = "block",
            FontSize = "16px",
        };
        root.LayoutEnvironment = new FakeLayoutEnvironment();
        return root;
    }

    private static CssBox Child(
        CssBox parent, RectangleF bounds, string tagName,
        IReadOnlyDictionary<string, string>? attributes = null) =>
        new(parent, new HtmlTag(tagName, false, attributes), BaseUrl)
        {
            Location = bounds.Location,
            Size = bounds.Size,
            Display = "block",
            FontSize = "16px",
        };

    // Minimal ILayoutEnvironment: these synthetic boxes carry no text or replaced content, so only
    // the font lookup ComputedStyleBuilder needs is exercised.
    private sealed class FakeLayoutEnvironment : ILayoutEnvironment
    {
        private static readonly Broiler.Graphics.Text.ILayoutFont TheFont = new FakeFont();

        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => TheFont;
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => SizeF.Empty;
        public void MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = 0; charFitWidth = 0; }
        public double GetWhitespaceWidth(Broiler.Graphics.Text.ILayoutFont font) => 0;
        public ImageIntrinsics GetImageIntrinsics(object imageHandle) => default;
        public Broiler.Graphics.Color.BColor ParseColor(string value) => default;
        public void RequestRefresh(bool relayout) { }
        public SizeF ViewportSize => new(1000, 1000);
        public PointF RootLocation => PointF.Empty;
        public SizeF ActualSize { get; set; }
        public bool AvoidGeometryAntialias => false;
        public SizeF PageSize => new(1000, 1000);
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

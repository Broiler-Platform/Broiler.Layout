using System.Drawing;
using Broiler.Layout.IR;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS Transforms 1 §3: the used value of a <c>transform</c> function list, which is a matrix and not
/// a parse — <c>translate(50%)</c> and <c>transform-origin</c> both resolve against the element's own
/// border box, so the answer depends on layout having run.
/// </summary>
/// <remarks>
/// <para>
/// The box below is 200×100 and offset from the origin, so a percentage that resolves against the
/// wrong axis, or a case that quietly folds the box's position into the matrix, cannot pass. The
/// matrix itself is origin-independent: only <see cref="CssTransform.AboutOrigin"/> puts it
/// somewhere.
/// </para>
/// <para>
/// The unit cases are the ones a parser written for the SVG <c>transform</c> attribute gets wrong —
/// it takes bare numbers, so it reads <c>translate(10px)</c> as ten user units by luck and
/// <c>rotate(0.5turn)</c> as half a degree by accident — and the CSS-only function names
/// (<c>translateX</c>, <c>scaleY</c>, <c>rotateZ</c>) are ones it does not recognise at all.
/// </para>
/// </remarks>
public sealed class CssTransformTests
{
    private static readonly RectangleF Box = new(30, 40, 200, 100);

    private static CssTransform Resolve(string value) => CssTransform.Resolve(value, Box);

    private static void AssertMatrix(
        float a, float b, float c, float d, float e, float f, CssTransform actual)
    {
        Assert.Equal(a, actual.A, 4);
        Assert.Equal(b, actual.B, 4);
        Assert.Equal(c, actual.C, 4);
        Assert.Equal(d, actual.D, 4);
        Assert.Equal(e, actual.E, 4);
        Assert.Equal(f, actual.F, 4);
    }

    // ── no transform ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("none")]
    [InlineData("NONE")]
    public void An_Absent_Or_None_Value_Resolves_To_Nothing(string? value)
    {
        Assert.False(CssTransform.TryResolve(value, Box, out var transform));
        Assert.True(transform.IsIdentity);
    }

    // A list that resolves to the identity is not the same as no list: rotate(0deg) is a valid
    // declaration and makes the element a containing block for fixed descendants, which `none` does
    // not. The matrix cannot tell the two apart, so the flag has to.
    [Fact(Timeout = 600000)]
    public void A_List_That_Resolves_To_The_Identity_Still_Resolved()
    {
        Assert.True(CssTransform.TryResolve("rotate(0deg) scale(1) translate(0, 0)", Box, out var transform));
        Assert.True(transform.IsIdentity);
    }

    // ── matrix ────────────────────────────────────────────────────────────

    [Fact(Timeout = 600000)]
    public void Matrix_Is_Taken_As_Written()
    {
        AssertMatrix(2, 0.5f, -0.25f, 3, 10, -20, Resolve("matrix(2, 0.5, -0.25, 3, 10, -20)"));
    }

    [Theory]
    // Five or seven arguments is not a matrix() at all, and §3 drops the whole declaration.
    [InlineData("matrix(1, 0, 0, 1, 0)")]
    [InlineData("matrix(1, 0, 0, 1, 0, 0, 0)")]
    [InlineData("matrix()")]
    [InlineData("matrix(1, 0, 0, 1, 0, x)")]
    public void A_Malformed_Matrix_Is_Invalid(string value) =>
        Assert.False(CssTransform.TryResolve(value, Box, out _));

    // ── translate ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("translate(10px)", 10f, 0f)]
    [InlineData("translate(10px, 25px)", 10f, 25f)]
    [InlineData("translate(0, 0)", 0f, 0f)]
    [InlineData("translateX(10px)", 10f, 0f)]
    [InlineData("translateY(25px)", 0f, 25f)]
    // The percentage resolves against the element's own border box, per axis: 50% of 200 wide and
    // 25% of 100 tall. A resolver that took one basis for both axes reads the second as 50.
    [InlineData("translate(50%, 25%)", 100f, 25f)]
    [InlineData("translate(50%)", 100f, 0f)]
    [InlineData("translateX(50%)", 100f, 0f)]
    [InlineData("translateY(25%)", 0f, 25f)]
    // Mixed forms, and a negative.
    [InlineData("translate(-10px, 50%)", -10f, 50f)]
    public void Translate_Resolves_Lengths_And_Percentages(string value, float e, float f) =>
        AssertMatrix(1, 0, 0, 1, e, f, Resolve(value));

    // The absolute length units are all resolvable without a font or a viewport, so they are.
    [Theory]
    [InlineData("translateX(1in)", 96f)]
    [InlineData("translateX(72pt)", 96f)]
    [InlineData("translateX(6pc)", 96f)]
    [InlineData("translateX(2.54cm)", 96f)]
    [InlineData("translateX(25.4mm)", 96f)]
    public void An_Absolute_Unit_Resolves_To_Pixels(string value, float e) =>
        AssertMatrix(1, 0, 0, 1, e, 0, Resolve(value));

    // A font- or viewport-relative unit needs context this resolver is not given. Refusing is the
    // point: reading "2em" as two pixels would put the element two pixels from where it paints and
    // the caller would have no way to know.
    [Theory]
    [InlineData("translateX(2em)")]
    [InlineData("translateX(2rem)")]
    [InlineData("translateX(2ex)")]
    [InlineData("translateX(2ch)")]
    [InlineData("translateX(10vw)")]
    [InlineData("translateX(10vh)")]
    public void A_Relative_Unit_Cannot_Be_Resolved(string value) =>
        Assert.False(CssTransform.TryResolve(value, Box, out _));

    // A <length> needs a unit — except zero, which the grammar takes bare.
    [Theory]
    [InlineData("translateX(10)")]
    [InlineData("translate(10, 20)")]
    [InlineData("translateX()")]
    [InlineData("translate(1px, 2px, 3px)")]
    public void A_Unitless_Non_Zero_Length_Is_Invalid(string value) =>
        Assert.False(CssTransform.TryResolve(value, Box, out _));

    // ── scale ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("scale(2)", 2f, 2f)]
    [InlineData("scale(2, 3)", 2f, 3f)]
    [InlineData("scale(-1, 1)", -1f, 1f)]
    [InlineData("scaleX(2)", 2f, 1f)]
    [InlineData("scaleY(3)", 1f, 3f)]
    // css-transforms-2: a scale factor may be a percentage, and the percentage is the ratio itself —
    // scale(50%) is scale(0.5), not a percentage of anything. Parsing it as a plain number instead
    // fails and collapses the box to nothing.
    [InlineData("scale(50%)", 0.5f, 0.5f)]
    [InlineData("scale(50%, 200%)", 0.5f, 2f)]
    [InlineData("scaleX(150%)", 1.5f, 1f)]
    public void Scale_Takes_Numbers_And_Percentages(string value, float a, float d) =>
        AssertMatrix(a, 0, 0, d, 0, 0, Resolve(value));

    // ── rotate ────────────────────────────────────────────────────────────

    // A quarter turn, spelled every way CSS allows. A parser that reads bare numbers as degrees gets
    // 0.25turn as a quarter of a degree and 1.5708rad as a degree and a half.
    [Theory]
    [InlineData("rotate(90deg)")]
    [InlineData("rotate(100grad)")]
    [InlineData("rotate(0.25turn)")]
    [InlineData("rotate(1.5707963rad)")]
    [InlineData("rotateZ(90deg)")]
    public void Rotate_Reads_Every_Angle_Unit(string value)
    {
        // Clockwise in CSS's y-down space: (1, 0) goes to (0, 1).
        AssertMatrix(0, 1, -1, 0, 0, 0, Resolve(value));
    }

    // A bare number is not an <angle> in CSS — only a bare zero is — so rotate(45) is invalid and
    // takes the declaration with it, exactly as it does in a browser. This is the line between this
    // grammar and the SVG transform attribute's, where a bare number means degrees.
    [Fact(Timeout = 600000)]
    public void A_Bare_Angle_Is_Invalid_Except_Zero()
    {
        Assert.False(CssTransform.TryResolve("rotate(45)", Box, out _));
        Assert.True(CssTransform.TryResolve("rotate(0)", Box, out var zero));
        Assert.True(zero.IsIdentity);
    }

    // ── skew ──────────────────────────────────────────────────────────────

    [Fact(Timeout = 600000)]
    public void SkewX_Shears_Along_The_Inline_Axis() =>
        AssertMatrix(1, 0, 1, 1, 0, 0, Resolve("skewX(45deg)"));

    [Fact(Timeout = 600000)]
    public void SkewY_Shears_Along_The_Block_Axis() =>
        AssertMatrix(1, 1, 0, 1, 0, 0, Resolve("skewY(45deg)"));

    // skew() is in the 2D function set too, and a resolver that omits it reports a perfectly valid
    // declaration as unresolvable.
    [Fact(Timeout = 600000)]
    public void Skew_Takes_Both_Angles_And_Defaults_The_Second_To_Zero()
    {
        AssertMatrix(1, 1, 1, 1, 0, 0, Resolve("skew(45deg, 45deg)"));
        AssertMatrix(1, 0, 1, 1, 0, 0, Resolve("skew(45deg)"));
    }

    // ── composition ───────────────────────────────────────────────────────

    // §3: the functions apply left to right as outermost to innermost, so the list is the product
    // M1·M2·…·Mn and the rightmost function reaches a point first. Folding the other way is the
    // classic error, and it is invisible on a one-function list: here it would translate by 10
    // rather than by 20.
    [Fact(Timeout = 600000)]
    public void Functions_Compose_Left_To_Right_As_Outermost_To_Innermost()
    {
        var transform = Resolve("scale(2) translate(10px, 5px)");

        AssertMatrix(2, 0, 0, 2, 20, 10, transform);
        Assert.Equal(new PointF(20, 10), transform.Map(0, 0));
    }

    [Fact(Timeout = 600000)]
    public void The_Reverse_Order_Is_A_Different_Matrix() =>
        AssertMatrix(2, 0, 0, 2, 10, 5, Resolve("translate(10px, 5px) scale(2)"));

    [Fact(Timeout = 600000)]
    public void Whitespace_Between_Functions_Is_Free() =>
        AssertMatrix(2, 0, 0, 2, 20, 10, Resolve("  scale(2)\n\ttranslate(10px,5px)  "));

    // Functions are separated by whitespace only. A comma between them is a different production,
    // and text that is not a function at all makes the declaration invalid rather than being
    // skipped over — skipping is how a caller ends up applying half of a list.
    [Theory]
    [InlineData("scale(2), translate(10px)")]
    [InlineData("scale(2) garbage translate(10px)")]
    [InlineData("scale(2) trailing")]
    [InlineData("scale(2")]
    [InlineData("scale 2")]
    public void A_List_That_Is_Not_A_Function_List_Is_Invalid(string value) =>
        Assert.False(CssTransform.TryResolve(value, Box, out _));

    // ── the 3D set ────────────────────────────────────────────────────────

    // These are valid CSS that this cannot express. Reporting them as unresolved is the honest
    // answer: the alternative on offer is to drop the z term, which reads translate3d(0,0,100px) as
    // the identity and rotateX(90deg) as no flattening — a wrong matrix the caller cannot detect.
    [Theory]
    [InlineData("translate3d(10px, 20px, 30px)")]
    [InlineData("translateZ(30px)")]
    [InlineData("scale3d(2, 2, 2)")]
    [InlineData("rotateX(45deg)")]
    [InlineData("rotateY(45deg)")]
    [InlineData("rotate3d(1, 1, 0, 45deg)")]
    [InlineData("matrix3d(1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1)")]
    [InlineData("perspective(500px)")]
    // And one valid 2D function alongside an unrepresentable one: the declaration goes as a whole,
    // it is not reduced to the part that could be read.
    [InlineData("scale(2) translateZ(30px)")]
    public void A_Function_Outside_The_Two_Dimensional_Set_Is_Not_Resolved(string value)
    {
        Assert.False(CssTransform.TryResolve(value, Box, out var transform));
        Assert.True(transform.IsIdentity);
    }

    // A calc() argument carries its own parentheses. Stopping at the first ')' would cut the list in
    // half and read "translate(calc(1px + 2px" as a function with one unreadable argument, and then
    // carry on with the ", 0)" as though it were a second function — so the guard matters even
    // though calc itself is not resolved here.
    [Fact(Timeout = 600000)]
    public void A_Nested_Function_Argument_Does_Not_End_The_Function_Early()
    {
        Assert.False(CssTransform.TryResolve("translate(calc(1px + 2px), 0)", Box, out _));
        Assert.False(CssTransform.TryResolve("translate(calc(1px + 2px), 0) scale(2)", Box, out _));
    }

    // ── the origin ────────────────────────────────────────────────────────

    // A matrix on its own is applied about the coordinate origin; CSS applies it about
    // transform-origin. For a 200×100 box at (30, 40) the default origin is its centre (130, 90), and
    // a scale about that centre keeps the centre fixed — which is what makes the element grow
    // outwards rather than away from the page corner.
    [Fact(Timeout = 600000)]
    public void A_Transform_Is_Applied_About_Its_Origin()
    {
        var origin = CssTransformOrigin.Resolve(null, Box, initialIsBoxCorner: false);
        Assert.Equal(new PointF(130, 90), origin);

        var scaled = Resolve("scale(2)").AboutOrigin(origin);

        Assert.Equal(origin, scaled.Map(origin.X, origin.Y));
        Assert.Equal(new RectangleF(-70, -10, 400, 200), scaled.MapBounds(Box));
    }

    // transform-origin: 0 0 anchors the box's own corner instead, which is the case that showed the
    // paint walker and the script bridge disagreeing: the same scale(0.5) reported a rect at the
    // corner and painted it inset by a quarter of the box.
    [Fact(Timeout = 600000)]
    public void The_Origin_Decides_Where_The_Scaled_Box_Lands()
    {
        var corner = CssTransformOrigin.Resolve("0 0", Box, initialIsBoxCorner: false);
        Assert.Equal(new PointF(30, 40), corner);

        var scaled = Resolve("scale(0.5)").AboutOrigin(corner);

        Assert.Equal(new RectangleF(30, 40, 100, 50), scaled.MapBounds(Box));
    }

    // getBoundingClientRect reports the axis-aligned bounds of the transformed box, which for a
    // rotation is larger than the box: a 200×100 box turned 45° about its centre spans 300/√2 on
    // both axes.
    [Fact(Timeout = 600000)]
    public void A_Rotated_Box_Bounds_Larger_Than_Itself()
    {
        var origin = CssTransformOrigin.Resolve(null, Box, initialIsBoxCorner: false);
        var bounds = Resolve("rotate(45deg)").AboutOrigin(origin).MapBounds(Box);

        var span = 300f / MathF.Sqrt(2f);
        Assert.Equal(span, bounds.Width, 3);
        Assert.Equal(span, bounds.Height, 3);
        // Still centred on the origin it turned about.
        Assert.Equal(origin.X, bounds.X + bounds.Width / 2f, 3);
        Assert.Equal(origin.Y, bounds.Y + bounds.Height / 2f, 3);
    }

    // The ancestor chain: an element's own transform is applied inside every transform above it, so
    // the composition runs outermost first. Composing the other way puts a nested element at the
    // ancestor's offset scaled instead of at its own.
    [Fact(Timeout = 600000)]
    public void An_Ancestor_Chain_Composes_Outermost_First()
    {
        var ancestorBox = new RectangleF(0, 0, 100, 100);
        var childBox = new RectangleF(10, 10, 20, 20);

        var ancestor = CssTransform.Resolve("scale(2)", ancestorBox)
            .AboutOrigin(CssTransformOrigin.Resolve("0 0", ancestorBox, initialIsBoxCorner: false));
        var child = CssTransform.Resolve("translate(5px, 0)", childBox)
            .AboutOrigin(CssTransformOrigin.Resolve("0 0", childBox, initialIsBoxCorner: false));

        // The child moves 5px in its own space, which the ancestor's scale doubles to 10px.
        Assert.Equal(new RectangleF(30, 20, 40, 40), ancestor.Concat(child).MapBounds(childBox));
    }

    // The matrix is independent of where the box sits: only AboutOrigin places it. A resolver that
    // folded the box's position in would report the same declaration differently for two identically
    // sized elements.
    [Fact(Timeout = 600000)]
    public void The_Matrix_Does_Not_Depend_On_The_Box_Position() =>
        Assert.Equal(
            CssTransform.Resolve("translate(50%, 25%) rotate(30deg)", new RectangleF(0, 0, 200, 100)),
            CssTransform.Resolve("translate(50%, 25%) rotate(30deg)", new RectangleF(999, -7, 200, 100)));

    [Fact(Timeout = 600000)]
    public void The_Identity_Maps_A_Rectangle_To_Itself() =>
        Assert.Equal(Box, CssTransform.Identity.MapBounds(Box));
}

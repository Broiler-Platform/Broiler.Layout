using System;
using System.Drawing;
using System.Globalization;

namespace Broiler.Layout.IR;

/// <summary>
/// CSS Transforms 1 §3: the <em>used</em> value of a <c>transform</c> function list, as the 2D
/// affine matrix <c>matrix(a, b, c, d, e, f)</c> mapping a point
/// <c>(x, y) → (a·x + c·y + e, b·x + d·y + f)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Resolving the list is not a parse, which is why the computed string a read model can carry is not
/// enough on its own: the percentages in <c>translate()</c> resolve against the element's own border
/// box, <c>transform-origin</c> resolves against it too (<see cref="CssTransformOrigin"/>), and that
/// box is a layout output. A consumer holding only the string can get as far as the function list and
/// no further.
/// </para>
/// <para>
/// <b>Not <see cref="SvgTransform"/>.</b> That type parses the SVG <c>transform</c> <em>attribute</em>
/// (SVG 1.1 §7.6), whose grammar is a different one: its numbers are bare user units, so
/// <c>translate(10px)</c> would fold as <c>translate(10)</c> and <c>rotate(0.5turn)</c> as half a
/// degree, and its function set has no <c>translateX</c>, <c>scaleY</c> or <c>rotateZ</c> in it. Both
/// are right for the grammar they were written for and neither can stand in for the other.
/// </para>
/// <para>
/// <b>What cannot be resolved says so.</b> This models the 2D function set —
/// <c>matrix</c>, <c>translate</c>/<c>X</c>/<c>Y</c>, <c>scale</c>/<c>X</c>/<c>Y</c>,
/// <c>rotate</c>/<c>rotateZ</c>, <c>skew</c>/<c>skewX</c>/<c>skewY</c> — and nothing else.
/// A list carrying a 3D function (<c>translate3d</c>, <c>rotateX</c>, <c>matrix3d</c>,
/// <c>perspective</c> and the rest) is reported as unresolved rather than approximated, because the
/// approximation on offer is to drop the term, which answers <c>translate3d(0, 0, 100px)</c> as the
/// identity and <c>rotateX(90deg)</c> as no flattening at all — a wrong matrix that the caller cannot
/// tell from a right one.
/// </para>
/// </remarks>
public readonly record struct CssTransform(float A, float B, float C, float D, float E, float F)
{
    /// <summary>The transform that moves nothing.</summary>
    public static CssTransform Identity { get; } = new(1, 0, 0, 1, 0, 0);

    /// <summary>Whether this is <see cref="Identity"/>.</summary>
    public bool IsIdentity => this == Identity;

    /// <summary>
    /// Resolves a <c>transform</c> function list against <paramref name="borderBox"/>, the box its
    /// percentages and its origin resolve against; an absent, <c>none</c>, invalid or
    /// non-2D list is <see cref="Identity"/>.
    /// </summary>
    public static CssTransform Resolve(string? transformList, RectangleF borderBox)
    {
        TryResolve(transformList, borderBox, out var transform);
        return transform;
    }

    /// <summary>
    /// Resolves a <c>transform</c> function list, reporting separately whether it yielded a used
    /// matrix at all.
    /// </summary>
    /// <param name="transformList">
    /// The computed <c>transform</c> value — a function list, or <c>none</c>.
    /// </param>
    /// <param name="borderBox">
    /// The element's border box, which the percentage forms of <c>translate()</c> resolve against.
    /// Only its size is read; the box's position does not enter the matrix, because a transform is
    /// applied about its origin and the caller supplies that separately
    /// (<see cref="AboutOrigin"/>).
    /// </param>
    /// <param name="transform">The used matrix, or <see cref="Identity"/> when this returns false.</param>
    /// <returns>
    /// <see langword="false"/> when no used matrix is available — the value is absent, <c>none</c>,
    /// invalid, or uses functions outside the 2D set. The geometry to report in every one of those
    /// cases is the untransformed box, which <see cref="Identity"/> gives; the flag is there for a
    /// caller that wants to tell "no transform" from "a transform that resolves to the identity",
    /// since <c>rotate(0deg)</c> and <c>scale(1)</c> are both genuinely <see cref="Identity"/>.
    /// </returns>
    public static bool TryResolve(string? transformList, RectangleF borderBox, out CssTransform transform)
    {
        transform = Identity;

        if (string.IsNullOrWhiteSpace(transformList)
            || transformList.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var result = Identity;
        var any = false;
        var position = 0;

        while (position < transformList.Length)
        {
            var open = transformList.IndexOf('(', position);
            if (open < 0)
            {
                // Trailing text that is not a function at all. Whitespace after the last function is
                // the normal end of a well-formed list; anything else makes the declaration invalid.
                if (!IsBlank(transformList, position, transformList.Length))
                    return false;

                break;
            }

            var close = FindMatchingParenthesis(transformList, open);
            if (close < 0)
                return false;

            var name = transformList[position..open].Trim();
            if (!TryResolveFunction(name, transformList[(open + 1)..close], borderBox, out var step))
                return false;

            result = result.Concat(step);
            any = true;
            position = close + 1;
        }

        if (!any)
            return false;

        transform = result;
        return true;
    }

    /// <summary>
    /// This transform applied about <paramref name="origin"/> rather than about the coordinate
    /// origin — the form CSS Transforms 1 §8 actually paints, and the one a caller composing an
    /// ancestor chain needs, since each element rotates and scales about its own
    /// <c>transform-origin</c> in the shared coordinate space.
    /// </summary>
    public CssTransform AboutOrigin(PointF origin) => new(
        A, B, C, D,
        E + origin.X - (A * origin.X + C * origin.Y),
        F + origin.Y - (B * origin.X + D * origin.Y));

    /// <summary>This transform followed by <paramref name="inner"/> applied first (<c>this · inner</c>).</summary>
    /// <remarks>
    /// The composition an ancestor chain wants: the outer element's transform is <c>this</c> and the
    /// descendant's is <paramref name="inner"/>, because a point is carried out through the
    /// descendant's transform before the ancestor's.
    /// </remarks>
    public CssTransform Concat(CssTransform inner) => new(
        A * inner.A + C * inner.B,
        B * inner.A + D * inner.B,
        A * inner.C + C * inner.D,
        B * inner.C + D * inner.D,
        A * inner.E + C * inner.F + E,
        B * inner.E + D * inner.F + F);

    /// <summary>Where this transform sends the point (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public PointF Map(float x, float y) => new(A * x + C * y + E, B * x + D * y + F);

    /// <summary>
    /// The transform that undoes this one — what carries a point on the page back into the
    /// element's own untransformed coordinates, which is how a hit test asks whether a rotated box
    /// really covers a point instead of testing its enclosing rectangle.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when the matrix is singular, which is a transform that has collapsed
    /// the element to a line or a point: there is no inverse because nothing maps back, and the
    /// element covers no area to be hit.
    /// </returns>
    public bool TryInvert(out CssTransform inverse)
    {
        inverse = Identity;

        var determinant = A * D - B * C;
        if (!float.IsFinite(determinant) || MathF.Abs(determinant) < 1e-9f)
            return false;

        float ia = D / determinant, ib = -B / determinant;
        float ic = -C / determinant, id = A / determinant;
        inverse = new CssTransform(ia, ib, ic, id, -(ia * E + ic * F), -(ib * E + id * F));
        return true;
    }

    /// <summary>
    /// The axis-aligned bounding box of a mapped rectangle — what <c>getBoundingClientRect</c>
    /// reports, which is why a rotated box's rect is larger than the box.
    /// </summary>
    public RectangleF MapBounds(RectangleF rect)
    {
        if (IsIdentity)
            return rect;

        PointF p0 = Map(rect.Left, rect.Top), p1 = Map(rect.Right, rect.Top);
        PointF p2 = Map(rect.Right, rect.Bottom), p3 = Map(rect.Left, rect.Bottom);
        float left = Math.Min(Math.Min(p0.X, p1.X), Math.Min(p2.X, p3.X));
        float right = Math.Max(Math.Max(p0.X, p1.X), Math.Max(p2.X, p3.X));
        float top = Math.Min(Math.Min(p0.Y, p1.Y), Math.Min(p2.Y, p3.Y));
        float bottom = Math.Max(Math.Max(p0.Y, p1.Y), Math.Max(p2.Y, p3.Y));
        return new RectangleF(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// One <c>name(args)</c> function as a matrix, or false when the name is not in the 2D set or
    /// its arguments do not fit it — either of which makes the whole declaration invalid (§3).
    /// </summary>
    private static bool TryResolveFunction(
        string name, string arguments, RectangleF borderBox, out CssTransform step)
    {
        step = Identity;

        var values = arguments.Split(',');
        for (var i = 0; i < values.Length; i++)
            values[i] = values[i].Trim();

        // A lone empty argument is an empty list, not one argument: "translate()" has no arguments.
        var count = values.Length == 1 && values[0].Length == 0 ? 0 : values.Length;

        switch (name.ToLowerInvariant())
        {
            case "matrix":
                if (count != 6)
                    return false;
                Span<float> m = stackalloc float[6];
                for (var i = 0; i < 6; i++)
                {
                    if (!TryParseNumber(values[i], out var element))
                        return false;

                    m[i] = element;
                }

                // e and f are <number>s that mean pixels, which is the one place the grammar takes a
                // length without a unit.
                step = new CssTransform(m[0], m[1], m[2], m[3], m[4], m[5]);
                return true;

            case "translate":
                if (count is not (1 or 2)
                    || !TryResolveLength(values[0], borderBox.Width, out var tx))
                {
                    return false;
                }

                var ty = 0f;
                if (count == 2 && !TryResolveLength(values[1], borderBox.Height, out ty))
                    return false;

                step = new CssTransform(1, 0, 0, 1, tx, ty);
                return true;

            case "translatex":
                if (count != 1 || !TryResolveLength(values[0], borderBox.Width, out var tOnlyX))
                    return false;
                step = new CssTransform(1, 0, 0, 1, tOnlyX, 0);
                return true;

            case "translatey":
                if (count != 1 || !TryResolveLength(values[0], borderBox.Height, out var tOnlyY))
                    return false;
                step = new CssTransform(1, 0, 0, 1, 0, tOnlyY);
                return true;

            case "scale":
                if (count is not (1 or 2) || !TryParseScaleFactor(values[0], out var sx))
                    return false;

                var sy = sx;
                if (count == 2 && !TryParseScaleFactor(values[1], out sy))
                    return false;

                step = new CssTransform(sx, 0, 0, sy, 0, 0);
                return true;

            case "scalex":
                if (count != 1 || !TryParseScaleFactor(values[0], out var sOnlyX))
                    return false;
                step = new CssTransform(sOnlyX, 0, 0, 1, 0, 0);
                return true;

            case "scaley":
                if (count != 1 || !TryParseScaleFactor(values[0], out var sOnlyY))
                    return false;
                step = new CssTransform(1, 0, 0, sOnlyY, 0, 0);
                return true;

            // rotateZ is the 3D spelling of the one rotation that stays in the plane, so it is the
            // only member of that family with an exact 2D matrix.
            case "rotate":
            case "rotatez":
                if (count != 1 || !TryParseAngle(values[0], out var radians))
                    return false;

                float cos = MathF.Cos(radians), sin = MathF.Sin(radians);
                step = new CssTransform(cos, sin, -sin, cos, 0, 0);
                return true;

            case "skew":
                if (count is not (1 or 2) || !TryParseAngle(values[0], out var skewAx))
                    return false;

                var skewAy = 0f;
                if (count == 2 && !TryParseAngle(values[1], out skewAy))
                    return false;

                step = new CssTransform(1, MathF.Tan(skewAy), MathF.Tan(skewAx), 1, 0, 0);
                return true;

            case "skewx":
                if (count != 1 || !TryParseAngle(values[0], out var skewOnlyX))
                    return false;
                step = new CssTransform(1, 0, MathF.Tan(skewOnlyX), 1, 0, 0);
                return true;

            case "skewy":
                if (count != 1 || !TryParseAngle(values[0], out var skewOnlyY))
                    return false;
                step = new CssTransform(1, MathF.Tan(skewOnlyY), 0, 1, 0, 0);
                return true;

            default:
                // A 3D function, or not a transform function at all. Either way there is no 2D
                // matrix for it, and §3 drops an invalid declaration whole rather than the function.
                return false;
        }
    }

    /// <summary>
    /// A <c>&lt;length-percentage&gt;</c> in CSS pixels, with a percentage taken of
    /// <paramref name="percentBasis"/>.
    /// </summary>
    /// <remarks>
    /// Only the absolute length units resolve here. A font- or viewport-relative one
    /// (<c>em</c>, <c>rem</c>, <c>vh</c>, <c>ch</c>, …) needs a font size or a viewport that this
    /// function is not given, and reading <c>2em</c> as two pixels would be a quiet error rather
    /// than a refusal — so it is a refusal. The computed value of <c>transform</c> has its lengths
    /// made absolute already, so a relative unit arriving here means the caller passed a specified
    /// value instead of a computed one.
    /// </remarks>
    private static bool TryResolveLength(string token, float percentBasis, out float pixels)
    {
        pixels = 0;
        if (token.Length == 0)
            return false;

        if (token[^1] == '%')
        {
            if (!TryParseNumber(token[..^1], out var percent))
                return false;

            pixels = percentBasis * percent / 100f;
            return true;
        }

        var (unit, factor) = UnitFactor(token);
        if (factor is 0f)
            return false;

        if (!TryParseNumber(token[..^unit], out var number))
            return false;

        pixels = number * factor;
        return true;
    }

    /// <summary>
    /// The trailing absolute length unit's length and its pixel factor, or a zero factor when the
    /// token carries no unit this can resolve. A unitless zero is the one length the grammar takes
    /// without a unit, so it resolves to zero pixels.
    /// </summary>
    private static (int Unit, float Factor) UnitFactor(string token)
    {
        if (token.EndsWith("px", StringComparison.OrdinalIgnoreCase)) return (2, 1f);
        if (token.EndsWith("cm", StringComparison.OrdinalIgnoreCase)) return (2, 96f / 2.54f);
        if (token.EndsWith("mm", StringComparison.OrdinalIgnoreCase)) return (2, 96f / 25.4f);
        if (token.EndsWith("in", StringComparison.OrdinalIgnoreCase)) return (2, 96f);
        if (token.EndsWith("pt", StringComparison.OrdinalIgnoreCase)) return (2, 96f / 72f);
        if (token.EndsWith("pc", StringComparison.OrdinalIgnoreCase)) return (2, 16f);
        if (token.EndsWith("q", StringComparison.OrdinalIgnoreCase)) return (1, 96f / 101.6f);

        return TryParseNumber(token, out var bare) && bare == 0f ? (0, 1f) : (0, 0f);
    }

    /// <summary>
    /// A scale factor: <c>&lt;number&gt;</c> or <c>&lt;percentage&gt;</c> (CSS Transforms 2), where
    /// the percentage is simply the ratio — <c>scale(50%)</c> is <c>scale(0.5)</c>.
    /// </summary>
    private static bool TryParseScaleFactor(string token, out float factor)
    {
        if (token.Length > 0 && token[^1] == '%')
        {
            if (!TryParseNumber(token[..^1], out factor))
                return false;

            factor /= 100f;
            return true;
        }

        return TryParseNumber(token, out factor);
    }

    /// <summary>
    /// An <c>&lt;angle&gt;</c> in radians. A bare number is <em>not</em> an angle in CSS — only a
    /// bare zero is, by the <c>&lt;zero&gt;</c> allowance — so <c>rotate(45)</c> is invalid and
    /// takes the declaration with it, exactly as it does in a browser. That is the point at which
    /// this parser and the SVG attribute grammar, where a bare number means degrees, part company.
    /// </summary>
    private static bool TryParseAngle(string token, out float radians)
    {
        radians = 0;
        if (token.Length == 0)
            return false;

        var (unit, factor) = token switch
        {
            _ when token.EndsWith("deg", StringComparison.OrdinalIgnoreCase) => (3, MathF.PI / 180f),
            _ when token.EndsWith("grad", StringComparison.OrdinalIgnoreCase) => (4, MathF.PI / 200f),
            _ when token.EndsWith("turn", StringComparison.OrdinalIgnoreCase) => (4, MathF.Tau),
            _ when token.EndsWith("rad", StringComparison.OrdinalIgnoreCase) => (3, 1f),
            _ => (0, 0f),
        };

        if (factor is 0f)
            return TryParseNumber(token, out var bare) && bare == 0f;

        if (!TryParseNumber(token[..^unit], out var number))
            return false;

        radians = number * factor;
        return true;
    }

    private static bool TryParseNumber(string token, out float number) =>
        float.TryParse(token.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
        && float.IsFinite(number);

    /// <summary>
    /// The <c>)</c> closing the <c>(</c> at <paramref name="open"/>, or -1 when it is unclosed. The
    /// nesting matters because a <c>calc()</c> argument carries its own parentheses, and stopping at
    /// the first <c>)</c> would cut the argument list in half and read what is left as valid.
    /// </summary>
    private static int FindMatchingParenthesis(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '(')
                depth++;
            else if (text[i] == ')' && --depth == 0)
                return i;
        }

        return -1;
    }

    /// <summary>Whether a stretch of the value is whitespace, which is all that may follow the last function.</summary>
    private static bool IsBlank(string text, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
                return false;
        }

        return true;
    }
}

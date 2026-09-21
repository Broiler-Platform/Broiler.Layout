using System;
using System.Collections.Generic;
using System.Drawing;

namespace Broiler.Layout.IR;

/// <summary>
/// CSS 2.1 Appendix E: which fragments of a laid-out tree cover a document-space point, topmost
/// first in painting order — the query behind <c>elementFromPoint</c> and <c>elementsFromPoint</c>.
/// </summary>
/// <remarks>
/// <para>
/// Painting order is not tree order, and the difference is not a detail: a <c>z-index: -1</c> child
/// paints behind its parent's background, the later of two positioned siblings loses to the earlier
/// one when that one has the higher <c>z-index</c>, and a top-layer <c>&lt;dialog&gt;</c> paints
/// above every ordinary stacking context wherever it sits in the tree. A consumer walking the DOM in
/// reverse child order gets all three backwards, and there is nothing in a bag of rectangles to
/// recover the order from. The inputs that decide it — <see cref="Fragment.CreatesStackingContext"/>,
/// <see cref="Fragment.StackLevel"/>, <see cref="Fragment.TopLayerOrder"/>, floats and positioning —
/// are resolved during layout and recorded on the fragment, so the query belongs beside them.
/// </para>
/// <para>
/// <b>Transforms are honoured.</b> Layout places boxes untransformed, because a transform is a
/// paint-time effect, so a fragment's <see cref="Fragment.Bounds"/> is its untransformed rectangle.
/// This composes each fragment's own <c>transform</c> about its <c>transform-origin</c> down the
/// tree and carries the query point back through the result
/// (<see cref="CssTransform.TryInvert"/>), so a rotated box is hit over the area it actually
/// covers rather than over the axis-aligned rectangle enclosing it — the corners of which it does
/// not cover at all.
/// </para>
/// <para>
/// <b>What this does not model.</b> <c>pointer-events</c> is not in the read model, so every visible
/// fragment is a candidate. And a positioned descendant with <c>z-index: auto</c> is treated as
/// though it created a stacking context, which is how Appendix E step 8 describes painting it; the
/// difference from the letter of the rule shows only when a descendant of such an element carries a
/// <c>z-index</c> that would interleave with boxes outside it.
/// </para>
/// </remarks>
public static class FragmentHitTest
{
    /// <summary>
    /// The fragments under <paramref name="root"/> whose box covers <paramref name="point"/>,
    /// topmost first in painting order. The instances are the ones in the tree that was passed in,
    /// so a caller holding the fragment-to-element map its producer built can name the elements.
    /// </summary>
    /// <param name="root">The root of a laid-out fragment tree.</param>
    /// <param name="point">The point, in the same absolute document space as <see cref="Fragment.Location"/>.</param>
    public static IReadOnlyList<Fragment> FragmentsAt(Fragment root, PointF point)
    {
        ArgumentNullException.ThrowIfNull(root);

        var painted = new List<Candidate>();
        var topLayer = new List<Candidate>();
        PaintStackingContext(root, Compose(CssTransform.Identity, root), painted, topLayer);
        PaintTopLayer(topLayer, painted);

        // Painting order runs bottom to top, so the hit list is its reverse.
        var hits = new List<Fragment>();
        for (var i = painted.Count - 1; i >= 0; i--)
        {
            if (Covers(painted[i], point))
                hits.Add(painted[i].Fragment);
        }

        return hits;
    }

    /// <summary>
    /// The topmost fragment covering <paramref name="point"/>, or null when none does —
    /// <c>elementFromPoint</c> to <see cref="FragmentsAt"/>'s <c>elementsFromPoint</c>.
    /// </summary>
    public static Fragment? TopmostAt(Fragment root, PointF point)
    {
        ArgumentNullException.ThrowIfNull(root);

        var hits = FragmentsAt(root, point);
        return hits.Count > 0 ? hits[0] : null;
    }

    /// <summary>A fragment together with the transform chain it sits under.</summary>
    private readonly struct Candidate(Fragment fragment, CssTransform transform)
    {
        public Fragment Fragment { get; } = fragment;

        public CssTransform Transform { get; } = transform;
    }

    /// <summary>
    /// A descendant waiting to be painted, with the tree position that breaks ties between equal
    /// stack levels — Appendix E orders those in tree order, and a sort that is not stable would
    /// decide them arbitrarily.
    /// </summary>
    private readonly struct Pending(Fragment fragment, CssTransform transform, int order)
    {
        public Fragment Fragment { get; } = fragment;

        public CssTransform Transform { get; } = transform;

        public int Order { get; } = order;

        public int StackLevel => Fragment.StackLevel;
    }

    /// <summary>The buckets Appendix E paints in sequence, within one stacking context.</summary>
    private sealed class Buckets
    {
        public List<Pending> StackingContexts { get; } = [];

        public List<Pending> BlockBackgrounds { get; } = [];

        public List<Pending> Floats { get; } = [];

        public List<Pending> Inlines { get; } = [];

        public List<Pending> PositionedAtZero { get; } = [];

        public int Next { get; set; }
    }

    /// <summary>
    /// Appends everything <paramref name="stackingContext"/> paints, in painting order, to
    /// <paramref name="painted"/>.
    /// </summary>
    private static void PaintStackingContext(
        Fragment stackingContext, CssTransform inherited,
        List<Candidate> painted, List<Candidate> topLayer)
    {
        // Step 1: the element forming the stacking context paints its own background and border
        // first, under everything it contains — which is exactly why a z-index: -1 child, painted in
        // step 3, ends up behind its own parent.
        painted.Add(new Candidate(stackingContext, inherited));

        var buckets = new Buckets();
        Collect(stackingContext, inherited, buckets, topLayer);

        // Step 3: child stacking contexts with negative stack levels, most negative first.
        var contexts = buckets.StackingContexts;
        contexts.Sort(static (left, right) =>
            left.StackLevel != right.StackLevel
                ? left.StackLevel.CompareTo(right.StackLevel)
                : left.Order.CompareTo(right.Order));

        var index = 0;
        while (index < contexts.Count && contexts[index].StackLevel < 0)
            PaintNested(contexts[index++], painted, topLayer);

        // Steps 4, 5 and 7: in-flow block-level backgrounds, then floats, then inline-level content.
        // These are flat across the whole stacking context, so every block background in it paints
        // under every float in it, which in turn paints under every line of text.
        foreach (var pending in buckets.BlockBackgrounds)
            painted.Add(new Candidate(pending.Fragment, pending.Transform));

        foreach (var pending in buckets.Floats)
            PaintNested(pending, painted, topLayer);

        // An atomic inline — inline-block and its relatives — paints as though it created a stacking
        // context (step 7.2.1); a plain inline's own children were spread into these same buckets,
        // so it contributes only itself.
        foreach (var pending in buckets.Inlines)
        {
            if (IsAtomicInline(pending.Fragment))
                PaintNested(pending, painted, topLayer);
            else
                painted.Add(new Candidate(pending.Fragment, pending.Transform));
        }

        // Step 8: positioned descendants with a stack level of zero, and the stacking contexts at
        // that level, interleaved in tree order — the sort above already put them in it.
        var positioned = buckets.PositionedAtZero;
        var atZero = new List<Pending>(positioned.Count);
        atZero.AddRange(positioned);
        while (index < contexts.Count && contexts[index].StackLevel == 0)
            atZero.Add(contexts[index++]);

        atZero.Sort(static (left, right) => left.Order.CompareTo(right.Order));
        foreach (var pending in atZero)
            PaintNested(pending, painted, topLayer);

        // Step 9: the positive stack levels, least positive first.
        while (index < contexts.Count)
            PaintNested(contexts[index++], painted, topLayer);
    }

    private static void PaintNested(Pending pending, List<Candidate> painted, List<Candidate> topLayer) =>
        PaintStackingContext(pending.Fragment, pending.Transform, painted, topLayer);

    /// <summary>
    /// CSS Position 4: the top layer paints above every ordinary stacking context, ordered so that a
    /// later-added element covers an earlier one. Each entry paints as its own stacking context.
    /// </summary>
    private static void PaintTopLayer(List<Candidate> topLayer, List<Candidate> painted)
    {
        if (topLayer.Count == 0)
            return;

        // A top-layer element may itself contain one — a dialog holding a popover. The ordinary walk
        // stops at the outer one and never descends past it, so the inner one is still unfound here;
        // collecting it now is what keeps it in the painting at all. Only the entries the ordinary
        // walk found are expanded, because each expansion descends through every top-layer box below
        // it, and those entries sit in disjoint subtrees — so nothing is collected twice.
        var found = topLayer.Count;
        for (var i = 0; i < found; i++)
            CollectTopLayer(topLayer[i].Fragment, topLayer[i].Transform, topLayer);

        // The layer is flat however deeply its entries nest, so one order decides all of them.
        topLayer.Sort(static (left, right) =>
            (left.Fragment.TopLayerOrder ?? 0).CompareTo(right.Fragment.TopLayerOrder ?? 0));

        // Every entry is in this list now, so the nested pass discards what it finds rather than
        // appending to a list being iterated.
        var nested = new List<Candidate>();
        foreach (var entry in topLayer)
            PaintStackingContext(entry.Fragment, entry.Transform, painted, nested);
    }

    /// <summary>
    /// The top-layer boxes anywhere below <paramref name="parent"/>, with the transform chain each
    /// sits under — including those below another top-layer box, which is why this descends past one
    /// rather than stopping at it the way <see cref="Collect"/> does.
    /// </summary>
    private static void CollectTopLayer(Fragment parent, CssTransform parentTransform, List<Candidate> into)
    {
        foreach (var child in parent.Children)
        {
            if (child is null || child.Style.Display == "none")
                continue;

            var transform = Compose(parentTransform, child);
            if (child.TopLayerOrder is not null)
                into.Add(new Candidate(child, transform));

            CollectTopLayer(child, transform, into);
        }
    }

    /// <summary>
    /// Sorts the descendants of one stacking context into the buckets Appendix E paints in turn,
    /// without crossing into a nested stacking context — that paints as a unit at its own level.
    /// </summary>
    private static void Collect(
        Fragment parent, CssTransform parentTransform, Buckets buckets, List<Candidate> topLayer)
    {
        foreach (var child in parent.Children)
        {
            if (child is null || child.Style.Display == "none")
                continue;

            var transform = Compose(parentTransform, child);

            // A top-layer box leaves the ordinary stacking entirely, and takes its subtree with it.
            if (child.TopLayerOrder is not null)
            {
                topLayer.Add(new Candidate(child, transform));
                continue;
            }

            var pending = new Pending(child, transform, buckets.Next++);

            if (child.CreatesStackingContext)
            {
                buckets.StackingContexts.Add(pending);
                continue;
            }

            // Appendix E step 8 paints a positioned descendant with z-index auto as though it were a
            // stacking context, so its subtree stays with it instead of being spread across the
            // buckets of the context above.
            if (IsPositioned(child))
            {
                buckets.PositionedAtZero.Add(pending);
                continue;
            }

            if (IsFloated(child))
            {
                buckets.Floats.Add(pending);
                continue;
            }

            if (IsInlineLevel(child))
            {
                buckets.Inlines.Add(pending);
                if (!IsAtomicInline(child))
                    Collect(child, transform, buckets, topLayer);

                continue;
            }

            // In-flow and block-level: its background joins this context's, and its contents are
            // this context's contents too.
            buckets.BlockBackgrounds.Add(pending);
            Collect(child, transform, buckets, topLayer);
        }
    }

    /// <summary>
    /// The transform chain at <paramref name="fragment"/>: its ancestors' composed with its own,
    /// applied about its <c>transform-origin</c>. An unresolvable value — a 3D function, say —
    /// contributes nothing, which leaves the fragment hit over its untransformed box.
    /// </summary>
    private static CssTransform Compose(CssTransform inherited, Fragment fragment)
    {
        if (!CssTransform.TryResolve(fragment.Style.Transform, fragment.Bounds, out var own))
            return inherited;

        var origin = CssTransformOrigin.Resolve(
            fragment.Style.TransformOrigin, fragment.Bounds, initialIsBoxCorner: false);

        return inherited.Concat(own.AboutOrigin(origin));
    }

    /// <summary>
    /// Whether the candidate's box covers <paramref name="point"/>, with the point carried back
    /// through the candidate's transform chain first so the test is against the box as laid out.
    /// </summary>
    private static bool Covers(Candidate candidate, PointF point)
    {
        var fragment = candidate.Fragment;

        // visibility: hidden removes the box as a hit target but not its visible descendants, which
        // are separate fragments and are tested on their own.
        if (fragment.Style.Visibility == "hidden" || fragment.Style.Visibility == "collapse")
            return false;

        var local = point;
        if (!candidate.Transform.IsIdentity)
        {
            if (!candidate.Transform.TryInvert(out var inverse))
                return false;

            local = inverse.Map(point.X, point.Y);
        }

        // An inline box spanning several lines is one fragment covering several rectangles, and the
        // rectangle enclosing them all reaches into the space beside each line, where the inline is
        // not.
        if (fragment.InlineRects is { Count: > 0 } rects)
        {
            foreach (var rect in rects)
            {
                if (rect.Contains(local))
                    return true;
            }

            return false;
        }

        return fragment.Bounds.Contains(local);
    }

    private static bool IsPositioned(Fragment fragment) =>
        fragment.Style.Position is "relative" or "absolute" or "fixed" or "sticky";

    private static bool IsFloated(Fragment fragment) =>
        fragment.Style.Float is "left" or "right";

    private static bool IsInlineLevel(Fragment fragment) =>
        fragment.Style.Display is "inline" or "inline-block" or "inline-table"
            or "inline-flex" or "inline-grid";

    /// <summary>
    /// An atomic inline-level box: inline-level outside, an independent formatting context inside,
    /// which Appendix E step 7.2.1 paints as though it created a stacking context.
    /// </summary>
    private static bool IsAtomicInline(Fragment fragment) =>
        fragment.Style.Display is "inline-block" or "inline-table" or "inline-flex" or "inline-grid";
}

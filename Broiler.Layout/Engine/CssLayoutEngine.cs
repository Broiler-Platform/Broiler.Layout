using Broiler.CSS;
using Broiler.Layout.Diagnostics;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace Broiler.Layout.Engine;

internal static class CssLayoutEngine
{
    /// <summary>
    /// Returns true when <paramref name="box"/> or any of its ancestors
    /// (up to but not including <paramref name="stop"/>) has
    /// <c>position:absolute</c> or <c>position:fixed</c>.
    /// </summary>
    private static bool IsInAbsposSubtree(CssBox box, CssBox stop)
    {
        for (var b = box; b != null && b != stop; b = b.ParentBox)
        {
            if (b.Position == CssConstants.Absolute || b.Position == CssConstants.Fixed)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Approximate ratio of font ascent to total font height for typical
    /// Latin fonts.  Used to compute baseline position when full font
    /// metrics are not directly available (CSS2.1 §10.8 strut).
    /// </summary>
    internal const double TypicalAscentRatio = 0.8;

    /// <summary>
    /// Resolves a replaced element's specified width/height to a definite pixel
    /// length when it is neither <c>auto</c>, a percentage, nor an intrinsic-size
    /// keyword. Unlike a raw <see cref="CssLength"/> pixel check this resolves
    /// font- and viewport-relative units (em/rem, vw/vh, …) through the length
    /// parser, so e.g. <c>block-size: 55vw</c> sizes an image. Percentages are
    /// excluded here because they resolve against the containing block, which the
    /// caller handles separately.
    /// </summary>
    internal static bool TryResolveDefiniteImageLength(string value, double em, out double px)
    {
        px = 0;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        string t = value.Trim();

        // Only a numeric length (or calc()) is a definite tag size. Reject every
        // keyword size — auto, none, min/max/fit-content, stretch,
        // fill-available, … — which resolves against layout, not to a fixed px
        // value here (and a percentage, which the caller resolves against the
        // containing block). A definite length always begins with a digit, sign,
        // or decimal point.
        char c0 = t[0];
        bool looksNumeric = char.IsDigit(c0) || c0 == '+' || c0 == '-' || c0 == '.';
        bool isCalc = t.StartsWith("calc(", StringComparison.OrdinalIgnoreCase);

        if (!(looksNumeric || isCalc) || t.EndsWith('%'))
            return false;

        double v = CssLengthParser.ParseLength(t, 0, em);

        if (double.IsNaN(v) || double.IsInfinity(v) || v < 0)
            return false;

        px = v;

        return true;
    }

    /// <summary>
    /// Whether the block-size basis a percentage on <paramref name="box"/> would resolve against
    /// lies beyond a grid area — in which case there is no basis to use here.
    /// </summary>
    /// <remarks>
    /// A grid item's block size comes from the track that holds it, and its descendants' percentage
    /// heights resolve against that grid area. Broiler does not model a grid area as a containing
    /// block, so the basis walk goes straight past the grid to the next ancestor that states a
    /// height — which is a different, usually larger, number. An `img { height: 100% }` in a `1fr`
    /// row of a 200px grid would resolve to 200 rather than to the row's 100
    /// (css-grid/replaced-element-percentage-height-in-grid-nested-in-flex-001). Declining leaves
    /// the image at its natural size, which is what it had before any of this resolved at all.
    /// </remarks>
    private static bool PercentageBlockBasisCrossesAGridArea(CssBox box)
    {
        for (var cb = box.ContainingBlock; cb?.ParentBox != null; cb = cb.ContainingBlock)
        {
            bool heightIsAuto = cb.Height == CssConstants.Auto || string.IsNullOrEmpty(cb.Height);

            // A stated, non-percentage height is the basis the walk would stop at, so nothing
            // beyond it matters.
            if (!heightIsAuto && !cb.Height.Contains('%'))
                return false;

            if (cb.ParentBox.Display is "grid" or "inline-grid")
                return true;
        }

        return false;
    }

    public static void MeasureImageSize(ILayoutEnvironment g, CssRectImage imageWord)
    {
        ArgumentNullException.ThrowIfNull(imageWord);
        ArgumentNullException.ThrowIfNull(imageWord.OwnerBox);

        // Phase 2b: replaced-element intrinsics come from the layout environment
        // rather than reading RImage members directly (see roadmap §4).
        ImageIntrinsics? image = imageWord.Image is { } handle ? g.GetImageIntrinsics(handle) : null;

        // HTML §4.8.4.3: a `srcset` candidate states the density it is meant to be shown at, and the
        // element's natural size is the decoded bitmap divided by it — a `2x` candidate is laid out
        // at half the pixels it decodes to, and a `100w` candidate in a `sizes="400px"` slot at four
        // times them. `PixelDensity` is 1 for every image that did not come from a candidate list,
        // which leaves this arithmetic an identity for them. An infinite density (a candidate
        // selected against a zero-width slot) gives the zero-sized image the spec asks for.
        image = ApplyPixelDensity(image, imageWord.PixelDensity);

        // CSS Containment 2 §3.2: a size-contained replaced element "must be treated as having no
        // natural dimensions and no natural aspect ratio". Dropping the decoded bitmap here is the
        // whole of that: the auto branches below fall to the contained stand-ins, and `usedRatio`
        // has nothing left to derive one axis from the other with. An author's own `width`,
        // `height` or `aspect-ratio` still applies — containment hides the contents, not the box's
        // own declarations.
        bool sizeContained = imageWord.OwnerBox.AppliesSizeContainment;

        if (sizeContained)
            image = null;

        double em = imageWord.OwnerBox.GetEmHeight();

        // A specified, non-percentage size counts as a "tag" size — but resolve it
        // through the length parser rather than only accepting raw pixels, so
        // font- and viewport-relative units (em/rem, vw/vh, …) that map onto an
        // image's width/height (including the logical block-size/inline-size, e.g.
        // `block-size: 55vw`) size the image instead of silently falling through to
        // its intrinsic size. (WPT css-grid/nested-grid-item-block-size-001.)
        bool hasImageTagWidth = TryResolveDefiniteImageLength(imageWord.OwnerBox.Width, em, out double tagWidthPx);
        bool hasImageTagHeight = TryResolveDefiniteImageLength(imageWord.OwnerBox.Height, em, out double tagHeightPx);

        // A percentage width is a stated width too — resolved against the containing block rather
        // than read off the declaration, which is the only reason it is not a "tag" width here.
        // The aspect-ratio pass below must not treat it as `auto` and derive it back from the
        // height, or `<img width="100%" height="50">` comes out 50px wide.
        bool hasStatedWidth = hasImageTagWidth;

        if (hasImageTagWidth)
        {
            imageWord.Width = tagWidthPx;
        }
        else
        {
            // Parse the width as a CssLength only here: it is unused when a definite
            // tag width was resolved above, so this avoids a redundant parse per
            // sized image. (The percentage branch needs the raw fraction, which the
            // definite-length resolver above deliberately rejects.)
            var width = new CssLength(imageWord.OwnerBox.Width);
            if (width.Number > 0 && width.IsPercentage)
            {
                imageWord.Width = width.Number * imageWord.OwnerBox.ContainingBlock.Size.Width;

                // CSS 2.1 §10.4 uses the intrinsic ratio to fill in a dimension that is `auto`, not
                // to overrule one the author stated — and a percentage width is no different from a
                // length one in that. Treating it as auto made `<img width="100%" height="50">`
                // come out as tall as it was wide. That is the shape CSS2's own reference files use,
                // so the cost landed on the reference side of 85 reftests in css/CSS2/backgrounds
                // alone.
                hasStatedWidth = true;
            }
            else if (image != null)
            {
                imageWord.Width = imageWord.ImageRectangle == RectangleF.Empty
                    ? image.Value.Width
                    : imageWord.ImageRectangle.Width / imageWord.PixelDensity;

                // CSS2.1 §10.3.2: when width is auto the used value is the
                // intrinsic width.  Do NOT clamp to the containing block —
                // inline replaced elements are allowed to overflow their
                // container.  Authors use max-width:100% to opt into clamping.
            }
            else if (sizeContained)
            {
                imageWord.Width = imageWord.OwnerBox.ContainedIntrinsicContentWidth;
            }
            else
            {
                imageWord.Width = hasImageTagHeight ? tagHeightPx / 1.14f : 20;
            }
        }

        // A percentage height is a stated height too, on exactly the footing the percentage *width*
        // branch above puts one: it resolves against the nearest ancestor that states a definite
        // block size, and the ratio then carries it to the inline axis. Nothing resolved it — the
        // definite-length resolver rejects percentages by design — so `img { height: 100% }` kept
        // its bitmap's height, and with it its bitmap's width. Every box sized from that image's
        // intrinsic contribution then came out the bitmap's width: a `float` around a 200x200 image
        // asked to be 100px tall is 100px wide, not 200 (css-sizing/intrinsic-percent-replaced-*,
        // 40 of its 45 tests, and the shrink-to-fit half of the same rule in css-flexbox).
        double percentHeightPx = 0;
        bool hasPercentageHeight =
            !hasImageTagHeight
            && imageWord.OwnerBox.Height is { Length: > 0 } blockSize
            && blockSize.Contains('%')
            && !PercentageBlockBasisCrossesAGridArea(imageWord.OwnerBox)
            && imageWord.OwnerBox.TryResolveSpecifiedReplacedContentHeight(out percentHeightPx);

        bool hasStatedHeight = hasImageTagHeight || hasPercentageHeight;

        if (hasImageTagHeight)
        {
            imageWord.Height = tagHeightPx;
        }
        else if (hasPercentageHeight)
        {
            imageWord.Height = percentHeightPx;
        }
        else if (image != null)
        {
            imageWord.Height = imageWord.ImageRectangle == RectangleF.Empty
                ? image.Value.Height
                : imageWord.ImageRectangle.Height / imageWord.PixelDensity;
        }
        else if (sizeContained)
        {
            imageWord.Height = imageWord.OwnerBox.ContainedIntrinsicContentHeight;
        }
        else
        {
            imageWord.Height = imageWord.Width > 0 ? imageWord.Width * 1.14f : 22.8f;
        }

        // CSS Sizing 4 §4: an explicit `aspect-ratio` on a replaced element with a
        // natural aspect ratio overrides that natural ratio for computing the
        // dimension left `auto`. Derive the missing side from the specified one
        // through the CSS ratio (width/height); fall back to the intrinsic image
        // ratio when no `aspect-ratio` is declared. (WPT css-grid/
        // nested-grid-item-block-size-001: `aspect-ratio: 2/1` + `block-size: 55vw`.)
        bool hasCssAspectRatio =
            CssBox.TryParseAspectRatio(imageWord.OwnerBox.AspectRatio, out double cssAspectRatio)
            && cssAspectRatio > 0;

        // The ratio the box is sized by: the author's preferred one when declared, otherwise the
        // image's natural one. Zero when the box has neither, which leaves the two axes independent.
        // HTML §14.4: a size-contained replaced element has no natural ratio left, but the UA
        // stylesheet's `aspect-ratio: attr(width) / attr(height)` is a declaration and survives —
        // so `<img width=60 height=60 style="width: 100px; height: auto; contain: size">` is still
        // square (WPT css-contain/contain-size-replaced-007, whose assert says exactly that).
        double usedRatio = hasCssAspectRatio
            ? cssAspectRatio
            : image is { HasIntrinsicRatio: true, Height: > 0 } natural ? natural.Width / natural.Height
            : sizeContained && imageWord.OwnerBox.TryGetContainedPresentationalRatio(out double attributeRatio)
                ? attributeRatio
                : 0;

        bool widthDriven = hasStatedWidth && !hasStatedHeight;

        if (usedRatio > 0)
        {
            // If only the width was stated, the ratio fills in the height, and vice versa.
            if (widthDriven)
                imageWord.Height = imageWord.Width / usedRatio;
            else if (hasStatedHeight && !hasStatedWidth)
                imageWord.Width = imageWord.Height * usedRatio;
        }

        // CSS2.1 §10.4/§10.7: apply the min/max constraints to the tentative size settled above.
        // The two axes are coupled through the ratio, so this cannot be four independent clamps —
        // see ReplacedBoxSizing for the table §10.4 resolves a double violation with.
        double usedWidth = imageWord.Width;
        double usedHeight = imageWord.Height;

        ReplacedBoxSizing.ApplyMinMax(
            ref usedWidth, ref usedHeight,
            widthIsAuto: !hasStatedWidth, heightIsAuto: !hasStatedHeight, usedRatio,
            imageWord.OwnerBox.ResolveInlineSizeBounds(),
            imageWord.OwnerBox.ResolveBlockSizeBounds());

        imageWord.Width = usedWidth;
        imageWord.Height = usedHeight;

        imageWord.Height += imageWord.OwnerBox.ActualBorderBottomWidth + imageWord.OwnerBox.ActualBorderTopWidth + imageWord.OwnerBox.ActualPaddingTop + imageWord.OwnerBox.ActualPaddingBottom;
    }

    /// <summary>
    /// The decoded bitmap's dimensions divided by the density its <c>srcset</c> candidate was
    /// selected at — HTML §4.8.4.3's <b>density-corrected natural width and height</b>. The aspect
    /// ratio is a quotient of the two and so is untouched by a uniform scale, which is what keeps
    /// this from disturbing the replaced-sizing path for the overwhelming majority of images, whose
    /// density is exactly 1.
    /// </summary>
    private static ImageIntrinsics? ApplyPixelDensity(ImageIntrinsics? image, double density)
    {
        if (image is not { } intrinsics || density == 1.0 || double.IsNaN(density) || density <= 0)
            return image;

        return intrinsics with
        {
            Width = intrinsics.Width / density,
            Height = intrinsics.Height / density,
        };
    }

    public static void CreateLineBoxes(ILayoutEnvironment g, CssBox blockBox)
    {
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(blockBox);

        using var trace = LayoutWorkTrace.Measure(LayoutWorkTrace.Ops.LineBreak);

        blockBox.LineBoxes.Clear();

        double limitRight = blockBox.ActualRight - blockBox.ActualPaddingRight - blockBox.ActualBorderRightWidth;

        //Get the start x and y of the blockBox
        double startx = blockBox.Location.X + blockBox.ActualPaddingLeft - 0 + blockBox.ActualBorderLeftWidth;
        double starty = blockBox.Location.Y + blockBox.ActualPaddingTop - 0 + blockBox.ActualBorderTopWidth;

        // CSS2.1 §9.5: the floats this block's lines have to share their width with. Collected
        // once per pass because every line consults them and the geometry does not change while
        // the block flows.
        blockBox.LineFloatBands = LineFloatBands.For(blockBox);

        double firstLineHeight = blockBox.ActualLineHeight > 0
            ? blockBox.ActualLineHeight
            : blockBox.ActualFont.Height;

        double curx = BandLeftAt(blockBox, starty, firstLineHeight, startx) + blockBox.ActualTextIndent;
        double cury = starty;

        //Reminds the maximum bottom reached
        double maxRight = startx;
        double maxBottom = starty;

        //First line box
        CssLineBox line = new(blockBox) { FlowTop = cury };

        //Flow words and boxes
        FlowBox(g, blockBox, blockBox, limitRight, 0, startx, ref line, ref curx, ref cury, ref maxRight, ref maxBottom);
        line.FlowBottom = maxBottom;

        DropTrailingForcedBreakLine(blockBox);

        // if width is not restricted we need to lower it to the actual width
        if (blockBox.ActualRight >= 90999)
        {
            blockBox.ActualRight = maxRight + blockBox.ActualPaddingRight + blockBox.ActualBorderRightWidth;
        }

        //Gets the rectangles for each line-box
        bool plaintext = string.Equals(blockBox.UnicodeBidi, "plaintext", StringComparison.OrdinalIgnoreCase);
        // CSS Text §bidi-linebox: under unicode-bidi:plaintext each line is its own
        // bidi paragraph.  Its base direction is the first strong character's; a line
        // with no strong character inherits the previous paragraph's base direction,
        // or the containing block's direction when there is none.  Otherwise every
        // line shares the block's own direction.  Because a <br> splits content into
        // sibling anonymous blocks, the "previous paragraph" may live in an earlier
        // sibling — seed the running base direction from the most recent strong
        // character preceding this block in document order.
        bool baseRtl = plaintext
            ? SeedPlaintextBaseRtl(blockBox)
            : blockBox.Direction == CssConstants.Rtl;

        foreach (var linebox in blockBox.LineBoxes)
        {
            bool lineRtl = baseRtl;
            if (plaintext)
            {
                lineRtl = LineFirstStrongRtl(linebox) ?? baseRtl;
                baseRtl = lineRtl;
            }

            ApplyHorizontalAlignment(linebox, lineRtl);
            ApplyRightToLeft(linebox, lineRtl);
            BubbleRectangles(blockBox, linebox);
            ApplyVerticalAlignment(linebox);
            FitWordlessInlineBoxes(linebox);

            linebox.AssignRectanglesToBoxes();
        }

        // CSS2.1 §10.8: After vertical alignment adjusts inline-block
        // positions (e.g. vertical-align: 2em raises boxes), recalculate
        // maxBottom from the actual post-alignment positions.
        //
        // CSS2.1 §10.8.1: The line box height is the distance between
        // the uppermost box top and the lowermost box bottom.  When
        // positive vertical-align raises inline-blocks above the flow
        // start, the line box extends upward.  The full line box height
        // must be reflected in the block's content height so that
        // subsequent siblings are positioned correctly.
        //
        // Example: Acid3's .buckets div has font: 0/0 (baseline at
        // content edge) and bucket6 extends 162px above the baseline.
        // The line box height is 162px, so the div's auto height = 162px
        // (plus padding/border).
        maxBottom = starty;

        // A flex container's lines are how this engine places its items, one per line in a column,
        // not line boxes: every item is blockified (CSS Display 3 §2.7) and lies in no inline
        // formatting context, so there is no strut whose line-height could make a line taller than
        // the item on it (CSS Flexbox §4). Holding each line to that height left a column one
        // line-height below a last item shorter than a line: three 10px items made a 39px column
        // where browsers make it 30, and one made it 19.
        bool linesHoldFlexItems = blockBox.Display is "flex" or "inline-flex";

        // Where each line ends, which the line after it starts below (see the restack below).
        var lineBottoms = new Dictionary<CssLineBox, double>();

        foreach (var linebox in blockBox.LineBoxes)
        {
            double blockBottom = maxBottom;
            maxBottom = double.MinValue;

            foreach (var rect in linebox.Rectangles)
            {
                // CSS2.1 §9.6.1: Absolutely/fixed positioned elements are
                // out of normal flow and must not affect the line box height.
                if (IsInAbsposSubtree(rect.Key, blockBox))
                    continue;

                // CSS2.1 §10.6.1: an inline, non-replaced box's vertical padding and border are
                // not part of its line. Its rectangle reaches above and below its words by them,
                // so a link with 10px of padding on a block's first line moved every line of the
                // block 10px down and made the block 10px taller. Its words, measured below, and
                // the atomic boxes inside it, which have rectangles of their own, are what it puts
                // on the line.
                if (rect.Key.IsInlineNonReplaced)
                    continue;

                maxBottom = Math.Max(maxBottom, rect.Value.Bottom);
                // CSS2.1 §10.8: an atomic inline-block contributes its *margin*
                // box plus the line's strut descent below the baseline to the
                // line-box height.  Its rectangle is only the border box (it
                // excludes the bottom margin), so for
                // an *anonymous* block — which has no visual box of its own and
                // exists purely to position the next block-level sibling (e.g.
                // the anonymous wrappers a <br> splits inline content into) —
                // extend its content height to the true line-box bottom.  This
                // mirrors what the inline-block *wrap* path (FlowInlineBlock)
                // already does in-scope, so a <br>-separated row lands where a
                // wrapped one does.  Restricted to anonymous blocks to avoid
                // changing the rendered height of author boxes.
                //
                // An inline-block with a line of text in it is left out: it stands on that line's
                // baseline (see LastLineBaseline), not on its bottom, and its own lines reach below
                // the baseline as the strut's do. Every block here is of the anonymous kind, the
                // ones the page's elements make too, so a line holding an inline-block of text was
                // a strut's descent taller than browsers make it.
                //
                // So is a box not aligned to the baseline, as an image is below: the strut's descent
                // lies below the baseline, and a box aligned `middle`, `top`, by a length or any
                // other way does not stand on it. An empty 30px inline-block aligned middle made a
                // 34px line of 20px text, where browsers make it 30px.
                if (blockBox.Kind == BoxKind.Anonymous
                    && IsBaselineAligned(rect.Key)
                    && ((rect.Key.Display == CssConstants.InlineBlock && LastLineBaseline(rect.Key) == null)
                        || rect.Key.Display is "inline-flex" or "inline-grid"))
                {
                    double marginBoxBottom = rect.Value.Bottom + rect.Key.ActualMarginBottom
                        + StrutDescent(blockBox);

                    maxBottom = Math.Max(maxBottom, marginBoxBottom);
                }

            }

            foreach (var word in linebox.Words)
            {
                if (IsInAbsposSubtree(word.OwnerBox, blockBox))
                    continue;

                // A word of text reaches as low as the inline box it stands in, the leading below
                // its glyphs lower (CSS 2.1 §10.8.1), as it reaches as high as the leading above
                // them (WordLayoutTop). The glyphs alone were measured: text that vertical-align
                // lowered left the line short by that leading, and a line of 16px/20px text with a
                // word aligned `sub` in it was 22.76px tall, where browsers make it 24.19px.
                maxBottom = Math.Max(maxBottom,
                    word.IsImage ? InlineWordLineBoxBottom(word) : WordLayoutBottom(word));

                // CSS2.1 §10.8: a baseline-aligned inline replaced element (image)
                // sits with its bottom on the baseline, so the line box still
                // extends below it by the strut's below-baseline descent.
                // InlineWordLineBoxBottom returns only the image bottom (the
                // baseline), so add that descent here — mirroring the inline-block
                // path above. Without it, a line whose height is set by a tall
                // image drops the text descent and every following line creeps up
                // (CSS2 visudet/replaced-elements-*).
                if (word.IsImage
                    && (word.OwnerBox == null
                        || string.IsNullOrEmpty(word.OwnerBox.VerticalAlign)
                        || word.OwnerBox.VerticalAlign == CssConstants.Baseline))
                {
                    maxBottom = Math.Max(maxBottom,
                        word.Bottom + ImageWordMarginBottom(word) + StrutDescent(blockBox));
                }

            }

            double lineTop = double.MaxValue;
            double inlineBoxTop = double.MaxValue;
            bool hasLineContent = false;

            foreach (var rect in linebox.Rectangles)
            {
                if (IsInAbsposSubtree(rect.Key, blockBox))
                    continue;

                // An inline, non-replaced box starts where its words do, as above. Its
                // rectangle is the line's top only when no word is on the line: that of an
                // inline box given a width, which the flow records from the line's top.
                if (rect.Key.IsInlineNonReplaced)
                    inlineBoxTop = Math.Min(inlineBoxTop, rect.Value.Top);
                else
                    lineTop = Math.Min(lineTop, rect.Value.Top);

                hasLineContent = true;
            }

            foreach (var word in linebox.Words)
            {
                if (IsInAbsposSubtree(word.OwnerBox, blockBox))
                    continue;

                lineTop = Math.Min(lineTop, WordLayoutTop(word));
                hasLineContent = true;
            }

            if (lineTop == double.MaxValue)
                lineTop = inlineBoxTop;

            double contentTop = lineTop;

            // The line starts where the flow put it, whatever alignment moved down from there: an
            // inline-block alone on a line stands on the strut's baseline, below the line's top,
            // and a line measured from the box was as much taller than its line height: at
            // 16px/20px, a 10px inline-block made a 24.85px line, where browsers make it 20px.
            // Nor does content raised above that top start a line after the first: such a line
            // is moved down below, with its strut, until the content starts at its top. The first
            // line is measured from what is highest on it, as it always was.
            if (hasLineContent && linebox.FlowTop is double flowTop)
                lineTop = ReferenceEquals(linebox, blockBox.LineBoxes[0]) ? Math.Min(lineTop, flowTop) : flowTop;

            if (hasLineContent && blockBox.ActualLineHeight > 0 && !linesHoldFlexItems)
                maxBottom = Math.Max(maxBottom, lineTop + blockBox.ActualLineHeight);

            // The strut stands on the line's baseline, as the text on it does, and reaches the
            // strut's descent below it (StrutDescent). Measured a line height down from the line's
            // top, it was too high by as much as content above the strut raised that top: a span
            // aligned `text-bottom` in a 10px font, alone on a line of 16px/20px text, reaches
            // above the strut, and the line ended where the span does, 1.44px above the strut.
            if (hasLineContent && blockBox.ActualLineHeight > 0 && !linesHoldFlexItems
                && linebox.Baseline is double lineBaseline)
            {
                maxBottom = Math.Max(maxBottom, lineBaseline + StrutDescent(blockBox));
            }

            // So does the strut of an inline box holding no text on the line (StrutOnlyInlineBoxes),
            // which its words would otherwise measure: a span with `line-height: 40px` around an
            // image or an inline-block made a line of 16px/20px text shorter than 40px.
            if (hasLineContent && !linesHoldFlexItems && linebox.Baseline is double ownBaseline)
            {
                foreach (var box in StrutOnlyInlineBoxes(linebox))
                {
                    if (LineBoxAlignedRoot(box, blockBox) == null)
                        maxBottom = Math.Max(maxBottom, StrutOnlyExtent(box, ownBaseline).Bottom);
                }
            }

            // The inline boxes together reach down from the top of the highest of them, and on a line
            // after the first that is above the line's top when content on the line is raised above
            // it. Such a line moves down below, and the block grows by as much. Measured from the
            // line's top, the line was as much taller besides: a span with `line-height: 40px`
            // raised 10px on the second line of 16px/20px text made a 70px block, where browsers make
            // it 60px.
            if (hasLineContent && !linesHoldFlexItems)
                maxBottom = Math.Max(maxBottom, TallInlineBoxLineBottom(blockBox, linebox, Math.Min(lineTop, contentTop)));

            lineBottoms[linebox] = maxBottom;
            maxBottom = Math.Max(blockBottom, maxBottom);
        }

        // CSS2.1 §10.8.1: a line box reaches from the top of the highest box on it to the bottom of
        // the lowest, and line boxes stack (§9.4.2). Content that vertical-align raises above the
        // top of the line the flow put it on moves that line down until it starts at the line's
        // top, and every line after it with it, and the block is as much taller.
        //
        // Only content above the block's own top was moved, by moving every line down together,
        // which only content on the first line can reach: content raised above a later line's top
        // reached into the line above it. An image raised 10px on a second line of 16px/20px text
        // started 14.85px down its block, over the first line, where browsers start it 20px down
        // and make the block 5px taller.
        //
        // A box aligned `bottom` ends at the bottom of the rest of its line, and one taller than the
        // rest reaches above the line's top: the line moves down to hold it too. It moved no line
        // after the first, and a 40px box aligned `bottom` on the second line of 16px/20px text
        // started at the block's top, over the first line, where browsers start it 20px down and
        // make the second line 40px tall. A box aligned `top` starts where the rest of its line
        // does, and moves nothing that the rest does not.
        //
        // A line starts where the line above it ends, too, however much taller than the flow left
        // room for vertical alignment made that one: the flow starts each line below the one before
        // as it placed it, before the alignment. Text in a larger font, or a box lowered from the
        // baseline, reached into the next line: in 16px/20px text, a 32px "B" made its line 26px
        // tall in browsers, and the next line started 20px down, where browsers start it 26px down.
        double restack = 0;
        double lineAboveBottom = double.MinValue;
        double settledBottom = starty;

        foreach (var linebox in blockBox.LineBoxes)
        {
            double flowTopBelow = (linebox.FlowTop ?? starty) + restack;
            if (lineAboveBottom > flowTopBelow + 0.01)
                restack += lineAboveBottom - flowTopBelow;

            if (restack > 0)
                ShiftLineBox(linebox, restack);

            double flowTop = (linebox.FlowTop ?? starty) + restack;
            double contentTop = LineContentTop(linebox, blockBox);
            double raised = 0;

            if (contentTop < flowTop - 0.01)
            {
                raised = flowTop - contentTop;
                ShiftLineBox(linebox, raised);
            }

            // Where the line is now, for the floats placed from it (InlineFloats): its top has
            // moved with the lines above it, and its bottom with its own content too.
            linebox.RestackTop = restack;
            linebox.RestackBottom = restack + raised;
            restack += raised;

            if (lineBottoms[linebox] > double.MinValue)
            {
                lineAboveBottom = lineBottoms[linebox] + linebox.RestackBottom;
                settledBottom = Math.Max(settledBottom, lineAboveBottom);
            }
        }

        // The block reaches the bottom of its lowest line where the lines now are. Each line moved by
        // its own shift, not the last one's, so the lowest line before the restack moved by the
        // whole of it only where it is the last.
        maxBottom = settledBottom;

        // CSS2.1 §9.4.3: the lines are settled, so the boxes the flow placed on them whole take
        // their relative offsets now.
        foreach (var linebox in blockBox.LineBoxes)
            ApplyRelativeOffsets(linebox);

        // CSS2.1 §10.8: The "strut" — each line box starts with an
        // imaginary zero-width inline box with the block container's font
        // and line-height properties.  This establishes the minimum line
        // box height for inline formatting contexts.
        // The strut only affects content height when height is 'auto';
        // an explicit height (CSS2.1 §10.6.3) overrides the content height.
        // CSS2.1 §9.4.2: The strut only contributes to height when the
        // inline formatting context has actual inline content (words or
        // inline-level boxes).  An empty block should have zero content
        // height from the IFC.
        bool hasExplicitHeight = blockBox.Height != null && blockBox.Height != CssConstants.Auto;
        bool hasInlineContent = false;
        foreach (var lb in blockBox.LineBoxes)
        {
            // CSS2.1 §9.6.1: Words and rectangles from absolutely/fixed
            // positioned elements are not in-flow inline content.
            foreach (var w in lb.Words)
            {
                if (!IsInAbsposSubtree(w.OwnerBox, blockBox))
                {
                    hasInlineContent = true;
                    break;
                }
            }

            if (hasInlineContent) break;

            foreach (var r in lb.Rectangles)
            {
                if (!IsInAbsposSubtree(r.Key, blockBox))
                {
                    hasInlineContent = true;
                    break;
                }
            }

            if (hasInlineContent) break;
        }

        if (blockBox.ActualLineHeight > 0 && !hasExplicitHeight && hasInlineContent && !linesHoldFlexItems)
            maxBottom = Math.Max(maxBottom, starty + blockBox.ActualLineHeight);

        // The anonymous block a block-level image is wrapped in has no line box of its own to hold a
        // strut: the image sits at its top, and it ends where the image's margin box does.
        if (BlockLevelImageOf(blockBox) is { } blockLevelImage)
            maxBottom = PlaceBlockLevelImage(blockBox, blockLevelImage, starty, maxBottom);

        blockBox.ActualBottom = maxBottom + blockBox.ActualPaddingBottom + blockBox.ActualBorderBottomWidth;

        // CSS2.1 §10.6.3: When height is not 'auto', the used value is the
        // specified value.  Content may overflow (controlled by 'overflow').
        // For overflow:hidden, overflow:auto, and overflow:scroll the box's
        // layout height is clamped to the specified height so that subsequent
        // siblings are not pushed down by overflowing content.
        if (hasExplicitHeight
            && blockBox.Overflow is CssConstants.Hidden or CssConstants.Auto or CssConstants.Scroll
            && blockBox.ActualBottom - blockBox.Location.Y > blockBox.ActualHeight)
            blockBox.ActualBottom = blockBox.Location.Y + blockBox.ActualHeight;

        // The inline boxes an inline-block sits in wrap it only now that the lines are settled,
        // and before the out-of-flow descendants below are laid out: one of them can be their
        // containing block.
        foreach (var linebox in blockBox.LineBoxes)
            BubbleAtomicInlineRectangles(linebox);

        // CSS2.1 §9.6.1 / §10.3.7: An out-of-flow (absolutely/fixed positioned)
        // descendant of this inline formatting context was flowed by FlowBox only
        // to establish its static line-box rectangle; it never received its own
        // PerformLayout, so its used size and inset-based position are unresolved.
        // Now that the context's line rectangles are assigned (so an inline
        // containing block such as a position:relative <span> has real geometry),
        // lay those boxes out. Without this an abspos inside an inline CB reports
        // its static line position instead of its top/left inset box.
        LayoutOutOfFlowInlineDescendants(g, blockBox);
    }

    /// <summary>
    /// CSS Text 3 §4.1: a forced line break at the <em>end</em> of a block
    /// generates no line box of its own. Removes the trailing line if the flow
    /// left one holding nothing but preserved newlines.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A preserved <c>\n</c> is flowed as a zero-width word that opens the next
    /// line and is then reported onto it, so text ending in one leaves a final
    /// line box whose only content is that break — and the block was sized a
    /// whole line taller than every browser renders it. The engine already got
    /// this right for a trailing <c>&lt;br&gt;</c>, which is the same rule; only
    /// the <c>white-space: pre</c> spelling of it was wrong.
    /// </para>
    /// <para>
    /// Only the last line, only when something precedes it, and only when that
    /// something is real content. A break is only "at the end of the block" if
    /// the block had a line to end — a container whose entire content is one
    /// preserved newline still occupies a line box, and that line carries its
    /// inline's line-height (<c>quirks/line-height-preserved-segment-break</c>
    /// makes a 100px-line-height span holding nothing but a newline fill a
    /// 100px box). An interior empty line is content too: <c>"a\n\nb"</c> is
    /// three lines and stays three.
    /// </para>
    /// </remarks>
    private static void DropTrailingForcedBreakLine(CssBox blockBox)
    {
        if (blockBox.LineBoxes.Count < 2)
            return;

        var last = blockBox.LineBoxes[^1];

        if (last.Words.Count == 0 || last.Rectangles.Count > 0)
            return;

        foreach (var word in last.Words)
        {
            if (!word.IsLineBreak)
                return;
        }

        bool precededByContent = false;
        for (int i = 0; i < blockBox.LineBoxes.Count - 1 && !precededByContent; i++)
            precededByContent = blockBox.LineBoxes[i].Words.Count > 0
                || blockBox.LineBoxes[i].Rectangles.Count > 0;

        if (!precededByContent)
            return;

        blockBox.LineBoxes.RemoveAt(blockBox.LineBoxes.Count - 1);
    }

    /// <summary>
    /// Lays out the absolutely/fixed positioned boxes that hang off an inline
    /// formatting context established by <paramref name="ifcRoot"/>. FlowBox
    /// descends the inline subtree to establish each out-of-flow box's static
    /// position but does not run its block layout (size + inset position); this
    /// walk does, mirroring how the block path lays out its out-of-flow children
    /// via <see cref="CssBox.PerformLayout"/>. It descends only through in-flow,
    /// non-atomic inline boxes — the boxes FlowBox itself entered — because
    /// floats, atomic inlines (inline-block/-flex/-grid/-table), and block-level
    /// boxes run their own layout, which already resolves their out-of-flow
    /// descendants.
    /// </summary>
    private static void LayoutOutOfFlowInlineDescendants(ILayoutEnvironment g, CssBox ifcRoot)
    {
        foreach (var child in ifcRoot.Boxes)
            LayoutOutOfFlowInlineDescendantsCore(g, child);
    }

    private static void LayoutOutOfFlowInlineDescendantsCore(ILayoutEnvironment g, CssBox box)
    {
        if (box.Display == CssConstants.None)
            return;

        if (box.Position == CssConstants.Absolute || box.Position == CssConstants.Fixed)
        {
            // PerformLayout resolves the box's own size + inset position and
            // recurses into its subtree, so do not descend past it here.
            box.PerformLayout(g);
            return;
        }

        // Only plain inline (and anonymous inline) boxes are part of this inline
        // formatting context. Floats and atomic/block boxes establish their own
        // layout and must not be re-entered here. Anonymous *block* wrappers (from
        // a block-in-inline split) run their own CreateLineBoxes, so excluding
        // IsBlock avoids laying their out-of-flow descendants out twice.
        if (box.Float != CssConstants.None || box.IsBlock)
            return;

        if (box.Display == CssConstants.Inline || box.Kind == BoxKind.Anonymous)
        {
            foreach (var child in box.Boxes)
                LayoutOutOfFlowInlineDescendantsCore(g, child);
        }
    }

    public static void ApplyCellVerticalAlignment(ILayoutEnvironment g, CssBox cell)
    {
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(cell);

        if (cell.VerticalAlign == CssConstants.Top || cell.VerticalAlign == CssConstants.Baseline)
            return;

        double cellbot = cell.ClientBottom;
        double bottom = CssBoxHelper.GetMaximumBottom(cell, 0f);
        double dist = 0f;

        if (cell.VerticalAlign == CssConstants.Bottom)
        {
            dist = cellbot - bottom;
        }
        else if (cell.VerticalAlign == CssConstants.Middle)
        {
            dist = (cellbot - bottom) / 2;
        }

        // CSS Box Alignment §6.2: When align-content is 'normal' on a
        // table cell, vertical-align maps to safe alignment.  If the
        // content overflows the cell (dist < 0), safe alignment clamps
        // to start (top), preventing negative shifts.
        if (dist < 0 && (cell.AlignContent == null || cell.AlignContent == "normal"))
            dist = 0;

        foreach (CssBox b in cell.Boxes)
        {
            b.OffsetTop(dist);
        }
    }

    /// <summary>
    /// CSS Box Alignment §6.2: aligns a table cell's in-flow content along the
    /// block axis from the cell's explicit <c>align-content</c> (overriding the
    /// vertical-align mapping), falling back to <see cref="ApplyCellVerticalAlignment"/>
    /// when align-content is absent/<c>normal</c>. Used for cells whose final height
    /// is only known after the row-height pass — notably rowspan cells whose trailing
    /// rows are collapsed/empty, where the per-box align-content pass in
    /// <c>CssBox.PerformLayout</c> ran while the cell was still content-sized
    /// (zero free space) and therefore did nothing.
    /// </summary>
    public static void ApplyCellContentAlignment(ILayoutEnvironment g, CssBox cell)
    {
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(cell);

        string ac = cell.AlignContent?.Trim() ?? string.Empty;
        if (ac.Length == 0 || ac.Equals(CssConstants.Normal, StringComparison.OrdinalIgnoreCase))
        {
            ApplyCellVerticalAlignment(g, cell);
            return;
        }

        bool isUnsafe = ac.StartsWith("unsafe ", StringComparison.OrdinalIgnoreCase);
        bool isSafe = ac.StartsWith("safe ", StringComparison.OrdinalIgnoreCase);
        string kw = (isUnsafe ? ac[7..] : isSafe ? ac[5..] : ac).Trim().ToLowerInvariant();

        double free = cell.ClientBottom - CssBoxHelper.GetMaximumBottom(cell, 0f);
        double shift = kw switch
        {
            "center" or "space-around" or "space-evenly" => free / 2,
            "end" or "flex-end" => free,
            // start / flex-start / baseline / space-between / unknown → start edge.
            _ => 0,
        };

        // CSS Box Alignment §5.3: overflow alignment defaults to 'safe' (clamp to
        // the start edge) unless 'unsafe' is requested.
        if (!isUnsafe && shift < 0)
            shift = 0;

        if (Math.Abs(shift) <= 0.5)
            return;

        foreach (CssBox b in cell.Boxes)
        {
            if (b.Position == CssConstants.Absolute || b.Position == CssConstants.Fixed)
                continue;

            if (b.Display == CssConstants.None)
                continue;

            b.OffsetTop(shift);
        }
    }

    /// <summary>
    /// The left edge available to a line of <paramref name="blockbox"/> at <paramref name="top"/>,
    /// which is <paramref name="contentLeft"/> unless a left float in the block formatting context
    /// reaches that line (CSS2.1 §9.5).
    /// </summary>
    private static double BandLeftAt(CssBox blockbox, double top, double lineHeight, double contentLeft) =>
        blockbox.LineFloatBands is { IsEmpty: false } bands
            ? bands.LeftAt(top, lineHeight, contentLeft)
            : contentLeft;

    /// <summary>
    /// The right edge available to a line of <paramref name="blockbox"/> at <paramref name="top"/>.
    /// </summary>
    /// <remarks>
    /// A block still being measured at an unrestricted width (the shrink-to-fit sentinel that
    /// <see cref="CreateLineBoxes"/> narrows afterwards) has no meaningful right edge to subtract a
    /// float from, so it keeps the sentinel and wraps only where its content says.
    /// </remarks>
    private static double BandRightAt(CssBox blockbox, double top, double lineHeight, double contentRight) =>
        contentRight < 90999 && blockbox.LineFloatBands is { IsEmpty: false } bands
            ? bands.RightAt(top, lineHeight, contentRight)
            : contentRight;

    /// <summary>
    /// CSS2.1 §9.5: moves a line down past the floats beside it when <paramref name="needed"/>
    /// does not fit in the band there, and returns the y it settles at. Each step drops to the
    /// bottom of the shallowest float still in the way, so the walk is bounded by the float count.
    /// </summary>
    /// <remarks>
    /// The line is shifted down "until either some content fits or there are no more floats
    /// present", so what is wider than the block itself goes below the floats beside it, and
    /// overflows there as it would with no float at all. It stayed beside them, running on across
    /// the floats' side, where browsers put it below them.
    /// </remarks>
    private static double DropLineBelowNarrowBands(
        CssBox blockbox,
        double top,
        double lineHeight,
        double contentLeft,
        double contentRight,
        double needed)
    {
        if (needed <= 0 || contentRight >= 90999 || blockbox.LineFloatBands is not { IsEmpty: false } bands)
            return top;

        for (int step = 0; step < 64; step++)
        {
            double left = bands.LeftAt(top, lineHeight, contentLeft);
            double right = bands.RightAt(top, lineHeight, contentRight);

            if (right - left >= needed || (left <= contentLeft && right >= contentRight))
                return top;

            double next = bands.NextBandBottom(top, lineHeight);

            if (double.IsNaN(next) || next <= top)
                return top;

            top = next;
        }

        return top;
    }

    /// <summary>
    /// The top of what is on <paramref name="linebox"/>: its words, and the boxes placed on it
    /// whole, but not the padding and borders of the inline boxes around its words, which are not
    /// part of it (CSS2.1 §10.6.1), nor what is out of the flow.
    /// <see cref="double.MaxValue"/> for a line with nothing on it.
    /// </summary>
    private static double LineContentTop(CssLineBox linebox, CssBox blockBox)
    {
        double top = double.MaxValue;

        foreach (var rect in linebox.Rectangles)
        {
            if (IsInAbsposSubtree(rect.Key, blockBox) || rect.Key.IsInlineNonReplaced)
                continue;

            top = Math.Min(top, rect.Value.Top);
        }

        foreach (var word in linebox.Words)
        {
            if (IsInAbsposSubtree(word.OwnerBox, blockBox))
                continue;

            top = Math.Min(top, word.IsImage ? word.Top - ImageWordMarginTop(word) : WordLayoutTop(word));
        }

        // The strut of an inline box holding no text on the line, which its words would otherwise
        // measure.
        if (linebox.Baseline is double baseline)
        {
            foreach (var box in StrutOnlyInlineBoxes(linebox))
            {
                if (LineBoxAlignedRoot(box, blockBox) == null)
                    top = Math.Min(top, StrutOnlyExtent(box, baseline).Top);
            }
        }

        return top;
    }

    /// <summary>
    /// Moves everything on <paramref name="linebox"/> down by <paramref name="shift"/>: its words,
    /// its rectangles, and the boxes placed on it whole with what is in them.
    /// </summary>
    private static void ShiftLineBox(CssLineBox linebox, double shift)
    {
        var keys = new List<CssBox>(linebox.Rectangles.Keys);
        foreach (var box in keys)
            ShiftOnLine(linebox, box, shift);

        foreach (var word in linebox.Words)
            word.Top += shift;

        if (linebox.Baseline is double baseline)
            linebox.Baseline = baseline + shift;
    }

    /// <summary>
    /// Moves <paramref name="box"/>'s rectangle on <paramref name="linebox"/> down by
    /// <paramref name="shift"/>, and the box with what is in it when the flow placed it on the line
    /// whole; its words on the line are the caller's to move.
    /// </summary>
    private static void ShiftOnLine(CssLineBox linebox, CssBox box, double shift)
    {
        var r = linebox.Rectangles[box];
        linebox.Rectangles[box] = new RectangleF(r.X, (float)(r.Y + shift), r.Width, r.Height);

        // An inline-block, or an inline flex or grid container, that the flow placed on
        // this line whole moves with everything in it, as SetBaseLine moves one: what it
        // holds is positioned absolutely, on lines and in blocks of its own. Moving its
        // Location alone left that content behind, and adding the shift to ActualBottom
        // as well, which is the Location plus the height, made the box taller by the
        // shift. An inline flex or grid container was not moved at all, only its
        // rectangle on this line.
        //
        // An inline-block holding words of its own, as a ::before with
        // `display: inline-block` does, and an image, which SetBaseLine places with its
        // word, move their own box alone: their words are on these lines, and are moved
        // with the line's. The image's box was left where it was, and it was drawn and measured
        // above its word; the inline-block's was moved and made taller by the shift too, its
        // ActualBottom being its Location plus its height.
        if (box.Words.Count == 0
            && box.Display is CssConstants.InlineBlock or "inline-flex" or "inline-grid")
        {
            box.OffsetTop(shift);
        }
        else if (box.Display == CssConstants.InlineBlock || box.IsImage)
        {
            box.Location = new PointF(box.Location.X, (float)(box.Location.Y + shift));
        }

        // Update the box's own Rectangles copy (assigned
        // earlier by AssignRectanglesToBoxes).
        if (box.Rectangles.ContainsKey(linebox))
            box.Rectangles[linebox] = linebox.Rectangles[box];
    }

    /// <summary>
    /// Whether anything is on <paramref name="line"/> yet: a word, or a box placed on it whole, as
    /// an inline-block is.
    /// </summary>
    private static bool LineHoldsContent(CssLineBox line) => line.Words.Count > 0 || line.Rectangles.Count > 0;

    /// <summary>
    /// How much of <paramref name="line"/>'s width what is on it takes, up to <paramref name="curx"/>:
    /// from its first word, or the margin edge of its first box placed whole, to there.
    /// </summary>
    private static double LineContentWidth(CssLineBox line, double curx)
    {
        double left = double.MaxValue;

        foreach (var word in line.Words)
            left = Math.Min(left, word.Left);

        foreach (var (box, rect) in line.Rectangles)
            left = Math.Min(left, rect.X - box.ActualMarginLeft);

        return left == double.MaxValue ? 0 : Math.Max(0, curx - left);
    }

    /// <summary>
    /// Begins a line at <paramref name="top"/> and ends <paramref name="previous"/> at
    /// <paramref name="bottom"/>, the lowest the flow has reached on it.
    /// </summary>
    private static CssLineBox NextLine(CssBox blockbox, CssLineBox previous, double bottom, double top)
    {
        previous.FlowBottom = bottom;
        return new CssLineBox(blockbox) { FlowTop = top };
    }

    private static void FlowBox(ILayoutEnvironment g, CssBox blockbox, CssBox box, double limitRight, double linespacing, double startx, ref CssLineBox line, ref double curx, ref double cury, ref double maxRight, ref double maxbottom)
    {
        var startX = curx;
        var startY = cury;
        box.FirstHostingLineBox = line;
        var localCurx = curx;
        var localMaxRight = maxRight;
        var localmaxbottom = maxbottom;

        foreach (CssBox b in box.Boxes)
        {
            // CSS2.1 §9.2.4: display:none elements generate no boxes and
            // must not participate in layout — skip them entirely.
            if (b.Display == CssConstants.None)
                continue;

            // CSS2.1 §9.5: Floated elements are out of normal flow and
            // must not participate in the inline formatting context.
            // Their positioning is handled separately in PerformLayoutImp, on the line the content
            // before them has reached (§9.5.1): note which line that is, and how much of it the
            // content takes.
            if (b.Float != CssConstants.None)
            {
                if (!InlineFloats.IsFlexOrGridContainer(blockbox))
                    b.InlineFloatPlacement = new InlineFloatPlacement(line, line.FlowTop ?? cury, LineContentWidth(line, curx));

                continue;
            }

            // CSS2.1 §9.6.1: Absolutely and fixed positioned elements are
            // out of normal flow.  Save the current flow state so we can
            // restore it after laying out the child — its words must not
            // shift subsequent siblings or inflate the parent's content
            // height.
            bool isAbsposChild = b.Position == CssConstants.Absolute
                || b.Position == CssConstants.Fixed;

            // CSS2.1 §10.3.7 / §10.6.4: record the out-of-flow child's static
            // position — the inline cursor it would occupy in this formatting
            // context — so its own block layout can honour it for auto-inset
            // axes (see CssBoxProperties.InlineStaticPosition).
            if (isAbsposChild)
                b.InlineStaticPosition = new PointF((float)curx, (float)cury);

            double childSaveCurx = curx;
            double childSaveCury = cury;
            double childSaveMaxRight = maxRight;
            double childSaveMaxBottom = maxbottom;
            CssLineBox childSaveLine = line;

            double leftspacing = !isAbsposChild ? b.ActualMarginLeft + b.ActualBorderLeftWidth + b.ActualPaddingLeft : 0;
            double rightspacing = !isAbsposChild ? b.ActualMarginRight + b.ActualBorderRightWidth + b.ActualPaddingRight : 0;

            b.RectanglesReset();
            b.MeasureWordsSize(g);

            curx += leftspacing;

            if (b.Words.Count > 0)
            {
                bool wrapNoWrapBox = false;

                // A box that does not wrap goes to the next line whole when it does not fit, where
                // the line may break before it (MayBreakBefore): not after what comes before it in
                // a box that does not wrap either, as text and images in a `white-space: nowrap`
                // block do.
                if (b.WhiteSpace == CssConstants.NoWrap && curx > startx && MayBreakBefore(b, blockbox))
                {
                    var boxRight = curx;
                    foreach (var word in b.Words)
                        boxRight += word.FullWidth;

                    if (boxRight > limitRight)
                        wrapNoWrapBox = true;
                }

                if (LayoutBoxUtils.IsBoxHasWhitespace(b))
                    curx += box.ActualWordSpacing;

                foreach (var word in b.Words)
                {
                    // CSS2.1 §10.8: Every line box has a minimum height
                    // from the block container's line-height (the "strut").
                    // When ActualLineHeight is 0, the minimum comes from the
                    // font's height, which ILayoutFont gives in CSS pixels.
                    double boxLineHeight = box.ActualLineHeight > 0
                        ? box.ActualLineHeight
                        : box.ActualFont.Height;

                    // The word may yet wrap, so the line it would start on is only certain to be
                    // as tall as the block's line height, which every line has; the rest goes to
                    // the line the word lands on, below. All of it here left the line before a
                    // wrapped word that tall too: a link with a taller line height whose first
                    // word went to the next line made the line above it as tall.
                    double blockLineHeight = blockbox.ActualLineHeight > 0
                        ? blockbox.ActualLineHeight
                        : blockbox.ActualFont.Height;
                    double lineHeightBeforeWrap = Math.Min(boxLineHeight, blockLineHeight);

                    // CSS2.1 §10.8: the line the word lands on is as tall as the line height of
                    // the box it is in, and of the box holding it where that is taller: a span
                    // holding its own text, with a taller line height than its parent's. A
                    // replaced box's line height does not apply to it.
                    double lineHeightAfterWrap = word.IsImage
                        ? boxLineHeight
                        : Math.Max(boxLineHeight, b.ActualLineHeight);

                    if (maxbottom - cury < lineHeightBeforeWrap)
                        maxbottom += lineHeightBeforeWrap - (maxbottom - cury);

                    // CSS2.1 §10.8: The "strut" — each line box has a minimum
                    // height from the block container's font and line-height.
                    // For replaced inline elements (images), apply the block
                    // container's strut so that baseline alignment pushes the
                    // image down when the font is larger than the image.
                    double strutHeight = 0;

                    // CSS2.1 §10.8.1: it is the *margin* box of an inline replaced element that
                    // sits on the baseline, so its vertical margins take part in the line the
                    // same way its horizontal ones take part in the advance. They were dropped —
                    // the image was placed at the line's top and the line closed at the image's
                    // own bottom — so a thumbnail with `margin: 3px` (MediaWiki puts that on
                    // every one) sat 3px too high in a wrapper 6px too short.
                    double imageMarginTop = word.IsImage ? ImageWordMarginTop(word) : 0;
                    double imageMarginBottom = word.IsImage ? ImageWordMarginBottom(word) : 0;
                    double imageMarginBoxHeight = word.Height + imageMarginTop + imageMarginBottom;

                    if (word.IsImage)
                    {
                        strutHeight = blockbox.ActualLineHeight;

                        if (strutHeight <= 0)
                            strutHeight = blockbox.ActualFont.Height;

                        if (maxbottom - cury < strutHeight)
                            maxbottom += strutHeight - (maxbottom - cury);
                    }

                    // CSS Text §5.1: a line box may not be broken before its
                    // first inline content. An unbreakable word wider than the
                    // line stays on the (otherwise empty) line and overflows;
                    // it must not be pushed to a phantom second line, which
                    // would double the block's height (WPT max-height-109: a
                    // 3000px Ahem word in a 200px line was wrapping to a second
                    // line, so #red-parent grew to ~400px and red showed above
                    // the green). Words are added to the line via
                    // ReportExistanceOf below, and a box placed on it whole, as
                    // an inline-block is, is in its rectangles: with neither,
                    // this word is the first on the current line. Such a box
                    // did not count, so a word after an inline-block that it
                    // did not fit beside never wrapped and ran on past the
                    // block's edge: in a 100px block, "abcdefgh" after a 90px
                    // inline-block stayed 90px in, where browsers put it on
                    // the next line.
                    bool lineHasContent = LineHoldsContent(line);
                    double lineRight = BandRightAt(blockbox, cury, boxLineHeight, limitRight);
                    if ((b.WhiteSpace != CssConstants.NoWrap && b.WhiteSpace != CssConstants.Pre && curx + word.Width + rightspacing > lineRight
                         && (b.WhiteSpace != CssConstants.PreWrap || !word.IsSpaces)
                         && (b.WhiteSpace != CssConstants.PreLine || !word.IsSpaces)
                         && lineHasContent) || word.IsLineBreak || wrapNoWrapBox)
                    {
                        wrapNoWrapBox = false;
                        cury = maxbottom + linespacing;

                        // CSS2.1 §9.5: the new line sits between the floats that reach it, and is
                        // pushed below them when what has to go on it does not fit even in the
                        // empty band. Both edges move, so the left one is re-read after the drop.
                        cury = DropLineBelowNarrowBands(
                            blockbox, cury, boxLineHeight, startx, limitRight,
                            word.Width + rightspacing);

                        curx = BandLeftAt(blockbox, cury, boxLineHeight, startx);

                        // handle if line is wrapped for the first text element where parent has left margin\padding
                        if (b == box.Boxes[0] && !word.IsLineBreak && (word == b.Words[0] || (box.ParentBox != null && box.ParentBox.IsBlock)))
                            curx += box.ActualMarginLeft + box.ActualBorderLeftWidth + box.ActualPaddingLeft;

                        line = NextLine(blockbox, line, maxbottom, cury);

                        if (word.IsImage || word.Equals(b.FirstWord))
                            curx += leftspacing;
                    }

                    // CSS2.1 §9.5: a line box beside floats too narrow for its first content is
                    // shifted down until the content fits or no float is beside it. Only a line
                    // that wrapped was: the first word on the block's first line, or after a
                    // <br>, stayed beside a float it did not fit beside and ran on across the
                    // float's side, where browsers put it below the float.
                    else if (!word.IsLineBreak && !word.IsSpaces && !lineHasContent
                        && curx + word.Width + rightspacing > lineRight)
                    {
                        double bandLeft = BandLeftAt(blockbox, cury, boxLineHeight, startx);
                        double dropped = DropLineBelowNarrowBands(
                            blockbox, cury, boxLineHeight, startx, limitRight,
                            curx - bandLeft + word.Width + rightspacing);

                        if (dropped > cury)
                        {
                            curx += BandLeftAt(blockbox, dropped, boxLineHeight, startx) - bandLeft;
                            cury = dropped;
                            line.FlowTop = cury;
                        }
                    }

                    if (maxbottom - cury < lineHeightAfterWrap)
                        maxbottom += lineHeightAfterWrap - (maxbottom - cury);

                    line.ReportExistanceOf(word);

                    word.Left = curx;

                    // CSS2.1 §10.8.1: Replaced inline elements (images) are
                    // baseline-aligned by default — the bottom of the replaced
                    // element sits on the baseline.  The baseline position
                    // within the strut is at the font's ascent from the top.
                    if (word.IsImage && strutHeight > imageMarginBoxHeight)
                    {
                        double fontHeight = blockbox.ActualFont.Height;
                        double baseline = fontHeight * TypicalAscentRatio;
                        word.Top = Math.Max(cury, cury + baseline - imageMarginBoxHeight) + imageMarginTop;
                    }
                    else
                    {
                        word.Top = cury + imageMarginTop;
                    }

                    if (!box.IsFixed)
                    {
                        word.BreakPage();
                    }

                    curx = word.Left + word.FullWidth;

                    maxRight = Math.Max(maxRight, word.Right);
                    maxbottom = Math.Max(maxbottom, InlineWordLineBoxBottom(word));

                    // CSS2.1 §10.8: a baseline-aligned inline replaced element
                    // (image) sits with its bottom on the baseline, so the line
                    // box still extends below it by the strut's below-baseline
                    // descent. InlineWordLineBoxBottom returns only the image
                    // bottom (the baseline); without adding that descent the next
                    // wrapped line starts too high and the error accumulates down
                    // the block (CSS2 visudet/replaced-elements-*).
                    if (word.IsImage
                        && (word.OwnerBox == null
                            || string.IsNullOrEmpty(word.OwnerBox.VerticalAlign)
                            || word.OwnerBox.VerticalAlign == CssConstants.Baseline))
                    {
                        maxbottom = Math.Max(maxbottom,
                            word.Bottom + imageMarginBottom + StrutDescent(blockbox));
                    }

                    if (b.Position == CssConstants.Absolute)
                    {
                        word.Left += box.ActualMarginLeft;
                        word.Top += box.ActualMarginTop;
                    }
                }
            }
            else
            {
                // Determine if this child should use inline-block sizing:
                // 1. Explicit display:inline-block
                // 2. display:inline-flex / inline-grid (inline-level flex/grid)
                // 3. Direct child of a flex/grid container (all children
                //    become flex/grid items with shrink-to-fit sizing per
                //    CSS Flexbox §4 / CSS Grid §6; since Broiler lacks a
                //    true flex/grid engine, use FlowInlineBlock as a
                //    reasonable approximation)
                bool useInlineBlockFlow = FlowsAsInlineBlock(b, box);

                if (useInlineBlockFlow)
                {
                    // CSS 2.1 §10.3.9/§10.6.6: Inline-block boxes are laid
                    // out as blocks internally, then placed atomically in
                    // the inline flow (like replaced inline elements).
                    FlowInlineBlock(g, blockbox, b, limitRight, linespacing, startx,
                        leftspacing, rightspacing,
                        ref line, ref curx, ref cury, ref maxRight, ref maxbottom);

                    // CSS Flexbox §9.4: flex-direction:column stacks items
                    // vertically — force a line break after each flex item so
                    // the next item starts on a new row.
                    if (box.Display is "flex" or "inline-flex" or "grid" or "inline-grid"
                        && box.FlexDirection is "column" or "column-reverse")
                    {
                        cury = maxbottom;
                        curx = startx;
                        line = NextLine(blockbox, line, maxbottom, cury);

                        // The item's right margin, border and padding end the line the item was
                        // on. The next line starts at the content edge, so they are not carried
                        // onto it. Adding them after the break put every item after a padded one
                        // that far to the right; a stretched item then overflowed the container by
                        // as much, and one as wide as the container no longer fit its line and
                        // wrapped a line's descent further down.
                        rightspacing = 0;
                    }
                }
                else
                {
                    // Block-level child inside inline flow: force a line break
                    // before and after the block (CSS2.1 §9.2.1.1 anonymous
                    // block boxes).  This ensures elements like <p> inside an
                    // inline <form> start on their own line.
                    //
                    // CSS2.1 §10.3.7/§10.6.4: An out-of-flow positioned child
                    // is laid out here only to establish its *static position*
                    // — the place a hypothetical inline box would occupy had
                    // the element been in flow.  It must therefore be placed at
                    // the current inline cursor, NOT forced onto its own line.
                    // (The surrounding flow state is restored after the child,
                    // so this break would be discarded anyway, but skipping it
                    // keeps the static position on the current line.)
                    if (b.IsBlock && !isAbsposChild)
                    {
                        if (curx > startx || maxbottom > cury)
                        {
                            cury = maxbottom;
                            curx = startx;
                            line = NextLine(blockbox, line, maxbottom, cury);
                        }
                    }

                    FlowBox(g, blockbox, b, limitRight, linespacing, startx, ref line, ref curx, ref cury, ref maxRight, ref maxbottom);

                    if (b.IsBlock && !isAbsposChild)
                    {
                        cury = maxbottom;
                        curx = startx;
                        line = NextLine(blockbox, line, maxbottom, cury);
                    }
                }
            }

            curx += rightspacing;

            // CSS2.1 §9.6.1: Restore flow state after an absolutely/fixed
            // positioned child so it does not affect siblings or parent
            // content height.  This includes the current line (`line`) and
            // block-axis cursor (`cury`): a block-level out-of-flow child
            // triggers the block line-break logic above to establish its
            // static position, but that break must not push subsequent
            // in-flow inline siblings onto a new line.
            if (isAbsposChild)
            {
                curx = childSaveCurx;
                cury = childSaveCury;
                maxRight = childSaveMaxRight;
                maxbottom = childSaveMaxBottom;
                line = childSaveLine;
            }
        }

        // CSS 2.1 §10.3.1, §10.6.1: `width` and `height` do not apply to an inline, non-replaced
        // box, which is as wide as what it holds and as tall as its line height; only a replaced
        // one, an <svg> or a <canvas> laid out inline, takes them. Every inline box took them here:
        // the empty <source width="84" height="29"> of a <picture> took 84px of its line, a span
        // holding "xy" with `width: 84px` was 84px wide, and a line wrapping after a span with
        // `height: 50px` started 50px down, where browsers give the first nothing, the second the
        // width of "xy", and start the line 20px down.
        bool takesSize = !box.IsInlineNonReplaced || box.IsReplaced;

        // handle height setting
        if (takesSize && maxbottom - startY < box.ActualHeight)
            maxbottom += box.ActualHeight - (maxbottom - startY);

        // handle width setting
        // CSS 2.1 §10.3.9: inline-block boxes handle their own sizing in
        // FlowInlineBlock — do not register them here when processing
        // their own internal content (box == blockbox).
        if (takesSize && box.IsInline && box != blockbox && 0 <= curx - startX && curx - startX < box.ActualWidth)
        {
            // hack for actual width handling
            curx += box.ActualWidth - (curx - startX);
            line.Rectangles.Add(box, new RectangleF((float)startX, (float)startY, (float)box.ActualWidth, (float)box.ActualHeight));
        }
        else if (box.IsInline && box != blockbox && !line.Rectangles.ContainsKey(box)
            && !InlineBoxHasInFlowContent(box))
        {
            // CSS Inline Layout §3 (invisible line boxes): an inline box whose only content
            // is out of flow — or which is empty — advances the inline cursor by nothing, so
            // the branch above (which pads a partially-filled inline out to its ActualWidth)
            // never records it and its position defaults to the document origin. Record its
            // line position on Location rather than as a line rectangle: a rectangle would make
            // the (otherwise empty, invisible) line box count as content and gain strut height,
            // shifting following blocks. Location leaves the line invisible while still giving
            // the box a real origin for getBoundingClientRect and, crucially, for an absolutely
            // positioned descendant that uses this inline as its containing block (which reads
            // Location when the inline has no line rectangles). WPT
            // css/css-inline/empty-span-scroll: an empty relative <span> holds the
            // scrollIntoView target, which must land on the line, not at the origin.
            box.Location = new PointF((float)startX, (float)startY);
        }

        // handle box that is only a whitespace
        if (box.Text.Length > 0 && box.Text.Span.IsWhiteSpace() && !box.IsImage && box.IsInline && box.Boxes.Count == 0 && box.Words.Count == 0)
            curx += box.ActualWordSpacing;

        // hack to support specific absolute position elements
        if (box.Position == CssConstants.Absolute)
        {
            curx = localCurx;
            maxRight = localMaxRight;
            maxbottom = localmaxbottom;

            // AdjustAbsolutePosition shifts the box's words by its own left/top so
            // an abspos child laid out at the *parent's* inline cursor (its static
            // position) lands at its CSS offset. But when the box flows its OWN
            // content (box == blockbox) AND PerformLayoutImp already advanced its
            // Location to the final left/top offset (AbsposLocationFinalized), the
            // words were flowed from startx = box.Location.X = the final origin, so
            // re-adding left/top would double the inset — painting content at ~2× the
            // offset while the border/background paint at the correct origin (auto-
            // sized abspos with inline content, e.g. css-anchor-position anchored
            // labels, issue #1163). Boxes that keep their static Location (e.g.
            // native form controls) still rely on the adjustment.
            //
            // Nor is a box that flows its own content moved when its `top` and `left` are both
            // auto: PerformLayoutImp put it at its static position with its margins, and its
            // words were flowed from there, so AdjustAbsolutePosition, which has no offset to
            // add, added its margins a second time. A box with `margin-top: 10px` drew its text
            // 10px below its content top; one with a negative margin drew it above, which the
            // lines, measured from where the flow put them, took for content raised above them.
            if (!(box == blockbox && (box.AbsposLocationFinalized || HasAutoTopAndLeft(box))))
                AdjustAbsolutePosition(box, 0, 0);
        }

        box.LastHostingLineBox = line;
    }

    /// <summary>
    /// True when an inline box contributes in-flow content to its line — its own words,
    /// or an in-flow child box (a nested inline, an inline-block, or a block-in-inline).
    /// Children that are out of flow (<c>display:none</c>, floated, or absolutely/fixed
    /// positioned) do not count: an inline whose only content is such a child is an empty
    /// (invisible) line box that still occupies a zero-width position on its line.
    /// </summary>
    private static bool InlineBoxHasInFlowContent(CssBox box)
    {
        if (box.Words.Count > 0)
            return true;

        foreach (var child in box.Boxes)
        {
            if (child.Display == CssConstants.None
                || child.Float != CssConstants.None
                || child.Position is CssConstants.Absolute or CssConstants.Fixed)
                continue;

            // An anonymous inline text run that collapsed to nothing — whitespace-only or
            // empty, with no words and no children of its own — contributes no rectangle to
            // the line, so it must not mask an otherwise-invisible inline box. Without this,
            // the whitespace an author leaves around an out-of-flow child (e.g. the newlines
            // between a relative <span> and its abspos target) counts as content, and the
            // empty-inline branch in FlowBox never records the span's flow position — it
            // defaults to the document origin, so the target (and scrollIntoView to it) lands
            // at 0 instead of on its line. WPT css/css-inline/empty-span-scroll.
            if (child.HtmlTag == null
                && child.Words.Count == 0
                && child.Boxes.Count == 0
                && (child.Text.IsEmpty || child.Text.Span.IsWhiteSpace()))
                continue;

            return true;
        }
        return false;
    }

    /// <summary>
    /// True when a grid container has at least one in-flow grid item that is an
    /// inline-level replaced element (an <c>&lt;img&gt;</c>). Such an item's word
    /// is positioned by the container's line-box flow, not by the item's own
    /// <see cref="CssBox.PerformLayout"/>, so the per-item block layout path in
    /// <see cref="FlowInlineBlock"/> would leave it orphaned and unpainted. When
    /// this is true the grid is instead laid out through the inline formatting
    /// context (<see cref="CreateLineBoxes"/>), matching the block-level grid path.
    /// </summary>
    private static bool GridHasInlineReplacedItem(CssBox grid)
    {
        foreach (var child in grid.Boxes)
        {
            if (child.Display == CssConstants.None
                || child.Position is CssConstants.Absolute or CssConstants.Fixed)
                continue;

            if (child.IsImage && child.IsInline)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Whether <paramref name="box"/>, or a flex item it is in, is an item its column flex
    /// container will lay out again at its stretched width, so that this layout of the box is a
    /// first one the stretch redoes.
    /// </summary>
    /// <remarks>
    /// Inside such an item as well as for the item itself, the column passes wait for the layout
    /// that counts. They stretch the items of the container they run for, and each stretch lays an
    /// item out again; run in a first layout too, they doubled the work at every stretched level of
    /// a nest, 65 layouts for twelve levels aligned in turn <c>flex-start</c> and <c>stretch</c>,
    /// and eight times as many for every six more. The stretch sets the item's width while it lays
    /// it out again, so in that layout the item no longer reads as one to stretch.
    /// </remarks>
    private static bool IsLaidOutAgainByAStretch(CssBox box)
    {
        for (var item = box; item.ParentBox is { } container; item = container)
        {
            if (container.WillStretchColumnItem(item))
                return true;
        }

        return false;
    }

    /// <summary>
    /// CSS 2.1 §10.3.9 / §10.6.6: Lay out an inline-block box as a
    /// block internally, then place it atomically in the inline flow.
    /// The inline-block establishes a new block formatting context for
    /// its children while participating in the parent's inline
    /// formatting context as a single opaque box.
    /// </summary>
    private static void FlowInlineBlock(ILayoutEnvironment g, CssBox blockbox, CssBox b,
        double limitRight, double linespacing, double startx,
        double leftspacing, double rightspacing,
        ref CssLineBox line, ref double curx, ref double cury,
        ref double maxRight, ref double maxbottom)
    {
        // Compute the container content width for resolving percentage and
        // em-based lengths on the inline-block.
        double containerWidth = blockbox.Size.Width
            - blockbox.ActualPaddingLeft - blockbox.ActualPaddingRight
            - blockbox.ActualBorderLeftWidth - blockbox.ActualBorderRightWidth;

        // CSS Images 3 §4 / CSS2.1 §10.4: a replaced atomic inline — a <canvas>, whose natural size
        // is its width/height content attributes — takes an auto axis from its natural size rather
        // than shrinking to fit its (absent) content, and keeps the two axes tied by the natural
        // ratio while min-*/max-* clamp them. Sized in one step below; the per-axis clamps that
        // follow are skipped for it.
        // CSS Containment 2 §3.2: size containment leaves a replaced element with no natural
        // dimensions and no natural ratio, so a contained <canvas>/<svg> is sized like any other
        // contained box rather than from its bitmap.
        var intrinsic = b.AppliesSizeContainment ? null : b.IntrinsicReplacedSize;
        bool isReplaced = intrinsic is { Width: > 0, Height: > 0 };

        // --- Compute inline-block content width ---
        // A replaced box settles both axes at once, constraints and all, so the per-axis clamps
        // below are skipped for it and the height branch reads the result back.
        double replacedContentHeight = 0;
        double ibContentWidth;
        if (isReplaced)
        {
            b.ResolveReplacedContentSize(intrinsic.Value, containerWidth, out ibContentWidth, out replacedContentHeight);
        }
        else if (b.Width != CssConstants.Auto && !string.IsNullOrEmpty(b.Width))
        {
            ibContentWidth = CssLengthParser.ParseLength(b.Width, containerWidth, b.GetEmHeight());
            if (b.BoxSizing.Equals("border-box", StringComparison.OrdinalIgnoreCase))
            {
                ibContentWidth -= b.ActualBorderLeftWidth + b.ActualBorderRightWidth
                    + b.ActualPaddingLeft + b.ActualPaddingRight;
                if (ibContentWidth < 0)
                    ibContentWidth = 0;
            }
        }
        else
        {
            // CSS 2.1 §10.3.9: auto-width inline-block uses shrink-to-fit.
            // Measure descendant words for intrinsic width computation.
            MeasureDescendantWords(g, b);
            b.GetMinMaxWidth(out double prefMin, out double prefMax);
            if (double.IsNaN(prefMin)) prefMin = 0;
            if (double.IsNaN(prefMax)) prefMax = 0;
            // GetMinMaxWidth returns border-box widths (content + padding +
            // border).  Convert to content-only widths so the shrink-to-fit
            // calculation matches the content-only `available` value and the
            // padding/border added back at ibBoxWidth below.
            double ownPaddingBorder = b.ActualBorderLeftWidth + b.ActualBorderRightWidth
                + b.ActualPaddingLeft + b.ActualPaddingRight;
            prefMin = Math.Max(0, prefMin - ownPaddingBorder);
            prefMax = Math.Max(0, prefMax - ownPaddingBorder);
            // curx already includes this box's left margin, border and padding (leftspacing), and
            // rightspacing its right ones, so what is left of the line is a content width already.
            // Taking the border and padding off again left every inline-block that has to fit the
            // space it is given — a flex column's item, which that column sizes to the item — that
            // much narrower than its content, and its text wrapped.
            double available = Math.Max(0, limitRight - curx - rightspacing);
            ibContentWidth = Math.Min(Math.Max(prefMin, available), prefMax);
        }

        // CSS 2.1 §10.4: Apply min-width constraint.
        // min-width takes priority over computed width (including
        // shrink-to-fit for auto-width inline-blocks).
        if (!isReplaced && b.MinWidth != "0" && !string.IsNullOrEmpty(b.MinWidth))
        {
            double minW = CssLengthParser.ParseLength(b.MinWidth, containerWidth, b.GetEmHeight());
            double minContentW = b.BoxSizing.Equals("border-box", StringComparison.OrdinalIgnoreCase)
                ? minW - b.ActualBorderLeftWidth - b.ActualBorderRightWidth
                    - b.ActualPaddingLeft - b.ActualPaddingRight
                : minW;

            if (minContentW > ibContentWidth)
                ibContentWidth = minContentW;
        }

        // CSS 2.1 §10.4: Apply max-width constraint.
        // max-width limits the computed width from above.  When both
        // min-width and max-width are specified, min-width wins if it
        // exceeds max-width (CSS2.1 §10.4).
        if (!isReplaced && b.MaxWidth != "none" && !string.IsNullOrEmpty(b.MaxWidth))
        {
            double maxW = CssLengthParser.ParseLength(b.MaxWidth, containerWidth, b.GetEmHeight());
            double maxContentW = b.BoxSizing.Equals("border-box", StringComparison.OrdinalIgnoreCase)
                ? maxW - b.ActualBorderLeftWidth - b.ActualBorderRightWidth
                    - b.ActualPaddingLeft - b.ActualPaddingRight
                : maxW;
            if (maxContentW < ibContentWidth)
                ibContentWidth = maxContentW;
        }

        double ibBoxWidth = ibContentWidth
            + b.ActualBorderLeftWidth + b.ActualBorderRightWidth
            + b.ActualPaddingLeft + b.ActualPaddingRight;

        // --- Line wrap check ---
        // Total inline extent: margin-left + box-width + margin-right.
        // curx already includes leftspacing (margin+border+padding), so the
        // border-box left edge is at curx - border - padding.
        double ibBorderLeft = curx - b.ActualBorderLeftWidth - b.ActualPaddingLeft;
        double edgeBeforeBox = ibBorderLeft - b.ActualMarginLeft;
        double totalExtent = b.ActualMarginLeft + ibBoxWidth + b.ActualMarginRight;

        // CSS2.1 §9.5: the line is as wide as the band the floats beside it leave, and a line too
        // narrow for its first content is shifted down until the content fits or no float is
        // beside it. The box was checked against the block's whole width and, first on its line,
        // was never moved: an inline-block too wide for the room beside a left float went to the
        // block's left edge, over the float, where browsers put it below the float.
        double lineHeight = blockbox.ActualLineHeight > 0 ? blockbox.ActualLineHeight : blockbox.ActualFont.Height;
        double bandLeft = BandLeftAt(blockbox, cury, lineHeight, startx);

        if (edgeBeforeBox + totalExtent > BandRightAt(blockbox, cury, lineHeight, limitRight))
        {
            if (edgeBeforeBox > bandLeft)
            {
                // The strut's descent below a box on the line it leaves is already in maxbottom (see
                // below). It was added here too, below any line an inline-block wrapped from, which
                // put a row of inline-blocks holding text a descent lower than browsers put it.
                //
                // Where the line may not break before the box (MayBreakBefore), it overflows the
                // line instead: an 84px and a 20px inline-block in a 25px `white-space: nowrap`
                // block went on two lines, where browsers keep them on one.
                if (MayBreakBefore(b, blockbox))
                {
                    cury = DropLineBelowNarrowBands(
                        blockbox, maxbottom + linespacing, lineHeight, startx, limitRight, totalExtent);
                    curx = BandLeftAt(blockbox, cury, lineHeight, startx) + leftspacing;
                    line = NextLine(blockbox, line, maxbottom, cury);
                }
            }
            else
            {
                double dropped = DropLineBelowNarrowBands(
                    blockbox, cury, lineHeight, startx, limitRight, edgeBeforeBox - bandLeft + totalExtent);

                if (dropped > cury)
                {
                    curx += BandLeftAt(blockbox, dropped, lineHeight, startx) - bandLeft;
                    cury = dropped;
                    line.FlowTop = cury;
                }
            }

            ibBorderLeft = curx - b.ActualBorderLeftWidth - b.ActualPaddingLeft;
        }

        // --- Position and size the inline-block ---
        b.Location = new PointF((float)ibBorderLeft, (float)(cury + b.ActualMarginTop));
        b.Size = new SizeF((float)ibBoxWidth, 0);
        b.ActualBottom = b.Location.Y;

        // --- Lay out children inside the inline-block ---
        // CSS Flexbox §5.4: a flex container lays its items out, and paints them, in order-modified
        // document order, and a grid container places its items in it. LayoutBlockChildren sorts a
        // block-level container's children into it before laying them out; a container laid out
        // here was left in document order, so `order` did nothing in an inline-flex or inline-grid
        // container, or in a flex or grid container that is itself an item laid out this way.
        if (b.Display is "flex" or "inline-flex" or "grid" or "inline-grid")
            b.ApplyOrderModifiedDocumentOrder();

        // `inline-flex` is a flex container in every way that matters here — only its outer display
        // differs, and this method is precisely the path an inline-level box arrives on. Testing
        // for `flex` alone meant an inline-flex container never ran the flex algorithm at all: its
        // items were flowed as ordinary inline-block content, so nothing sized them from the
        // container's cross size and nothing distributed its free space. Every
        // css-flexbox/aspect-ratio-intrinsic-size test builds its case on an `inline-flex` box.
        if (b.Display is "flex" or "inline-flex" && b.IsRowFlexContainer() && HasBlockLevelFlexItems(b))
        {
            b.PerformFlexRowLayout(g);
        }
        else if (b.Display is "grid" or "inline-grid")
        {
            // CSS Grid Level 1: Grid items should be laid out as blocks
            // (not inline-blocks) so that width:auto stretches to the
            // column width.  Use the block layout path, then apply grid
            // stacking or auto-placement to fix positioning.
            //
            // Exception: an inline replaced grid item (an <img>) is neither sized
            // nor painted by its own PerformLayout — a replaced inline element's
            // word is positioned by the *container's* line-box flow, which the
            // block path never runs, leaving the word orphaned (no container line
            // box owns it) so the image renders blank. Route such a grid through
            // the inline formatting context instead — the same CreateLineBoxes
            // path the block-level `display:grid` case in CssBox.PerformLayoutImp
            // uses — so the image's word lands in a container line box and is
            // painted; ApplyGridLayoutAfterInline then re-flows the items into
            // their tracks and re-stretches auto-width items. (WPT
            // css-grid/grid-items/grid-minimum-size-grid-items-021 and the other
            // inline-grid + <img> tests rendered blank before this.) Narrowed to
            // grids that actually contain such an item so every other grid keeps
            // its existing block-layout path.
            if (GridHasInlineReplacedItem(b))
            {
                CreateLineBoxes(g, b);
            }
            else
            {
                foreach (var child in b.Boxes)
                    child.PerformLayout(g);

                double childMaxBottom = b.Location.Y;
                foreach (var child in b.Boxes)
                    childMaxBottom = Math.Max(childMaxBottom, child.ActualBottom);

                b.ActualBottom = childMaxBottom;
            }

            b.ApplyGridLayoutAfterInline();
        }
        else if (b.Display == CssConstants.Table || b.Display == CssConstants.InlineTable)
        {
            // A table laid out through this atomic-inline path (an `inline-table`,
            // or a `display:table` blockified into a flex/grid item, which
            // ContainsInlinesOnly routes here) must run the table formatting
            // algorithm — its row/row-group/cell/caption boxes have no standalone
            // block layout. Without this the table's children were laid out
            // individually as blocks, so the rows and cells (and their text) were
            // never positioned and a `<table>` grid item rendered empty. (WPT
            // css-grid/table-grid-item-dynamic-002.)
            CssLayoutEngineTable.PerformLayout(g, b, b.BaseUrl);
        }
        else if (LayoutBoxUtils.ContainsInlinesOnly(b) || InlineContentWithBrsOnly(b))
        {
            // Inline-block content that is inline runs interrupted only by <br>
            // line breaks is an inline formatting context: lay it out with line
            // boxes so the breaks split it into lines. A <br> computes to a
            // block-level box in Broiler, which would otherwise route this to the
            // block-children branch below — that branch lays each anonymous inline
            // child out via its own PerformLayout (which sets no block height for
            // an inline box), collapsing the inline-block to zero height. This is
            // the shape of a multi-line grid item (e.g. `X<br>X`), which the
            // css-grid check-layout reference tests use throughout.
            CreateLineBoxes(g, b);

            // CSS2.1 §9.5: the floats among its children are laid out as a block container lays
            // out its own, and the lines flowed again beside them; and, as it establishes a
            // formatting context of its own, the box contains them (§10.6.7). Neither happened
            // here: a float in an inline-block, or in a flex item laid out as one, stayed 0×0 at
            // the page's origin, painted nowhere, and the box ended at its lines.
            b.LayOutFloatedChildren(g);
            b.ContainDescendantFloats();

            // An inline-flex column container lands here too, and line layout only stacks its
            // items. A block-level column container gets the column passes after that; this
            // route stopped short of them, so the items kept their content width: not
            // stretched, not aligned, not reversed under column-reverse and not flexed.
            //
            // A flex item that is itself a column container comes this way as well. When its
            // container stretches it, it lays it out again, through block layout and so with the
            // passes, and running them here too would lay out each level of nested column
            // containers twice, a twelve-level nest 6,145 times instead of 26. Every other one
            // runs them here, as nothing else will: one its container aligns `flex-start` or
            // `center`, one with a width or an `auto` margin of its own, and one in a grid, which
            // stretches an item without laying it out again. Those had no row gaps, no flexing,
            // no alignment of their items and no `column-reverse`.
            if (b.IsColumnFlexContainer() && (b.Display == "inline-flex" || !IsLaidOutAgainByAStretch(b)))
                b.FinishFlexColumnLayout(g);
        }
        else if (b.Boxes.Count > 0)
        {
            foreach (var child in b.Boxes)
                child.PerformLayout(g);

            // An inline-block, and a flex or grid item, establishes a block formatting context
            // (CSS2.1 §9.4.1, CSS Flexbox §3), so its content box ends at the bottom margin edge of
            // its last in-flow child: that margin cannot collapse through it (§8.3.1), including a
            // margin that has collapsed through the child from the child's own last child. Its own
            // padding and border follow (§10.6.7). This branch ended at the child's border box: a
            // `display: inline-block; padding: 40px` box holding a 20px block was 60px tall where
            // browsers make it 100, and DuckDuckGo's hero section (`padding: 40px 24px`, an h1 and
            // a p) was 56px short in any column that did not stretch it. It is the last child, as a
            // block's height ends at its last (CssBox.MarginBottomCollapse), not the lowest: after a
            // 30px block, one with margin-top: -25px and height: 10px ended an inline-block 30px
            // down, where browsers end it 15px down.
            double childMaxBottom = b.Location.Y + b.ActualBorderTopWidth + b.ActualPaddingTop;
            CssBox? lastInFlowChild = null;

            foreach (var child in b.Boxes)
            {
                // CSS 2.1 §10.6.7: absolutely positioned children are ignored in this box's auto
                // height, and a fixed one is just as out of flow (§9.6). The loop above has laid
                // them out, and an absolute one has entered the document's scrollable size there;
                // counting them here as well made the box as tall as its out-of-flow contents, and
                // its line with it, so everything below moved down by the difference. A column flex
                // container flows its items through here too, so there it moved every item after
                // this one: DuckDuckGo's viewport-tall off-canvas menu, fixed inside its relative
                // header, pushed the whole first screen one viewport down.
                if (child.Position is CssConstants.Absolute or CssConstants.Fixed)
                    continue;

                if (child.Display == CssConstants.None)
                    continue;

                // A float's margins never collapse, and the box contains the float (§10.6.7).
                if (child.Float != CssConstants.None)
                {
                    childMaxBottom = Math.Max(childMaxBottom, child.ActualBottom + child.ActualMarginBottom);
                    continue;
                }

                // An inline box has no bottom of its own; see CssBox.MarginBottomCollapse.
                if (child.Display == CssConstants.Inline)
                    continue;

                lastInFlowChild = child;
            }

            if (lastInFlowChild != null)
                childMaxBottom = Math.Max(childMaxBottom, CssBox.LastInFlowChildEdge(lastInFlowChild, collapsesThrough: false));

            b.ActualBottom = childMaxBottom + b.ActualPaddingBottom + b.ActualBorderBottomWidth;
        }

        // --- Compute height ---
        double ibHeight;
        bool heightIsPercent = !string.IsNullOrEmpty(b.Height) && b.Height.Contains('%');

        // A grid item's percentage block size resolves against its grid *area*
        // (the track), which is not known here — the track pass / PlaceItemInArea
        // sizes percentage/auto grid items to their area later. So measure an
        // in-flow grid item at its content height for now instead of resolving
        // the percentage against the container *width* (the wrong basis in the
        // branch below): that basis made an auto-height grid item with
        // height:100% balloon to ~100% of the grid width and, clipped to the
        // viewport, paint a full-viewport box (WPT
        // css-grid/grid-items/whitespace-in-grid-item-001); it also handed the
        // §11 track pass an inflated block size that tripped its "did a narrowed
        // column reflow this?" guard into declining to the stacking
        // approximation. Restricted to in-flow grid items — every other box
        // (inline-block, flex item, replaced inline SVG/img, out-of-flow static
        // positions) keeps its existing sizing untouched.
        bool isInFlowGridItem =
            b.Position is not (CssConstants.Absolute or CssConstants.Fixed)
            && b.ParentBox != null
            && b.ParentBox.Display is "grid" or "inline-grid";

        if (isReplaced)
        {
            // Already settled with the width, constraints and all.
            ibHeight = replacedContentHeight
                + b.ActualBorderTopWidth + b.ActualBorderBottomWidth
                + b.ActualPaddingTop + b.ActualPaddingBottom;
        }
        else if (heightIsPercent && isInFlowGridItem)
        {
            ibHeight = Math.Max(0, b.ActualBottom - b.Location.Y);
        }
        else if (TryResolveAtomicInlineSpecifiedHeight(b, containerWidth, out double cssHeight))
        {
            ibHeight = b.BoxSizing.Equals("border-box", StringComparison.OrdinalIgnoreCase)
                ? cssHeight
                : cssHeight
                    + b.ActualBorderTopWidth + b.ActualBorderBottomWidth
                    + b.ActualPaddingTop + b.ActualPaddingBottom;
        }
        // CSS Sizing 4 §4: an auto block axis takes its used size from the used
        // inline size through the box's preferred aspect ratio. An atomic
        // inline-level box computes its height here rather than in
        // CssBox.ResolveUsedBlockHeight, so the transfer has to be repeated on this
        // path — without it an outer <svg>, whose SVG children are not CSS boxes,
        // measured its content height as zero and vanished. b.Size.Width is the
        // used border-box width settled above (min-/max-width already applied), so
        // the transfer reads the same width the box is painted at.
        else if (b.TryGetAspectRatioBlockHeight(out double ratioHeight))
        {
            ibHeight = ratioHeight;
        }
        // CSS Containment 2 §3.2: an atomic inline computes its height here rather than in
        // CssBox.ResolveUsedBlockHeight, so size containment has to be honoured on this path too —
        // otherwise the contents this box just laid out are measured straight back into it. Below
        // the ratio arm, because a preferred aspect ratio and a definite inline size settle the
        // block axis outright and `contain-intrinsic-size` only stands in for *contents*; above the
        // content-derived arm, which is precisely what containment removes.
        else if (b.AppliesSizeContainment)
        {
            ibHeight = b.ContainedIntrinsicContentHeight
                + b.ActualBorderTopWidth + b.ActualBorderBottomWidth
                + b.ActualPaddingTop + b.ActualPaddingBottom;
        }
        else
        {
            ibHeight = Math.Max(0, b.ActualBottom - b.Location.Y);

        }

        // CSS 2.1 §10.7: clamp the block axis to min-height / max-height. ibHeight is a border-box
        // height, so a content-box bound has the box's own border and padding added to it.
        // max-height had no arm here at all until now, which is why a `display: inline-block` box
        // with `height: 1000px; max-height: 60px` stayed 1000px tall while the same declarations on
        // a `display: block` box clamped correctly (WPT issue #1562 problem 30 recorded it as the
        // second of the three gaps behind css-sizing/replaced-max-size-saturation).
        var blockBounds = isReplaced ? ReplacedBoxSizing.Bounds.Unconstrained : b.ResolveBlockSizeBounds();
        if (!blockBounds.IsUnconstrained)
        {
            double borderAndPadding = b.BoxSizing.Equals("border-box", StringComparison.OrdinalIgnoreCase)
                ? 0
                : b.ActualBorderTopWidth + b.ActualBorderBottomWidth
                  + b.ActualPaddingTop + b.ActualPaddingBottom;

            ibHeight = new ReplacedBoxSizing.Bounds(
                    blockBounds.Min > 0 ? blockBounds.Min + borderAndPadding : 0,
                    double.IsPositiveInfinity(blockBounds.Max) ? blockBounds.Max : blockBounds.Max + borderAndPadding)
                .Clamp(ibHeight);
        }

        b.ActualBottom = b.Location.Y + ibHeight;
        b.Size = new SizeF(b.Size.Width, (float)ibHeight);

        // --- Rotate a vertical writing-mode inline-block into physical space ---
        // An inline-block that is a vertical writing-mode root lays its content
        // out in the logical (horizontal) frame here (its Width/Height already
        // report the swapped logical extents via WillBeVerticalTransposed).
        // Unlike a block-level root, it never passes through CssBox.PerformLayout,
        // so the post-layout rotation was skipped and its content stayed in the
        // logical frame (e.g. a vertical-rl block child left-aligned instead of
        // block-start/right aligned). Rotate it in place now — its inline
        // position on the line is already correct — then advance the line by the
        // box's *physical* border-box width (Size.Width after the swap), which
        // differs from the logical ibBoxWidth for non-square boxes.
        double physicalBoxWidth = ibBoxWidth;
        if (VerticalFlowPrototype.Enabled
            && CssBoxProperties.IsVerticalWritingMode(b.WritingMode)
            && (b.ParentBox == null || !CssBoxProperties.IsVerticalWritingMode(b.ParentBox.WritingMode)))
        {
            b.ApplyVerticalWritingModeFlow();
            physicalBoxWidth = b.Size.Width;
        }

        // --- Register the inline-block as a rectangle in the line box ---
        line.Rectangles[b] = new RectangleF(b.Location.X, b.Location.Y,
            (float)physicalBoxWidth, (float)(b.ActualBottom - b.Location.Y));

        // --- Advance flow position ---
        // curx has leftspacing (margin+border+padding) already added.
        // After the inline-block, set curx so that after rightspacing
        // (margin+border+padding right) is added, we end up at the
        // right margin edge of the box.
        curx = ibBorderLeft + physicalBoxWidth
            - b.ActualBorderRightWidth - b.ActualPaddingRight;

        // CSS2.1 §10.8: a box that stands on the baseline with its bottom margin edge has the
        // strut's descent below it, as a baseline-aligned image does (see FlowBox), and the next
        // line starts below that. It started at the box's bottom, and the text beside the box,
        // which stands on that bottom (ApplyVerticalAlignment), reached into it.
        if (IsBaselineAligned(b)
            && ((b.Display == CssConstants.InlineBlock && LastLineBaseline(b) == null)
                || b.Display is "inline-flex" or "inline-grid"))
        {
            maxbottom = Math.Max(maxbottom,
                b.ActualBottom + b.ActualMarginBottom + StrutDescent(blockbox));
        }

        maxRight = Math.Max(maxRight, ibBorderLeft + physicalBoxWidth);
        maxbottom = Math.Max(maxbottom, b.ActualBottom + b.ActualMarginBottom);

        // CSS2.1 §10.8: the line the box stands on has the strut, as tall as the block's line
        // height, and the next line starts below that, as it does below a line of words. It
        // started below the box and the strut's descent: an 84px and a 20px inline-block, each 10px
        // tall, in a 25px block of 16px/20px text, put the second box's line 15.15px down, where
        // browsers start it 20px down. A flex container's lines hold its items and have no strut
        // (CreateLineBoxes).
        //
        // An inline box around the box is as tall as its own line height (CSS 2.1 §10.8.1), as an
        // image's is in FlowBox: a span with `line-height: 40px` around an inline-block started the
        // next line 20px down a block of 16px/20px text, where browsers start it 40px down.
        if (blockbox.Display is not ("flex" or "inline-flex"))
            maxbottom = Math.Max(maxbottom, cury + Math.Max(lineHeight, InlineBoxesLineHeight(b, blockbox)));

        // A relative offset waits for the line to be settled: see ApplyRelativeOffsets.
    }

    /// <summary>
    /// Whether a line of <paramref name="blockbox"/> may break before <paramref name="box"/>, when
    /// what comes before it on the line leaves it no room.
    /// </summary>
    /// <remarks>
    /// CSS Text 3 §5.1: whether there is a soft wrap opportunity between two pieces of content is
    /// for the <c>white-space</c> of their nearest common ancestor, and one that does not wrap,
    /// <c>nowrap</c> or <c>pre</c>, gives none. That ancestor is the parent of the box, or of the
    /// inline box around it, that has in-flow content before it; a box with none before it in the
    /// block is first on its line.
    /// </remarks>
    private static bool MayBreakBefore(CssBox box, CssBox blockbox)
    {
        for (var node = box; node.ParentBox is { } parent; node = parent)
        {
            if (HasInFlowContentBefore(node, parent))
                return parent.WhiteSpace is not (CssConstants.NoWrap or CssConstants.Pre);

            if (parent == blockbox || parent.Display != CssConstants.Inline)
                break;
        }

        return true;
    }

    /// <summary>Whether a child of <paramref name="parent"/> in the flow comes before <paramref name="child"/>.</summary>
    private static bool HasInFlowContentBefore(CssBox child, CssBox parent)
    {
        foreach (var sibling in parent.Boxes)
        {
            if (sibling == child)
                return false;

            if (sibling.Display != CssConstants.None
                && sibling.Float == CssConstants.None
                && sibling.Position is not (CssConstants.Absolute or CssConstants.Fixed))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether <see cref="FlowBox"/> places <paramref name="box"/>, a child of
    /// <paramref name="parent"/> with no words of its own, on a line whole, through
    /// <see cref="FlowInlineBlock"/>: an inline-block, an inline flex or grid container, or an item
    /// of a flex or grid container, which this engine lays out as one.
    /// </summary>
    private static bool FlowsAsInlineBlock(CssBox box, CssBox parent) =>
        box.Display == CssConstants.InlineBlock
        || box.Display is "inline-flex" or "inline-grid"
        || parent.Display is "flex" or "inline-flex" or "grid" or "inline-grid";

    /// <summary>
    /// Moves each box <see cref="FlowInlineBlock"/> placed on <paramref name="line"/> by its
    /// relative offset, with its rectangle on the line.
    /// </summary>
    /// <remarks>
    /// CSS2.1 §9.4.3: a relative offset moves a box and its content, and nothing around it; the line
    /// is laid out as if the box were where the flow put it. The flow places such a box from its
    /// position on the line, overwriting any offset the box's own layout applied, so the offset is
    /// applied again. It was applied as the flow placed the box, and the line then measured and
    /// aligned the box where the offset had put it: <c>top: 5px</c> made the line 5px taller and
    /// stood an inline-block beside it 5px lower, and <c>top: -5px</c> moved the block's lines 5px
    /// down. Vertical alignment, which places an inline-block afresh, and a right-to-left line,
    /// which places everything on it again, dropped the offset. Applied once the lines are settled,
    /// it moves only the box. Block-level boxes get their offset from
    /// <c>CssBox.ApplyRelativePositionOffset</c>, which these boxes never run.
    /// <para>
    /// The flow places only boxes inside the line's block. The lines an inline-block lays its own
    /// content out on can carry a rectangle for the inline-block too, bubbled out of its words, and
    /// it takes its offset on the line it sits on, not on those.
    /// </para>
    /// </remarks>
    private static void ApplyRelativeOffsets(CssLineBox line)
    {
        foreach (var box in new List<CssBox>(line.Rectangles.Keys))
        {
            if (box.Position != CssConstants.Relative
                || box.Words.Count > 0
                || box.ParentBox is not { } parent
                || !FlowsAsInlineBlock(box, parent)
                || !IsInside(box, line.OwnerBox))
            {
                continue;
            }

            double dx = CssBoxHelper.GetRelativeOffsetX(box);
            double dy = CssBoxHelper.GetRelativeOffsetY(box);
            if (dx == 0 && dy == 0)
                continue;

            if (dx != 0)
                box.OffsetLeft(dx);
            if (dy != 0)
                box.OffsetTop(dy);

            var rect = line.Rectangles[box];
            line.Rectangles[box] = new RectangleF((float)(rect.X + dx), (float)(rect.Y + dy), rect.Width, rect.Height);
            if (box.Rectangles.ContainsKey(line))
                box.Rectangles[line] = line.Rectangles[box];
        }
    }

    /// <summary>Whether <paramref name="box"/> is a descendant of <paramref name="ancestor"/>.</summary>
    private static bool IsInside(CssBox box, CssBox ancestor)
    {
        for (var parent = box.ParentBox; parent != null; parent = parent.ParentBox)
        {
            if (parent == ancestor)
                return true;
        }

        return false;
    }

    /// <summary>
    /// The specified block size an atomic inline-level box takes from its own <c>height</c>, or
    /// <see langword="false"/> when it has none the caller can use — an <c>auto</c> height, or a
    /// percentage the containing block cannot give a basis for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A percentage used to be measured against <paramref name="containerWidth"/>: the wrong axis
    /// outright, and the reason an outer <c>&lt;svg width="100%" height="100%"&gt;</c> — the shape
    /// every <c>conformance-checkers/html-svg</c> page has — came out as tall as the page is wide
    /// and then let "xMidYMid meet" centre its drawing a couple of hundred pixels down the box.
    /// The in-flow grid-item branch above already sidesteps this basis for its own case and names it
    /// as wrong.
    /// </para>
    /// <para>
    /// CSS 2.1 §10.5: with no definite basis the percentage computes to <c>auto</c>, so this
    /// declines and the caller falls through to the aspect-ratio transfer — which is what gives that
    /// <c>&lt;svg&gt;</c> the height its <c>viewBox</c> ratio implies, the same height the reference
    /// browser uses.
    /// </para>
    /// </remarks>
    private static bool TryResolveAtomicInlineSpecifiedHeight(
        CssBox b, double containerWidth, out double cssHeight)
    {
        cssHeight = 0;
        if (b.Height == CssConstants.Auto || string.IsNullOrEmpty(b.Height))
            return false;

        if (!b.Height.Contains('%'))
        {
            cssHeight = CssLengthParser.ParseLength(b.Height, containerWidth, b.GetEmHeight());
            return true;
        }

        if (!b.TryGetPercentageBlockSizeBasis(out double basis))
            return false;

        cssHeight = CssLengthParser.ParseLength(b.Height, basis, b.GetEmHeight());
        return true;
    }

    /// <summary>
    /// True when <paramref name="box"/>'s in-flow content is inline-level except
    /// for <c>&lt;br&gt;</c> elements — which compute to block-level boxes in
    /// Broiler but merely force a line break within an inline formatting context.
    /// Such a box should be laid out with <see cref="CreateLineBoxes"/> (the
    /// breaks split the inline content into lines) rather than the block-children
    /// path, which never establishes line boxes for the anonymous inline runs and
    /// so leaves the box zero-height. Requires at least one <c>&lt;br&gt;</c> so a
    /// genuinely block-only box is unaffected (it is not inline content).
    /// </summary>
    internal static bool InlineContentWithBrsOnly(CssBox box)
    {
        bool sawBr = false;
        foreach (var child in box.Boxes)
        {
            if (child.Display == CssConstants.None)
                continue;

            if (child.Position is CssConstants.Absolute or CssConstants.Fixed)
                continue;

            if (child.IsBrElement)
            {
                sawBr = true;
                continue;
            }

            if (!child.IsInline && child.Float == CssConstants.None)
                return false;
        }

        return sawBr;
    }

    private static bool HasBlockLevelFlexItems(CssBox box)
    {
        foreach (var child in box.Boxes)
        {
            if (child.Display == CssConstants.None)
                continue;

            if (child.Position is CssConstants.Absolute or CssConstants.Fixed)
                continue;

            if (!child.IsInline)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Recursively measures word sizes on all descendant boxes so that
    /// intrinsic width calculations are reliable.
    /// </summary>
    private static void MeasureDescendantWords(ILayoutEnvironment g, CssBox box)
    {
        box.MeasureWordsSize(g);

        foreach (var child in box.Boxes)
            MeasureDescendantWords(g, child);
    }

    /// <summary>
    /// Whether the box's <c>top</c> and <c>left</c> are both <c>auto</c>, so that neither of them
    /// places it.
    /// </summary>
    private static bool HasAutoTopAndLeft(CssBox box) =>
        (string.IsNullOrEmpty(box.Top) || box.Top == CssConstants.Auto)
        && (string.IsNullOrEmpty(box.Left) || box.Left == CssConstants.Auto);

    private static void AdjustAbsolutePosition(CssBox box, double left, double top)
    {
        left += box.ActualMarginLeft;
        top += box.ActualMarginTop;

        // CSS 2.1 §9.3.2: Apply 'top' and 'left' offsets for absolutely
        // positioned elements.
        if (box.Top != CssConstants.Auto && !string.IsNullOrEmpty(box.Top))
        {
            double topOffset = CssLengthParser.ParseLength(box.Top, box.Size.Height, box.GetEmHeight());

            if (!double.IsNaN(topOffset))
                top += topOffset;
        }

        if (box.Left != CssConstants.Auto && !string.IsNullOrEmpty(box.Left))
        {
            double leftOffset = CssLengthParser.ParseLength(box.Left, box.Size.Width, box.GetEmHeight());

            if (!double.IsNaN(leftOffset))
                left += leftOffset;
        }

        if (box.Words.Count > 0)
        {
            foreach (var word in box.Words)
            {
                word.Left += left;
                word.Top += top;
            }
        }
        else
        {
            foreach (var b in box.Boxes)
                AdjustAbsolutePosition(b, left, top);
        }
    }

    private static void BubbleRectangles(CssBox box, CssLineBox line)
    {
        if (box.Words.Count > 0)
        {
            double x = float.MaxValue, y = float.MaxValue, r = float.MinValue, b = float.MinValue;
            List<CssRect> words = line.WordsOf(box);

            if (words.Count <= 0)
                return;

            foreach (CssRect word in words)
            {
                // handle if line is wrapped for the first text element where parent has left margin\padding
                var left = word.Left;

                if (box.ParentBox is { } boxParent && box == boxParent.Boxes[0] && word == box.Words[0] && word == line.Words[0] && line != line.OwnerBox.LineBoxes[0] && !word.IsLineBreak)
                    left -= boxParent.ActualMarginLeft + boxParent.ActualBorderLeftWidth + boxParent.ActualPaddingLeft;


                x = Math.Min(x, left);
                r = Math.Max(r, word.Right);
                y = Math.Min(y, word.Top);
                b = Math.Max(b, word.Bottom);
            }

            line.UpdateRectangle(box, x, y, r, b);
        }
        else
        {
            foreach (CssBox b in box.Boxes)
                BubbleRectangles(b, line);
        }
    }

    /// <summary>
    /// Gives the inline boxes an atomic inline-level box sits in a rectangle around it on
    /// <paramref name="line"/>, as <see cref="BubbleRectangles"/> gives them one around their words.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The flow puts an inline-block, or an inline flex or grid container, on the line as a single
    /// border box, and lays its content out on lines of its own, so bubbling the line's words finds
    /// nothing of it. An inline box around one, an <c>&lt;a&gt;</c> wrapping an inline-block button,
    /// used to get its rectangle from the atomic box's own lines instead, by
    /// <see cref="CssLineBox.UpdateRectangle"/> bubbling out of them. That no longer happens, because
    /// it made the atomic box part of its own line; the rectangle it gave was also where the box's
    /// content was before this line was aligned, x=16 on a centred line that had moved the box to 154.
    /// </para>
    /// <para>
    /// This runs once the lines are settled, so the inline box's own padding and border around the
    /// atomic box do not move them: CSS 2.1 §10.6.1 leaves them out of the line box's height.
    /// </para>
    /// </remarks>
    private static void BubbleAtomicInlineRectangles(CssLineBox line)
    {
        bool bubbled = false;

        foreach (var box in new List<CssBox>(line.Rectangles.Keys))
        {
            if (!CssBoxHelper.IsAtomicInlineLevel(box.Display)
                || box.ParentBox is not { IsInline: true } parent
                || parent == line.OwnerBox
                || IsInAbsposSubtree(box, line.OwnerBox))
                continue;

            // CSS 2.1 §9.4.3: a relative offset moves the box and nothing around it, so the inline
            // box wraps where the flow put it.
            RectangleF rect = line.Rectangles[box];
            if (box.Position == CssConstants.Relative)
                rect.Offset(-(float)CssBoxHelper.GetRelativeOffsetX(box), -(float)CssBoxHelper.GetRelativeOffsetY(box));

            line.UpdateRectangle(parent, rect.Left, rect.Top, rect.Right, rect.Bottom);
            bubbled = true;
        }

        if (bubbled)
            line.AssignRectanglesToBoxes();
    }

    private static void ApplyHorizontalAlignment(CssLineBox lineBox, bool lineRtl)
    {
        var box = lineBox.OwnerBox;

        // CSS Text 4 §text-align / §text-align-last: text-align governs every line;
        // the *last* line of the block is instead governed by text-align-last.  The
        // shorthand value 'justify-all' additionally sets the last line to justify
        // (a plain 'justify' leaves the last line 'start'-aligned).  Resolve the
        // per-line alignment keyword first, then map the logical values below.
        bool isLastLine = lineBox.Equals(box.LineBoxes[^1]);
        bool justifyAll = string.Equals(box.TextAlign, "justify-all", StringComparison.OrdinalIgnoreCase);
        string effectiveAlign = isLastLine
            ? ResolveTextAlignLast(box, justifyAll)
            : (justifyAll ? CssConstants.Justify : box.TextAlign);

        // Resolve the logical 'start'/'end' keywords (and the initial value, which
        // is 'start') against the line's base direction.  In a left-to-right base,
        // start=left and end=right; in a right-to-left base they swap.  Physical
        // 'left'/'right'/'center'/'justify' values pass through unchanged.  Under
        // unicode-bidi:plaintext the base is the per-line resolved direction; this
        // is what keeps right-to-left lines aligned to the right edge and, without
        // it, an RTL box left its 'start'-aligned text on the left (CSS Text
        // §text-align).
        string resolvedAlign = effectiveAlign switch
        {
            null or "" or "start" => lineRtl ? CssConstants.Right : CssConstants.Left,
            "end" => lineRtl ? CssConstants.Left : CssConstants.Right,
            // Legacy -webkit-{left,right,center} align inline content like their
            // standard counterparts (they additionally drive block alignment,
            // handled in CssBox justify-self resolution).
            "-webkit-left" => CssConstants.Left,
            "-webkit-right" => CssConstants.Right,
            "-webkit-center" => CssConstants.Center,
            _ => effectiveAlign
        };

        switch (resolvedAlign)
        {
            case CssConstants.Right:
                ApplyRightAlignment(lineBox);
                break;

            case CssConstants.Center:
                ApplyCenterAlignment(lineBox);
                break;

            case CssConstants.Justify:
                // The caller only routes the last line here when text-align-last
                // resolved to justify (justify-all or text-align-last:justify), so
                // justify it too — no last-line skip needed at this point.
                ApplyJustifyAlignment(lineBox);
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Resolves the effective alignment keyword for a block's <b>last</b> line per
    /// CSS Text 4 §text-align-last.  An explicit <c>text-align-last</c> value wins;
    /// the initial <c>auto</c> follows <c>text-align</c>, except that a plain
    /// <c>justify</c> text-align leaves the last line <c>start</c>-aligned (ragged)
    /// unless the shorthand value was <c>justify-all</c>, which justifies it too.
    /// </summary>
    private static string ResolveTextAlignLast(CssBox box, bool justifyAll)
    {
        string last = box.TextAlignLast;
        if (!string.IsNullOrEmpty(last)
            && !string.Equals(last, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return last;
        }

        // auto:
        if (justifyAll)
            return CssConstants.Justify;

        if (string.Equals(box.TextAlign, CssConstants.Justify, StringComparison.OrdinalIgnoreCase))
            return "start"; // a justified block's last line stays ragged (start-aligned)

        return box.TextAlign; // every other value applies to the last line as well
    }

    /// <summary>
    /// Returns the line's base direction as resolved from its first strong
    /// (Hebrew/Arabic vs. Latin/Greek/Cyrillic) character: <c>true</c> for
    /// right-to-left, <c>false</c> for left-to-right, or <c>null</c> when the line
    /// has no strong character (so the caller inherits the previous paragraph's or
    /// the containing block's direction).  Used for <c>unicode-bidi: plaintext</c>.
    /// </summary>
    private static bool? LineFirstStrongRtl(CssLineBox line)
    {
        foreach (CssRect word in line.Words)
        {
            string text = word.Text;

            if (string.IsNullOrEmpty(text))
                continue;

            foreach (char c in text)
            {
                if (IsRtlStrongChar(c))
                    return true;

                if (IsLtrStrongChar(c))
                    return false;
            }
        }

        return null; // no strong character → inherit base direction
    }

    /// <summary>
    /// Seeds the running base direction for a <c>unicode-bidi: plaintext</c> block
    /// whose first paragraph has no strong character of its own.  Such a paragraph
    /// inherits the previous paragraph's direction; because neutral paragraphs
    /// propagate that direction forward, the result equals the direction of the most
    /// recent strong character that appears before this block in document order.
    /// Falls back to the block's own direction when no preceding strong character
    /// exists (i.e. the containing block's direction).
    /// </summary>
    private static bool SeedPlaintextBaseRtl(CssBox blockBox)
    {
        var parent = blockBox.ParentBox;

        if (parent != null)
        {
            int index = parent.Boxes.IndexOf(blockBox);

            for (int i = index - 1; i >= 0; i--)
            {
                bool? strong = LastStrongRtl(parent.Boxes[i]);
                if (strong.HasValue)
                    return strong.Value;
            }
        }

        return blockBox.Direction == CssConstants.Rtl;
    }

    /// <summary>
    /// Returns the direction of the last strong character within <paramref name="box"/>'s
    /// subtree in document order (<c>true</c> RTL, <c>false</c> LTR), or <c>null</c>
    /// when the subtree has no strong character.
    /// </summary>
    private static bool? LastStrongRtl(CssBox box)
    {
        for (int i = box.Boxes.Count - 1; i >= 0; i--)
        {
            bool? strong = LastStrongRtl(box.Boxes[i]);
            if (strong.HasValue)
                return strong;
        }

        for (int i = box.Words.Count - 1; i >= 0; i--)
        {
            string text = box.Words[i].Text;
            if (string.IsNullOrEmpty(text))
                continue;

            for (int c = text.Length - 1; c >= 0; c--)
            {
                if (IsRtlStrongChar(text[c]))
                    return true;

                if (IsLtrStrongChar(text[c]))
                    return false;
            }
        }

        return null;
    }

    private static bool IsRtlStrongChar(char c) =>
        (c >= 0x0590 && c <= 0x05FF) ||   // Hebrew
        (c >= 0x0600 && c <= 0x06FF) ||   // Arabic
        (c >= 0x0750 && c <= 0x077F) ||   // Arabic Supplement
        (c >= 0x08A0 && c <= 0x08FF) ||   // Arabic Extended-A
        (c >= 0xFB1D && c <= 0xFDFF) ||   // Hebrew/Arabic presentation forms-A
        (c >= 0xFE70 && c <= 0xFEFF);     // Arabic presentation forms-B

    private static bool IsLtrStrongChar(char c) =>
        (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
        (c >= 0x00C0 && c <= 0x024F) ||   // Latin-1 supplement / extended
        (c >= 0x0370 && c <= 0x03FF) ||   // Greek
        (c >= 0x0400 && c <= 0x04FF);     // Cyrillic

    private static void ApplyRightToLeft(CssLineBox lineBox, bool lineRtl)
    {
        // When the line's base direction is right-to-left the whole line is
        // mirrored; otherwise only the individual inline boxes that opt into RTL
        // are reversed.  Under unicode-bidi:plaintext 'lineRtl' is the per-line
        // resolved direction, so left-to-right lines inside an RTL block stay on
        // the left instead of being mirrored to the right edge.
        if (lineRtl)
        {
            ApplyRightToLeftOnLine(lineBox);
        }
        else
        {
            foreach (var box in lineBox.RelatedBoxes)
            {
                if (box.Direction == CssConstants.Rtl)
                    ApplyRightToLeftOnSingleBox(lineBox, box);
            }
        }
    }

    private static void ApplyRightToLeftOnLine(CssLineBox line)
    {
        if (line.Words.Count <= 0)
            return;

        double left = line.Words[0].Left;
        double right = line.Words[^1].Right;

        foreach (CssRect word in line.Words)
        {
            double diff = word.Left - left;
            double wright = right - diff;

            word.Left = wright - word.Width;
        }
    }

    private static void ApplyRightToLeftOnSingleBox(CssLineBox lineBox, CssBox box)
    {
        int leftWordIdx = -1;
        int rightWordIdx = -1;

        for (int i = 0; i < lineBox.Words.Count; i++)
        {
            if (lineBox.Words[i].OwnerBox != box)
                continue;

            if (leftWordIdx < 0)
                leftWordIdx = i;

            rightWordIdx = i;
        }

        if (leftWordIdx <= -1 || rightWordIdx <= leftWordIdx)
            return;

        double left = lineBox.Words[leftWordIdx].Left;
        double right = lineBox.Words[rightWordIdx].Right;

        for (int i = leftWordIdx; i <= rightWordIdx; i++)
        {
            double diff = lineBox.Words[i].Left - left;
            double wright = right - diff;

            lineBox.Words[i].Left = wright - lineBox.Words[i].Width;
        }
    }

    /// <summary>
    /// CSS 2.1 §10.8: A text run contributes its inline box's <em>line-height</em>
    /// to the line box (and hence the block's content height), not its taller
    /// font content area.  When <c>line-height</c> is smaller than the content
    /// area the glyphs overflow the line box, but they must not increase it —
    /// otherwise an explicit small <c>line-height</c> (e.g. <c>line-height:1</c>
    /// on a font whose natural box is ~1.16em) produces a too-tall block.
    /// The glyph rectangle itself is left untouched, so glyph positions (and
    /// calibrated layouts) are unchanged; only the height contribution is
    /// clamped.  Replaced inline content (images) and runs with no positive
    /// line-height keep contributing their full box.
    /// </summary>
    private static double InlineWordLineBoxBottom(CssRect word)
    {
        double ownerLineHeight = word.OwnerBox?.ActualLineHeight ?? 0;
        if (word.IsImage)
            return word.Bottom + ImageWordMarginBottom(word);

        if (ownerLineHeight <= 0)
            return word.Bottom;

        return Math.Min(word.Bottom, word.Top + ownerLineHeight);
    }

    /// <summary>
    /// The bottom of a line holding a word whose box's <c>line-height</c> is taller than the word
    /// and than the block's line height, from <paramref name="lineTop"/>; for any other line
    /// <see cref="double.MinValue"/>, which leaves it to its words and the block's line height.
    /// </summary>
    /// <remarks>
    /// CSS2.1 §10.8.1 makes each inline box on a line as tall as its <c>line-height</c>, half the
    /// leading above its glyphs and half below, and the line box as tall as all of them together,
    /// the block's own (the strut) among them. A line counted a word's line height only where it was
    /// shorter than the word, so <c>&lt;a style="line-height: 60px"&gt;</c> in a block of normal line
    /// height left the block one word tall, where browsers make it 60px. The glyphs stand where
    /// vertical alignment put them, half their box's leading below its top, and the line is as tall
    /// as the inline boxes together from <paramref name="lineTop"/>, the top of the highest of them.
    /// Other lines are measured by the block's line height and their words.
    /// </remarks>
    internal static double TallInlineBoxLineBottom(CssBox blockBox, CssLineBox line, double lineTop)
    {
        double blockLineHeight = blockBox.ActualLineHeight;
        bool holdsTallerBox = false;

        foreach (var word in line.Words)
        {
            double lineHeight = WordLineHeight(word);
            if (lineHeight > word.Height && lineHeight > blockLineHeight
                && !IsInAbsposSubtree(word.OwnerBox, blockBox))
            {
                holdsTallerBox = true;
                break;
            }
        }

        if (!holdsTallerBox)
            return double.MinValue;

        // The strut: the block's line height around its font's glyphs, which stand on the line's
        // baseline.
        double fontHeight = blockBox.ActualFont.Height;
        double strutGlyphTop = line.Baseline is double lineBaseline
            ? lineBaseline - fontHeight * TypicalAscentRatio
            : lineTop;
        double top = strutGlyphTop - HalfLeading(blockBox);
        double bottom = strutGlyphTop + fontHeight + LeadingBelow(blockBox);

        foreach (var word in line.Words)
        {
            if (IsInAbsposSubtree(word.OwnerBox, blockBox))
                continue;

            if (word.IsImage)
            {
                top = Math.Min(top, word.Top - ImageWordMarginTop(word));
                bottom = Math.Max(bottom, word.Bottom + ImageWordMarginBottom(word));
                continue;
            }

            double lineHeight = WordLineHeight(word);
            double leading = lineHeight > 0 ? lineHeight - word.Height : 0;
            double above = Math.Floor(leading / 2);
            top = Math.Min(top, word.Top - above);
            bottom = Math.Max(bottom, word.Bottom + leading - above);
        }

        foreach (var (box, rect) in line.Rectangles)
        {
            if (box.IsInlineNonReplaced || IsInAbsposSubtree(box, blockBox))
                continue;

            top = Math.Min(top, rect.Top);
            bottom = Math.Max(bottom, rect.Bottom);
        }

        return lineTop + (bottom - top);
    }

    /// <summary>The <c>line-height</c> of the box a word is in, or 0 for an image or a normal one.</summary>
    private static double WordLineHeight(CssRect word) =>
        word.IsImage ? 0 : word.OwnerBox?.ActualLineHeight ?? 0;

    /// <summary>
    /// The block-start margin of an inline replaced element, which CSS2.1 §10.8.1 makes part of
    /// the margin box the line aligns. A percentage resolves against the containing block's
    /// <em>width</em> (CSS2.1 §8.3), which <c>ActualMarginTop</c> already does.
    /// </summary>
    private static double ImageWordMarginTop(CssRect word)
    {
        double margin = word.OwnerBox?.ActualMarginTop ?? 0;
        return double.IsNaN(margin) ? 0 : margin;
    }

    /// <summary>The block-end margin counterpart of <see cref="ImageWordMarginTop"/>.</summary>
    private static double ImageWordMarginBottom(CssRect word)
    {
        double margin = word.OwnerBox?.ActualMarginBottom ?? 0;
        return double.IsNaN(margin) ? 0 : margin;
    }

    /// <summary>
    /// The block-level image <paramref name="blockBox"/> exists to position, when it is the
    /// anonymous block the box fix-up wraps one in; otherwise null.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Block flow here cannot position a block-level replaced box, so the box fix-up
    /// (<c>DomParser.CorrectImgBoxes</c>, in Broiler.HTML) wraps each <c>display: block</c> image
    /// that is not a row flex item in an anonymous block and makes the image inline inside it. That
    /// is an image declared <c>display: block</c>, as most CSS resets declare every image, and every
    /// image that is a column flex item or a grid item, which blockification makes block-level.
    /// Inline, the image was laid out on a line of its own, and a line has a strut (CSS2.1 §10.8):
    /// the image stood on the strut's baseline, so the wrapper ended the strut's descent below it,
    /// 3.8px at a 16px font, and an image shorter than the strut's ascent was pushed down as well.
    /// A block-level box is on no line. Browsers put the next box directly below the image, and a
    /// 10px image takes 10px.
    /// </para>
    /// <para>
    /// An inline image alone in an anonymous block looks the same from here: the block-inside-inline
    /// correction wraps the image of <c>&lt;div&gt;&lt;img&gt;&lt;div&gt;</c> that way, and there the
    /// strut is right, as browsers leave the descent below an inline image. So the image carries
    /// whether it was block-level (<see cref="CssBoxImage.IsBlockLevel"/>), recorded before the
    /// fix-up rewrote its <c>display</c>. A floated or absolutely positioned image is on no line of
    /// the wrapper's, and is left alone.
    /// </para>
    /// </remarks>
    private static CssBoxImage? BlockLevelImageOf(CssBox blockBox) =>
        blockBox.HtmlTag == null
        && blockBox.Display == CssConstants.Block
        && blockBox.Boxes.Count == 1
        && blockBox.Boxes[0] is CssBoxImage { IsBlockLevel: true } image
        && image.Float == CssConstants.None
        && image.Position is not (CssConstants.Absolute or CssConstants.Fixed)
            ? image
            : null;

    /// <summary>
    /// Places a block-level image at the top of the anonymous block that wraps it, below its own top
    /// margin, and returns the bottom of its margin box, which is where the wrapper's content ends.
    /// Returns <paramref name="bottom"/> and moves nothing if no line of the wrapper holds the image.
    /// </summary>
    private static double PlaceBlockLevelImage(CssBox blockBox, CssBoxImage image, double top, double bottom)
    {
        foreach (var line in blockBox.LineBoxes)
        {
            if (!line.Rectangles.TryGetValue(image, out RectangleF rect))
                continue;

            var word = image.Words[0];
            double flowedTop = word.Top;
            word.Top = top + ImageWordMarginTop(word);

            // As the flow does for every word: in paged media, an image that would straddle a page
            // break starts the next page instead.
            if (!blockBox.IsFixed)
                word.BreakPage();

            // Across the wrapper too, the image is placed as a block-level box and not as the content
            // of a line: its line put it where `text-align` put the line's content, and gave an
            // `auto` margin nothing, as an inline box's is.
            double shiftX = BlockLevelImageLeft(blockBox, image, rect.Width) - rect.X;
            word.Left += shiftX;

            double shift = word.Top - flowedTop;
            var placed = new RectangleF((float)(rect.X + shiftX), (float)(rect.Y + shift), rect.Width, rect.Height);
            line.Rectangles[image] = placed;
            image.Rectangles[line] = placed;

            // Baseline alignment gives an image it moves a position of its own
            // (CssLineBox.SetBaseLine), and the geometry handed to script takes that in preference
            // to the image's line rectangle. It is stale now the image is back at the top, so it
            // goes: the image is placed by its line rectangle alone, as every image that alignment
            // leaves where it is already is.
            if (image.Size.Height != 0)
            {
                image.Location = new PointF(image.Location.X, placed.Y);
                image.Size = SizeF.Empty;
            }

            return word.Bottom + ImageWordMarginBottom(word);
        }

        return bottom;
    }

    /// <summary>
    /// Where the border box of a block-level image <paramref name="width"/> wide starts across the
    /// anonymous block that wraps it.
    /// </summary>
    /// <remarks>
    /// CSS2.1 §10.3.4 places a block-level replaced element by the margin rules of §10.3.3. Both
    /// margins <c>auto</c> centre it, which is how <c>img { display: block; margin: 0 auto }</c>
    /// centres an image; one <c>auto</c> margin takes all the room there is. When the image does not
    /// fit, an <c>auto</c> margin counts as zero, and when no margin is <c>auto</c> the one on the
    /// end side of the containing block's direction gives way: the image starts at the left in a
    /// left-to-right block and ends at the right in a right-to-left one. <c>text-align</c> does not
    /// come into it, since the image is not inline content.
    /// </remarks>
    private static double BlockLevelImageLeft(CssBox blockBox, CssBoxImage image, double width)
    {
        double left = blockBox.ClientLeft;
        double right = blockBox.ClientRight;
        bool autoLeft = image.IsSpecifiedMarginLeftAuto;
        bool autoRight = image.IsSpecifiedMarginRightAuto;
        double marginLeft = autoLeft || double.IsNaN(image.ActualMarginLeft) ? 0 : image.ActualMarginLeft;
        double marginRight = autoRight || double.IsNaN(image.ActualMarginRight) ? 0 : image.ActualMarginRight;
        double free = right - left - marginLeft - width - marginRight;

        if (free < 0)
            autoLeft = autoRight = false;

        if (autoLeft && autoRight)
            return left + marginLeft + free / 2;

        if (autoLeft)
            return left + marginLeft + free;

        if (autoRight)
            return left + marginLeft;

        return blockBox.Direction == CssConstants.Rtl
            ? right - marginRight - width
            : left + marginLeft;
    }

    /// <summary>
    /// Returns whether <paramref name="box"/> ends with atomic inline-level
    /// content (an <c>inline-block</c>/<c>inline-flex</c>/<c>inline-grid</c>
    /// box), looking through the anonymous block wrapper that the
    /// block-inside-inline correction generates around inline content split by a
    /// <c>&lt;br&gt;</c>.  Used to decide whether a following <c>&lt;br&gt;</c>'s
    /// empty-line spacer is spurious (it merely ends the inline-block's line).
    /// </summary>
    internal static bool EndsWithAtomicInlineBlock(CssBox box)
    {
        if (box == null)
            return false;

        if (box.Display == CssConstants.InlineBlock
            || box.Display is "inline-flex" or "inline-grid")
            return true;

        if (box.Kind != BoxKind.Anonymous)
            return false;

        for (int i = box.Boxes.Count - 1; i >= 0; i--)
        {
            var c = box.Boxes[i];
            if (c.Display == CssConstants.None
                || c.Position is CssConstants.Absolute or CssConstants.Fixed
                || c.Float != CssConstants.None)
                continue;

            return EndsWithAtomicInlineBlock(c);
        }

        return false;
    }

    /// <summary>
    /// Returns whether <paramref name="box"/> is the anonymous block wrapper that the
    /// block-inside-inline correction generates around inline content, and that content holds
    /// text or an image, looking through the inline boxes it is in. A following
    /// <c>&lt;br&gt;</c> ends the content's last line, as it ends the line of an inline-block (see
    /// <see cref="EndsWithAtomicInlineBlock"/>), rather than making an empty one.
    /// </summary>
    /// <remarks>
    /// The wrapper is the box without an element: <see cref="CssBoxProperties.Kind"/> is
    /// <c>Anonymous</c> for every box the host does not classify, a <c>&lt;div&gt;</c> included, and
    /// a <c>&lt;br&gt;</c> after a <c>&lt;div&gt;</c> does start an empty line.
    /// </remarks>
    internal static bool HoldsInlineContent(CssBox box) =>
        box is { HtmlTag: null, IsInline: false } && box.Display != CssConstants.None && InlineRunHoldsContent(box);

    private static bool InlineRunHoldsContent(CssBox box)
    {
        foreach (var word in box.Words)
        {
            if (word.IsImage || !word.IsSpaces)
                return true;
        }

        foreach (var child in box.Boxes)
        {
            if (child.Display == CssConstants.Inline
                && child.Position is not (CssConstants.Absolute or CssConstants.Fixed)
                && child.Float == CssConstants.None
                && InlineRunHoldsContent(child))
                return true;
        }

        return false;
    }

    /// <summary>
    /// How far the box's line height reaches above its glyphs (CSS 2.1 §10.8.1): half its leading,
    /// the line height less the font's height, negative for a line height less than the font's.
    /// As browsers do, the half is floored to a whole pixel, and what that leaves goes below the
    /// glyphs (<see cref="LeadingBelow"/>). 0 for a box with no line height.
    /// </summary>
    internal static double HalfLeading(CssBox box)
    {
        double lineHeight = box.ActualLineHeight;
        return lineHeight > 0 ? Math.Floor((lineHeight - box.ActualFont.Height) / 2) : 0;
    }

    /// <summary>
    /// How far the box's line height reaches below its glyphs: the rest of its leading, the half
    /// that <see cref="HalfLeading"/> puts above them less what the floor took off it.
    /// </summary>
    internal static double LeadingBelow(CssBox box)
    {
        double lineHeight = box.ActualLineHeight;
        return lineHeight > 0 ? lineHeight - box.ActualFont.Height - HalfLeading(box) : 0;
    }

    /// <summary>
    /// How far below the baseline the strut of <paramref name="blockBox"/>'s lines reaches: its
    /// font's descent and the leading below it. It was taken as a fifth of the line height, which
    /// is the font's descent only where the line height is the font's height: a 100px image on the
    /// baseline of a 60px line had 12px below it, where browsers leave the strut's 25px.
    /// </summary>
    internal static double StrutDescent(CssBox blockBox) =>
        blockBox.ActualFont.Height * (1.0 - TypicalAscentRatio) + LeadingBelow(blockBox);

    /// <summary>
    /// The top of the inline box a word of text stands in on its line: the leading above its glyphs
    /// higher, which a line height less than the font's height puts below them. An image's top is
    /// its own.
    /// </summary>
    internal static double WordLayoutTop(CssRect word) =>
        word.IsImage || word.OwnerBox == null ? word.Top : word.Top - HalfLeading(word.OwnerBox);

    /// <summary>
    /// The bottom of the inline box a word of text stands in on its line: the leading below its
    /// glyphs lower (<see cref="LeadingBelow"/>), which a line height less than the font's height
    /// puts above their bottom. An image's bottom is its own.
    /// </summary>
    internal static double WordLayoutBottom(CssRect word) =>
        word.IsImage || word.OwnerBox == null ? word.Bottom : word.Bottom + LeadingBelow(word.OwnerBox);

    /// <summary>
    /// How far below a box's top edge its baseline sits, for the purpose of aligning it on the line.
    /// </summary>
    /// <remarks>
    /// CSS2.1 §10.8 gives two answers. An ordinary inline box sits on the baseline of its own text,
    /// which is its font's ascent below its top. An <b>atomic</b> inline — an <c>inline-block</c>
    /// with no in-flow line boxes, and every inline <b>replaced</b> element — has its baseline at its
    /// bottom margin edge instead, so its whole height is ascent and it hangs above the line's
    /// baseline rather than straddling it.
    /// <para>
    /// Only the <c>inline-block</c> half of that was implemented, so an <c>&lt;img&gt;</c> was aligned
    /// by the ascent of a font it does not draw: every image on a line was placed the same ~13px
    /// below the line top regardless of its height, which reads as top-aligned and is what a page
    /// with images of two different heights on one line showed. The line-box <em>height</em> code has
    /// always assumed the other rule — <see cref="CreateLineBoxes"/> extends a line below a tall
    /// image by the strut's descent precisely because the image's bottom is the baseline — so the
    /// two halves disagreed with each other, not merely with the spec.
    /// </para>
    /// </remarks>
    private static double BaselineAscentOf(CssBox box, CssLineBox lineBox)
    {
        if (!IsAtomicInline(box) || !lineBox.Rectangles.TryGetValue(box, out RectangleF rect))
            return box.ActualFont.Height * TypicalAscentRatio;

        // An inline-block with a line of text in it and nothing clipped has that line's baseline.
        return box.Display == CssConstants.InlineBlock && LastLineBaseline(box) is double baseline
            ? baseline - rect.Top
            : rect.Height;
    }

    /// <summary>
    /// Where the baseline of an inline-block's last in-flow line box lies, or null when the
    /// inline-block's baseline is its bottom margin edge instead.
    /// </summary>
    /// <remarks>
    /// CSS 2.1 §10.8.1: the baseline of an <c>inline-block</c> is the baseline of its last line box
    /// in the normal flow, unless it has none or its <c>overflow</c> is not <c>visible</c> (see
    /// <see cref="CssBox.UsesBottomMarginEdgeBaseline"/>). The line is the lowest of the in-flow
    /// line boxes in it and in its in-flow blocks, not in the atomic inlines it holds, whose lines
    /// are their own; its baseline is where its words aligned to the baseline stand on it, a word
    /// of text its font's ascent below its top and an image at its bottom. A last line with an
    /// atomic inline on it has that box's baseline among its own, which is not tracked, so it gives
    /// null too, and the inline-block stands as it did before its baseline was tracked at all.
    /// </remarks>
    internal static double? LastLineBaseline(CssBox box)
    {
        if (box.Display != CssConstants.InlineBlock || box.UsesBottomMarginEdgeBaseline)
            return null;

        // An inline-block holding words of its own, as a ::before with display: inline-block does,
        // lays them out on its parent's line, and their baseline is its baseline.
        if (box.Words.Count > 0)
            return WordsBaseline(box.Words, box);

        CssLineBox? last = null;
        double lastBottom = double.MinValue;
        FindLastLine(box, box, ref last, ref lastBottom);

        if (last == null)
            return null;

        foreach (var key in last.Rectangles.Keys)
        {
            if (IsNestedAtomicInline(key, box))
                return null;
        }

        return WordsBaseline(last.Words, box);
    }

    /// <summary>
    /// Where the words aligned to the baseline among <paramref name="words"/> stand, a word of text
    /// its font's ascent below its top and an image at its bottom; where any of them does when none
    /// is aligned to it; or null when there are no words in the flow of <paramref name="root"/>.
    /// </summary>
    private static double? WordsBaseline(IEnumerable<CssRect> words, CssBox root)
    {
        double aligned = double.MinValue;
        double any = double.MinValue;

        foreach (var word in words)
        {
            if (word.OwnerBox == null || IsInAbsposSubtree(word.OwnerBox, root))
                continue;

            double wordBaseline = word.Top + (word.IsImage
                ? word.Height
                : word.OwnerBox.ActualFont.Height * TypicalAscentRatio);

            any = Math.Max(any, wordBaseline);

            if (string.IsNullOrEmpty(word.OwnerBox.VerticalAlign) || word.OwnerBox.VerticalAlign == CssConstants.Baseline)
                aligned = Math.Max(aligned, wordBaseline);
        }

        double found = aligned > double.MinValue ? aligned : any;
        return found > double.MinValue ? found : null;
    }

    private static void FindLastLine(CssBox box, CssBox root, ref CssLineBox? last, ref double lastBottom)
    {
        foreach (var line in box.LineBoxes)
        {
            double bottom = double.MinValue;

            foreach (var word in line.Words)
            {
                if (word.OwnerBox != null && !IsInAbsposSubtree(word.OwnerBox, root))
                    bottom = Math.Max(bottom, word.Bottom);
            }

            foreach (var rect in line.Rectangles)
            {
                if (IsNestedAtomicInline(rect.Key, root))
                    bottom = Math.Max(bottom, rect.Value.Bottom);
            }

            if (bottom > double.MinValue && bottom >= lastBottom)
            {
                last = line;
                lastBottom = bottom;
            }
        }

        foreach (var child in box.Boxes)
        {
            if (child.Display == CssConstants.None
                || child.Position is CssConstants.Absolute or CssConstants.Fixed
                || child.Float != CssConstants.None
                || child.Display is CssConstants.InlineBlock or "inline-flex" or "inline-grid" or "inline-table")
            {
                continue;
            }

            FindLastLine(child, root, ref last, ref lastBottom);
        }
    }

    /// <summary>
    /// Whether the box is an atomic inline-level box in the normal flow inside
    /// <paramref name="root"/>, other than an image, whose line it is a word of.
    /// </summary>
    private static bool IsNestedAtomicInline(CssBox box, CssBox root) =>
        box != root
        && box.Display is CssConstants.InlineBlock or "inline-flex" or "inline-grid" or "inline-table"
        && !IsInAbsposSubtree(box, root);

    /// <summary>
    /// Fits the rectangle of each inline box on <paramref name="line"/> that holds no words of its
    /// own around what the boxes in it hold, where vertical alignment has put it.
    /// </summary>
    /// <remarks>
    /// Such a box's rectangle is what the flow found around its contents before the line's baseline
    /// moved them, and CssLineBox.SetBaseLine cannot place it from words it does not hold: it
    /// measured its offset from its first word, which the pass had already moved. A span around
    /// "a" beside an empty 30px inline-block stayed at the top of the line, and getBoundingClientRect,
    /// which unions these rectangles, reported the line's top, where the "a" was drawn 15.15px down
    /// and browsers report 15.
    /// </remarks>
    private static void FitWordlessInlineBoxes(CssLineBox line)
    {
        foreach (var box in new List<CssBox>(line.Rectangles.Keys))
            FitToContent(box, line);
    }

    /// <summary>
    /// The top and bottom of what <paramref name="box"/> holds on <paramref name="line"/>, fitting
    /// its rectangle around them first where it holds no words of its own; null when it has no
    /// rectangle on the line.
    /// </summary>
    private static (double Top, double Bottom)? FitToContent(CssBox box, CssLineBox line)
    {
        if (!line.Rectangles.TryGetValue(box, out RectangleF r))
            return null;

        // What a box's vertical padding and border enclose is its own; the box around it reaches
        // only as far as its content (see CssLineBox.UpdateRectangle). An atomic box counts whole.
        if (box.IsImage || box.Display != CssConstants.Inline)
            return (r.Top, r.Bottom);

        double topSpacing = box.ActualBorderTopWidth + box.ActualPaddingTop;
        double bottomSpacing = box.ActualBorderBottomWidth + box.ActualPaddingBottom;

        if (box.Words.Count > 0)
            return (r.Top + topSpacing, r.Bottom - bottomSpacing);

        double top = double.MaxValue, bottom = double.MinValue;

        foreach (var child in box.Boxes)
        {
            if (FitToContent(child, line) is not var (childTop, childBottom))
                continue;

            top = Math.Min(top, childTop);
            bottom = Math.Max(bottom, childBottom);
        }

        if (top > bottom)
            return (r.Top + topSpacing, r.Bottom - bottomSpacing);

        line.Rectangles[box] = RectangleF.FromLTRB(r.Left, (float)(top - topSpacing), r.Right, (float)(bottom + bottomSpacing));
        return (top, bottom);
    }

    /// <summary>
    /// Whether the box is an atomic inline-level box whose baseline is its bottom margin edge: an
    /// <c>inline-block</c>, or an inline replaced element (an image — the only replaced content that
    /// reaches a line box as a word of its own).
    /// </summary>
    private static bool IsAtomicInline(CssBox box) =>
        box.Display == CssConstants.InlineBlock || box.IsImage;

    /// <summary>
    /// Whether a <c>vertical-align</c> value positions the box against the <em>parent's font
    /// metrics</em> rather than against the line's baseline — <c>middle</c>, <c>text-top</c> and
    /// <c>text-bottom</c> (CSS2.1 §10.8.1). <c>top</c>/<c>bottom</c> align against the line box
    /// itself and are handled by their own second pass; every other value, including
    /// <c>sub</c>/<c>super</c> and a length, is an offset <em>from</em> the baseline and so does
    /// help establish it.
    /// </summary>
    private static bool IsAlignedToParentFontMetrics(string verticalAlign) =>
        verticalAlign == CssConstants.Middle
        || verticalAlign == CssConstants.TextTop
        || verticalAlign == CssConstants.TextBottom;

    /// <summary>
    /// Whether the box stands on the line's baseline: <c>vertical-align: baseline</c>, the initial
    /// value (CSS2.1 §10.8.1).
    /// </summary>
    private static bool IsBaselineAligned(CssBox box) =>
        string.IsNullOrEmpty(box.VerticalAlign) || box.VerticalAlign == CssConstants.Baseline;

    /// <summary>
    /// How far <c>vertical-align: sub</c> lowers the box's baseline below its parent's: a fifth of
    /// the parent's font size, and a pixel, as Chromium has it. CSS 2.1 §10.8.1 leaves "the proper
    /// position for subscripts of the parent's box" to the user agent, but it is the parent's font
    /// that sets it, not the box's own height: half the height of the box lowered a 60px
    /// inline-block by 30px, where browsers lower it by about 4px.
    /// </summary>
    private static double SubscriptShift(CssBox box) => ParentEmHeight(box) / 5 + 1;

    /// <summary>
    /// How far <c>vertical-align: super</c> raises the box's baseline above its parent's: a third
    /// of the parent's font size, and a pixel, as Chromium has it. A fifth of the box's own height
    /// raised a 10px inline-block by 2px, where browsers raise it by about 6px.
    /// </summary>
    private static double SuperscriptShift(CssBox box) => ParentEmHeight(box) / 3 + 1;

    /// <summary>The font size, in CSS pixels, of the box's parent, or of the box if it has none.</summary>
    private static double ParentEmHeight(CssBox box) => (box.ParentBox ?? box).GetEmHeight();

    /// <summary>
    /// How far a <c>&lt;length&gt;</c> or <c>&lt;percentage&gt;</c> value of
    /// <c>vertical-align</c> raises the box, a percentage being of the box's own line height
    /// (CSS 2.1 §10.8.1); 0 for any other value.
    /// </summary>
    private static double LengthRaise(CssBox box)
    {
        if (string.IsNullOrEmpty(box.VerticalAlign) || box.VerticalAlign == CssConstants.Baseline)
            return 0;

        double lineHeight = box.ActualLineHeight > 0 ? box.ActualLineHeight : box.ActualFont.Height;
        double offset = CssLengthParser.ParseLength(box.VerticalAlign, lineHeight, box.GetEmHeight());
        return double.IsNaN(offset) ? 0 : offset;
    }

    /// <summary>
    /// How far below the line's baseline the baseline of the box's parent lies: each inline box
    /// around the box, up to the block, that <c>sub</c>, <c>super</c> or a length lowers or raises
    /// from its own parent's baseline moves it by as much, and so does one that <c>middle</c>,
    /// <c>text-top</c> or <c>text-bottom</c> aligns to its parent's font (ParentFontShift). One
    /// aligned <c>top</c> or <c>bottom</c> moves it by nothing here: it is aligned to the line box
    /// with what is in it once the rest of the line is (<see cref="LineBoxAlignedRoot"/>).
    /// </summary>
    private static double ParentBaselineShift(CssBox box)
    {
        double shift = 0;
        for (var parent = box.ParentBox; parent is { Display: CssConstants.Inline }; parent = parent.ParentBox)
            shift += BaselineShift(parent);

        return shift;
    }

    /// <summary>
    /// How far below its parent's baseline the box's own <c>vertical-align</c> puts its baseline:
    /// <c>sub</c> lowers it and <c>super</c> raises it, a length raises it by as much, and an inline
    /// box aligned to its parent's font moves it as <see cref="ParentFontShift"/> does. 0 for a box
    /// on the baseline, or aligned <c>top</c> or <c>bottom</c>.
    /// </summary>
    private static double BaselineShift(CssBox box) => box.VerticalAlign switch
    {
        CssConstants.Sub => SubscriptShift(box),
        CssConstants.Super => -SuperscriptShift(box),
        CssConstants.Middle or CssConstants.TextTop or CssConstants.TextBottom => ParentFontShift(box),
        _ => -LengthRaise(box),
    };

    /// <summary>
    /// How far below its parent's baseline an inline, non-replaced box that <c>middle</c>,
    /// <c>text-top</c> or <c>text-bottom</c> aligns to its parent's font puts its own baseline; 0
    /// for any other box.
    /// </summary>
    /// <remarks>
    /// CSS 2.1 §10.8.1 aligns an inline, non-replaced box by its line height: its glyphs with half
    /// its leading above them and the rest below (<see cref="HalfLeading"/>,
    /// <see cref="LeadingBelow"/>). <c>text-top</c> puts the top of that at the top of the parent's
    /// content area, its font's ascent above its baseline; <c>text-bottom</c> puts the bottom at the
    /// bottom of it, its font's descent below; <c>middle</c> puts its middle half the parent's
    /// x-height above the parent's baseline, taken as a quarter of the parent's font height as for
    /// an atomic box.
    /// </remarks>
    private static double ParentFontShift(CssBox box)
    {
        if (!box.IsInlineNonReplaced || !IsAlignedToParentFontMetrics(box.VerticalAlign))
            return 0;

        double parentFontHeight = (box.ParentBox ?? box).ActualFont.Height;
        double fontHeight = box.ActualFont.Height;
        double above = fontHeight * TypicalAscentRatio + HalfLeading(box);
        double below = fontHeight * (1.0 - TypicalAscentRatio) + LeadingBelow(box);

        return box.VerticalAlign switch
        {
            CssConstants.TextTop => above - parentFontHeight * TypicalAscentRatio,
            CssConstants.TextBottom => parentFontHeight * (1.0 - TypicalAscentRatio) - below,
            _ => (above - below) / 2 - parentFontHeight / 4,
        };
    }

    /// <summary>
    /// The height of the font of the box's parent, whose content area <c>text-top</c> and
    /// <c>text-bottom</c> align the box to: an inline box around it, or the block the line is in.
    /// </summary>
    private static double ParentFontHeightOf(CssBox box, CssLineBox lineBox) =>
        (box.ParentBox ?? lineBox.OwnerBox)?.ActualFont.Height ?? 0;

    /// <summary>
    /// The inline boxes on <paramref name="lineBox"/> that hold no text on it, only images or boxes
    /// placed on it whole: the inline boxes around those, up to the block, that no word of text on
    /// the line is in.
    /// </summary>
    /// <remarks>
    /// CSS 2.1 §10.8.1 makes every inline box as tall as its line height around its own font,
    /// whatever it holds, and the line box holds them all. One holding text reaches as far as its
    /// words do, the leading around them with them (<see cref="WordLayoutTop"/>,
    /// <see cref="WordLayoutBottom"/>); one of these reaches as far as its own strut does
    /// (<see cref="StrutOnlyExtent"/>).
    /// </remarks>
    private static List<CssBox> StrutOnlyInlineBoxes(CssLineBox lineBox)
    {
        var block = lineBox.OwnerBox;
        var holdingText = new HashSet<CssBox>();

        foreach (var word in lineBox.Words)
        {
            if (word.IsImage)
                continue;

            for (var box = word.OwnerBox; box != null && box != block && box.IsInlineNonReplaced; box = box.ParentBox)
                holdingText.Add(box);
        }

        var boxes = new List<CssBox>();
        foreach (var atomic in lineBox.Rectangles.Keys)
        {
            if (atomic.IsInlineNonReplaced || atomic.IsBrElement || IsInAbsposSubtree(atomic, block))
                continue;

            for (var box = atomic.ParentBox; box != null && box != block && box.IsInlineNonReplaced; box = box.ParentBox)
            {
                if (!holdingText.Contains(box) && !boxes.Contains(box))
                    boxes.Add(box);
            }
        }

        return boxes;
    }

    /// <summary>
    /// Where the inline box of <paramref name="box"/>, an inline box holding no text on its line
    /// (<see cref="StrutOnlyInlineBoxes"/>), starts and ends: its line height around its own font's
    /// glyphs, half its leading above them and the rest below, on its own baseline, which the inline
    /// boxes around it and its own alignment move off the line's.
    /// </summary>
    private static (double Top, double Bottom) StrutOnlyExtent(CssBox box, double lineBaseline)
    {
        double baseline = lineBaseline + ParentBaselineShift(box) + BaselineShift(box);
        double fontHeight = box.ActualFont.Height;

        return (baseline - fontHeight * TypicalAscentRatio - HalfLeading(box),
            baseline + fontHeight * (1.0 - TypicalAscentRatio) + LeadingBelow(box));
    }

    /// <summary>
    /// The tallest line height among the inline boxes around <paramref name="box"/>, up to
    /// <paramref name="blockbox"/>; 0 where there are none.
    /// </summary>
    private static double InlineBoxesLineHeight(CssBox box, CssBox blockbox)
    {
        double lineHeight = 0;
        for (var parent = box.ParentBox; parent != null && parent != blockbox && parent.IsInlineNonReplaced; parent = parent.ParentBox)
            lineHeight = Math.Max(lineHeight, parent.ActualLineHeight > 0 ? parent.ActualLineHeight : parent.ActualFont.Height);

        return lineHeight;
    }

    /// <summary>
    /// The box aligned <c>top</c> or <c>bottom</c> whose aligned subtree the box is in on a line of
    /// <paramref name="block"/>: the box itself, or the nearest inline box around it, up to the
    /// block, that is aligned so; null for a box in none.
    /// </summary>
    /// <remarks>
    /// CSS 2.1 §10.8.1: the aligned subtree of an inline element is the element and the aligned
    /// subtrees of its inline children not aligned <c>top</c> or <c>bottom</c> themselves, which
    /// are aligned subtrees of their own.
    /// </remarks>
    private static CssBox? LineBoxAlignedRoot(CssBox? box, CssBox? block)
    {
        for (var b = box; b != null && b != block; b = b.ParentBox)
        {
            if (b.VerticalAlign is CssConstants.Top or CssConstants.Bottom)
                return b;

            if (b.ParentBox is not { Display: CssConstants.Inline })
                break;
        }

        return null;
    }

    /// <summary>
    /// Where the aligned subtree of <paramref name="root"/>, a box aligned <c>top</c> or
    /// <c>bottom</c>, starts and ends on <paramref name="lineBox"/>: a box placed on the line whole
    /// is its rectangle, and an inline box reaches as far as the words and whole boxes in its
    /// subtree, each word with the leading around it (<see cref="WordLayoutTop"/>,
    /// <see cref="WordLayoutBottom"/>). <see cref="double.MaxValue"/> and
    /// <see cref="double.MinValue"/> for an inline box with nothing in its subtree on the line.
    /// </summary>
    private static (double Top, double Bottom) AlignedSubtreeExtent(CssLineBox lineBox, CssBox root)
    {
        if (!root.IsInlineNonReplaced)
        {
            var rect = lineBox.Rectangles[root];
            return (rect.Top, rect.Bottom);
        }

        double top = double.MaxValue;
        double bottom = double.MinValue;

        foreach (var (box, rect) in lineBox.Rectangles)
        {
            if (!box.IsInlineNonReplaced && LineBoxAlignedRoot(box, lineBox.OwnerBox) == root)
            {
                top = Math.Min(top, rect.Top);
                bottom = Math.Max(bottom, rect.Bottom);
            }
        }

        foreach (var word in lineBox.Words)
        {
            if (LineBoxAlignedRoot(word.OwnerBox, lineBox.OwnerBox) == root)
            {
                top = Math.Min(top, WordLayoutTop(word));
                bottom = Math.Max(bottom, WordLayoutBottom(word));
            }
        }

        return (top, bottom);
    }

    /// <summary>
    /// Moves the aligned subtree of <paramref name="root"/>, an inline box aligned <c>top</c> or
    /// <c>bottom</c>, down <paramref name="lineBox"/> by <paramref name="shift"/>: the boxes placed
    /// on the line whole with what is in them, and the words of the inline boxes in it.
    /// </summary>
    /// <remarks>
    /// An inline box's words are moved as vertical alignment moves them everywhere else, through
    /// <see cref="CssLineBox.SetBaseLine"/>, which takes the box's rectangle with them as far as it
    /// takes any inline box's.
    /// </remarks>
    private static void ShiftAlignedSubtree(CssLineBox lineBox, CssBox root, double shift)
    {
        foreach (var box in new List<CssBox>(lineBox.Rectangles.Keys))
        {
            if (LineBoxAlignedRoot(box, lineBox.OwnerBox) != root)
                continue;

            var words = lineBox.WordsOf(box);

            if (box.IsInlineNonReplaced)
            {
                if (words.Count > 0)
                    lineBox.SetBaseLine(box, words[0].Top + shift);

                continue;
            }

            ShiftOnLine(lineBox, box, shift);

            foreach (var word in words)
                word.Top += shift;
        }
    }

    /// <summary>
    /// Whether an inline box around the box, up to the block, is aligned to its parent's font
    /// (<see cref="IsAlignedToParentFontMetrics"/>).
    /// </summary>
    private static bool IsInsideBoxAlignedToParentFont(CssBox box)
    {
        for (var parent = box.ParentBox; parent is { Display: CssConstants.Inline }; parent = parent.ParentBox)
        {
            if (IsAlignedToParentFontMetrics(parent.VerticalAlign))
                return true;
        }

        return false;
    }

    private static void ApplyVerticalAlignment(CssLineBox lineBox)
    {
        // CSS 2.1 §10.8: The baseline is where text sits, approximated as
        // the top of each box plus the font ascent. Most Latin fonts have
        // an ascent/height ratio near 0.8 (e.g. OS/2 sTypoAscender is
        // typically ~80% of UPM). This matches common browser heuristics.
        const double TypicalAscentRatio = 0.8;

        // CSS2.1 §10.8.1: Boxes with vertical-align:top or bottom do not
        // contribute to the initial line box height calculation.  Collect
        // them for a second positioning pass.
        var topBottomBoxes = new HashSet<CssBox>();
        foreach (var box in lineBox.Rectangles.Keys)
        {
            if (box.VerticalAlign == CssConstants.Top || box.VerticalAlign == CssConstants.Bottom)
                topBottomBoxes.Add(box);
        }

        // CSS2.1 §10.8: The "strut" — an imaginary zero-width inline box
        // with the block container's font and line-height — establishes
        // the initial baseline of the line box.  This is critical when the
        // parent has font-size: 0 (e.g. .buckets { font: 0/0 }): the strut
        // baseline is at the top of the content area and must not be
        // overridden by child inline-block font metrics.
        double lineTop = double.MaxValue;
        double inlineBoxTop = double.MaxValue;
        foreach (var kvp in lineBox.Rectangles)
        {
            if (topBottomBoxes.Contains(kvp.Key))
                continue;

            // An inline box's padding and border are not part of the line (CSS2.1 §10.6.1), and
            // its rectangle reaches above the line by them: the strut stood that much higher.
            if (kvp.Key.IsInlineNonReplaced)
                inlineBoxTop = Math.Min(inlineBoxTop, kvp.Value.Top);
            else
                lineTop = Math.Min(lineTop, kvp.Value.Top);
        }

        // The strut stands at the top of the line, where the flow put the line. An image stands
        // on the strut's baseline from the flow, below the line's top, and alone on its line it
        // put the baseline as far below again: at 16px/20px, a 10px image was 9.7px down its line
        // and the line 29.7px tall, where browsers put it 5px down a 20px line.
        //
        // A line holding only boxes aligned `top` or `bottom` has its strut as well, and they are
        // aligned to the line box it makes. It had none, and a 10px box aligned `bottom` alone in
        // 16px/20px text stood at the top of its line, where browsers put it 10px down.
        bool holdsLineBoxAligned = false;
        foreach (var box in topBottomBoxes)
            holdsLineBoxAligned |= !IsInAbsposSubtree(box, lineBox.OwnerBox);

        if ((lineTop < double.MaxValue || inlineBoxTop < double.MaxValue || holdsLineBoxAligned)
            && lineBox.FlowTop is double flowTop)
        {
            lineTop = Math.Min(lineTop, flowTop);
        }

        if (lineTop == double.MaxValue)
            lineTop = inlineBoxTop;

        // Start with the strut baseline: the block's font ascent below the line's top, and half
        // its leading above that. CSS 2.1 §10.8.1 gives the strut, as every inline box, its line
        // height with half the leading above its glyphs and half below; the glyphs stood at the
        // line's top, and a 16px letter in a 60px line was drawn at its top, where browsers draw
        // it 21px down.
        double parentFontHeight = lineBox.OwnerBox?.ActualFont.Height ?? 0;
        double baseline = (lineTop < double.MaxValue)
            ? lineTop + (lineBox.OwnerBox is { } owner ? HalfLeading(owner) : 0) + parentFontHeight * TypicalAscentRatio
            : float.MinValue;

        // Non-inline-block boxes also contribute to the baseline — but only the ones that are
        // aligned *to* it. CSS2.1 §10.8.1: a box aligned `middle`, `text-top` or `text-bottom` is
        // positioned from the parent's font metrics, not from the line's baseline, so where its
        // own baseline would fall says nothing about where the line's is. Letting it push the
        // baseline down is self-defeating: the box is then placed relative to the baseline it just
        // moved, so it drives itself away from where it belongs. An `<img>` is the visible case,
        // because its baseline is its bottom edge — a `vertical-align: middle` image in a short
        // line put the baseline a whole image-height down and then centred the image on *that*,
        // leaving half an image of blank space above it. That is the band above the MediaWiki
        // thumbnail: the skin sets `line-height: 0` on the figure and `vertical-align: middle` on
        // the image, so the strut contributed nothing and the image's own bottom became the
        // baseline.
        //
        // An inline-block aligned to the baseline counts as well. One with a line of text in it
        // counts as the text does: its last line's baseline is its baseline. One whose baseline is
        // its bottom margin edge (CssBox.UsesBottomMarginEdgeBaseline) counts as an image does,
        // with that edge. Where either stands in the flow, at the line's top, its baseline can only
        // be at or below the strut's, so it can only move the line's baseline down, and the text
        // beside it with it, never raise the box above the line. One raised or lowered from the
        // baseline is left out, as every inline-block was, and placed from the baseline the rest of
        // the line sets.
        //
        // What is inside an inline box aligned to its parent's font stands where that box puts it,
        // and says no more about where the line's baseline is than the box does. Nor does what is
        // inside one aligned `top` or `bottom`, which is aligned to the line box with it: the text
        // of a span with `line-height: 40px` aligned `top` in 16px/20px text set the baseline half
        // its leading down, and the rest of the line stood beside it, 10px down, where browsers
        // leave it at the line's top.
        //
        // An inline box holding no text on the line counts below, by its own strut: its rectangle
        // here is the image's in it, if any, which the flow stands below the line's top.
        var strutOnly = StrutOnlyInlineBoxes(lineBox);

        foreach (var box in lineBox.Rectangles.Keys)
        {
            if (topBottomBoxes.Contains(box)
                || IsAlignedToParentFontMetrics(box.VerticalAlign)
                || IsInsideBoxAlignedToParentFont(box)
                || (topBottomBoxes.Count > 0 && LineBoxAlignedRoot(box, lineBox.OwnerBox) != null)
                || strutOnly.Contains(box))
            {
                continue;
            }

            bool bottomEdge = box.UsesBottomMarginEdgeBaseline;
            if (box.Display == CssConstants.InlineBlock
                && (!IsBaselineAligned(box) || (!bottomEdge && LastLineBaseline(box) == null)))
            {
                continue;
            }

            // An inline box's text stands half its own leading below the line's top too, where
            // the flow put its words.
            double boxBaseline = lineBox.Rectangles[box].Top + BaselineAscentOf(box, lineBox)
                + (bottomEdge ? box.ActualMarginBottom : 0)
                + (box.IsInlineNonReplaced ? HalfLeading(box) : 0);

            // A box lowered from the baseline, by its own alignment or by an inline box around it,
            // stands as much below where the flow put it, and starts at the line's top with the
            // baseline as much higher. Put where it would stand unlowered, the baseline was as
            // much too low: a span with `line-height: 40px` lowered 10px in 16px/20px text made a
            // 50px line, where browsers make it 40px. A box raised stands above the line's top,
            // and moves the line down after (CreateLineBoxes).
            boxBaseline -= Math.Max(0, ParentBaselineShift(box) + BaselineShift(box));
            baseline = Math.Max(baseline, boxBaseline);
        }

        // CSS 2.1 §10.8.1: an inline box is as tall as its line height around its own font whatever
        // it holds, and its strut stands at the line's top as the text of one holding text does. One
        // holding only an image or an inline-block counted for nothing, or as though its text started
        // where the image does: a span with `line-height: 40px` around an inline-block made a 20px
        // line of 16px/20px text, where browsers make it 40px.
        if (lineTop < double.MaxValue)
        {
            foreach (var box in strutOnly)
            {
                if (IsAlignedToParentFontMetrics(box.VerticalAlign)
                    || IsInsideBoxAlignedToParentFont(box)
                    || LineBoxAlignedRoot(box, lineBox.OwnerBox) != null)
                {
                    continue;
                }

                double boxBaseline = lineTop + HalfLeading(box) + box.ActualFont.Height * TypicalAscentRatio
                    - Math.Max(0, ParentBaselineShift(box) + BaselineShift(box));
                baseline = Math.Max(baseline, boxBaseline);
            }
        }

        // CSS2.1 §10.8.1: an atomic inline-block's baseline is its bottom margin edge, so one aligned
        // to the baseline stands on the line's baseline with that edge, as an image does, and the
        // text beside it stands level with it: two of different heights have their bottoms flush,
        // and the words beside them stand on the same line as their bottoms. They were once left
        // where the flow put them, at the top of the line; then stood on the lowest bottom among
        // them, with the text left at the top, because the strut's baseline, measured with a font
        // height a third too large, sat well below the text and would have pushed them down past
        // it. With the font's height taken as it is, the line's baseline is where the text stands.
        //
        // It is the *margin* box that stands on the baseline, and the rectangle here is the border
        // box, so the bottom margin is added on both sides.

        lineBox.Baseline = baseline > float.MinValue ? baseline : null;

        // --- Phase 1: Position all non-top/bottom boxes ---
        var boxes = new List<CssBox>(lineBox.Rectangles.Keys);
        foreach (CssBox box in boxes)
        {
            if (topBottomBoxes.Contains(box))
                continue;

            // CSS 2.1 §10.8.1: a box is aligned against its parent's baseline, and an inline box
            // raised or lowered from its own parent's carries that baseline, and what stands on it,
            // with it. The text of an element is in an inline box of its own, aligned to the
            // baseline, so aligning the element's box alone moved nothing: the text of every <sup>
            // and <sub> stayed on the line's baseline.
            double parentBaseline = baseline + ParentBaselineShift(box);

            if (IsBaselineAligned(box)
                && box.UsesBottomMarginEdgeBaseline
                && baseline > float.MinValue)
            {
                lineBox.SetBaseLine(box,
                    parentBaseline - box.ActualMarginBottom - lineBox.Rectangles[box].Height);
                continue;
            }

            // For inline text boxes, SetBaseLine receives the desired
            // word-top position, so baseline-relative values must be
            // converted from baseline Y to word-top Y by subtracting
            // the box's ascent.
            //
            // For inline-block and inline replaced boxes, CSS 2.1 §10.8.1: the
            // baseline of an inline-block with no in-flow line boxes, and of an
            // inline replaced element, is the bottom margin edge.  SetBaseLine
            // positions the box by its top, so we must subtract the box height to
            // convert from the desired bottom-edge position to the top-edge position.
            double boxAscent = BaselineAscentOf(box, lineBox);

            // An inline, non-replaced box aligned to its parent's font is aligned by its line
            // height, not by its glyphs or its padding box (ParentFontShift), and its text, in an
            // inline box of its own, stands on the baseline that puts it on. The switch below
            // aligned the box alone, which holds no words, so its text stayed on the line's
            // baseline: the text of a span aligned `text-bottom` stood where one on the baseline
            // does.
            if (box.IsInlineNonReplaced && IsAlignedToParentFontMetrics(box.VerticalAlign))
            {
                if (baseline > float.MinValue)
                    lineBox.SetBaseLine(box, parentBaseline + ParentFontShift(box) - boxAscent);

                continue;
            }

            //Important notes on http://www.w3.org/TR/CSS21/tables.html#height-layout
            switch (box.VerticalAlign)
            {
                case CssConstants.Sub:
                    lineBox.SetBaseLine(box, parentBaseline - boxAscent + SubscriptShift(box));
                    break;

                case CssConstants.Super:
                    lineBox.SetBaseLine(box, parentBaseline - boxAscent - SuperscriptShift(box));
                    break;

                case CssConstants.TextTop:
                    // CSS 2.1 §10.8.1: Align the top of the box with the
                    // top of the parent element's content area (font top).
                    //
                    // The parent's font, not the block's: in a 32px span in 16px text, an image
                    // aligned `text-top` stood at the top of a 16px letter, 12.8px below the
                    // span's, where browsers put it level with the span's letters.
                    if (baseline > float.MinValue)
                    {
                        double parentContentTop = parentBaseline - ParentFontHeightOf(box, lineBox) * TypicalAscentRatio;
                        lineBox.SetBaseLine(box, parentContentTop);
                    }
                    break;

                case CssConstants.TextBottom:
                    // CSS 2.1 §10.8.1: Align the bottom of the box with the
                    // bottom of the parent element's content area (font bottom),
                    // the parent's font's, as for `text-top`.
                    if (baseline > float.MinValue && lineBox.Rectangles.TryGetValue(box, out RectangleF value))
                    {
                        double boxHeight = value.Height;
                        double parentContentBottom = parentBaseline + ParentFontHeightOf(box, lineBox) * (1.0 - TypicalAscentRatio);
                        lineBox.SetBaseLine(box, parentContentBottom - boxHeight);
                    }
                    break;

                case CssConstants.Middle:
                    // CSS 2.1 §10.8.1: Align the vertical midpoint of the box
                    // with the baseline plus half the x-height of the parent.
                    // x-height ≈ 0.5 × font height for Latin fonts; half of
                    // that is 0.25 × font height.
                    //
                    // "Plus" raises, as it does for a length: the midpoint is half an x-height above
                    // the baseline, where y is smaller. The half x-height was added to y, which put
                    // the midpoint as far below the baseline instead, so an icon aligned middle beside
                    // text hung below the text rather than centring on its lowercase letters.
                    if (lineBox.Rectangles.TryGetValue(box, out RectangleF value1) && baseline > float.MinValue)
                    {
                        double boxHeight = value1.Height;
                        double parentFont = box.ParentBox?.ActualFont.Height ?? 0;
                        double halfXHeight = parentFont * 0.25;
                        lineBox.SetBaseLine(box, parentBaseline - halfXHeight - boxHeight / 2);
                    }
                    break;

                default:
                    // CSS 2.1 §10.8.1: A <length> or <percentage> value
                    // raises (positive) or lowers (negative) the box by
                    // the given distance relative to the baseline.
                    // A percentage is calculated against the line-height
                    // of the element itself.
                    double offset = LengthRaise(box);
                    if (offset != 0)
                    {
                        // Positive values move the box UP (raise).
                        lineBox.SetBaseLine(box, parentBaseline - boxAscent - offset);
                        break;
                    }

                    //case: baseline
                    lineBox.SetBaseLine(box, parentBaseline - boxAscent);
                    break;
            }
        }

        // --- Phase 2: Position top/bottom-aligned boxes ---
        // CSS 2.1 §10.8.1: After all other boxes are positioned, compute
        // the final line box extent and align top/bottom boxes within it.
        if (topBottomBoxes.Count > 0)
        {
            double finalTop = double.MaxValue;
            double finalBottom = double.MinValue;

            // An inline, non-replaced box reaches as far as its words, which the next loop
            // measures: its vertical padding and border are not part of the line (CSS2.1 §10.6.1).
            foreach (var kvp in lineBox.Rectangles)
            {
                if (!kvp.Key.IsInlineNonReplaced && LineBoxAlignedRoot(kvp.Key, lineBox.OwnerBox) == null)
                {
                    finalTop = Math.Min(finalTop, kvp.Value.Top);
                    finalBottom = Math.Max(finalBottom, kvp.Value.Bottom);
                }
            }

            // A word of text reaches as far as the inline box it stands in, the leading above and
            // below its glyphs (WordLayoutTop, WordLayoutBottom), and the strut stands on the
            // baseline as far above and below it as its line height (CSS 2.1 §10.8.1). The glyphs
            // alone were measured: a 30px box aligned `bottom` beside 16px/20px text ended where
            // the glyphs do, 1.44px above the bottom of the strut, which the line then reached
            // below the box.
            foreach (var word in lineBox.Words)
            {
                if (LineBoxAlignedRoot(word.OwnerBox, lineBox.OwnerBox) == null)
                {
                    finalTop = Math.Min(finalTop, WordLayoutTop(word));
                    finalBottom = Math.Max(finalBottom, WordLayoutBottom(word));
                }
            }

            if (baseline > float.MinValue && lineBox.OwnerBox is { ActualLineHeight: > 0 } strutBox)
            {
                finalTop = Math.Min(finalTop, baseline - parentFontHeight * TypicalAscentRatio - HalfLeading(strutBox));
                finalBottom = Math.Max(finalBottom, baseline + StrutDescent(strutBox));
            }

            // An inline box holding no text on the line reaches as far as its own strut.
            if (baseline > float.MinValue)
            {
                foreach (var box in strutOnly)
                {
                    if (LineBoxAlignedRoot(box, lineBox.OwnerBox) != null)
                        continue;

                    var (strutTop, strutBottom) = StrutOnlyExtent(box, baseline);
                    finalTop = Math.Min(finalTop, strutTop);
                    finalBottom = Math.Max(finalBottom, strutBottom);
                }
            }

            if (finalTop < double.MaxValue)
            {
                // A box aligned `top` or `bottom` is aligned with its aligned subtree, what is in it
                // but for what is aligned so itself, to the top or bottom of the line box
                // (CSS 2.1 §10.8.1); the box alone was moved, and an inline box holds no words, its
                // text being in an inline box of its own: the text of a span aligned `top` beside a
                // 40px inline-block stood on the baseline, 25.15px down the line, where browsers
                // put it at the top. The line box is as tall as the tallest subtree, where that is
                // taller than the rest of the line: a subtree aligned `top` makes it reach lower,
                // and one aligned `bottom` higher. Each was aligned to the line as the rest of it
                // reached, and a box aligned `bottom` beside a taller one aligned `top` ended at the
                // bottom of the text, not of the line.
                var subtrees = new List<(CssBox Root, double Top, double Bottom)>();

                foreach (CssBox box in boxes)
                {
                    if (!topBottomBoxes.Contains(box))
                        continue;

                    var (top, bottom) = AlignedSubtreeExtent(lineBox, box);
                    if (top == double.MaxValue)
                        continue;

                    subtrees.Add((box, top, bottom));

                    double grow = bottom - top - (finalBottom - finalTop);
                    if (grow > 0)
                    {
                        if (box.VerticalAlign == CssConstants.Top)
                            finalBottom += grow;
                        else
                            finalTop -= grow;
                    }
                }

                foreach (var (root, top, bottom) in subtrees)
                {
                    double shift = root.VerticalAlign == CssConstants.Top ? finalTop - top : finalBottom - bottom;

                    if (root.IsInlineNonReplaced)
                        ShiftAlignedSubtree(lineBox, root, shift);
                    else
                        lineBox.SetBaseLine(root, top + shift);
                }
            }
        }
    }

    private static void ApplyJustifyAlignment(CssLineBox lineBox)
    {
        // Whether the block's last line is justified is decided by the caller
        // (ApplyHorizontalAlignment): under a plain text-align:justify the last
        // line is routed to 'start' and never reaches here, so this method
        // unconditionally stretches whatever line it is given.  A single-word line
        // has nothing to stretch and is left flush at the line start below.
        double indent = lineBox.Equals(lineBox.OwnerBox.LineBoxes[0]) ? lineBox.OwnerBox.ActualTextIndent : 0f;
        double textSum = 0f;
        double words = 0f;
        double availWidth = lineBox.OwnerBox.ClientRectangle.Width - indent;

        // Gather text sum
        foreach (CssRect w in lineBox.Words)
        {
            textSum += w.Width;
            words += 1f;
        }

        if (words <= 0f)
            return; //Avoid Zero division

        double spacing = (availWidth - textSum) / words; //Spacing that will be used
        double curx = lineBox.OwnerBox.ClientLeft + indent;

        // A line with a single word (common on a justify-all / text-align-last:justify
        // last line) has no inter-word gaps to stretch: it stays flush at the start
        // edge rather than being pushed to the right edge.
        bool stretch = lineBox.Words.Count > 1;

        foreach (CssRect word in lineBox.Words)
        {
            word.Left = curx;
            curx = word.Right + spacing;

            if (stretch && word == lineBox.Words[^1])
                word.Left = lineBox.OwnerBox.ClientRight - word.Width;
        }
    }

    private static void ApplyCenterAlignment(CssLineBox line)
    {
        if (line.Words.Count == 0 && line.Rectangles.Count == 0)
            return;

        double right = line.OwnerBox.ActualRight - line.OwnerBox.ActualPaddingRight - line.OwnerBox.ActualBorderRightWidth;

        // Lines may contain only inline-block elements (e.g. form controls inside
        // <center>) with no direct text words.
        double contentRight = AlignedContentRight(line);

        double diff = (right - contentRight) / 2;

        if (diff <= 0)
            return;

        foreach (CssRect word in line.Words)
            word.Left += diff;

        foreach (CssBox b in ToList(line.Rectangles.Keys))
        {
            RectangleF r = line.Rectangles[b];
            line.Rectangles[b] = new RectangleF((float)(r.X + diff), r.Y, r.Width, r.Height);
            ShiftInlineBlockBox(b, diff);
        }
    }

    private static void ApplyRightAlignment(CssLineBox line)
    {
        if (line.Words.Count == 0 && line.Rectangles.Count == 0)
            return;

        double right = line.OwnerBox.ActualRight - line.OwnerBox.ActualPaddingRight - line.OwnerBox.ActualBorderRightWidth;
        double contentRight = AlignedContentRight(line);

        double diff = right - contentRight;

        if (diff <= 0)
            return;

        foreach (CssRect word in line.Words)
            word.Left += diff;

        foreach (CssBox b in ToList(line.Rectangles.Keys))
        {
            RectangleF r = line.Rectangles[b];
            line.Rectangles[b] = new RectangleF((float)(r.X + diff), r.Y, r.Width, r.Height);
            ShiftInlineBlockBox(b, diff);
        }
    }

    /// <summary>
    /// Where what is on <paramref name="line"/> ends, for aligning it: the rightmost of its last word
    /// with the right padding, border and margin of each box it ends, and the right margin edge of
    /// each box placed on the line whole.
    /// </summary>
    /// <remarks>
    /// CSS 2.1 §16.2 aligns the line's inline-level boxes within the line box, each with its margins,
    /// as the flow placed them; a line they overflow is not aligned (CSS Text 3 §7.1). The right
    /// margin of what ends the line was left out, so a centred line stood half that margin too far to
    /// the right and a right-aligned one the whole of it: an image with <c>margin-right: 20px</c>
    /// ended at the right of a right-aligned line, where browsers end it 20px from it, and an image
    /// whose margin box is 2px wider than its centred line was moved 0.5px along it, where browsers
    /// start it at the line's start.
    /// </remarks>
    private static double AlignedContentRight(CssLineBox line)
    {
        double contentRight = 0;
        if (line.Words.Count > 0)
        {
            CssRect lastWord = line.Words[^1];
            contentRight = lastWord.Right;

            // The box the word is in, an image's own box or the text's, and each inline box around
            // it that ends with it, close after it as the flow closed them.
            for (var box = lastWord.OwnerBox; box != null && box != line.OwnerBox; box = box.ParentBox)
            {
                bool endsHere = box.Words.Count > 0
                    ? ReferenceEquals(box.Words[^1], lastWord)
                    : ReferenceEquals(box.LastHostingLineBox, line);

                if (!endsHere || !(box.IsImage || box.Display == CssConstants.Inline))
                    break;

                contentRight += box.ActualPaddingRight + box.ActualBorderRightWidth + RightMargin(box);
            }
        }

        foreach (var (box, rect) in line.Rectangles)
        {
            double margin = !box.IsInlineNonReplaced || box.LastHostingLineBox == line ? RightMargin(box) : 0;
            contentRight = Math.Max(contentRight, rect.Right + margin);
        }

        return contentRight;
    }

    /// <summary>The box's right margin, 0 where it has none to speak of.</summary>
    private static double RightMargin(CssBox box) =>
        double.IsNaN(box.ActualMarginRight) ? 0 : box.ActualMarginRight;

    /// <summary>
    /// Shifts an atomic inline-level box — inline-block, inline-flex, inline-grid or inline-table —
    /// and all its descendant boxes horizontally.
    /// Called by <see cref="ApplyCenterAlignment"/> and <see cref="ApplyRightAlignment"/>
    /// to ensure the box's actual <see cref="CssBox.Location"/> matches the shifted
    /// line-box rectangle, so background, border, and child content paint at the
    /// correct position.  CSS 2.1 §9.4.2.
    /// </summary>
    /// <remarks>
    /// It moved only an inline-block. Every atomic inline-level box is laid out inside at its own
    /// <see cref="CssBox.Location"/>, and its text and its rounded clip paint from there, while its
    /// background and border paint from the line rectangle the alignment had moved. A centred
    /// <c>inline-flex</c> button therefore had its fill moved and its text left behind: Google's
    /// consent buttons, white text on a blue pill, showed a sliver of blue and white text on white.
    /// </remarks>
    private static void ShiftInlineBlockBox(CssBox b, double dx)
    {
        if (!CssBoxHelper.IsAtomicInlineLevel(b.Display))
            return;

        b.Location = new PointF((float)(b.Location.X + dx), b.Location.Y);

        // Shift all descendant boxes so child content (text, nested boxes)
        // renders at the correct position.
        ShiftDescendantBoxes(b, dx);

        // Shift rectangles already assigned to the inline-block from its own
        // line boxes (via BubbleRectangles + AssignRectanglesToBoxes that ran
        // before centering).  Without this, the FragmentTreeBuilder captures
        // stale InlineRects at the original position, causing double borders.
        ShiftAssignedRectangles(b, dx);
    }

    private static void ShiftDescendantBoxes(CssBox parent, double dx)
    {
        // Each word is shifted through the box that owns it, which holds it whether or not a line
        // box of this subtree does. A grid item's words are in no line box of the grid's, so an
        // inline-grid moved and left its text behind when words were shifted through line boxes.
        // Every word has one owner and every box is visited once, so none moves twice.
        foreach (var word in parent.Words)
            word.Left += dx;

        foreach (var child in parent.Boxes)
        {
            child.Location = new PointF((float)(child.Location.X + dx), child.Location.Y);

            // Shift rectangles already assigned to this child.
            ShiftAssignedRectangles(child, dx);

            ShiftDescendantBoxes(child, dx);
        }

        // Shift the rectangles within this box's own line boxes.
        foreach (var lineBox in parent.LineBoxes)
        {
            foreach (var key in ToList(lineBox.Rectangles.Keys))
            {
                var r = lineBox.Rectangles[key];
                lineBox.Rectangles[key] = new RectangleF((float)(r.X + dx), r.Y, r.Width, r.Height);
            }
        }
    }

    /// <summary>
    /// Shifts all per-line-box rectangles that have been assigned to a box
    /// (via <see cref="CssLineBox.AssignRectanglesToBoxes"/>) by <paramref name="dx"/>
    /// pixels horizontally.
    /// </summary>
    private static void ShiftAssignedRectangles(CssBox box, double dx)
    {
        if (box.Rectangles.Count == 0)
            return;

        foreach (var key in ToList(box.Rectangles.Keys))
        {
            var r = box.Rectangles[key];
            box.Rectangles[key] = new RectangleF((float)(r.X + dx), r.Y, r.Width, r.Height);
        }
    }

    /// <summary>
    /// todo: optimizate, not creating a list each time
    /// </summary>
    private static List<T> ToList<T>(IEnumerable<T> collection)
    {
        List<T> result = [.. collection];
        return result;
    }
}

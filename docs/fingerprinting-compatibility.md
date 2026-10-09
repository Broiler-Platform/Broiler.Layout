# Sannysoft/CreepJS layout compatibility — 2026-10-09

The scoped Browser compatibility pass is implemented locally and packed as
`0.1.0-preview.23` in `artifacts/compatibility-completion-final`. No public package
upload was performed.

- FragmentTreeBuilder omits unused line objects with no words and no rectangles.
  Real blank lines remain. The earlier CreepJS line-order violations are gone.
- `overflow-wrap: break-word` and `word-wrap` split oversized words using measured
  grapheme clusters. Original words are restored before relayout, so increasing the
  width removes old splits. `nowrap` and `pre` remain unbroken.
- A canvas with HtmlBridge's render-only `data-broiler-canvas-bitmap` attribute uses
  replaced-image painting while retaining its canvas tag and natural sizing rules.
  The bridge projection leaves the live DOM unchanged.

Validation: all 3,021 Layout tests pass. Browser's pixel-level integration test
checks the canvas handoff. A 400px fixture checks narrow cards/table cells and
resize behavior against Chrome; font differences prevent pixel parity. Both live
sites have zero layout invariant violations in the final 1280×900 analysis.

Bounds: `anywhere` shares emergency wrapping but does not yet change min-content
sizing; words spanning inline elements may still overflow. General ellipsis,
axis-specific overflow and font parity were not implemented in this slice.

Browser owns the [roadmap](https://github.com/Broiler-Platform/Broiler.Browser/blob/main/docs/fingerprinting-compatibility-roadmap.md),
[completion report](https://github.com/Broiler-Platform/Broiler.Browser/blob/main/docs/fingerprinting-completion-2026-10-09.md)
and [reproduction runner](https://github.com/Broiler-Platform/Broiler.Browser/blob/main/eng/verify-fingerprinting.ps1).

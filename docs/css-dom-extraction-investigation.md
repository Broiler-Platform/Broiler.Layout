# CSS and DOM extraction investigation

Date: 2026-10-06

## Scope and conclusion

This investigation reviewed the local `Broiler.Layout`, `Broiler.CSS`, and
`Broiler.DOM` repositories for code that could move out of Layout, with particular
attention to static helpers and types named CSS or DOM.

Several extractions are worthwhile. The strongest candidates are CSS color
processing, stylesheet dependency analysis, and HTML document-mode classification.
Most `CssBox*` and other CSS-prefixed engine types are actual layout machinery and
should remain in Layout. Being static or having CSS/DOM in a name is not sufficient
to determine ownership; dependencies and responsibility are the deciding factors.

This was a source and dependency audit. No implementation changes or migrations
were attempted, and no builds or tests were run. Source locations below describe
the inspected checkouts, not necessarily the versions of their published packages.

## Progress

| Step | State |
|---|---|
| 1. `CssColor4` | Now `Broiler.CSS.CssColor4`. Layout drops its copy and consumes Broiler.CSS 0.1.0-preview.11. |
| 2. `CascadeInvalidationSet` | Now `Broiler.CSS.Dom.CascadeInvalidationSet`. Layout drops its copy and consumes Broiler.CSS.Dom 0.1.0-preview.11. Broiler.HTML's two references change namespace when it takes that Layout. |
| 3–6 and consolidation | Open. |

Layout already passed its full suite against Broiler.CSS/CSS.Dom 0.1.0-preview.10 and
Broiler.Dom 0.1.0-preview.12, the `main` of each repository, before the moves. So
the dependency bump that carries them includes no change beyond the moves themselves.

## Recommended ownership

| Responsibility | Destination |
|---|---|
| CSS syntax, values, parsing, normalization, and reusable value math | `Broiler.CSS` |
| Selector matching, cascade, and incremental style analysis | `Broiler.CSS.Dom` |
| Canonical tree, nodes, attributes, and engine-neutral DOM operations | `Broiler.Dom` |
| HTML parsing, document queries, and HTML attribute grammars | `Broiler.Dom.Html` |
| Box construction, used layout geometry, render policy, and graphics adaptation | `Broiler.Layout` |

The principal DOM-related candidates belong in `Broiler.Dom.Html`, rather than the
general `Broiler.Dom` tree kernel.

## Ranked candidates

### 1. Move `CssColor4` to `Broiler.CSS`

Source: `Broiler.Layout/IR/CssColor4.cs`, especially lines 42, 55, 65, and 1159.

This is the strongest whole-type candidate: approximately 1,790 lines of CSS color
parsing, normalization, conversion, and math. Its implementation depends only on
framework types and the existing `Broiler.CSS.CssValueParser`. The source explains
its Layout location in terms of avoiding changes across the former submodule
boundary, rather than a dependency on layout.

Move the implementation intact first. Integrating it directly into
`CssValueParser.TryParseColor` should be a separate change: the normalizer already
calls that parser, so naive integration can introduce recursion. Preserve its
supplied `currentColor` context, recursion limits, and rounding behavior.

Move the isolated `CssColor4Tests` to the CSS test project and retain rendering
integration coverage in Layout or its consumers.

### 2. Move `CascadeInvalidationSet` to `Broiler.CSS.Dom`

Source: `Broiler.Layout/Engine/CascadeInvalidationSet.cs`, especially lines 70,
120 (`Build`), and 194 (`AffectsStyle`).

This type scans stylesheet selectors and declarations to determine whether an
attribute change could affect styling. It depends only on framework and CSS types;
it does not require boxes or geometry. Its public methods already accept neutral
inputs.

The CSS kernel is technically a possible destination, but `Broiler.CSS.Dom` is the
better semantic home beside `CssCascadeRuleIndex` and the cascade engine. Keep
`RenderTreeInvalidation` in Layout: it decides whether the render tree must be
rebuilt and also handles attributes consumed directly by layout.

Move `CascadeInvalidationSetTests` with the type. Keep
`RenderTreeInvalidationTests` as integration coverage.

### 3. Extract document-mode classification to `Broiler.Dom.Html`

Source: `Broiler.Layout/DocumentModeContext.cs`, especially lines 97
(`IsQuirksHtml`), 143 (`IsQuirksDoctype`), and the supporting scanners and legacy
identifier tables below them.

Move the HTML prolog/doctype parsing and quirks classification. Keep
`CurrentQuirksMode`, thread-local storage, and ambient render-state bookkeeping in
Layout.

This removes an explicit duplication of HTML tokenizer and initial insertion-mode
behavior. `Broiler.Dom.Html` already owns `HtmlTokenizer`, `HtmlDocumentParser`, and
the related `HtmlDocumentQueries.HasHtmlDoctype` query.

**The existing query is not an equivalent replacement.** `HasHtmlDoctype` checks
the doctype name, while Layout's classifier also checks legacy public/system
identifiers. For example, an HTML 4.0 Transitional public doctype can have the name
`html` and still select quirks mode. Do not replace the classifier with
`!HasHtmlDoctype`.

Consolidate classification with canonical tokenization while preserving the
existing parser-deviation tests. The current predicate answers full quirks only;
introducing a three-state document-mode model would be a separate API change.

Move the classification tests from `DocumentModeContextTests` and
`DocumentModeDoctypeTests`; retain ambient-state and layout behavior tests.

### 4. Consider moving the CSS position and transform helpers together

Sources:

- `Broiler.Layout/IR/CssPositionValue.cs`, lines 40 and 53.
- `Broiler.Layout/IR/CssTransformOrigin.cs`, lines 46 and 58.
- `Broiler.Layout/IR/CssTransform.cs`, lines 38 and 78.

These are self-contained CSS value resolution and matrix helpers. Sizes, reference
rectangles, and font size are supplied as arguments; no `CssBox`, DOM, or graphics
backend dependency is required. `PointF`, `SizeF`, and `RectangleF` are framework
primitive types.

They can live in `Broiler.CSS`, but the ownership case is weaker than for color
parsing because they resolve used geometry. Keeping geometric projection in Layout
and extracting only grammar is also reasonable. Keep CSS transform parsing distinct
from SVG transform-attribute parsing, whose grammar differs.

All three have dedicated test classes. They are public Layout APIs, so moving them
requires a compatibility strategy or coordinated consumer updates.

### 5. Extract `image-set()` parsing and selection to `Broiler.CSS`

Source: `Broiler.Layout/IR/CssImageSet.cs`, especially lines 41, 57, 103, and 155.

The syntax and candidate-selection algorithm are reusable CSS functionality. The
current class also hardcodes renderer-supported MIME types and a device-pixel ratio
of 1.0. Keep those policies in Layout, passing them as explicit inputs, or expose
parsed candidates for the caller to select.

There is related validation in
`Broiler.CSS.Dom/CssStyleEngine.Values.cs` around line 2293, which separately scans
for negative image-set resolutions. A shared parser could reduce this overlap.

Move pure `CssImageSetTests` with the extracted functionality and retain tests of
renderer selection policy.

### 6. Split HTML and CSS parsing out of `ResponsiveImageSourceSet`

Source: `Broiler.Layout/Engine/ResponsiveImageSourceSet.cs`.

| Part | Destination | Source location |
|---|---|---|
| Candidate model, `srcset` parsing, descriptor validation | `Broiler.Dom.Html` | Lines 234, 241, 301, and 363 |
| Reusable CSS component-value scanning and syntax handling | `Broiler.CSS` | Approximately lines 599–930 |
| Box-based selection, decoder support, and used source-size resolution | Remain in Layout | Lines 54, 177, 217, and 397 |

The HTML parser portion uses framework types only. The surrounding selection logic
depends on boxes, viewport/font state, CSS evaluation, and supported image formats.

The CSS scanner overlaps `CssSyntax` and numeric parsing already in CSS. Its
malformed-input and end-of-input recovery behavior must be compared before replacing
it. Some comments describe missing exponent support that is already present in the
current CSS source, so existing workarounds should be re-evaluated against the
package version actually consumed.

Keep `ResponsiveImageSourceSetTests` integration coverage for density selection and
natural-size behavior; add or move isolated parser cases to the owning projects.

## Consolidation opportunities

### Reuse CSS animation primitives

`Broiler.Layout/Engine/CssAnimationResolver.cs` duplicates time parsing at line 143
and overlaps easing evaluation at line 168. CSS already provides
`CssAnimation.TryParseTime` and `CssEasing.Evaluate`.

This is not a mechanical substitution:

- Layout normalizes case/whitespace and defaults empty easing to `ease`.
- The CSS evaluator supports step functions that Layout currently reports as
  unsupported and evaluates using a linear fallback.
- Parsing behavior, numerical tolerances, and fallback diagnostics differ.
- `LayoutDiagnosticsTests` explicitly tests the current unsupported-easing report.

Consolidate through a compatibility wrapper or an explicitly tested behavior change.
Keep application to boxes, static snapshot timing, diagnostics, and graphics
adaptation in Layout. Keyframe parsing is another potential CSS extraction.

### Split grammar from assignments in `CssUtils`

`Broiler.Layout/Engine/CssUtils.cs` is primarily a box-property adapter. Selected
parsers could return values or longhand declarations from CSS:

- Flex-flow and flex shorthand: lines 196 and 212.
- Intrinsic-size and grid shorthands: lines 997, 1036, and 1085.
- Logical border, list-style, and text-decoration: lines 1352, 1409, and 1510.

Keep `CssBox` assignments, measurement, and renderer fallback policy in Layout.
`NormalizeDisplayValue` and `NormalizeWhiteSpaceValue` include approximations that
should not silently become general CSS semantics.

`DefaultDisplayForElement` and `BlockLevelHtmlTags` also overlap
`Broiler.CSS.Dom.CssUserAgentDefaults`, but their behavior differs, including the
default for `summary`. Consolidation needs a deliberate behavior decision.

### HTML constants are eligible but low priority

`Broiler.Layout/LayoutHtmlSupport.cs` contains five HTML name constants that could
move to `Broiler.Dom.Html` alongside its HTML name utilities. Adding a dependency
solely for those literals would provide little value; combine this with a larger
HTML extraction if useful.

The same file's character-based line-breaking heuristic and size maximum helper
should remain outside DOM.

## Keep in Layout

| Type or group | Reason |
|---|---|
| `CssBox*`, `CssRect*`, `CssLineBox`, `CssLayoutEngine*`, `CssBoxHelper` | Own boxes, line construction, sizing, and layout algorithms. |
| `CssStyleRecalc` as a whole | Collects source elements from the layout box tree and warms styles for that traversal. |
| `RenderTreeInvalidation` | Encodes render-tree rebuild policy, beyond CSS selector dependencies. |
| `HtmlTag` | Layout source descriptor, including synthetic tags; moving it into DOM would introduce another element representation. |
| `ComputedStyle` and `ComputedStyleBuilder` | Snapshot box-derived actual values, geometry, and graphics types. |
| `PositionAreaGrid`, `SvgViewBox` | Calculate used geometry from anchors, containing blocks, or viewports. |
| `SystemFontIndex` | Implements machine/font discovery policy rather than CSS grammar or DOM operations. |
| `Net.DataUrl` | Networking/resource parsing is outside the proposed CSS/DOM scope. |

## Migration constraints

1. **Preserve dependency direction.** CSS and DOM kernels are dependency-free apart
   from framework assemblies. CSS.Dom depends on CSS and Dom. Do not introduce
   Layout or Graphics dependencies upstream to facilitate a move.
2. **Coordinate package consumption.** Layout currently pins CSS/CSS.Dom
   `0.1.0-preview.8` and Dom `0.1.0-preview.11` in `Directory.Packages.props`.
   It consumes packages rather than sibling project references. Local source moves
   alone do not update its dependencies.
3. **Decide the HTML dependency boundary.** Layout currently references Dom but not
   Dom.Html. Retaining forwarding methods that call a new HTML API would require
   adding Dom.Html and updating `LayoutArchitectureTests`. Alternatively, update
   host callers to classify documents before publishing the mode to Layout.
4. **Handle public APIs deliberately.** Several candidates are public Layout types.
   Preserve compatible adapters where appropriate or coordinate downstream source
   changes. Internal helpers moved across assemblies need a supported API boundary;
   avoid expanding friend assemblies as a shortcut.
5. **Separate relocation from behavior changes.** Move existing isolated tests with
   their implementation, retain integration tests, and explicitly test any
   consolidation whose defaults, parsing, or diagnostics differ.

## Suggested implementation order

1. Move `CssColor4` intact and relocate its isolated tests.
2. Move `CascadeInvalidationSet` to CSS.Dom and retain Layout invalidation coverage.
3. Extract document-mode classification into Dom.Html, resolving the host/dependency
   boundary and legacy-doctype behavior together.
4. Extract image-set and srcset syntax with explicit environment/renderer inputs.
5. Decide ownership of the position/transform helpers and coordinate their public
   APIs if moved.
6. Consolidate animation helpers, shorthand parsing, and CSS tokenization in smaller
   changes with behavior-specific regression tests.

For each extraction, run the destination project's unit and architecture tests,
the affected Layout tests, and downstream integration checks before adopting the
new package versions. Full consumer compatibility remains unverified by this audit.

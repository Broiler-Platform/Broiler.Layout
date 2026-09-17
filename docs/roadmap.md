# Broiler.Layout Roadmap

**Status:** The extraction and measurement de-duplication projects are complete.
The remaining work is preview hardening and cleanup.

## Review the current revision

- The recorded first-preview review targets commit
  `6eaa76cc8fbe753ad2ba4db9f570f66256306c55`. The component has changed
  substantially since then, so a new release needs source review and a new
  attributable human decision.
- Complete dependency/license review and record static-analysis and
  vulnerability-scan evidence for the reviewed revision.
- Keep the README dependency and friend-assembly inventory aligned with the
  executable architecture tests.
- Release `DocumentModeContext.IsQuirksHtml`'s initial-insertion-mode rule (a
  DOCTYPE after anything but comments and ASCII whitespace is ignored) no earlier
  than the Broiler.Dom.Html release whose tree builder applies the same rule (the
  one after 0.1.0-preview.2). Consumers (Broiler.HtmlBridge, Broiler.HTML) take
  both package updates in one change: against preview.2 a page with content
  before its DOCTYPE is quirks mode from the source and standards mode from the
  serialized tree.

## Resource hardening

- Establish CPU, allocation, nesting, and document-size baselines for malformed
  and adversarial layout inputs.
- Add stress/fuzz coverage for deep trees, extreme numeric values, large inline
  runs, tables, grid/flex interactions, anchor positioning, and repeated
  relayout.
- Define host-enforceable budgets or cancellation points where measurements show
  unbounded work.

## Active layout correctness

- Close the remaining absolute-position static-position and self-alignment WPT
  clusters.
- Complete vertical-grid fit-content and baseline behavior, plus the open
  multi-column layout cases.
- Finish `position-try` fit geometry, state/cascade handling, and flip tactics.
- Keep focused unit/architecture checks and the affected WPT pixel/reftest
  clusters as CI gates for each correction; do not replace behavior evidence
  with roadmap status text.

## Transitional cleanup

- Remove dead and transitional code after dependent HTML/bridge refactors have
  stabilized.
- Reduce the internal friend-assembly surface when consumers can use owned
  projections instead of the concrete box tree.
- Track compiler/maintainability cleanup, including unused imports, eligible
  static members, and obsolete compatibility seams.

## Stabilization

- Freeze the public layout/environment contracts after consumer review.
- Add package-consumption, XML-documentation, API-compatibility, and
  cross-platform build gates.
- Re-run rendering/layout characterization before broadening the preview claim.

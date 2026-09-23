# Broiler.Layout

A .NET 10 CSS box-model and layout engine over the canonical Broiler DOM and computed
styles. The component is graphics-backend independent and is currently integrated with
Broiler.HTML through internal compatibility boundaries.

## Preview status

This is first-preview software. The API, layout behavior, and integration boundaries may
change without compatibility guarantees. Substantial implementation work was AI-assisted.
The recorded [human review](https://github.com/Broiler-Platform/Broiler.Layout/blob/main/HUMAN_REVIEW.md) approved a specific 2026-07-01
revision with conditions. The component has changed substantially since that
revision, so the approval must not be treated as covering current source or a
broader release. Re-review and hardening work is tracked in the
[current roadmap](https://github.com/Broiler-Platform/Broiler.Layout/blob/main/docs/roadmap.md).

Broiler.Layout is an independent Broiler component. Its integration with Broiler.HTML
inherits architectural context from HTML Renderer, but it must not be represented as an
official HTML Renderer component or as endorsed by that project's contributors.

## Architecture and API boundary

The public seam consists of the host abstractions (`ILayoutEnvironment`, `ILayoutFont`,
and `ILayoutImageLoader`), their value types, and the renderer-neutral HTML metadata
types used to construct a layout input. The concrete box tree and layout algorithms
remain internal. Layout consumes only `Broiler.CSS`, `Broiler.CSS.Dom`, `Broiler.Dom`,
the backend-neutral `Broiler.Graphics` primitive layer, and the BCL; painting,
resource acquisition, HTML parsing, and platform UI remain in their owning
assemblies.

`ILayoutView` is also public, but it is a contract a consumer implements and injects, not
one this component fills: nothing here — and nothing in any other Broiler component today —
implements it. A consumer that injects no view reads no engine geometry.

These assemblies have deliberate access to internal layout state (the `InternalsVisibleTo`
set in `Broiler.Layout.csproj` is authoritative):

| Friend assembly | Compatibility need |
|---|---|
| `Broiler.HTML.Dom` | Parses, serializes, and visits renderer box trees. |
| `Broiler.HTML` | Implements selection, hit-testing, and context-menu interaction over laid-out boxes. |
| `Broiler.HTML.Orchestration` | Projects computed style, invokes layout, and builds paint fragments. |
| `Broiler.DevConsole` | Converts the internal tree into renderer-independent diagnostic snapshots. |
| `Broiler.Cli.Tests` | Characterizes renderer integration and internal box state during the preview compatibility window. |
| `Broiler.DevConsole.Tests` | Characterizes diagnostic snapshots with constructed boxes. |
| `Broiler.Layout.Tests` | Exercises the internal layout kernel directly. |
| `Broiler.Wpt` | Enables scoped native anchor placement around final WPT renders. |
| `Broiler.HtmlBridge.Dom` | Supplies the visual-viewport scale around geometry snapshots. |

Facade and application assemblies must consume owned projections instead of traversing
the internal tree. In particular, `Broiler.HTML.Image` delegates canvas-background
resolution to orchestration, and the WPF app consumes dev-console snapshots. The exact
friend set is locked by `LayoutArchitectureTests` so new grants require an explicit
boundary decision and documentation update.

## Build and test

Every dependency, the Broiler packages included, restores from nuget.org; no feed
credentials are needed. `NuGet.config` clears whatever sources the machine has configured
and maps every package to nuget.org. Versions are pinned in `Directory.Packages.props`.

```bash
dotnet build Broiler.Layout.slnx -c Release
pwsh -File eng/run-tests.ps1 -Configuration Release
node --test eng/resolve-preview-version.test.mjs
pwsh -File eng/pack.ps1
```

NuGet caches packages by id and version only. A Broiler package that was once restored
from a local or retired feed under the same version shadows the one on nuget.org, and the
build then reports Broiler types as missing. Delete that version from
`~/.nuget/packages/<id>/` and restore again.

## Continuous integration and publishing

CI builds and tests `Release` on Ubuntu and Windows (with a floor on the executed test
count), then packs and verifies the package on Ubuntu and attaches it as `nuget-packages`.
**Publish** (manual, or a `v0.1.0-preview.N` tag) resolves the next unused preview
version, reruns CI with it, verifies a fresh consumer restore from nuget.org, and pushes
the validated package and its symbols to nuget.org with the `NUGET_TOKEN` secret.
`dry-run=true` is the default. The workflows and `eng/` scripts are shared with
Broiler.CSS, Broiler.DOM and Broiler.Graphics.

Preview numbers are cumulative: the next version is one past the highest preview of the
line already on nuget.org, and never below the `VersionSuffix` floor in
`Directory.Build.props`. That floor carries the numbers the retired GitHub Packages feed
already spent (`0.1.0-preview.1` through `preview.5`), which nuget.org cannot see.

## License

Broiler.Layout is licensed under the [Apache License 2.0](https://github.com/Broiler-Platform/Broiler.Layout/blob/main/LICENSE). Third-party material,
if present, retains the license identified with that material. The license provides the
software on an “AS IS” basis, without warranties or conditions.

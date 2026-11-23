# Model-check screen: compliance per version and over time (design)

The page is one screen with three regions.

## The page becomes one screen with three regions (no tabs)
Top bar (one row): plan name + version | milestone select | [Open requirement file or IDS] | [Check this model] | [Save result].

### Region 1 - VERSIONS (always visible, full width, about 5 rows high, scrolls)
One table row per version, ordered by the version's own order (Server tag / created date; for local files by the version label in the file name), NEVER by the date the check ran. Newest at the bottom, the open model's row marked "open".
Columns: Version (label + date) | Elements | Element types meeting the target (n / m) | Score | Rules met (n / m) | Since the previous version | State.
- "Since the previous version" is words + a signed count, coloured by direction, with a glyph so hue is not the only signal: "▲ 1 rule met again", "▼ 2 rules now fail", "= no change", plus "values moved: 3" in muted text when nothing flipped. Better = good green, worse = bad red, same = muted.
- State: Checked (date) / Not checked / Checking n of N / Failed (reason).
- Selecting a row makes that version the SUBJECT of regions 2 and 3. A second pick with the "Compare with" control sets the BASE (default: the previous version).
- Buttons on the region: [Versions from VIM Server...] (project + model source picker, then the list fills with Not checked rows), [Check versions] (every Not checked row: bring the VIM down, check, keep the result; progress in the State column; a version already checked for this plan + milestone is skipped), [Add results from a folder...] (what exists today).

### Region 2 - OVER TIME (the centre of the page)
A matrix, not a stack of charts: rows = things that are measured, columns = versions (same order as region 1, the subject column and the base column outlined with the inset selection keyline).
- Row groups, in this order: "Model rules" (every rule switched on in the plan, labelled with the rule's short sentence from the plan, e.g. "No duplicate Mark", never the raw id; the limit shown beside the label: "limit 0", "at most 2,200", "at least 95 %"); then "Element types" (only the element types whose score changes anywhere in the series, plus a first row "All element types" = the overall score); a toggle "Show unchanged rows" reveals the rest.
- Cell = the value at that version, mono, right-aligned, thousands separators display-only; a dot + tint for met / failed (good / bad tokens); a small ▲ or ▼ in the cell where the value differs from the previous version (direction coloured by whether it got better or worse FOR THAT RULE: fewer warnings = better, a higher % on a level = better); an empty cell with "not checked" for a version without a result; "n/c" with the reason on hover AND in the detail for a rule the model could not answer. A gap is never a zero.
- Last column of each row: a sparkline of the row over the versions (use the DS chart primitive if the shipped DS has one; otherwise a small inline SVG polyline with the token colours, no animation) and the net change first -> last ("+547", "-1,704", "-3.6 pt").
- Sort inside "Model rules": rules that flipped anywhere first, then rules whose value moved, then steady rules; keep that order stable.
- Clicking a cell selects that version as the subject and that row as the focus of region 3.
With the demo data this region must show at a glance: duplicate-Mark elements 6 -> 8 at the 6th version -> 14 at the 8th; warnings creeping from 2,104 then jumping to 2,651 at the last version (flips to failed, limit 2,200); lights outside the MEP model dropping 4,575 -> 2,871 at the 14th; elements on a level 96.1 % -> 94.5 % at the 12th (flips to failed, limit 95 %); the door work package row 0 % -> 8 %.

### Region 3 - THIS VERSION (below; what the Check tab shows today, for the subject version)
Header line: "Version 12 - checked 2026-11-23 - compared with version 11".
- Summary sentence + two tiles (as today).
- "What changed since version 11" (new, first): rules that flipped (met -> failed first), then rules whose value moved (old -> new, signed delta, better/worse colour + glyph), then requirement rows whose real-value % moved by more than 1 point (element type / parameter: old % -> new %). For a rule that has elements: two counts, "New: 448" and "Fixed: 0" = elements failing now that were not failing in the base version, and the reverse.
- Then the existing tables (element types with their parameter rows; model rules; not covered; required but not in the model).
- 3D: when the subject version IS the open model, every row isolates its elements as today, and the "New" count isolates only the new failing elements. When the subject is NOT the open model, the tables are read from the saved result, the 3D actions are disabled with the visible words "Open this version to see it in 3D" and one button [Open this version] (if the host lets the page open a model; else the words say to open it from VIM Flex's own File menu).

## Data: result file format 1.1 (additive; 1.0 files still load)
- `model.version`: { label, order (number), tag (Server version tag or null), created (ISO date or null), projectId, sourceId, vimId (Server id or null), fileName }.
- every model rule: `direction`: "lowerIsBetter" | "higherIsBetter" (from the rule kind) and `label` (the plan's sentence), `limitText`.
- every model rule that has elements: `failingKeys`: the full list of element keys, key = BIM document name + "-" + Revit element id (UniqueId is not unique across linked models). No cap on the list.
- requirement rows keep counts only (no element lists in the file).
- A results folder also gets `versions.json` (the ordered list) so the order never depends on file dates.

## Server versions
[Versions from VIM Server...]: sign-in is whatever the other pages already use for the Server (reuse their module; do not write a second one). List = the model source's version history (tag, created, size). [Check versions] per row: download (the Server's download address for that version), open in this Flex, check, save the result in the page's results folder, next. If the host cannot open a model from a page, or the page does not survive the model change, do not fake it: keep the batch runner (tower-plan\run-series-demo.ps1) as the way to produce the results folder, show the versions list with "Not checked" and the sentence "Checking a version from the Server needs VIM Flex to open it; use Check this model after opening it", and report exactly which host call is missing.

## Words
Revit's own: Version, Model rules, Element types, Mark, Workset, Family, Type. Never: timeline ids, "unique-value", "final90", file names like v07.vim as the only label (show "Version 7" + date; the file name in muted text). The milestone shows its name ("Final design 90 %").

## Envelope
Shipped DS only (tokens, ds-table / the page's existing table parts, .ds-dot, data-ds-tip tooltips, createSelect); no shadows, no gradients, no transitions, no raw colours, no native title; numbers mono + tabular; both themes. Layout must work at the page's real width (the panel is about 900 px wide beside the 3D view): the matrix scrolls horizontally inside its own box with the row-label column frozen; the collapse glyph and labels stay reachable without horizontal scroll.

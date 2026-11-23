# A model-check screen built on the OpenBEP requirement file

OpenBEP writes the plan. It does not check models. This folder shows one checker that reads OpenBEP's
requirement file: a page in VIM Flex (a desktop BIM viewer) that shows how far a Revit model follows the plan,
for each version of the model and over time.

It is here as a reference for anyone who builds a similar screen, for example on the web. Take the ideas,
fix the known faults listed below, and improve on it.

The plan in the examples is a made-up demo plan ("Medical tower (demo)"). The numbers come from 15 versions of
one test model.

## The screen

One screen, three regions, no tabs. Full description: [DESIGN.md](DESIGN.md).

1. **Versions.** One row per model version: elements, element types meeting their target, score, rules met,
   and what changed since the previous version.
2. **Over time.** A grid: one row per rule, one column per version. Green is met, red is failed. A small arrow
   marks every version where the value changed. The last columns are a trend line and the change from the first
   version to the last.
3. **This version.** The detail of the selected version, starting with "What changed since version N": rules
   that flipped, values that moved, and counts of elements that fail now and did not before ("New") or the
   reverse ("Fixed"). In the viewer, pressing "New" shows only those elements in 3D.

## Pictures

| Picture | Shows |
|---|---|
| [full-window.png](pictures/full-window.png) | The whole window: the page on the left, the 3D view on the right |
| [ui-top-wide.png](pictures/ui-top-wide.png) | Versions and the over-time grid, wide |
| [ui-top-panel.png](pictures/ui-top-panel.png) | The same at the page's usual width (about 900 px) |
| [ui-matrix-wide.png](pictures/ui-matrix-wide.png) | The over-time grid alone |
| [ui-changed-v14.png](pictures/ui-changed-v14.png) | "What changed since version 13" |
| [ui-v07-not-open.png](pictures/ui-v07-not-open.png) | An older version selected: numbers from the saved result, 3D actions off with the reason |
| [ui-top-light.png](pictures/ui-top-light.png) | Light theme |

## Example data

In [example-data](example-data):

- `requirements.json`: the requirement file exported by the OpenBEP Builder (format `openbep-requirements` 1.5).
  It holds the element types, their Revit categories, the parameters required at each milestone, the value
  rules, and the model rules.
- `result-v13.json`, `result-v14.json`: what the checker saved for two versions (format `openbep-result` 1.1).
  Per element type: score and per-parameter percentages. Per model rule: value, limit, met or not, direction
  (`lowerIsBetter` / `higherIsBetter`), a short label, and the failing elements as keys
  (document name + "-" + Revit element id). In these example files each list of failing elements is cut to
  5 entries; `failingKeysTotal` gives the real count.
- `versions.json`: the ordered list of versions, so the order never depends on file dates.

## Known faults in this screen: fix them, do not copy them

1. The versions table shows 3 rows before it scrolls. It should show about 5.
2. The arrows mean two things. In the grid an arrow shows the value going up or down. In the "what changed"
   list it shows better or worse. Use one meaning everywhere: the arrow is the direction of the value, the
   colour is better or worse.
3. Versions have no date, only "Version 7".
4. Clicking a cell in the grid does not bring that rule's detail into view.
5. The "not covered" and "required but not in the model" tables show only after a fresh check, not from a
   saved result.
6. The plan header reads "version - ·" with no version number.
7. The short rule labels ("No duplicate Mark") are built in the page. They belong in the requirement file.
8. The desktop page cannot fetch model versions from a server by itself, so the 15 versions were checked by a
   batch run. A web version does not have this limit.

## Rules worth keeping

- Use Revit's own words on screen: Version, Model rules, Element types, Mark, Workset, Family, Type.
- A gap is never a zero. "Not checked" and "cannot be checked" are shown as such, with the reason.
- A rule's row says its limit beside its name ("at most 2,200", "at least 95 %").
- Elements are compared between versions by document name + Revit element id.
- The requirement file and the IDS export describe Revit parameters. No IFC export is needed to check a
  Revit model.

# OpenBEP Sync: Revit add-in (v0.1, built for Revit 2022, first run in Revit pending)

This add-in adds an **OpenBEP** tab to Revit with four buttons: **Sync parameters**, **Build family library** (see [`library/`](../../library/)), **Replace materials** (see [below](#replace-materials)) and **Create views** (see [below](#create-views)). Sync parameters makes a model's shared parameters match the plan written in the BEP Builder.

## What it does

1. **You pick the binding list (CSV)** exported from the BEP Builder (step "Element requirements" › "Download the binding list"). The add-in looks for the OpenBEP shared parameter file (.txt) in the same folder, or asks for it.
2. **It compares the model with the plan** and changes nothing yet. Every shared parameter in the plan falls into one of these:
   - **to add**: not in the model;
   - **to bind to more categories**: in the model, but missing some categories;
   - **already correct**;
   - **skipped**: its GUID isn't in the .txt, or its category doesn't exist in this Revit version.
3. **It flags problems**:
   - **Name conflicts**: a parameter with the same name but a different GUID. This is the classic duplicate that makes data unreadable.
   - **Scope mismatches**: a parameter bound as instance in the model when the plan says type, or the reverse. These are left as they are.
   - **Leftovers**: OpenBEP parameters (`OB_` prefix, or `CA_` from before October 2026) in the model that are no longer in the plan.
   - **File mismatches**: before Revit opens the .txt, the add-in checks it against the binding list. It flags a GUID the list needs that the file lacks, a GUID the file gives another name, and an `OB_`/`CA_` parameter whose GUID isn't the fixed OpenBEP one.

   The binding list can come from any Builder version with project-chosen levels: columns can be in any order, extra columns are ignored, and "Required from LOD" can hold any level ("LOD 351", "450 (custom)") or be blank.
4. **It writes a report** (`OpenBEP-sync-report-<date>.csv`) next to the binding list.
5. **You choose** one of two options:
   - add and bind the missing parameters;
   - also delete the leftover OpenBEP parameters. This asks for confirmation, because their values are deleted too.

   Parameters without the `OB_` or `CA_` prefix are never deleted. Everything happens in one transaction, so one Undo reverses it.

The user's own shared parameter file setting is restored afterwards.

## Replace materials

A free rebuild of Polynine Studio's *Find and Replace Materials for Revit* (2017 to 2020, no longer sold), using its archived product page and screenshots as the model. It works in a project and in the Family Editor, and brings a model's materials to the OpenBEP names (`OB_<Class>_<Material>_<Detail>`, see [`docs/graphics-and-materials-standard.md`](../../docs/graphics-and-materials-standard.md)).

The window has the same three columns as Polynine's, with the replace list under them:

1. **Tick what to replace** (left). Each material shows how many places use it. Names that aren't OpenBEP names are in dark red. *Show unused* adds the materials nothing uses.
2. **Pick the new material** (middle), or **type a name that doesn't exist yet**: the old material is then renamed, so nothing has to be reassigned. `<By Category>` removes the material, so elements fall back to their category's.
3. **Add to list.** The replace list (bottom) can also be typed in like a spreadsheet, imported or exported as CSV (`Old name,New name`; Polynine's `Existing material,New material` files are read too). An AI or a BIM manager can write the list; a person checks it here before running it.
4. **Elements affected** (right) shows every element, type, object style or family type each line touches. Untick one to leave it alone.
5. **Replace** runs the whole list in one transaction: one Undo reverses it.

The list is checked before anything changes, and each line says what will happen:

- **Replace**: the new name exists; every use moves to it.
- **Rename**: the new name doesn't exist; the old material is renamed. When several old names go to the same new name, the most used one is renamed and the others merge into it.
- **Remove**: the new name is `<By Category>`.
- **Skip**: the old name isn't in the model, a line goes from a name to itself, or an earlier line already moves the same name. Chains are followed (A to B, then B to C, sends A straight to C); a loop skips every line in it.
- A note flags a new name that doesn't follow the OpenBEP rule.

Where it finds and changes materials:

- material parameters of every element and type: built-in (Material, Structural Material), shared and family parameters;
- layers of walls, floors, roofs, ceilings and building pads;
- painted faces (walls, floors and other host elements, parts, and family solids in the Family Editor);
- object styles (a category's or subcategory's material);
- in the Family Editor, the material parameters of every family type.

The same window replaces **appearance, physical and thermal assets** (the *Replace* menu at the top): every material using the old asset is switched to the new one.

*Delete replaced materials left unused* (off by default) deletes each old material that no longer has a use the tool can see.

Not done yet:

- **Materials set directly inside a loaded family's geometry**, without a family parameter, can't be reached from the project (Revit doesn't expose them there). Open the family and run the tool in the Family Editor. Nested families aren't walked yet.
- **Integral wall sweeps** in a wall type's structure are listed but not changed; change them in Edit Structure.
- **Railings' own structure** materials aren't reachable through the Revit API.
- Polynine's "Force integral wall sweeps", eyedropper and paint bucket aren't rebuilt.
- As with any material change in Revit, dimensions hosted on a material's surface pattern can disappear when the new material has a different pattern.

## Create views

A batch view creator in the spirit of Ideate ViewCreator, built around the OpenBEP view rule ([`docs/naming-rules.md`](../../docs/naming-rules.md#views)). Names are never typed: they come from the rule, so every firm using it names views the same way. One window, no wizard:

1. **Top bar:** phase, drawing set (`CON`, `PERMIT`, `W`, your own `Z-USER-<NAME>`…), design option, discipline (for templates) and the Title on Sheet language.
2. **Grid:** one row per level (top floor first), one column per plan type (`010-PLAN`, `020-PLAN-EGRESS`, `030-RCP`…). Tick a cell, or click a level or a type to tick its whole row or column. Views that already exist show as grey ticks, so the grid is also a map of what the project has. Levels not named by the OpenBEP rule are in red.
3. **Dependents per scope box:** tick scope boxes named `SB-A`, `SB-B`…; each ticked plan gets a dependent per zone (`N-CON-PLAN-L02-A`) cropped to its scope box.
4. **Preview:** every view the run makes, with its name, template, Title on Sheet and status (new, exists, or what stops it). Change a template or a title there, or untick a line.
5. **Create** makes them all in one transaction: one Undo reverses it.

Each new view gets:

- its name, `<PHASE>-<SET>-<TYPE>-<Level>[-OPTn][-Zone]`;
- `OB_ViewSet` and `OB_ViewType`, so it lands in its browser folder at once (run Sync parameters first so the model has them);
- the template `<Discipline>-<TYPE>-<Set>-<scale>` when the model has one (the largest scale when there are several);
- its phase, and when the template doesn't set one, the phase filter from the rule (`Show Previous + Demo` for demolition plans, `Show Complete` in the existing phase, `Show Previous + New` otherwise);
- Title on Sheet in words from the level and type (`Level 2 - Egress plan`, `Niveau 2 - Plan d'évacuation`), so the codes never reach the drawings.

**Save plan** writes the ticks as a view plan (CSV: `Phase,Set,Type,Level,Option,Zone`); **Load plan** reads one back, in the next project or written by an AI. Nothing is made until a person presses Create.

Not done yet: sections, elevations, 3D views per level, drafting views, room and element views, copying a whole set to another phase or set, renaming existing views to the rule (planned for the Set up project audit), placing views on sheets, and setting a design option's visibility (Revit doesn't let add-ins set it; the `-OPTn` name is added, the option is picked by hand).

## Build

Requires Windows, Revit 2022 or later, and the .NET SDK 8 or later.

```
dotnet build -c Release -p:RevitVersion=2025
```

- Revit 2025 to 2027 build for .NET 8; Revit 2022 to 2024 build for .NET Framework 4.8.
- Built for Revit 2022 (`-p:RevitVersion=2022`) on 2026-10-20 with the .NET SDK 9: no source changes, 0 warnings.
- `RevitAPI.dll` and `RevitAPIUI.dll` are referenced from `C:\Program Files\Autodesk\Revit <version>`.

### Offline tests (no Revit needed)

The binding list and shared parameter file reading, the `OB_`/`CA_` prefix rule, the fixed-GUID rule and the replace list rules (replace, rename, merge, chains, loops, OpenBEP names) and the view naming rules (names, phase codes, templates, titles, the view plan file, and that the view types and drawing sets match `docs/naming-rules.md`) are tested without Revit, against Builder v0.37 exports (school, rail, a project with custom levels) and a hand-made list with loose LOD values, in `OpenBepSync.Tests/fixtures`. From the repository root:

```
dotnet test revit-addin/OpenBepSync.Tests
```

## Install (per user, no admin rights)

1. Copy `OpenBepSync.addin` to `%APPDATA%\Autodesk\Revit\Addins\<version>\`.
2. Copy the build output (`OpenBepSync.dll`) to `%APPDATA%\Autodesk\Revit\Addins\<version>\OpenBepSync\`.
3. Restart Revit. Revit asks once whether to load the add-in.

## Files

| File | Role |
|---|---|
| `App.cs` | Adds the ribbon tab and its four buttons. |
| `LibraryCommand.cs` | Builds the families in `library/families.csv` from Revit's metric templates, with types and OpenBEP shared parameters. |
| `AutoTest.cs` | Developer test, off unless `%AppData%\OpenBEP\autotest.json` exists (see below). |
| `SyncCommand.cs` | Dry run, report, choice, then one transaction that adds, binds or deletes. |
| `BindingList.cs` | Reads the BEP Builder's binding list and merges rows by GUID. |
| `SharedParameterFile.cs` | Reads a shared parameter file without Revit; the OpenBEP prefix and GUID rules. |
| `MaterialsCommand.cs` | Opens the Replace materials window. |
| `MaterialsWindow.cs` | The window: three lists, the replace list, import, export and the run. |
| `MaterialIndex.cs` | Finds every use of each material (or asset) and changes them; the same walk does both, so the preview is what changes. |
| `MaterialRules.cs` | Reads and writes replace lists and decides what each line does; no Revit needed. |
| `CreateViewsCommand.cs` | Opens the Create views window. |
| `CreateViewsWindow.cs` | The window: the level × view type grid, scope boxes, the preview, view plan files and the run. |
| `ViewCreator.cs` | Previews and makes the views: name, phase, template, phase filter, browser parameters, title, dependents. |
| `ViewNaming.cs` | The OpenBEP view rule in code: sets, types, names, templates, titles, the view plan file; no Revit needed. |
| `OpenBepSync.csproj` | Build file; set `RevitVersion`. |
| `OpenBepSync.addin` | Revit manifest. |

## Developer test (automatic run)

For developers only; nothing happens unless `%AppData%\OpenBEP\autotest.json` exists. When it does, on Revit's first idle moment the add-in:

1. builds the library from `familiesCsv` with no dialogs, and logs each family's category, hosting and solid size per type;
2. creates a project from `DefaultMetric.rte` (or `template`), saves it in `out`, places the showroom and exports PNGs of the 3D view and the plan;
3. runs Sync parameters on `bindings` (dry run, apply, second dry run);
4. if `ifcPsets` is set: fills empty OpenBEP `OB_` parameters, and empty Type Mark, Mark, Manufacturer, Model and Description, with unique test values (warnings deleted, so a duplicate-mark warning cannot roll the fill back) and exports `OpenBEP-autotest.ifc` (IFC4, base quantities, common property sets, plus the user-defined property set file from [`shared-parameters/`](../../shared-parameters/)); check it with `test/check_ifc.py model.ifc plan.ids` (ifcopenshell and ifctester) against the IDS downloaded from the BEP Builder;
5. makes plans, egress plans and ceiling plans for every storey with Create views (and dependents if the model has scope boxes), logs each view's phase, phase filter, browser parameters and title, and checks a second preview finds them all;
6. tests Replace materials without its window: renames the most used material to `OB_Test_Renamed`, merges the second into the third, logs the use counts before and after, rolls it all back, lists the assets and round-trips a replace list CSV;
7. saves the project, writes `autotest.log` and `autotest-done.txt` in `out`, and closes Revit (unless `"exit": "no"`).

Every Revit dialog is answered automatically ("save?" questions with No) and logged.

```json
{ "familiesCsv": "C:\\path\\openbep\\library\\families.csv",
  "bindings": "C:\\path\\openbep\\revit-addin\\OpenBepSync\\test\\OpenBEP-parameter-bindings-school-example.csv",
  "ifcPsets": "C:\\path\\openbep\\shared-parameters\\OpenBEP_IFC_PropertySets_v0.1.txt",
  "out": "C:\\temp\\openbep-autotest" }
```

Delete `autotest.json` afterwards, or Revit runs the test at every start.

Revit asks again about an unsigned add-in each time the DLL changes. For a rebuild-and-test loop, point the `.addin` at a small fixed loader DLL that reads a build path from a text file and loads it with `Assembly.LoadFrom`, so "Always Load" is clicked once, on the loader only. The released add-in stays a single DLL.

## Not done yet

- **Not run yet.** It builds and installs for Revit 2022; the first sync should be on a copy of a model, with the files in [`test/`](test/).
- **Parameter group.** All parameters go in Revit's "Data" group (`GroupTypeId.Data`). The OpenBEP groups only exist in the .txt.
- **MCP tool.** The same sync logic can later be exposed as a tool on a Revit MCP server, as Sam chose: add-in first, MCP after.

# OpenBEP Sync: Revit add-in (v0.1, built for Revit 2022, first run in Revit pending)

This add-in adds an **OpenBEP** tab to Revit with two buttons: **Sync parameters** and **Build family library** (see [`library/`](../../library/)). Sync parameters makes a model's shared parameters match the plan written in the BEP Builder.

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
4. **It writes a report** (`OpenBEP-sync-report-<date>.csv`) next to the binding list.
5. **You choose** one of two options:
   - add and bind the missing parameters;
   - also delete the leftover OpenBEP parameters. This asks for confirmation, because their values are deleted too.

   Parameters without the `OB_` or `CA_` prefix are never deleted. Everything happens in one transaction, so one Undo reverses it.

The user's own shared parameter file setting is restored afterwards.

## Build

Requires Windows, Revit 2022 or later, and the .NET SDK 8 or later.

```
dotnet build -c Release -p:RevitVersion=2025
```

- Revit 2025 to 2027 build for .NET 8; Revit 2022 to 2024 build for .NET Framework 4.8.
- Built for Revit 2022 (`-p:RevitVersion=2022`) on 2026-10-20 with the .NET SDK 9: no source changes, 0 warnings.
- `RevitAPI.dll` and `RevitAPIUI.dll` are referenced from `C:\Program Files\Autodesk\Revit <version>`.

## Install (per user, no admin rights)

1. Copy `OpenBepSync.addin` to `%APPDATA%\Autodesk\Revit\Addins\<version>\`.
2. Copy the build output (`OpenBepSync.dll`) to `%APPDATA%\Autodesk\Revit\Addins\<version>\OpenBepSync\`.
3. Restart Revit. Revit asks once whether to load the add-in.

## Files

| File | Role |
|---|---|
| `App.cs` | Adds the ribbon tab and button. |
| `LibraryCommand.cs` | Builds the families in `library/families.csv` from Revit's metric templates, with types and OpenBEP shared parameters. |
| `AutoTest.cs` | Developer test, off unless `%AppData%\OpenBEP\autotest.json` exists (see below). |
| `SyncCommand.cs` | Dry run, report, choice, then one transaction that adds, binds or deletes. |
| `BindingList.cs` | Reads the BEP Builder's binding list and merges rows by GUID. |
| `OpenBepSync.csproj` | Build file; set `RevitVersion`. |
| `OpenBepSync.addin` | Revit manifest. |

## Developer test (automatic run)

For developers only; nothing happens unless `%AppData%\OpenBEP\autotest.json` exists. When it does, on Revit's first idle moment the add-in:

1. builds the library from `familiesCsv` with no dialogs, and logs each family's category, hosting and solid size per type;
2. creates a project from `DefaultMetric.rte` (or `template`), saves it in `out`, places the showroom and exports PNGs of the 3D view and the plan;
3. runs Sync parameters on `bindings` (dry run, apply, second dry run);
4. if `ifcPsets` is set: fills empty OpenBEP `OB_` parameters, and empty Type Mark, Mark, Manufacturer, Model and Description, with unique test values (warnings deleted, so a duplicate-mark warning cannot roll the fill back) and exports `OpenBEP-autotest.ifc` (IFC4, base quantities, common property sets, plus the user-defined property set file from [`shared-parameters/`](../../shared-parameters/)); check it with `test/check_ifc.py model.ifc plan.ids` (ifcopenshell and ifctester) against the IDS downloaded from the BEP Builder;
5. saves the project, writes `autotest.log` and `autotest-done.txt` in `out`, and closes Revit (unless `"exit": "no"`).

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

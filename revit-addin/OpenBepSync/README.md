# OpenBEP Sync: Revit add-in (v0.1, built for Revit 2022, first run in Revit pending)

This add-in adds a **OpenBEP › Sync parameters** button to Revit. It makes a model's shared parameters match the plan written in the BEP Builder.

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
   - **Leftovers**: OpenBEP parameters (`CA_` prefix) in the model that are no longer in the plan.
4. **It writes a report** (`OpenBEP-sync-report-<date>.csv`) next to the binding list.
5. **You choose** one of two options:
   - add and bind the missing parameters;
   - also delete the leftover OpenBEP parameters. This asks for confirmation, because their values are deleted too.

   Parameters without the `CA_` prefix are never deleted. Everything happens in one transaction, so one Undo reverses it.

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
| `SyncCommand.cs` | Dry run, report, choice, then one transaction that adds, binds or deletes. |
| `BindingList.cs` | Reads the BEP Builder's binding list and merges rows by GUID. |
| `OpenBepSync.csproj` | Build file; set `RevitVersion`. |
| `OpenBepSync.addin` | Revit manifest. |

## Not done yet

- **Not run yet.** It builds and installs for Revit 2022; the first sync should be on a copy of a model, with the files in [`test/`](test/).
- **Parameter group.** All parameters go in Revit's "Data" group (`GroupTypeId.Data`). The OpenBEP groups only exist in the .txt.
- **MCP tool.** The same sync logic can later be exposed as a tool on a Revit MCP server, as Sam chose: add-in first, MCP after.

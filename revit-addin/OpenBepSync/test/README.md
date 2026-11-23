# Test files

A binding list and a shared parameter file exported from the BEP Builder's school renovation example. Use them to try **OpenBEP › Sync parameters** on a copy of a model or on a new project, without exporting your own first.

1. Open a copy of a model, or start a new project from a template.
2. Click **OpenBEP › Sync parameters** and pick `OpenBEP-parameter-bindings-school-example.csv`. The add-in finds the .txt in the same folder.
3. Read the dry run and the report written next to the CSV, then choose **Add and bind the missing parameters**. One Undo reverses it.

For the IFC check, the same plan's IDS file and Revit IFC property set file are here too:

- `OpenBEP-school-example.ids`: the IDS file, used by `check_ifc.py model.ifc OpenBEP-school-example.ids`.
- `OpenBEP-school-example-revit.ids`: the IDS file for Revit (Builder v0.51), Revit categories and Revit parameters only; see `shared-parameters/README.md`, "IDS for Revit". Refresh it from the Builder's "Download the IDS file for Revit".
- `OpenBEP-IFC-property-sets-school-example.txt`: pick it under IFC export › Modify setup › Property Sets › Export user defined property sets. Also turn on IFC common property sets and base quantities.

The same plan's requirement file is here too:

- `OpenBEP-requirements-school-example.json`: what the plan asks of every element type at each milestone, and its model rules (format in `shared-parameters/README.md`). Written by `node translations/tools/reqfile_parity.js --write`, which also checks it against `reqfile-parity-model.csv`, a small made-up model table.
- `reqfile-parity-fallback-expected.json`: what the Builder gives on that table without its Function and Family columns, as the Builder before wall kinds (v0.44) did (refreshed from v0.46, when fire and acoustic ratings became whole numbers, and from v0.47, when the table gained three structural walls, a Strength Class column and the structural-walls rows, from v0.48, when the structural material became a structural-walls row at LOD 200, and from v0.50, when exposure class and reinforcement ratio became "Concrete only": with no Structural Material Class column they are not checked, so interior walls go from 49% to 55% at final50 to handover; and from v0.51, when walls' Load bearing reads Structural Usage: its row on interior walls 81% -> 88%); `reqfile_parity.js --no-function-column` checks the Builder still gives exactly that. After a legitimate change to the requirements (LOD list or school example), refresh it with `reqfile_parity.js --no-function-column --write`.

To refresh them, export all four files again from the Builder (step "Element requirements") with the school example loaded. The files here, and the school-example files in `OpenBepSync.Tests/fixtures`, come from app v0.46 (fire ratings in minutes, acoustic ratings as whole numbers). The IDS file and the requirement file were exported again from v0.47 (structural walls): the IDS changed only in the instructions of the interior and exterior wall specifications; the shared parameter, binding and IFC property set files are byte-identical to a v0.47 export. The requirement file was exported again from v0.48 (structural material from LOD 200); the other four files are byte-identical to a v0.48 export.

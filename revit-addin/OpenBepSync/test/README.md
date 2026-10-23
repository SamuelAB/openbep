# Test files

A binding list and a shared parameter file exported from the BEP Builder's school renovation example. Use them to try **OpenBEP › Sync parameters** on a copy of a model or on a new project, without exporting your own first.

1. Open a copy of a model, or start a new project from a template.
2. Click **OpenBEP › Sync parameters** and pick `OpenBEP-parameter-bindings-school-example.csv`. The add-in finds the .txt in the same folder.
3. Read the dry run and the report written next to the CSV, then choose **Add and bind the missing parameters**. One Undo reverses it.

For the IFC check, the same plan's IDS file and Revit IFC property set file are here too:

- `OpenBEP-school-example.ids`: the IDS file, used by `check_ifc.py model.ifc OpenBEP-school-example.ids`.
- `OpenBEP-IFC-property-sets-school-example.txt`: pick it under IFC export › Modify setup › Property Sets › Export user defined property sets. Also turn on IFC common property sets and base quantities.

To refresh them, export all four files again from the Builder (step "Element requirements") with the school example loaded. The files here come from app v0.43.

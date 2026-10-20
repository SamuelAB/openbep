# OpenBEP family library (v0.1, first families)

Generic Revit families that already carry the OpenBEP shared parameters for their element type, so a model built with them passes the parameter part of the plan from the start. The first step toward a national, Revit-first object library.

The library is a list, not a folder of .rfa files: [`families.csv`](families.csv) names each family, its template, its types and sizes. The Revit add-in's **OpenBEP › Build family library** button turns the list into .rfa files on your own machine, from the metric templates installed with your Revit version.

## What each family has

- **Template:** `Metric Door.rft` and `Metric Window.rft` for doors and windows; `Metric Generic Model face based.rft` with the category set for everything else.
- **Hosting:** Families are face-based (Sam's rule) so they can be hosted on a level, reference plane or any face; doors and windows are wall-hosted.
- **Geometry:** LOD 200 envelopes only. A box for equipment, fixtures and furniture (Width, Depth and Height parameters drive it), a leaf for doors and a glazing panel for windows (the template's Width and Height drive them). No product detail.
- **Types:** placeholder sizes in millimetres, listed in `families.csv`. They are typical sizes, not products.
- **Identity:** Assembly Code (UniFormat II, unverified), Description and Type Comments saying it is a generic family.
- **Parameters:** every OpenBEP shared parameter the [manifest](../shared-parameters/) lists for the element type, with its fixed GUID, instance or type as the manifest says, in the Data group.

| Family | French name | Element type | Types |
|---|---|---|---|
| OpenBEP_Door_Single-Flush | Porte simple à âme pleine | Doors | 4 |
| OpenBEP_Window_Fixed | Fenêtre fixe | Windows and skylights | 4 |
| OpenBEP_PlumbingFixture_Generic | Appareil de plomberie générique | Plumbing fixtures | 3 |
| OpenBEP_LightFixture_Generic | Appareil d'éclairage générique | Light fixtures | 3 |
| OpenBEP_Furniture_Generic | Mobilier générique | Furniture and casework | 3 |
| OpenBEP_Casework_Generic | Ébénisterie générique | Furniture and casework | 2 |
| OpenBEP_AHU_Generic | Unité de traitement d'air générique | Mechanical equipment - air handling unit | 2 |
| OpenBEP_Pump_Generic | Pompe générique | Pumps | 2 |
| OpenBEP_Fan_Generic | Ventilateur générique | Fans | 1 |
| OpenBEP_Panelboard_Generic | Panneau de distribution générique | Switchboards and panelboards | 2 |
| OpenBEP_SpecialtyEquipment_Generic | Équipement spécialisé générique | Specialty and owner equipment | 1 |

## Build it

1. Install the add-in ([`revit-addin/`](../revit-addin/)) and restart Revit.
2. Click **OpenBEP › Build family library** (no project needs to be open) and pick `library/families.csv` in your copy of this repository.
3. The .rfa files and `OpenBEP-library-report.csv` are written to `library/rfa/Revit<version>/`. The report says, per family, what was built and anything that did not work.

## Rules

- **Names:** `OpenBEP_<Category>_<Name>`, one English identifier; the French name is kept in the list. Type names are sizes.
- **Add a family:** add rows to `families.csv` (one per type) and run `python3 library/check_library.py`; CI runs the same check.
- **Not committed:** the built .rfa files (`library/rfa/`) until a family has been reviewed in Revit.

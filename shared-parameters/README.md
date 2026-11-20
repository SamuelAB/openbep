# OpenBEP universal Revit parameter list, v0.1 (draft)

One list of Revit parameters that every owner in Canada can ask for, tied to the LODs and BIM uses in the BEP Builder. Each parameter has one name and one fixed GUID. That way, "fire rating" means the same thing on every project and from every firm, and data can be combined across projects.

## Files

| File | What it is |
|---|---|
| `OpenBEP_SharedParameters_v0.1.txt` | A Revit shared parameter file with 321 shared parameters in 9 groups. `OB_ViewSet` and `OB_ViewType` (group 01) file views by drawing set and view type; see [naming rules](../docs/naming-rules.md). Nineteen were added for the LOD specification (OB_SpecificationSection, OB_EPDReference, OB_RecycledContent, OB_EmbodiedCarbon, and 15 for baggage handling and passenger boarding bridges in group 09 Specialty systems) and are listed in [`lod/spec.json`](../lod/spec.json), not yet in the manifest. Load it in Revit under Manage › Shared Parameters. |
| `OpenBEP_Parameters_Manifest_v0.1.csv` | The full list, one row per parameter and element type. It covers all 398 parameters: 300 shared and 98 Revit built-in. Each row gives the Revit categories, instance or type, the LOD it is required from, the BIM uses that need it, the IFC target, an example value, the placeholder values the checker rejects, and the English and French labels. |
| `gen_sp.py` | The script that writes the .txt and the manifest from the BEP Builder element library (`lb_all.json`, to be added). GUIDs are derived from names, so reruns give the same GUIDs. |
| `OpenBEP_IFC_PropertySets_v0.1.txt` | Revit IFC exporter user-defined property sets (IFC export › Modify setup › Property Sets). Every manifest shared parameter, and every built-in parameter the manifest names plainly (Type Mark, Manufacturer, Model…), is exported in one property set `OpenBEP` on occurrences, type parameters included, under its Revit name, which is what the BEP Builder's IDS checks. The 105 parameters whose IFC target is a standard IFC4 property are also written into those 61 `Pset_*` sets, on the classes each set applies to. Built-in voltages and currents are written as IfcElectricVoltageMeasure and IfcElectricCurrentMeasure; panel and device voltage comes from the family's `Voltage` connector parameter, since the panel's Distribution System is an element id. Regenerate with `gen_ifc_psets.py`. |
| `gen_ifc_psets.py` | Writes that file from the manifest (needs ifcopenshell for the IFC4 property set templates). |

The BEP Builder (v0.24, step "Element requirements") exports the subset each project needs. That subset comes from the chosen BIM uses and target LODs. The export is a .txt file plus a binding list (CSV). The school example gives 153 shared parameters, 65 built-in parameters and 54 element types.

The same step also exports an IDS file (buildingSMART Information Delivery Specification 1.0) so an IFC export can be checked against the plan, plus a property set file for the Revit IFC exporter:

- One IDS specification per element type at its target LOD (handover target, or final design target). Each requirement says which BIM uses need it and from which LOD.
- Each specification targets IFC4 and IFC4X3_ADD2, except rail, road and bridge elements, which exist only in IFC4X3_ADD2.
- OpenBEP shared parameters, and built-in parameters with no standard IFC location, are checked in a property set named `OpenBEP`, under their Revit parameter names. The property set file tells the Revit IFC exporter to write them there.
- Built-in parameters with a standard IFC location are checked there (for example `Qto_SlabBaseQuantities.Depth`); UniFormat codes as a classification; materials as a material.
- Items with no fixed place in IFC (phase status, circuit, connector sizes) are listed in each specification's instructions, to check in Revit.
- Native Revit stays the contract master; the IDS only checks IFC copies. Not yet tested on a real Revit export.

## Requirement file (JSON)

The same step also exports `OpenBEP-requirements-<project>.json` (BEP Builder v0.44). It states, in one file, what the plan asks of every element type at each milestone of "Check a model now", and the rules a model must follow. It states requirements only: OpenBEP does not check models with it. A model checking tool reads it. The school example's file is in [`revit-addin/OpenBepSync/test/`](../revit-addin/OpenBepSync/test/).

`node translations/tools/reqfile_parity.js` proves the file is enough on its own: a small checker that reads only the file gets the same result as the Builder's own check on a test model table, for every milestone, element type and requirement.

Format `openbep-requirements`, version `1.0`:

| Key | What it holds |
|---|---|
| `plan` | `name`, `fingerprint` (the plan fingerprint), `version` (the released version this plan matches, or null when it changed since), `listVersion` (OpenBEP parameter list), `builderVersion`, `generated` (ISO date and time). |
| `scoreRule` | How an element type is scored: per requirement, `round(100 × elements with a real value / elements of the type)`; the score is the rounded mean of those; it passes when the score is at least the milestone's `targetPercent`. No requirement scores 100. |
| `placeholders` | `always` (a regular expression, flags `i`): values that are never real, such as `x`, `-`, `tbc`, `n/a`. |
| `tableRule` | How a model table (a Revit schedule export with a Category column) is read: header normalization, which element type a row belongs to, which column a requirement reads. |
| `milestones` | `id`, `name`, `targetPercent`, `lodFrom`: `fixed:200` (always LOD 200), `final` (the element type's final design level) or `handover` (its handover level, else its final design level). |
| `elementTypes` | Every element type of the list, in list order, switched on or not (`on`); the Builder's check reads every type it finds in a model. |
| `unresolvedSplits` | Element types a Revit model cannot tell apart from another type on its own: they share a Revit category with no Revit-side rule (`revitWhere`) to separate them, or their IFC target needs a property value Revit has no certain equivalent for. |
| `modelRules` | Model-wide rules from the plan's "Model rules" (see below). |

Each element type has `id`, `name`, `on`, `revitCategories` (`name`, `id` as `OST_` name), `categoryNames` (the normalized names a Category cell must equal: each Revit category and the type's own name), `ifc` (`class`, `predefinedType`, `only4x3`, `where` as [property set, property, value]), `revitWhere` (`parameter`, `scope`, `equals`, only where certain: wall Function Interior or Exterior), `lod` (level per milestone; empty when the type has no numeric level), `requiredAt` (requirement ids per milestone, exactly what the Builder's check asks), and `requirements`.

Each requirement has `id` (the OpenBEP parameter id, or `owner:<name>` for a parameter the owner added), `kind` (`parameter`, `material`, `classification`, `mark`, `description`, `typeName` or `ifcClass`: where an IFC checker finds the value; a table checker reads all kinds from a column), `label`, `revitParameter` (built-in name when built-in), `guid` (null for built-in), `shared`, `bindable` (in the shared parameter file or binding list), `scope`, `dataType`, `ifcType`, `fromLod`, `neededFor` (BIM uses), `placeholders` (values that count as empty, ignoring case), `valueRule`, `example`, `ifcTarget`, `ownerAddition`, and `columnNames` (normalized column headers that hold the value).

A value is real when, trimmed, it is not empty, is not one of the requirement's `placeholders`, does not match `placeholders.always`, and matches `valueRule.pattern` (ignoring case, anywhere in the value). `valueRule.source` is `builder`: the same rule as the Builder's own check (`check` is `boolean`, `number`, `text` or `any`). The stricter patterns in `OpenBEP_Validation_Rules_v0.1.json` are not embedded; a later version may add them with `source: "validation-rules-file"`.

### Model rules

Step "Information and software", section "Model rules": the owner switches each rule on, sets its threshold, the milestone it applies from and its severity. Each rule switched on is one sentence in the BEP draft (section 6, "Model rules"). Every rule is one entry in `modelRules`, switched on or not: `id`, `kind`, `statement` (the English sentence as printed), `listed` (the names printed beside it), `on`, `params`, `appliesFrom` (a milestone id, or null for from the start) and `severity` (`critical`, `major` or `minor`). OpenBEP only states the rules; a model checking tool checks them.

Rules start switched off, except those the plan already fixes elsewhere: one set of grids and one set of levels when background models are imported once and never re-exported, the export view when milestone exports come from a named export view, and the worksets when the plan names them. A project saved before v0.44 has no model rules and opens unchanged.

| Kind | `params` | The rule |
|---|---|---|
| `unique-value` | `parameter` (default `Mark`), `scope` (`perCategory`), `categories` (empty: all) | No two elements of the same category share a value of the parameter. |
| `warnings-max` | `total`, `bySeverity` (null: Revit gives warnings no severity) | The model has no more than `total` Revit warnings. |
| `no-identical-instances` | none | No two identical instances sit in the same place. |
| `one-grid-set` | none | One set of grids, no duplicates (a background model re-exported brings its grids back). |
| `one-level-set` | none | One set of levels, no duplicates. |
| `elements-on-level` | `minPercent` | At least that share of the model elements are associated with a level. |
| `worksets-named` | `names` (default `Shared Levels and Grids`) | Every model uses the worksets listed. |
| `export-view-present` | `viewName` (empty: any named view) | Every model has the 3D export view used for milestone exports. |
| `no-in-place-families` | `maxCount` | No more than that many in-place families. |
| `no-imported-cad` | none | CAD files are linked, never imported. |
| `real-material` | `minPercent`, `notMaterial` (names that are no material, from [What counts as no material](../docs/graphics-and-materials-standard.md#what-counts-as-no-material)) | At least that share of the model elements carry a real material. Applies from Final design 50% by default (LOD 300). |
| `category-owned-by-model` | `category`, `discipline` (a discipline id of the Builder, such as `elec`) | Elements of the category are modelled only in that discipline's model; other models show none. |
| `views-named` | `patterns` (`all`: one pattern for every view type; empty: the plan's view naming rules) | Every view name follows the naming rules. |
| `views-on-sheets` | `maxUnplacedPercent` | No more than that share of the views are left off sheets. |
| `no-empty-parameters` | none | Every project parameter has a value on at least one element. |
| `elements-near-levels` | `maxDistance` (mm) | No element sits further than that below the lowest level or above the highest level. |

## Rules used

1. **Use Revit's built-in parameters wherever they exist.** For example, Fire Rating, Manufacturer, Model and Flow are built in. They are listed in the manifest as "Built-in" and never duplicated.
2. **One concept, one parameter.**
   - Our own data had "Fire-resistance rating" twice: walls and doors use Revit's built-in, dampers need a shared one. They are now one canonical entry, `OB_FireResistanceRating`, used only where Revit has no built-in.
   - "Acoustic rating", "Mounting type" and "Finish colour" were merged the same way.
   - 18 such groups were merged.
3. **Names.** Shared parameters use `OB_` (OpenBEP) plus the English label in PascalCase. Until October 2026 the prefix was `CA_`; each GUID is still hashed from that first name, so the rename changed no GUID. NUMBER parameters carry the unit, for example `OB_DesignAirflow_Ls` or `OB_RatedPower_kW`. The French label is in the manifest.
4. **Data types.**
   - The file uses only the classic tokens that Autodesk's own IFC parameter file uses: TEXT, NUMBER, LENGTH, AREA, VOLUME, YESNO, INTEGER and MULTILINETEXT.
   - Engineering values such as power, flow and pressure are NUMBER, with the unit in the name. Autodesk's IFC parameter file does the same.
5. **GUIDs are fixed.**
   - Each GUID is a UUID v5 (namespace URL) of `urn:canbim:parameter:<name>`. Anyone can regenerate the same GUID from the name.
   - The research recommends publishing the list in the buildingSMART Data Dictionary (bSDD) later and deriving the GUIDs from the bSDD URIs. The bSDD Revit plugin already does this. That would change the GUIDs once, before v1.0.
6. **Groups.**
   - A parameter used across three or more disciplines goes in "01 Identity and asset".
   - Every other parameter goes in its main discipline: architecture, structure, mechanical, plumbing and fire, electrical, site and civil, or rail, bridges and tunnels.

## Adding and removing parameters in a model

- **Without an add-in:**
  - Revit loads the .txt, but each project parameter must be added and bound to its categories one at a time.
  - Revit cannot delete unused shared parameters in bulk. Autodesk staff confirm the user interface never fully purges them.
- **With Autodesk's Parameters Service** (needs Autodesk Construction Cloud and an administrator):
  - Bulk add is possible.
  - An uploaded .txt still needs its categories set, which is what the binding list supplies.
- **Proposed next step:**
  - Build a small add-in, or a tool on a Revit MCP server, that reads the binding list and makes the model match it.
  - It would add the missing parameters with the OpenBEP GUIDs, bind them to the right categories, and remove the parameters that are not in the plan.
  - Two open-source Revit MCP servers can already create and bind shared parameters: Horizun (Apache-2.0) and LuDattilo (MIT). Neither reconciles a model against a required list.

## Limits of this draft

- **Not yet loaded in Revit.** The file follows the format of Autodesk's own file, but it should be tested in Revit 2025 to 2027.
- **Built-in parameter names vary.** Some vary by Revit version and are marked "verify" in the manifest.
- **Coverage.** The list covers the 63 element types in the BEP Builder, not every Revit category.
- **Wording.** Labels and descriptions are OpenBEP's own. IFC targets are IFC 4.3 property sets.

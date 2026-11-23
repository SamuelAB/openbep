# OpenBEP universal Revit parameter list, v0.1 (draft)

One list of Revit parameters that every owner in Canada can ask for, tied to the LODs and BIM uses in the BEP Builder. Each parameter has one name and one fixed GUID. That way, "fire rating" means the same thing on every project and from every firm, and data can be combined across projects.

## What changed (BEP Builder v0.46): fire and acoustic ratings are whole numbers

- **Fire ratings are now numbers of minutes, on shared parameters.** Revit's built-in "Fire Rating" is free text ("45 min", "1 HR", "TBD"), so no tool can compare it. Two new INTEGER type parameters replace it as the requirement:
  - `OB_FireResistanceRating_min` on assemblies: walls, curtain walls, floors and slabs, roofs, columns, beams, structural members, ceilings, stairs, tunnel lining and fire zones.
  - `OB_FireProtectionRating_min` on closures: doors, windows and dampers.
  - Accepted values: 0, 20, 30, 45, 60, 90, 120, 180 or 240. 0 means "no rating required", so it is a real value here, and a blank always means "not filled in".
  - Every element type that asked for Fire Rating now asks for the minutes parameter from the same LOD and for the same BIM uses. Interior, exterior and curtain walls also ask for it for design review / authoring, which every design-authoring project has.
- **Acoustic ratings are whole numbers from 20 to 80.** `OB_STC` replaces the text `OB_AcousticRating` where it was asked (walls, curtain walls, floors, doors, windows); `OB_ASTC` is asked on interior walls and floors, which separate occupancies; `OB_IIC` on floors. 0 is not accepted.
- **Optional, off by default:** `OB_FireRatingHeadJoint_min` and `OB_FireRatingBaseJoint_min` (the joints at the top and base of a rated wall), `OB_FireRatingFromAbove_min` and `OB_FireRatingFromBelow_min` (floors, roofs, ceilings), `OB_FireRatingFromInside_min` and `OB_FireRatingFromOutside_min` (exterior walls, curtain walls). No BIM use asks for them; the owner switches one on per element type in the BEP Builder (Element requirements, "Add a parameter from the OpenBEP list", under "Suggested for this element type").
- **Existing models keep their parameters.** Nothing was deleted or renamed and no GUID changed. Fire Rating, `OB_FireResistanceRating` and `OB_AcousticRating` stay in the list as optional parameters, useful for tags; a plan no longer requires them unless the owner switches them on. A model that only has the old text values shows the new minutes parameters as missing until they are filled in.

## Files

| File | What it is |
|---|---|
| `OpenBEP_SharedParameters_v0.1.txt` | A Revit shared parameter file with 332 shared parameters in 9 groups. `OB_ViewSet` and `OB_ViewType` (group 01) file views by drawing set and view type; see [naming rules](../docs/naming-rules.md). Nineteen were added for the LOD specification (OB_SpecificationSection, OB_EPDReference, OB_RecycledContent, OB_EmbodiedCarbon, and 15 for baggage handling and passenger boarding bridges in group 09 Specialty systems) and are listed in [`lod/spec.json`](../lod/spec.json), not yet in the manifest. Load it in Revit under Manage › Shared Parameters. |
| `OpenBEP_Parameters_Manifest_v0.1.csv` | The full list, one row per parameter and element type. It covers all 409 parameters: 311 shared and 98 Revit built-in. Each row gives the Revit categories, instance or type, the LOD it is required from, the BIM uses that need it, the IFC target, an example value, the placeholder values the checker rejects, and the English and French labels. |
| `gen_sp.py` | The script that writes the .txt and the manifest from the BEP Builder element library (`lb_all.json`, to be added). GUIDs are derived from names, so reruns give the same GUIDs. |
| `OpenBEP_IFC_PropertySets_v0.1.txt` | Revit IFC exporter user-defined property sets (IFC export › Modify setup › Property Sets). Every manifest shared parameter, and every built-in parameter the manifest names plainly (Type Mark, Manufacturer, Model…), is exported in one property set `OpenBEP` on occurrences, type parameters included, under its Revit name, which is what the BEP Builder's IDS checks. The 108 parameters whose IFC target is a standard IFC4 property are also written into those 67 `Pset_*` sets, on the classes each set applies to. A whole number written into a standard label is written as a Label: `OB_FireResistanceRating_min` and `OB_FireProtectionRating_min` go to `Pset_*Common.FireRating` of walls, slabs, roofs, columns, beams, members, coverings, stairs, curtain walls, doors and windows. That label holds the number of minutes as Revit writes the parameter, for example `60`, read as 60 min; the property set file maps a parameter's value and cannot add the word "min". Not yet tested on a real Revit export. Built-in voltages and currents are written as IfcElectricVoltageMeasure and IfcElectricCurrentMeasure; panel and device voltage comes from the family's `Voltage` connector parameter, since the panel's Distribution System is an element id. Regenerate with `gen_ifc_psets.py`. |
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

The same step also exports `OpenBEP-requirements-<project>.json` (BEP Builder v0.47). It states, in one file, what the plan asks of every element type at each milestone of "Check a model now", and the rules a model must follow. It states requirements only: OpenBEP does not check models with it. A model checking tool reads it. The school example's file is in [`revit-addin/OpenBepSync/test/`](../revit-addin/OpenBepSync/test/).

`node translations/tools/reqfile_parity.js` proves the file is enough on its own: a small checker that reads only the file gets the same result as the Builder's own check on a test model table, for every milestone, element type and requirement.

Format `openbep-requirements`, version `1.3` (1.2 had no `when` on requirements; 1.1 had no `requirementGroups`; 1.0 had `revitWhere` as `{parameter, scope, equals}`, a note could appear in `revitCategories` with a null `id`, and `unresolvedSplits` was a list of ids):

| Key | What it holds |
|---|---|
| `plan` | `name`, `fingerprint` (the plan fingerprint), `version` (the released version this plan matches, or null when it changed since), `listVersion` (OpenBEP parameter list), `builderVersion`, `generated` (ISO date and time). |
| `scoreRule` | How an element type is scored: per requirement, `round(100 × elements with a real value / elements of the type)`; the score is the rounded mean of those; it passes when the score is at least the milestone's `targetPercent`. No requirement scores 100. |
| `placeholders` | `always` (a regular expression, flags `i`): values that are never real, such as `x`, `-`, `tbc`, `n/a`. |
| `tableRule` | How a model table (a Revit schedule export with a Category column) is read: header normalization, which element type a row belongs to, which column a requirement reads. `split` says how rows of category Walls are placed by `revitWhere` when the table has a Function column (see Walls below). |
| `milestones` | `id`, `name`, `targetPercent`, `lodFrom`: `fixed:200` (always LOD 200), `final` (the element type's final design level) or `handover` (its handover level, else its final design level). |
| `elementTypes` | Every element type of the list, in list order, switched on or not (`on`); the Builder's check reads every type it finds in a model. |
| `unresolvedSplits` | Element types a Revit model cannot tell apart from another type on its own, as `{ id, sharesCategoryWith, reason }`: they share a Revit category with no Revit-side rule (`revitWhere`) to separate them, or their IFC target needs a property value Revit has no certain equivalent for. `reason` says which. |
| `uncovered` | Elements no element type covers, as `{ revitCategory, where, note }` (`where` has the same `all` shape as `revitWhere`). A checker lists them as "not covered by the plan" with their count instead of dropping them. |
| `wallKinds` | How walls are told apart: `kind` (Basic, Curtain, Stacked from the wall's family name Basic Wall, Curtain Wall, Stacked Wall; any other family in Walls is a wall modelled in place, InPlace), `function` (the six values of the wall type's Function), `structural` (Structural Usage Bearing, Shear or Structural combined; Structural = Yes only when the model has no Structural Usage). Structural is a second axis inside interior and exterior walls; structural walls also carry the rows of `requirementGroups` entry `structural_walls` (v0.47). |
| `requirementGroups` | Conditional rows: requirements on some element types that apply only to the elements a Revit condition picks. Today one entry, structural walls (see below). The key is the file's name for them; a person never sees it: on screen and in the BEP draft they are ordinary rows of the element type's list, marked "Structural walls only". |
| `modelRules` | Model-wide rules from the plan's "Model rules" (see below). |

Each element type has `id`, `name`, `on`, `revitCategories` (`name`, `id` as `OST_` name), `categoryNames` (the normalized names a Category cell must equal: each Revit category and the type's own name), `ifc` (`class`, `predefinedType`, `only4x3`, `where` as [property set, property, value]), `revitWhere` (see Walls below; null when no Revit-side rule is certain), `categoryNotes` (text from the list's category column that names no Revit category, such as "Curtain walls are separate (B2020)"), `lod` (level per milestone; empty when the type has no numeric level), `requiredAt` (requirement ids per milestone, exactly what the Builder's check asks), and `requirements`.

Each requirement has `id` (the OpenBEP parameter id, or `owner:<name>` for a parameter the owner added), `kind` (`parameter`, `material`, `classification`, `mark`, `description`, `typeName` or `ifcClass`: where an IFC checker finds the value; a table checker reads all kinds from a column), `label`, `revitParameter` (built-in name when built-in), `guid` (null for built-in), `shared`, `bindable` (in the shared parameter file or binding list), `scope`, `dataType`, `ifcType`, `fromLod`, `neededFor` (BIM uses), `placeholders` (values that count as empty, ignoring case), `valueRule`, `example`, `ifcTarget`, `ownerAddition`, `columnNames` (normalized column headers that hold the value) and, from 1.3, an optional `when` (see Concrete only below) and an optional `revitParameters` (the Revit parameters, all required, when one plan entry needs two; `revitParameter` is then the first; see IDS for Revit, Revit names).

A value is real when, trimmed, it is not empty, is not one of the requirement's `placeholders`, does not match `placeholders.always`, and matches `valueRule.pattern` (ignoring case, anywhere in the value). Whole-number ratings (`check` `integer`) also carry `allowed` (the list of accepted values) or `min` and `max`; their pattern is anchored and accepts exactly those values, so a reader that only applies the pattern gets the same answer. `valueRule.source` is `builder`: the same rule as the Builder's own check (`check` is `boolean`, `number`, `integer`, `text` or `any`). The stricter patterns in `OpenBEP_Validation_Rules_v0.1.json` are not embedded; a later version may add them with `source: "validation-rules-file"`.

### Walls

`revitWhere` is `{ "revitCategory": "OST_Walls", "all": [ condition, ... ] }`. The rule splits that one Revit category; the element type's other categories (curtain panels, mullions) take their elements with no condition. Every condition must hold. A condition is `{ "parameter": "Function", "scope": "type", "in": [values] }` (read on the element's type when `scope` is `type`) or `{ "wallKind": [kinds] }` (a list; a single string means a list of one).

| Element type | Conditions |
|---|---|
| `wall_interior` | wallKind Basic or Stacked, Function Interior or Core-shaft |
| `wall_exterior` | wallKind Basic or Stacked, Function Exterior |
| `retaining_wall` | wallKind Basic or Stacked, Function Foundation or Retaining |
| `curtain_wall` | wallKind Curtain (its Curtain Panels and Curtain Wall Mullions by category, no condition) |
| `uncovered` | Basic or Stacked with Function Soffit; walls modelled in place |

Precedence: the conditions never overlap (a curtain wall is never a basic wall, and each Function value is in one list), so a wall lands in at most one element type. A wall that matches no element type and no `uncovered` entry (for example a blank Function) is not covered either. An element type that lists a category with no rule for it takes none of that category's elements when other types split it by `revitWhere` (`bridge_substructure` and Walls; see its `unresolvedSplits` reason). A value is compared after normalizing (lowercase; spaces, underscores and hyphens removed).

The Builder's own table check reads the same rules: with a Function column, rows of category Walls go to the element type their Function (and, with a Family column, their wall kind) says; without a Family column a condition on wallKind is skipped and the result says so; without a Function column every Walls row stays with interior walls, as before v0.45, and the result says once that walls could not be told apart. French and Japanese Revit column names and values are not recognised yet.

### IDS for Revit (v0.49)

Owner ruling (2026-11-20): an IDS is used to check the Revit model itself, or a model exported from Revit (for example a VIM file), with no IFC export. The Builder's "Download the IDS file for Revit" writes `OpenBEP-<project>-revit.ids` beside the IFC IDS file, which is unchanged. It is a schema-valid IDS 1.0 file (checked with ifctester) with Revit content only:

- Every facet is a property in one property set named `Revit`, or a material facet. There is no entity facet and no standard property set. (IDS requires the `ifcVersion` attribute and takes data types from its own list, so those two stay.)
- Applicability: `Category` = the Revit category name (for example `Walls`); where the element type has a Revit rule in the requirement file (`revitWhere`), the same conditions: `Family` in the wall kind's family names (Basic Wall, Stacked Wall, Curtain Wall) and `Function` in the listed values. A type with a rule and a second category (curtain walls and Curtain Panels) has a second specification, identifier `<id>-2`, for the other category.
- Requirements: one property per Revit parameter, `baseName` = the Revit parameter name (built-in or shared) as Revit shows it; Type name, Mark and Description by those names; the material as a material facet ("a real material"). The data type comes from the parameter's Revit data type. A whole-number rule becomes an `xs:integer` enumeration (fire minutes) or bounds (STC); a validation-rules-file pattern or list becomes an `xs:string` restriction. A shared parameter's GUID is in that property's `instructions`.
- One specification per element type at the same target level as the IFC IDS file (handover target, else the final design target), so its requirements are the requirement file's `handover` milestone. Not in this file: the IFC class requirement (an export setting), plan entries that no Revit parameter carries as one value (see Revit names below; nothing is left as "check in the Revit model"), placeholders and the Builder's text rule (IDS has no such notion: any value meets a requirement without a restriction), milestones and model rules.
- Element types that share a Revit category with no rule to tell them apart are each written with the category alone, so an element is checked against both (said in the specification's `instructions`), as the requirement file's checker does. A type with no rule of its own in a category that a rule splits (Walls) is left out and named in the file's description.
- Structural walls are their own specifications, identifier `<wall type id>:structural_walls`: the wall type's applicability plus `Structural Usage` in (Bearing, Shear, Structural combined), and the structural-walls rows at that level. The "Concrete only" rows (exposure class, cover, reinforcement ratio; `when` in the requirement file) are in a further specification, `<wall type id>:structural_walls:concrete`, whose applicability adds `Structural Material Class` = `Concrete`. Revit has no parameter of that name: a checker resolves it from the element's Structural Material (instance, then type) to that material's Class as Revit shows it on the material (a VIM file: `Materials.materialCategory`). A pattern on the material name was not used: it is not certain. The Structural = Yes fallback of the requirement file (read only when no wall has Structural Usage) cannot be said in an IDS and is not in the file.
- Structural columns and slabs (v0.50): their "Concrete only" rows are in `<id>:concrete`, one specification per Revit category (`slab:concrete` for Floors, `slab:concrete-2` for Structural Foundations): `Category` plus `Structural Material Class` = `Concrete`. The element type's own specification no longer holds them.
- Revit names (v0.50, owner: "forget about all the IFC concepts ... just focus on Revit parameters for the IDS"). A plan entry whose Revit entry names several parameters, or one with a note, is written with the Revit parameter(s) that carry it, word for word from its entry in `lod/spec.json`; the requirement file's `revitParameter` is the same name (and `revitParameters` lists them all when several are needed; `revitParameter` stays the first, for older readers), so a checker reads the same parameter from either file:

  | Plan entry | Revit parameter(s) |
  |---|---|
  | Status (`status`) | `Phase Created` |
  | Photometric file (`lf_photometric_file`) | `Photometric Web File` |
  | Panel / circuit (`lf_circuit`) | `Panel` and `Circuit Number` |
  | Insulation type and thickness (`mep_insulation`) | `Insulation Type` and `Insulation Thickness` |
  | Rebar spacing (`rb_spacing`) | `Spacing` |
  | Thermal transmittance (`door_u_value`, `wall_u_value`, `glz_u_value`) (v0.51) | `Thermal Transmittance (U)` |
  | Insulation thermal resistance (`roof_insulation_rsi`) (v0.51) | `Thermal Resistance (R)` |
  | Ramp slope (`rmp_slope`) (v0.51) | `Ramp Max Slope (1/x)` (the ramp type's limit) |
  | End connection type (`stl_connection`) (v0.51) | `Start Connection` and `End Connection` |
  | Room finishes (`spc_finishes`) (v0.51) | `Floor Finish`, `Wall Finish`, `Ceiling Finish` and `Base Finish` |
  | Internal liner (`dct_liner`) (v0.51) | `Lining Type` and `Lining Thickness` (text; a duct with no liner has them blank and counts as not filled) |
  | Load bearing (`load_bearing`) on walls only (v0.51) | `Structural Usage` (see Format 1.4 below) |

  The v0.51 names were checked on a Revit 2027 model's own parameter list (2026-11-20). A name in brackets is Revit's whole name. Load Bearing, Lumens, Max Slope and Moment Connection Start / End are not Revit names.

  Left out of the IDS for Revit, because no single Revit parameter carries the value as the plan asks it: Phase Demolished (blank on every element not demolished, so it cannot be required); the IFC class (an export setting); Room / Space (calculated); Layer build-up (`Structure` is the compound-structure editor, not a value); Material grade (a property of the material, not of the element); Load bearing on element types other than walls (they keep the plan's yes/no rule); luminous flux (the plan asks the initial lumen output of the fixture / light source, which does not establish Revit's photometric `Initial Intensity`, given in lm, cd, W or lm/W); connector values (neck size, rated voltage, voltage and poles, connected load, fixture units); and entries whose Revit name depends on the family or the Revit version (total wattage, section profile, mounting height, voltage or distribution system, clear width, member length, pipe size, cable tray or conduit size). A plan still asks them in the requirement file.

### Format 1.4 (v0.51)

- Load bearing on walls (interior, exterior, foundation and retaining walls: the element types whose only Revit category is Walls) reads `Structural Usage` (instance). `valueRule.check` is `enum`, `allowed` lists Non-bearing, Bearing, Shear and Structural combined, and the pattern accepts exactly those: filled means the wall has one of them. `derived` says how a checker reports yes/no: yes when Structural Usage is not Non-bearing (it is reported, never scored). Its only column in a model table is Structural Usage. Other element types keep the plan's yes/no rule and their old text.
- Assembly Code carries the Uniformat II format of `OpenBEP_Validation_Rules_v0.1.json` ("Assembly Code": a letter A to G, then 2, 4 or more digits, for example C1020 or B2010.10), `valueRule.source` `builder`, `check` `format`. The Builder's own check, the requirement file, the IDS for Revit (an `xs:pattern`, without `^` and `$` because an XSD pattern is always the whole value) and the Flex page all apply it. It is the only rule of that file wired so far (386 rules in the file). Since format 1.5 it is read case-sensitive everywhere (`valueRule.caseSensitive`): Uniformat codes are upper case, so "c1020" is invalid in the Builder, the requirement file, the IDS for Revit and the Flex page.
- `revitParameters` (two or more names) means every one is required: an element counts as filled only when each has a real value. In a model table each name is a column; with only some of the columns the requirement is checked on those and the Builder names the missing column; with none, the requirement's usual column is read.
- Workset and Design option are built-in parameters every element type can ask, optional and off by default, like the optional fire ratings.

### Format 1.5 (v0.52)

New keys, each optional (a reader of 1.4 that ignores them reads the file as before):

- `valueRule.caseSensitive` (true on Assembly Code): match `valueRule.pattern` without ignoring case. Every other pattern is still read ignoring case.
- `notCheckable` on a requirement, with `revitParameter` null: no single Revit parameter is known to hold the value, so a checker that reads Revit parameters reports it "Not checked" with this reason and leaves it out of the score, and the IDS for Revit leaves it out. Luminous flux carries "no Revit parameter holds the fixture's lumen output with a known unit" (Revit's Initial Intensity can be lumens, candela, watts or efficacy). Every other built-in requirement whose library name is not one Revit name (for example "Size / Diameter" or "Voltage (connector)") carries "no single Revit parameter name holds it" and the library's text. `columnNames` is kept: the Builder's table check still reads a schedule column for it.
- `noneAllowed: true` on a requirement with `revitParameters` (Lining Type + Lining Thickness, Insulation Type + Insulation Thickness): an element with every one of them blank has none (no liner, no insulation). It is counted as "none", neither real nor a failure, and left out of that row's percentage (round(100 * real / (elements - none))); a row whose elements all have none is left out of the element type's mean. One filled and the other blank fails and names the blank one. In a model table, "none" needs a column for every name. Panel + Circuit Number has no `noneAllowed`: both are required. An IDS cannot express "both blank is allowed, one blank fails": the IDS for Revit lists each of these names as an optional property whose value, when present, must not be blank.
- `role` on each `revitCategories` entry: `element` (scored) or `part`. Curtain walls: the element type's requirements apply to the curtain wall elements (category Walls, wall family Curtain Wall); Curtain Panels and Curtain Wall Mullions are its parts, counted and shown with the element type ("10 parts") and never scored, because no requirement in the library names them. The IDS for Revit applies the curtain wall specification to Walls only. In a model table a row of a part category is counted on its element type and not checked; panels with no curtain wall row do not make the element type present.

### Structural walls (rows marked "Structural walls only", v0.47)

Owner ruling (2026-11-20): a wall whose built-in Structural Usage is Bearing, Shear or Structural combined keeps the requirements of its wall type (interior, exterior, foundation and retaining walls) and must also carry the rows below. When the model has no Structural Usage value, Structural = Yes is read instead (`where.fallback`). They are stated once in the LOD list (`lod/spec.json`, key `RG`, with `m` the mark) and shown on the LOD page, in the Builder's Element requirements of those three wall types and in the BEP draft as ordinary rows of the wall's list, in the normal order, each marked "Structural walls only" (and "Concrete only" where it applies), switched per requirement and per element type like any other row (v0.50; before, they were a separate sub-list). In the file they are the `requirementGroups` entry `structural_walls`.

| Requirement | From LOD | Note |
|---|---|---|
| Primary material (`material`) | 200 | Owner ruling 2026-11-20, as on structural columns and slabs |
| Material grade / strength (`col_material_grade`) | 300 | |
| Fire-resistance rating, minutes (`fire_resistance_min`) | 300 | |
| UniFormat II code (`uniformat_code`) | 300 | |
| Concrete exposure class (`col_exposure_class`) | 300 | Concrete only |
| Concrete cover (`col_cover`) | 350 | Concrete only |
| Reinforcement ratio (`col_rebar_ratio`) | 300 | Concrete only, before detailing |
| Net volume (`col_volume`) | 300 | |

Each row carries the BIM uses of the same row on structural columns, so a plan asks it only when it picks one of those uses. The three "Concrete only" rows are asked only of structural walls whose structural material is concrete (v0.50, see Concrete only below).

Each entry has `id`, `name`, `label` (the mark shown on its rows: "Structural walls only"), `statement`, `appliesTo` (element type ids), `where` (`{ all: [ {parameter, scope, in} ], fallback: { all: [...] } }`), `requiredAt` and `requirements` (the same shape as an element type's). `requiredAt` is per element type, then per milestone (`requiredAt.wall_interior.final90`), because each element type has its own level at each milestone and its own switches. A parameter the element type itself already requires at that milestone is left out of the entry there: it is checked once, on every wall. So the structural material is a structural-walls row only where the wall's level is 200 (for example Preliminary design 100%): there a structural wall must carry a real material, a Non-bearing wall is not asked it; from LOD 300 every wall of the type is asked it on its own rows (v0.48).

Score (`scoreRule.groups`): for an element type in `appliesTo`, the entry's requirements are extra rows computed over the elements `where` picks only: `round(100 × those elements with a real value / those elements)`. The element type's score is the mean over all its rows, its own rows over all its elements and the structural-walls rows over the structural ones. A structural-walls row with no structural wall is left out of the mean. A Non-bearing wall is never counted in a structural-walls row.

Model table (`tableRule.groups`): the structural-walls rows are checked only when the table has a Structural Usage column (normalized header); with only blank Structural Usage cells in those element types and a Structural column, the Structural column is read. With no Structural Usage column the walls are scored on their own rows, as before v0.47, and the Builder says so once. The IFC path of the Builder's check does not read these rows (IFC has no Structural Usage).

IFC IDS: no IFC property states Revit's Structural Usage, so the IFC IDS cannot pick structural walls. Their parameters are listed in the wall specifications' `instructions` ("Also required when the wall is structural (not checked in IFC: check in the Revit model)"); they are not IDS requirements. The IDS for Revit has them as real specifications (see above).

### Concrete only (`when`, format 1.3, v0.50)

Owner ruling (2026-11-20): concrete cover, exposure class and reinforcement ratio are asked only of elements whose structural material is concrete. In the LOD list (`lod/spec.json`, key `RW`, entry `concrete`): `m` (the mark, "Concrete only"), `when`, and `rows` (element type or `RG` id -> parameter ids). Today: structural columns, slabs and the structural-walls rows, each on `col_exposure_class`, `col_cover` and `col_rebar_ratio`. Footings, retaining walls, bridge elements and rebar keep these rows for every element (they carry no "Concrete only" note).

In the file, such a requirement (an element type's own, or a structural-walls row) carries `"when": { "structuralMaterialClass": ["Concrete"] }`:

- The element's Structural Material (the built-in parameter, instance first, then type; for walls the wall type's) names a Revit material; that material's Class (as Revit shows it on the material, English class names, compared ignoring case) must be one of the listed values.
- Score (`scoreRule.when`): the row is computed over the elements the condition picks, among the elements it is otherwise computed over (the element type's, or the structural walls): `round(100 × those with a real value / those)`. None picked: the row is left out of the mean. When the class of the structural material cannot be read (the model carries no material class), the row is not checked and left out of the mean. A class is never guessed from the material's name.
- Model table (`tableRule.when`): a schedule names the structural material, not its class. The row is checked only when the table has a `Structural Material Class` column with a value on at least one of the row's elements; then over the elements whose class is listed. Otherwise it is not checked, and the Builder says once: "Rows marked Concrete only were not checked: they need the class of each element's structural material, and this schedule names the material but has no Structural Material Class column."
- A reader of format 1.2 that ignores `when` asks these rows of every element, as before.

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

1. **Use Revit's built-in parameters wherever they exist.** For example, Manufacturer, Model and Flow are built in. They are listed in the manifest as "Built-in" and never duplicated. One exception since v0.46: Revit's Fire Rating is free text, so the requirement is a number of minutes on a shared parameter (`OB_FireResistanceRating_min`, `OB_FireProtectionRating_min`); Fire Rating stays as an optional parameter for tags.
2. **One concept, one parameter.**
   - Our own data had "Fire-resistance rating" twice: walls and doors use Revit's built-in, dampers need a shared one. They are now one canonical entry, `OB_FireResistanceRating`, used only where Revit has no built-in. Since v0.46 the requirement is the minutes parameters; these text parameters stay as optional.
   - "Acoustic rating", "Mounting type" and "Finish colour" were merged the same way.
   - 18 such groups were merged.
3. **Names.** Shared parameters use `OB_` (OpenBEP) plus the English label in PascalCase. Until October 2026 the prefix was `CA_`; each GUID is still hashed from that first name, so the rename changed no GUID. NUMBER parameters, and whole numbers counted in a unit, carry the unit, for example `OB_FireResistanceRating_min`, `OB_DesignAirflow_Ls` or `OB_RatedPower_kW`. The French label is in the manifest.
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

# OpenBEP naming rules v0.1

How OpenBEP names things in a Revit project: the rules every list follows, model files, levels, views, and the few built-in parameters whose job is fixed. The lists themselves (parameters, materials, lines, text, families) are on [openbep.com/names](https://openbep.com/names/), built from this file, the [graphics and materials standard](graphics-and-materials-standard.md) and the [shared parameter files](../shared-parameters/).

*Règles de nommage : des noms littéraux et courts, du général au particulier, qui se classent seuls par ordre alphabétique. Les noms restent en anglais et sans accents; l'étiquette française accompagne chaque nom.*

## Rules for every name

| Rule | Write | Not | Règle (FR) |
| --- | --- | --- | --- |
| Literal: anyone outside the project understands it | `OB_Masonry_Brick_Red-Matte` | `OB_Mat07` | Littéral : compris par quelqu'un hors du projet |
| As short as it can be and still be understood; abbreviate when the short form is common | `N-C-L02-RCP` | `New - Construction - Level 2 - Reflected Ceiling Plan` | Aussi court que possible tout en restant compris |
| General to specific: major, then medium, then minor | `Glass_Laminated_Clear` | `Clear_Laminated_Glass` | Du général au particulier |
| No spaces around separators | `Line-Xhatch` | `Line - Xhatch` | Pas d'espaces autour des séparateurs |
| Read the list sorted: values that belong together must sort together and in a sensible order | `Light`, `Medium`, `Wide` | `Light`, `Medium`, `Heavy` | Vérifier la liste triée par ordre alphabétique |
| English, no accents, no brand; the French label sits beside the name | `OB_Finish_Paint_Latex` | `OB_Peinture_AcmeLatex` | Anglais, sans accents ni marque |

Underscores separate the parts of OpenBEP names (`OB_Text_2.5`). Hyphens join the words inside one part of a view name (`PLAN-EGRESS`), where Revit's own view lists already use them.

## Model files

`<Project number>_<Place>_<Project>_<Discipline>_R<year>.rvt`

Example: `24017_Laval_EcoleDesPins_ARCH_R24.rvt`. The Revit version (`R24` for Revit 2024) is in the name because a file opened in a newer version can't go back.

When the plan uses ISO 19650 file naming fields instead, the discipline codes below still fill the role field.

| Code | Model | Modèle (FR) |
| --- | --- | --- |
| `ARCH` | Architecture | Architecture |
| `STRUCT` | Structure | Structure |
| `MEP` | Mechanical, electrical and plumbing in one model | Mécanique, électricité et plomberie en un modèle |
| `MECH` | Mechanical (HVAC) | Mécanique (CVCA) |
| `ELECT` | Electrical | Électricité |
| `PLUMB` | Plumbing | Plomberie |
| `FIRE` | Fire protection and sprinklers | Protection incendie et gicleurs |
| `REFRIG` | Refrigeration | Réfrigération |
| `SITE` | Site and landscaping | Aménagement du site et paysage |
| `CIVIL` | Civil engineering | Génie civil |
| `LOCATION` | Grids, levels and shared coordinates only | Grilles, niveaux et coordonnées partagées seulement |
| `CON` | Contractor's construction model | Modèle de construction de l'entrepreneur |

Any other discipline follows the same logic and is listed in the plan.

## Levels

One set of levels for the whole project, owned by the `LOCATION` model (or the architect's model when there is none). Every other model copy-monitors them and keeps the same names.

The level name is a short code that sorts the way the building stacks. No elevation in the name: elevations change and the name would lie. The words (Ground floor, Rez-de-chaussée) go in each plan's Title on Sheet, not in the level name.

| Code | Level | Building Story | Niveau (FR) |
| --- | --- | --- | --- |
| `B2` | Second basement | Yes | Deuxième sous-sol |
| `B1` | First basement | Yes | Premier sous-sol |
| `L01` | Ground floor | Yes | Rez-de-chaussée |
| `L01M` | Mezzanine above level 1 | Yes, when it is a storey for code or areas | Mezzanine au-dessus du niveau 1 |
| `L02` | Level 2 | Yes | Niveau 2 |
| `RF` | Roof | Yes | Toit |
| `L02_TOS` | Top of steel under level 2 (a reference level) | No | Dessus de l'acier sous le niveau 2 |

- **Two digits** after `L`, so `L10` sorts after `L09`. The ground floor is `L01`, as Canadian codes number it.
- **Building Story** is ticked only for real storeys: each becomes one IfcBuildingStorey in an IFC export. A reference level (top of steel, underside of deck, top of parapet) has Building Story off and its storey's code first, `L02_TOS`; elements hosted on it export with the storey below.
- **Mezzanine:** `L01M` when it counts as a storey for the building code or areas; otherwise a reference level. The plan says which.
- **Disciplines don't make their own storey levels.** Structure and MEP copy-monitor the storeys and add only the reference levels they need.

### Example building

| Code | Elevation (m) | Building Story |
| --- | --- | --- |
| `B1` | -4.200 | Yes |
| `L01` | 0.000 | Yes |
| `L01M` | 2.700 | Yes |
| `L02` | 4.500 | Yes |
| `L02_TOS` | 4.150 | No |
| `L03` | 8.700 | Yes |
| `RF` | 12.900 | Yes |

## Views

Every view name starts with its phase and drawing set, then the level, the view type and the purpose. Names stay consistent, and a view copied into another phase or set never needs renaming later. The project browser groups the same way: phase, then drawing set, then view type.

### Phases

| Code | Phase | Phase (FR) |
| --- | --- | --- |
| `EX` | Existing | Existant |
| `N` | New Construction | Nouvelle construction |

Revit's own phase names, each with a short code that starts every view name. A project with more phases names them in the plan and keeps the codes short.

### Drawing sets

| Code | Set | Holds | Ensemble (FR) |
| --- | --- | --- | --- |
| `AN` | Analysis | Daylight, code, areas, departments, egress | Analyse |
| `BIM` | BIM management | Shared coordinates, copy-monitor of grids and levels, scope boxes | Gestion BIM |
| `C` | Construction | Views placed on the construction set | Construction |
| `EXP` | Export | Views exported to other software (IFC, Navisworks, viewers) | Exportation |
| `P` | Presentation | Views for the client, committees or the city; one set each (`P-CLIENT`, `P-CITY`) | Présentation |
| `PE` | Permit | Views placed on the permit set | Permis |
| `QC` | Quality control | Views that isolate each phase, workset and floor | Contrôle de la qualité |
| `W` | Work | Working views, including views for one task (rooms, doors, tracing an old drawing) | Travail |

### View types

| Code | View | Vue (FR) |
| --- | --- | --- |
| `3D` | Camera view | Vue caméra |
| `AXO` | Axonometric view | Vue axonométrique |
| `PLAN` | Plan | Plan |
| `PLAN-LRG` | Enlarged plan | Plan agrandi |
| `RCP` | Reflected ceiling plan | Plan de plafond réfléchi |
| `SECT` | Section | Coupe |
| `SECT-DET` | Section detail | Coupe de détail |
| `ELEV` | Elevation | Élévation |
| `DET` | Detail | Détail |

The type is followed by the purpose: `PLAN-EGRESS`, `PLAN-DEPT`, `PLAN-ROOF`, `PLAN-FURN`, `PLAN-LRG-STAIR`.

### View names

`<PHASE>-<SET>-<Level>-<TYPE>-<purpose>`

Example: `N-C-L02-PLAN-EGRESS` is the new construction, construction set, level 2 egress plan.

- A view with no level drops that part: `N-C-SECT-STAIR-01`, `N-P-3D-LOBBY`.
- A working view ends with the user's initials: `N-W-L02-PLAN-JD`.
- The printed title comes from Title on Sheet, so the codes never reach the drawings.

### Project browser

Grouped from view parameters, not by reading the name, so a misspelt name still lands in the right folder.

| Browser level | Revit parameter | Set by |
| --- | --- | --- |
| 1 | Phase (built-in) | The view, when it is created |
| 2 | `OB_ViewSet` (OpenBEP shared parameter) | The view template |
| 3 | Family and Type (built-in) | Revit |
| Sort | Associated Level, then View Name | Revit |

### View parameters

| Name | Type | Label (EN) | Label (FR) | Holds |
| --- | --- | --- | --- | --- |
| `OB_ViewSet` | TEXT | Drawing set | Ensemble de dessins | The view's drawing set code, such as `C` |

A view schedule filtered on a blank `OB_ViewSet` finds the views nobody filed.

### View templates

`<Discipline>-<TYPE>-<Set>-<scale>`, for example `ARCH-PLAN-C-100` or `MECH-RCP-C-50`. Each template sets `OB_ViewSet`, so a new view files itself.

### Example project browser

- New Construction
  - C - Construction
    - Floor Plan
      - N-C-L01-PLAN
      - N-C-L02-PLAN
      - N-C-L02-PLAN-EGRESS
    - Ceiling Plan
      - N-C-L02-RCP
    - Section
      - N-C-SECT-STAIR-01
  - P - Presentation
    - Floor Plan
      - N-P-L02-PLAN
    - 3D View
      - N-P-3D-LOBBY
  - W - Work
    - Floor Plan
      - N-W-L02-PLAN-JD
- Existing
  - C - Construction
    - Floor Plan
      - EX-C-L01-PLAN

## Built-in parameters with a fixed job

Revit already has these; use them for their job and never copy their job into another parameter.

| Parameter | Job | Example | Rôle (FR) |
| --- | --- | --- | --- |
| `Type Mark` | The tag code of a type | Window type `A` | Code d'étiquette du type |
| `Mark` | The unique id of one element; never two the same in one category | Window `101`, `102` | Identifiant unique de l'élément |
| `Type Comments` | Drawing notes only | | Notes de dessin seulement |
| `Description` | Schedule descriptions only | | Descriptions de nomenclature seulement |

## Keep the lists closed

- **Materials, line patterns, line styles, text types, fill patterns:** only the template's. Families from old projects or the internet bring their own and are cleaned out before they're loaded. A list of random text styles is the sign that this rule was broken.
- **Additions:** a firm may add up to five text styles for its own work (red-marking, for example) without asking. Anything more, and any new line pattern, line style or fill pattern, is agreed with the owner's BIM lead and named with `OB_X_` so it shows at handover.
- **Hatching** comes from materials or filled regions, never from model lines, drafting lines or CAD.
- **No text in families**: use a tag or an annotation.
- **Logos on title blocks** are native lines and filled regions, not images.

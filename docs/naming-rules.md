# OpenBEP naming rules v0.1

How OpenBEP names things in a Revit project: the rules every list follows, model files, views, and the few built-in parameters whose job is fixed. The lists themselves (parameters, materials, lines, text, families) are on [openbep.com/names](https://openbep.com/names/), built from this file, the [graphics and materials standard](graphics-and-materials-standard.md) and the [shared parameter files](../shared-parameters/).

*Règles de nommage : des noms littéraux et courts, du général au particulier, qui se classent seuls par ordre alphabétique. Les noms restent en anglais et sans accents; l'étiquette française accompagne chaque nom.*

## Rules for every name

| Rule | Write | Not | Règle (FR) |
| --- | --- | --- | --- |
| Literal: anyone outside the project understands it | `OB_Masonry_Brick_Clay` | `OB_Mat07` | Littéral : compris par quelqu'un hors du projet |
| As short as it can be and still be understood; abbreviate when the short form is common | `PLAN-RCP` | `Plan-ReflectedCeilingPlan` | Aussi court que possible tout en restant compris |
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

## Views

A view name reads `<PHASE>-<SET>-<TYPE>-<purpose>` and the project browser is sorted in the same order: phase, then drawing set, then view type. Everyone sees which phase they are drawing in, and each drawing set keeps its own tags and sections without disturbing the others.

Example: `N-C-PLAN-EGRESS-L02` is the new construction, construction set, level 2 egress plan.

### Phases

| Code | Phase | Phase (FR) |
| --- | --- | --- |
| `EX` | Existing | Existant |
| `N` | New construction | Nouvelle construction |

A project with more phases names them in the plan and keeps codes short.

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

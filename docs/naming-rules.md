# OpenBEP naming rules v0.1

How OpenBEP names things in a Revit project: the rules every list follows, model files, levels, views, and the few built-in parameters whose job is fixed. The lists themselves (parameters, materials, lines, text, families) are on [openbep.com/names](https://openbep.com/names/), built from this file, the [graphics and materials standard](graphics-and-materials-standard.md) and the [shared parameter files](../shared-parameters/).

*Règles de nommage : des noms littéraux et courts, du général au particulier, qui se classent seuls par ordre alphabétique. Les noms restent en anglais et sans accents; l'étiquette française accompagne chaque nom.*

## Rules for every name

| Rule | Write | Not | Règle (FR) |
| --- | --- | --- | --- |
| Literal: anyone outside the project understands it | `OB_Masonry_Brick_Red-Matte` | `OB_Mat07` | Littéral : compris par quelqu'un hors du projet |
| As short as it can be and still be understood; abbreviate when the short form is common | `N-CON-RCP-L02` | `New - Construction - Reflected Ceiling Plan - Level 2` | Aussi court que possible tout en restant compris |
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
| `LIGHT` | Lighting | Éclairage |
| `COMM` | Communications, data and audiovisual | Télécommunications, données et audiovisuel |
| `SECUR` | Security and access control | Sécurité et contrôle d'accès |
| `BAS` | Building automation and controls | Automatisation et régulation du bâtiment |
| `INT` | Interiors | Aménagement intérieur |
| `LAND` | Landscape | Architecture de paysage |
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

The project browser groups views in four levels: phase, drawing set, view type, then Revit's own view family (Floor Plans, 3D Views). Every view name repeats the phase, set and type, then ends with the level. Names stay consistent, and a view copied into another phase or set never needs renaming later.

### Phases

| Code | Phase | Phase (FR) |
| --- | --- | --- |
| `EX` | Existing | Existant |
| `N` | New Construction | Nouvelle construction |
| `P1` | Phase 1 | Phase 1 |
| `P2` | Phase 2 | Phase 2 |

Revit's own phase names, each with a short code that starts every view name. A project with one new phase uses `N`; a phased project numbers them `P1`, `P2` and the plan lists them.

### Drawing sets

`OB_ViewSet` holds the folder name in full words; the view name carries the short code.

| Code | Folder | Holds | Ensemble (FR) |
| --- | --- | --- | --- |
| `AN` | ANALYSIS | Daylight, code, areas, departments, egress | Analyse |
| `BIM` | BIM MANAGEMENT | Georeferencing, grids and levels copy-monitor, scope boxes, export views for viewers | Gestion BIM |
| `CON` | CONSTRUCTION | Views placed on the construction set | Construction |
| `EXPORT` | EXPORT | Views exported to other software (IFC, Navisworks, viewers) | Exportation |
| `PERMIT` | PERMIT | Views placed on the permit set | Permis |
| `PRES` | PRESENTATION | Views for the client, committees or the city; one set each (`PRES-CLIENT`, `PRES-CITY`) | Présentation |
| `QC` | QUALITY CONTROL | Views that isolate each phase, workset and floor | Contrôle de la qualité |
| `W` | WORK | Shared working views, including views for one task (rooms, doors, tracing an old drawing) | Travail |
| `Z-USER-<NAME>` | Z-USER-SAM | One person's views | Vues personnelles |
| `Z-WIP` | Z-WIP | Unfinished views, Revit's default `{3D}` | Travail en cours |

Sets starting with `Z-` sort last. A browser filter on `OB_ViewSet` not beginning with `Z-` hides them all, and they are deleted or left out at handover.

### View types

`OB_ViewType` holds the type with a three-digit number in front, so the folders sort in a fixed order: 3D first, then site, then floor plans, ceilings, sections, elevations and details. The numbers leave gaps; a project adds its own types in the gaps and lists them in the plan. The view name carries the type without its number.

| Type | View | Vue (FR) |
| --- | --- | --- |
| `000-AXO` | Axonometric views | Vues axonométriques |
| `001-PLAN-GEOREF` | Georeferencing and shared coordinates | Géoréférencement et coordonnées partagées |
| `002-PLAN-GRID` | Grids and levels | Grilles et niveaux |
| `003-PLAN-SCOPE` | Scope boxes | Zones de définition |
| `004-AXO-EXPORT` | 3D views for viewers and exports | Vues 3D pour visionneuses et exportations |
| `005-PLAN-SITE` | Site plan | Plan d'implantation |
| `006-PLAN-CIVIL` | Civil plan | Plan de génie civil |
| `007-PLAN-BPAD` | Building pad | Plateforme du bâtiment |
| `009-PLAN-DEMO` | Demolition plan | Plan de démolition |
| `010-PLAN` | Floor plan | Plan d'étage |
| `011-PLAN-ROOF` | Roof plan | Plan de toiture |
| `012-PLAN-CAD` | Plan for tracing old CAD drawings | Plan de calque d'anciens dessins DAO |
| `013-PLAN-ROOM` | Room plan | Plan des locaux |
| `014-PLAN-FIRE` | Fire separation plan | Plan des séparations coupe-feu |
| `015-PLAN-DOOR` | Door plan | Plan des portes |
| `016-PLAN-WALL` | Wall type plan | Plan des types de murs |
| `017-RCP-LIGHT` | Lighting ceiling plan | Plan de plafond, éclairage |
| `018-PLAN-MASSING` | Massing plan | Plan de volumétrie |
| `019-PLAN-PANEL` | Panel layout plan | Plan de calepinage des panneaux |
| `020-PLAN-EGRESS` | Egress plan | Plan d'évacuation |
| `021-PLAN-DEPT` | Department plan | Plan des services |
| `023-PLAN-LIGHT` | Lighting plan | Plan d'éclairage |
| `030-RCP` | Reflected ceiling plan | Plan de plafond réfléchi |
| `050-PLAN-FURN` | Furniture plan | Plan du mobilier |
| `070-PLAN-STAIR` | Enlarged stair plan | Plan agrandi d'escalier |
| `100-PLAN-DETL` | Detail plan | Plan de détail |
| `200-SECT` | Section | Coupe |
| `210-SECT-DET` | Section detail | Coupe de détail |
| `300-ELEV` | Elevation | Élévation |
| `400-DET` | Detail | Détail |
| `410-LEGEND` | Legend drawn as a drafting view | Légende (vue de dessin) |
| `500-3D` | Camera view | Vue caméra |

### View names

`<PHASE>-<SET>-<TYPE>-<Level>`

Example: `N-CON-PLAN-EGRESS-L02` is the new construction, construction set, egress plan of level 2.

- A view with no level ends with what tells it apart: `N-CON-SECT-STAIR-01`, `N-PRES-3D-LOBBY`.
- Design options end with `-OPT1`, `-OPT2`: `P1-W-PLAN-L01-OPT2`.
- A person's own views go in their own set: `N-Z-USER-SAM-PLAN-L02`.
- Drafting views have no phase, so `DRAFT` takes the phase's place: `DRAFT-CON-DET-PARAPET`, `DRAFT-CON-LEGEND-WALLTYPES`. With `OB_ViewSet` and `OB_ViewType` filled, they still sort into set and type folders under `???`. (Proposal: drafting views have no agreed logic yet.)
- The printed title comes from Title on Sheet, so the codes never reach the drawings.

### Project browser

Grouped from view parameters, not by reading the name, so a misspelt name still lands in the right folder. `???` shows where a parameter is blank. At the top, it holds the drafting views: they have no phase, so Revit can't put them anywhere else, but their set and type folders still sort them. Inside a phase, it holds views nobody filed, which is how they stand out.

| Browser level | Revit parameter | Set by |
| --- | --- | --- |
| 1 | Phase (built-in) | The view, when it is created |
| 2 | `OB_ViewSet` (OpenBEP shared parameter) | The view template |
| 3 | `OB_ViewType` (OpenBEP shared parameter) | The view template |
| 4 | Family (built-in: Floor Plans, 3D Views) | Revit |
| Sort | Associated Level, then View Name | Revit |

Name the browser organization `Views (Phase-Set-Type-Family)` so everyone knows which one is on.

### View parameters

| Name | Type | Label (EN) | Label (FR) | Holds |
| --- | --- | --- | --- | --- |
| `OB_ViewSet` | TEXT | Drawing set | Ensemble de dessins | The view's drawing set folder, such as `CONSTRUCTION` |
| `OB_ViewType` | TEXT | View type | Type de vue | The view's numbered type, such as `010-PLAN` |

### View templates

`<Discipline>-<TYPE>-<Set>-<scale>`, for example `ARCH-PLAN-CON-100` or `MECH-RCP-CON-50`. Each template sets `OB_ViewSet` and `OB_ViewType`, so a new view files itself.

### Example project browser

- Views (Phase-Set-Type-Family)
  - ???
    - CONSTRUCTION
      - 400-DET
        - Drafting Views
          - DRAFT-CON-DET-PARAPET
      - 410-LEGEND
        - Drafting Views
          - DRAFT-CON-LEGEND-WALLTYPES
  - New Construction
    - ???
      - ???
        - Sections (Building Section)
          - Section 1
    - BIM MANAGEMENT
      - 002-PLAN-GRID
        - Floor Plans
          - N-BIM-PLAN-GRID-L01
    - CONSTRUCTION
      - 010-PLAN
        - Floor Plans
          - N-CON-PLAN-L01
          - N-CON-PLAN-L02
      - 020-PLAN-EGRESS
        - Floor Plans
          - N-CON-PLAN-EGRESS-L02
      - 200-SECT
        - Sections (Building Section)
          - N-CON-SECT-STAIR-01
    - WORK
      - 000-AXO
        - 3D Views
          - N-W-AXO-OPT1
          - N-W-AXO-OPT2
      - 013-PLAN-ROOM
        - Floor Plans
          - N-W-PLAN-ROOM-L01
    - Z-USER-SAM
      - 010-PLAN
        - Floor Plans
          - N-Z-USER-SAM-PLAN-L02
    - Z-WIP
      - 000-AXO
        - 3D Views
          - {3D}
  - Existing
    - CONSTRUCTION
      - 010-PLAN
        - Floor Plans
          - EX-CON-PLAN-L01

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

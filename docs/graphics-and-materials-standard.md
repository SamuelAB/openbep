# OpenBEP Revit Graphics and Materials Standard v0.1

Oct 17, 2026 · @Samuel Arsenault-Brassard

One set of line types, text styles and materials for every Revit project in Canada, named like the parameters: English names, French labels, no duplicates.

All the names below, with the parameters and families, are listed and downloadable on [openbep.com/names](https://openbep.com/names/). The general rules every name follows are in [naming rules](naming-rules.md).

## Why one standard

Today every firm ships its own template, so a project with four firms has four sets of line weights, four "Dash" patterns and four names for the same concrete. Linked models print with mixed weights, materials can't be totalled by type, and the owner inherits the mess.

What already exists, and what we take from it:

| Standard | What it sets | What we keep |
| --- | --- | --- |
| [ISO 128-2:2020](https://cdn.standards.iteh.ai/samples/69129/38e651842df746fd990d29679e3c2e98/ISO-128-2-2020.pdf) | Line widths 0.13 to 2 mm in a √2 series; wide to narrow 2:1 | The width series |
| [ISO 3098-0](https://cdn.standards.iteh.ai/samples/24792/1811286881f24ea49763a66a747029c8/ISO-3098-0-1997.pdf) | Text heights 1.8, 2.5, 3.5, 5, 7, 10 mm | The height series |
| [AEC (UK) Revit standard](https://aecuk.files.wordpress.com/2010/06/aecukbimstandardforrevit-v1-0.pdf) | 16 pens per scale, prefixed names (`AEC_Dash 3mm`, `AEC_1-Solid`), Arial Narrow | Pens that thin out at smaller scales; prefixed names |
| [PSPC CADD Standard 2024](https://web.archive.org/web/20241215044218if_/https://publications.gc.ca/collections/collection_2024/spac-pspc/P26-4-2024-eng.pdf) | 7 weight classes (0.13 to 1.00 mm), Arial Narrow and Arial, 2.0 mm minimum, French accents required | The weight classes and 2.0 mm floor |
| [Metrolinx CADD/BIM manual](https://assets.metrolinx.com/image/upload/v1738161857/Documents/Engineering/Metrolinx_CADD_BIM_Standards_Manual.pdf) | 3.0 mm default text, legible at half size | The half-size test |
| [Open BIM Object Standard](https://masterspec.co.nz/filescust/CMS/Open%20BIM%20Object%20Standard%20Consultation%20Draft%20NZ.pdf) | Names as `Type_Subtype_Differentiator` | The material name pattern |

National Defence's [CAD/BIM Standard 2.3](https://cafnet.dcc-cdc.gc.ca/cad_bim_gis/index-eng.htm) (August 2012, account needed) is the closest Canadian match. Its Revit template applies the same rules. SQI's BIM requirements set no graphics or material rules.

| Topic | DND Standard 2.3 | This standard | Why we differ |
| --- | --- | --- | --- |
| Pen widths | 6 preferred: 0.09 (grids), 0.18, 0.25, 0.35, 0.50, 0.70 | Revit's own metric table, pens 1 to 7 | No template edits; Revit thins lines at small scales by itself |
| Colour | Never sets the printed width | Same |  |
| Line patterns | `DND_DOT`, `DND_DASHED_SHORT`, `DND_CENTER_DASH` and others | `OB_Dot`, `OB_Hidden`, `OB_Centre` | Same set, OB\_ names |
| Service lines | About 130 named line types with letters in the line (W, SAN, G) | Revit systems and tags, see Service lines below | Revit line patterns can't hold text |
| Fonts | Arial Narrow 2.5 (notes), 3.5 (room names); Arial 5 (titles) | Same |  |
| Smallest text | 2.0 mm for object IDs (poles, manholes) | 2.5 mm | Must read at half size |
| Case | Upper case | Sentence case (your call) | CNIB legibility advice |
| Dimensions | `DND_MM` (arrows), `DND_Architectural` (slashes), `DND_Metre` (3 decimals, degrees-minutes-seconds) | Same split, decimal degrees | Decimal degrees are easier to check |
| Bilingual | English and French text on separate layers; sheet number ends in B (bilingual) or F (French) | Two parameters per tag; B and F suffixes kept | Same intent in Revit terms |
| Materials | No rules | Full naming and data rules | Gap in DND |

## Rules for all three

The same rules as the [parameter dictionary](parameter-dictionary.md): names are code, French is for people.

| Rule | Why | Règle |
| --- | --- | --- |
| Names start with `OB_`, in English, no accents | Same name in every model, readable by scripts | Les noms commencent par `OB_`, en anglais, sans accents |
| Every item has an English and a French label in this document | French users read the label, not the name | Chaque élément a une étiquette anglaise et française |
| Metric only: mm on paper, mm or m in the model | Canada builds in metric | Métrique seulement |
| Sizes are given at print size, not model size | A 2.5 mm note is 2.5 mm on the sheet at any scale | Les tailles sont données à l'impression |
| The template ships only what this standard lists | Each extra line style, text type or material is a duplicate waiting to happen | Le gabarit contient seulement ce qui est listé ici |
| Project additions use `OB_X_` and are reported at handover | Lets the owner see what was added and why | Les ajouts de projet utilisent `OB_X_` |
| No brand names in names; brands go in a parameter | Products change, the name shouldn't | Pas de marque dans les noms |

## Line types

We keep Revit's own metric line weight table unchanged, so a firm's template needs no edits and nothing drifts between projects. The standard only fixes which pen each category uses. Colour never sets the printed weight.

### Model line weights (mm on paper)

Revit metric defaults, pens 1 to 8 (pens 9 to 16 run to 9 mm and are not used):

| Pen | 1:10 | 1:20 | 1:50 | 1:100 | 1:200 | 1:500 | Used for | Usage (FR) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 0.18 | 0.18 | 0.18 | 0.10 | 0.05 | 0.05 | Hatch, hidden, beyond | Hachures, caché |
| 2 | 0.25 | 0.25 | 0.25 | 0.18 | 0.10 | 0.05 | Projection, fine detail, furniture | Projection fine |
| 3 | 0.35 | 0.35 | 0.35 | 0.25 | 0.18 | 0.10 | Projection, main outlines | Projection |
| 4 | 0.70 | 0.50 | 0.50 | 0.35 | 0.25 | 0.18 | Cut, minor elements (doors, windows) | Coupe, éléments mineurs |
| 5 | 1.00 | 0.70 | 0.70 | 0.50 | 0.35 | 0.25 | Cut, walls, slabs | Coupe, murs et dalles |
| 6 | 1.40 | 1.00 | 1.00 | 0.70 | 0.50 | 0.35 | Cut, structure, site boundary | Coupe, structure |
| 7 | 2.00 | 1.40 | 1.40 | 1.00 | 0.70 | 0.50 | Profile, ground line | Profil, ligne de sol |
| 8 | 2.80 | 2.00 | 2.00 | 1.40 | 1.00 | 0.70 | Not used in views; reserved | Réservé |

Values checked against Revit's Line Weights dialog in a new metric project. At 1:200 and 1:500, pens 1 and 2 drop to 0.05 mm, which many printers can't show, so hatch at those scales uses pen 2 or 3. At 1:10, pen 5 and up are heavy, so details use pens 1 to 4.

Annotation pens are the one place we change Revit: its metric default starts at 0.30 mm, too heavy for 2.5 mm text and leaders. We use the USACE metric annotation set: 1 = 0.10, 2 = 0.18, 3 = 0.25, 4 = 0.35, 5 = 0.50, 6 = 0.70, 7 = 1.00 (sheet border).

### Line patterns (lengths on paper)

| Name | Dashes and gaps (mm) | Used for | Nom (FR) |
| --- | --- | --- | --- |
| `OB_Solid` | continuous | Default | Continu |
| `OB_Hidden` | 3 dash, 1.5 gap | Hidden edges | Caché |
| `OB_Overhead` | 6 dash, 3 gap | Above the cut plane | Au-dessus |
| `OB_Centre` | 12 dash, 3 gap, 1.5 dash, 3 gap | Centrelines, grids | Axe |
| `OB_Demolish` | 1.5 dash, 1.5 gap | Elements to demolish | À démolir |
| `OB_Dot` | dot, 1.5 gap | Clearance zones, setbacks | Pointillé |
| `OB_PropertyLine` | 18 dash, 3 gap, 1.5 dash, 3 gap, 1.5 dash, 3 gap | Property and lot lines | Limite de propriété |
| `OB_Existing` | 9 dash, 3 gap | Existing to remain (civil) | Existant à conserver |

### Service lines

CAD standards draw each utility with its own line type carrying letters, such as DND's `WATER` (W) or `SAN`. Revit line patterns can only hold dashes, gaps and dots. So in Revit a service is a modelled system (pipe, duct, conduit) whose System Classification and `System Name` carry the meaning, and a tag prints the letter code along the run. Site services drawn as lines use `OB_<pen>_<Pattern>` with an overhead (`_OH`) or underground (`_UG`) suffix in the line style name, for example `OB_2_Hidden_UG`.

### Line styles

One pattern per pen where needed, named `OB_<pen>_<Pattern>`: for example `OB_1_Hidden`, `OB_2_Solid`, `OB_2_Overhead`, `OB_5_Solid`, `OB_2_Centre`, `OB_4_Demolish`. Revit's own line styles (`<Hidden>`, `<Overhead>`) are set to the same pens and patterns so both read the same.

### Object styles (key categories)

| Category | Projection pen | Cut pen | Pattern |
| --- | --- | --- | --- |
| Walls | 2 | 5 | Solid |
| Floors, roofs | 2 | 5 | Solid |
| Structural columns, framing | 3 | 6 | Solid |
| Doors, windows | 2 | 4 | Solid |
| Stairs, railings | 2 | 4 | Solid |
| Ceilings | 1 | 4 | Solid |
| Furniture, casework, equipment | 1 | 3 | Solid |
| Ducts, pipes, cable trays | 2 | 3 | Solid |
| Grids | 2 | — | `OB_Centre` |
| Topography (toposolid) | 2 | 7 | Solid |

Pens are a proposal checked against AEC (UK) and PSPC, not tested on a printed set yet.

## Text

Two fonts, five heights, and nothing below 2.5 mm, so every sheet can still be read when printed at half size.

- **Fonts:** Arial Narrow for notes, dimensions and tags; Arial for titles. Both are on every Windows machine, carry all French accents, and match PSPC. French text runs longer than English, which Narrow absorbs.
- **Case:** sentence case, not all capitals. CNIB's [Clear Print guide](https://cnib.ca/sites/default/files/2018-07/CNIB%20Clear%20Print%20Guide.pdf) advises against all-caps, and French capitals keep their accents (É, À). PSPC and DND both ask for upper case, so federal projects may override this.
- **Width factor 1.0, no italics, transparent background.**

### Text types

| Name | Font | Height (mm) | Used for | Usage (FR) |
| --- | --- | --- | --- | --- |
| `OB_Text_2.5` | Arial Narrow | 2.5 | Notes, tags, schedules | Notes, étiquettes |
| `OB_Text_2.5_Bold` | Arial Narrow | 2.5 | Note headings | Titres de notes |
| `OB_Text_3.5` | Arial Narrow | 3.5 | Room names, sub-headings | Noms de locaux |
| `OB_Text_5_Bold` | Arial | 5 | View titles | Titres de vues |
| `OB_Text_7_Bold` | Arial | 7 | Sheet titles | Titres de feuilles |

All heights come from the ISO 3098 series. 1.8 mm is not allowed, since PSPC requires approval below 2 mm and Metrolinx requires legibility at half size.

### Dimension types

| Name | Units | Text | Used for |
| --- | --- | --- | --- |
| `OB_Dim_Linear_mm` | mm, no decimals | `OB_Text_2.5` | Buildings |
| `OB_Dim_Linear_m` | m, 3 decimals | `OB_Text_2.5` | Site, civil, rail |
| `OB_Dim_Angular` | degrees, 1 decimal | `OB_Text_2.5` | All |
| `OB_Dim_Radial` | mm | `OB_Text_2.5` | All |
| `OB_Spot_Elevation_m` | m, 3 decimals | `OB_Text_2.5` | Levels, grading |
| `OB_Spot_Slope_Percent` | %, 1 decimal | `OB_Text_2.5` | Ramps, roofs, site |

Arrowheads: `OB_Arrow_Filled_15` for leaders, `OB_Tick_Diagonal` for dimensions.

### Bilingual sheets

When a sheet must be in both languages, tags read two parameters (`_EN` and `_FR`) and show them on two lines, French first in Quebec. Never type both languages into one note: the next edit updates one and forgets the other.

Sheet numbers follow DND: a sheet with both languages ends in B, a French-only sheet ends in F, and an English-only sheet has no suffix.

*Texte : Arial Narrow pour les notes, Arial pour les titres, hauteur minimale de 2,5 mm, minuscules avec accents. Sur une feuille bilingue, chaque étiquette lit deux paramètres, \_FR et \_EN.*

## Materials

A material is named for what it is, not for who made it or how thick it is. The layer sets the thickness, and the brand goes in Manufacturer and Model once a product is chosen.

### Name pattern

`OB_<Class>_<Material>_<Detail>`: the class first, so all the wood lists together, then the material, then what sets it apart (grade, finish, colour, or the use when the use changes the material). Following the Open BIM Object Standard and the Dutch Revit standards, which also put the material before the use.

Add the use only when it changes the finish, grade or cost. Oak flooring and oak ceiling slats are two materials; the same concrete in walls, floors and columns is one, so its quantity totals in one line.

| Example | Class | Counted | Matériau (FR) |
| --- | --- | --- | --- |
| `OB_Concrete_CastInPlace_35MPa` | Concrete | Yes | Béton coulé en place 35 MPa |
| `OB_Concrete_Precast_50MPa` | Concrete | Yes | Béton préfabriqué 50 MPa |
| `OB_Concrete_Generic` | Concrete | Yes | Béton (à préciser) |
| `OB_Masonry_ConcreteBlock_Normal` | Masonry | Yes | Bloc de béton |
| `OB_Masonry_Brick_Red-Matte` | Masonry | Yes | Brique rouge mate |
| `OB_Masonry_Brick_Red-Paver` | Masonry | Yes | Pavé de brique rouge |
| `OB_Metal_Steel_350W` | Metal | Yes | Acier 350W |
| `OB_Metal_Aluminium_Anodized` | Metal | Yes | Aluminium anodisé |
| `OB_Wood_Lumber_SPF` | Wood | Yes | Bois d'œuvre É-P-S |
| `OB_Wood_CLT` | Wood | Yes | Bois lamellé-croisé |
| `OB_Wood_Oak_Flooring` | Wood | Yes | Plancher de chêne |
| `OB_Wood_Oak_CeilingSlat` | Wood | Yes | Lattes de plafond en chêne |
| `OB_Gypsum_Board_TypeX` | Gypsum | Yes | Gypse type X |
| `OB_Insulation_MineralWool_Batt` | Insulation | Yes | Laine minérale en matelas |
| `OB_Insulation_XPS_Board` | Insulation | Yes | Polystyrène extrudé |
| `OB_Membrane_AirVapour_SelfAdhered` | Membrane | Yes | Pare-air/vapeur autocollant |
| `OB_Glass_Laminated_Clear` | Glass | Yes | Verre feuilleté clair |
| `OB_Finish_Paint_Latex-Eggshell` | Finish | Yes | Peinture latex coquille d'œuf |
| `OB_Earth_Gravel_Granular-A` | Earth | Yes | Gravier granulaire A |
| `OB_Generic_Metal` | Generic | No | Métal générique |
| `OB_Generic_Plastic` | Generic | No | Plastique générique |
| `OB_Generic_Wood` | Generic | No | Bois générique |
| `OB_Generic_Glass` | Generic | No | Verre générique |
| `OB_Generic_Fabric` | Generic | No | Tissu générique |

Classes are fixed: Concrete, Masonry, Metal, Wood, Gypsum, Insulation, Membrane, Glass, Finish, Plastic, Earth, Generic. The class goes in Revit's material Class field too.

Two kinds of generic:

- **`OB_<Class>_Generic`** (`OB_Concrete_Generic`, `OB_Masonry_Generic`): the class is known, the exact material isn't yet. Allowed at LOD 100 and 200, and counted, since early estimates need it.
- **`OB_Generic_<Kind>`** (`OB_Generic_Metal`, `OB_Generic_Plastic`): materials nobody counts. Furniture, equipment, appliances and fixtures use these, so the thirty plastics of a loaded toaster become one `OB_Generic_Plastic`. Quantity take-offs skip every material starting with `OB_Generic_`. A project whose uses need those materials (visualisation, a furniture inventory) lists the exceptions in the plan.

### Data each material must carry

| Revit field | When | Rule | Champ (FR) |
| --- | --- | --- | --- |
| Class | Always | One of the 12 classes | Classe |
| Description | Always | Plain English, shown in tags | Description |
| `OB_Description_FR` | Always | Plain French, shown in French tags | Description française |
| Keynote | LOD 300 and up | Masterformat section, such as 09 21 16 | Note-clé |
| Thermal: conductivity (W/(m·K)), density (kg/m³), specific heat (J/(kg·K)) | Energy analysis use | From the manufacturer or a cited reference table, never left at Revit's default | Propriétés thermiques |
| Structural: strength, modulus | Structural analysis use | From the structural engineer's specification | Propriétés structurales |
| Manufacturer, Model | LOD 400 and up | The product actually bought | Fabricant, modèle |

Each material also carries `OB_UniclassMa`, its code from the free [Uniclass Materials table](https://uniclass.thenbs.com/taxon/ma), such as Ma\_40\_19 for concrete. The checker refuses a Class and a code that disagree.

### Graphics and appearance

- **Cut and surface patterns** come from the material, not from overrides, so every view agrees. Each class has one cut pattern, such as `OB_Concrete`, `OB_Insulation_Batt` or `OB_Earth`, drawn with pen 1.
- **Appearance:** one appearance asset per material, Revit's own library or a plain colour. No imported textures unless the visualisation use is chosen, since they bloat the model.
- **No duplicates:** Revit copies a material on every family load. At each milestone, a check lists every material not in the project's list with its element count. The **Replace materials** button of the OpenBEP Revit add-in ([`revit-addin/OpenBepSync`](../revit-addin/OpenBepSync/README.md#replace-materials)) then merges or renames them to a listed name (a toaster's thirty plastics to `OB_Generic_Plastic`) from an old-to-new list that a person checks before it runs. Materials set inside a loaded family's own geometry are changed by running it on the family.

### What counts as no material

On real projects the materials that reach the estimator are the ones the model carries, and a model can look finished while most of its elements carry none. Three rules, from LOD 300 on:

| Rule | Why | Règle |
| --- | --- | --- |
| `Default`, `<By Category>`, an empty material, or a material whose name is a Revit or family default (`Default Wall`, `Generic`, a family name) is **no material**. An element whose only material is one of these does not meet the element requirements of any use that needs quantities or materials. | A family that was never assigned a material still shows a colour and a pattern, so nobody notices until the quantity take-off comes back short. Written in the plan, the rule is the same for the modeller and for whatever tool checks the delivery, whichever file is delivered. | `Default`, `<By Category>`, un matériau vide ou un nom de matériau par défaut de Revit ne sont **pas un matériau**. Un élément qui n'en porte pas d'autre échoue à la vérification. |
| One material per real material. Fifty concretes that differ only by family of origin are one `OB_Concrete_CastInPlace_35MPa`. The deliverable at each milestone lists every material in use with its count of elements, and the list must fit on a page. | Every duplicate splits the quantity totals and hides the real amount of each material. On one federated hospital model, grouping materials by the words in their names was the only way to get a usable concrete total. | Un seul matériau par matériau réel. La liste des matériaux en usage est livrée à chaque jalon. |
| Material and category are checked together: a `Generic Models` or `Specialty Equipment` element with a real material still needs a proper category or a classification code before it is counted. | Generic Models is where elements land when the modeller did not find a category. They carry no useful built-in parameters and fall out of every schedule. | Un élément `Generic Models` avec un matériau réel doit tout de même porter une catégorie ou un code de classification. |

### Quantities by material

The quantity take-off and cost estimating uses total the model's area and volume **per material per element**: concrete in a composite column, each layer of a wall, each layer of a floor. Those numbers exist only when:

- the element is a **closed solid** drawn with a Revit family, system family or compound structure (wall, floor, roof, ceiling layers). A mass, an in-place sketch left open, a model line, a linked mesh (DWG, SAT, IFC proxy) or a Direct Shape gives no volume, no length and no material quantity;
- the material is assigned to the **layer or the solid**, not painted on a face. Paint gives an area only, never a volume;
- the compound structure carries its layers with thicknesses, so each layer's volume follows from the element's geometry.

An element that fails these counts as "not measurable": its cost line stays empty rather than guessed. The plan does not accept a count of one, an average or a default in place of a missing quantity: a missing number stays missing until the model carries it.

Estimating practices that price by material volume per cost item (for example a quantity standard that lists concrete, formwork and reinforcement per structural element) need these quantities at the preliminary design milestone already, at LOD 200 with generic materials and LOD 300 with the real ones. Say so in the plan's element requirements rather than discovering it at tender.

The parameter dictionary's built-in `Material / Structural Material` field takes these names, so quantities can be totalled by material across a portfolio.

*Matériaux : nommés selon ce qu'ils sont (classe, type, précision), sans marque ni épaisseur. Chaque matériau porte une description en anglais et en français, ses propriétés thermiques si l'usage énergie est choisi, et son fabricant à partir du LOD 400.*

## What we borrow from other standards

The most used graphics standards worldwide, and the rule each one gives us.

| Standard | Reach | Rule we take | Règle (FR) |
| --- | --- | --- | --- |
| [USACE A/E/C Graphics Standard 2.2](https://www.wbdg.org/FFC/AECCAD/ERDC_ITL_SR_23_1_2023.pdf) (US Army, Air Force, Navy) | Free; the reference manual for US military work since 1994 | Dimension spacing set by text height (TH): first line 4 × TH from the object, lines 3 × TH apart, extension gap and overshoot 0.5 × TH; one arrow style per set | Espacement des cotes selon la hauteur du texte |
| USACE Revit metric template | Same | Annotation line weights 0.10 to 1.00 mm | Poids des lignes d'annotation |
| [VA BIM Manual](https://www.cfm.va.gov/til/bim/BIM-Manual.pdf) (US Veterans Affairs) | Required on VA projects | Dimensions stay linked to the model and are never overridden | Les cotes ne sont jamais modifiées à la main |
| [Dutch Revit Standards (NLRS)](https://help.daqs.io/NL/NLRS/materiaalNaamgeving/) | Used by large Dutch contractors; Belgium's federal buildings agency runs a version | A classification code tied to each material | Un code de classification par matériau |
| [Dutch NAA.K.T.](https://www.digigo.nu/ilsen-en-richtlijnen/tools-voor-informatiemanagement/naa-k-t/) (BIM basis ILS) | Required in the Dutch national information standard | Each part of a material name comes from a controlled list, so names can be checked by software | Chaque partie du nom vient d'une liste contrôlée |
| [Uniclass Materials table](https://uniclass.thenbs.com/taxon/ma) (UK, NBS) | Free; UK national classification | `OB_UniclassMa` code on each material, such as Ma\_40\_19 Concrete | Code Uniclass Ma |
| [NBS BIM Object Standard](https://www.thenbs.com/knowledge/exploring-the-nbs-bim-object-standard-metadata-requirements) | UK national object library | Textures at least 512 px, 150 dpi, tileable | Textures d'au moins 512 px |
| [Transport for NSW](https://standards.transport.nsw.gov.au/_entity/annotation/1a48d971-b235-ed11-9db2-000d3ae019e0) (applies AS 1100) | State transport agency | Every sheet must read at half size | Lisible à demi-échelle |

### US National CAD Standard v4

The NCS is the main US drafting standard, published by NIBS with AIA and CSI. Version 4 agrees with most of this standard. What it adds:

- **Pen width follows text height:** 2.5 mm text uses a 0.25 mm pen, 4 mm text a 0.35 mm pen, 6 mm titles a 0.50 mm pen. We apply this to our text types.
- **Minimum CAD text 2.5 mm**, because smaller text can't be read on half-size sets. Same as ours.
- **No bold, italic or underline in notes.** `OB_Text_2.5_Bold` is kept for note headings only.
- **Hatch sparingly:** plans at 1:100 and smaller rarely need material hatching, and large areas are hatched only at their edges. This also avoids Revit's 0.05 mm pens at 1:200 and 1:500.
- **Notes use generic material words** from MasterFormat ("membrane roofing", not a brand). Same rule as our material names.
- **Dimensions:** at least 14.5 mm from the object and 10 mm between strings. USACE ties these to text height (10 mm and 7.5 mm at 2.5 mm text). We use the USACE rule so spacing scales with text.
- **Fonts capitalized, sans-serif.** The NCS joins PSPC and DND on upper case, which weighs on the case decision.

The NCS is licensed, so this document summarizes it and does not copy its tables.

Checked but no graphics or material rules: US GSA, NATSPEC's BIM guide (Australia), Finland's COBIM, Singapore's CORENET X. Paid and not read: AS 1100, BS 1192, BS 8541 (NCS read in its older version 4). The full NLRS line and text rules need a free login.

## Sources

Pages opened on 2026-10-17. Not readable: PSPC Revit template annex, US National CAD Standard drafting module and CSA B651 (paid).

- [ISO 128-2:2020 sample](https://cdn.standards.iteh.ai/samples/69129/38e651842df746fd990d29679e3c2e98/ISO-128-2-2020.pdf)
- [ISO 3098-0:1997 sample](https://cdn.standards.iteh.ai/samples/24792/1811286881f24ea49763a66a747029c8/ISO-3098-0-1997.pdf)
- [AEC (UK) BIM Standard for Revit v1.0](https://aecuk.files.wordpress.com/2010/06/aecukbimstandardforrevit-v1-0.pdf)
- [PSPC National CADD Standard 2024](https://web.archive.org/web/20241215044218if_/https://publications.gc.ca/collections/collection_2024/spac-pspc/P26-4-2024-eng.pdf)
- [US National CAD Standard FAQ](https://www.nationalcadstandard.org/ncs6/faqs.php)
- [Metrolinx CADD/BIM Standards Manual](https://assets.metrolinx.com/image/upload/v1738161857/Documents/Engineering/Metrolinx_CADD_BIM_Standards_Manual.pdf)
- [CNIB Clear Print Guide](https://cnib.ca/sites/default/files/2018-07/CNIB%20Clear%20Print%20Guide.pdf)
- [Open BIM Object Standard, NZ draft](https://masterspec.co.nz/filescust/CMS/Open%20BIM%20Object%20Standard%20Consultation%20Draft%20NZ.pdf)
- [bimstore bible v13.1 (archived)](https://web.archive.org/web/20220309004758if_/https://storage.googleapis.com/bimstore-uploads/uk/uploads/software/bimstore-bible-v13-1-published.pdf)
- [Revit API ThermalAsset](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/c4dac7e3-96e2-bc6c-1299-f696a253e879.htm)
- [DND BIM Template and Content User Guide v1.0](https://www.geniemdnquebec.ca/SalleTechniqueCommuns/DND_BIM_Template_and_Content_User_Guide_v1.0.pdf)
- [SQI exigences BIM](https://www.sqi.gouv.qc.ca/fileadmin/expertises/bim-pci/fr-gp-030.079_-exigences_bim_web.pdf)

National Defence [CAD/BIM Standard 2.3, C-98-002-CAD/FP-003](https://cafnet.dcc-cdc.gc.ca/cad_bim_gis/index-eng.htm), August 2012 (sections 2.1, 3, 4.3 to 4.7, Appendices A to C; access needs a DND account). DND states the standard is its property and only pre-authorized use is allowed, so this document cites it and does not copy its tables.



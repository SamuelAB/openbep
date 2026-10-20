# OpenBEP Revit Parameter Dictionary v0.1

Oct 17, 2026 · @Samuel Arsenault-Brassard

## Why one list

One owner asks for "Fire Rating", the next for "FRR", a third for "Degré coupe-feu". Each is a different parameter with a different GUID, so the data from one project can't be read on the next, and nobody can compare buildings across a portfolio.

This is what vague BIM looks like in practice. When the only instruction is "do the BIM", every firm invents its own parameters, and the models don't talk to each other.

This dictionary fixes the list once for Canada:

- **One name and one GUID per piece of information.** CA\_FireResistanceRating means the same thing on every project.
- **Revit's own parameters first.** If Revit already has the field (Fire Rating, Mark, Assembly Code), we use it and add nothing.
- **Each parameter is tied to a BIM use and an LOD.** The BEP Builder only asks for the ones the project's chosen uses need.
- **Accepted values are written down.** A programmer could check every value automatically, and so can our model checker.

Version 0.1 has 398 parameters: 300 OpenBEP shared parameters and 98 Revit built-ins, across 63 element types and 17 BIM uses. It has not yet been tested in Revit.

## Rules

Parameter names are code, not text for people. They stay in English everywhere in Canada, and French lives in the labels, the schedules and this document.

| Rule | Why | Règle |
| --- | --- | --- |
| Names start with `CA_`, then PascalCase English, no spaces or accents: `CA_FireResistanceRating` | Scripts, exports and IFC mapping break on accents, spaces and "FR \| EN" names | Les noms commencent par `CA_`, en anglais, sans espaces ni accents |
| Numbers carry their unit in the name when Revit has no unit type: `CA_DesignFlow_Ls` | The value is unreadable without the unit | Le nom d'un nombre sans type d'unité Revit inclut l'unité |
| Each parameter has a fixed GUID made from its name (UUID v5) and never changes | Same GUID = same data on every project | Chaque paramètre a un GUID fixe qui ne change jamais |
| If Revit has a built-in parameter, use it (Fire Rating, Mark, Assembly Code) | Avoids two fields for the same fact | Si Revit a déjà le paramètre, on l'utilise |
| Every parameter has an English label and a French label | People read labels, scripts read names | Chaque paramètre a une étiquette anglaise et une étiquette française |
| French schedules rename the column heading, not the parameter | Revit lets you change a schedule heading without touching the data | Les nomenclatures françaises changent l'en-tête de colonne, pas le paramètre |
| Values from a list are stored as codes (`EI60`, `PASS`, `NA`), never as sentences | A code reads the same in both languages and can be checked | Les valeurs de liste sont des codes, pas des phrases |
| Free text that must be bilingual uses two fields: `_EN` and `_FR` | One field with a vertical bar can't be sorted, checked or exported cleanly | Un texte libre bilingue utilise deux champs, `_EN` et `_FR` |
| No placeholder values: "TBD", "?", "0" and "-" are rejected | A placeholder looks filled but carries no information | Aucune valeur bidon : « À venir », « ? », « 0 » et « - » sont refusés |

We chose English names because a French-only list would split Canada in two, and "FR | EN" names double the length of every name and still break scripts. The cost is that every explanation must exist in French, which this document does.

What others do: National Defence uses one name holding both languages, such as `DND_Location | Emplacement` ([DND guide, 2021](https://www.geniemdnquebec.ca/SalleTechniqueCommuns/DND_BIM_Template_and_Content_User_Guide_v1.0.pdf), spelling to check against the PDF). The UK [bimstore bible](https://www.bimstore.co/bimstore-bible) still exists (version 17, login needed) and uses English names in CamelCase. We found no rule on parameter language from PSPC, SQI, Ville de Montréal or Hydro-Québec. Revit can't show a shared parameter's name in another language ([AUGI forum](https://forums.augi.com/showthread.php?151769-Shared-parameter-displayed-in-different-languages)), which is why the French goes in labels and schedule headings.

## Data types and how values are checked

Every parameter has one data type and one rule, so a value is either right or wrong. The model checker applies the rules, and they are written out in both languages in OpenBEP\_Validation\_Rules\_v0.1.json.

| Type | Revit data type | Count | What is accepted | Example | Type (FR) |
| --- | --- | --- | --- | --- | --- |
| Text | Text | 95 | Free text, or a fixed format where the rule gives one (fire rating: `20 min` to `4 h` only) | `1.5 h` | Texte |
| Number | Number | 89 | A number in the unit named in the parameter, within min and max | 25 (L/s) | Nombre |
| Length | Length | 62 | Greater than zero, in mm (47) or m (15) | 200 | Longueur |
| Code list | Text | 48 | One code from a fixed list; anything else is wrong | `GAS`, `NOTDEFINED` | Liste de codes |
| Yes/No | Yes/No | 41 | Yes or No; empty is not allowed once required | Yes | Oui/Non |
| Identifier | Text | 17 | Text matching a pattern, no spaces | `C1020`, `AHU3.SAT` | Identifiant |
| Integer | Integer | 15 | A whole number within min and max | 4 | Entier |
| Multiline text | Multiline text | 6 | Free text | Maintenance notes | Texte multiligne |
| Area, volume | Area, Volume | 9 | Greater than zero, in m² or m³ | 12.5 | Surface, volume |
| Duration | Text | 2 | ISO 8601 duration | `P25Y`, `P18M` | Durée |
| Date | Text | 2 | ISO date, YYYY-MM-DD | 2026-10-17 | Date |

Where the codes come from: 34 lists are IFC 4.3 property enumerations, 13 are OpenBEP proposals (such as NBC occupancy groups and CSA exposure classes), and 1 is Revit's own System Classification. Placeholder values ("TBD", "N/A", "0", "Default", "\<By Category>") always fail.

Still to check: the fire-rating list, the Uniformat and OmniClass patterns, and the CSA class lists against current editions. Fields like voltage ("120/208") hold one number for now.

## How to use it

1. **Pick the BIM uses and LODs in the BEP Builder.** The tool works out which parameters each element type needs, phase by phase.
2. **Download two files** from the step "Element requirements": the OpenBEP shared parameter file (.txt) and the project's binding list (.csv).
3. **Load the .txt in Revit** (Manage › Shared Parameters). It holds all 300 OpenBEP parameters in 8 groups, with their fixed GUIDs.
4. **Run OpenBEP › Sync parameters** (the Revit add-in). It reads the binding list, shows what it will add or bind and any duplicates with the same name but another GUID, writes a report, and changes nothing until you confirm.
5. **Check the model** with the BEP Builder's model checker: export the schedules and it flags missing or wrong values against the rules below.

The add-in is written but not yet built or tested. A Revit MCP tool using the same logic comes after.

*En bref : on choisit les usages BIM dans l'outil, on télécharge le fichier de paramètres partagés et la liste de liaisons, et le module d'extension Revit ajoute les bons paramètres aux bonnes catégories après confirmation.*

## The parameters

386 parameter names in 8 groups. Revit built-ins come first in each group. "From LOD" is the lowest LOD at which any element needs the parameter. The full rule for each one, in English and French, is in the rules file; the per-element detail is in the manifest.

*Les 386 paramètres, en 8 groupes. Chaque ligne donne l'étiquette française. La règle complète en français est dans le fichier de règles.*

### 01 Identity and asset (38)

| Parameter | Label EN / FR | Type | Accepted values | Revit categories | BIM uses | From LOD |
| --- | --- | --- | --- | --- | --- | --- |
| `Assembly Code` (built-in) | UniFormat II code / Code Uniformat II | Identifier (text), type | Pattern; e.g. `C1020`, `B2010.10` | Air Terminals, Cable Tray Fittings, Cable Trays, Casework, Ceilings, Communication Devices +49 | Asset register, Cost estimating, Quantity take-off | 300 |
| `Default Thickness` (built-in) | Overall thickness / Épaisseur totale | Length, type | > 0 mm; e.g. 200 | Floors, Structural Foundations, Structural Framing | Cost estimating, Design review / authoring, Quantity take-off | 200 |
| `Description` (built-in) | Description / Description | Multiline text, type | Free text; e.g. Recessed LED troffer 600x1200 | Air Terminals, Casework, Ceilings, Communication Devices, Curtain Panels, Curtain Wall Mullions +34 | Design review / authoring, Facility management / maintenance, Handover / record model | 300 |
| `Export to IFC As / IFC Predefined Type (names vary by Revit version - verify)` (built-in) | IFC class and predefined type / Classe et type prédéfini IFC | Identifier (text), type | Pattern; e.g. `IfcLightFixture.POINTSOURCE`, `IfcWall` | Air Terminals, Areas, Cable Tray Fittings, Cable Trays, Casework, Ceilings +60 | 3D coordination / clash avoidance, Asset register, Code and accessibility compliance checking, Handover / record model +1 | 200 |
| `Fire Rating` (built-in) | Fire-resistance rating / Degré de résistance au feu | Text, type | Pattern; e.g. `1.5 h`, `45 min` | Areas, Ceilings, Curtain Panels, Curtain Wall Mullions, Doors, Duct Accessories +13 | Code and accessibility compliance checking, Facility management / maintenance | 300 |
| `Function (walls) or shared` (built-in) | Is external / Est extérieur | Yes/No, type | `Yes`, `No` | Curtain Panels, Curtain Wall Mullions, Doors, Floors, Landings, Railings +8 | Code and accessibility compliance checking, Cost estimating, Design review / authoring, Energy and load analysis | 200 |
| `Length` (built-in) | Length / Longueur | Length, instance | > 0 mm; e.g. 12.4 | Cable Tray Fittings, Cable Trays, Conduit Fittings, Conduits, Duct Fittings, Ducts +9 | Cost estimating, Quantity take-off | 200 |
| `Manufacturer` (built-in) | Manufacturer / Fabricant | Text, type | Free text; e.g. Acme Lighting Inc. | Air Terminals, Casework, Ceilings, Communication Devices, Curtain Panels, Curtain Wall Mullions +27 | Asset register, Cost estimating, Design review / authoring, Facility management / maintenance +3 | 300 |
| `Mark` (built-in) | Instance mark / tag / Identifiant unique (no.) | Identifier (text), instance | Pattern; e.g. `D-2.014`, `AHU-3` | Casework, Communication Devices, Data Devices, Doors, Duct Accessories, Electrical Equipment +26 | 3D coordination / clash avoidance, Asset register, Code and accessibility compliance checking, Commissioning +4 | 200 |
| `Material / Structural Material / compound structure` (built-in) | Primary material / Matériau | Text, type | Free text; e.g. Concrete 35 MPa | Cable Tray Fittings, Cable Trays, Conduit Fittings, Conduits, Duct Fittings, Ducts +18 | Asset register, Cost estimating, Energy and load analysis, Quantity take-off +1 | 200 |
| `Model` (built-in) | Model name / number / Modèle | Text, type | Free text; e.g. TRX-24-40K-L | Air Terminals, Casework, Ceilings, Communication Devices, Curtain Panels, Curtain Wall Mullions +24 | Asset register, Cost estimating, Facility management / maintenance, Handover / record model +1 | 300 |
| `Mounting` (built-in) | Mounting type / Type de montage | Code list (text), type | `SURFACE`, `RECESSED`, `SEMIRECESSED`, `FREESTANDING` | Electrical Equipment | 3D coordination / clash avoidance | 300 |
| `Panel / Circuit Number (read-only, from electrical circuits)` (built-in) | Panel / circuit / Panneau / circuit | Identifier (text), instance | Pattern; e.g. `LP-2A-14`, `DP-1-2,4,6` | Electrical Equipment, Electrical Fixtures, Lighting Devices, Lighting Fixtures, Mechanical Equipment, Plumbing Equipment | 3D coordination / clash avoidance, Commissioning, Facility management / maintenance | 300 |
| `Phase Created / Phase Demolished (map to IFC Status on export)` (built-in) | Status (new / existing / demolish / temporary) / Statut (neuf / existant / à démolir / temporaire) | Code list (text), instance | `DEMOLISH`, `EXISTING`, `NEW`, `TEMPORARY`, `OTHER`, `NOTKNOWN`, `UNSET` | Air Terminals, Areas, Cable Tray Fittings, Cable Trays, Casework, Ceilings +56 | Cost estimating, Existing conditions modelling / renovation, Quantity take-off, Space management | 200 |
| `Room / Space (calculated, read-only)` (built-in) | Room / space served or containing / Local | Identifier (text), instance | Pattern; e.g. `2.014`, `B1-105` | Air Terminals, Casework, Communication Devices, Data Devices, Doors, Duct Accessories +19 | Commissioning, Facility management / maintenance, Handover / record model, Space management | 300 |
| `Structural (walls) / Structural Usage` (built-in) | Load bearing / Est porteur | Yes/No, type | `Yes`, `No` | Floors, Landings, Ramps, Roofs, Runs, Stairs +5 | 3D coordination / clash avoidance, Code and accessibility compliance checking | 200 |
| `Structure (compound structure)` (built-in) | Layer build-up / Composition | Text, type | Free text; e.g. 16 GWB / 92 steel stud + insulation / 16 GWB | Floors, Hardscape, Parking, Roofs, Structural Foundations, Walls | Cost estimating, Design review / authoring, Energy and load analysis, Quantity take-off +1 | 300 |
| `System Name` (built-in) | System served / Système desservi | Text, instance | Free text; e.g. AHU-3 / Zone N | Areas, Duct Accessories, Duct Fittings, Ducts, Flex Ducts, Flex Pipes +8 | 3D coordination / clash avoidance, Commissioning, Facility management / maintenance | 200 |
| `Type Mark` (built-in) | Type mark / reference / Marque de type | Identifier (text), type | Pattern; e.g. `A1`, `LF-A1` | Air Terminals, Casework, Ceilings, Communication Devices, Curtain Panels, Curtain Wall Mullions +38 | Cost estimating, Design review / authoring, Quantity take-off | 300 |
| `Type Name` (built-in) | Type name / Nom du type | Text, type | Free text; e.g. LF-A1 2x4 LED troffer | Air Terminals, Cable Tray Fittings, Cable Trays, Casework, Ceilings, Communication Devices +50 | Cost estimating, Design review / authoring, Handover / record model, Quantity take-off | 200 |
| `Width` (built-in) | Overall thickness / Épaisseur totale | Length, type | > 0 mm; e.g. 200 | Doors, Structural Foundations, Walls, Windows | Code and accessibility compliance checking, Cost estimating, Energy and load analysis, Quantity take-off +1 | 200 |
| `CA_BarcodeAssetLabel` | Barcode / asset label / Code-barres | Identifier (text), instance | Pattern; e.g. `QC-SQI-000123`, `00012345678905` | Casework, Communication Devices, Data Devices, Doors, Duct Accessories, Electrical Equipment +14 | Asset register, Facility management / maintenance | 500 |
| `CA_BuildingAutomationReference` | Building automation (BAS) reference / Référence GTB (gestion technique du bâtiment) | Identifier (text), instance | Pattern; e.g. `AHU3.SAT`, `B2.VAV-2-14.ZNT` | Duct Accessories, Electrical Equipment, Mechanical Equipment, Pipe Accessories, Plumbing Equipment | Commissioning, Facility management / maintenance | 400 |
| `CA_EnergySourceFuel` | Energy source / fuel / Source d'énergie | Code list (text), type | `COAL`, `COAL_PULVERIZED`, `ELECTRICITY`, `GAS`, `OIL`, `PROPANE`, `WOOD`, `WOOD_CHIP`, `WOOD_PELLET`, `WOOD_PULVERIZED`, `OTHER`, `NOTKNOWN` (+1 more) | Electrical Equipment, Mechanical Equipment, Plumbing Equipment | Code and accessibility compliance checking, Design review / authoring, Energy and load analysis | 200 |
| `CA_ExpectedServiceLife` | Expected service life / Durée de vie utile | Duration, type | ISO 8601; e.g. `P20Y`, `P50Y` | Casework, Ceilings, Communication Devices, Curtain Panels, Curtain Wall Mullions, Data Devices +28 | Asset register, Facility management / maintenance | 500 |
| `CA_FinishColour` | Finish colour / Couleur de finition | Text, type | Free text; e.g. White RAL 9010 | Air Terminals, Casework, Furniture, Furniture Systems, Plumbing Fixtures | Design review / authoring, Visualisation and photometric rendering | 300 |
| `CA_FireResistanceRating` | Fire-resistance rating / Degré de résistance au feu | Text, type | Pattern; e.g. `1.5 h`, `1 h` | Duct Accessories | Code and accessibility compliance checking, Facility management / maintenance | 300 |
| `CA_GrossWeight_kg` | Gross weight / Poids brut | Number, type | > 0 kg; e.g. 1850 | Electrical Equipment, Floors, Food Service Equipment, Lighting Fixtures, Mechanical Equipment, Medical Equipment +5 | 3D coordination / clash avoidance, Cost estimating, Design review / authoring, Prefabrication / digital fabrication | 350 |
| `CA_InstallationDate` | Installation date / Date d'installation | Date (text), instance | YYYY-MM-DD; e.g. `2027-05-14` | Casework, Communication Devices, Curtain Panels, Curtain Wall Mullions, Data Devices, Doors +31 | Asset register, Facility management / maintenance, Handover / record model | 500 |
| `CA_MountingType` | Mounting type / Type de montage | Code list (text), type | `CABLESPANNED`, `FREESTANDING`, `POLE_SIDE`, `POLE_TOP`, `RECESSED`, `SURFACE`, `SUSPENDED`, `TRACKMOUNTED`, `FLATFLUSH`, `LAYIN`, `BACKTOWALL`, `COUNTERTOP` (+15 more) | Air Terminals, Lighting Fixtures, Mechanical Equipment, Plumbing Fixtures | 3D coordination / clash avoidance, Cost estimating, Design review / authoring | 300 |
| `CA_OwnerAssetIdentifier` | Owner asset identifier / Identifiant d'actif du propriétaire | Identifier (text), instance | Pattern; e.g. `AHU-0003`, `P-CHW-001` | Casework, Communication Devices, Curtain Panels, Curtain Wall Mullions, Data Devices, Doors +30 | Asset register, Facility management / maintenance | 500 |
| `CA_PieceFabricationMark` | Piece / fabrication mark / Repère de fabrication | Identifier (text), instance | Pattern; e.g. `C12-L2`, `B204` | Duct Fittings, Ducts, Flex Ducts, Flex Pipes, Floors, Landings +12 | Prefabrication / digital fabrication | 400 |
| `CA_PositionalAccuracy` | Positional accuracy (as-built) / Exactitude de position (tel que construit) | Length, instance | > 0 mm; e.g. 25 | Cable Tray Fittings, Cable Trays, Conduit Fittings, Conduits, Duct Fittings, Ducts +16 | Existing conditions modelling / renovation, Handover / record model | 200 |
| `CA_Prefabricated` | Prefabricated (true/false) / Statut de préfabrication | Yes/No, type | `Yes`, `No` | Cable Tray Fittings, Cable Trays, Conduit Fittings, Conduits, Curtain Panels, Curtain Wall Mullions +24 | Prefabrication / digital fabrication | 400 |
| `CA_SerialNumber` | Serial number / Numéro de série | Identifier (text), instance | Pattern; e.g. `SN-0049231`, `4K21J0093` | Communication Devices, Data Devices, Doors, Duct Accessories, Electrical Equipment, Food Service Equipment +12 | Asset register, Facility management / maintenance, Handover / record model | 500 |
| `CA_StationChainageRange` | Station / chainage range / Chaînage (début - fin) | Length, instance | ≥ 0 m; e.g. 10250 | Floors, Generic Models, Signage, Specialty Equipment, Structural Columns, Structural Foundations +2 | 3D coordination / clash avoidance, Asset register | 200 |
| `CA_WarrantyDuration` | Warranty duration / Durée de garantie | Duration, type | ISO 8601; e.g. `P5Y`, `P18M` | Casework, Communication Devices, Curtain Panels, Curtain Wall Mullions, Data Devices, Doors +22 | Facility management / maintenance, Handover / record model | 500 |
| `CA_WarrantyStartDate` | Warranty start date / Début de garantie | Date (text), instance | YYYY-MM-DD; e.g. `2027-06-01` | Casework, Communication Devices, Curtain Panels, Curtain Wall Mullions, Data Devices, Doors +22 | Facility management / maintenance, Handover / record model | 500 |

### 02 Architecture (86)

| Parameter | Label EN / FR | Type | Accepted values | Revit categories | BIM uses | From LOD |
| --- | --- | --- | --- | --- | --- | --- |
| `Actual Number of Risers` (built-in) | Number of risers / Nombre de contremarches | Integer, instance | ≥ 1; e.g. 18 | Landings, Runs, Stairs | Code and accessibility compliance checking | 300 |
| `Actual Riser Height` (built-in) | Riser height / Hauteur de contremarche | Length, instance | > 0 and ≤ 250 mm; e.g. 180 | Landings, Runs, Stairs | Code and accessibility compliance checking | 300 |
| `Actual Run Width / Width` (built-in) | Clear width / Largeur libre | Length, instance | > 0 mm; e.g. 1100 | Floors, Landings, Ramps, Runs, Stairs, Structural Foundations | Code and accessibility compliance checking, Design review / authoring | 200 |
| `Actual Tread Depth` (built-in) | Tread run / Giron | Length, instance | > 0 and ≤ 500 mm; e.g. 280 | Landings, Runs, Stairs | Code and accessibility compliance checking | 300 |
| `Apparent Load (connector)` (built-in) | Connected electrical load / Charge électrique raccordée | Number, type | > 0 kW; e.g. 3.2 | Food Service Equipment, Medical Equipment, Specialty Equipment, Vertical Circulation | Design review / authoring, Energy and load analysis | 300 |
| `Area` (built-in) | Net side area / Surface nette | Area, instance | > 0 m²; e.g. 18.4 | Areas, Ceilings, Curtain Panels, Curtain Wall Mullions, Duct Fittings, Ducts +9 | Cost estimating, Energy and load analysis, Handover / record model, Quantity take-off +1 | 200 |
| `Cost` (built-in) | Unit cost / Coût unitaire | Number, type | > 0 CAD; e.g. 1450.00 | Casework, Furniture, Furniture Systems | Cost estimating | 300 |
| `Department` (built-in) | Department / Service | Text, instance | Free text; e.g. Administration | Areas, Rooms, Spaces | Space management | 300 |
| `Floor / Wall / Ceiling / Base Finish` (built-in) | Finishes / Finis | Text, instance | Pattern; e.g. `RF-1 / PT-2 / ACT-1 / RB-1`, `CPT-3` | Areas, Rooms, Spaces | Cost estimating, Design review / authoring, Facility management / maintenance | 300 |
| `Heat Transfer Coefficient (U) - analytical properties` (built-in) | Thermal transmittance (U) / Transmittance thermique (U) | Number, type | > 0 W/(m²·K); e.g. 1.9 | Curtain Panels, Curtain Wall Mullions, Doors, Floors, Roofs, Structural Foundations +2 | Energy and load analysis | 200 |
| `Height` (built-in) | Nominal height / Hauteur | Length, instance/type | > 0 mm; e.g. 2134 | Doors, Windows | Cost estimating, Energy and load analysis, Quantity take-off | 200 |
| `Height Offset From Level` (built-in) | Ceiling height / Hauteur du plafond | Length, instance | > 0 mm; e.g. 2700 | Ceilings | 3D coordination / clash avoidance, Code and accessibility compliance checking, Design review / authoring | 200 |
| `Name` (built-in) | Room name / Nom du local | Text, instance | Free text; e.g. Classroom | Areas, HVAC Zones, Rooms, Spaces | Design review / authoring, Facility management / maintenance, Space management | 200 |
| `Number` (built-in) | Room number / Numéro de local | Identifier (text), instance | Pattern; e.g. `2.014`, `B1-105` | Areas, Rooms, Spaces | Design review / authoring, Facility management / maintenance, Handover / record model, Space management | 200 |
| `Railing Height` (built-in) | Guard / railing height / Hauteur du garde-corps | Length, type | > 0 mm; e.g. 1070 | Railings | Code and accessibility compliance checking, Design review / authoring | 200 |
| `Ramp Max Slope (1/x)` (built-in) | Slope / Pente | Number, instance | ≥ 1; e.g. 12 | Ramps | Code and accessibility compliance checking, Design review / authoring, Handover / record model | 200 |
| `Sill Height` (built-in) | Sill height / Hauteur d'allège | Length, instance | > 0 mm; e.g. 900 | Windows | Code and accessibility compliance checking, Design review / authoring | 300 |
| `Slope` (built-in) | Slope / Pente | Number, instance | ≥ 0 %; e.g. 2 | Flex Pipes, Pipe Fittings, Pipes, Roofs | Code and accessibility compliance checking, Design review / authoring | 300 |
| `Solar Heat Gain Coefficient - analytical properties` (built-in) | Solar heat gain coefficient / Coefficient de gain de chaleur solaire (CGCS) | Number, type | > 0 and ≤ 1; e.g. 0.30 | Curtain Panels, Curtain Wall Mullions, Walls, Windows | Energy and load analysis, Lighting / daylighting analysis | 300 |
| `Thermal Resistance (R) - analytical properties` (built-in) | Insulation thermal resistance (RSI) / Résistance thermique de l'isolant (RSI) | Number, type | > 0 m²·K/W; e.g. 5.3 | Roofs | Energy and load analysis | 300 |
| `Unbounded Height` (built-in) | Height / Hauteur | Length, instance | > 0 mm; e.g. 2700 | Areas, Rooms, Spaces | Code and accessibility compliance checking, Energy and load analysis | 300 |
| `Visual Light Transmittance - analytical properties` (built-in) | Visible light transmittance / Transmission de la lumière visible | Number, type | > 0 and ≤ 1; e.g. 0.55 | Curtain Panels, Curtain Wall Mullions, Walls, Windows | Lighting / daylighting analysis, Visualisation and photometric rendering | 300 |
| `Volume` (built-in) | Volume / Volume | Volume, instance | > 0 m³; e.g. 173 | Areas, Floors, Rooms, Spaces, Structural Columns, Structural Foundations +2 | Cost estimating, Energy and load analysis, Quantity take-off | 300 |
| `CA_AccessibleCeiling` | Accessible (demountable) ceiling / Plafond démontable | Yes/No, type | `Yes`, `No` | Ceilings | 3D coordination / clash avoidance, Facility management / maintenance | 300 |
| `CA_AcousticRating` | Acoustic rating (STC / ASTC) / Indice de transmission du son | Text, type | Pattern; e.g. `STC 50`, `STC 45` | Curtain Panels, Curtain Wall Mullions, Doors, Floors, Structural Foundations, Walls +1 | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_AirLeakageRate_Lsm2` | Air leakage rate / Taux d'infiltration d'air | Number, type | > 0 L/(s·m²); e.g. 0.3 | Curtain Panels, Curtain Wall Mullions, Walls, Windows | Energy and load analysis | 300 |
| `CA_BarrierFree` | Barrier-free (accessible) / Sans obstacles | Yes/No, type | `Yes`, `No` | Doors | Code and accessibility compliance checking | 300 |
| `CA_BarrierFreeFeatures` | Barrier-free features / Conception sans obstacles | Yes/No, type | `Yes`, `No` | Areas, Floors, Landings, Ramps, Rooms, Runs +3 | Code and accessibility compliance checking | 300 |
| `CA_BirdFriendlyGlazing` | Bird-friendly glazing / Vitrage sécuritaire pour les oiseaux | Yes/No, type | `Yes`, `No` | Curtain Panels, Curtain Wall Mullions, Walls, Windows | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_BuiltIn` | Built-in / Encastré | Yes/No, type | `Yes`, `No` | Casework, Furniture, Furniture Systems | Cost estimating, Space management | 300 |
| `CA_CarInsideDimensions` | Car inside dimensions / Dimensions intérieures de la cabine | Text, type | Pattern; e.g. `1600 x 2400 x 2400`, `1100 x 2100 x 2300` | Specialty Equipment, Vertical Circulation | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_CeilingSystem` | Ceiling system / Système de plafond | Text, type | Free text; e.g. 600x600 mineral fibre on 24 mm grid | Ceilings | Cost estimating, Design review / authoring, Facility management / maintenance | 300 |
| `CA_CombustibleConstruction` | Combustible construction / Construction combustible | Yes/No, type | `Yes`, `No` | Ceilings, Walls | Code and accessibility compliance checking | 300 |
| `CA_CurtainWallSystem` | Curtain wall system / Système de mur-rideau | Text, type | Free text; e.g. Unitized, 150 mm thermally broken aluminium | Curtain Panels, Curtain Wall Mullions, Walls | Cost estimating, Design review / authoring | 300 |
| `CA_DesignLoad_kNm` | Design load / Charge de calcul | Number, type | > 0 kN/m; e.g. 0.75 | Railings | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_DesignTemperatureSetPoints` | Design temperature set-points / Consignes de température de conception | Text, instance | Pattern; e.g. `21 heating / 24 cooling`, `20.5 heating / 23.5 cooling` | Areas, Rooms, Spaces | Commissioning, Energy and load analysis | 300 |
| `CA_ExtendsToStructure` | Extends to structure / Jusqu'à la structure | Yes/No, type | `Yes`, `No` | Walls | 3D coordination / clash avoidance, Code and accessibility compliance checking, Cost estimating | 300 |
| `CA_Finish` | Finish / Fini | Multiline text, type | Free text; e.g. White, fine fissured | Ceilings | Design review / authoring, Visualisation and photometric rendering | 300 |
| `CA_FireCompartmentBoundary` | Fire compartment boundary / Séparation coupe-feu | Yes/No, type | `Yes`, `No` | Floors, Structural Foundations, Walls | Code and accessibility compliance checking, Facility management / maintenance | 300 |
| `CA_FireExit` | Fire exit / Issue | Yes/No, instance | `Yes`, `No` | Doors | Code and accessibility compliance checking | 300 |
| `CA_FirefightersElevator` | Firefighters' elevator / Ascenseur pour pompiers | Yes/No, type | `Yes`, `No` | Specialty Equipment, Vertical Circulation | Code and accessibility compliance checking | 300 |
| `CA_FrameMaterial` | Frame material / Matériau du cadre | Text, type | Free text; e.g. Hollow metal | Doors, Windows | Cost estimating, Visualisation and photometric rendering | 300 |
| `CA_GlazingMakeUp` | Glazing make-up / Composition du vitrage | Text, type | Free text; e.g. 6 low-e / 13 argon / 6 clear | Curtain Panels, Curtain Wall Mullions, Walls, Windows | Cost estimating, Design review / authoring | 300 |
| `CA_GrossFloorArea` | Gross floor area / Superficie brute | Area, instance | > 0 m²; e.g. 68.9 | Areas, Rooms, Spaces | Space management | 300 |
| `CA_HandrailDiameter` | Handrail diameter / Diamètre de la main courante | Length, type | > 0 and ≤ 100 mm; e.g. 38 | Railings | Code and accessibility compliance checking | 300 |
| `CA_HardwareSet` | Hardware set / Groupe de quincaillerie | Identifier (text), type | Pattern; e.g. `HW-07`, `HW-112A` | Doors | Cost estimating, Design review / authoring, Facility management / maintenance | 300 |
| `CA_Headroom` | Headroom / Échappée | Length, instance | > 0 mm; e.g. 2050 | Landings, Ramps, Runs, Stairs | Code and accessibility compliance checking | 300 |
| `CA_InfillType` | Infill type / Type de remplissage | Text, type | Free text; e.g. Tempered laminated glass | Railings | Code and accessibility compliance checking, Cost estimating, Design review / authoring | 300 |
| `CA_LeafMaterial` | Leaf material / Matériau de la porte | Text, type | Free text; e.g. Solid core wood | Doors | Cost estimating, Visualisation and photometric rendering | 300 |
| `CA_LegendMessage` | Legend / message / Message | Multiline text, type | Free text; e.g. Exit / Sortie | Generic Models, Signage, Specialty Equipment | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_MajorOccupancy` | Major occupancy (NBC group) / Usage principal (groupe du CNB) | Code list (text), instance | `A-1`, `A-2`, `A-3`, `A-4`, `B-1`, `B-2`, `B-3`, `C`, `D`, `E`, `F-1`, `F-2` (+1 more) | Areas, Rooms, Spaces | Code and accessibility compliance checking | 300 |
| `CA_MemberSpaces` | Member spaces / Locaux membres | Text, instance | Pattern; e.g. `2.010; 2.011; 2.014`, `B1-105` | Areas, HVAC Zones, Rooms | Facility management / maintenance, Handover / record model, Space management | 300 |
| `CA_NominalDimensions` | Nominal dimensions / Dimensions nominales | Text, type | Pattern; e.g. `1800 x 750 x 740`, `600 x 600 x 900` | Casework, Furniture, Furniture Systems | Design review / authoring, Space management | 200 |
| `CA_NonSlipSurface` | Non-slip surface / Surface antidérapante | Yes/No, type | `Yes`, `No` | Landings, Ramps, Runs, Stairs | Code and accessibility compliance checking | 300 |
| `CA_OccupantLoad` | Occupant load / Nombre de personnes | Integer, instance | ≥ 0; e.g. 32 | Areas, Rooms, Spaces | Code and accessibility compliance checking, Energy and load analysis | 300 |
| `CA_OperationPartitioning` | Operation / partitioning / Mode d'ouverture | Code list (text), type | `SINGLE_PANEL`, `DOUBLE_PANEL_VERTICAL`, `DOUBLE_PANEL_HORIZONTAL`, `TRIPLE_PANEL_VERTICAL`, `TRIPLE_PANEL_BOTTOM`, `TRIPLE_PANEL_TOP`, `TRIPLE_PANEL_LEFT`, `TRIPLE_PANEL_RIGHT`, `TRIPLE_PANEL_HORIZONTAL`, `USERDEFINED`, `NOTDEFINED` | Windows | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_OperationType` | Operation type / Mode d'ouverture | Code list (text), type | `SINGLE_SWING_LEFT`, `SINGLE_SWING_RIGHT`, `DOUBLE_DOOR_SINGLE_SWING`, `DOUBLE_DOOR_SINGLE_SWING_OPPOSITE_LEFT`, `DOUBLE_DOOR_SINGLE_SWING_OPPOSITE_RIGHT`, `DOUBLE_SWING_LEFT`, `DOUBLE_SWING_RIGHT`, `DOUBLE_DOOR_DOUBLE_SWING`, `SLIDING_TO_LEFT`, `SLIDING_TO_RIGHT`, `DOUBLE_DOOR_SLIDING`, `FOLDING_TO_LEFT` (+13 more) | Doors | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_OutdoorAirVentilation_Ls` | Outdoor-air ventilation / Débit d'air neuf | Number, instance | > 0 L/s; e.g. 180 | Areas, Rooms, Spaces | Code and accessibility compliance checking, Energy and load analysis | 300 |
| `CA_PartOfAnExit` | Part of an exit / Fait partie d'une issue | Yes/No, instance | `Yes`, `No` | Landings, Ramps, Runs, Stairs | Code and accessibility compliance checking | 300 |
| `CA_PerformanceClass` | Performance class (NAFS) / Classe de performance (NAFS) | Text, type | Pattern; e.g. `CW-PG50`, `LC-PG35` | Curtain Panels, Curtain Wall Mullions, Walls, Windows | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_ProgramArea` | Program area / Superficie au programme | Area, instance | > 0 m²; e.g. 65 | Areas, Rooms, Spaces | Design review / authoring, Space management | 200 |
| `CA_RatedLoad_kg` | Rated load / Charge nominale | Number, type | > 0 kg; e.g. 1600 | Specialty Equipment, Vertical Circulation | Code and accessibility compliance checking, Design review / authoring | 200 |
| `CA_RatedPassengers` | Rated passengers / Nombre de passagers | Integer, type | ≥ 1; e.g. 21 | Specialty Equipment, Vertical Circulation | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_RatedSpeed_ms` | Rated speed / Vitesse nominale | Number, type | > 0 m/s; e.g. 1.75 | Specialty Equipment, Vertical Circulation | Design review / authoring, Energy and load analysis | 300 |
| `CA_RegulatorDeviceNumber` | Regulator device number / Numéro d'enregistrement de l'appareil | Text, instance | Free text; e.g. TSSA-EL-004512 | Specialty Equipment, Vertical Circulation | Asset register, Facility management / maintenance, Handover / record model | 500 |
| `CA_RequiredIlluminance_lx` | Required illuminance (space) / Éclairement requis (local) | Number, instance | > 0 lx; e.g. 500 | Areas, Rooms, Spaces | Lighting / daylighting analysis | 300 |
| `CA_RetroreflectiveSheeting` | Retroreflective sheeting / Pellicule rétroréfléchissante | Text, type | Pattern; e.g. `ASTM D4956 Type XI`, `ASTM D4956 Type III` | Generic Models, Signage, Specialty Equipment | Code and accessibility compliance checking, Cost estimating | 300 |
| `CA_RoofingMembraneSystem` | Roofing membrane system / Système de membrane de toiture | Text, type | Free text; e.g. 2-ply SBS modified bitumen | Roofs | Cost estimating, Facility management / maintenance | 300 |
| `CA_SecurityRating` | Security rating / Cote de sécurité | Text, type | Free text; e.g. Owner level 2 | Doors | Design review / authoring, Facility management / maintenance | 350 |
| `CA_SeismicRestraint` | Seismic restraint / Ancrage parasismique | Yes/No, type | `Yes`, `No` | Casework, Furniture, Furniture Systems | 3D coordination / clash avoidance, Code and accessibility compliance checking | 350 |
| `CA_SelfClosing` | Self-closing / Fermeture automatique | Yes/No, type | `Yes`, `No` | Doors | Code and accessibility compliance checking | 350 |
| `CA_SignCategory` | Sign category / Catégorie de panneau | Code list (text), type | `Regulatory`, `Warning`, `Temporary conditions`, `Guide`, `Information`, `Wayfinding`, `Room identification`, `Life safety` | Generic Models, Signage, Specialty Equipment | Design review / authoring | 200 |
| `CA_SignSize` | Sign size / Dimensions du panneau | Text, type | Pattern; e.g. `750 x 750`, `300 x 150` | Generic Models, Signage, Specialty Equipment | Code and accessibility compliance checking, Cost estimating | 300 |
| `CA_SmokeStop` | Smoke stop / Coupe-fumée | Yes/No, type | `Yes`, `No` | Doors | Code and accessibility compliance checking | 350 |
| `CA_SolarReflectanceIndex` | Solar reflectance index (SRI) / Indice de réflectance solaire (IRS) | Number, type | ≤ 130; e.g. 82 | Roofs | Design review / authoring, Energy and load analysis | 300 |
| `CA_SoundAbsorption` | Sound absorption (NRC) / Coefficient de réduction du bruit (CRB) | Text, type | Pattern; e.g. `NRC 0.70`, `NRC 0.55` | Ceilings | Design review / authoring | 300 |
| `CA_SpaceClassification` | Space classification / Classification du local | Identifier (text), instance | Pattern; e.g. `13-25 11 00`, `13-31 11 11` | Areas, Rooms, Spaces | Asset register, Space management | 300 |
| `CA_Sprinklered` | Sprinklered / Protégé par gicleurs | Yes/No, instance | `Yes`, `No` | Areas, Rooms, Spaces | Code and accessibility compliance checking | 300 |
| `CA_StandardSignCode` | Standard sign code / Code normalisé du panneau | Identifier (text), type | Pattern; e.g. `Ra-1`, `P-90-P` | Generic Models, Signage, Specialty Equipment | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_StopsOpenings` | Stops / openings / Arrêts / ouvertures | Text, instance | Free text; e.g. 6 stops, front openings | Specialty Equipment, Vertical Circulation | Design review / authoring | 300 |
| `CA_SupplyInstallResponsibility` | Supply / install responsibility / Responsabilité (fourni par / installé par) | Code list (text), type | `OFOI`, `OFCI`, `CFCI`, `CFOI` | Casework, Food Service Equipment, Furniture, Furniture Systems, Medical Equipment, Specialty Equipment | Cost estimating, Design review / authoring | 200 |
| `CA_SurfaceFlameSpreadRating` | Surface flame-spread rating / Indice de propagation de la flamme | Text, type | Pattern; e.g. `FSR 25`, `FSR 150` | Ceilings, Walls | Code and accessibility compliance checking | 300 |
| `CA_TactileBraille` | Tactile / braille / Tactile / braille | Yes/No, type | `Yes`, `No` | Generic Models, Signage, Specialty Equipment | Code and accessibility compliance checking | 300 |
| `CA_UtilityConnections` | Utility connections / Raccordements requis | Multiline text, type | Free text; e.g. 120 V 20 A, CW, drain, data | Food Service Equipment, Medical Equipment, Specialty Equipment | 3D coordination / clash avoidance, Design review / authoring | 300 |
| `CA_ZoneArea` | Zone area / Superficie de la zone | Area, instance | > 0 m²; e.g. 840 | Areas, HVAC Zones, Rooms | Code and accessibility compliance checking, Space management | 300 |
| `CA_ZoneType` | Zone type / Type de zone | Code list (text), instance | `CONSTRUCTION`, `FIRESAFETY`, `LIGHTING`, `OCCUPANCY`, `SECURITY`, `THERMAL`, `TRANSPORT`, `VENTILATION`, `RESERVATION`, `INTERFERENCE`, `USERDEFINED`, `NOTDEFINED` | Areas, HVAC Zones, Rooms | Code and accessibility compliance checking, Design review / authoring | 200 |

### 03 Structure (31)

| Parameter | Label EN / FR | Type | Accepted values | Revit categories | BIM uses | From LOD |
| --- | --- | --- | --- | --- | --- | --- |
| `Bar Diameter` (built-in) | Bar size / Diamètre de barre | Code list (text), type | `10M`, `15M`, `20M`, `25M`, `30M`, `35M`, `45M`, `55M` | Structural Area Reinforcement, Structural Fabric Reinforcement, Structural Rebar | 3D coordination / clash avoidance, Prefabrication / digital fabrication | 350 |
| `Camber Size` (built-in) | Camber / Contre-flèche | Length, instance | > 0 mm; e.g. 20 | Floors, Structural Framing | Prefabrication / digital fabrication | 350 |
| `Cut Length / Length` (built-in) | Length / Longueur | Length, instance | > 0 mm; e.g. 4200 | Floors, Structural Foundations, Structural Framing, Structural Trusses | Cost estimating, Handover / record model, Quantity take-off | 200 |
| `Elevation at Bottom` (built-in) | Bottom elevation / Élévation du dessous | Length, instance | any m; e.g. 98.450 | Structural Columns, Structural Foundations, Structural Framing, Walls | 3D coordination / clash avoidance, Cost estimating, Design review / authoring, Existing conditions modelling / renovation +1 | 300 |
| `Elevation at Top` (built-in) | Cut-off elevation / Élévation de recépage | Length, instance | any m; e.g. 97.900 | Structural Foundations | 3D coordination / clash avoidance | 300 |
| `Foundation Thickness` (built-in) | Thickness / Épaisseur | Length, type | > 0 mm; e.g. 600 | Structural Foundations | Cost estimating, Quantity take-off | 200 |
| `Moment Connection Start / End` (built-in) | End connection type / Type d'assemblage d'extrémité | Text, instance | Free text; e.g. Shear tab / moment | Structural Framing, Structural Trusses | 3D coordination / clash avoidance, Prefabrication / digital fabrication | 350 |
| `Quantity` (built-in) | Number of bars / Nombre de barres | Integer, instance | ≥ 1; e.g. 48 | Structural Area Reinforcement, Structural Fabric Reinforcement, Structural Rebar | Prefabrication / digital fabrication, Quantity take-off | 400 |
| `Spacing (rebar set layout)` (built-in) | Spacing / Espacement | Length, instance | > 0 mm; e.g. 300 | Structural Area Reinforcement, Structural Fabric Reinforcement, Structural Rebar | 3D coordination / clash avoidance, Prefabrication / digital fabrication | 350 |
| `Structural Material (material properties)` (built-in) | Material grade / strength / Nuance / résistance | Text, type | Free text; e.g. CSA G40.21 350W / 35 MPa | Floors, Structural Columns, Structural Foundations, Structural Framing, Structural Trusses, Walls | Code and accessibility compliance checking, Cost estimating, Design review / authoring | 300 |
| `Total Bar Length` (built-in) | Total bar length / Longueur totale des barres | Length, instance | > 0 m; e.g. 612 | Structural Area Reinforcement, Structural Fabric Reinforcement, Structural Rebar | Prefabrication / digital fabrication, Quantity take-off | 400 |
| `Type Name / Section Name Key (structural families)` (built-in) | Section / profile / Profil de la section | Text, type | Free text; e.g. W310x97 | Floors, Structural Columns, Structural Framing, Structural Trusses | 3D coordination / clash avoidance, Cost estimating, Design review / authoring, Quantity take-off | 200 |
| `Unconnected Height` (built-in) | Retained height / Hauteur retenue | Length, instance | > 0 mm; e.g. 3500 | Walls | Design review / authoring, Quantity take-off | 200 |
| `CA_BarCoating` | Bar coating / Revêtement des barres | Code list (text), type | `Black`, `Epoxy coated`, `Galvanized`, `Stainless steel` | Structural Area Reinforcement, Structural Fabric Reinforcement, Structural Rebar | Cost estimating, Design review / authoring | 300 |
| `CA_CoatingCorrosionProtection` | Coating / corrosion protection / Revêtement / protection anticorrosion | Text, type | Free text; e.g. Hot-dip galvanized | Structural Framing, Structural Trusses | Cost estimating, Facility management / maintenance | 300 |
| `CA_ConcreteCover` | Concrete cover / Enrobage | Length, type | > 0 mm; e.g. 40 | Floors, Structural Area Reinforcement, Structural Columns, Structural Fabric Reinforcement, Structural Foundations, Structural Framing +2 | 3D coordination / clash avoidance, Code and accessibility compliance checking, Design review / authoring, Prefabrication / digital fabrication | 300 |
| `CA_ConcreteExposureClass` | Concrete exposure class / Classe d'exposition | Code list (text), type | `C-XL`, `C-1`, `C-2`, `C-3`, `C-4`, `F-1`, `F-2`, `N`, `A-XL`, `A-1`, `A-2`, `A-3` (+4 more) | Floors, Structural Columns, Structural Foundations, Structural Framing, Walls | Code and accessibility compliance checking, Cost estimating, Design review / authoring | 300 |
| `CA_DelegatedDesign` | Delegated (supplier) design / Conception déléguée | Yes/No, type | `Yes`, `No` | Structural Framing, Structural Trusses | Cost estimating, Design review / authoring | 300 |
| `CA_DesignAxialResistance_kN` | Design axial resistance / Résistance axiale pondérée | Number, type | > 0 kN; e.g. 2400 | Structural Foundations | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_DesignBearingResistance_kPa` | Design bearing resistance / Résistance pondérée du sol | Number, instance | > 0 kPa; e.g. 225 | Structural Foundations | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_DesignLiveLoad_kPa` | Design live load / Surcharge d'utilisation | Number, instance | > 0 kPa; e.g. 4.8 | Floors, Structural Foundations | Code and accessibility compliance checking, Design review / authoring, Facility management / maintenance | 300 |
| `CA_DiameterSection` | Diameter / section / Diamètre ou section | Text, type | Pattern; e.g. `610 dia.`, `HP310x110` | Structural Foundations | Cost estimating, Quantity take-off | 200 |
| `CA_DrainageProvisions` | Drainage provisions / Dispositif de drainage | Multiline text, type | Free text; e.g. Perforated drain and free-draining backfill | Walls | Design review / authoring, Facility management / maintenance | 300 |
| `CA_FireProtection` | Fire protection / Protection incendie | Text, type | Free text; e.g. Intumescent, 1 h | Structural Framing | Code and accessibility compliance checking, Cost estimating | 300 |
| `CA_FrostProtectionDepth` | Frost protection depth / Profondeur de protection contre le gel | Length, instance | > 0 mm; e.g. 1500 | Structural Foundations | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_InstallationRecord` | Installation record / Relevé de mise en place | Multiline text, instance | Free text; e.g. Driven 22.4 m, set 3 mm/blow | Structural Foundations | Existing conditions modelling / renovation, Handover / record model | 500 |
| `CA_ReinforcementGrade` | Reinforcement grade / Nuance de l'armature | Text, type | Pattern; e.g. `CSA G30.18 400W`, `CSA G30.18 500W` | Structural Area Reinforcement, Structural Fabric Reinforcement, Structural Rebar | Cost estimating, Design review / authoring | 300 |
| `CA_ReinforcementRatio_kgm3` | Reinforcement ratio / Taux d'armature | Number, type | > 0 kg/m³; e.g. 180 | Floors, Structural Area Reinforcement, Structural Columns, Structural Fabric Reinforcement, Structural Foundations, Structural Framing +2 | Cost estimating, Quantity take-off | 200 |
| `CA_Span` | Span / Portée | Length, instance | > 0 mm; e.g. 9000 | Floors, Structural Framing | Design review / authoring | 200 |
| `CA_TotalMass_kg` | Total mass / Masse totale | Number, instance | > 0 kg; e.g. 1840 | Structural Area Reinforcement, Structural Fabric Reinforcement, Structural Rebar | Cost estimating, Quantity take-off | 350 |
| `CA_WallSystem` | Wall system / Type de mur de soutènement | Text, type | Free text; e.g. Cast-in-place cantilever | Walls | Cost estimating, Design review / authoring | 200 |

### 04 Mechanical (60)

| Parameter | Label EN / FR | Type | Accepted values | Revit categories | BIM uses | From LOD |
| --- | --- | --- | --- | --- | --- | --- |
| `Connector diameter / width x height` (built-in) | Neck / connection size / Dimension du col | Length, type | > 0 mm; e.g. 200 | Air Terminals | 3D coordination / clash avoidance, Cost estimating | 300 |
| `Flow` (built-in) | Design airflow / Débit d'air de conception | Number, instance | > 0 L/s; e.g. 120 | Air Terminals, Duct Accessories, Duct Fittings, Ducts, Flex Ducts, Flex Pipes +4 | Commissioning, Design review / authoring, Energy and load analysis, Facility management / maintenance | 200 |
| `Insulation Type / Insulation Thickness` (built-in) | Insulation type and thickness / Isolation (type et épaisseur) | Text, instance | Free text; e.g. Glass fibre, 50 mm, FSK jacket | Duct Fittings, Ducts, Flex Ducts, Flex Pipes, Pipe Fittings, Pipes | 3D coordination / clash avoidance, Cost estimating, Energy and load analysis | 300 |
| `Lining Type / Lining Thickness` (built-in) | Internal liner / Revêtement intérieur | Yes/No, instance | `Yes`, `No` | Duct Fittings, Ducts, Flex Ducts | 3D coordination / clash avoidance, Design review / authoring | 300 |
| `Size` (built-in) | Size / Dimensions | Text, instance | Pattern; e.g. `300 x 100`, `250ø` | Duct Accessories, Duct Fittings, Ducts, Flex Ducts, Pipe Accessories | 3D coordination / clash avoidance, Cost estimating, Energy and load analysis | 200 |
| `System Classification` (built-in) | Air flow type / Type de débit d'air | Code list (text), instance | `Supply Air`, `Return Air`, `Exhaust Air`, `Other Air` | Air Terminals | 3D coordination / clash avoidance, Energy and load analysis | 200 |
| `Voltage / Number of Poles (connector)` (built-in) | Electrical supply (V / phase) / Alimentation (V / phase) | Number, type | > 0 V; e.g. 600 | Mechanical Equipment, Plumbing Equipment | 3D coordination / clash avoidance, Commissioning, Cost estimating | 300 |
| `Width / Height / Diameter(Trade Size)` (built-in) | Size / Dimensions | Text, instance | Pattern; e.g. `300 x 100`, `27` | Cable Tray Fittings, Cable Trays, Conduit Fittings, Conduits | 3D coordination / clash avoidance, Cost estimating | 200 |
| `CA_AccessDoorProvided` | Access door provided / Porte d'accès prévue | Yes/No, instance | `Yes`, `No` | Duct Accessories | 3D coordination / clash avoidance, Code and accessibility compliance checking, Facility management / maintenance | 350 |
| `CA_ActuationOperation` | Actuation / operation / Mode de manœuvre | Code list (text), type | `GRAVITY`, `SPRING`, `AUTOMATIC`, `MANUAL`, `OTHER`, `NOTKNOWN`, `UNSET` | Duct Accessories | Commissioning, Facility management / maintenance | 300 |
| `CA_CapacityControl` | Capacity control / Régulation de débit | Code list (text), type | `BLADEPITCHANGLE`, `DISCHARGEDAMPER`, `INLETVANE`, `TWOSPEED`, `VARIABLESPEEDDRIVE`, `OTHER`, `NOTKNOWN`, `UNSET` | Mechanical Equipment | Energy and load analysis | 300 |
| `CA_CircuitType` | Circuit type / Type de circuit | Code list (text), type | `CLOSEDCIRCUITDRY`, `CLOSEDCIRCUITDRYWET`, `CLOSEDCIRCUITWET`, `OPENCIRCUIT`, `OTHER`, `NOTKNOWN`, `UNSET` | Mechanical Equipment | Design review / authoring | 300 |
| `CA_Construction` | Construction (factory / site-built) / Construction | Code list (text), type | `CONSTRUCTEDONSITE`, `MANUFACTUREDITEM`, `OTHER`, `NOTKNOWN`, `UNSET` | Mechanical Equipment | Cost estimating, Prefabrication / digital fabrication | 300 |
| `CA_CoolingCapacity_kW` | Cooling capacity / Puissance frigorifique | Number, type | > 0 kW; e.g. 1050 | Mechanical Equipment | Design review / authoring, Energy and load analysis | 200 |
| `CA_CrossSectionShape` | Cross-section shape / Forme de la section | Code list (text), type | `FLATOVAL`, `RECTANGULAR`, `ROUND`, `OTHER`, `NOTKNOWN`, `UNSET` | Duct Fittings, Ducts, Flex Ducts | 3D coordination / clash avoidance, Cost estimating | 200 |
| `CA_DesignFlows` | Design flows (primary / secondary) / Débits de conception (primaire / secondaire) | Text, type | Pattern; e.g. `12.0 / 11.5`, `8 / 8` | Mechanical Equipment | Commissioning, Energy and load analysis | 300 |
| `CA_DesignSupplyAirflow_Ls` | Design supply airflow / Débit d'air soufflé | Number, type | > 0 L/s; e.g. 9500 | Mechanical Equipment | Design review / authoring, Energy and load analysis | 200 |
| `CA_DesignSupplyTemperature_C` | Design supply temperature / Température d'alimentation de conception | Number, type | > 0 and ≤ 200 °C; e.g. 60 | Mechanical Equipment | Commissioning, Energy and load analysis | 300 |
| `CA_DesignWetBulbTemperature_C` | Design wet-bulb temperature / Température humide de calcul | Number, type | ≥ -10 and ≤ 40 °C; e.g. 23 | Mechanical Equipment | Energy and load analysis | 300 |
| `CA_DriveType` | Drive type / Type d'entraînement | Code list (text), type | `BELTDRIVE`, `COUPLING`, `DIRECTDRIVE`, `TRACTION_GEARED`, `TRACTION_GEARLESS`, `TRACTION_MRL`, `HYDRAULIC`, `HYDRAULIC_ROPED`, `OTHER`, `NOTKNOWN`, `UNSET` | Mechanical Equipment, Specialty Equipment, Vertical Circulation | Cost estimating, Energy and load analysis, Facility management / maintenance | 300 |
| `CA_DynamicStaticRating` | Dynamic / static rating / Homologation dynamique / statique | Code list (text), type | `DYNAMIC`, `STATIC`, `OTHER`, `NOTKNOWN`, `UNSET` | Duct Accessories | Code and accessibility compliance checking | 300 |
| `CA_ExternalStaticPressure_Pa` | External static pressure / Pression statique externe | Number, type | > 0 Pa; e.g. 500 | Mechanical Equipment | Commissioning, Energy and load analysis | 300 |
| `CA_FaceType` | Face type / Type de face | Code list (text), type | `DOUBLEDEFLECTION`, `EGGCRATE`, `FOURWAYPATTERN`, `LOUVERED`, `PERFORATED`, `SIGHTPROOF`, `SINGLEDEFLECTION`, `OTHER`, `NOTKNOWN`, `UNSET` | Air Terminals | Design review / authoring, Visualisation and photometric rendering | 300 |
| `CA_FanArrangement` | Fan arrangement / Disposition du ventilateur | Code list (text), type | `BLOWTHROUGH`, `DRAWTHROUGH`, `OTHER`, `NOTKNOWN`, `UNSET` | Mechanical Equipment | Design review / authoring | 300 |
| `CA_FanMotorPower_kW` | Fan motor power / Puissance moteur ventilateur | Number, type | > 0 kW; e.g. 15 | Mechanical Equipment | Cost estimating, Energy and load analysis | 300 |
| `CA_FanPowered` | Fan-powered / Avec ventilateur | Yes/No, type | `Yes`, `No` | Duct Accessories, Mechanical Equipment | Energy and load analysis | 300 |
| `CA_FilterRating` | Filter rating (MERV) / Cote de filtre (MERV) | Text, type | Pattern; e.g. `MERV 13`, `ISO ePM1 50%` | Mechanical Equipment | Design review / authoring, Facility management / maintenance | 300 |
| `CA_FiringOperatingMode` | Firing / operating mode / Mode de fonctionnement | Code list (text), type | `FIXED`, `MODULATING`, `TWOSTEP`, `OTHER`, `NOTKNOWN`, `UNSET` | Mechanical Equipment | Commissioning, Energy and load analysis | 300 |
| `CA_FlowArrangement` | Flow arrangement / Disposition des écoulements | Code list (text), type | `COUNTERFLOW`, `CROSSFLOW`, `MULTIPASS`, `PARALLELFLOW`, `OTHER`, `NOTKNOWN`, `UNSET` | Mechanical Equipment | Design review / authoring | 300 |
| `CA_FullLoadEfficiency` | Full-load efficiency (COP) / Efficacité à pleine charge (COP) | Number, type | > 0 and ≤ 15; e.g. 6.1 | Mechanical Equipment | Energy and load analysis | 300 |
| `CA_HasIntegralDamperControl` | Has integral damper / control / Registre intégré | Yes/No, type | `Yes`, `No` | Air Terminals | 3D coordination / clash avoidance, Commissioning | 350 |
| `CA_HeatRecoveryEffectiveness_pct` | Heat-recovery effectiveness / Efficacité de récupération | Number, type | > 0 and ≤ 100 %; e.g. 70 | Mechanical Equipment | Energy and load analysis | 300 |
| `CA_HeatRejectionCapacity_kW` | Heat rejection capacity / Puissance de refroidissement | Number, type | > 0 kW; e.g. 1250 | Mechanical Equipment | Design review / authoring, Energy and load analysis | 200 |
| `CA_HeatRejectionRate_kW` | Heat rejection rate / Taux de rejet thermique | Number, type | > 0 kW; e.g. 1230 | Mechanical Equipment | Energy and load analysis | 300 |
| `CA_HeatTransferCapacity_kW` | Heat transfer capacity / Puissance d'échange | Number, type | > 0 kW; e.g. 800 | Mechanical Equipment | Design review / authoring, Energy and load analysis | 200 |
| `CA_HeatingCapacity_kW` | Heating capacity / Puissance calorifique | Number, type | > 0 kW; e.g. 220 | Mechanical Equipment | Energy and load analysis | 200 |
| `CA_HeatingOutput_kW` | Heating output / Puissance utile | Number, type | > 0 kW; e.g. 610 | Mechanical Equipment | Design review / authoring, Energy and load analysis | 200 |
| `CA_InletSize` | Inlet size / Diamètre d'entrée | Length, type | > 0 mm; e.g. 200 | Duct Accessories, Mechanical Equipment | 3D coordination / clash avoidance | 300 |
| `CA_InputPower_kW` | Input power / Puissance absorbée | Number, type | > 0 kW; e.g. 172 | Mechanical Equipment | Design review / authoring, Energy and load analysis | 300 |
| `CA_InputRating_kW` | Input rating / Puissance absorbée | Number, type | > 0 kW; e.g. 650 | Mechanical Equipment, Plumbing Equipment | Code and accessibility compliance checking, Energy and load analysis | 300 |
| `CA_LeakageWhenClosed_Ls` | Leakage when closed / Fuite en position fermée | Number, type | > 0 L/s; e.g. 12 | Duct Accessories | Code and accessibility compliance checking, Commissioning | 300 |
| `CA_MakeUpWaterRequirement_Ls` | Make-up water requirement / Besoin en eau d'appoint | Number, type | > 0 L/s; e.g. 1.8 | Mechanical Equipment | Design review / authoring, Energy and load analysis | 300 |
| `CA_MinimumMaximumAirflow_Ls` | Minimum / maximum airflow / Débit d'air minimal / maximal | Number, instance | > 0 L/s; e.g. 420 | Duct Accessories, Mechanical Equipment | Commissioning, Energy and load analysis, Handover / record model | 300 |
| `CA_MinimumOutdoorAirflow_Ls` | Minimum outdoor airflow / Débit d'air neuf minimal | Number, type | > 0 L/s; e.g. 2400 | Mechanical Equipment | Code and accessibility compliance checking, Commissioning, Energy and load analysis | 300 |
| `CA_NoiseCriterionAtDesignFlow` | Noise criterion (NC) at design flow / Critère de bruit (NC) | Text, type | Pattern; e.g. `NC 30`, `NC 25` | Air Terminals | Commissioning, Design review / authoring | 300 |
| `CA_NominalAirflow_Ls` | Nominal airflow / Débit d'air nominal | Number, type | > 0 L/s; e.g. 1200 | Duct Accessories | Commissioning | 300 |
| `CA_NumberOfCells` | Number of cells / Nombre de cellules | Integer, type | ≥ 1; e.g. 2 | Mechanical Equipment | Design review / authoring | 300 |
| `CA_NumberOfPlates` | Number of plates / Nombre de plaques | Integer, type | ≥ 1; e.g. 61 | Mechanical Equipment | Facility management / maintenance | 400 |
| `CA_PartLoadEfficiency` | Part-load efficiency (IPLV) / Efficacité à charge partielle (IPLV) | Number, type | > 0 and ≤ 20; e.g. 9.2 | Mechanical Equipment | Energy and load analysis | 300 |
| `CA_PressureClass_Pa` | Pressure class / Classe de pression | Number, type | > 0 Pa; e.g. 500 | Duct Fittings, Ducts, Flex Ducts | Commissioning, Cost estimating | 300 |
| `CA_PressureRating_kPa` | Pressure rating / Pression nominale | Number, type | > 0 kPa; e.g. 1100 | Mechanical Equipment | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_Refrigerant` | Refrigerant / Frigorigène | Text, type | Pattern; e.g. `R-513A`, `R-1234ze(E)` | Mechanical Equipment | Code and accessibility compliance checking, Design review / authoring, Facility management / maintenance | 300 |
| `CA_ReheatType` | Reheat type / Type de réchauffage | Code list (text), type | `ELECTRICALREHEAT`, `GASREHEAT`, `NONE`, `STEAMCOILREHEAT`, `WATERCOILREHEAT`, `OTHER`, `NOTKNOWN`, `UNSET` | Duct Accessories, Mechanical Equipment | Energy and load analysis | 300 |
| `CA_ReleaseTemperature_C` | Release temperature / Température de déclenchement | Number, type | ≥ 50 and ≤ 300 °C; e.g. 74 | Duct Accessories | Code and accessibility compliance checking, Facility management / maintenance | 300 |
| `CA_SealLeakageClass` | Seal / leakage class / Classe d'étanchéité | Code list (text), type | `Seal class A`, `Seal class B`, `Seal class C` | Duct Fittings, Ducts, Flex Ducts | Commissioning, Energy and load analysis | 300 |
| `CA_Shape` | Shape / Forme | Code list (text), type | `RECTANGULAR`, `ROUND`, `SLOT`, `SQUARE`, `OTHER`, `NOTKNOWN`, `UNSET` | Air Terminals | Design review / authoring, Visualisation and photometric rendering | 300 |
| `CA_StaticPressure_Pa` | Static pressure / Pression statique | Number, type | > 0 Pa; e.g. 375 | Mechanical Equipment | Commissioning, Energy and load analysis | 300 |
| `CA_ThermalEfficiency_pct` | Thermal efficiency / Rendement thermique | Number, type | > 0 and ≤ 100 %; e.g. 95 | Mechanical Equipment, Plumbing Equipment | Energy and load analysis | 300 |
| `CA_ThrowLength` | Throw length / Portée | Length, type | > 0 m; e.g. 3.0 | Air Terminals | Design review / authoring, Energy and load analysis | 300 |
| `CA_TotalCoolingCapacity_kW` | Total cooling capacity / Puissance frigorifique | Number, type | > 0 kW; e.g. 180 | Mechanical Equipment | Energy and load analysis | 200 |

### 05 Plumbing and fire protection (31)

| Parameter | Label EN / FR | Type | Accepted values | Revit categories | BIM uses | From LOD |
| --- | --- | --- | --- | --- | --- | --- |
| `Connector diameter` (built-in) | Drain size / Diamètre du drain | Length, type | > 0 mm; e.g. 32 | Plumbing Fixtures | 3D coordination / clash avoidance | 350 |
| `Elevation from Level / Offset` (built-in) | Mounting / rim height / Hauteur d'installation | Length, instance | > 0 mm; e.g. 430 | Plumbing Fixtures | Code and accessibility compliance checking | 300 |
| `Fixture Units (plumbing connector / family)` (built-in) | Fixture units (DFU / WSFU) / Facteurs d'évacuation / d'alimentation | Number, type | > 0; e.g. 4 | Plumbing Fixtures | Code and accessibility compliance checking, Design review / authoring | 300 |
| `Fluid Type` (built-in) | Fluid / service / Fluide transporté | Text, instance | Free text; e.g. Chilled water, 30 % propylene glycol | Flex Pipes, Pipe Fittings, Pipes | Design review / authoring, Facility management / maintenance | 300 |
| `Size / Diameter` (built-in) | Nominal size / Diamètre nominal | Length, instance | > 0 mm; e.g. 50 | Flex Pipes, Pipe Fittings, Pipes | 3D coordination / clash avoidance, Cost estimating, Design review / authoring | 200 |
| `CA_BarrierFreeFixture` | Barrier-free fixture / Appareil sans obstacles | Yes/No, type | `Yes`, `No` | Plumbing Fixtures | Code and accessibility compliance checking | 300 |
| `CA_BaseDriveArrangement` | Base / drive arrangement / Socle / entraînement | Code list (text), instance | Pattern; e.g. `FRAME / DIRECTDRIVE`, `BASE / COUPLING` | Mechanical Equipment, Plumbing Equipment | 3D coordination / clash avoidance, Cost estimating | 300 |
| `CA_ConnectionSize` | Connection size / Diamètre de raccordement | Length, type | > 0 mm; e.g. 80 | Mechanical Equipment, Plumbing Equipment | 3D coordination / clash avoidance | 300 |
| `CA_CoverageArea` | Coverage area / Surface de couverture | Area, type | > 0 m²; e.g. 81 | Fire Alarm Devices, Sprinklers | Code and accessibility compliance checking | 300 |
| `CA_DesignHead_kPa` | Design head / Hauteur manométrique | Number, type | > 0 kPa; e.g. 210 | Mechanical Equipment, Plumbing Equipment | Design review / authoring, Energy and load analysis | 200 |
| `CA_FlowCoefficient` | Flow coefficient (Kv / Cv) / Coefficient de débit (Kv / Cv) | Number, type | > 0; e.g. 25 | Pipe Accessories | Commissioning, Energy and load analysis | 300 |
| `CA_FlowRateFlushVolume` | Flow rate / flush volume / Débit / volume de chasse | Text, type | Pattern; e.g. `4.8 Lpf`, `1.9 L/min` | Plumbing Fixtures | Code and accessibility compliance checking, Energy and load analysis | 300 |
| `CA_HazardClassification` | Hazard classification / Classe de risque | Code list (text), instance | `Light hazard`, `Ordinary hazard group 1`, `Ordinary hazard group 2`, `Extra hazard group 1`, `Extra hazard group 2` | Sprinklers | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_JointType` | Joint type / Type de joint | Text, type | Free text; e.g. Grooved mechanical | Flex Pipes, Pipe Fittings, Pipes | Cost estimating, Prefabrication / digital fabrication | 300 |
| `CA_KFactor` | K-factor / Facteur K | Number, type | > 0; e.g. 80 | Sprinklers | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_MotorNameplatePower_kW` | Motor nameplate power / Puissance nominale du moteur | Number, type | > 0 kW; e.g. 7.5 | Mechanical Equipment, Plumbing Equipment | Cost estimating, Energy and load analysis | 300 |
| `CA_NominalLength` | Nominal length / Longueur nominale | Length, type | > 0 mm; e.g. 560 | Plumbing Fixtures | Code and accessibility compliance checking, Design review / authoring, Space management | 300 |
| `CA_NominalSpeed_rpm` | Nominal speed / Vitesse de rotation | Number, type | > 0 rpm; e.g. 1150 | Mechanical Equipment, Plumbing Equipment | Commissioning | 300 |
| `CA_NominalWidth` | Nominal width / Largeur nominale | Length, type | > 0 mm; e.g. 460 | Plumbing Fixtures | Code and accessibility compliance checking, Design review / authoring, Space management | 300 |
| `CA_NormallyOpen` | Normally open / Normalement ouvert | Yes/No, instance | `Yes`, `No` | Pipe Accessories | Commissioning, Facility management / maintenance | 300 |
| `CA_Operation` | Operation / Mode de manœuvre | Code list (text), type | `DROPWEIGHT`, `FLOAT`, `HYDRAULIC`, `LEVER`, `LOCKSHIELD`, `MOTORIZED`, `PNEUMATIC`, `SOLENOID`, `SPRING`, `THERMOSTATIC`, `WHEEL`, `OTHER` (+2 more) | Pipe Accessories | Commissioning, Facility management / maintenance | 300 |
| `CA_RecoveryRate_Lh` | Recovery rate / Taux de récupération | Number, type | > 0 L/h; e.g. 1200 | Mechanical Equipment, Plumbing Equipment | Design review / authoring | 300 |
| `CA_Response` | Response / Réponse | Code list (text), type | `QUICK`, `STANDARD`, `NOTKNOWN`, `UNSET` | Sprinklers | Code and accessibility compliance checking | 300 |
| `CA_SprinklerType` | Sprinkler type / Type de gicleur | Code list (text), type | `CEILING`, `CONCEALED`, `CUTOFF`, `PENDANT`, `RECESSEDPENDANT`, `SIDEWALL`, `UPRIGHT`, `OTHER`, `NOTKNOWN`, `UNSET` | Sprinklers | 3D coordination / clash avoidance, Design review / authoring | 200 |
| `CA_StorageCapacity_L` | Storage capacity / Volume de stockage | Number, type | > 0 L; e.g. 450 | Mechanical Equipment, Plumbing Equipment | Design review / authoring, Energy and load analysis | 200 |
| `CA_StorageSetPoint_C` | Storage set-point / Température de consigne du stockage | Number, instance | > 0 and ≤ 100 °C; e.g. 60 | Mechanical Equipment, Plumbing Equipment | Code and accessibility compliance checking, Facility management / maintenance | 300 |
| `CA_TemperatureRating_C` | Temperature rating / Température de déclenchement | Number, type | ≥ 50 and ≤ 350 °C; e.g. 68 | Sprinklers | Code and accessibility compliance checking | 300 |
| `CA_ValveMechanism` | Valve mechanism / Mécanisme du robinet | Code list (text), type | `BALL`, `BUTTERFLY`, `CONFIGUREDGATE`, `GLAND`, `GLOBE`, `LUBRICATEDPLUG`, `NEEDLE`, `PARALLELSLIDE`, `PLUG`, `WEDGEGATE`, `OTHER`, `NOTKNOWN` (+1 more) | Pipe Accessories | Cost estimating, Facility management / maintenance | 300 |
| `CA_VariableFrequencyDrive` | Variable-frequency drive / Variateur de fréquence | Yes/No, type | `Yes`, `No` | Mechanical Equipment, Plumbing Equipment | Commissioning, Energy and load analysis | 300 |
| `CA_WorkingPressureRating_kPa` | Working pressure rating / Pression de service | Number, type | > 0 kPa; e.g. 1035 | Flex Pipes, Pipe Fittings, Pipes | Commissioning, Design review / authoring | 300 |
| `CA_WorkingTestPressure_kPa` | Working / test pressure / Pression de service / d'essai | Number, type | > 0 kPa; e.g. 1380 | Pipe Accessories | Design review / authoring | 300 |

### 06 Electrical and communications (52)

| Parameter | Label EN / FR | Type | Accepted values | Revit categories | BIM uses | From LOD |
| --- | --- | --- | --- | --- | --- | --- |
| `Apparent Load / Wattage (electrical data - family dependent)` (built-in) | Total wattage / Puissance totale | Number, type | > 0 W; e.g. 32 | Lighting Fixtures | Energy and load analysis | 200 |
| `Elevation from Level` (built-in) | Mounting height / Hauteur de montage | Length, instance | > 0 mm; e.g. 1200 | Communication Devices, Data Devices, Electrical Fixtures, Fire Alarm Devices, Generic Models, Lighting Devices +5 | 3D coordination / clash avoidance, Code and accessibility compliance checking | 300 |
| `Enclosure` (built-in) | Enclosure rating / Indice de protection du boîtier | Text, type | Free text; e.g. Type 1 / IP20 | Electrical Equipment | Code and accessibility compliance checking, Design review / authoring | 300 |
| `Initial Color` (built-in) | Colour temperature (CCT) / Température de couleur | Number, type | ≥ 1000 and ≤ 10000 K; e.g. 4000 | Lighting Fixtures | Design review / authoring, Lighting / daylighting analysis, Visualisation and photometric rendering | 300 |
| `Initial Intensity (luminous flux option)` (built-in) | Luminous flux (lumen output) / Flux lumineux | Number, type | > 0 lm; e.g. 4000 | Lighting Fixtures | Facility management / maintenance, Handover / record model, Lighting / daylighting analysis, Visualisation and photometric rendering | 200 |
| `Light Loss Factor` (built-in) | Maintenance (light loss) factor / Facteur de maintenance | Number, type | > 0 and ≤ 1; e.g. 0.8 | Lighting Fixtures | Lighting / daylighting analysis | 300 |
| `MCB Rating` (built-in) | Main device / Dispositif principal | Text, type | Free text; e.g. Main breaker 225 A | Electrical Equipment | Cost estimating, Design review / authoring | 300 |
| `Mains` (built-in) | Rated current (mains / bus) / Courant assigné (barres) | Number, type | > 0 A; e.g. 400 | Electrical Equipment, Electrical Fixtures, Lighting Devices | Cost estimating, Design review / authoring | 200 |
| `Max #1 Pole Breakers` (built-in) | Number of circuits / spaces / Nombre de circuits / espaces | Integer, type | ≥ 1; e.g. 42 | Electrical Equipment | Cost estimating, Design review / authoring | 300 |
| `Number of Poles` (built-in) | Phases / number of poles / Phases / nombre de pôles | Integer, type | ≥ 1 and ≤ 3; e.g. 3 | Electrical Equipment | Design review / authoring | 300 |
| `Photometric Web File (Revit does not keep a link to the file)` (built-in) | Photometric file (IES / LDT) / Fichier photométrique (IES) | Text, type | Pattern; e.g. `TRX24-40K.ies`, `LF-A1_4000lm.IES` | Lighting Fixtures | Handover / record model, Lighting / daylighting analysis, Visualisation and photometric rendering | 300 |
| `Service Type` (built-in) | Service type / Type de service | Code list (text), instance | `Power - normal`, `Power - emergency`, `Data`, `Fire alarm`, `Security`, `Audio-visual`, `Controls` | Cable Tray Fittings, Cable Trays, Conduit Fittings, Conduits | 3D coordination / clash avoidance, Facility management / maintenance | 300 |
| `Short Circuit Rating` (built-in) | Short-circuit rating / Pouvoir de coupure / tenue aux courts-circuits | Number, type | > 0 kA; e.g. 42 | Electrical Equipment | Code and accessibility compliance checking, Design review / authoring | 300 |
| `Voltage (connector)` (built-in) | Rated voltage / Tension assignée | Number, type | > 0 V; e.g. 600 | Lighting Fixtures | Commissioning, Cost estimating, Energy and load analysis | 300 |
| `Voltage / Distribution System` (built-in) | Rated voltage / Tension assignée | Number, type | > 0 V; e.g. 600 | Electrical Equipment, Electrical Fixtures, Food Service Equipment, Lighting Devices, Medical Equipment, Specialty Equipment | 3D coordination / clash avoidance, Design review / authoring | 200 |
| `CA_CSAConfiguration` | CSA configuration / Configuration CSA | Text, type | Pattern; e.g. `5-20R`, `L5-30R` | Electrical Fixtures, Lighting Devices | Design review / authoring | 300 |
| `CA_CatalogueArticleNumber` | Catalogue / article number / Numéro d'article | Identifier (text), type | Pattern; e.g. `TRX24-4000LM-40K-UNV`, `7731-120` | Lighting Fixtures | Facility management / maintenance, Handover / record model, Prefabrication / digital fabrication | 400 |
| `CA_ColourRenderingIndex` | Colour rendering index / Indice de rendu des couleurs | Integer, type | ≥ 0 and ≤ 100; e.g. 90 | Lighting Fixtures | Design review / authoring, Lighting / daylighting analysis | 300 |
| `CA_DetectionSetPoint` | Detection set point / Seuil de détection | Text, type | Free text; e.g. 57 °C fixed / rate of rise | Fire Alarm Devices | Code and accessibility compliance checking, Commissioning | 300 |
| `CA_DeviceAddress` | Device address / Adresse de l'appareil | Identifier (text), instance | Pattern; e.g. `L2-D045`, `L1-M012` | Fire Alarm Devices | Commissioning, Facility management / maintenance, Handover / record model | 400 |
| `CA_Efficiency_pct` | Efficiency / Rendement | Number, type | > 0 and ≤ 100 %; e.g. 98.6 | Electrical Equipment | Energy and load analysis | 300 |
| `CA_EmergencySecurityLightingType` | Emergency / security lighting type / Éclairage de sécurité | Code list (text), type | `BLUEILLUMINATION`, `EMERGENCYEXITLIGHT`, `SAFETYLIGHT`, `WARNINGLIGHT`, `OTHER`, `NOTKNOWN`, `UNSET` | Lighting Fixtures | Code and accessibility compliance checking | 300 |
| `CA_FillRatio_pct` | Fill ratio / Taux de remplissage | Number, instance | > 0 and ≤ 100 %; e.g. 40 | Cable Tray Fittings, Cable Trays, Conduit Fittings, Conduits | Design review / authoring | 350 |
| `CA_FuelAutonomy_h` | Fuel autonomy / Autonomie en carburant | Number, type | > 0 h; e.g. 2 | Electrical Equipment | Code and accessibility compliance checking | 300 |
| `CA_FuelTankCapacity_L` | Fuel tank capacity / Capacité du réservoir | Number, type | > 0 L; e.g. 1800 | Electrical Equipment | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_GeneratorEfficiency_pct` | Generator efficiency / Rendement de l'alternateur | Number, type | > 0 and ≤ 100 %; e.g. 94 | Electrical Equipment | Energy and load analysis | 300 |
| `CA_GroundFaultProtected` | Ground-fault protected (GFCI) / Protégé par DDFT | Yes/No, type | `Yes`, `No` | Electrical Fixtures, Lighting Devices | Code and accessibility compliance checking | 300 |
| `CA_HasCover` | Has cover / Avec couvercle | Yes/No, type | `Yes`, `No` | Cable Tray Fittings, Cable Trays, Conduit Fittings, Conduits | Cost estimating | 300 |
| `CA_HeatGainToSpace_W` | Heat gain to space / Gain thermique au local | Number, type | > 0 W; e.g. 28 | Electrical Equipment, Food Service Equipment, Lighting Fixtures, Medical Equipment, Specialty Equipment | Energy and load analysis | 300 |
| `CA_IPRating` | IP rating / Indice IP | Text, type | Pattern; e.g. `IP65`, `IP20` | Lighting Fixtures | Code and accessibility compliance checking | 300 |
| `CA_Impedance` | Impedance / Impédance | Text, type | Pattern; e.g. `5.75 %`, `4.5 %` | Electrical Equipment | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_LampLightSourceType` | Lamp / light source type / Type de lampe | Code list (text), type | `COMPACTFLUORESCENT`, `FLUORESCENT`, `HALOGEN`, `HIGHPRESSUREMERCURY`, `HIGHPRESSURESODIUM`, `LED`, `METALHALIDE`, `OLED`, `TUNGSTENFILAMENT`, `USERDEFINED`, `NOTDEFINED` | Lighting Fixtures | Asset register, Energy and load analysis, Facility management / maintenance, Handover / record model | 400 |
| `CA_ListingCertification` | Listing / certification / Homologation | Text, type | Free text; e.g. ULC-S529 | Fire Alarm Devices | Code and accessibility compliance checking | 300 |
| `CA_MainBoard` | Main board / Tableau principal | Yes/No, instance | `Yes`, `No` | Electrical Equipment | Design review / authoring | 300 |
| `CA_NetworkHeadEndReference` | Network / head-end reference / Référence réseau / centrale | Text, instance | Free text; e.g. SW-2A port 14 | Communication Devices, Data Devices, Nurse Call Devices, Security Devices, Telephone Devices | Commissioning, Facility management / maintenance, Handover / record model | 400 |
| `CA_NumberOfGangs` | Number of gangs / Nombre de modules | Integer, type | ≥ 1 and ≤ 6; e.g. 1 | Electrical Fixtures, Lighting Devices | Design review / authoring | 300 |
| `CA_NumberOfLightSources` | Number of light sources / Nombre de sources | Integer, type | ≥ 1; e.g. 1 | Lighting Fixtures | Facility management / maintenance, Lighting / daylighting analysis | 300 |
| `CA_NumberOfSockets` | Number of sockets / Nombre de prises | Integer, type | ≥ 1 and ≤ 6; e.g. 2 | Electrical Fixtures, Lighting Devices | Design review / authoring | 300 |
| `CA_OnEmergencyLifeSafetyPower` | On emergency / life-safety power / Alimentation de secours | Yes/No, instance | `Yes`, `No` | Electrical Equipment, Electrical Fixtures, Fire Alarm Devices, Lighting Devices, Mechanical Equipment, Plumbing Equipment | Code and accessibility compliance checking | 300 |
| `CA_Placing` | Placing (ceiling / wall / floor ...) / Emplacement | Code list (text), type | `CEILING`, `FLOOR`, `FURNITURE`, `POLE`, `WALL`, `OTHER`, `NOTKNOWN`, `UNSET` | Lighting Fixtures | 3D coordination / clash avoidance, Design review / authoring | 300 |
| `CA_PowerSource` | Power source / Source d'alimentation | Code list (text), type | `PoE`, `PoE+`, `PoE++`, `24 V AC`, `24 V DC`, `120 V`, `347 V`, `Battery` | Communication Devices, Data Devices, Nurse Call Devices, Security Devices, Telephone Devices | Design review / authoring | 300 |
| `CA_PrimaryVoltage_V` | Primary voltage / Tension primaire | Number, type | > 0 V; e.g. 600 | Electrical Equipment | Design review / authoring | 300 |
| `CA_RatedOutput_kW` | Rated output / Puissance assignée | Number, type | > 0 kW; e.g. 500 | Electrical Equipment | Design review / authoring | 200 |
| `CA_RatedPower_kVA` | Rated power / Puissance assignée | Number, type | > 0 kVA; e.g. 150 | Electrical Equipment | Design review / authoring, Energy and load analysis | 200 |
| `CA_SecondaryVoltage_V` | Secondary voltage / Tension secondaire | Number, type | > 0 V; e.g. 208 | Electrical Equipment | Design review / authoring | 300 |
| `CA_SecurityZone` | Security zone / Zone de sécurité | Text, instance | Free text; e.g. Zone B - staff only | Communication Devices, Data Devices, Nurse Call Devices, Security Devices, Telephone Devices | Design review / authoring, Facility management / maintenance | 300 |
| `CA_SwitchFunction` | Switch function / Fonction de l'interrupteur | Code list (text), type | `DOUBLETHROWSWITCH`, `INTERMEDIATESWITCH`, `ONOFFSWITCH`, `OTHER`, `NOTKNOWN`, `UNSET` | Electrical Fixtures, Lighting Devices | Design review / authoring | 300 |
| `CA_TrayConduitType` | Tray / conduit type / Type de chemin de câbles / conduit | Code list (text), type | `LADDER_TRAY`, `VENTILATED_TRAY`, `SOLID_BOTTOM_TRAY`, `WIRE_MESH_TRAY`, `CHANNEL_TRAY`, `EMT`, `RMC`, `IMC`, `RPVC`, `FMC`, `LFMC`, `BUSWAY` | Cable Tray Fittings, Cable Trays, Conduit Fittings, Conduits | Cost estimating | 300 |
| `CA_Type` | Type (dry / liquid-filled) / Type (sec / à liquide isolant) | Text, type | Free text; e.g. Dry type, 150 °C rise | Electrical Equipment | Code and accessibility compliance checking, Cost estimating | 300 |
| `CA_VectorGroup` | Vector group / Couplage | Text, type | Pattern; e.g. `Dyn1`, `YNyn0` | Electrical Equipment | Design review / authoring | 300 |
| `CA_VideoResolution` | Video resolution / Résolution vidéo | Text, type | Pattern; e.g. `3840 x 2160`, `1920 x 1080` | Communication Devices, Data Devices, Nurse Call Devices, Security Devices, Telephone Devices | Design review / authoring | 300 |
| `CA_VisualSignalIntensity_cd` | Visual signal intensity / Intensité du signal visuel | Number, type | > 0 cd; e.g. 75 | Fire Alarm Devices | Code and accessibility compliance checking | 300 |

### 07 Site and civil (39)

| Parameter | Label EN / FR | Type | Accepted values | Revit categories | BIM uses | From LOD |
| --- | --- | --- | --- | --- | --- | --- |
| `Invert Elevation` (built-in) | Invert elevation / Élévation du radier | Length, instance | any m; e.g. 96.120 | Flex Pipes, Pipe Fittings, Pipes | 3D coordination / clash avoidance, Design review / authoring, Existing conditions modelling / renovation, Handover / record model | 300 |
| `CA_BarrierSystemTestLevel` | Barrier system / test level / Système de glissière / niveau d'essai | Text, type | Pattern; e.g. `Steel W-beam, MASH TL-3`, `Concrete single slope, MASH TL-5` |  | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_BeddingClass` | Bedding class / Classe de lit de pose | Text, instance | Free text; e.g. Class B, granular | Pipes | Cost estimating | 300 |
| `CA_ChamberSize` | Chamber size / Dimensions de la chambre | Text, type | Pattern; e.g. `1200 dia.`, `1500 x 1200` | Generic Models, Plumbing Equipment | Cost estimating | 200 |
| `CA_CoordinateReferenceSystem` | Coordinate reference system / Système de référence de coordonnées | Text, instance | Free text; e.g. NAD83(CSRS) MTM zone 8 / CGVD2013 | Site, Topography, Toposolid | 3D coordination / clash avoidance, Design review / authoring, Existing conditions modelling / renovation | 100 |
| `CA_CoverFrame` | Cover / frame / Couvercle / cadre | Text, type | Free text; e.g. Cast iron, heavy duty | Generic Models, Plumbing Equipment | Cost estimating, Facility management / maintenance | 300 |
| `CA_CrossSlope_pct` | Cross slope / Pente transversale | Number, instance | ≥ 0 and ≤ 100 %; e.g. 2 | Floors, Hardscape, Parking | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_CulvertOpening` | Culvert opening / Ouverture du ponceau | Text, type | Pattern; e.g. `2400 x 1800`, `1200 dia.` | Pipes | Design review / authoring | 300 |
| `CA_CurbType` | Curb type / Type de bordure | Text, type | Free text; e.g. Barrier curb, 150 mm reveal | Floors, Hardscape, Parking | Cost estimating, Design review / authoring | 300 |
| `CA_CutFillVolumes` | Cut / fill volumes / Volumes de déblai / remblai | Volume, instance | > 0 m³; e.g. 3200 | Site, Topography, Toposolid | Cost estimating, Quantity take-off | 300 |
| `CA_Depth` | Depth / Profondeur | Length, instance | > 0 m; e.g. 3.4 | Generic Models, Plumbing Equipment | Cost estimating, Quantity take-off | 300 |
| `CA_DepthOfCover` | Depth of cover / Profondeur de recouvrement | Length, instance | > 0 mm; e.g. 1800 | Pipes | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_DesignSpeed_kmh` | Design speed / Vitesse de base | Number, instance | > 0 km/h; e.g. 80 |  | Code and accessibility compliance checking, Design review / authoring | 200 |
| `CA_DropStructure` | Drop structure / Chute (regard de chute) | Yes/No, instance | `Yes`, `No` | Generic Models, Plumbing Equipment | Design review / authoring | 300 |
| `CA_EndTerminal` | End terminal / Extrémité | Text, type | Free text; e.g. Energy-absorbing terminal |  | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_FromToStructure` | From / to structure / Regard amont / aval | Text, instance | Pattern; e.g. `MH-12 to MH-13`, `CB-4 to MH-2` | Pipes | Asset register, Design review / authoring | 300 |
| `CA_GeotechnicalReport` | Geotechnical report / Étude géotechnique | Text, instance | Free text; e.g. GEO-2026-014 | Site, Topography, Toposolid | Design review / authoring, Existing conditions modelling / renovation | 200 |
| `CA_HasStepsLadder` | Has steps / ladder / Avec échelons | Yes/No, type | `Yes`, `No` | Generic Models, Plumbing Equipment | Code and accessibility compliance checking, Facility management / maintenance | 300 |
| `CA_InvertElevation` | Invert elevation(s) / Élévation(s) du radier | Length, instance | any m; e.g. 97.820 | Generic Models, Plumbing Equipment | 3D coordination / clash avoidance, Design review / authoring, Handover / record model | 300 |
| `CA_LaneWidth` | Lane width / Largeur de voie | Length, instance | > 0 m; e.g. 3.5 |  | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_LayerFunction` | Layer function / Fonction de la couche | Code list (text), type | `SURFACE_COURSE`, `BINDER_COURSE`, `BASE`, `SUBBASE`, `SUBGRADE` |  | Cost estimating, Design review / authoring | 200 |
| `CA_LayerMass_t` | Layer mass / Masse de la couche | Number, instance | > 0 t; e.g. 985 |  | Cost estimating, Quantity take-off | 300 |
| `CA_LayerThickness` | Layer thickness / Épaisseur de la couche | Length, type | > 0 mm; e.g. 50 |  | Cost estimating, Design review / authoring, Handover / record model | 200 |
| `CA_LayerVolume` | Layer volume / Volume de la couche | Volume, instance | > 0 m³; e.g. 420 |  | Cost estimating, Quantity take-off | 300 |
| `CA_MixMaterialSpecification` | Mix / material specification / Enrobé / matériau | Text, type | Free text; e.g. HMA surface course, PG 58-28 |  | Cost estimating | 300 |
| `CA_MoveableTemporary` | Moveable / temporary / Amovible | Yes/No, type | `Yes`, `No` |  | Design review / authoring | 300 |
| `CA_ParkingStalls` | Parking stalls / Places de stationnement | Integer, instance | ≥ 1; e.g. 84 | Floors, Hardscape, Parking | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_ParkingUse` | Parking use / Usage du stationnement | Text, instance | Pattern; e.g. `Accessible / EV charging`, `Standard` | Floors, Hardscape, Parking | Code and accessibility compliance checking | 300 |
| `CA_PavementMarking` | Pavement marking / Marquage de la chaussée | Text, type | Free text; e.g. Thermoplastic, white, 100 mm | Floors, Hardscape, Parking | Cost estimating | 300 |
| `CA_PavementThickness` | Pavement thickness / Épaisseur de la chaussée | Length, type | > 0 mm; e.g. 450 | Floors, Hardscape, Parking | Cost estimating, Design review / authoring | 300 |
| `CA_PavementType` | Pavement type / Type de revêtement | Code list (text), type | `FLEXIBLE`, `RIGID`, `USERDEFINED`, `NOTDEFINED` | Floors, Hardscape, Parking | Cost estimating | 200 |
| `CA_PipeClass` | Pipe class / Classe de tuyau | Text, type | Free text; e.g. PVC DR35 | Pipes | Cost estimating, Design review / authoring | 300 |
| `CA_PostSpacing` | Post spacing / Espacement des poteaux | Length, type | > 0 mm; e.g. 1905 |  | Cost estimating | 300 |
| `CA_RimCoverElevation` | Rim / cover elevation / Élévation du couvercle | Length, instance | any m; e.g. 101.250 | Generic Models, Plumbing Equipment | 3D coordination / clash avoidance, Design review / authoring, Handover / record model | 300 |
| `CA_RoughnessAtHandover_mkm` | Roughness (IRI) at handover / Uni (IRI) à la réception | Number, instance | > 0 and ≤ 20 m/km; e.g. 1.2 |  | Asset register, Handover / record model | 500 |
| `CA_SiteArea` | Site area / Superficie du terrain | Area, instance | > 0 m²; e.g. 12500 | Site, Topography, Toposolid | Code and accessibility compliance checking, Design review / authoring | 200 |
| `CA_SurveySourceAndDate` | Survey source and date / Source et date du relevé | Text, instance | Pattern; e.g. `Land surveyor, 2026-04-12`, `Lidar scan, 2025-09-03` | Site, Topography, Toposolid | Existing conditions modelling / renovation | 200 |
| `CA_TransitionSection` | Transition section / Section de transition | Yes/No, instance | `Yes`, `No` |  | Design review / authoring | 300 |
| `CA_UtilityService` | Utility service / Réseau | Code list (text), instance | `AIRCONDITIONING`, `AUDIOVISUAL`, `CHEMICAL`, `CHILLEDWATER`, `COMMUNICATION`, `COMPRESSEDAIR`, `CONDENSERWATER`, `CONTROL`, `CONVEYING`, `DATA`, `DISPOSAL`, `DOMESTICCOLDWATER` (+39 more) | Generic Models, Pipes, Plumbing Equipment | 3D coordination / clash avoidance, Design review / authoring | 200 |

### 08 Rail, bridges and tunnels (49)

| Parameter | Label EN / FR | Type | Accepted values | Revit categories | BIM uses | From LOD |
| --- | --- | --- | --- | --- | --- | --- |
| `CA_ApproachSpeed_kmh` | Approach speed / Vitesse d'approche | Number, instance | > 0 km/h; e.g. 70 |  | Design review / authoring | 300 |
| `CA_BallastDepth` | Ballast depth / Épaisseur du ballast | Length, instance | > 0 mm; e.g. 300 |  | Cost estimating, Quantity take-off | 300 |
| `CA_BallastedTrack` | Ballasted track / Voie ballastée | Yes/No, instance | `Yes`, `No` |  | Cost estimating, Design review / authoring | 200 |
| `CA_Cant` | Cant (superelevation) / Dévers | Length, instance | ≥ 0 and ≤ 200 mm; e.g. 75 |  | Design review / authoring | 300 |
| `CA_ContactWireHeight` | Contact wire height / Hauteur du fil de contact | Length, instance | > 0 mm; e.g. 5600 |  | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_ContinuousWeldedRail` | Continuous welded rail / Longs rails soudés | Yes/No, instance | `Yes`, `No` |  | Design review / authoring | 300 |
| `CA_DesignCodeAndLiveLoad` | Design code and live load / Norme de calcul et charge routière | Text, type | Pattern; e.g. `CSA S6:19, CL-625`, `CSA S6:14, CL-625-ONT` | Floors, Structural Columns, Structural Foundations, Structural Framing, Walls | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_DesignMovementRange` | Design movement range / Course de déplacement de calcul | Text, type | Pattern; e.g. `±75 long. / ±10 trans.`, `±50 long. / ±0 trans.` | Generic Models, Structural Connections | Design review / authoring, Facility management / maintenance | 300 |
| `CA_DisplacementAccommodated` | Displacement accommodated / Déplacement permis | Yes/No, type | `Yes`, `No` | Generic Models, Structural Connections | Design review / authoring | 300 |
| `CA_DivergingRadius` | Diverging radius / Rayon de la voie déviée | Length, type | > 0 m; e.g. 190 |  | Design review / authoring | 300 |
| `CA_DivergingSpeed_kmh` | Diverging speed / Vitesse en voie déviée | Number, type | > 0 km/h; e.g. 40 |  | Design review / authoring | 300 |
| `CA_EdgeOffsetFromTrack` | Edge offset from track / Distance du bord à l'axe de la voie | Length, instance | > 0 mm; e.g. 1600 | Floors, Structural Foundations | 3D coordination / clash avoidance, Code and accessibility compliance checking, Design review / authoring, Handover / record model | 300 |
| `CA_ElectricalClearance` | Electrical clearance / Distance d'isolement | Length, type | > 0 mm; e.g. 250 |  | 3D coordination / clash avoidance, Code and accessibility compliance checking | 300 |
| `CA_FasteningSystem` | Fastening system / Système d'attache | Text, type | Free text; e.g. Elastic clip on tie plate |  | Cost estimating, Facility management / maintenance | 300 |
| `CA_GasketWaterproofing` | Gasket / waterproofing / Joint / étanchéité | Text, type | Free text; e.g. EPDM gasket + hydrophilic strip |  | Design review / authoring, Facility management / maintenance | 300 |
| `CA_Hand` | Hand / Sens de déviation | Code list (text), instance | `LEFTDEVIATION`, `LEFT_LEFTDEVIATION`, `LEFT_RIGHTDEVIATION`, `RIGHTDEVIATION`, `RIGHT_LEFTDEVIATION`, `RIGHT_RIGHTDEVIATION`, `SYMETRIC`, `OTHER`, `NOTKNOWN`, `UNSET` |  | Design review / authoring | 200 |
| `CA_InspectionElementReference` | Inspection element reference / Élément d'inspection | Text, type | Free text; e.g. OSIM 201-GIRDER | Floors, Generic Models, Structural Columns, Structural Connections, Structural Foundations, Structural Framing +1 | Asset register, Facility management / maintenance | 500 |
| `CA_InternalDiameter` | Internal diameter / Diamètre intérieur | Length, type | > 0 mm; e.g. 5800 |  | Design review / authoring | 200 |
| `CA_LineSpeed_kmh` | Line speed / Vitesse de la ligne | Number, instance | > 0 km/h; e.g. 80 |  | Design review / authoring | 200 |
| `CA_LiningThickness` | Lining thickness / Épaisseur du revêtement | Length, type | > 0 mm; e.g. 300 |  | Cost estimating, Design review / authoring | 200 |
| `CA_NumberOfLampsAspects` | Number of lamps / aspects / Nombre de feux | Integer, type | ≥ 1; e.g. 3 |  | Cost estimating, Design review / authoring | 300 |
| `CA_PlatformHeightAboveRail` | Platform height above rail / Hauteur du quai au-dessus du rail | Length, instance | > 0 mm; e.g. 915 | Floors, Structural Foundations | Code and accessibility compliance checking, Design review / authoring, Handover / record model | 300 |
| `CA_PlatformLength` | Platform length / Longueur du quai | Length, instance | > 0 m; e.g. 120 | Floors, Structural Foundations | Design review / authoring, Quantity take-off | 200 |
| `CA_PointMachines` | Point machines / Moteurs d'aiguille | Integer, type | ≥ 0; e.g. 2 |  | Cost estimating, Design review / authoring | 300 |
| `CA_PoleHeight` | Pole height / Hauteur du poteau | Length, type | > 0 m; e.g. 9.0 |  | Cost estimating, Design review / authoring | 300 |
| `CA_PoleStrength` | Pole strength / Résistance du poteau | Text, type | Free text; e.g. Bending 60 kN·m, wind class B |  | Design review / authoring | 300 |
| `CA_Prestressing` | Prestressing / Précontrainte | Text, type | Free text; e.g. Pretensioned, 15.2 mm strand | Floors, Structural Framing | Design review / authoring, Prefabrication / digital fabrication | 300 |
| `CA_RailSection` | Rail section / Profil de rail | Text, type | Pattern; e.g. `115RE`, `60E1` |  | Cost estimating, Design review / authoring | 300 |
| `CA_ReferenceAlignment` | Reference alignment / Alignement de référence | Text, instance | Free text; e.g. ML-01 mainline | Floors, Structural Foundations, Structural Framing | 3D coordination / clash avoidance, Design review / authoring | 100 |
| `CA_RequiredSightingDistance` | Required sighting distance / Distance de visibilité requise | Length, type | > 0 m; e.g. 250 |  | Code and accessibility compliance checking, Design review / authoring | 300 |
| `CA_RingSegmentType` | Ring / segment type / Type d'anneau / de voussoir | Text, type | Free text; e.g. Universal ring 6+1, 1.5 m |  | Design review / authoring, Prefabrication / digital fabrication | 300 |
| `CA_RotationAccommodated` | Rotation accommodated / Rotation permise | Yes/No, type | `Yes`, `No` | Generic Models, Structural Connections | Design review / authoring | 300 |
| `CA_ScourProtection` | Scour protection / Protection contre l'affouillement | Text, instance | Free text; e.g. Rip-rap, design scour 2.5 m | Structural Columns, Structural Foundations, Structural Framing, Walls | Design review / authoring | 300 |
| `CA_SettingTemperaturePreset` | Setting temperature / preset / Température de pose / préréglage | Text, instance | Free text; e.g. 15 °C, preset 12 mm | Generic Models, Structural Connections | Facility management / maintenance, Handover / record model | 500 |
| `CA_SignalType` | Signal type / Type de signal | Code list (text), type | Pattern; e.g. `HOMESIGNAL`, `STARTINGSIGNAL / ROUTEINDICATOR` |  | Design review / authoring | 200 |
| `CA_SpanLength` | Span length / Longueur de portée | Length, instance | > 0 m; e.g. 55 |  | Design review / authoring | 300 |
| `CA_Stagger` | Stagger / Désaxement | Length, instance | > 0 mm; e.g. 200 |  | Design review / authoring | 300 |
| `CA_StructureNumber` | Structure number / Numéro de la structure | Text, instance | Free text; e.g. BR-0123 | Floors, Generic Models, Structural Columns, Structural Connections, Structural Foundations, Structural Framing +1 | Asset register, Design review / authoring | 200 |
| `CA_SupportType` | Support type / Type de support | Code list (text), type | `ENDCATENARYSUPPORT`, `HEADSPANSUPPORT`, `HERSE`, `MULTITRACKSUPPORT`, `RIGIDGANTRY`, `SIMPLESUPPORT`, `OTHER`, `NOTKNOWN`, `UNSET` |  | Cost estimating, Design review / authoring | 200 |
| `CA_SwitchHeater` | Switch heater / Réchauffeur d'aiguillage | Text, type | Free text; e.g. Electric, 2 x 1.5 kW |  | Design review / authoring, Energy and load analysis | 300 |
| `CA_SystemHeight` | System height / Hauteur du système | Length, instance | > 0 mm; e.g. 1200 |  | Design review / authoring | 300 |
| `CA_TactileWarningSurface` | Tactile warning surface / Surface d'avertissement tactile | Yes/No, type | `Yes`, `No` | Floors, Structural Foundations | Code and accessibility compliance checking | 300 |
| `CA_TieSleeperType` | Tie / sleeper type / Type de traverse | Code list (text), type | `COMPOSITESLEEPER`, `CONCRETESLEEPER`, `INSULATEDSTEELSLEEPER`, `MONOBLOCKCONCRETESLEEPER`, `NOTINSULATEDSTEELSLEEPER`, `TWOBLOCKCONCRETESLEEPER`, `WOODENSLEEPER`, `OTHER`, `NOTKNOWN`, `UNSET` |  | Cost estimating | 300 |
| `CA_TieSpacing` | Tie spacing / Espacement des traverses | Length, instance | > 0 mm; e.g. 610 |  | Cost estimating, Quantity take-off | 300 |
| `CA_TrackGauge` | Track gauge / Écartement de la voie | Length, type | > 0 mm; e.g. 1435 |  | Design review / authoring | 200 |
| `CA_TrackIdentifier` | Track identifier / Identifiant de voie | Text, instance | Free text; e.g. Track 2 |  | Design review / authoring, Facility management / maintenance | 200 |
| `CA_TurnoutType` | Turnout type / Type d'aiguillage | Text, type | Free text; e.g. No. 10, RBM frog |  | Cost estimating, Design review / authoring | 200 |
| `CA_VerticalLoadCapacity_kN` | Vertical load capacity / Capacité verticale | Number, type | > 0 kN; e.g. 3500 | Generic Models, Structural Connections | Design review / authoring | 300 |
| `CA_WearingSurfaceWaterproofing` | Wearing surface / waterproofing / Couche de roulement / imperméabilisation | Text, type | Free text; e.g. Membrane + 90 mm asphalt | Floors, Structural Framing | Cost estimating, Facility management / maintenance | 300 |

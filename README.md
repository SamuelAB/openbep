# OpenBEP

OpenBEP is an open toolkit for writing BIM Execution Plans (BEPs) in Canada, for every province and asset type, in English and French.

Vague BIM is useless BIM. When an owner only asks for "the BIM", with no uses, no plan and no deliverables, every firm models its own way and the models don't talk to each other. OpenBEP starts from the uses the owner actually needs and builds everything else from them: the plan, the level of development per element, the parameters each element must carry, and the checks at each milestone.

Why it matters, with the evidence and every source: [openbep.com/why](https://openbep.com/why/) ([français](https://openbep.com/why/fr/)).

> Status: early draft (v0.x). Nothing here has been tested on a live project yet.

## What's here

| Folder | What it is | Status |
|---|---|---|
| [`app/`](app/) | **BEP Builder**: a single-page web app that builds a BEP from a menu of BIM uses across all phases, with an LOD/LOI builder, a model checker for Revit schedule exports, and English/French output. **Project Scorecard**: tracks each party's performance against the plan. Open either file in a browser. | Prototype |
| [`lod/`](lod/) | **LOD specification**: for each of 63 element types, what must be drawn in 3D and which Revit parameters it must carry at LOD 100 to 500 (built-in, OpenBEP shared or family, with the IFC export as an option), and which BIM uses need each requirement. Each element type links to its row in the BEP Builder's LOD matrix. One data file ([`spec.json`](lod/spec.json)) feeds both this page and the BEP Builder; run `python3 lod/build_lod.py` after changing it. Live at [openbep.com/lod/](https://openbep.com/lod/). | Draft v0.2 |
| [`shared-parameters/`](shared-parameters/) | One universal list of Revit shared parameters (`CA_*`, fixed GUIDs) tied to LODs and BIM uses, plus the full manifest and validation rules. | Draft v0.1, not yet loaded in Revit |
| [`docs/parameter-dictionary.md`](docs/parameter-dictionary.md) | The parameter dictionary: every parameter with English and French labels, data type, accepted values, categories, uses and LOD. | Draft v0.1 |
| [`docs/graphics-and-materials-standard.md`](docs/graphics-and-materials-standard.md) | One set of line weights, text styles and material names for Revit projects in Canada. | Draft v0.1 |
| [`revit-addin/`](revit-addin/) | **OpenBEP Sync**: a Revit add-in that makes a model's shared parameters match the plan. | Source only, not yet built or tested |
| [`translations/`](translations/) | One English-keyed dictionary for every page of the site, with French (Quebec), and a glossary of BIM terms it is checked against. | Filled for French |

## Try it or run your own copy

Try it at [openbep.com](https://openbep.com). Your work stays in your browser. Real BEPs hold private information, so for real projects run your own copy on your own computers or network: see [Run your own copy of OpenBEP](docs/self-hosting.md).

## Principles

- **Uses first.** Every requirement traces back to a BIM use the owner chose.
- **Native Revit is the contractual model.** IFC is produced only when a use needs it.
- **One name, one GUID.** Parameter names are English code; French lives in labels and schedules.
- **No brand defaults.** Software and products are examples, never requirements.
- **LOD 500 is never assumed.** Record models are opt-in and priced.
- **Our own wording.** LOD definitions are written for OpenBEP; other standards are cited, not copied.

## Licence

Code is under the [Apache License 2.0](LICENSE). Standards text and data are under [CC BY 4.0](LICENSE-docs.md).

## About the name

The project was drafted under the working name CANBIM. Files and groups now carry the OpenBEP name; the add-in still finds shared parameter files exported under the old name. The `CA_` prefix and the GUIDs stay: they mark Canadian parameters, and changing them would break every model that already holds them.

# OpenBEP translations

Every piece of text on the site is translated from one dictionary. Fix a translation once in the dictionary and every page that shows that English text picks it up. Nobody translates pages one by one.

| File | What it is |
|---|---|
| [`canbim_strings.csv`](canbim_strings.csv) | **The dictionary.** Column `en` is the exact English text shown on the site (the key), then one column per language, then `where` (the page or app step it first appeared on). |
| [`glossary.csv`](glossary.csv) | **The BIM glossary.** One row per BIM term: the approved word in each language, the words to avoid, a note, and where the choice comes from. The dictionary is checked against it. |
| [`glossary_exceptions.csv`](glossary_exceptions.csv) | Rows where a glossary term means something else (an issue's "owner", a product "model"), so the check skips them. |
| [`languages.csv`](languages.csv) | Each language's name and whether the language menu shows it. |
| [`i18n.js`](i18n.js) | The code that looks text up in the dictionary. Site pages load it with `strings.js`. |
| `strings.js` | Built from the dictionary. Do not edit. |
| [`build_i18n.py`](build_i18n.py), [`check_i18n.py`](check_i18n.py), [`tools/`](tools/) | Build, check and test. |

## How a page finds its translation

The page looks up each piece of English text, in this order:

1. the exact sentence;
2. the sentence with its numbers and dates replaced by `{1}`, `{2}`... So one row `Issues ({1})` = `Problèmes ({1})` covers "Issues (3)" and "Issues (66)". Numbers are copied as they are;
3. a list whose parts are all in the dictionary: "Walls, Concept" or "A · B · C";
4. several sentences that are each in the dictionary;
5. "Label: rest", when the label and the rest are each in the dictionary.

Text with no translation stays in English. Text with no lowercase word (codes such as `W310x97` or `PENDANT`) is never reported as missing and stays as typed unless the dictionary has a row for it (`CDE` = `EDC`); that matches the rule that list values are stored as codes.

## Common jobs

**Fix a translation.** Edit the cell in `canbim_strings.csv`, then run:

```
python3 translations/check_i18n.py --fix
python3 translations/build_i18n.py
```

**Change a BIM term everywhere.** Change the approved word in `glossary.csv`, run `python3 translations/check_i18n.py`: it lists every row that still uses the old word. Fix those rows, then build.

**The naming lists page** (`names/`) is built by `python3 names/build_names.py`, not by `build_pages.py`: it writes `names/fr/` from the English master with the same dictionary, plus the French for the text inside the lists (descriptions, groups, element types). Strings with no French are listed, and CI fails on them. No Japanese yet.

**Find text with no translation.** Run `node translations/tools/crawl_app.js` (it opens the BEP Builder and the Scorecard in a headless browser, walks every step of both examples, and lists the dictionary rows they need), then `python3 translations/check_i18n.py --app`. Add the missing rows with their French and build. Open the app with `?translate=1` (for example `app/bep-builder.html?translate=1`) to get a "Strings to translate (CSV)" button in the Glossary: it exports the same list from your own browser.

**Add a language** (Japanese, Spanish...). Add a column to `canbim_strings.csv` named with the language code (`es`), add a line to `languages.csv` with `show` set to `no`, and add `es` and `es_avoid` columns to `glossary.csv`. Fill the glossary first, then the dictionary. Set `show` to `yes` and build when it is ready. Nothing else changes: no new pages.

**Translate a new page.** Write it in English and load the dictionary at the end of the `<head>`:

```html
<script src="../translations/strings.js"></script>
<script src="../translations/i18n.js"></script>
```

Put `<select data-i18n-lang></select>` where the language menu goes (or link with `?lang=fr`). Mark anything that must stay as typed (names, codes, the user's own text) with `data-notr`. Then crawl, add its rows, build.

## The automatic check

On every pull request, GitHub runs the lookup tests, opens the pages in a headless browser and runs `check_i18n.py --app --strict`. It fails when:

- a row uses a word the glossary says to avoid, or none of the approved words;
- a `{1}` placeholder is lost in a translation;
- French typography is off (no-break space before a colon and inside « », none before ; ? ! in Quebec usage, curly apostrophe). `--fix` corrects these;
- the app or `strings.js` was not rebuilt after a dictionary change;
- a page shows English text that has no row in a language the menu shows.

## French

Quebec usage and OQLF typography, accents always. Terms follow the glossary; its `source` column says which document each choice comes from, and only documents we actually opened are cited. Judgment calls still to review with Sam: stage names (conception préliminaire / définitive), "maquette d’archives" for record model, "décision d’un tiers (adjudication)", "plan de livraison" for delivery plan, "fiche de suivi" for scorecard.

## Japanese

Plain business Japanese, です・ます in sentences, short noun phrases in labels and buttons. BIM terms follow the Japanese government's building BIM guidelines (MLIT, 建築分野におけるBIMの標準ワークフローとその活用方策に関するガイドライン 第2版, and 官庁営繕事業におけるBIM活用ガイドライン): BEP = BIM実行計画書, EIR = 発注者情報要件, owner = 発注者, clash detection = 干渉チェック, CDE = 共通データ環境, LOD = 詳細度, preliminary / final design = 基本設計 / 実施設計. Stage "Concept" = 基本計画, "Blocking:" = 要対応：, "To review:" = 要確認：. Canadian names, laws and organisations are kept and translated as written. **Not yet reviewed by a native speaker.** The lookup runtime gives Japanese a full-width colon, 、 between list items and no space between sentences.

The file is still named `canbim_strings.csv` from the project's working name; the app's export button saves under that name too.

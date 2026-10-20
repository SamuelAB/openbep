# "A file format won't fix vague BIM" page

- `index.html` is the English master, served at https://openbep.com/formats/. `fr/` and `ja/` are generated from it by `translations/build_pages.py`; never edit them by hand.
- The English and French pages are written for Canada and the United States, with Quebec's guide as the main written case for open BIM. The Japanese page leaves out the North American intro and the internet comparison (`<!--not:ja-->` blocks) and adds a Japan intro (the `<!--only:ja ... only:ja-->` block). The MLIT permit-review points are on every version.
- To change the text: edit `index.html`, run `python3 translations/build_pages.py --extract`, fill the `fr` and `ja` cells in `translations/canbim_strings.csv` (rows marked "Formats page"), then `python3 translations/check_i18n.py --fix && python3 translations/build_pages.py`.
- French quotes from the Quebec guide use its original wording; quotes from other languages are our translations. Every quote is word for word and every source was opened before it was cited. Never copy research notes, transcripts or licensed standards into the repository.

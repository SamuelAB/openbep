"""Build translated copies of the website's static pages from the English master.

Usage:
  python3 build_pages.py            -> writes every language page, lists missing strings
  python3 build_pages.py --extract  -> adds new English strings to canbim_strings.csv (empty cells)

How it works:
- The English page is the master (for example why/index.html). Every element that carries
  the attribute data-i18n is one translation unit, keyed by its exact inner HTML in English
  (whitespace collapsed). Inline tags such as <a>, <strong> and <sup> stay inside the key,
  so a translator keeps them in the translated cell.
- canbim_strings.csv is the dictionary for the whole site: column en is the key, then one
  column per language (fr, ja, es, ...), then where. Add a language by adding a column.
- Each language page is written to <folder>/<lang>/index.html. A string with no translation
  stays in English and is listed, so nothing is lost silently.
- A page can differ by language. <!--not:ja-->...<!--/not:ja--> is left out of the Japanese
  page. <!--only:ja ... only:ja--> is a comment on the English master (so it does not show)
  and becomes part of the Japanese page only. Its strings are translated like any other.
- Links to another translated page (../why/, ../formats/) point to the same language.
"""
import csv, html, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
TABLE = os.path.join(HERE, 'canbim_strings.csv')  # the one dictionary, shared with the app
# English masters (relative to the repository root) and the where label of their rows
PAGES = {'why/index.html': 'Why OpenBEP', 'formats/index.html': 'Formats page', 'names/index.html': 'Names page'}
# Pages whose language copies another script writes (it adds the French for the lists' data): names/build_names.py
BUILT_ELSEWHERE = {'names/index.html'}

UNIT = re.compile(r'<(?P<tag>[a-z0-9]+)(?P<attrs>[^>]*\bdata-i18n(?![-\w])[^>]*)>(?P<body>.*?)</(?P=tag)>', re.S)
ATTR = re.compile(r'(?P<name>content|alt|aria-label|title|placeholder)="(?P<val>[^"]*)"')
URL = re.compile(r'\b(?P<name>href|src|data-root)="(?P<val>[^"#][^"]*)"')


def norm(s):
    return re.sub(r'\s+', ' ', s).strip()


def units(page):
    """Every translatable string on an English page: element bodies and data-i18n-attr values."""
    out = [norm(m.group('body')) for m in UNIT.finditer(page)]
    for m in re.finditer(r'<[^>]*\bdata-i18n-attr\b[^>]*>', page):
        out += [html.unescape(a.group('val')) for a in ATTR.finditer(m.group(0))]
    return out


def load():
    if not os.path.exists(TABLE):
        return [], ['en', 'fr', 'where']
    with open(TABLE, encoding='utf-8-sig', newline='') as f:
        r = csv.DictReader(f)
        return list(r), r.fieldnames


def save(rows, fields):
    with open(TABLE, 'w', encoding='utf-8-sig', newline='') as f:
        w = csv.DictWriter(f, fieldnames=fields, quoting=csv.QUOTE_ALL, lineterminator='\n')
        w.writeheader()
        w.writerows(rows)


def relative(val, depth):
    if re.match(r'^[a-z]+:|^/', val):
        return val
    return '../' * depth + val


def variants(page, lang):
    """Keep the parts of a page meant for this language (see the module notes)."""
    page = re.sub(r'<!--not:%s-->.*?<!--/not:%s-->\n?' % (lang, lang), '', page, flags=re.S)
    page = re.sub(r'<!--only:(\w+)\n(.*?)\nonly:\1-->\n?',
                  lambda m: m.group(2) + '\n' if m.group(1) == lang else '', page, flags=re.S)
    return page


def translate(page, lang, table, missing):
    page = variants(page, lang)
    def body(m):
        key = norm(m.group('body'))
        t = table.get(key)
        if not t:
            missing.add(key)
            t = key
        return '<%s%s>%s</%s>' % (m.group('tag'), m.group('attrs'), t, m.group('tag'))

    def attrs(m):
        def one(a):
            key = html.unescape(a.group('val'))
            t = table.get(key)
            if not t:
                missing.add(key)
                t = key
            return '%s="%s"' % (a.group('name'), html.escape(t, quote=True))
        return ATTR.sub(one, m.group(0))

    out = UNIT.sub(body, page)
    out = re.sub(r'<[^>]*\bdata-i18n-attr\b[^>]*>', attrs, out)
    out = re.sub(r'<html lang="en"', '<html lang="%s"' % lang, out, count=1)
    # Links into the app open it in the page's language.
    out = out.replace('bep-builder.html?lang=en', 'bep-builder.html?lang=%s' % lang)
    # The LOD page reads ?lang= (the master links to it with ?lang=en).
    out = re.sub(r'href="\.\./lod/(?:\?lang=en)?(#[^"]*)?"', lambda m: 'href="../lod/?lang=%s%s"' % (lang, m.group(1) or ''), out)
    # Links to another translated page stay in this language.
    for folder in sorted({os.path.dirname(p) for p in PAGES}):
        out = out.replace('href="../%s/"' % folder, 'href="../%s/%s/"' % (folder, lang))
    # The language page sits one folder deeper than the master.
    out = URL.sub(lambda m: '%s="%s"' % (m.group('name'), relative(m.group('val'), 1)), out)
    return out


def main():
    rows, fields = load()
    langs = [c for c in fields if c not in ('en', 'where')]
    if '--extract' in sys.argv:
        current = {p: list(units(open(os.path.join(ROOT, p), encoding='utf-8').read())) for p in PAGES}
        # Drop strings no longer on their page (the English was reworded), and say so.
        label = {PAGES[p]: p for p in PAGES}
        stale = [r for r in rows if r.get('where') in label and r['en'] not in current[label[r['where']]]]
        for r in stale:
            print('  removed (no longer on %s): %s' % (label[r['where']], r['en'][:90]))
        rows = [r for r in rows if r not in stale]
        known = {r['en'] for r in rows}
        added = 0
        for p in PAGES:
            for key in current[p]:
                if key and key not in known:
                    rows.append({**{c: '' for c in fields}, 'en': key, 'where': PAGES[p]})
                    known.add(key)
                    added += 1
        save(rows, fields)
        print('added %d new strings to %s' % (added, os.path.basename(TABLE)))
        return
    for p in PAGES:
        if p in BUILT_ELSEWHERE:
            continue
        master = open(os.path.join(ROOT, p), encoding='utf-8').read()
        for lang in langs:
            table = {norm(r['en']): r[lang] for r in rows if r.get(lang)}
            if not any(k in table for k in units(master)):
                continue  # language column not started for this page
            missing = set()
            out = translate(master, lang, table, missing)
            dest = os.path.join(ROOT, os.path.dirname(p), lang, os.path.basename(p))
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            open(dest, 'w', encoding='utf-8').write(out)
            print('%s -> %s (%d missing)' % (p, os.path.relpath(dest, ROOT), len(missing)))
            for k in sorted(missing):
                print('  missing [%s]: %s' % (lang, k[:90]))


if __name__ == '__main__':
    main()

#!/usr/bin/env python3
"""Build the naming lists page (names/index.html) and its CSV downloads.

Sources, never edited here:
- shared-parameters/OpenBEP_SharedParameters_v0.1.txt   (every OB_ shared parameter, GUID, group)
- shared-parameters/OpenBEP_Parameters_Manifest_v0.1.csv (labels, element types, LOD, built-ins)
- lod/spec.json                                          (labels of shared parameters not yet in the manifest)
- docs/graphics-and-materials-standard.md                (materials, line, text and dimension tables)
- library/families.csv                                   (family names)
- docs/naming-rules.md                                   (rules for every name, model files, views)

Writes:
- the block between /*NAMES*/ and /*/NAMES*/ in names/index.html
- names/openbep-naming-rules.csv, openbep-parameters.csv, openbep-materials.csv, openbep-lines.csv, openbep-text.csv, openbep-families.csv

Run `python3 names/build_names.py` after changing a source, or `--check` (CI) to fail when out of date.
"""
import csv, io, json, re, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'names'
SP = ROOT / 'shared-parameters'
DOC = ROOT / 'docs' / 'graphics-and-materials-standard.md'


def lod_key(v):
    m = re.match(r'\d+', v or '')
    return int(m.group()) if m else 999


def parameters():
    txt = (SP / 'OpenBEP_SharedParameters_v0.1.txt').read_text(encoding='utf-8')
    groups, shared = {}, {}
    for line in txt.splitlines():
        f = line.split('\t')
        if f[0] == 'GROUP':
            groups[f[1]] = f[2].replace('OpenBEP ', '', 1)
        elif f[0] == 'PARAM':
            shared[f[2]] = {'guid': f[1], 'type': f[3], 'group': f[5]}
    rows = {}
    with open(SP / 'OpenBEP_Parameters_Manifest_v0.1.csv', encoding='utf-8-sig') as fh:
        for r in csv.DictReader(fh):
            n = r['Parameter (Revit name)']
            p = rows.setdefault(n, {
                'name': n, 'kind': 'Built-in' if r['Shared or built-in'] == 'Built-in' else 'Shared',
                'guid': r['GUID'], 'type': r['Data type'], 'unit': r['Unit'], 'scope': r['Instance or type'],
                'group': r['Group'], 'en': r['Label (EN)'], 'fr': r['Label (FR)'], 'els': [], 'lod': '',
                'ex': r['Example value'], 'desc': r['Description']})
            if r['Element type'] and r['Element type'] not in p['els']:
                p['els'].append(r['Element type'])
            if r['Required from LOD'] and (not p['lod'] or lod_key(r['Required from LOD']) < lod_key(p['lod'])):
                p['lod'] = r['Required from LOD']
    spec = json.loads((ROOT / 'lod' / 'spec.json').read_text(encoding='utf-8'))
    by_rn = {}
    def walk(o):
        if isinstance(o, dict):
            if o.get('rn') and o.get('l'):
                by_rn.setdefault(o['rn'], o)
            for v in o.values():
                walk(v)
        elif isinstance(o, list):
            for v in o:
                walk(v)
    walk(spec)
    for n, s in shared.items():
        p = rows.get(n)
        if p is None:
            src = by_rn.get(n, {})
            p = rows[n] = {'name': n, 'kind': 'Shared', 'guid': s['guid'], 'type': s['type'], 'unit': src.get('u') or '',
                           'scope': src.get('sc') or '', 'group': groups[s['group']], 'en': src.get('l', ''),
                           'fr': src.get('lf', ''), 'els': [], 'lod': '', 'ex': src.get('ex') or '', 'desc': src.get('d', '')}
        else:
            p['guid'], p['type'], p['group'] = s['guid'], s['type'], groups[s['group']]
    missing = [n for n, p in rows.items() if p['kind'] == 'Shared' and n not in shared]
    if missing:
        sys.exit('Shared in the manifest but not in the shared parameter file: ' + ', '.join(missing))
    return sorted(rows.values(), key=lambda p: (p['kind'] != 'Shared', p['group'], p['name'].lower()))


def tables_after(md, heading):
    """Rows (list of dicts) of the first Markdown table after an exact heading line."""
    lines = md.splitlines()
    i = lines.index(heading)
    while not lines[i].startswith('|'):
        i += 1
    head = [c.strip() for c in lines[i].strip('|').split('|')]
    out = []
    for line in lines[i + 2:]:
        if not line.startswith('|'):
            break
        cells = [clean(c) for c in re.split(r'(?<!\\)\|', line.strip()[1:-1])]
        out.append(dict(zip(head, cells)))
    return out


def clean(s):
    return s.strip().replace('`', '').replace('\\_', '_').replace('\\|', '|').replace('**', '')


def graphics():
    md = DOC.read_text(encoding='utf-8')
    classes = re.search(r'Classes are fixed: ([^.]+)\.', md).group(1).split(', ')
    mats = [{'name': r['Example'], 'cls': r['Class'], 'fr': r['Matériau (FR)']} for r in tables_after(md, '### Name pattern')]
    patterns = [{'name': r['Name'], 'dash': r['Dashes and gaps (mm)'], 'use': r['Used for'], 'fr': r['Nom (FR)']}
                for r in tables_after(md, '### Line patterns (lengths on paper)')]
    pens = [{'pen': r['Pen'], 'w': [r[k] for k in ('1:10', '1:20', '1:50', '1:100', '1:200', '1:500')],
             'use': r['Used for'], 'fr': r['Usage (FR)']} for r in tables_after(md, '### Model line weights (mm on paper)')]
    sec = md.split('### Line styles', 1)[1].split('###', 1)[0]
    styles = re.findall(r'`(OB_\d_[A-Za-z]+)`', sec)
    objs = [{'cat': r['Category'], 'proj': r['Projection pen'], 'cut': r['Cut pen'], 'pat': r['Pattern']}
            for r in tables_after(md, '### Object styles (key categories)')]
    text = [{'name': r['Name'], 'font': r['Font'], 'h': r['Height (mm)'], 'use': r['Used for'], 'fr': r['Usage (FR)']}
            for r in tables_after(md, '### Text types')]
    dims = [{'name': r['Name'], 'units': r['Units'], 'text': r['Text'], 'use': r['Used for']}
            for r in tables_after(md, '### Dimension types')]
    arrows = re.findall(r'`(OB_(?:Arrow|Tick)_[A-Za-z0-9_]+)`', md)
    return {'classes': classes, 'materials': mats, 'patterns': patterns, 'pens': pens, 'styles': styles,
            'objects': objs, 'text': text, 'dims': dims, 'arrows': list(dict.fromkeys(arrows))}


def naming():
    md = (ROOT / 'docs' / 'naming-rules.md').read_text(encoding='utf-8')
    rules = [{'rule': r['Rule'], 'yes': r['Write'], 'no': r['Not'], 'fr': r['Règle (FR)']}
             for r in tables_after(md, '## Rules for every name')]
    pattern = re.search(r'## Model files\n\n`([^`]+)`', md).group(1)
    example = re.search(r'Example: `([^`]+\.rvt)`', md).group(1)
    disc = [{'code': r['Code'], 'en': r['Model'], 'fr': r['Modèle (FR)']} for r in tables_after(md, '## Model files')]
    phases = [{'code': r['Code'], 'en': r['Phase'], 'fr': r['Phase (FR)']} for r in tables_after(md, '### Phases')]
    sets = [{'code': r['Code'], 'en': r['Set'], 'holds': r['Holds'], 'fr': r['Ensemble (FR)']}
            for r in tables_after(md, '### Drawing sets')]
    vtypes = [{'code': r['Code'], 'en': r['View'], 'fr': r['Vue (FR)']} for r in tables_after(md, '### View types')]
    vsec = md.split('## Views', 1)[1]
    view_pattern = re.search(r'`(<PHASE>[^`]+)`', vsec).group(1)
    view_example = re.search(r'Example: `([^`]+)`', vsec).group(1)
    builtins = [{'name': r['Parameter'], 'job': r['Job'], 'ex': r['Example'], 'fr': r['Rôle (FR)']}
                for r in tables_after(md, '## Built-in parameters with a fixed job')]
    return {'rules': rules, 'file': pattern, 'fileEx': example, 'disc': disc, 'phases': phases, 'sets': sets,
            'vtypes': vtypes, 'view': view_pattern, 'viewEx': view_example, 'builtins': builtins}


def families():
    out = {}
    with open(ROOT / 'library' / 'families.csv', encoding='utf-8-sig') as fh:
        for r in csv.DictReader(fh):
            f = out.setdefault(r['Family'], {'name': r['Family'], 'fr': r['Family (FR)'], 'el': r['Element type'],
                                             'tpl': r['Templates'], 'types': []})
            f['types'].append(r['Type'])
    return list(out.values())


def csv_text(head, rows):
    b = io.StringIO()
    w = csv.writer(b, lineterminator='\r\n')
    w.writerow(head)
    w.writerows(rows)
    return '﻿' + b.getvalue()


def build():
    P, G, F, N = parameters(), graphics(), families(), naming()
    files = {
        'openbep-naming-rules.csv': csv_text(
            ['List', 'Code or rule', 'Meaning or example', 'Not', 'Label (FR)'],
            [['Rule for every name', r['rule'], r['yes'], r['no'], r['fr']] for r in N['rules']] +
            [['Model file name', N['file'], N['fileEx'], '', '']] +
            [['Discipline code', d['code'], d['en'], '', d['fr']] for d in N['disc']] +
            [['View name', N['view'], N['viewEx'], '', '']] +
            [['Phase code', x['code'], x['en'], '', x['fr']] for x in N['phases']] +
            [['Drawing set code', x['code'], x['en'] + ': ' + x['holds'], '', x['fr']] for x in N['sets']] +
            [['View type code', x['code'], x['en'], '', x['fr']] for x in N['vtypes']] +
            [['Built-in parameter', b['name'], b['job'] + (' (' + b['ex'] + ')' if b['ex'] else ''), '', b['fr']]
             for b in N['builtins']]),
        'openbep-parameters.csv': csv_text(
            ['Revit name', 'Shared or built-in', 'GUID', 'Data type', 'Unit', 'Instance or type', 'Group', 'Label (EN)',
             'Label (FR)', 'Element types', 'First required at LOD', 'Example value', 'Description'],
            [[p['name'], p['kind'], p['guid'], p['type'], p['unit'], p['scope'], p['group'], p['en'], p['fr'],
              '; '.join(p['els']), p['lod'], p['ex'], p['desc']] for p in P]),
        'openbep-materials.csv': csv_text(
            ['List', 'Name', 'Class', 'Label (FR)'],
            [['Class', c, c, ''] for c in G['classes']] +
            [['Example material', m['name'], m['cls'], m['fr']] for m in G['materials']]),
        'openbep-lines.csv': csv_text(
            ['List', 'Name', 'Dashes and gaps (mm) or pen', 'Used for', 'Label (FR)'],
            [['Line pattern', x['name'], x['dash'], x['use'], x['fr']] for x in G['patterns']] +
            [['Line style (example)', s, 'Pen ' + s.split('_')[1], '', ''] for s in G['styles']] +
            [['Pen', x['pen'], ' / '.join(x['w']) + ' mm at 1:10 / 1:20 / 1:50 / 1:100 / 1:200 / 1:500', x['use'], x['fr']]
             for x in G['pens']]),
        'openbep-text.csv': csv_text(
            ['List', 'Name', 'Font or units', 'Height (mm) or text type', 'Used for', 'Label (FR)'],
            [['Text type', t['name'], t['font'], t['h'], t['use'], t['fr']] for t in G['text']] +
            [['Dimension type', d['name'], d['units'], d['text'], d['use'], ''] for d in G['dims']] +
            [['Arrowhead', a, '', '', '', ''] for a in G['arrows']]),
        'openbep-families.csv': csv_text(
            ['Family', 'Family (FR)', 'Element type', 'Template', 'Types'],
            [[f['name'], f['fr'], f['el'], f['tpl'], '; '.join(f['types'])] for f in F]),
    }
    data = json.dumps({'P': P, 'G': G, 'F': F, 'N': N}, ensure_ascii=False, separators=(',', ':'))
    page = (OUT / 'index.html').read_bytes().decode('utf-8')
    a, b = '/*NAMES*/', '/*/NAMES*/'
    i, j = page.index(a) + len(a), page.index(b)
    files['index.html'] = page[:i] + 'const D=' + data + ';' + page[j:]
    return files


def main():
    check = '--check' in sys.argv
    stale = []
    for name, text in build().items():
        path = OUT / name
        old = path.read_bytes().decode('utf-8') if path.exists() else None
        if old != text:
            stale.append(name)
            if not check:
                path.write_text(text, encoding='utf-8', newline='')
    if check and stale:
        sys.exit('names/ is out of date, run python3 names/build_names.py: ' + ', '.join(stale))
    print(('Up to date' if not stale else 'Wrote ' + ', '.join(stale)))


if __name__ == '__main__':
    main()

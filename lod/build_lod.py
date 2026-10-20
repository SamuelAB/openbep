"""Build the OpenBEP LOD specification data from its single source.

The source is lod/spec.json: levels, BIM uses, parameters (P) and element types (C).
Each element type carries
  g   one-line summary of the geometry at each LOD,
  gx  granular geometry requirements: t (text), f (required from LOD), k (kind), u (BIM uses),
  nx  what is never expected in the model, and where that information lives instead,
  r   parameter requirements: [parameter id, required from LOD, BIM uses, note on a valid value],
  m   the LOD matrix row it follows in the BEP Builder (discipline.row).

Usage: python3 lod/build_lod.py [--check]
- Writes lod/spec-data.js, which the LOD specification page loads.
- Rewrites the block between /*LOD*/ and /*/LOD*/ in app/bep-builder.html, so the
  Builder's element requirements use exactly the same data.
- Copies lod/illustrations.js (the LOD sketches) between /*ILL*/ and /*/ILL*/ in the app,
  so the LOD matrix can show the same sketches as the specification page.
- With --check, changes nothing and fails if either file is out of date.
Run it after every change to spec.json, then commit the rebuilt files.
"""
import json, os, sys
here = os.path.dirname(os.path.abspath(__file__))
root = os.path.dirname(here)
APP = os.path.join(root, 'app', 'bep-builder.html')
JS = os.path.join(here, 'spec-data.js')
LEVELS = ['100', '200', '300', '350', '400', '500']
KINDS = {'shape', 'place', 'open', 'zone', 'conn', 'part', 'rec'}


def validate(d):
    errs = []
    for c in d['C']:
        for x in c.get('gx', []):
            if x['f'] not in LEVELS: errs.append(f"{c['id']}: bad LOD {x['f']}")
            if x['k'] not in KINDS: errs.append(f"{c['id']}: bad kind {x['k']}")
            errs += [f"{c['id']}: unknown use {u}" for u in x.get('u', []) if u not in d['uses']]
        errs += [f"{c['id']}: unknown parameter {r[0]}" for r in c['r'] if r[0] not in d['P']]
        errs += [f"{c['id']}: unknown use {u}" for r in c['r'] for u in r[2] if u not in d['uses']]
    return errs


def main(check):
    d = json.load(open(os.path.join(here, 'spec.json'), encoding='utf-8'))
    errs = validate(d)
    if errs: sys.exit('spec.json errors:\n' + '\n'.join(errs))
    blob = json.dumps(d, ensure_ascii=False, separators=(',', ':'))
    js = '/* Built by lod/build_lod.py from lod/spec.json. Do not edit. */\nwindow.OPENBEP_LOD=' + blob + ';\n'
    s = open(APP, encoding='utf-8').read()
    a, b = '/*LOD*/', '/*/LOD*/'
    i = s.index(a) + len(a); j = s.index(b, i)
    app = s[:i] + blob + s[j:]
    ill = open(os.path.join(here, 'illustrations.js'), encoding='utf-8').read().strip()
    a, b = '/*ILL*/', '/*/ILL*/'
    i = app.index(a) + len(a); j = app.index(b, i)
    app = app[:i] + '\n' + ill + '\n' + app[j:]
    old_js = open(JS, encoding='utf-8').read() if os.path.exists(JS) else ''
    if check:
        bad = [n for n, ok in (('lod/spec-data.js', old_js == js), ('app/bep-builder.html', app == s)) if not ok]
        if bad: sys.exit('Out of date, run python3 lod/build_lod.py: ' + ', '.join(bad))
        print('LOD data up to date:', len(d['C']), 'element types')
        return
    open(JS, 'w', encoding='utf-8').write(js)
    open(APP, 'w', encoding='utf-8').write(app)
    print(len(d['C']), 'element types,', sum(len(c.get('gx', [])) for c in d['C']), 'geometry requirements,',
          sum(len(c['r']) for c in d['C']), 'parameter requirements')


if __name__ == '__main__':
    main('--check' in sys.argv)

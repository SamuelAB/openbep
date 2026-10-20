#!/usr/bin/env python3
"""Checks library/families.csv against the parameter manifest and the LOD element types.

Every family must name an element type the manifest knows (so it gets its OpenBEP shared parameters),
a Revit category that element type uses, a known kind, and positive sizes; type names are unique per family.
"""
import csv, json, pathlib, re, sys

root = pathlib.Path(__file__).resolve().parent.parent
rows = list(csv.DictReader(open(root / 'library/families.csv', encoding='utf-8-sig')))
man = list(csv.DictReader(open(next((root / 'shared-parameters').glob('OpenBEP_Parameters_Manifest*.csv')), encoding='utf-8-sig')))
spec = json.load(open(root / 'lod/spec.json', encoding='utf-8'))
cats = {c['n']: {m for r in c.get('rc', []) for m in re.findall(r'\((OST_\w+)\)', r)} for c in spec['C']}
shared = {}
for m in man:
    if m['Shared or built-in'] == 'Shared':
        shared.setdefault(m['Element type'], set()).add(m['GUID'])

errors, seen, fams = [], set(), {}
for i, r in enumerate(rows, start=2):
    el, fam = r['Element type'], r['Family']
    if el not in cats: errors.append(f'row {i}: element type "{el}" is not in lod/spec.json')
    elif r['Revit category id'] not in cats[el]: errors.append(f'row {i}: {r["Revit category id"]} is not a category of "{el}"')
    if el not in shared: errors.append(f'row {i}: the manifest has no shared parameters for "{el}"')
    if r['Kind'] not in ('box', 'door', 'window'): errors.append(f'row {i}: unknown kind "{r["Kind"]}"')
    if not re.fullmatch(r'OpenBEP_[A-Za-z]+_[A-Za-z0-9-]+', fam): errors.append(f'row {i}: family name "{fam}" does not follow OpenBEP_<Category>_<Name>')
    if not all(t.strip().endswith('.rft') for t in r['Templates'].split(';')): errors.append(f'row {i}: templates must be .rft file names')
    for k in ('Width mm', 'Depth mm', 'Height mm'):
        try:
            if float(r[k]) <= 0: raise ValueError
        except ValueError: errors.append(f'row {i}: {k} must be a positive number')
    if (fam, r['Type']) in seen: errors.append(f'row {i}: type "{r["Type"]}" appears twice in {fam}')
    seen.add((fam, r['Type']))
    first = fams.setdefault(fam, r)
    for k in ('Family (FR)', 'Element type', 'Revit category id', 'Templates', 'Kind', 'Assembly Code', 'Description'):
        if r[k] != first[k]: errors.append(f'row {i}: {k} differs from the first row of {fam}')

for e in errors: print('ERROR', e)
print(f'{len(fams)} families, {len(rows)} types, {sum(len(shared.get(r["Element type"], ())) for r in fams.values())} shared parameters in all. {len(errors)} errors.')
sys.exit(1 if errors else 0)

#!/usr/bin/env python3
"""Writes compliance/example-school.json: a SIMULATED series of VIM Server versions of one
federated model, checked against the example school plan. Nothing here was measured on a real model.

The numbers are invented to show what a compliance-over-time screen has to make visible:
a firm that slows down, a requirement that grows at a milestone, a holiday, a version that
could not be checked, and a contractor who joins after award.

Usage: python3 compliance/build_example.py          (rewrites the file)
       python3 compliance/build_example.py --check  (fails if the file is out of date)
"""
import json, math, os, random, sys
from datetime import date, timedelta

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'example-school.json')
R = random.Random(20271126)

ROLES = [
    {'code': 'ARCH', 'role': 'Architect', 'firm': 'Architect (example)', 'document': 'SCH-ARCH.rvt', 'from': None},
    {'code': 'STRUCT', 'role': 'Structural engineer', 'firm': 'Structural engineer (example)', 'document': 'SCH-STRUCT.rvt', 'from': None},
    {'code': 'MECH', 'role': 'MEP engineer', 'firm': 'MEP engineer (example)', 'document': 'SCH-MECH.rvt', 'from': None},
    {'code': 'CON', 'role': 'Contractor', 'firm': 'Contractor (example, named at award)', 'document': 'SCH-CON.rvt', 'from': '2027-09-24'},
]
GOALS = [
    {'id': 'cost', 'name': 'Control cost and budget', 'uses': ['Quantities and cost estimate']},
    {'id': 'change', 'name': 'Reduce change orders', 'uses': ['3D coordination (clash detection)', 'Design review in 3D', 'Drawings from the model']},
    {'id': 'handover', 'name': 'Better handover to maintenance', 'uses': ['Asset handover to maintenance', 'Record, as-built and operations models']},
    {'id': 'basics', 'name': 'Basics every plan requires', 'uses': ['Design in BIM, all disciplines']},
]
MILESTONES = [
    {'id': 'prelim100', 'name': 'Preliminary design 100%', 'date': '2027-03-26', 'targetPercent': 70},
    {'id': 'final90', 'name': 'Final design 90%', 'date': '2027-06-25', 'targetPercent': 85},
    {'id': 'ifc', 'name': 'Issued for construction', 'date': '2027-09-10', 'targetPercent': 95},
    {'id': 'constr', 'name': 'Construction, first coordination cycle', 'date': '2027-11-26', 'targetPercent': 95},
]
HOLIDAYS = [{'name': 'Construction holiday (example)', 'from': '2027-07-18', 'to': '2027-07-31'}]

# Value checks: required values that must be filled with a real value. k = required values per element, by role.
# lag < 1 means the role fills this check later than its other work.
VALUE_CHECKS = [
    {'id': 'assembly-code', 'goal': 'cost', 'label': 'Assembly Code filled', 'k': {'ARCH': 1, 'STRUCT': 1, 'MECH': 1, 'CON': .2}, 'lag': {'MECH': .85}},
    {'id': 'real-material', 'goal': 'cost', 'label': 'Elements with a real material', 'k': {'ARCH': 1, 'STRUCT': 1}, 'lag': {'ARCH': .9}},
    {'id': 'lod-params', 'goal': 'change', 'label': 'Parameters required by the LOD matrix at this milestone', 'k': {'ARCH': 2.2, 'STRUCT': 2.6, 'MECH': 2.0, 'CON': .5}, 'lag': {}},
    {'id': 'fire-rating', 'goal': 'change', 'label': 'Fire rating on walls, doors and floors', 'k': {'ARCH': .35, 'STRUCT': .2}, 'lag': {'ARCH': 1.05}},
    {'id': 'asset-data', 'goal': 'handover', 'label': 'Equipment asset data (OB_ asset parameters)', 'k': {'MECH': 1.6, 'ARCH': .15, 'CON': 3.0}, 'lag': {'MECH': .55, 'ARCH': .7, 'CON': .8}},
    {'id': 'rooms', 'goal': 'handover', 'label': 'Rooms numbered and named by the plan', 'k': {'ARCH': .12}, 'lag': {'ARCH': 1.1}},
    {'id': 'worksets', 'goal': 'basics', 'label': 'Elements on a named workset', 'k': {'ARCH': 1, 'STRUCT': 1, 'MECH': 1, 'CON': 1}, 'lag': {'ARCH': 1.15, 'STRUCT': 1.2, 'MECH': 1.1, 'CON': 1.2}},
    {'id': 'view-names', 'goal': 'basics', 'label': 'Views named by the naming rules', 'k': {'ARCH': .06, 'STRUCT': .05, 'MECH': .05, 'CON': .03}, 'lag': {'MECH': .8}},
]
RULES = [
    {'id': 'on-level', 'goal': 'cost', 'label': 'Elements on a level', 'limit': 95, 'unit': '%', 'limitText': 'at least 95 %', 'direction': 'higherIsBetter'},
    {'id': 'clashes', 'goal': 'change', 'label': 'Clashes open for more than one coordination cycle', 'limit': 25, 'unit': '', 'limitText': 'at most 25', 'direction': 'lowerIsBetter'},
    {'id': 'warnings', 'goal': 'change', 'label': 'Revit warnings', 'limit': 1500, 'unit': '', 'limitText': 'at most 1,500', 'direction': 'lowerIsBetter'},
    {'id': 'dup-mark', 'goal': 'handover', 'label': 'No duplicate Mark', 'limit': 0, 'unit': '', 'limitText': 'limit 0', 'direction': 'lowerIsBetter'},
    {'id': 'worksets-exist', 'goal': 'basics', 'label': 'The named worksets exist', 'limit': 0, 'unit': '', 'limitText': 'limit 0 missing', 'direction': 'lowerIsBetter'},
    {'id': 'dup-levels', 'goal': 'basics', 'label': 'Duplicate levels', 'limit': 0, 'unit': '', 'limitText': 'limit 0', 'direction': 'lowerIsBetter'},
]

D = date.fromisoformat
START, END = D('2027-01-08'), D('2027-11-26')
def in_holiday(d):
    return any(D(h['from']) <= d <= D(h['to']) for h in HOLIDAYS)
weeks = []
d = START
while d <= END:
    weeks.append(d); d += timedelta(days=7)

def lerp(points, x):
    """Piecewise-linear through (iso date, value) points."""
    pts = [(D(a).toordinal(), b) for a, b in points]
    x = x.toordinal()
    if x <= pts[0][0]: return pts[0][1]
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        if x <= x1: return y0 + (y1 - y0) * (x - x0) / (x1 - x0)
    return pts[-1][1]

# Elements each role has in its model.
ELEMS = {
    'ARCH': [('2027-01-08', 8200), ('2027-03-26', 12400), ('2027-06-25', 17800), ('2027-09-10', 20600), ('2027-11-26', 21300)],
    'STRUCT': [('2027-01-08', 2900), ('2027-03-26', 4700), ('2027-06-25', 6100), ('2027-11-26', 6500)],
    'MECH': [('2027-01-08', 900), ('2027-03-26', 2600), ('2027-05-07', 5200), ('2027-06-25', 11200), ('2027-09-10', 13800), ('2027-11-26', 14500)],
    'CON': [('2027-09-24', 300), ('2027-11-26', 2400)],
}
# Share of the required values each role has filled. It falls at each milestone because the plan asks for more
# from the next day. ARCH slows in April and May (staff turnover), MECH starts late and sprints before Final 90%.
LEVEL = {
    'ARCH': [('2027-01-08', .46), ('2027-03-26', .78), ('2027-04-02', .66), ('2027-04-09', .68), ('2027-05-21', .70), ('2027-06-25', .86),
             ('2027-07-02', .79), ('2027-07-16', .82), ('2027-08-06', .83), ('2027-09-10', .95), ('2027-09-17', .93), ('2027-11-26', .965)],
    'STRUCT': [('2027-01-08', .58), ('2027-03-26', .90), ('2027-04-02', .80), ('2027-06-25', .95), ('2027-07-02', .90), ('2027-09-10', .975),
               ('2027-09-17', .96), ('2027-11-26', .98)],
    'MECH': [('2027-01-08', .30), ('2027-03-26', .52), ('2027-04-02', .41), ('2027-05-07', .47), ('2027-06-04', .55), ('2027-06-25', .77),
             ('2027-07-02', .68), ('2027-08-06', .72), ('2027-09-10', .90), ('2027-09-17', .86), ('2027-11-26', .93)],
    'CON': [('2027-09-24', .35), ('2027-10-22', .52), ('2027-11-26', .81)],
}
# Each milestone asks for more values per element than the one before (the LOD matrix steps up),
# so the share filled drops the day after a milestone even though nothing was lost.
GROWTH = {'prelim100': 1.0, 'final90': 1.18, 'ifc': 1.30, 'constr': 1.33}
# Of the values that do not meet the plan, the share that holds something wrong (a placeholder, a value
# outside the allowed list) rather than nothing. MECH fills many placeholders.
WRONG = {'ARCH': .14, 'STRUCT': .08, 'MECH': .38, 'CON': .2}
# Of the values required on elements added this week, the share that arrives filled. A firm that models
# clean keeps this high; a firm that models first and fills later does not.
CLEAN = {'ARCH': .82, 'STRUCT': .93, 'MECH': .44, 'CON': .66}
# Weeks a role uploaded nothing new. The federated version keeps that role's previous model.
NO_UPLOAD = {
    'ARCH': {'2027-04-16', '2027-04-30', '2027-05-07'},
    'STRUCT': {'2027-02-05', '2027-07-09', '2027-08-13', '2027-08-27', '2027-10-01', '2027-10-15', '2027-10-29', '2027-11-12'},
    'MECH': {'2027-01-15', '2027-01-29', '2027-02-12', '2027-02-26', '2027-03-12', '2027-04-09', '2027-04-23', '2027-08-06'},
    'CON': {'2027-10-08'},
}
NOT_CHECKED = {'2027-05-14': 'The check did not finish. Run it again on this version.'}

def active(code, day):
    f = next(r['from'] for r in ROLES if r['code'] == code)
    return f is None or day >= D(f)

def rule_values(day, i):
    j = lambda n: R.randint(-n, n)
    lvl = lerp([('2027-01-08', 97.2), ('2027-04-09', 96.8), ('2027-04-16', 94.1), ('2027-05-21', 94.6), ('2027-05-28', 96.9), ('2027-09-24', 97.4), ('2027-10-01', 95.6), ('2027-11-26', 97.1)], day)
    return {
        'on-level': round(lvl + R.uniform(-.1, .1), 1),
        'clashes': max(0, round(lerp([('2027-01-08', 0), ('2027-03-05', 0), ('2027-03-26', 64), ('2027-05-07', 118), ('2027-06-25', 41), ('2027-08-06', 58), ('2027-09-10', 12), ('2027-10-08', 37), ('2027-11-26', 9)], day)) + (j(3) if day > D('2027-03-12') else 0)),
        'warnings': round(lerp([('2027-01-08', 610), ('2027-03-26', 980), ('2027-06-04', 1420), ('2027-06-25', 1890), ('2027-08-13', 1760), ('2027-09-10', 1310), ('2027-11-26', 1180)], day)) + j(12),
        'dup-mark': max(0, round(lerp([('2027-01-08', 3), ('2027-02-26', 3), ('2027-03-05', 0), ('2027-05-28', 0), ('2027-06-04', 22), ('2027-06-25', 31), ('2027-07-16', 6), ('2027-08-06', 0), ('2027-10-01', 0), ('2027-10-08', 14), ('2027-11-05', 2), ('2027-11-26', 0)], day))),
        'worksets-exist': 2 if day < D('2027-02-05') else 0,
        'dup-levels': 1 if D('2027-04-16') <= day <= D('2027-04-23') else 0,
    }

versions, prev, n = [], None, 0
state = {r['code']: None for r in ROLES}     # the model each role last uploaded: {elements, checks:{id:[required, met]}}
for day in weeks:
    iso = day.isoformat()
    if in_holiday(day):
        continue
    n += 1
    ms = next((m for m in MILESTONES if day <= D(m['date'])), MILESTONES[-1])
    uploaded, fixed, broken, added, newq, newm = [], {}, {}, {}, {}, {}
    for r in ROLES:
        c = r['code']
        if not active(c, day): continue
        if iso in NO_UPLOAD.get(c, ()) and state[c] is not None:
            fixed[c] = broken[c] = added[c] = newq[c] = newm[c] = 0
            st = state[c]
            if st['ms'] != ms['id']:   # same model, but the plan now asks for more values
                g = GROWTH[ms['id']] / GROWTH[st['ms']]
                st['checks'] = {i: [round(q * g), m] for i, (q, m) in st['checks'].items()}
                st['ms'] = ms['id']
            continue
        uploaded.append(c)
        el = round(lerp(ELEMS[c], day) + R.uniform(-.004, .004) * lerp(ELEMS[c], day))
        lvl = lerp(LEVEL[c], day)
        checks = {}
        for vc in VALUE_CHECKS:
            k = vc['k'].get(c)
            if not k: continue
            req = max(1, round(el * k * GROWTH[ms['id']]))
            f = min(.995, max(0, lvl * vc['lag'].get(c, 1) + R.uniform(-.006, .006)))
            checks[vc['id']] = [req, round(req * f)]
        old = state[c]
        met_new = sum(v[1] for v in checks.values()); met_old = sum(v[1] for v in old['checks'].values()) if old else 0
        b = 0 if not old else round(met_old * R.uniform(.002, .012))
        fixed[c] = max(0, met_new - met_old + b) if old else 0
        broken[c] = b if old else 0
        added[c] = el - old['elements'] if old else el
        req_all = sum(x[0] for x in checks.values())
        newq[c] = round(max(0, added[c]) * req_all / el) if old else 0
        newm[c] = min(fixed[c], round(newq[c] * min(.99, CLEAN[c] + R.uniform(-.06, .06))))
        wrong = round((req_all - met_new) * min(.95, WRONG[c] + R.uniform(-.03, .03)))
        state[c] = {'elements': el, 'checks': checks, 'ms': ms['id'], 'wrong': wrong}
    v = {
        'order': n, 'label': f'Version {n}', 'tag': f'v{n}', 'created': iso + 'T16:00:00-05:00',
        'milestone': ms['id'], 'uploadedBy': uploaded,
        'state': 'notChecked' if iso in NOT_CHECKED else 'checked',
    }
    if iso in NOT_CHECKED:
        v['reason'] = NOT_CHECKED[iso]
    else:
        v['elementCount'] = sum(s['elements'] for s in state.values() if s)
        v['byRole'] = {c: {'elements': s['elements'], 'required': sum(x[0] for x in s['checks'].values()), 'met': sum(x[1] for x in s['checks'].values()),
                           'wrong': s['wrong'],
                           'uploaded': c in uploaded, 'elementsAdded': added[c], 'valuesFixed': fixed[c], 'valuesBroken': broken[c],
                           'newRequired': newq[c], 'newMet': newm[c]}
                       for c, s in state.items() if s}
        v['valueChecks'] = {vc['id']: {c: s['checks'][vc['id']] for c, s in state.items() if s and vc['id'] in s['checks']} for vc in VALUE_CHECKS}
        rv = rule_values(day, n)
        v['rules'] = {}
        for ru in RULES:
            val = rv[ru['id']]
            v['rules'][ru['id']] = {'value': val, 'pass': val >= ru['limit'] if ru['direction'] == 'higherIsBetter' else val <= ru['limit']}
    versions.append(v)

doc = {
    'format': 'openbep-compliance-series', 'formatVersion': '0.2', 'example': True,
    'note': 'SIMULATED. Invented numbers for a made-up school project. No real model, firm or VIM Server was measured. Written by compliance/build_example.py.',
    'plan': {'name': 'Example: Primary school renovation and extension', 'owner': 'School service centre (example)', 'delivery': 'Design-bid-build',
             'goals': GOALS, 'milestones': MILESTONES, 'holidays': HOLIDAYS, 'roles': ROLES},
    'model': {'server': 'VIM Server (simulated)', 'project': 'Primary school (example)', 'source': 'SCH-FEDERATED.vim',
              'schedule': 'One version every Friday, built from the Revit models each firm uploaded that week.'},
    'valueChecks': [{k: v for k, v in vc.items() if k in ('id', 'goal', 'label')} for vc in VALUE_CHECKS],
    'rules': RULES,
    'versions': versions,
}
text = json.dumps(doc, indent=1, ensure_ascii=False) + '\n'
if '--check' in sys.argv:
    if not os.path.exists(OUT) or open(OUT, encoding='utf-8').read() != text:
        sys.exit('compliance/example-school.json is out of date: run python3 compliance/build_example.py')
    print('compliance/example-school.json is up to date')
else:
    open(OUT, 'w', encoding='utf-8').write(text)
    print(f'wrote {OUT}: {len(versions)} versions')

"""Check a Revit IFC export against the BEP Builder's IDS and say where each OpenBEP parameter landed.

Usage: python check_ifc.py model.ifc plan.ids [report.html]
Needs: pip install ifcopenshell ifctester
"""
import sys, collections
import ifcopenshell, ifcopenshell.util.element as UE
from ifctester import ids, reporter

ifc_path, ids_path = sys.argv[1], sys.argv[2]
model = ifcopenshell.open(ifc_path)
spec = ids.open(ids_path)
spec.validate(model)

print(f'IFC {model.schema}: {ifc_path}')
print('\n== IDS results (specifications that apply to something in the model)')
for s in spec.specifications:
    if not s.applicable_entities: continue
    failed = [r for r in s.requirements if r.failures]
    print(f'{"PASS" if s.status else "FAIL"}  {s.name}: {len(s.applicable_entities)} objects, '
          f'{len(s.requirements) - len(failed)}/{len(s.requirements)} requirements met')
    for r in failed[:6]:
        print(f'      missing: {r.to_string("requirement")[:140]}')
    if len(failed) > 6: print(f'      ... {len(failed) - 6} more')

print('\n== Where the OpenBEP parameters landed (OpenBEP_ families only)')
where = collections.defaultdict(set)   # pset -> property names
classes = collections.Counter()
for e in model.by_type('IfcElement'):
    t = UE.get_type(e)
    name = (t.Name if t else '') or ''
    fam = e.ObjectType or ''
    if 'OpenBEP_' not in fam + name + (e.Name or ''): continue
    classes[f'{e.is_a()}/{getattr(e, "PredefinedType", None) or "-"}'] += 1
    for ps, props in UE.get_psets(e).items():
        for k, v in props.items():
            if k == 'id': continue
            if ps == 'OpenBEP' or k.startswith('OB_') or (ps.startswith('Pset_') and isinstance(v, str) and v.startswith('test OB_')):
                where[ps].add(k)
print('IFC classes:', ', '.join(f'{k} x{v}' for k, v in sorted(classes.items())))
for ps in sorted(where):
    print(f'{ps}: {len(where[ps])} properties: {", ".join(sorted(where[ps])[:12])}{" ..." if len(where[ps]) > 12 else ""}')

if len(sys.argv) > 3:
    r = reporter.Html(spec); r.report(); r.to_file(sys.argv[3])
    print('\nHTML report:', sys.argv[3])

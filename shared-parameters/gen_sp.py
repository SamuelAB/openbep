import json,uuid,re,csv,collections,unicodedata,sys
LB=json.load(open('lb_all.json')); P=LB['P']; C=LB['C']; USES=LB['uses']
VER='0.1'
DISC={'01 Identity and asset':[], '02 Architecture':'door wall_interior wall_exterior curtain_wall window ceiling stair ramp railing roof furniture_casework specialty_equipment signage space zone elevator'.split(),
 '03 Structure':'footing pile retaining_wall slab beam structural_member structural_column rebar'.split(),
 '04 Mechanical':'air_terminal ahu duct damper boiler chiller cooling_tower fan vav_box heat_exchanger'.split(),
 '05 Plumbing and fire protection':'plumbing_fixture pipe valve pump water_heater sprinkler'.split(),
 '06 Electrical and communications':'light_fixture fire_alarm_device distribution_board transformer generator cable_carrier wiring_device comm_security_device'.split(),
 '07 Site and civil':'site_terrain paving_parking utility_pipe utility_structure road_pavement road_barrier'.split(),
 '08 Rail, bridges and tunnels':'rail_track rail_turnout rail_signal ocs_support rail_platform bridge_superstructure bridge_substructure bridge_bearing tunnel_lining'.split()}
E2G={e:g for g,es in DISC.items() for e in es}
missing=[c['id'] for c in C if c['id'] not in E2G]; assert not missing,missing
def ascii_(s): return unicodedata.normalize('NFKD',s or '').encode('ascii','ignore').decode()
LEN={'IfcPositiveLengthMeasure','IfcLengthMeasure','IfcNonNegativeLengthMeasure'}
def dtype(p):
    t=p['t']
    if t=='IfcBoolean': return 'YESNO'
    if t=='IfcInteger': return 'INTEGER'
    if t in LEN and p.get('u') in ('mm','m'): return 'LENGTH'
    if t=='IfcAreaMeasure': return 'AREA'
    if t=='IfcVolumeMeasure' and p.get('u')=='m³': return 'VOLUME'
    if t=='IfcText': return 'MULTILINETEXT'
    if t.startswith('Ifc') and ('Measure' in t or t in ('IfcReal',)) : return 'NUMBER'
    return 'TEXT'
UNIT={'L/s':'Ls','kW':'kW','%':'pct','kPa':'kPa','°C':'C','V':'V','km/h':'kmh','kg':'kg','W/(m²·K)':'Wm2K','Pa':'Pa','W':'W','kN':'kN','rpm':'rpm','L':'L','CAD':'CAD','lm':'lm','K':'K','kg/m³':'kgm3','lx':'lx','A':'A','kA':'kA','m²·K/W':'m2KW','kN/m':'kNm','L/(s·m²)':'Lsm2','L/h':'Lh','cd':'cd','kVA':'kVA','h':'h','m/s':'ms','t':'t','m/km':'mkm','m³':'m3','mm':'mm','m':'m'}
def pascal(s):
    s=ascii_(s); s=re.sub(r'\(.*?\)','',s); w=re.findall(r'[A-Za-z0-9]+',s)
    return ''.join(x[:1].upper()+x[1:] for x in w)
# usage per param
use=collections.defaultdict(list)
for c in C:
    for pid,lod,us,note in c.get('r',[]): use[pid].append((c,lod,us))
# canonical merge: same normalized label + datatype + unit
def norm(p): return re.sub(r'[^a-z0-9]+',' ',p['l'].lower()).strip()
canon={}; members=collections.defaultdict(list)
ALIAS={'door_acoustic_rating':'wall_acoustic_rating','pf_mounting':'lf_mounting_type','fan_mounting':'lf_mounting_type','sig_mounting':'lf_mounting_type','pnl_mounting':'lf_mounting_type','at_mounting_type':'lf_mounting_type','pf_color':'at_finish_color','fur_finish':'at_finish_color'}
for k,p in P.items():
    q=P[ALIAS.get(k,k)]
    key=(norm(q),dtype(q),q.get('u'))
    members[key].append(k)
rows=[]; names=set()
for key,ks in members.items():
    ck=next((ALIAS[k] for k in ks if k in ALIAS),sorted(ks)[0]); p=P[ck]; dt=dtype(p)
    blt=[k for k in ks if P[k].get('rv') and P[k]['rv'][0]=='builtin']
    shared_needed=any(not(P[k].get('rv') and P[k]['rv'][0]=='builtin') for k in ks)
    els=[(c,lod,us,k) for k in ks for (c,lod,us) in use[k]]
    grps=collections.Counter(E2G[c['id']] for c,_,_,_ in els)
    grp='01 Identity and asset' if len(grps)>=3 or not grps else grps.most_common(1)[0][0]
    nm='CA_'+pascal(p['l'])
    if dt=='NUMBER' and p.get('u') in UNIT: nm+='_'+UNIT[p['u']]
    if shared_needed:
        if nm in names: nm='CA_'+''.join(x[:1].upper()+x[1:] for x in re.findall(r'[A-Za-z0-9]+',ascii_(p['l'])))
        if nm in names: print('COLLIDE',nm,ks); nm+='_'+ck
        assert nm not in names, nm
        names.add(nm)
    else: nm='; '.join(sorted({P[k]['rv'][1] for k in blt if P[k]['rv'][1]}))
    # GUIDs are fixed forever: the namespace keeps the project's first working name on purpose.
    gid=str(uuid.uuid5(uuid.NAMESPACE_URL,'urn:canbim:parameter:'+nm))
    cats=sorted({rc.split(' (')[0] for c,_,_,_ in els for rc in c.get('rc',[])})
    rows.append(dict(id=ck,merged=';'.join(sorted(ks)),name=nm,guid=gid,dt=dt,unit=p.get('u') or '',scope=p['sc'],group=grp,
      builtin='; '.join(sorted({P[k]['rv'][1] for k in blt if P[k]['rv'][1]})),shared='yes' if shared_needed else 'no',
      cats=cats,els=els,p=p))
rows.sort(key=lambda r:(r['group'],r['name']))
G=sorted({r['group'] for r in rows}); GID={g:i+1 for i,g in enumerate(G)}
# Revit shared parameter file (shared ones only)
L=['# This is a Revit shared parameter file.','# Do not edit manually.','*META\tVERSION\tMINVERSION','META\t2\t1','*GROUP\tID\tNAME']
L+=[f'GROUP\t{GID[g]}\tOpenBEP {g}' for g in G]
L.append('*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE')
ns=0
for r in rows:
    if r['shared']!='yes': continue
    p=r['p']; d=ascii_(f"{p['l']}{' ('+r['unit']+')' if r['unit'] else ''}. {p.get('d','')}").replace('\t',' ').replace('\n',' ')[:250]
    L.append(f"PARAM\t{r['guid']}\t{r['name']}\t{r['dt']}\t\t{GID[r['group']]}\t1\t{d}\t1"); ns+=1
open('OpenBEP_SharedParameters_v'+VER+'.txt','w',encoding='ascii',newline='').write('\r\n'.join(L)+'\r\n')
# manifest: one row per parameter x element
f=open('OpenBEP_Parameters_Manifest_v'+VER+'.csv','w',encoding='utf-8-sig',newline='')
w=csv.writer(f); w.writerow(['Parameter (Revit name)','GUID','Shared or built-in','Revit built-in name','Data type','Unit','Instance or type','Group','Label (EN)','Label (FR)','Element type','Revit categories','Required from LOD','BIM uses that need it','IFC target','Example value','Placeholder values the checker rejects','Description','Tool keys merged'])
for r in rows:
    p=r['p']
    for c,lod,us,k in (r['els'] or [(None,'','',r['id'])]):
        rv=P[k].get('rv') or ['shared',None]; isb=rv[0]=='builtin'
        w.writerow([rv[1] if isb else r['name'],'' if isb else r['guid'], 'Built-in' if isb else 'Shared',rv[1] if isb else '',r['dt'],r['unit'],r['scope'],r['group'],p['l'],p.get('lf',''),
          c['n'] if c else '','; '.join(rc.split(' (')[0] for rc in c.get('rc',[])) if c else '',lod,'; '.join(USES.get(u,u) for u in us) if us else '',p.get('ifc') or '',p.get('ex') or '','; '.join(x for x in p.get('sus',[]) if x),p.get('d',''),r['merged']])
f.close()
# app map
json.dump({r['id']:[r['name'],r['guid'],r['dt'],r['group'],r['shared']] for r in rows for _ in [0]} | {k:[r['name'],r['guid'],r['dt'],r['group'],r['shared']] for r in rows for k in r['merged'].split(';')},open('sp_map.json','w'))
print('canonical',len(rows),'shared in file',ns,'built-in only',sum(r['shared']=='no' for r in rows),'merged groups',sum(1 for r in rows if ';' in r['merged']))
print(collections.Counter(r['dt'] for r in rows if r['shared']=='yes'))
print(collections.Counter(r['group'] for r in rows))
for r in rows:
    if ';' in r['merged']: print(r['name'],r['merged'],r['shared'],r['builtin'],file=sys.stderr)
SP={'v':VER,'g':G,'el':{c['id']:E2G[c['id']][:2] for c in C},'p':{k:[r['name'],r['guid'],r['dt'],GID[r['group']],1 if r['shared']=='yes' else 0] for r in rows for k in r['merged'].split(';')}}
open('sp_embed.js','w').write('const SP='+json.dumps(SP,separators=(',',':'),ensure_ascii=False)+';\n')

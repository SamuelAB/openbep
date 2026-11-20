// Proves the requirement file (JSON) is enough on its own: a checker that reads only that file gets the same result
// as the BEP Builder's own "Check a model now" on a model table.
// Opens the Builder on the school example, downloads the requirement file, then for every milestone compares
//   (a) the Builder's own table check (ckRunTable) on revit-addin/OpenBepSync/test/reqfile-parity-model.csv, and
//   (b) the small evaluator below, which reads only the JSON file and the same table.
// Per element type: element count, score, pass, and each requirement's present % and real-value % must be equal.
// The same is done again after adding an owner parameter to Doors, which must appear in the file as an owner addition.
// Walls: the table has Function, Family and Structural Usage columns. Wall rows are placed by revitWhere (exterior, foundation,
// retaining, core-shaft, curtain, stacked); soffit, blank-Function and in-place walls are "not covered". The comparison runs three times
// per milestone: the full table, without the Family column, and without Function and Family (the old path).
// Usage: node translations/tools/reqfile_parity.js [--break] [--write] [--no-function-column [--head <older bep-builder.html>]]
//   --break  removes one Doors requirement from the JSON in memory; the test must then FAIL (negative control).
//   --break-rule  turns the fire-protection minutes rule in the JSON back into "any number" (no allowed list); the test must then FAIL.
// Fire and acoustic ratings are whole numbers: valueRule.allowed (fire minutes, 0 = no rating required) or min/max (STC 20 to 80).
// Doors carry 60, 0, 50, 1HR and a blank in both; each value is also checked alone, Builder against file, with hand-set answers.
// Optional parameters (joints, fire exposure side) are off in the school example and enter the file only once switched on.
//   --write  also writes revit-addin/OpenBepSync/test/OpenBEP-requirements-school-example.json (the school example as loaded).
//   --no-function-column  only the old path: the table without Function and Family columns must give exactly the result saved in
//            revit-addin/OpenBepSync/test/reqfile-parity-fallback-expected.json (made from the Builder before wall kinds, v0.44),
//            and the same as the file. --head <path> runs that older Builder live instead; with --write it saves its result.
//            --write alone refreshes the saved result from this Builder (after a legitimate change to the requirements).
// Structural walls (format 1.2, requirementGroups): a wall whose Structural Usage is Bearing, Shear or Structural combined also meets the
// group's rows, computed over those walls only; a Non-bearing wall is never counted in them. Also run without the Structural Usage column
// (the group is not checked, one note) and with only a Structural column (the fallback).
//   --break-group  removes requirementGroups from the JSON in memory; the test must then FAIL.
// Needs Playwright + Chromium, like crawl_app.js. CHROMIUM=/path/to/chrome uses a browser already installed;
// PW_CHANNEL=msedge uses the installed Microsoft Edge.
let pw; try { pw = require('playwright'); } catch (e) { pw = require('playwright-core'); }
const { pathToFileURL } = require('node:url');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert');

const root = path.resolve(__dirname, '..', '..');
const testDir = path.join(root, 'revit-addin', 'OpenBepSync', 'test');
const BREAK_GROUP = process.argv.includes('--break-group');
const BREAK = process.argv.includes('--break'), BREAK_RULE = process.argv.includes('--break-rule'), WRITE = process.argv.includes('--write'), NOFN = process.argv.includes('--no-function-column');
const HEAD = process.argv.includes('--head') ? process.argv[process.argv.indexOf('--head') + 1] : null;
const OWNER = 'Access control zone';

// ---- The evaluator: reads only the requirement file and the model table. ----
// A plain CSV reader (comma, double quotes, cells trimmed); the header is the first row with two filled cells.
function readTable(txt) {
  const rows = [];
  for (const line of txt.replace(/^﻿/, '').split(/\r?\n/)) {
    const out = []; let cur = '', q = false;
    for (let i = 0; i < line.length; i++) {
      const c = line[i];
      if (q) { if (c === '"') { if (line[i + 1] === '"') { cur += '"'; i++; } else q = false; } else cur += c; }
      else if (c === '"') q = true; else if (c === ',') { out.push(cur); cur = ''; } else cur += c;
    }
    out.push(cur); rows.push(out.map(x => x.trim()));
  }
  const filled = rows.filter(r => r.some(Boolean)), h = filled.findIndex(r => r.filter(Boolean).length >= 2);
  return { head: filled[h], rows: filled.slice(h + 1).filter(r => r.length >= 2) };
}
// tableRule.normalize in the file.
const norm = x => String(x || '').toLowerCase().replace(/[\s_\-]/g, '').replace(/\(.*$/, '');
// A copy of a table without some columns (by normalized header).
const dropCols = (T, names) => { const keep = T.head.map((h, i) => names.includes(norm(h)) ? -1 : i).filter(i => i >= 0);
  return { head: keep.map(i => T.head[i]), rows: T.rows.map(r => keep.map(i => r[i] ?? '')) }; };
const toCsv = T => [T.head, ...T.rows].map(r => r.map(c => /[",]/.test(c) ? '"' + c.replace(/"/g, '""') + '"' : c).join(',')).join('\n') + '\n';
// One revitWhere condition on one table row: true, false, or null when the table has no column for it (tableRule.split).
function holds(sp, T, q, r) {
  const col = names => T.head.findIndex(h => (names || []).includes(norm(h)));
  if (q.wallKind) { const i = col(sp.columns.wallKind); if (i < 0) return null; const v = norm(r[i]);
    return [].concat(q.wallKind).includes(sp.wallKindValues[v] || (v ? sp.wallKindOther : '')); }
  const i = col(sp.columns[q.parameter]); if (i < 0) return null; const v = norm(r[i]);
  return v !== '' && q.in.some(x => norm(x) === v);
}
function evaluate(F, T, mid) {
  const ms = F.milestones.find(m => m.id === mid), always = new RegExp(F.placeholders.always, F.placeholders.flags);
  const ci = T.head.findIndex(h => norm(h) === F.tableRule.categoryColumn);
  assert.ok(ci >= 0, 'the model table needs a Category column');
  const typeOf = v => { const n = norm(v); return F.elementTypes.find(t => t.categoryNames.includes(n)); };
  // Rows of a split category (Walls) are placed by revitWhere when the table has the trigger column (Function).
  const sp = (F.tableRule.split || [])[0], on = !!sp && T.head.some(h => sp.columns.Function.includes(norm(h)));
  const all = (w, r) => { const h = w.all.map(q => holds(sp, T, q, r)).filter(x => x !== null); return h.length > 0 && h.every(Boolean); };
  const place = r => F.elementTypes.find(t => t.revitWhere && t.revitWhere.all && t.revitWhere.revitCategory === sp.revitCategory && all(t.revitWhere, r));
  const walls = { n: 0, split: on, kind: on && T.head.some(h => sp.columns.wallKind.includes(norm(h))), out: 0 }, why = {};
  const by = new Map();
  T.rows.forEach(r => { let t = typeOf(r[ci]);
    if (t && sp && sp.cells.includes(norm(r[ci]))) { walls.n++;
      if (on) { t = place(r); if (!t) { walls.out++; const u = (F.uncovered || []).find(u => u.revitCategory === sp.revitCategory && all(u.where, r)); const k = u ? u.note : 'matches no element type'; why[k] = (why[k] || 0) + 1; return; } } }
    if (!t) return; if (!by.has(t.id)) by.set(t.id, { t, els: [] }); by.get(t.id).els.push(r); });
  const real = (q, v) => {
    if (v == null) return false; const s = v.trim();
    if (s === '' || q.placeholders.map(x => String(x).toLowerCase()).includes(s.toLowerCase()) || always.test(s)) return false;
    const w = q.valueRule, byPattern = new RegExp(w.pattern, 'i').test(s);
    if (!w.allowed && w.min == null && w.max == null) return byPattern;
    // A whole number in the allowed list, or within min and max. The pattern alone must give the same answer.
    const byRule = /^(0|[1-9]\d*)$/.test(s) && (w.allowed ? w.allowed.includes(+s) : (w.min == null || +s >= w.min) && (w.max == null || +s <= w.max));
    assert.strictEqual(byPattern, byRule, `${q.id}: valueRule.pattern and allowed/min/max disagree on "${s}"`);
    return byRule;
  };
  // tableRule.groups: the group is checked only when the table has its where column; the fallback column when no placed row has a value.
  const grpSub = new Map((F.requirementGroups || []).map(g => {
    const els = [...by.values()].filter(x => g.appliesTo.includes(x.t.id)).flatMap(x => x.els), col = q => T.head.findIndex(h => norm(h) === norm(q.parameter));
    const m = g.where.all.map(q => ({ q, i: col(q) })); if (m.some(x => x.i < 0)) return [g.id, null];
    const fb = g.where.fallback && g.where.fallback.all.map(q => ({ q, i: col(q) })), use = !els.some(r => m.some(x => r[x.i])) && fb && fb.every(x => x.i >= 0) ? fb : m;
    return [g.id, new Set(els.filter(r => use.every(x => x.q.in.some(v => norm(v) === norm(r[x.i])))))]; }));
  const cats = [...by.values()].map(({ t, els }) => {
    const reqs = new Map(t.requirements.map(q => [q.id, q]));
    const rows = t.requiredAt[mid].map(id => {
      const q = reqs.get(id), col = T.head.findIndex(h => q.columnNames.includes(norm(h)));
      let has = 0, ok = 0;
      els.forEach(r => { const v = col >= 0 ? (r[col] || '') : null; if (col >= 0) has++; if (real(q, v)) ok++; });
      return { n: q.label, has: Math.round(100 * has / els.length), ok: Math.round(100 * ok / els.length) };
    });
    // Requirement groups (scoreRule.groups): extra rows over the elements the group's where picks; none picked = rows left out.
    for (const g of F.requirementGroups || []) {
      if (!g.appliesTo.includes(t.id)) continue; const ids = g.requiredAt[t.id][mid], sub = grpSub.get(g.id); if (!ids.length || !sub) continue;
      const se = els.filter(r => sub.has(r)); if (!se.length) continue; const greqs = new Map(g.requirements.map(q => [q.id, q]));
      for (const id of ids) { const q = greqs.get(id), col = T.head.findIndex(h => q.columnNames.includes(norm(h))); let has = 0, ok = 0;
        se.forEach(r => { const v = col >= 0 ? (r[col] || '') : null; if (col >= 0) has++; if (real(q, v)) ok++; });
        rows.push({ n: q.label, grp: g.id, of: se.length, has: Math.round(100 * has / se.length), ok: Math.round(100 * ok / se.length) }); }
    }
    const fill = rows.length ? Math.round(rows.reduce((s, r) => s + r.ok, 0) / rows.length) : 100;
    return { id: t.id, lod: t.lod[mid], n: els.length, fill, pass: fill >= ms.targetPercent, rows };
  });
  return { cats, walls, why };
}

// ---- The run. ----
(async () => {
  const launch = process.env.CHROMIUM ? { executablePath: process.env.CHROMIUM } : process.env.PW_CHANNEL ? { channel: process.env.PW_CHANNEL } : {};
  const browser = await pw.chromium.launch({ headless: true, ...launch });
  const page = await (await browser.newContext({ acceptDownloads: true })).newPage();
  const errors = [];
  page.on('pageerror', e => errors.push(String(e)));
  page.on('dialog', d => d.dismiss());
  let failed = 0;
  try {
    await page.goto(pathToFileURL(path.join(root, 'app', 'bep-builder.html')).href);
    await page.evaluate(() => localStorage.clear()); await page.reload();
    await page.locator('[data-ex="school"]').first().click(); await page.waitForTimeout(200);
    const openAll = async () => { await page.evaluate(() => { document.querySelectorAll('details').forEach(d => { d.open = true }); document.querySelectorAll('[data-fall="1"]').forEach(b => b.click()); }); await page.waitForTimeout(150); };
    const exportFile = async () => {
      await page.locator('#steps [data-step="6"]').click(); await page.waitForTimeout(150); await openAll();
      const [dl] = await Promise.all([page.waitForEvent('download'), page.locator('#reqBtn').click()]);
      assert.match(dl.suggestedFilename(), /^OpenBEP-requirements-.+\.json$/);
      return JSON.parse(fs.readFileSync(await dl.path(), 'utf8'));
    };
    const table = fs.readFileSync(path.join(testDir, 'reqfile-parity-model.csv'), 'utf8');
    const T = readTable(table);

    const school = await exportFile();
    assert.strictEqual(school.format, 'openbep-requirements');
    assert.strictEqual(school.formatVersion, '1.2');
    const sortId = a => [...a].sort((x, y) => x.id.localeCompare(y.id));
    const same = (a, b, where) => { assert.deepStrictEqual(b.map(x => x.id), a.map(x => x.id), where + ': element types found');
      a.forEach((x, i) => { const y = b[i], w = `${where}, ${x.id}`; assert.strictEqual(y.lod, x.lod, w + ': level'); assert.strictEqual(y.n, x.n, w + ': elements');
        assert.deepStrictEqual(y.rows, x.rows, w + ': requirement rows'); assert.strictEqual(y.fill, x.fill, w + ': score'); assert.strictEqual(y.pass, x.pass, w + ': pass'); }); };

    // --no-function-column: the old path. Without a Function column the Builder must give exactly what the previous Builder gave.
    if (NOFN) {
      const T0 = dropCols(T, ['function', 'family']), csv0 = toCsv(T0), exp = path.join(testDir, 'reqfile-parity-fallback-expected.json');
      let expected;
      if (HEAD) { // the previous Builder (a copy of app/bep-builder.html from before this change), on the same table
        const hp = await (await browser.newContext()).newPage(); hp.on('pageerror', e => errors.push('previous Builder: ' + e));
        await hp.goto(pathToFileURL(path.resolve(HEAD)).href); await hp.evaluate(() => localStorage.clear()); await hp.reload();
        await hp.locator('[data-ex="school"]').first().click(); await hp.waitForTimeout(200);
        expected = {}; for (const m of school.milestones) expected[m.id] = sortId(await hp.evaluate(([t, mid]) => window.__ckTable(t, mid), [csv0, m.id]));
        if (WRITE) { fs.writeFileSync(exp, JSON.stringify(expected, null, 1) + '\n'); console.log('wrote ' + path.relative(root, exp)); }
      } else if (WRITE) { // a legitimate change to the requirements (lod/spec.json, the school example): refresh from this Builder
        expected = {}; for (const m of school.milestones) expected[m.id] = sortId(await page.evaluate(([t, mid]) => window.__ckTable(t, mid), [csv0, m.id]));
        fs.writeFileSync(exp, JSON.stringify(expected, null, 1) + '\n'); console.log('wrote ' + path.relative(root, exp) + ' from this Builder');
      } else expected = JSON.parse(fs.readFileSync(exp, 'utf8'));
      let n = 0;
      for (const m of school.milestones) {
        const now = sortId(await page.evaluate(([t, mid]) => window.__ckTable(t, mid), [csv0, m.id]));
        assert.deepStrictEqual(now, expected[m.id], `${m.id}: the Builder without a Function column differs from the previous Builder`);
        const mine = evaluate(school, T0, m.id); same(now, sortId(mine.cats), `no Function column, ${m.id}`);
        const W = await page.evaluate(([t, mid]) => window.__ckWalls(t, mid), [csv0, m.id]);
        assert.deepStrictEqual(W.walls, mine.walls, m.id + ': wall counts');
        assert.ok(!W.walls.split && W.walls.n === 14 && W.walls.out === 0, m.id + ': walls ' + JSON.stringify(W.walls));
        assert.strictEqual(W.html.split('Walls could not be told apart without a Function column').length - 1, 1, m.id + ': the note must show once');
        assert.strictEqual(now.find(x => x.id === 'wall_interior').n, 16, m.id + ': every wall row goes to interior walls (14 Walls + 2 named rows)');
        n += now.length;
      }
      if (errors.length) throw new Error('Page errors: ' + errors.join(' | '));
      console.log(`NO-FUNCTION-COLUMN OK milestones=${school.milestones.length} types=${n / school.milestones.length}: same as the previous Builder${HEAD ? ' (run live)' : WRITE ? ' (only just refreshed from this Builder: not a comparison)' : ' (saved result)'} and as the file; the note shows once`);
      return;
    }
    if (WRITE) {
      const out = path.join(testDir, 'OpenBEP-requirements-school-example.json');
      fs.writeFileSync(out, JSON.stringify(school, null, 2) + '\n');
      console.log('wrote ' + path.relative(root, out));
    }

    // Owner addition on Doors (step "Element requirements", "Your own parameters").
    await page.locator('[data-lbsel="door"]').first().click(); await page.waitForTimeout(150); await openAll();
    await page.fill('#lbc-n', OWNER); await page.selectOption('#lbc-l', '300'); await page.fill('#lbc-d', 'Zone letter from the security plan');
    await page.locator('#lbAdd').click(); await page.waitForTimeout(150);
    const owned = await exportFile();
    const door = owned.elementTypes.find(t => t.id === 'door');
    const own = door.requirements.find(q => q.id === 'owner:' + OWNER);
    assert.ok(own, 'owner addition missing from the Doors requirements');
    assert.strictEqual(own.ownerAddition, true);
    assert.strictEqual(own.fromLod, '300');
    for (const m of owned.milestones) assert.strictEqual(door.requiredAt[m.id].includes(own.id), door.lod[m.id] !== '' && +door.lod[m.id] >= 300, `owner addition at ${m.id}`);
    assert.ok(['final50', 'final90', 'final100', 'handover'].every(m => door.requiredAt[m].includes(own.id)), 'owner addition not required at final design');
    console.log(`owner addition OK: "${OWNER}" on Doors, ownerAddition true, required at ${owned.milestones.filter(m => door.requiredAt[m.id].includes(own.id)).map(m => m.id).join(', ')}`);

    // Negative control: drop one Doors requirement that the table has a column for.
    if (BREAK) for (const F of [school, owned]) {
      const d = F.elementTypes.find(t => t.id === 'door'), gone = 'door_hardware_set';
      d.requirements = d.requirements.filter(q => q.id !== gone);
      Object.keys(d.requiredAt).forEach(m => { d.requiredAt[m] = d.requiredAt[m].filter(x => x !== gone); });
      console.log(`--break: removed ${gone} from Doors`);
    }
    // Negative control for the structural walls group: the file loses requirementGroups, the Builder still checks them.
    if (BREAK_GROUP) for (const F of [school, owned]) { delete F.requirementGroups; console.log('--break-group: removed requirementGroups'); }
    // Negative control for the whole-number rule: the exterior walls' fire minutes become "any number" in the file only.
    if (BREAK_RULE) for (const F of [school, owned]) {
      const q = F.elementTypes.find(t => t.id === 'wall_exterior').requirements.find(x => x.id === 'fire_resistance_min');
      q.valueRule = { ...q.valueRule, allowed: null, min: null, max: null, check: 'number', pattern: '\\d' };
      console.log('--break-rule: exterior walls fire_resistance_min accepts any number in the file');
    }

    // Parity, per milestone, for the school example and again with the owner addition.
    let types = 0, rowsCompared = 0, partial = 0, wallsChecked = 0;
    const su = T.head.findIndex(h => norm(h) === 'structuralusage'), isSt = v => ['bearing', 'shear', 'structuralcombined'].includes(norm(v));
    const TS = { head: [...T.head, 'Structural'], rows: T.rows.map(r => [...r.map((c, i) => i === su ? '' : c), isSt(r[su]) ? 'Yes' : (r[su] ? 'No' : '')]) };
    const variants = [['full table', T], ['no Family column', dropCols(T, ['family'])], ['no Function column', dropCols(T, ['function', 'family'])],
      ['no Structural Usage column', dropCols(T, ['structuralusage'])], ['Structural column only', TS]];
    for (const [label, F, withOwner] of [['with owner addition', owned, true], ['school example', school, false]]) {
      if (withOwner) { /* the Builder state now carries the owner addition, as in F */ }
      else { // put the Builder back to the plain school example for its own check
        await page.evaluate(() => localStorage.clear()); await page.reload();
        await page.locator('[data-ex="school"]').first().click(); await page.waitForTimeout(200);
      }
      for (const m of F.milestones) for (const [tl, TT] of variants) {
        const csv = toCsv(TT);
        const a = sortId(await page.evaluate(([t, mid]) => window.__ckTable(t, mid), [csv, m.id]));
        const mine = evaluate(F, TT, m.id), b = sortId(mine.cats);
        const W = await page.evaluate(([t, mid]) => window.__ckWalls(t, mid), [csv, m.id]);
        try {
          same(a, b, `${label}, ${tl}, ${m.id}`);
          assert.deepStrictEqual(W.walls, mine.walls, `${label}, ${tl}, ${m.id}: wall counts`);
          a.forEach(x => { rowsCompared += x.rows.length; partial += x.rows.filter(r => r.ok > 0 && r.ok < 100).length; });
        } catch (e) { failed++; console.error('MISMATCH ' + e.message.split('\n')[0] + ` (Builder: ${a.map(x => x.id)}; file: ${b.map(x => x.id)})`); if (failed > 5) throw new Error('too many mismatches'); continue; }
        if (withOwner && tl === 'full table') {
          types = a.length;
          if (m.id !== 'prelim100') assert.ok(a.find(x => x.id === 'door').rows.some(r => r.n === OWNER), 'Builder check has no owner row');
          // Hand-counted placement of the 14 Walls rows (see the CSV): interior 3002 (+2 named rows), exterior 3003 3004 3010 (stacked),
          // retaining 3001 3008, curtain 3007, core-shaft 3005 and 3012 3013 3014 -> interior; not covered: 3006 soffit, 3009 blank Function, 3011 in place.
          const nOf = id => (a.find(x => x.id === id) || {}).n;
          assert.deepStrictEqual([nOf('wall_interior'), nOf('wall_exterior'), nOf('retaining_wall'), nOf('curtain_wall')], [7, 3, 2, 1], m.id + ': walls placed');
          assert.deepStrictEqual(W.walls, { n: 14, split: true, kind: true, out: 3 }, m.id + ': wall counts');
          assert.deepStrictEqual(mine.why, { 'Soffit walls: no element type of this plan covers them.': 1, 'matches no element type': 1, 'Wall modelled in place; has no wall type Function.': 1 }, m.id + ': not covered');
          assert.ok(W.html.includes('Not covered by the plan: 3 walls') && !W.html.includes('without a Function column') && !W.html.includes('without a Family column'), m.id + ': wall notes');
          wallsChecked++;
        }
        if (withOwner && tl === 'no Family column' && m.id === 'final90') {
          // Without the wall kind, the curtain wall (Function Exterior) goes to exterior walls and in-place walls cannot be seen.
          const nOf = id => (a.find(x => x.id === id) || {}).n;
          assert.deepStrictEqual([nOf('wall_exterior'), nOf('curtain_wall')], [4, undefined], 'no Family column: placement');
          assert.ok(W.html.includes('Curtain walls could not be told apart without a Family column'), 'no Family column: note');
        }
      }
    }
    if (errors.length) { failed++; console.error('Page errors:\n' + errors.join('\n')); }
    if (failed) throw new Error(`${failed} milestone comparisons failed`);
    assert.ok(types >= 4 && rowsCompared > 100 && partial > 10, `test table too thin: ${types} types, ${rowsCompared} rows, ${partial} partly filled`);
    console.log(`${rowsCompared} requirement rows compared, ${partial} partly filled`);
    assert.strictEqual(wallsChecked, school.milestones.length, 'wall placement checked at every milestone');

    // The file's wall data (format 1.1).
    const ET = id => school.elementTypes.find(t => t.id === id);
    const nullIds = school.elementTypes.flatMap(t => t.revitCategories.filter(r => !r.id).map(r => t.id + ': ' + r.name));
    assert.deepStrictEqual(nullIds, [], 'revitCategories with no OST id');
    assert.deepStrictEqual(ET('wall_exterior').categoryNotes, ['Curtain walls are separate (B2020)'], 'the note is a categoryNote');
    const wallTypes = school.elementTypes.filter(t => t.revitCategories.some(r => r.id === 'OST_Walls'));
    const noRule = wallTypes.filter(t => !(t.revitWhere && t.revitWhere.revitCategory === 'OST_Walls'));
    for (const t of noRule) assert.ok(school.unresolvedSplits.some(u => u.id === t.id && /OST_Walls/.test(u.reason)), t.id + ': Walls type with no revitWhere and no stated reason');
    assert.ok(school.unresolvedSplits.every(u => u.id && Array.isArray(u.sharesCategoryWith) && u.reason), 'unresolvedSplits entries need id, sharesCategoryWith, reason');
    // No wall kind and Function value lands in two places (element types plus the uncovered list).
    const sp = school.tableRule.split[0], fns = school.wallKinds.function.values, wkinds = school.wallKinds.kind.values;
    const famOf = { Basic: 'Basic Wall', Curtain: 'Curtain Wall', Stacked: 'Stacked Wall', InPlace: 'Stair wrap' };
    const T1 = { head: ['Family', 'Function'], rows: [] };
    for (const k of wkinds) for (const f of [...fns, '']) {
      const r = [famOf[k], f], ok = w => w.all.every(q => holds(sp, T1, q, r));
      const hits = [...wallTypes.filter(t => t.revitWhere && ok(t.revitWhere)).map(t => t.id), ...school.uncovered.filter(u => ok(u.where)).map(u => 'uncovered: ' + u.note)];
      assert.ok(hits.length <= 1, `${k} wall with Function ${f || '(blank)'} lands in ${hits.join(' and ')}`);
    }
    console.log(`WALLS OK: ${wallTypes.length} element types in Walls (${wallTypes.filter(t => t.revitWhere).map(t => t.id).join(', ')} placed by revitWhere; ${noRule.map(t => t.id).join(', ') || 'none'} with a stated reason); no wall lands twice; no null category id; unresolvedSplits=${school.unresolvedSplits.length}`);

    // Owner ruling 2026-11-20: exterior walls carry a fire rating, in minutes. Walls (interior, exterior, curtain) ask it for design
    // review / authoring, a use every design-authoring project has, so the school example AS LOADED requires it from LOD 300.
    const ex = ET('wall_exterior'), fr = ex.requirements.find(q => q.id === 'fire_resistance_min');
    assert.ok(fr, 'exterior walls: no fire_resistance_min requirement in the school example as loaded');
    assert.strictEqual(fr.fromLod, '300'); assert.ok(fr.neededFor.includes('design_review'), 'fire minutes not needed for design review');
    assert.strictEqual(fr.revitParameter, 'OB_FireResistanceRating_min'); assert.strictEqual(fr.dataType, 'INTEGER');
    assert.deepStrictEqual(fr.valueRule.allowed, [0, 20, 30, 45, 60, 90, 120, 180, 240]); assert.ok(!fr.placeholders.includes('0'), '0 must not be a placeholder for fire minutes');
    for (const m of school.milestones) assert.strictEqual(ex.requiredAt[m.id].includes('fire_resistance_min'), +ex.lod[m.id] >= 300, 'fire_resistance_min at ' + m.id);
    for (const id of ['wall_interior', 'curtain_wall']) assert.ok(ET(id).requiredAt.final90.includes('fire_resistance_min'), id + ': no fire minutes at final90');
    assert.ok(!school.elementTypes.some(t => Object.values(t.requiredAt).some(a => a.includes('fire_rating'))), 'the built-in Fire Rating text is still required somewhere');
    const fb = (await page.evaluate(([t, mid]) => window.__ckTable(t, mid), [table, 'final90'])).find(x => x.id === 'wall_exterior');
    const fe = evaluate(school, T, 'final90').cats.find(x => x.id === 'wall_exterior');
    const frRow = fb.rows.find(r => r.n === fr.label);
    assert.deepStrictEqual(frRow, { n: fr.label, has: 100, ok: 67 }, 'Builder: exterior walls fire minutes row (120, 50, 120)');
    assert.deepStrictEqual(fe.rows.find(r => r.n === fr.label), frRow, 'file: exterior walls fire minutes row');
    console.log(`FIRE-RATING OK: the school example as loaded requires "${fr.label}" (${fr.revitParameter}) on exterior walls from LOD 300 (at ${school.milestones.filter(m => ex.requiredAt[m.id].includes('fire_resistance_min')).map(m => m.id).join(', ')}); the Builder and the file both score it ${frRow.ok}%`);
    // Each value alone, Builder against file: fire minutes on an exterior wall, STC on a door.
    const stc = ET('door').requirements.find(q => q.id === 'stc');
    assert.ok(stc && ET('door').requiredAt.final90.includes('stc'), 'doors: STC not required at final90');
    const expect = { '60': [100, 100], '0': [100, 0], '50': [0, 100], '1HR': [0, 0], '': [0, 0] };
    for (const [v, [wantFire, wantStc]] of Object.entries(expect)) {
      const TV = { head: ['Category', 'OB_FireResistanceRating_min', 'OB_STC'], rows: [['Exterior walls', v, ''], ['Doors', '', v]] }, csv = toCsv(TV);
      const a = await page.evaluate(([t, mid]) => window.__ckTable(t, mid), [csv, 'final90']), b = evaluate(school, TV, 'final90').cats;
      const got = (L, id, lbl) => (L.find(x => x.id === id).rows.find(r => r.n === lbl) || {}).ok;
      assert.deepStrictEqual([got(a, 'wall_exterior', fr.label), got(a, 'door', stc.label)], [wantFire, wantStc], `Builder on "${v}"`);
      assert.deepStrictEqual([got(b, 'wall_exterior', fr.label), got(b, 'door', stc.label)], [wantFire, wantStc], `file on "${v}"`);
    }
    const dr = evaluate(school, T, 'final90').cats.find(x => x.id === 'door').rows.find(r => r.n === stc.label);
    assert.deepStrictEqual(dr, { n: stc.label, has: 100, ok: 40 }, 'doors STC row on the table (60, 0, 50, 1HR, blank)');
    console.log('VALUE-RULES OK: 60, 0, 50, "1HR", blank: fire minutes 100/100/0/0/0, STC 100/0/100/0/0, Builder = file; doors STC on the table 40%');
    // Structural walls (owner ruling 2026-11-20): the group applies from LOD 300 (cover 350) to interior, exterior and foundation walls
    // whose Structural Usage is Bearing, Shear or Structural combined. Hand-computed on the table at final90 (Strength Class = grade):
    //   interior structural 3005 (Shear, 20 MPa), 3012 (Bearing, 35 MPa), 3013 (Shear, blank) -> grade 2 of 3 = 67%; 3002 3014 3101 3102 are Non-bearing.
    //   exterior structural 3003 (Bearing, blank), 3010 (Structural combined, blank) -> grade 0%; 3004 Non-bearing, the curtain wall is not an exterior wall.
    //   exposure class, reinforcement ratio, net volume: no column -> 0%. Retaining walls already ask all of them (no group rows).
    const G = school.requirementGroups.find(g => g.id === 'structural_walls');
    assert.ok(G && G.where.all[0].parameter === 'Structural Usage' && G.where.fallback, 'structural_walls group');
    assert.deepStrictEqual(G.appliesTo, ['wall_interior', 'wall_exterior', 'retaining_wall']);
    for (const q of G.requirements) assert.strictEqual(q.fromLod, q.id === 'col_cover' ? '350' : '300', q.id + ': from LOD');
    const gwant = ['col_material_grade', 'col_exposure_class', 'col_rebar_ratio', 'col_volume'];
    assert.deepStrictEqual(G.requiredAt.wall_interior.final90, gwant, 'interior walls group at final90');
    assert.deepStrictEqual(G.requiredAt.wall_interior.prelim100, [], 'nothing structural at LOD 200');
    assert.deepStrictEqual(G.requiredAt.retaining_wall.final90, [], 'retaining walls already ask the group parameters');
    const ckF = async (TT, mid) => sortId(await page.evaluate(([t, mid]) => window.__ckTable(t, mid), [toCsv(TT), mid]));
    const a90 = await ckF(T, 'final90'), f90 = sortId(evaluate(school, T, 'final90').cats), lbl = id => G.requirements.find(q => q.id === id).label;
    const grpRows = (L, id) => L.find(x => x.id === id).rows.filter(r => r.grp);
    const hand = (of, g) => gwant.map((id, i) => ({ n: lbl(id), grp: 'structural_walls', of, has: i === 0 ? 100 : 0, ok: i === 0 ? g : 0 }));
    assert.deepStrictEqual(grpRows(a90, 'wall_interior'), hand(3, 67), 'Builder: interior walls group rows');
    assert.deepStrictEqual(grpRows(a90, 'wall_exterior'), hand(2, 0), 'Builder: exterior walls group rows');
    assert.deepStrictEqual(grpRows(f90, 'wall_interior'), hand(3, 67), 'file: interior walls group rows');
    assert.deepStrictEqual(grpRows(a90, 'retaining_wall'), [], 'retaining walls: no group rows');
    for (const L of [a90, f90]) for (const x of L) assert.strictEqual(x.fill, Math.round(x.rows.reduce((s, r) => s + r.ok, 0) / x.rows.length), x.id + ': score = mean of all rows');
    // A Non-bearing wall is never in the group: giving 3002 and 3014 a grade, or taking 3014 away, changes no group row.
    const gi = T.head.findIndex(h => norm(h) === 'strengthclass'), TN = { head: T.head, rows: T.rows.map(r => ['3002', '3014'].includes(r[1]) ? r.map((c, i) => i === gi ? '35 MPa' : c) : r) };
    assert.deepStrictEqual(await ckF(TN, 'final90'), a90, 'a Non-bearing wall with a grade changed the result');
    const TX = { head: T.head, rows: T.rows.filter(r => r[1] !== '3014') }, ax = await ckF(TX, 'final90');
    assert.deepStrictEqual(grpRows(ax, 'wall_interior'), grpRows(a90, 'wall_interior'), 'the Non-bearing wall 3014 counted in the group');
    // Without the Structural Usage column: no group rows, one note; with only a Structural column: the same walls (fallback).
    const T0s = dropCols(T, ['structuralusage']), a0 = await ckF(T0s, 'final90'), W0 = await page.evaluate(([t, mid]) => window.__ckWalls(t, mid), [toCsv(T0s), 'final90']);
    assert.ok(a0.every(x => !x.rows.some(r => r.grp)), 'no Structural Usage column: group rows present');
    const NOTE = "Structural walls were checked only against the requirements of their wall type: the schedule has no Structural Usage column. Add the wall's Structural Usage to the schedule to check their extra requirements.";
    assert.strictEqual(W0.html.split(NOTE).length - 1, 1, 'no Structural Usage column: the note must show once');
    assert.ok(!(await page.evaluate(([t, mid]) => window.__ckWalls(t, mid), [table, 'final90'])).html.includes(NOTE), 'the note shows with the column');
    assert.deepStrictEqual(grpRows(await ckF(TS, 'final90'), 'wall_interior'), hand(3, 67), 'Structural column only (fallback): interior group rows');
    for (const L of ['fr', 'ja']) for (const t of [NOTE, G.label, G.statement])
      assert.ok(await page.evaluate(([t, L]) => window.__trText(t, L), [t, L]), `no ${L} translation: ${t}`);
    console.log(`STRUCTURAL OK: group ${G.id} on ${G.appliesTo.join(', ')}; final90 interior walls ${grpRows(a90, 'wall_interior').map(r => r.ok + '% of ' + r.of).join(' / ')}, exterior ${grpRows(a90, 'wall_exterior').map(r => r.ok + '% of ' + r.of).join(' / ')}; Builder = file = hand; Non-bearing walls not counted; no column -> not checked + note; Structural fallback same walls`);
    // Optional parameters: off in the school example; they enter the file only once the owner switches one on.
    const OPTS = ['fire_head_joint_min', 'fire_base_joint_min', 'fire_from_above_min', 'fire_from_below_min', 'fire_from_inside_min', 'fire_from_outside_min'];
    const inFile = F => F.elementTypes.flatMap(t => [...t.requirements.map(q => q.id), ...Object.values(t.requiredAt).flat()].filter(id => OPTS.includes(id)).map(id => t.id + ':' + id));
    assert.deepStrictEqual(inFile(school), [], 'optional parameters in the school example as loaded');
    await page.locator('#steps [data-step="6"]').click(); await page.waitForTimeout(150);
    await page.locator('[data-lbsel="wall_exterior"]').first().click(); await page.waitForTimeout(150); await openAll();
    const offered = await page.evaluate(() => [...document.querySelectorAll('#lbp-p optgroup')][0].querySelectorAll('option').length && [...[...document.querySelectorAll('#lbp-p optgroup')][0].querySelectorAll('option')].map(o => o.value));
    assert.ok(offered && ['fire_from_outside_min', 'fire_head_joint_min'].every(x => offered.includes(x)), 'optional parameters not offered under "Suggested for this element type": ' + offered);
    await page.selectOption('#lbp-p', 'fire_from_outside_min'); await page.selectOption('#lbp-f', '300'); await page.locator('#lbpAdd').click(); await page.waitForTimeout(150);
    const opted = await exportFile(), oe = opted.elementTypes.find(t => t.id === 'wall_exterior'), oq = oe.requirements.find(q => q.id === 'fire_from_outside_min');
    assert.deepStrictEqual(inFile(opted).filter((x, i, a) => a.indexOf(x) === i), ['wall_exterior:fire_from_outside_min'], 'only the one switched on');
    assert.ok(oq && oq.fromLod === '300' && oq.revitParameter === 'OB_FireRatingFromOutside_min' && oq.valueRule.allowed.length === 9, 'switched-on optional parameter: ' + JSON.stringify(oq));
    for (const m of opted.milestones) assert.strictEqual(oe.requiredAt[m.id].includes('fire_from_outside_min'), +oe.lod[m.id] >= 300, 'optional at ' + m.id);
    console.log(`OPTIONAL OK: none of ${OPTS.length} optional parameters in the school example as loaded; switched on "Fire-resistance rating, fire from outside (min)" on exterior walls -> in the file from LOD 300, alone`);
    for (const L of ['fr', 'ja']) for (const t of ['Walls could not be told apart without a Function column, so every wall was checked as an interior wall. Add the wall type\'s Function to the schedule to check exterior, foundation and curtain walls against their own requirements.',
      'Curtain walls could not be told apart without a Family column, so walls were placed by their Function alone.',
      'Not covered by the plan: 3 walls whose Function no element type of this plan covers (for example Soffit), is left empty, or that are modelled in place. They were not checked.'])
      assert.ok(await page.evaluate(([t, L]) => window.__trText(t, L), [t, L]), `no ${L} translation: ${t}`);
    console.log('wall notes: French and Japanese present');

    // Model rules (step "Information and software"): stated in the draft and carried in the file, nothing evaluated.
    const go = async i => { await page.locator(`#steps [data-step="${i}"]`).click(); await page.waitForTimeout(150); await openAll(); };
    const kinds = school.modelRules.map(r => r.kind);
    assert.ok(kinds.length >= 16 && school.modelRules.every(r => typeof r.on === 'boolean' && r.statement && ['critical', 'major', 'minor'].includes(r.severity)), 'modelRules entries');
    console.log(`model rules on in the school example as loaded: ${school.modelRules.filter(r => r.on).map(r => r.kind).join(', ') || 'none'}`);
    const draft = async () => { await go(13); return page.locator('#doc').innerText(); };
    // A rule the plan already fixes starts on: background models imported once -> one set of grids and one set of levels.
    await go(8); await page.locator('[data-info="bgOnce"]').check(); await page.waitForTimeout(150);
    let D = await exportFile();
    const onNow = D.modelRules.filter(r => r.on).map(r => r.kind).sort().join(',');
    assert.strictEqual(onNow, 'one-grid-set,one-level-set', 'rules on by default after ticking background models: ' + onNow);
    // Settings typed the way an owner types them: focus stays, the sentence follows, the file carries them, a reload keeps them.
    await go(8);
    const typeIn = async (q, text) => { const x = page.locator(q); await x.fill(''); await x.pressSequentially(text, { delay: 20 });
      const st = await page.evaluate(q => { const a = document.activeElement; return [a && a.matches(q), a && a.value]; }, q);
      assert.deepStrictEqual(st, [true, text], `typing into ${q} lost focus or text: ${JSON.stringify(st)}`); await x.press('Tab'); await page.waitForTimeout(150); };
    await typeIn('[data-mr="warnings-max"][data-k="p.total"]', '50');
    await typeIn('[data-mr="unique-value"][data-k="p.categories"]', 'Doors, Windows');
    const rowText = await page.locator('#mrSec').innerText();
    assert.ok(rowText.includes('The model has no more than 50 Revit warnings.'), 'the rule sentence did not follow the typed number');
    assert.ok(rowText.includes('In each listed category, no two elements share a value of the listed parameter.'), 'the rule sentence did not follow the typed categories');
    const typed = F => { const w = F.modelRules.find(r => r.kind === 'warnings-max'), u = F.modelRules.find(r => r.kind === 'unique-value');
      assert.strictEqual(w.params.total, 50); assert.strictEqual(w.statement, 'The model has no more than 50 Revit warnings.');
      assert.deepStrictEqual(u.params.categories, ['Doors', 'Windows']); assert.deepStrictEqual(u.listed, ['Mark', 'Doors', 'Windows']);
      assert.ok(u.statement.startsWith('In each listed category')); };
    typed(await exportFile());
    await page.reload(); await page.waitForTimeout(300);
    typed(await exportFile());
    console.log('model rule settings OK: typed 50 and "Doors, Windows" kept focus, changed the sentence, are in the file and survive a reload');
    await go(8);
    for (const k of kinds) { const b = page.locator(`[data-mr="${k}"][data-k="on"]`); if (!(await b.isChecked())) { await b.click(); await page.waitForTimeout(100); } }
    await page.selectOption('#mr-f-warnings-max', 'final90');
    let F = await exportFile(), text = await draft();
    assert.ok(F.modelRules.every(r => r.on), 'a rule switched on is not on in the file');
    assert.strictEqual(F.modelRules.find(r => r.kind === 'warnings-max').appliesFrom, 'final90');
    for (const r of F.modelRules) assert.ok(text.includes(r.statement), `draft lacks: ${r.statement}`);
    assert.ok(text.includes('Model rules'), 'draft has no Model rules section');
    for (const L of ['fr', 'ja']) for (const t of [...F.modelRules.map(r => r.statement), 'Every model delivered at a milestone follows these rules. They are also in the requirement file, for a model checking tool; OpenBEP does not check them.', 'Model rules'])
      assert.ok(await page.evaluate(([t, L]) => window.__trText(t, L), [t, L]), `no ${L} translation: ${t}`);
    await go(8); await page.locator('[data-mr="no-imported-cad"][data-k="on"]').click(); await page.waitForTimeout(100);
    F = await exportFile(); text = await draft();
    const cad = F.modelRules.find(r => r.kind === 'no-imported-cad');
    assert.strictEqual(cad.on, false, 'switched-off rule must be on:false');
    assert.ok(!text.includes(cad.statement), 'a switched-off rule is still in the draft');
    console.log(`MODEL-RULES OK kinds=${kinds.length}: each switched on is in the draft and the file, switched off is on:false and not in the draft, French and Japanese present`);
    // Structural walls in the Builder: concrete cover only from LOD 350; a group row is switched per element type, kept on reload,
    // and comes back with "Back to the OpenBEP defaults".
    await page.locator('#steps [data-step="6"]').click(); await page.waitForTimeout(150);
    await page.locator('[data-lbsel="wall_interior"]').first().click(); await page.waitForTimeout(150); await openAll();
    await page.selectOption('[data-lodt="wall_interior.final"]', '350'); await page.waitForTimeout(200);
    let SG = (await exportFile()).requirementGroups[0];
    assert.ok(SG.requiredAt.wall_interior.final90.includes('col_cover'), 'interior walls at LOD 350: no concrete cover in the group');
    assert.ok(!SG.requiredAt.wall_exterior.final90.includes('col_cover'), 'exterior walls at LOD 300 ask concrete cover');
    const KEY = 'wall_interior|structural_walls:col_volume';
    await openAll(); await page.locator(`[data-lbk="${KEY}"]`).uncheck(); await page.waitForTimeout(200);
    const off = async () => { const g = (await exportFile()).requirementGroups[0];
      return [g.requiredAt.wall_interior.final90.includes('col_volume'), g.requiredAt.wall_exterior.final90.includes('col_volume')]; };
    assert.deepStrictEqual(await off(), [false, true], 'switching off net volume on interior walls');
    await page.reload(); await page.waitForTimeout(300);
    assert.deepStrictEqual(await off(), [false, true], 'the switch did not survive a reload');
    await page.locator('#steps [data-step="6"]').click(); await page.waitForTimeout(150);
    await page.locator('[data-lbsel="wall_interior"]').first().click(); await page.waitForTimeout(150); await openAll();
    await page.locator('#lbResetAll').click(); await page.waitForTimeout(200);
    assert.deepStrictEqual(await off(), [true, true], '"Back to the OpenBEP defaults" did not bring net volume back');
    console.log('STRUCTURAL SWITCHES OK: concrete cover only where the level is 350; net volume switched off on interior walls only, kept on reload, back with the defaults');
    if (errors.length) throw new Error('Page errors: ' + errors.join(' | '));
    console.log(`REQFILE-PARITY OK milestones=${school.milestones.length} types=${types}`);
  } catch (e) {
    console.error('REQFILE-PARITY FAILED: ' + e.message.split('\n')[0]);
    process.exitCode = 1;
  } finally { await browser.close(); }
})();

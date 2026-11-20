// Proves the requirement file (JSON) is enough on its own: a checker that reads only that file gets the same result
// as the BEP Builder's own "Check a model now" on a model table.
// Opens the Builder on the school example, downloads the requirement file, then for every milestone compares
//   (a) the Builder's own table check (ckRunTable) on revit-addin/OpenBepSync/test/reqfile-parity-model.csv, and
//   (b) the small evaluator below, which reads only the JSON file and the same table.
// Per element type: element count, score, pass, and each requirement's present % and real-value % must be equal.
// The same is done again after adding an owner parameter to Doors, which must appear in the file as an owner addition.
// Usage: node translations/tools/reqfile_parity.js [--break] [--write]
//   --break  removes one Doors requirement from the JSON in memory; the test must then FAIL (negative control).
//   --write  also writes revit-addin/OpenBepSync/test/OpenBEP-requirements-school-example.json (the school example as loaded).
// Needs Playwright + Chromium, like crawl_app.js. CHROMIUM=/path/to/chrome uses a browser already installed;
// PW_CHANNEL=msedge uses the installed Microsoft Edge.
let pw; try { pw = require('playwright'); } catch (e) { pw = require('playwright-core'); }
const { pathToFileURL } = require('node:url');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert');

const root = path.resolve(__dirname, '..', '..');
const testDir = path.join(root, 'revit-addin', 'OpenBepSync', 'test');
const BREAK = process.argv.includes('--break'), WRITE = process.argv.includes('--write');
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
function evaluate(F, T, mid) {
  const ms = F.milestones.find(m => m.id === mid), always = new RegExp(F.placeholders.always, F.placeholders.flags);
  const ci = T.head.findIndex(h => norm(h) === F.tableRule.categoryColumn);
  assert.ok(ci >= 0, 'the model table needs a Category column');
  const typeOf = v => { const n = norm(v); return F.elementTypes.find(t => t.categoryNames.includes(n)); };
  const by = new Map();
  T.rows.forEach(r => { const t = typeOf(r[ci]); if (!t) return; if (!by.has(t.id)) by.set(t.id, { t, els: [] }); by.get(t.id).els.push(r); });
  const real = (q, v) => {
    if (v == null) return false; const s = v.trim();
    if (s === '' || q.placeholders.map(x => String(x).toLowerCase()).includes(s.toLowerCase()) || always.test(s)) return false;
    return new RegExp(q.valueRule.pattern, 'i').test(s);
  };
  return [...by.values()].map(({ t, els }) => {
    const reqs = new Map(t.requirements.map(q => [q.id, q]));
    const rows = t.requiredAt[mid].map(id => {
      const q = reqs.get(id), col = T.head.findIndex(h => q.columnNames.includes(norm(h)));
      let has = 0, ok = 0;
      els.forEach(r => { const v = col >= 0 ? (r[col] || '') : null; if (col >= 0) has++; if (real(q, v)) ok++; });
      return { n: q.label, has: Math.round(100 * has / els.length), ok: Math.round(100 * ok / els.length) };
    });
    const fill = rows.length ? Math.round(rows.reduce((s, r) => s + r.ok, 0) / rows.length) : 100;
    return { id: t.id, lod: t.lod[mid], n: els.length, fill, pass: fill >= ms.targetPercent, rows };
  });
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

    // Parity, per milestone, for the school example and again with the owner addition.
    let types = 0, rowsCompared = 0, partial = 0;
    for (const [label, F, withOwner] of [['with owner addition', owned, true], ['school example', school, false]]) {
      if (withOwner) { /* the Builder state now carries the owner addition, as in F */ }
      else { // put the Builder back to the plain school example for its own check
        await page.evaluate(() => localStorage.clear()); await page.reload();
        await page.locator('[data-ex="school"]').first().click(); await page.waitForTimeout(200);
      }
      for (const m of F.milestones) {
        const builder = await page.evaluate(([t, mid]) => window.__ckTable(t, mid), [table, m.id]);
        const mine = evaluate(F, T, m.id);
        const key = x => x.id;
        const a = [...builder].sort((x, y) => key(x).localeCompare(key(y))), b = [...mine].sort((x, y) => key(x).localeCompare(key(y)));
        try {
          assert.deepStrictEqual(b.map(key), a.map(key), 'element types found');
          a.forEach((x, i) => {
            const y = b[i], where = `${label}, ${m.id}, ${x.id}`;
            assert.strictEqual(y.lod, x.lod, where + ': level');
            assert.strictEqual(y.n, x.n, where + ': elements');
            assert.deepStrictEqual(y.rows, x.rows, where + ': requirement rows');
            assert.strictEqual(y.fill, x.fill, where + ': score');
            assert.strictEqual(y.pass, x.pass, where + ': pass');
            rowsCompared += x.rows.length; partial += x.rows.filter(r => r.ok > 0 && r.ok < 100).length;
          });
        } catch (e) { failed++; console.error('MISMATCH ' + e.message.split('\n')[0] + ` (Builder: ${a.map(key)}; file: ${b.map(key)})`); if (failed > 5) throw new Error('too many mismatches'); continue; }
        if (withOwner) {
          types = a.length;
          if (m.id !== 'prelim100') assert.ok(a.find(x => x.id === 'door').rows.some(r => r.n === OWNER), 'Builder check has no owner row');
        }
      }
    }
    if (errors.length) { failed++; console.error('Page errors:\n' + errors.join('\n')); }
    if (failed) throw new Error(`${failed} milestone comparisons failed`);
    assert.ok(types >= 4 && rowsCompared > 100 && partial > 10, `test table too thin: ${types} types, ${rowsCompared} rows, ${partial} partly filled`);
    console.log(`${rowsCompared} requirement rows compared, ${partial} partly filled`);

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
    if (errors.length) throw new Error('Page errors: ' + errors.join(' | '));
    console.log(`REQFILE-PARITY OK milestones=${school.milestones.length} types=${types}`);
  } catch (e) {
    console.error('REQFILE-PARITY FAILED: ' + e.message.split('\n')[0]);
    process.exitCode = 1;
  } finally { await browser.close(); }
})();

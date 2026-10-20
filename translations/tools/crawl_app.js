// Opens the BEP Builder in a headless browser, walks every step of both
// examples with all sections open (in English, then again in French to catch
// page errors, then in English with a country other than Canada), then opens the other site pages (Scorecard), and lists the dictionary rows the app
// needed (numbers already replaced by {1}, {2}...) and whether French exists.
// Writes translations/build/app_seen.json; check_i18n.py --app reads it.
// Usage: node translations/tools/crawl_app.js   (needs Playwright + Chromium;
// set CHROMIUM=/path/to/chrome to use a browser that is already installed)
const { chromium } = require('playwright');
const { pathToFileURL } = require('node:url');
const { writeFileSync, mkdirSync } = require('node:fs');
const path = require('node:path');

(async () => {
const here = __dirname;
const root = path.resolve(here, '..', '..');
const app = pathToFileURL(path.join(root, 'app', 'bep-builder.html')).href;
const browser = await chromium.launch(process.env.CHROMIUM ? { executablePath: process.env.CHROMIUM } : {});
const page = await browser.newPage();
page.on('dialog', d => d.dismiss());
const errors = [];
page.on('pageerror', e => errors.push(String(e)));
await page.goto(app);
await page.evaluate(() => { localStorage.clear(); });
await page.reload();
const openAll = () => page.evaluate(() => { document.querySelectorAll('details').forEach(d => { d.open = true }); document.querySelectorAll('[data-fall="1"]').forEach(b => b.click()); const ws = document.querySelector('[data-showws]'); if (ws && !ws.checked) ws.click(); });
const seen = new Map();
const walk = async () => { for (const ex of ['school', 'rail']) {
  const b = page.locator(`[data-ex="${ex}"]`).first();
  if (await b.count()) { await b.click(); await page.waitForTimeout(200); }
  const steps = await page.locator('#steps [data-step]').count();
  for (let i = 1; i <= steps; i++) {
    await page.locator(`#steps [data-step="${i}"]`).click();
    await page.waitForTimeout(150);
    await openAll(); await page.waitForTimeout(150);
  }
} };
await walk();
for (const [k, w, fr] of await page.evaluate(() => window.__canbimStrings())) if (!seen.has(k)) seen.set(k, [w, fr]);
await page.selectOption('#langSel', 'fr');
await walk();
// Outside Canada the app swaps Canadian references for neutral wording; record that wording too.
if (await page.locator('#ctrySel').count()) {
  await page.evaluate(() => { localStorage.clear(); }); await page.reload();
  await page.selectOption('#ctrySel', 'jp'); await walk();
  // Japan and design-build choices, new BIM use options, overtime and AR fields, so their wording is recorded too.
  const go = async i => { await page.locator(`#steps [data-step="${i}"]`).click(); await page.waitForTimeout(150); await openAll(); await page.waitForTimeout(150); };
  const tick = q => page.evaluate(q => document.querySelectorAll(q).forEach(x => { if (!x.checked) x.click(); }), q);
  await go(1); await page.selectOption('#p-view', 'db'); await page.waitForTimeout(150); await openAll();
  await tick('[data-jp]'); await openAll(); await tick('[data-hand]'); await openAll();
  await go(4); await tick('[data-opt^="qto."],[data-opt^="ar."],[data-opt="survey.control"],[data-opt^="handover."],[data-opt^="space."],[data-opt^="maint."],[data-opt^="monitor."],[data-opt^="security."]');
  await go(7);
  const setv = (q, v) => page.evaluate(([q, v]) => { const x = document.querySelector(q); if (x) { x.value = v; x.dispatchEvent(new Event('change', { bubbles: true })); } }, [q, v]);
  for (const [k, v] of [['otAvg', '60'], ['otPeak', '110'], ['otMonths', '7']]) { await setv(`[data-pe="0"][data-k="${k}"]`, v); await openAll(); }
  await setv('[data-pe="1"][data-k="otAvg"]', '50'); await openAll(); await setv('[data-pe="2"][data-k="otAvg"]', '20'); await openAll();
  
  // Each delivery mode's notes, as shown outside Canada (Canadian contract references removed).
  for (const d of ['dbb', 'cm', 'db', 'ccp', 'ipd', 'ppp', 'pppm']) { await go(1); await page.selectOption('#p-deliv', d); await page.waitForTimeout(150); await openAll(); await go(13); }
  await go(1); await page.selectOption('#p-view', 'gc'); await page.waitForTimeout(150); await openAll();
  for (let i = 1; i <= 13; i++) await go(i);
  for (const [k, w, fr] of await page.evaluate(() => window.__canbimStrings())) if (!seen.has(k)) seen.set(k, [w, fr]);
}

// LOD matrix drill-down into one element type, with one edit of each kind so the "edited" wording shows.
for (const lang of ['en']) {
  await page.evaluate(() => { localStorage.clear(); }); await page.reload();
  if (await page.locator('#ctrySel').count()) await page.selectOption('#ctrySel', 'ca');
  await page.selectOption('#langSel', lang);
  await page.locator('[data-ex="school"]').first().click(); await page.waitForTimeout(200);
  await page.locator('#steps [data-step="5"]').click(); await openAll();
  // The worked example and the element-type view under a matrix row (one with a 2D and a "not modelled" stage).
  await page.locator('[data-lodpeekgo]').click(); await page.waitForTimeout(150); await openAll();
  for (const [k, w, fr] of await page.evaluate(() => window.__canbimStrings())) if (!seen.has(k)) seen.set(k, [w, fr]);
  await page.locator('button.etchip', { hasText: 'Concrete reinforcement' }).first().click(); await page.waitForTimeout(150); await openAll();
  for (const [k, w, fr] of await page.evaluate(() => window.__canbimStrings())) if (!seen.has(k)) seen.set(k, [w, fr]);
  await page.locator('.pk-h .x').click(); await page.waitForTimeout(150);
  if (!(await page.locator('[data-lbgo]').count())) { await page.locator('[data-lodx]').first().click(); await page.waitForTimeout(150); await openAll(); }
  await page.locator('[data-lbgo]').first().click(); await page.waitForTimeout(150); await openAll();
  const setv = (q, v) => page.evaluate(([q, v]) => { const x = document.querySelector(q); if (x) { if (x.type === 'checkbox') x.checked = v; else x.value = v; x.dispatchEvent(new Event('change', { bubbles: true })); } }, [q, v]);
  await setv('[data-lbnote]', 'Crawl note'); await openAll(); await setv('[data-lbgf]', '400'); await openAll();
  await page.fill('#lbg-t', 'Crawl geometry'); await page.locator('#lbgAdd').click(); await page.waitForTimeout(150); await openAll();
  await setv('[data-lodt$=".ops"]', '500'); await page.waitForTimeout(150); await openAll(); await page.locator('[data-lbview="500"]').click(); await page.waitForTimeout(150); await openAll();
  for (const [k, w, fr] of await page.evaluate(() => window.__canbimStrings())) if (!seen.has(k)) seen.set(k, [w, fr]);
}
// Airport terminal: specialty systems (baggage handling, boarding bridges) and an element type the project adds itself.
for (const lang of ['en']) {
  await page.evaluate(() => { localStorage.clear(); }); await page.reload();
  if (await page.locator('#ctrySel').count()) await page.selectOption('#ctrySel', 'ca');
  await page.selectOption('#langSel', lang);
  await page.locator('[data-ex="school"]').first().click(); await page.waitForTimeout(200);
  await page.locator('#steps [data-step="1"]').click(); await page.selectOption('#p-asset', 'airportb'); await page.waitForTimeout(150);
  await page.locator('#steps [data-step="5"]').click(); await openAll();
  await page.locator('#steps [data-step="6"]').click(); await openAll();
  await page.fill('#cel-n', 'Crawl element'); await page.locator('#celAdd').click(); await page.waitForTimeout(150); await openAll();
  await page.locator('[data-lbsel="baggage_handling"]').click(); await page.waitForTimeout(150); await openAll();
  for (const [k, w, fr] of await page.evaluate(() => window.__canbimStrings())) if (!seen.has(k) && !k.includes('Crawl element')) seen.set(k, [w, fr]);
  await page.locator('[data-lbsel="boarding_bridge"]').click(); await page.waitForTimeout(150); await openAll();
  for (const [k, w, fr] of await page.evaluate(() => window.__canbimStrings())) if (!seen.has(k) && !k.includes('Crawl element')) seen.set(k, [w, fr]);
}
// Other site pages use translations/i18n.js; record what they show too.
const record = async (p2, rel) => {
  for (const k of await p2.evaluate(() => window.openbepStrings ? window.openbepStrings() : [])) {
    const r = await p2.evaluate(k => { const F = (window.OPENBEP_STRINGS || {}).fr || {}; if (k.endsWith('…') || !/[a-z]{3}/.test(k)) return null; if (F[k]) return [k, true]; if (trText(k, F, 'fr')) return null; return [i18nKey(k), !!F[i18nKey(k)]]; }, k);
    if (r && !seen.has(r[0])) seen.set(r[0], [rel, r[1]]);
  }
};
for (const rel of ['app/scorecard.html', 'lod/index.html']) {
  const p2 = await browser.newPage();
  p2.on('pageerror', e => errors.push(rel + ': ' + String(e)));
  p2.on('dialog', d => d.dismiss());
  await p2.goto(pathToFileURL(path.join(root, rel)).href);
  for (const id of ['#exBtn', '#reportBtn']) if (await p2.locator(id).count()) { await p2.locator(id).first().click(); await p2.waitForTimeout(300); }
  await p2.evaluate(() => document.querySelectorAll('details').forEach(d => { d.open = true }));
  await p2.waitForTimeout(200);
  // LOD specification: open every element type, then once with a BIM use filter so the "hidden" notes show.
  if (rel === 'lod/index.html') {
    for (const id of await p2.evaluate(() => [...document.querySelectorAll('[data-el]')].map(b => b.dataset.el))) await p2.locator(`[data-el="${id}"]`).click();
    await p2.locator('[data-use="fm"]').click(); await p2.waitForTimeout(100);
    // IFC columns, each grouping of the list, and a project saved by the BEP Builder.
    await p2.locator('#ifc').check(); await p2.locator('[data-el="door"]').click();
    for (const g of ['uf', 'mf', 'az']) { await p2.selectOption('#grp', g); await p2.waitForTimeout(100); }
    await p2.evaluate(() => localStorage.setItem('openbep-lod-project', JSON.stringify({ name: 'Test', stages: [['final', 'Final design']], el: { door: { final: '300' } }, cls: [] })));
    await record(p2, rel); await p2.reload(); await p2.waitForTimeout(200); await p2.locator('[data-el="door"]').click();
  }
  await record(p2, rel);
  await p2.selectOption('select[data-i18n-lang]', 'fr');
  await p2.waitForTimeout(200);
  await p2.close();
}
await browser.close();
mkdirSync(path.join(root, 'translations', 'build'), { recursive: true });
writeFileSync(path.join(root, 'translations', 'build', 'app_seen.json'), JSON.stringify([...seen.entries()].map(([k, [w, fr]]) => ({ en: k, where: w, fr })), null, 1));
const missing = [...seen.values()].filter(v => !v[1]).length;
console.log(`${seen.size} dictionary rows needed, ${missing} without French`);
if (errors.length) { console.error('Page errors:\n' + errors.join('\n')); process.exitCode = 1; }
})();

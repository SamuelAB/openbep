// Proves the compliance page needs no custom code per plan: it renders the example and three
// very different plans built from it (2 firms, 6 firms with other codes, 3 versions with one
// milestone and no rules), and refuses a broken file with plain reasons.
// Usage: node compliance/test/test_page.js   (needs Playwright + Chromium)
const { chromium } = require('playwright');
const http = require('node:http'), fs = require('node:fs'), path = require('node:path');
const { validateSeries } = require('../series.js');
const root = path.resolve(__dirname, '..', '..');
const ex = JSON.parse(fs.readFileSync(path.join(root, 'compliance', 'example-school.json'), 'utf8'));
const clone = o => JSON.parse(JSON.stringify(o));

function keepRoles(d, codes) {
  d.plan.roles = d.plan.roles.filter(r => codes.includes(r.code));
  for (const v of d.versions) {
    v.uploadedBy = v.uploadedBy.filter(c => codes.includes(c));
    if (v.state !== 'checked') continue;
    for (const c of Object.keys(v.byRole)) if (!codes.includes(c)) delete v.byRole[c];
    for (const per of Object.values(v.valueChecks)) for (const c of Object.keys(per)) if (!codes.includes(c)) delete per[c];
  }
  return d;
}
function renameRoles(d, map) {
  for (const r of d.plan.roles) r.code = map[r.code];
  for (const v of d.versions) {
    v.uploadedBy = v.uploadedBy.map(c => map[c]);
    if (v.state !== 'checked') continue;
    v.byRole = Object.fromEntries(Object.entries(v.byRole).map(([c, x]) => [map[c], x]));
    for (const k of Object.keys(v.valueChecks)) v.valueChecks[k] = Object.fromEntries(Object.entries(v.valueChecks[k]).map(([c, x]) => [map[c], x]));
  }
  return d;
}
const fixtures = {};
fixtures.example = ex;
{ // Two firms, goals renamed, one goal with nothing checkable, holidays removed.
  const d = keepRoles(clone(ex), ['ARCH', 'MECH']);
  d.example = false; d.plan.name = 'Test: two firms'; d.plan.holidays = [];
  d.plan.goals = d.plan.goals.map((g, i) => ({ ...g, name: 'Goal number ' + (i + 1) + ' with a long name that must wrap and never be cut' }));
  d.plan.goals.push({ id: 'carbon', name: 'Energy and carbon targets', uses: ['Energy analysis'] });
  fixtures.two = d;
}
{ // Six firms with other codes: two extra roles copy STRUCT's numbers.
  const d = renameRoles(clone(ex), { ARCH: 'A', STRUCT: 'S', MECH: 'M', CON: 'GC' });
  d.example = false; d.plan.name = 'Test: six firms';
  d.plan.roles.push({ code: 'ELEC', role: 'Electrical engineer', firm: 'E (test)', document: 'E.rvt', from: null }, { code: 'LAND', role: 'Landscape architect', firm: 'L (test)', document: 'L.rvt', from: null });
  for (const v of d.versions) { if (v.state !== 'checked') continue; for (const c of ['ELEC', 'LAND']) { v.byRole[c] = clone(v.byRole.S); v.uploadedBy.push(c); for (const per of Object.values(v.valueChecks)) if (per.S) per[c] = clone(per.S); } }
  fixtures.six = d;
}
{ // Three versions, one milestone, no model rules, a monthly cadence.
  const d = clone(ex); d.example = false; d.plan.name = 'Test: three versions';
  d.rules = []; d.plan.milestones = [{ id: 'm', name: 'Only milestone', date: '2027-12-31', targetPercent: 80 }];
  d.versions = d.versions.filter(v => v.state === 'checked').slice(0, 3).map((v, i) => ({ ...v, milestone: 'm', rules: {}, created: `2027-0${i + 1}-15T12:00:00Z` }));
  d.model.cadenceDays = 30; d.plan.holidays = [];
  fixtures.three = d;
}
const broken = clone(ex); broken.rules[0].goal = 'nope'; broken.versions[0].byRole.ARCH.met = -1;

let fail = 0;
const ok = (c, m) => { if (!c) { fail++; console.error('FAIL', m); } else console.log('ok  ', m); };
for (const [k, d] of Object.entries(fixtures)) { const p = validateSeries(d); ok(!p.length, `${k}: valid series` + (p.length ? ' — ' + p.slice(0, 3).join(' | ') : '')); }
ok(validateSeries(broken).length >= 2, 'broken file: validator lists the problems');

const server = http.createServer((req, res) => {
  const u = new URL(req.url, 'http://x');
  const m = u.pathname.match(/^\/fixture\/(\w+)\.json$/);
  if (m) { res.setHeader('content-type', 'application/json'); return res.end(JSON.stringify(m[1] === 'broken' ? broken : fixtures[m[1]])); }
  const f = path.join(root, decodeURIComponent(u.pathname).replace(/\/$/, '/index.html'));
  if (!f.startsWith(root) || !fs.existsSync(f)) { res.statusCode = 404; return res.end(); }
  res.setHeader('content-type', f.endsWith('.html') ? 'text/html' : f.endsWith('.js') ? 'text/javascript' : 'application/json');
  fs.createReadStream(f).pipe(res);
}).listen(0, async () => {
  const port = server.address().port;
  const browser = await chromium.launch(process.env.CHROMIUM ? { executablePath: process.env.CHROMIUM } : {});
  for (const width of [1280, 390]) for (const k of [...Object.keys(fixtures), 'broken']) {
    const page = await browser.newPage({ viewport: { width, height: 900 } });
    const errs = []; page.on('pageerror', e => errs.push(String(e)));
    await page.route(/fonts\.(googleapis|gstatic)\.com/, r => r.abort());
    const q = k === 'example' ? '' : `?data=/fixture/${k}.json`;
    await page.goto(`http://127.0.0.1:${port}/compliance/${q}`);
    await page.waitForFunction(() => document.querySelector('#tiles .tile') || document.querySelector('.err'), null, { timeout: 10000 });
    const r = await page.evaluate(() => ({
      err: !!document.querySelector('.err'), goals: document.querySelectorAll('#goals .goal').length, pace: document.querySelectorAll('#paceTbl tbody tr').length,
      wide: document.documentElement.scrollWidth > innerWidth + 1, ell: [...document.querySelectorAll('main *')].filter(e => e.tagName !== 'SELECT' && e.tagName !== 'OPTION' && !e.closest('svg') && !e.closest('.mx') && !e.matches('.tw') && !e.closest('details') && !e.closest('.stripbox') && !e.closest('#changed') && e.scrollWidth > e.clientWidth + 1 && getComputedStyle(e).overflowX !== 'visible').map(e => e.className || e.tagName).slice(0, 3).join(','),
      msg: (document.querySelector('.err') || {}).innerText, cut: [...document.querySelectorAll('#paceTbl *')].filter(e => { const b = e.getBoundingClientRect(), t = document.querySelector('#paceTbl').getBoundingClientRect(); return innerWidth < 700 && b.width && b.right > t.right + 1; }).length, wideEl: [...document.querySelectorAll('body *')].filter(e => e.getBoundingClientRect().right > innerWidth + 1 && !e.closest('.mx') && !e.closest('.tw') && !e.closest('.stripbox')).map(e => e.id || e.className || e.tagName).slice(0, 4).join(','),
    }));
    ok(!errs.length, `${k} @${width}: no page errors ${errs.join(' ')}`);
    if (k === 'broken') { ok(r.err, `broken @${width}: refused with reasons`); await page.close(); continue; }
    const d = fixtures[k], last = [...d.versions].reverse().find(v => v.state === 'checked');
    ok(!r.err, `${k} @${width}: renders ${r.msg || ''}`);
    ok(r.goals === d.plan.goals.length, `${k} @${width}: one card per goal (${r.goals}/${d.plan.goals.length})`);
    ok(r.pace === Object.keys(last.byRole).length, `${k} @${width}: one pace row per firm (${r.pace})`);
    ok(!r.wide, `${k} @${width}: no page-wide horizontal scroll ${r.wideEl}`);
    ok(!r.ell, `${k} @${width}: nothing clipped ${r.ell}`);
    ok(!r.cut, `${k} @${width}: pace cards fit the phone (${r.cut} past the edge)`);
    // Select the first version and back, to exercise the "no earlier version" path.
    await page.selectOption('#selV', '0'); await page.waitForTimeout(50);
    ok(!errs.length, `${k} @${width}: first version selectable`);
    await page.close();
  }
  await browser.close(); server.close();
  if (fail) { console.error(`${fail} check(s) failed`); process.exit(1); }
  console.log('all compliance page checks passed');
});

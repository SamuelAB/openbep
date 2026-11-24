// The model page: drill from the whole model to one element with any results file, numbers add up,
// a link reopens the same place, and a broken file is refused. The 3D viewer is not loaded here.
// Usage: node compliance/test/test_model.js   (needs Playwright + Chromium)
const { chromium } = require('playwright');
const http = require('node:http'), fs = require('node:fs'), path = require('node:path');
const root = path.resolve(__dirname, '..', '..');
const ex = JSON.parse(fs.readFileSync(path.join(root, 'compliance/model/example-residence.json'), 'utf8'));
// A second, very different file: two models, real worksets, three checks.
const other = {
  format: 'openbep-compliance-elements', formatVersion: '0.1', project: { name: 'Other' }, model: { name: 'Two models' }, version: { label: 'V1' },
  checks: [{ id: 'a', label: 'Check A', parameter: 'A', on: 'type' }, { id: 'b', label: 'Check B', parameter: 'B', on: 'instance' }, { id: 'c', label: 'Check C', on: 'element' }],
  columns: ex.columns, strings: ['', 'M1', 'M2', 'Shell', 'Interior', 'Walls', 'Doors', 'Fam', 'T1', 'T2', 'L1'],
  elements: Array.from({ length: 40 }, (_, i) => [1000 + i, 1 + i % 2, 3 + (i >> 1) % 2, 5 + (i >> 2) % 2, 7, 8 + (i >> 3) % 2, 10, 7, i % 8]),
};
const broken = Object.assign({}, other, { elements: [[1, 1, 3, 5, 7, 8, 10, 1, 3]] });
const files = { '/compliance/model/other.json': other, '/compliance/model/broken.json': broken };
const pop = x => { let n = 0; while (x) { n += x & 1; x >>>= 1; } return n; };

const srv = http.createServer((q, r) => {
  const u = decodeURIComponent(q.url.split('?')[0]);
  if (files[u]) return r.end(JSON.stringify(files[u]));
  let f = path.join(root, u); if (u.endsWith('/')) f += 'index.html';
  fs.readFile(f, (e, d) => {
    if (e) { r.writeHead(404); r.end(); return; }
    r.writeHead(200, { 'content-type': f.endsWith('.html') ? 'text/html' : 'application/json' }); r.end(d);
  });
});
srv.listen(0, async () => {
  const base = 'http://localhost:' + srv.address().port + '/compliance/model/';
  const b = await chromium.launch(); let failed = 0;
  const ok = (n, c) => { console.log((c ? 'ok   ' : 'FAIL ') + n); if (!c) failed++; };
  for (const [name, file, d] of [['example', '', ex], ['other', 'other.json', other]]) for (const w of [1280, 390]) {
    const p = await b.newPage({ viewport: { width: w, height: 800 } }); const errs = []; p.on('pageerror', e => errs.push(e.message));
    await p.route(/^https:/, r => r.abort());
    const q = file ? '&file=' + file : '';
    await p.goto(base + '?no3d=1' + q); await p.waitForSelector('#rows .row');
    const req = d.elements.reduce((s, r) => s + pop(r[7]), 0), met = d.elements.reduce((s, r) => s + pop(r[8]), 0);
    const want = (Math.floor(1000 * met / req) / 10).toFixed(1) + ' %';
    ok(name + ' ' + w + ': verdict shows ' + want, (await p.textContent('#verdict .gt')).startsWith(want));
    const counts = await p.$$eval('#rows .row .sm', a => a.map(x => parseInt(x.textContent.replace(/,/g, ''), 10)));
    ok(name + ' ' + w + ': first level adds up to every element', counts.reduce((s, n) => s + n, 0) === d.elements.length);
    let guard = 0;
    while (await p.$('#rows .row[data-into]') && guard++ < 8) await p.click('#rows .row[data-into]');
    ok(name + ' ' + w + ': drill reaches single elements', !!(await p.$('#rows .row[data-el]')));
    await p.click('#rows .row[data-el]'); await p.waitForSelector('.el table');
    ok(name + ' ' + w + ': element shows one line per check', (await p.$$('.el tbody tr')).length === d.checks.length);
    ok(name + ' ' + w + ': fits the screen', await p.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
    const u = p.url(); await p.goto(u); await p.waitForSelector('.el table');
    ok(name + ' ' + w + ': link reopens the element', /el=\d+/.test(u));
    await p.goto(base + '?no3d=1&check=' + d.checks[0].id + q); await p.waitForSelector('#rows .row');
    ok(name + ' ' + w + ': check filter is on', !!(await p.$('.chk.on')));
    ok(name + ' ' + w + ': no script errors', !errs.length); if (errs.length) console.log(errs);
    await p.close();
  }
  const p = await b.newPage(); await p.goto(base + '?file=broken.json'); await p.waitForSelector('.err');
  ok('broken file is refused with a reason', /not a valid row/.test(await p.textContent('.err')));
  await b.close(); srv.close();
  if (failed) { console.log(failed + ' failed'); process.exit(1); }
  console.log('all model page checks passed');
});

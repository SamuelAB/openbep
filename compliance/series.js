/* OpenBEP compliance series: the one data contract every compliance screen reads
   (this web page, a VIM Flex page, or any other checker's screen). See compliance/FORMAT.md.
   validateSeries(doc) returns a list of problems in plain words; an empty list means any
   screen built on this format can show the file with no custom code.
   Used by compliance/index.html and by compliance/test/test_page.js. */
(function (root) {
  'use strict';
  const DIRS = ['lowerIsBetter', 'higherIsBetter'];
  const isNum = x => typeof x === 'number' && isFinite(x);
  const isDate = x => typeof x === 'string' && /^\d{4}-\d{2}-\d{2}/.test(x) && !isNaN(Date.parse(x.slice(0, 10)));
  function validateSeries(d) {
    const e = [];
    const need = (ok, msg) => { if (!ok) e.push(msg); return ok; };
    if (!need(d && typeof d === 'object', 'The file is not a JSON object.')) return e;
    need(d.format === 'openbep-compliance-series', 'format must be "openbep-compliance-series".');
    need(typeof d.formatVersion === 'string' && /^0\./.test(d.formatVersion), 'formatVersion must be 0.x (this page reads 0.1 and 0.2).');
    const p = d.plan || {};
    need(typeof p.name === 'string' && p.name, 'plan.name is missing.');
    const goals = Array.isArray(p.goals) ? p.goals : [], ms = Array.isArray(p.milestones) ? p.milestones : [], roles = Array.isArray(p.roles) ? p.roles : [];
    need(goals.length, 'plan.goals needs at least one goal.');
    need(ms.length, 'plan.milestones needs at least one milestone.');
    need(roles.length, 'plan.roles needs at least one role.');
    const gid = new Set(), mid = new Set(), codes = new Set();
    goals.forEach((g, i) => { need(g.id && g.name, `plan.goals[${i}] needs id and name.`); need(!gid.has(g.id), `Goal id "${g.id}" is used twice.`); gid.add(g.id); need(!g.uses || Array.isArray(g.uses), `plan.goals[${i}].uses must be a list.`); });
    ms.forEach((m, i) => { need(m.id && m.name, `plan.milestones[${i}] needs id and name.`); need(isDate(m.date), `Milestone "${m.name}" needs a date (YYYY-MM-DD).`); need(isNum(m.targetPercent) && m.targetPercent >= 0 && m.targetPercent <= 100, `Milestone "${m.name}" needs targetPercent between 0 and 100.`); if (i) need(m.date >= ms[i - 1].date, 'plan.milestones must be in date order.'); mid.add(m.id); });
    roles.forEach((r, i) => { need(r.code && r.role, `plan.roles[${i}] needs code and role.`); need(!codes.has(r.code), `Role code "${r.code}" is used twice.`); codes.add(r.code); need(r.from == null || isDate(r.from), `Role ${r.code}: from must be a date or null.`); });
    (p.holidays || []).forEach((h, i) => need(isDate(h.from) && isDate(h.to) && h.to >= h.from, `plan.holidays[${i}] needs from and to dates, in order.`));
    const vcs = Array.isArray(d.valueChecks) ? d.valueChecks : [], rules = Array.isArray(d.rules) ? d.rules : [];
    const vid = new Set(), rid = new Set();
    vcs.forEach((c, i) => { need(c.id && c.label, `valueChecks[${i}] needs id and label.`); need(gid.has(c.goal), `Value check "${c.label}" names goal "${c.goal}", which is not in plan.goals.`); vid.add(c.id); });
    rules.forEach((r, i) => { need(r.id && r.label, `rules[${i}] needs id and label.`); need(gid.has(r.goal), `Rule "${r.label}" names goal "${r.goal}", which is not in plan.goals.`); need(DIRS.includes(r.direction), `Rule "${r.label}": direction must be lowerIsBetter or higherIsBetter.`); need(isNum(r.limit), `Rule "${r.label}" needs a numeric limit.`); need(typeof r.limitText === 'string' && r.limitText, `Rule "${r.label}" needs limitText (for example "at most 1,500").`); rid.add(r.id); });
    need(vcs.length, 'valueChecks needs at least one check: compliance is the share of required values filled.');
    const vs = Array.isArray(d.versions) ? d.versions : [];
    if (!need(vs.length, 'versions is empty.')) return e;
    let nChecked = 0;
    vs.forEach((v, i) => {
      const L = v.label || `versions[${i}]`;
      need(isNum(v.order), `${L}: order must be a number.`);
      need(isDate(v.created), `${L}: created must be a date.`);
      if (i) { need(v.order > vs[i - 1].order, 'versions must be sorted by order.'); need(String(v.created).slice(0, 10) >= String(vs[i - 1].created).slice(0, 10), 'versions must be in date order.'); }
      need(mid.has(v.milestone), `${L}: milestone "${v.milestone}" is not in plan.milestones.`);
      need(Array.isArray(v.uploadedBy) && v.uploadedBy.every(c => codes.has(c)), `${L}: uploadedBy must list role codes from plan.roles.`);
      if (v.state === 'notChecked') { need(typeof v.reason === 'string' && v.reason, `${L}: a version that was not checked needs a reason.`); return; }
      if (!need(v.state === 'checked', `${L}: state must be "checked" or "notChecked".`)) return;
      nChecked++;
      const br = v.byRole || {};
      need(Object.keys(br).length, `${L}: byRole is empty.`);
      for (const [c, r] of Object.entries(br)) {
        need(codes.has(c), `${L}: byRole has "${c}", which is not in plan.roles.`);
        need(['elements', 'required', 'met', 'elementsAdded', 'valuesFixed', 'valuesBroken'].every(k => isNum(r[k])) && typeof r.uploaded === 'boolean', `${L}, ${c}: byRole needs elements, required, met, uploaded, elementsAdded, valuesFixed, valuesBroken.`);
        need(r.required > 0 && r.met >= 0 && r.met <= r.required, `${L}, ${c}: met must be between 0 and required, and required above 0.`);
        // Optional since 0.2: values that hold something wrong (not empty), and how clean new elements arrive.
        if (r.wrong != null) need(isNum(r.wrong) && r.wrong >= 0 && r.wrong <= r.required - r.met, `${L}, ${c}: wrong must be between 0 and required − met.`);
        if (r.newRequired != null || r.newMet != null) need(isNum(r.newRequired) && isNum(r.newMet) && r.newMet >= 0 && r.newMet <= r.newRequired, `${L}, ${c}: newMet must be between 0 and newRequired.`);
      }
      const sums = {};
      for (const [id, per] of Object.entries(v.valueChecks || {})) {
        need(vid.has(id), `${L}: value check "${id}" is not in valueChecks.`);
        for (const [c, pair] of Object.entries(per)) {
          need(br[c], `${L}: value check "${id}" has role "${c}", which is not in byRole.`);
          if (need(Array.isArray(pair) && pair.length === 2 && isNum(pair[0]) && isNum(pair[1]) && pair[1] <= pair[0] && pair[1] >= 0, `${L}: value check "${id}", ${c} must be [required, met] with met ≤ required.`)) { sums[c] = sums[c] || [0, 0]; sums[c][0] += pair[0]; sums[c][1] += pair[1]; }
        }
      }
      for (const [c, r] of Object.entries(br)) if (sums[c]) need(sums[c][0] === r.required && sums[c][1] === r.met, `${L}, ${c}: the value checks add up to ${sums[c][1]} of ${sums[c][0]}, but byRole says ${r.met} of ${r.required}.`);
      for (const id of rid) { const x = (v.rules || {})[id]; need(x && isNum(x.value) && typeof x.pass === 'boolean', `${L}: rule "${id}" needs value and pass.`); }
    });
    need(nChecked, 'No version was checked.');
    return e;
  }
  root.validateSeries = validateSeries;
  if (typeof module !== 'undefined') module.exports = { validateSeries };
})(typeof window !== 'undefined' ? window : globalThis);

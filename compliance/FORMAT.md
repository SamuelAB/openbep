# Compliance series: one file, any plan, no custom screen

OpenBEP writes the plan. A checker (VIM, or any other platform) checks the model. This folder shows how the
results are shown over time, and fixes the contract so that **a different plan never needs a different screen**.

The page at `/compliance/` contains no project name, no goal, no firm, no rule and no milestone. It reads all of
them from one file. A VIM Flex page, or any other screen, can read the same file and follow the same rules.

- Format name: `openbep-compliance-series`, version `0.6` (draft). 0.2 added three optional counts per role; 0.3 the plan's escalation steps and the owner of a failing rule; 0.4 an icon per check; 0.5 the roles that use each check's data; 0.6 a plain-words sentence per check. All optional. Older files still load.
- Example: [example-school.json](example-school.json), simulated, written by `build_example.py`.
- Validator: [series.js](series.js), `validateSeries(doc)` returns a list of problems in plain words. The page
  runs it on every file and refuses a file that fails, with the reasons.
- Proof: `test/test_page.js` (run in CI) shows the example and three very different plans (2 firms; 6 firms
  with other codes; 3 versions, one milestone, no model rules, monthly) at desktop and phone width, with no
  page error, no page-wide scroll and nothing clipped.

Try another file: "Open a series file" on the page, or `/compliance/?data=<address of a file on the same site>`.

## Where each thing comes from

| In the series file | Comes from | Never from |
|---|---|---|
| Goals, the BIM uses behind them | The plan (BEP Builder, owner goals and chosen uses) | The screen |
| Milestones, dates, target % | The plan | The screen |
| Roles, codes, firms, start date | The plan (Roles and firms; the contractor joins at award) | The screen |
| Agreed holidays | The plan (Teams and capacity) | The screen |
| Value checks and model rules, their labels, limits, direction, and the goal each one serves | The plan's requirement file | The screen |
| Versions, dates, who uploaded | The model server (VIM Server version history) | File dates |
| Required and met counts, rule values, pass or fail | The checker | The screen |

The screen only adds up, divides, compares and draws.

## The file

```
format, formatVersion, example (true when the data is invented), note
plan
  name, owner, delivery
  goals[]       { id, name, uses[] }
  milestones[]  { id, name, date, targetPercent }          in date order
  holidays[]    { name, from, to }                         weeks inside are not counted
  roles[]       { code, role, firm, document, from }       from = null, or the date the role joins
  escalation[]  the plan's steps when a firm misses milestones in a row, in order (optional, 0.3)
model           { server, project, source, schedule, cadenceDays (7 when absent) }
valueChecks[]   { id, goal, label, icon, usedBy }
rules[]         { id, goal, label, limit, unit, limitText, direction, icon, usedBy }
                usedBy (optional, 0.5): role codes that need this data to do their own work.
                help (optional, 0.6): one plain sentence saying why this check matters, for readers who do not know BIM.
                icon (optional, 0.4): one of value, code, material, fire, asset, room, workset, name, level,
                clash, warning, duplicate. Anything else, or none, shows the icon of the kind of check.
                direction: lowerIsBetter | higherIsBetter
versions[]      in order
  order, label, tag, created, milestone (the one in force), uploadedBy[] (role codes)
  state         checked | notChecked (then: reason)
  elementCount
  byRole        { CODE: { elements, required, met, uploaded, elementsAdded, valuesFixed, valuesBroken,
                          wrong, newRequired, newMet } }   the last three are optional (0.2)
  valueChecks   { checkId: { CODE: [required, met] } }      adds up to byRole
  rules         { ruleId: { value, pass, byRole } }         byRole = failing elements per role (optional, 0.3)
```

- **required** = the number of values the plan asks for at the milestone in force (elements × the parameters
  required for their element type). **met** = how many hold a real value, by the requirement file's own
  definition of a real value.
- **valuesFixed** = required values that meet the plan now and did not in the previous version, including
  values on elements added since. **valuesBroken** = values that met the plan and no longer do. Elements are matched between versions by document name + Revit element id.
- **wrong** = required values that hold something that is not a real value (a placeholder, a value outside
  the allowed list). The rest of required − met is empty.
- **newRequired**, **newMet** = the values required on elements added since the previous version, and how many
  of them are already filled.
- A role that uploaded nothing keeps its previous numbers, with `uploaded: false` and zeros for the three
  change counts.
- A version that could not be checked has no numbers and a `reason`. A gap is never a zero.

## What every screen works out the same way

| Number | Rule |
|---|---|
| Overall compliance | met ÷ required, all roles |
| A firm's compliance | met ÷ required, that role |
| A goal's compliance | met ÷ required over the value checks of that goal |
| Target | `targetPercent` of the version's milestone |
| Pace | valuesFixed over the last four working periods ÷ 4. Holiday periods are skipped. No upload counts as zero. |
| Needed pace | (target × required − met) ÷ working periods left to the milestone |
| Model rules | pass or fail as the checker says; never averaged into a percentage |
| Gate | passes when overall compliance ≥ target and every model rule passes |
| Gap | per value check and role: target × required − met, largest first |
| Milestone result | judged on the last version checked on or before the milestone date: passed when the gate passes |
| Deficiency | one per value check and role below the target, one per failing model rule |
| Open since | first version of the current unbroken run of that deficiency |
| Closes at this pace | size ÷ pace of the last four working periods, counted forward skipping holidays |
| Urgency | Overdue: open through a milestone it should have met. Will miss: closes after the milestone, or never. On course: closes in time. |
| Severity | Required values: points below the target. Critical 15 or more, major 5 to 15, minor under 5. A rule in %: the same, in points from its limit. A counted rule: critical at half its limit or more beyond it, major from a tenth; with a limit of 0, critical from 10, major from 3. |
| Payment check | Per firm, four conditions: its model was uploaded and checked in the latest working period; its own compliance is at the target; no model rule fails on its elements; nothing of its own is overdue. A rule not split by firm holds no single firm. The contract decides payment. |
| Late or ahead | values short of the target ÷ (values fixed − values broken per working period), against the working periods left |
| Firm verdict | Badly behind: 4 working periods or more late, or never reaches the target at its pace, or 2 milestones missed in a row. Behind: late by less, or an item overdue or going to miss. Otherwise on track. |
| Holds back | a firm holds back each other role named in `usedBy` of one of its open deficiencies |
| Share of what is missing | the firm's values short of the target ÷ all firms' values short |
| Step set by the plan | milestones a firm missed in a row on its own elements, read against `plan.escalation` |
| New elements arrive filled | newMet ÷ newRequired over the last four working periods |

## What every screen shows the same way

Three levels, from the owner to the investigator. The address keeps the level (`firm`, `item`).

- **Project.** The gate, who needs attention first and who is doing well, one card per firm (verdict, compliance and trend, late or ahead, share of what is missing, who it holds back, milestones missed in a row, open items, payment claim), the five biggest problems, the milestone track, compliance over time.
- **Firm.** Its verdict and facts, its compliance over time, its deficiencies, its accountability row, its payment check.
- **Item.** One check of one firm: severity and urgency, the numbers now, the chart over every version, the timeline, the result at each milestone, and every version's numbers.

The parts, in the order they were added:

1. The gate: would this version pass the milestone, and which conditions fail. Then the summary: overall
   compliance, model rules met, forecast, versions checked.
   Then one card per milestone: date, target, result, failing rules, firms below the target.
   Then the active deficiencies, grouped by firm or all together, most urgent first, each with its owner,
   size, age, pace and closing date. Then one accountability row per firm: milestones met and missed,
   missed in a row, the plan's step, what is open now.
   Then one payment card per firm: the four conditions, met or missing.
   Every deficiency carries the same graphics: severity bars, the icon of its check, a bar of size against
   the target or limit, a trend line with an arrow, and a timeline (opened, this version, milestone, closing date).
   The milestones are also drawn as one track.
2. Overall compliance over time, with the stepped target, milestones, holidays and unchecked versions; the
   same chart in values shows what the plan requires against what is filled.
3. One row per role: compliance, wrong and empty, pace, how clean new elements arrive, needed pace, uploads.
   One square per role per week. A firm filter narrows the whole page to one role.
4. One card per goal: compliance over time, each role's share, its value checks, its model rules.
5. What changed between two versions, by role, then rules, then value checks.
6. Every check by every version. The arrow is the direction of the value; the colour is better or worse.

Counts change with the plan; the layout does not:

- Role colours follow the role's position in `plan.roles` (4 validated colours, light and dark). From the 5th
  role on, a neutral gray and the code label. The code is always written, so colour is never the only signal.
- A goal with nothing checkable still gets its card, saying so.
- Long names wrap. Nothing is cut with "…". Wide tables scroll inside their own box.
- Rule labels and limits are printed as given. The screen builds no wording of its own.

## What is still missing upstream

The requirement file the BEP Builder exports today (`openbep-requirements` 1.5) and the checker's result file
(`openbep-result` 1.1) do not yet carry everything this series needs. Until they do, a series cannot be built
from a real project without someone filling the gaps by hand:

1. **Goal of each requirement and each model rule.** The requirement file lists parameters and rules, not the
   goal or BIM use that asked for them.
2. **Roles.** Role codes, firms, the Revit document of each role, and the date a role joins.
3. **Milestone dates and agreed holidays.** The requirement file has milestone names and targets, no dates.
4. **Short rule labels.** Today the VIM Flex page builds them; they belong in the requirement file
   (known fault 7 in `docs/model-check-screen`).
5. **Per-document counts in the result.** The result file gives counts per element type for the whole model,
   not per Revit document, so a firm's share cannot be worked out.
6. **Fixed, broken, wrong and new-element counts.** The result file lists failing elements per model rule
   only, not per required value, and does not tell a wrong value from an empty one.

Items 1 to 4 are an addition to the requirement file (Builder). Items 5 and 6 are an addition to the result
file (checker). With both, a small script turns a folder of results into this series, with no judgment calls.

## Element results: `openbep-compliance-elements` 0.1 (draft)

The page `/compliance/model/` shows where in one model version the plan is not followed, down to the single element and the single required value, and colours the model in 3D. It reads one file and has no project-specific code.

```
format            "openbep-compliance-elements"
formatVersion     "0.1"
example, note     optional: true and a sentence when the file is an example
project.name
model             name, url (https link to a .vim file, optional), format "vim", sizeMB, workshared
version           label, date
checks[]          1 to 30. id, label, icon, help, parameter (the Revit parameter name),
                  on: "type" | "instance" | "element" (where the value is fixed)
columns           always ["id","model","workset","category","family","type","level","required","filled"]
strings[]         every name used once
elements[]        one row per element, in the order of columns:
                  id       the Revit element id
                  model to level   positions in strings ("" means none)
                  required bit i is 1 when check i asks something of this element
                  filled   bit i is 1 when that value is filled (never 1 where required is 0)
```

Rules the page applies, the same for every file:

- Compliance of any group is filled bits over required bits.
- An element is green when every required value is filled, yellow at half or more, red below half, grey when nothing is required of it by the check being looked at. The lists and the 3D view use the same four colours.
- Levels: model, workset, category, family, type, element, required value. Or level (storey) first. A level with a single group is passed through.
- For a check fixed on the type, the page counts the types to fill and the elements that fixes.
- Without `model.url` the lists still work; there is no 3D view.

The 3D view is VIM's open web viewer (github.com/vimaec/vim-web, MIT), loaded from a public CDN only when the model is shown. The example file is built once by `compliance/model/tools/build_residence.js` from real values in VIM's public sample model. Open gaps: a real file needs VIM Server (or another checker) to export one row per element, which the result file 1.1 does not carry yet. One file is one version; change over time per element is not in the format yet.

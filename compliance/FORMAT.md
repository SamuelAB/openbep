# Compliance series: one file, any plan, no custom screen

OpenBEP writes the plan. A checker (VIM, or any other platform) checks the model. This folder shows how the
results are shown over time, and fixes the contract so that **a different plan never needs a different screen**.

The page at `/compliance/` contains no project name, no goal, no firm, no rule and no milestone. It reads all of
them from one file. A VIM Flex page, or any other screen, can read the same file and follow the same rules.

- Format name: `openbep-compliance-series`, version `0.1` (draft).
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
model           { server, project, source, schedule, cadenceDays (7 when absent) }
valueChecks[]   { id, goal, label }
rules[]         { id, goal, label, limit, unit, limitText, direction }
                direction: lowerIsBetter | higherIsBetter
versions[]      in order
  order, label, tag, created, milestone (the one in force), uploadedBy[] (role codes)
  state         checked | notChecked (then: reason)
  elementCount
  byRole        { CODE: { elements, required, met, uploaded, elementsAdded, valuesFixed, valuesBroken } }
  valueChecks   { checkId: { CODE: [required, met] } }      adds up to byRole
  rules         { ruleId: { value, pass } }
```

- **required** = the number of values the plan asks for at the milestone in force (elements × the parameters
  required for their element type). **met** = how many hold a real value, by the requirement file's own
  definition of a real value.
- **valuesFixed** = values that did not meet the plan in the previous version and do now. **valuesBroken** = the
  reverse. Elements are matched between versions by document name + Revit element id.
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

## What every screen shows the same way

1. Summary: overall compliance, model rules met, forecast at the milestone, versions checked.
2. Overall compliance over time, with the stepped target, milestones, holidays and unchecked versions.
3. One row per role: compliance, pace, needed pace, uploads. One square per role per week.
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
6. **Fixed and broken counts.** The result file lists failing elements per model rule only, not per required
   value.

Items 1 to 4 are an addition to the requirement file (Builder). Items 5 and 6 are an addition to the result
file (checker). With both, a small script turns a folder of results into this series, with no judgment calls.

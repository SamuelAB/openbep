# What similar dashboards do, and what this page took from them

Read on 2026-11-23. Public pages only. Each row names what the source says and what changed on
`/compliance/` because of it. Nothing here is a claim about how good those products are.

| Source | What it does | Taken into round two |
|---|---|---|
| SonarQube, quality gates ([docs](https://docs.sonarsource.com/sonarqube-server/9.8/user-guide/clean-as-you-code)) | One gate, "a set of conditions that tells you whether or not your project is ready for release". | **The gate** at the top of the page: would this version pass the milestone, and which of the plan's conditions fail. |
| SonarQube, new code (same page) | Standards are enforced on new code: "New code is the part of the codebase you can act on most efficiently." | **New elements arrive filled**: of the values required on elements a firm just added, the share already filled. |
| Codecov, patch and project coverage ([docs](https://docs.codecov.com/docs/commit-status), [FAQ](https://docs.codecov.com/docs/frequently-asked-questions)) | Two numbers: coverage of the whole project, and coverage "ONLY of the code that has been changed". | Same split: overall compliance beside the compliance of what was added this period. |
| Plannerly Verify ([page](https://plannerly.com/verify/)) | "every value on every model is marked correct, incorrect or missing". | **Wrong or empty**: values that do not meet the plan are split into wrong (a placeholder, a value not allowed) and empty. |
| Plannerly Verify (same page) | "Filter it by milestone, team, team member, status or work package, so each person sees the part they're accountable for." | **Firm filter**: the whole page narrowed to one firm. |
| Plannerly Verify (same page) | "shows what passes, what fails and who owns the fix"; a PDF report that can "go out every week". | **Where the gap is**: the largest shortfalls by check and by firm. **Print this version**, and an address that reopens the same version and firm. |
| Autodesk Model Checker for Revit with Power BI ([support article](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/How-to-create-Power-BI-Dashboard-for-model-checker-results-of-Autodesk-Interoperability-Tools.html), [AU class](https://www.autodesk.com/autodesk-university/class/Automatic-Checking-Your-BIM-360-Models-2020)) | Check results ("pass/fail, counts, or lists") exported to a Power BI template that the team loads and refreshes. | Nothing copied. It confirms the reason for one fixed data format: a template that must be rebuilt or re-pointed for each project is the thing to avoid. |
| Own addition, no source | Draw what is required and what is filled as two lines, so that a plan asking for more is not mistaken for lost progress. | **Values: required and filled** view of the main chart. It shows why the percentage drops after a milestone: the plan asks for more. |

## Not taken, and why

- **A single letter grade (A to E).** It hides which condition failed. The gate lists the conditions instead.
- **3D colouring of elements.** That belongs in the checker (VIM Flex shows failing elements in 3D). OpenBEP
  writes the plan and does not check or display models.
- **Per-person breakdown.** The plan names firms and roles. Ranking individuals is not a use the plan asks for.
- **Issue boards.** Assigning and tracking issues is the common data environment's job.

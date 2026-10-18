# CivicConnect — Final Release Evidence Register

**Purpose:** A concise record of the final submission version, release decision and remaining evidence. The [PED](../../CivicConnect_PED_M3.md) remains the primary engineering document.

## Final repository version

- **Repository:** https://github.com/GhostBranch404/CivicConnect
- **Documentation branch:** `docs/ped-v4-final`
- **Submission commit SHA:** Record the final full commit SHA after all changes are committed.
- **Assessed branch/tag:** Confirm the exact branch or tag to be assessed.
- **Azure app:** https://civicconnect1-gwasb9d4guchefe9.southafricanorth-01.azurewebsites.net/
- **Application host:** Azure App Service, `civicconnect1`.
- **Database provider:** Aiven for MySQL hosts separate Staging and Production databases.

This documentation branch does not trigger deployment. The current Azure workflow deploys from `main`; do not describe this branch as deployed unless a workflow run confirms it.

## Final release / go-live decision

**Recommendation: Staging / conditional no-go for production.**

The team reports that the deployed site is accessible and database connectivity/persistence checks succeeded. The earlier Azure 404 from 15 October 2026 was later reported as resolved, so it is historical rather than the latest known state. The exact deployed commit and dated, repeatable evidence for current route and database checks still need to be recorded.

Production approval is withheld until the team can show:
- current homepage, health endpoint and representative API route working on the intended deployed commit;
- tested server-side authentication and authorization (login currently returns a user profile but does not issue an authentication token);
- automated test results, including relevant database-backed and failure-path tests;
- verified Aiven TLS, encryption-at-rest, backup and restore configuration;
- tested rollback/concurrency paths and evidence that related history/log writes remain consistent;
- remaining Must-have requirements met or explicitly justified and accepted as deferred.

This is a credible staging submission, not a claim of production readiness.

## M1 scope-to-final delivery reflection

The final percentage must be calculated against the **approved M1 acceptance criteria**, not by counting headings, code files or later-added features. For each applicable criterion, record evidence and assign 1.0 (fully met), 0.5 (partially met), or 0.0 (unmet). Exclude criteria removed by an approved scope change only where that change is documented and traceable.

**Formula:** `Scope completion (%) = (sum of completion values / number of applicable approved M1 acceptance criteria) × 100`

| Approved M1 acceptance criterion | Final implementation evidence | Completion value (1 / 0.5 / 0) | Notes / approved scope change |
|---|---|---:|---|
| Copy each applicable criterion from the approved M1 baseline here | Link to code, test result or demo evidence | — | — |
| **Total** | Number of applicable criteria: **not yet counted** | Sum: **not yet calculated** | Percentage: **must be calculated before submission** |

The criterion-by-criterion calculation is intentionally not invented in this branch. Complete it before submission. A requirement or test method is not proof that an acceptance criterion passed.

### Reflection prompts for the PED

- **Delivered from M1:** identify original Must-have outcomes implemented and demonstrated.
- **Partially delivered:** state what works and what acceptance evidence is missing.
- **Not delivered / deferred:** name the feature, why it was deferred and its stakeholder impact.
- **Scope changes:** link the approval/change record and explain whether it changed the completion denominator.
- **Main lesson:** explain one concrete change in the team's understanding between M1 and final delivery.

## Evidence index and status

| Evidence area | Repository evidence / source | Status and honest interpretation |
|---|---|---|
| Architecture and design | `src/docs/adr/ADR-001_architectural_style.md`, `ADR-002_Tech_stack_and_persistence.md`, `ADR-003_data_persistence.md`, `ADR-004_hosting_and_release.md` | Decision records exist. Defend the layered monolith as a fit for team size and delivery window; discuss boundary and testing trade-offs. |
| Database design | `src/docs/database-design.md`, SQL migrations, ERD image | Schema and persistence approach documented. Aiven hosts both environments. Keep credentials private. |
| UI and wireframes | `src/docs/ui-design-decisions.md`, `src/docs/wireframes/` | Design decisions and wireframes documented. Analytics/reporting is still described as placeholder functionality. |
| Email registration | `src/CivicConnect/Controllers/AuthController.cs`, `Tests/CivicConnect.Tests/Integration/AuthApiIntegrationTests.cs`, README | Code and test cases exist. Capture actual test output and redacted inbox receipt before claiming live delivery is verified. |
| Password reset | Auth controller, password-reset service, `src/CivicConnect/wwwroot/js/app.js` | Code-based reset flow exists in source. Capture automated results and end-to-end email evidence. |
| Unit/integration/persistence testing | `Tests/CivicConnect.Tests/`, `.github/workflows/ci.yml` | Test source and CI workflow exist. CI currently targets `main` and `dev`, not this branch. Record actual run results. |
| Azure deployment | `.github/workflows/main_civicconnect1.yml`, live app link above | Workflow deploys on `main` or manual dispatch. Record exact deployed SHA and successful run. |
| Database/persistence | Team reports successful connectivity and persistence checks | Team-reported, not independently rerun in this documentation change. Attach date, environment, operation and outcome. |
| Security / RBAC | PED security/RBAC sections and auth code | Login does not issue an authentication token. UI role navigation is not a replacement for server-side authorization. |
| AI use | PED AI Usage Register | Retain actual tools/uses and human review. Do not claim AI-generated code passed tests unless results are recorded. |
| Risks, defects and technical debt | PED risk register, ADRs and release limitations | Distinguish resolved historical issues from open release risks; link evidence for closed high-priority items. |

## Final evidence checklist

- [ ] Add the final full commit SHA and identify the exact assessed branch/tag.
- [ ] Confirm the current deployed commit and successful Azure workflow run.
- [ ] Retest the homepage, `/health` and a representative API route; save date and outcome.
- [ ] Save database evidence for the intended Aiven environment without connection strings or secrets.
- [ ] Run `dotnet restore CivicConnect.sln`, `dotnet build CivicConnect.sln --configuration Release`, and `dotnet test CivicConnect.sln --configuration Release`; record pass/fail/skip counts and environment.
- [ ] Record transaction rollback and concurrent-update/conflict test results.
- [ ] Verify TLS, encryption-at-rest, backups, restore process, retention and provider plan/cost.
- [ ] Verify email registration and password reset end to end with redacted evidence.
- [ ] Verify server-side role restrictions and negative access cases, or state clearly that production use is not approved.
- [ ] Complete the M1 acceptance-criteria worksheet and calculate the scope-completion percentage.
- [ ] Update the final RTM and risk/defect/decision/AI registers to reflect actual evidence, not planned work.
- [ ] Rehearse the demo and each team member's individual defence.

## Suggested final demonstration flow

1. **Product demonstration:** show the visitor dashboard, registration/email confirmation, sign-in, request submission, request tracking/history and staff workflow that is genuinely available.
2. **M1 scope versus final delivery:** compare approved acceptance criteria with working evidence and identify partial/deferred requirements.
3. **M3 issues and fixes:** explain the Azure 404 as a historical issue that was later reported resolved; show what evidence confirms the current deployment.
4. **Engineering evidence:** show architecture/ADRs, database design and migrations, test results, RTM, risk register and release decision.
5. **Conclusion:** state what the system can currently demonstrate, its limitations and why the release decision remains staging/conditional no-go until open checks close.

Each team member should be ready to explain their own contribution, trace one requirement through implementation and test evidence, defend one engineering decision, and describe one remaining limitation or risk.

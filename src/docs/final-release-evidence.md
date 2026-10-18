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



## Defect and technical-debt register

| ID | Type | Current state | Impact | Next action |
|---|---|---|---|---|
| DEF-01 | Deployment defect | Historical Azure 404 on 15 October 2026; later reported resolved by the team | A broken homepage/API would block demonstration and use; the current release commit still needs traceable proof | Save the current deployed SHA/workflow run and retest homepage, health endpoint and one API route. |
| DEF-02 | Security limitation | Login currently returns a user profile without issuing an authentication token | The application cannot claim a complete authenticated session or production-grade role enforcement on that basis | Establish trusted caller identity and test unauthenticated, wrong-role and spoofed-actor calls. |
| TD-01 | Deferred functionality | Reports / Analytics pages are documented as placeholders | Original management reporting scope is only partially met | Implement and verify the required reports or record an approved scope deferral and stakeholder impact. |
| TD-02 | CI/deployment workflow debt | CI triggers on main and dev; Azure deployment triggers on main or manual dispatch | Changes on this documentation branch are not automatically tested/deployed by those workflows | Run tests deliberately for the assessed SHA and record the run; workflow trigger changes should be reviewed separately. |
| TD-03 | Evidence debt | No final test-result pack or M1 scope-completion calculation is attached yet | Completion and release claims are difficult to independently audit | Complete the scope worksheet, run the suite and attach dated evidence before final hand-in. |
| TD-04 | Development configuration risk | Docker Compose includes development-only example credentials | Risk if these are reused in shared or hosted environments | Keep them disposable/local, never reuse them for Azure/Aiven, and move shared use to environment-supplied settings. |

## Final decision log snapshot

| Decision | Current decision | Trade-off / open condition |
|---|---|---|
| Application hosting | Azure App Service civicconnect1 | Simple managed hosting and GitHub Actions integration; confirm exact deployed commit. |
| Database hosting | Aiven hosts both Staging and Production MySQL databases | Environments stay separate; TLS, encryption, backup/restore, plan limits and costs still require provider-side verification. |
| Persistence | MySQL + Dapper + MySqlConnector with version-controlled SQL migrations | Explicit SQL and transaction control; the team must retain rollback/concurrency test evidence. |
| Architecture | Controller → Service → Repository layered monolith | Suitable for this team and scope; boundaries need review to avoid coupling. |
| Authentication/release posture | Do not approve production until identity/session and server-side authorization are evidenced | Login does not currently issue an authentication token. This remains a release-blocking limitation unless addressed or explicitly accepted by the assessment authority. |

# CivicConnect — Final Release Evidence Register

**Purpose:** A concise record of the final submission version, release decision and remaining evidence. The [PED](../../CivicConnect_PED_M3.md) remains the primary engineering document.

## Final repository version

- **Repository:** https://github.com/GhostBranch404/CivicConnect
- **Documentation branch:** `docs/ped-v4-final`
- **Submission commit SHA:** Record the final full commit SHA after all changes are committed.
- **Suggested assessed branch:** `docs/ped-v4-final` (this branch was created from `staging`; assess this branch only if the team intends it to be the submitted version).
- **Azure app:** https://civicconnect1-gwasb9d4guchefe9.southafricanorth-01.azurewebsites.net/
- **Application host:** Azure App Service, `civicconnect1`.
- **Database provider:** Aiven for MySQL hosts separate Staging and Production databases.

This documentation branch does not trigger deployment or the configured CI workflow. The current Azure workflow deploys from `main`, while CI targets `main` and `dev`. Do not describe this branch as deployed or tested by those workflows unless a run confirms it.

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

| Approved M1 acceptance criterion | Final implementation evidence to assess | Completion value (1 / 0.5 / 0) | Notes / approved scope change |
|---|---|---:|---|
| FR-001 — Service request submission | Request API, service/repository, SQL migration; see final RTM in the PED | To assess | Verify create, persistence and retrieval on the intended database. |
| FR-002 — Request tracking and history | Retrieval routes, requester history UI and RequestHistory | To assess | Verify data reloads and lifecycle history is visible. |
| FR-003 — Request assignment | Staff queue and assignment-related UI/data structures | To assess | Verify assignment persistence and authorised access. |
| FR-004 — Status transition and resolution | Status update route, comments/history and reject outcome | To assess | Verify valid/invalid transition and failure-path behaviour. |
| FR-005 — Dashboard oversight | Management dashboard; reports/analytics are described as placeholders | To assess | Assess the original acceptance criteria carefully; full reporting may be partial/deferred. |
| FR-006 — User authentication | Registration/login endpoints and password hashing | To assess | Login does not issue an authentication token; assess against the approved M1 criterion and do not overstate. |
| FR-007 — Role-based access | Role-aware UI and Admin service checks | To assess | Verify trusted caller identity and server-side negative access tests. |
| FR-008 — Request comments and history | Comment/history UI and data model | To assess | Verify saved comment/history and responsible actor/time. |
| FR-009 — Application logging | Developer Logs page, middleware/repository and email-failure logging | To assess | Verify expected rows and that sensitive values are not logged. |
| NFR-001 — Role-based access control | Auth/role-related service logic | To assess | Security acceptance test evidence required. |
| NFR-002 — Performance threshold | Load-test source and indexes | To assess | Use an actual measured result, not the presence of a test file or green build. |
| NFR-003 — Audit trail logging | RequestHistory and DeveloperLogs write paths | To assess | Verify related records persist after a real operation. |
| NFR-004 — Data integrity/rollback | Constraints, transaction paths and rollback test case | To assess | Record database-backed failure-path results. |
| NFR-005 — Concurrency control | Version-based update and stale-update test case | To assess | Record conflict response and final stored state. |
| NFR-006 — Maintainability | Controller → Service → Repository separation | To assess | Record build result and architecture review. |
| **Total** | Applicable criterion count: **confirm against the approved M1 baseline** | Sum: **calculate after assessing rows** | Percentage: **must be calculated before submission** |

The criterion-by-criterion calculation is intentionally left unscored because assigning values without the team's final acceptance-test outcomes would invent a result. Before submission, the team must apply the rubric against the approved M1 baseline, record the evidence for each applicable criterion, calculate the percentage and state any approved scope changes. A requirement, UI screen or test method is not proof that an acceptance criterion passed.

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
| UI and wireframes | `src/docs/ui-design-decisions.md`, `src/docs/wireframes/` | Design decisions and wireframes documented. The screen-status table describes implementation, not automatic acceptance-test proof. Analytics/reporting is still described as placeholder functionality. |
| Email registration | `src/CivicConnect/Controllers/AuthController.cs`, `Tests/CivicConnect.Tests/Integration/AuthApiIntegrationTests.cs`, README | Code and test cases exist. Capture actual test output and redacted inbox receipt before claiming live delivery is verified. |
| Password reset | Auth controller, password-reset service, `src/CivicConnect/wwwroot/js/app.js` | Code-based reset flow exists in source. Capture automated results and end-to-end email evidence. |
| Unit/integration/persistence testing | `Tests/CivicConnect.Tests/`, `.github/workflows/ci.yml` | Test source and CI workflow exist. CI currently targets `main` and `dev`, not this branch. Record actual run results. |
| Azure deployment | `.github/workflows/main_civicconnect1.yml`, live app link above | Workflow deploys on `main` or manual dispatch. Record exact deployed SHA and successful run. |
| Database/persistence | Team reports successful connectivity and persistence checks | Team-reported, not independently rerun in this documentation change. Attach date, environment, operation and outcome. |
| Security / RBAC | PED security/RBAC sections and auth code | Login does not issue an authentication token. UI role navigation is not a replacement for server-side authorization. |
| Presentation / individual defence | `src/docs/final-presentation-and-defence.md` | Rehearsal outline for the demo, scope reflection, engineering evidence and individual questions. It is a speaking aid, not proof of implementation or test results. |\n| AI use | PED AI Usage Register | Retain actual tools/uses and human review. Do not claim AI-generated code passed tests unless results are recorded. |
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

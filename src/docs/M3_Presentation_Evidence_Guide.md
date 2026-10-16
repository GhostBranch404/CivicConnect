# CivicConnect M3 Presentation Evidence Guide

**Purpose:** A short screen-sharing checklist mapped to the SEN381 M3 rubric. Use this alongside the PED v3, not as a replacement for the PED, RTM, risk/defect registers or actual test output.

**Important:** Present the exact release-candidate commit/tag that the team has agreed to demonstrate. The `staging` branch contains the documentation baseline, but the current Azure deployment workflow deploys from `main`. Do not imply that a staging-only commit is the deployed version.

## 1. Quick links for screen sharing

- [Repository](https://github.com/GhostBranch404/CivicConnect)
- [README and setup](../../README.md)
- [PED v3 / M3](../../CivicConnect_PED_M3.md)
- [Database design](database-design.md)
- [ADR-001: architecture](adr/ADR-001_architectural_style.md)
- [ADR-002: technology and persistence](adr/ADR-002_Tech_stack_and_persistence.md)
- [ADR-003: data persistence](adr/ADR-003_data_persistence.md)
- [ADR-004: hosting and release](adr/ADR-004_hosting_and_release.md)
- [SQL migration 001](../../src/CivicConnect/database/migrations/001_initial_schema.sql)
- [CI workflow](../../.github/workflows/ci.yml)
- [Azure deployment workflow](../../.github/workflows/main_civicconnect1.yml)
- [GitHub Actions runs](https://github.com/GhostBranch404/CivicConnect/actions)
- [Pull requests](https://github.com/GhostBranch404/CivicConnect/pulls)
- [Test project](../../Tests/CivicConnect.Tests/)

## 2. Rubric-to-evidence checklist

| Rubric area | What to show live | Current repository pointers | Do not claim without proof |
|---|---|---|---|
| **A. Implementation & M2 baseline (5)** | Release commit; request create/read/status update; controller → service → repository → MySQL flow; explain significant deviations. | `src/CivicConnect/Controllers/RequestController.cs`, `Services/RequestService.cs`, `Data/RequestRepository.cs`, models, SQL migration, ADRs. | A route works in Azure unless you demonstrate it against the release candidate. |
| **B. Collaboration & configuration (4)** | Commit history, feature/fix branches, PR descriptions/reviews, configuration without showing secrets. | [Branches](https://github.com/GhostBranch404/CivicConnect/branches), [PRs](https://github.com/GhostBranch404/CivicConnect/pulls), [CI workflow](../../.github/workflows/ci.yml). | Two independent approvals or protected-main rules unless the PR/repository settings visibly prove them. |
| **C. Automated verification & CI (6)** | Open an actual Actions run; show build/test outcome, relevant assertions and a failure that blocks progression. | [Actions](https://github.com/GhostBranch404/CivicConnect/actions), `Tests/CivicConnect.Tests/RequestServiceUnitTests.cs`, `RequestAdditionalUnitTests.cs`, `RequestPersistenceTests.cs`, `Integration/RequestApiIntegrationTests.cs`. | A test file is not a test result. Show pass/fail/skip counts and commit SHA. |
| **D. Black-box/integration/system (6)** | Input partitions/boundaries where relevant, expected vs actual, API positive and negative cases, and critical E2E journeys. | `Black-boxTestCase.cs`, `White-boxTestCase.cs`, `Integration/RequestApiIntegrationTests.cs`, persistence and service tests. | Full E2E coverage or unauthorised-case coverage unless an actual test/run demonstrates it. |
| **E. Quality/defects/traceability (4)** | One requirement traced through acceptance criteria → code → test → execution result → defect/status. Show risk and technical-debt entries. | PED v3, database design, ADRs, tests, GitHub issues if used. | Complete RTM/defect/debt/AI registers unless the current linked artefacts actually contain them. |
| **F. Performance/staging (3)** | Explain workload, environment, duration, request rate, latency/error measurements, target/baseline and limitations. Demonstrate the release candidate. | `Tests/CivicConnect.Tests/LoadTest.cs`, Azure deployment workflow, app URL. | Performance results from the script alone. Run it, capture output and label whether staging is real or simulated. |
| **G. Release review/residual risk (2)** | Release Evidence Summary; READY / CONDITIONALLY READY / NOT READY decision; blockers and residual risks. | PED v3, ADR-004, current Actions run and real staging/API/database evidence. | READY without evidence for the exact demonstrated commit. |

## 3. Minimum test baseline from the brief

Before recording, count actual meaningful cases and group them by type. The brief requires:

- At least **15 meaningful cases overall**.
- At least **5 unit/component**, **5 black-box functional**, **3 API/integration**, and **2 E2E/system** cases (overlap is allowed when justified).
- At least **3 negative/failure/unauthorised cases**.
- At least **5 high-priority requirement/risk cases**.
- At least **2 justified black-box techniques**.
- One separate small performance/load exercise.
- Feasible static/code-quality and security/dependency/secrets checks.

Do not assume the minimum is satisfied just because similarly named test files exist. Make a case matrix and link each case to the actual test method and recorded execution result. Identify gaps honestly.

## 4. Suggested 25–35 minute running order

1. **Introductions and release ID (1 min):** each member shows face, full name and speaks.
2. **M1/M2 → implementation (4 min):** scope, architecture, controlled changes, one requirement trace.
3. **Git and collaboration (3 min):** branches, meaningful PR, reviews, secrets/config.
4. **CI and automated tests (4 min):** actual run, result counts, what a failing gate does.
5. **Functional/API/integration tests (5 min):** show expected vs actual and negative cases.
6. **Working application and database (5 min):** create/reload request, update status, show history/logs if verified.
7. **Performance (2 min):** workload and captured measurements, limitations.
8. **Risk and release decision (3 min):** blockers, residual risk, readiness category.
9. **Individual defence (3–5 min):** each person defends one requirement, one decision, quality evidence, failure/risk and readiness.

Keep screen text readable and the audio clear. Everyone must appear on camera and make a substantive contribution.

## 5. Evidence that must be checked before recording

- [ ] Agree and record exact release commit/tag.
- [ ] Confirm the deployed app corresponds to that commit; current Azure workflow deploys from `main`.
- [ ] Run the test suite and record pass/fail/skip totals, environment and Actions URL.
- [ ] Confirm the database-backed tests use a test database with migrations applied.
- [ ] Demonstrate one real request create/retrieve and one status update; verify history/logging if claimed.
- [ ] Run the performance exercise and save actual output.
- [ ] Confirm two independent PR approvals and branch protection from real GitHub evidence.
- [ ] Complete the RTM/defect/technical-debt/AI usage and Release Evidence Summary links required by the brief.
- [ ] Check all links open for the assessor; never expose secrets in screen share.
- [ ] Test the full recording playback and submit an accessible video link or complete playable ZIP.

## 6. Honest current caveat

The repository contains source code, migration scripts, test methods and workflows. That is useful engineering evidence, but it is not by itself proof of successful execution. The latest earlier deployment diagnosis reported 404 responses for `/` and `/swagger`; recheck the current deployed release before presenting it as working. If a criterion is not verified by recording time, identify it as a limitation and explain the mitigation rather than fabricating evidence.


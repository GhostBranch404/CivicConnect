# CivicConnect — Final Presentation and Individual Defence Guide

**Purpose:** A practical guide for the final demonstration and individual engineering questions. Use the PED v4.0 Final and the Final Release Evidence Register as the authoritative records. This guide is a rehearsal aid, not extra proof that a feature passed testing.

## Before presenting

- Open the exact branch/tag that will be assessed and record the final full commit SHA.
- Confirm the demo URL loads. Keep a local build or screenshots ready in case internet access fails.
- Use a test account and test data. Do not show real passwords, confirmation codes, API keys, connection strings, personal information or production records.
- Have the PED, final RTM, database design, UI design decisions, ADRs, test output and release evidence register open in separate tabs.
- Do not present this documentation branch as deployed unless a workflow run proves that the deployed artifact contains this commit. The current Azure workflow deploys from `main`, while CI targets `main` and `dev`, not this branch.
- Rehearse a clean, short journey. If any step fails, state what happened and show the related limitation/evidence rather than improvising a claim.

## Suggested 28-minute presentation

### Product demonstration — about 6 minutes

1. Introduce CivicConnect as a community service-request platform: people can submit a request and follow its progress; staff can work on request status and assignments; oversight features are intended for management.
2. Show the public landing/dashboard and explain what the service is for before signing in.
3. Demonstrate registration and the email-confirmation flow only if the configured email delivery works during rehearsal. Do not show a real code or address on the recording.
4. If a confirmed test account is available, submit a sample request and show its ticket/status in the UI. Show history after a status change only if the operation succeeds.
5. Briefly show staff/admin/developer screens that are actually accessible in the demo environment. State clearly that Reports/Analytics are placeholder pages and that login does not currently issue an authentication token.
6. Close by showing where a user can find request status/history and where technical logs appear, without exposing private details.

### Original M1 scope versus final delivery — about 6 minutes

- Start with the approved M1 scope and the original acceptance criteria, not with newly added features.
- Group the criteria into fully met, partially met and unmet/deferred. Show the linked requirement, implementation location and acceptance evidence for each group.
- Use the formula in the PED/release register: `scope completion (%) = sum of completion values ÷ number of applicable approved M1 acceptance criteria × 100`, with 1.0 for fully met, 0.5 for partially met and 0.0 for unmet.
- Calculate the percentage only after assessing the approved M1 baseline and documenting approved scope changes. Do not invent a percentage or treat code presence as proof of acceptance.
- Explain what was deferred and its impact. Reports/Analytics are currently documented as placeholders; authentication and role enforcement still have release limitations.

### M3 fixes and issues — about 4 minutes

- Explain the Azure 404 issue from 15 October 2026 as a historical defect. The team later reported that the site was accessible after the fix.
- Show current evidence if available: deployed commit SHA, workflow run, homepage/health/API result and a database-backed operation.
- Distinguish confirmed evidence from a team-reported result. If a current result or test output is missing, say so and identify the exact evidence still needed.
- Explain the value of keeping the older M1/M2/M3 design material as history while making the v4.0 final status authoritative.

### Final engineering evidence — about 4 minutes

- Walk through the final RTM: requirement → implementation location → verification approach/result → residual limitation.
- Explain the three-layer architecture (Controller → Service → Repository) and why a layered monolith fits the team's size and deadline better than microservices.
- Show the MySQL schema/migrations and the role of Dapper/MySqlConnector. Explain why related writes use transactions and why version-based concurrency handling matters; only claim these paths passed if you have actual results.
- Point out the CI and Azure workflow branch triggers, current test results and the final release decision.
- State that Aiven hosts both Staging and Production databases, while Azure App Service hosts the application.

### Conclusion — about 4 minutes

Summarise the user problem, the most useful outcomes delivered, one meaningful engineering decision, the largest remaining risk and the release recommendation. A defensible ending is better than calling the product production-ready without the evidence to support it.

## Individual engineering defence

Each person should be ready to answer these questions without reading a paragraph verbatim:

- **What did you personally implement or document?** Name the relevant files, decisions and one concrete change. Do not claim a teammate's work as your own.
- **Trace one requirement.** Identify the requirement, the code/UI/database component that implements it, the test or demonstration used to verify it, and its remaining limitation.
- **Why this design?** Explain the decision, one realistic alternative, why it was not selected for this project, and the trade-off accepted.
- **How did you verify it?** State the actual test method/command, the observed result and the environment. If you only inspected the code, say that.
- **What could fail?** Discuss one risk such as email delivery, database connectivity, transaction rollback, stale updates, unauthenticated access, backup/restore or a missing report.
- **What would you do next?** Give the highest-impact follow-up and how you would prove it worked.

### Suggested individual focus

| Team role / area | Useful evidence to explain |
|---|---|
| Database and persistence | ERD, SQL migrations, schema constraints, Dapper repository, RequestHistory/DeveloperLogs, transactions and concurrency evidence, Aiven environment separation. |
| API and backend | Controller → Service → Repository boundaries, request validation, API contracts, authentication/email flows, failure handling and API/integration test results. |
| UI and user journeys | Wireframes, colour/type/navigation choices, responsive states, request/history journeys, role-aware screens, user management and known Reports/Analytics limitations. |

Use this table to prepare, not to restrict anyone to one area. Every team member should understand the overall architecture, release decision and the limitations described in the final PED.

## Final release decision: how to explain it

The current recommendation is **staging / conditional no-go for production**. The team reports that the live site is accessible and database connectivity/persistence checks succeeded, but this branch does not independently reproduce those checks. Production readiness still requires traceable deployment evidence, actual test-run output, reliable authentication/session behaviour and server-side authorization verification, and provider-side checks for TLS, encryption, backup and restore.

If asked whether the project is complete, distinguish three things: what is present in source, what has been demonstrated, and what has passed an acceptance test with saved evidence. They are not interchangeable.

## Final 10-minute rehearsal checklist

- [ ] Open the intended final branch/tag and note its full commit SHA.
- [ ] Load the live app and rehearse the exact demo using non-sensitive test data.
- [ ] Verify email confirmation before deciding to demo it live.
- [ ] Prepare a fallback if the live app or external email service is unavailable.
- [ ] Confirm all shown features really work in the environment used.
- [ ] Have the final RTM, Aiven/Azure hosting decision, test results and open risks ready.
- [ ] Complete the M1 completion calculation from the approved baseline; do not leave placeholders in the submitted copy.
- [ ] Make sure every team member can explain one requirement, one design decision, one test result and one limitation.

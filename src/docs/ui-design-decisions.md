# CivicConnect – UI design decisions


This page explains how the CivicConnect front end looks and behaves, and more importantly *why* we built it that way. It covers the colours, type and components we settled on, how navigation works for each role, the state of each screen, and what we still want to improve.

The UI is a single page served by the ASP.NET app from `src/CivicConnect/wwwroot` (`index.html`, `css/styles.css` and `js/app.js`). We started from the seven [wireframes](wireframes/) and kept to them fairly closely, changing things only where the real app needed something the wireframes didn't cover (sign-up, request history, rejecting requests, user management and logs).

---

## What changed 

A few big pieces of work shaped the UI as it is now:

- **We implemented the main UI journeys from the wireframes** and connected the implemented workflows to API calls. Reports and Analytics remain placeholder pages, as recorded below; they should not be presented as completed features.
- **Deployment issue — historical status.** Azure returned 404 for `/` and `/swagger` on 15 October 2026. The team later reported the issue resolved after addressing the startup/project-file problem and build artifacts. Record the deployed commit and current route checks in the [final release register](final-release-evidence.md) before treating the fix as independently verified.
- **We removed all demo data.** There are no hard-coded accounts or sample requests any more. Everything comes from the database, and the only thing the browser remembers is who is signed in.
- **We added a "Rejected" outcome.** Staff can reject a request, but they have to give a reason, and the requester sees that reason. "My Requests" is split into *Current Requests* and *Request History* so finished work doesn't clutter the list.
- **We added user management for Admins and Devs.** They can search users, promote someone or remove their access, and add workers. Every one of those changes needs a *second* admin to approve it before it happens.
- **We added a Dev-only Logs page** that shows server crashes, access-denied attempts, browser errors and failed confirmation emails, so we can debug the live site without digging through Azure.
- **We redesigned the dashboard** to feel more welcoming: a greeting, a banner with a clear "Submit a request" button, stat cards with icons, recent requests and shortcuts for common issues (potholes, water leaks, streetlights, waste collection).
- **Visitors see the dashboard before signing in.** If they try to submit a request or open My Requests, a small prompt asks them to sign in and then takes them back to where they were heading.
- **Sign-up is email only.** You register with your name, email and password, and we email you a 6-digit code to confirm the address. If that email can't be sent, the account simply isn't created, so nobody ends up with an account they can't use. (We dropped the SMS option entirely.)

---

## Visual design

### Colour palette

We wanted the app to feel calm and official, like something a municipality would actually use, so we built it around a deep navy and a teal. The four request states each get their own colour so you can read a list at a glance.

| Token | Hex | Where it's used |
|---|---|---|
| Brand (teal) | `#0F7A6C` | Primary buttons (Login, Submit, Approve), links, the active tab, ticket numbers and the focus ring |
| Brand soft | `#E3F3F0` | Selected states, like the chosen role card, and light highlights |
| Sidebar (navy) | `#10293D` | The navigation sidebar and the email header; gives the app its "civic" feel |
| Sidebar accent | `#4FC3B3` | The "Connect" in the logo |
| Active menu item | `#1D6D68` | Highlights the page you're on in the sidebar |
| Sidebar text | `#C9D6E2` | Menu items you're not currently on |
| Ink | `#15283A` | Main body text |
| Muted | `#627284` | Secondary text: labels, hints, dates and table headers |
| Background | `#F3F6F8` | The page behind the cards |
| Surface | `#FFFFFF` | Cards, tables, forms and pop-ups |
| Line | `#E1E6EC` | Borders and dividers |
| Pending (amber) | `#A15C00` on `#FFF2DC` | Pending status and warnings |
| In Progress (blue) | `#1F5FBF` on `#E3EDFC` | In Progress status and information |
| Resolved (green) | `#1D7A3E` on `#E1F4E7` | Resolved status and the "Completed" badge |
| Danger (red) | `#B42318` on `#FDE8E6` | Rejected status, errors, "Remove access" and overdue counts |

### Typography

We use **Inter** from Google Fonts, falling back to the system font if it doesn't load. It's very readable at small sizes, which matters in tables. Ticket numbers and log details use a monospace font (Consolas) so they line up and are easy to copy.

| Element | Size | Weight | Notes |
|---|---|---|---|
| Dashboard greeting | 28 px | 700 | "Good afternoon, Jaco" |
| Login / register title | 26 px | 700 | |
| Page title (H1) | 22 px | 700 | One per page, e.g. "My Requests" |
| Section heading (H2) | 15–18 px | 600 | e.g. "Recent requests", "Manage Users" |
| Stat numbers | 24–30 px | 700 | Tabular figures so the numbers line up |
| Body text | 14 px | 400 | Line height 1.5 |
| Form labels and buttons | 13–14 px | 600 | |
| Table headers, badges, hints | 12 px | 600 / 400 | Table headers are uppercase |

### Components

We kept a small set of building blocks and reused them everywhere, so every screen feels like part of the same app:

- **Buttons** come in default (white), primary (teal), danger (red outline) and small versions, all with 8 px rounded corners. Disabled buttons fade to 55 % opacity.
- **Inputs, dropdowns and text areas** have a thin border, 8 px corners and a teal border with a soft glow when focused.
- **Cards** are white with a light border, 10 px corners and a subtle shadow. **Tables** have uppercase grey headers, highlight on hover, and clicking a row opens the request.
- **Badges** are pill-shaped with a dot and a text label. Completed and Rejected also get a tick or a cross icon.
- **Spacing** is 32 px around the page on desktop (16 px on phones), 14 px between cards and 16–20 px inside them.

### Contrast and readability

The UI design notes record the following contrast ratios as intended measurements. Treat them as verified WCAG results only if the team can reproduce them using a contrast-checking tool and retain the output; otherwise label them as provisional and recheck before claiming a formal accessibility pass. The WCAG 2.1 AA target for normal text is at least 4.5:1:

| Colour pair | Contrast | Result |
|---|---|---|
| Body text on the page background | 13.9 : 1 | Pass (AAA) |
| Muted text on white / on the background | 4.9 / 4.5 : 1 | Pass (AA) |
| White text on teal buttons | 5.2 : 1 | Pass (AA) |
| Sidebar text on navy | 10.1 : 1 | Pass (AAA) |
| White text on the active menu item | 6.1 : 1 | Pass (AA) |
| White text on the dashboard banner | 11.4 : 1 | Pass (AAA) |
| Pending / In Progress / Resolved / Rejected badges | 4.7 / 5.2 / 4.7 / 5.6 : 1 | Pass (AA) |

A few other things we did for readability:

- **Status is never shown by colour alone.** Every badge has a text label, so it still works for colour-blind users or on a bad screen.
- **Errors are hard to miss.** They show as a red box with plain-language text right above the form, not as a tiny message somewhere else.
- **Focus is visible on inputs and role cards.** Other elements still use the browser's default outline for now (see the to-do list below).

---

## Navigation and layout

### Menus and home pages

Everyone lands on the same dashboard, whether they're signed in or not. That way the app always opens on something useful, and anyone can report an issue. After that, each role sees extra menu items for their own work, and nothing they're not allowed to use.

| Role (database value) | Sidebar menu | Home page |
|---|---|---|
| Visitor (not signed in) | Dashboard · My Requests · Submit Request (the last two ask you to sign in) | Dashboard |
| Requester | Dashboard · My Requests · Submit Request | Dashboard |
| Worker (`Staff`) | The three above + Request Queue · My Assignments · Reports | Dashboard |
| Admin (`Manager`) | The three above + Management · Request Queue · Reports · Analytics · Users | Dashboard |
| Dev (`Dev`) | Everything Admin has + Logs | Dashboard |

Detail pages sit under the list they came from: My Requests → request details, Request Queue → staff request details, Management → Add a worker. Each one has a "Back to …" link.

### Who can see what

| Page | Visitor | Requester | Worker | Admin | Dev |
|---|---|---|---|---|---|
| Dashboard | ✓ | ✓ | ✓ | ✓ | ✓ |
| My Requests / Submit Request | sign-in prompt | ✓ | ✓ | ✓ | ✓ |
| Request details (your own requests) | | ✓ | ✓ | ✓ | ✓ |
| Request Queue / staff request details | | | ✓ | ✓ | ✓ |
| My Assignments | | | ✓ | | |
| Reports | | | ✓ | ✓ | ✓ |
| Management, Analytics, Users | | | | ✓ | ✓ |
| Manage Users, approvals, Add a worker | | | | ✓ | ✓ |
| Logs | | | | | ✓ |

If someone navigates to a page outside their current UI role, the client redirects them. Some administrative API handlers also contain role-related checks, but login does not currently issue an authentication token. This is not evidence of complete authenticated server-side authorization; protected endpoints need explicit negative tests before production use.

### Titles and back links

Every page has one H1 title, and every detail page has a "Back to …" link. Menu labels match the wireframes, with one rename: the Admin page is now called **Management** in the menu, so it no longer clashes with the shared Dashboard.

### Desktop and mobile

We checked the main screens at 1280 px (desktop) and 390 px (phone): the dashboard, login, register, My Requests, Manage Users and Add a worker. On phones the sidebar turns into a slide-out menu, cards stack in a single column and filters go full width. The dashboard banner also switches to a stacked layout so the text stays readable over the photo.

---

## Screens and user journeys

The status below describes what the UI/design record says is implemented, not a claim that every journey has passed end-to-end acceptance testing. Use the final RTM and saved test output for release claims.

| Screen | Status | Notes |
|---|---|---|
| Dashboard (everyone) | Complete | Greeting, banner, totals by status, recent requests and report shortcuts. Visitors see the layout without any request data. |
| Sign-in prompt | Complete | Shown when a visitor tries something that needs an account; takes them back afterwards. |
| Login / Register / Confirm email / Password reset | Partial | Passwords are hashed, registration uses a 6-digit email confirmation code, and the UI includes a code-based password-reset flow. Login currently returns a profile but does not issue an authentication token, so a secure authenticated session and complete server-side authorization must not be claimed. |
| Submit request | Complete | Categories come from the database; every field is validated. The dashboard shortcuts pre-fill the title. |
| My Requests | Complete | Current Requests and Request History (Completed / Rejected). |
| Request details, history and comments | Complete | A timeline from RequestHistory, saved comments, and an outcome banner showing the reason. |
| Staff queue / My Assignments | Complete | Search and filters; shows who each request is assigned to. |
| Staff details, assignment and status updates | Complete | Assign, update status, resolve or reject (a reason is required). We removed Priority because there's no database column for it. |
| Management | Complete | Totals, overdue requests (older than 7 days), a category donut chart, status bars and recent requests. |
| Manage Users and approvals | Complete | The two-admin approval rule is enforced on the server. Approval notifications are written to the server log for now. |
| Add a worker | Complete | Workers and Devs are created straight away; a new Admin needs approval. |
| Users list | Complete | A read-only list with each person's role and whether they still have access. |
| Logs (Dev) | Complete | Server errors, access-denied attempts, browser errors and failed confirmation emails. |
| Reports / Analytics | Not built yet | Placeholder pages only. |

---

## How the UI behaves

- **Validation and messages.** Every form checks its fields before sending anything. If the server rejects something, the reason shows in a red box, and when something succeeds you get a short pop-up message.
- **Loading, empty and error states.** You see "Loading…" while data loads and a friendly message when there's nothing to show (like "You haven't submitted any requests yet."). If an API call fails, you get a readable error instead of a blank page.
- **Refresh and back/forward.** Every page has its own address (e.g. `#/my-requests`), so refreshing and the browser's back and forward buttons just work. The server returns the app for any non-API address, so deep links never 404.
- **Safe confirmations.** Promoting someone, removing access, approving, declining and adding users all ask "Yes / No" first. Rejecting a request needs a second click.
- **No browser pop-ups.** We use our own dialogs instead of the browser's `alert()` and `confirm()`. They look like the rest of the app and close with Esc.

---

## Why we made these choices

- **A calm civic palette.** Navy and teal feel official and trustworthy, which suits a municipal service. Giving amber, blue, green and red to the four request states means people learn them quickly and can scan a list at a glance.
- **One clear main action per screen.** The teal button is always the thing you most likely want to do. Destructive actions are red outlines, so you're less likely to click them by accident.
- **A friendly front door.** Showing the dashboard before sign-in means people can see what CivicConnect is for, and what they can report, before being asked to make an account. We only ask for an account at the moment it's actually needed.
- **Short, role-based menus.** Each person only sees what they can use, which keeps the sidebar short and matches the wireframes.
- **Accessibility from the start.** WCAG AA contrast, text labels alongside colour, real form labels, keyboard-reachable table rows and our own Esc-closable dialogs.
- **Two-admin approval for access changes.** One mistaken or malicious click can't promote someone to Admin or lock a colleague out.
- **No account without a confirmed email.** If we can't send the code, we don't create the account. That way an email address never gets stuck on an account nobody can confirm.

---

## Database migrations behind the UI

These scripts in `src/CivicConnect/database/migrations` add what the screens above rely on:

1. `002_user_accounts.sql` – adds `Users.PasswordHash` for sign-in.
2. `003_rejected_status.sql` – adds the Rejected status (4).
3. `004_admin_approvals.sql` – creates the `AdminApprovals` table for two-admin approval.
4. `005_user_registration_confirmation.sql` – adds the email confirmation columns. Verify the applied migration state against both Aiven environments separately; do not assume Staging and Production are at the same schema version just because one environment works. Apply any Production migration only through the team's controlled release procedure.



## Final submission verification notes

The password-reset flow is present in the current source: the UI calls `POST /api/auth/forgot-password`, `POST /api/auth/verify-reset-code` and `POST /api/auth/reset-password`. The service sends a 6-digit code and exchanges a valid code for a short-lived reset token. This is evidence of an implemented flow, not proof of successful live email delivery or complete security testing; retain test output and a redacted end-to-end result.

The earlier Azure 404 is a historical defect, not the latest reported status. The team reports that the site became accessible after the startup/project-file issue was fixed. The exact deployed commit and successful workflow run should still be recorded in the [Final Release Evidence Register](final-release-evidence.md). This documentation branch is not automatically deployed by the current workflow.

### Known limitations to state honestly

- Login does not currently issue an authentication token. UI role-aware navigation alone is not a security boundary; server-side authorization needs explicit verification before production use.
- Reports / Analytics are described above as placeholder pages, not completed reporting.
- The current deployment workflow targets `main`; documentation changes on `staging` do not automatically deploy.
- Treat the documented accessibility contrast ratios as verified only if measurements were actually performed; otherwise label them as intended/estimated and verify with a contrast tool.

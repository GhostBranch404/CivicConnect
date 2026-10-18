/* CivicConnect front end
 * Everything here reads and writes through the ASP.NET API, which stores it in MySQL:
 *   POST /api/auth/register, POST /api/auth/login        accounts (Users)
 *   GET  /api/users[?role=Staff]                         user lists
 *   GET  /api/categories, GET /api/statuses              reference data
 *   GET/POST /api/requests, GET /api/requests/{id}       service requests
 *   PATCH /api/requests/{id}/status                      status changes (+ RequestHistory)
 *   GET  /api/requests/{id}/history                      RequestHistory
 *   GET/POST /api/requests/{id}/comments                 RequestComments
 *   GET  /api/assignments, PUT /api/requests/{id}/assignment   Assignments
 * The only thing kept in the browser is who is signed in.
 */
(() => {
  "use strict";

  const OVERDUE_DAYS = 7; // open requests older than this count as overdue on the management view

  // ---------- Helpers ----------
  const $ = (s, el = document) => el.querySelector(s);
  const esc = s => String(s ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
  // The API returns UTC times without a zone marker; treat them as UTC.
  const toDate = s => s ? new Date(/[zZ]|[+-]\d\d:\d\d$/.test(s) ? s : s + "Z") : null;
  const fmtDate = s => s ? toDate(s).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" }) : "—";
  const fmtDateTime = s => s ? toDate(s).toLocaleString(undefined, { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" }) : "—";
  const initials = n => String(n || "?").split(" ").filter(Boolean).map(p => p[0]).slice(0, 2).join("").toUpperCase();
  const CLOSED = ["Resolved", "Rejected"];
  const isClosed = r => CLOSED.includes(r.requestStatus);
  const statusBadge = s => `<span class="badge ${s === "Resolved" ? "b-resolved" : s === "Rejected" ? "b-rejected" : s === "In Progress" ? "b-progress" : "b-pending"}">${esc(s)}</span>`;
  // Outcome shown in a requester's Request History: green "Completed" or red "Rejected".
  const outcomeBadge = s => s === "Rejected"
    ? `<span class="outcome outcome-rejected"><svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="3"><path d="M6 6l12 12M18 6L6 18"/></svg>Rejected</span>`
    : `<span class="outcome outcome-completed"><svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="3"><path d="M5 12.5l4.5 4.5L19 7.5"/></svg>Completed</span>`;
  const statusColor = n => n === "Resolved" ? "var(--resolved)" : n === "Rejected" ? "var(--danger)" : n === "In Progress" ? "var(--progress)" : "var(--pending)";
  const isOverdue = r => !isClosed(r) && (Date.now() - toDate(r.requestDate)) / 864e5 > OVERDUE_DAYS;
  const store = {
    get(k) { try { return JSON.parse(localStorage.getItem("cc." + k)); } catch { return null; } },
    set(k, v) { try { v == null ? localStorage.removeItem("cc." + k) : localStorage.setItem("cc." + k, JSON.stringify(v)); } catch { /* ignore */ } }
  };

  // Map database role names onto the three areas of the app.
  function appRole(role) {
    const r = String(role || "").toLowerCase();
    if (r === "staff") return "Staff";
    if (r === "manager" || r === "management" || r === "admin") return "Manager";
    if (r === "dev" || r === "developer") return "Dev";
    return "Requester"; // Requester, Citizen, anything else
  }
  // What each database role is called on screen.
  const ROLE_LABEL = { Requester: "Requester", Staff: "Worker", Manager: "Admin", Dev: "Dev" };
  const roleLabel = r => ROLE_LABEL[appRole(r)];
  const isAdminRole = r => ["Manager", "Dev"].includes(appRole(r));

  function toast(msg, err) {
    const t = $("#toast"); t.textContent = msg; t.className = "toast show" + (err ? " err" : "");
    clearTimeout(toast.t); toast.t = setTimeout(() => (t.className = "toast"), 3200);
  }

  // ---------- API ----------
  async function http(method, url, body) {
    let r;
    try {
      r = await fetch(url, {
        method, cache: "no-store",
        headers: {
          Accept: "application/json",
          ...(body ? { "Content-Type": "application/json" } : {}),
          ...(session.user ? { "X-CivicConnect-User": String(session.user.userId) } : {})
        },
        body: body ? JSON.stringify(body) : undefined
      });
    } catch {
      throw new Error("Can't reach the CivicConnect server. Check your connection and try again.");
    }
    const text = await r.text();
    let data = null;
    try { data = text ? JSON.parse(text) : null; } catch { /* not JSON */ }
    if (!r.ok) {
      const msg = (data && (data.message || data.Message || data.title)) || `Request failed (${r.status}).`;
      const e = new Error(msg); e.status = r.status; throw e;
    }
    return data;
  }

  const api = {
    login: (email, password) => http("POST", "/api/auth/login", { email, password }),
    register: (fullName, email, password) => http("POST", "/api/auth/register", { fullName, email, password }),
    confirm: (email, code) => http("POST", "/api/auth/confirm", { email, code }),
    resendConfirmation: email => http("POST", "/api/auth/resend-confirmation", { email }),
    users: role => http("GET", "/api/users" + (role ? `?role=${encodeURIComponent(role)}` : "")),
    categories: () => http("GET", "/api/categories"),
    statuses: () => http("GET", "/api/statuses"),
    requests: () => http("GET", "/api/requests"),
    request: id => http("GET", `/api/requests/${id}`),
    createRequest: body => http("POST", "/api/requests", body),
    updateStatus: (id, body) => http("PATCH", `/api/requests/${id}/status`, body),
    history: id => http("GET", `/api/requests/${id}/history`),
    comments: id => http("GET", `/api/requests/${id}/comments`),
    addComment: (id, userId, commentText) => http("POST", `/api/requests/${id}/comments`, { userId, commentText }),
    assignments: () => http("GET", "/api/assignments"),
    assign: (id, staffUserId, changedByUserId) => http("PUT", `/api/requests/${id}/assignment`, { staffUserId, changedByUserId }),
    // Admin / Dev user management (every change needs a second admin's approval)
    adminSearch: (name, role) => http("GET", `/api/admin/users?actingUserId=${session.user.userId}&name=${encodeURIComponent(name)}&role=${encodeURIComponent(role)}`),
    adminPromote: id => http("POST", `/api/admin/users/${id}/promote`, { actingUserId: session.user.userId }),
    adminRemoveAccess: id => http("POST", `/api/admin/users/${id}/remove-access`, { actingUserId: session.user.userId }),
    adminCreateUser: body => http("POST", "/api/admin/users", { ...body, actingUserId: session.user.userId }),
    adminApprovals: () => http("GET", `/api/admin/approvals?actingUserId=${session.user.userId}`),
    adminApprove: id => http("POST", `/api/admin/approvals/${id}/approve`, { actingUserId: session.user.userId }),
    adminDecline: id => http("POST", `/api/admin/approvals/${id}/decline`, { actingUserId: session.user.userId }),
    logs: (severity, search) => http("GET", `/api/logs?actingUserId=${session.user.userId}&severity=${encodeURIComponent(severity)}&search=${encodeURIComponent(search)}&limit=300`)
  };

  // Report JavaScript errors to the server so devs can see them on the Logs page.
  let reported = 0;
  function reportError(message, stack) {
    if (reported++ >= 10 || !message) return;
    try {
      fetch("/api/logs/client", {
        method: "POST", keepalive: true,
        headers: { "Content-Type": "application/json", ...(session.user ? { "X-CivicConnect-User": String(session.user.userId) } : {}) },
        body: JSON.stringify({ message: String(message).slice(0, 1000), stack: String(stack || "").slice(0, 5000), page: location.hash || "/" })
      }).catch(() => {});
    } catch { /* never let error reporting cause errors */ }
  }
  window.addEventListener("error", e => reportError(e.message, e.error && e.error.stack));
  window.addEventListener("unhandledrejection", e => reportError(e.reason && e.reason.message || String(e.reason), e.reason && e.reason.stack));

  // Reference data, loaded once per page load.
  const ref = { categories: null, statuses: null };
  async function loadRef() {
    if (!ref.categories) [ref.categories, ref.statuses] = await Promise.all([api.categories(), api.statuses()]);
  }

  // Requests joined with their current assignment.
  async function loadRequests() {
    const [reqs, assigns] = await Promise.all([api.requests(), api.assignments().catch(() => [])]);
    const byReq = new Map(assigns.map(a => [a.requestId, a]));
    return reqs.map(r => ({ ...r, assignment: byReq.get(r.requestId) || null }))
      .sort((a, b) => toDate(b.requestDate) - toDate(a.requestDate));
  }

  // ---------- Icons ----------
  const I = {
    logo: `<svg viewBox="0 0 40 40" fill="none"><rect width="40" height="40" rx="10" fill="#1f8a7e"/><path d="M9 16.5L20 10l11 6.5" stroke="#fff" stroke-width="2.4" stroke-linejoin="round" stroke-linecap="round"/><path d="M10.5 17h19M13.5 19.5v7M18 19.5v7M22 19.5v7M26.5 19.5v7M9.5 29h21" stroke="#fff" stroke-width="2.4" stroke-linecap="round"/></svg>`,
    pin: `<svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2a7 7 0 00-7 7c0 5.2 7 13 7 13s7-7.8 7-13a7 7 0 00-7-7zm0 9.5A2.5 2.5 0 1112 6.5a2.5 2.5 0 010 5z"/></svg>`,
    clipboard: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><rect x="5" y="4" width="14" height="17" rx="2"/><path d="M9 4V3h6v1M9 10h6M9 14h6M9 18h3"/></svg>`,
    clock: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></svg>`,
    gear: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 00.3 1.8l.1.1a2 2 0 11-2.8 2.8l-.1-.1a1.7 1.7 0 00-1.8-.3 1.7 1.7 0 00-1 1.5V21a2 2 0 01-4 0v-.1a1.7 1.7 0 00-1.1-1.5 1.7 1.7 0 00-1.8.3l-.1.1a2 2 0 11-2.8-2.8l.1-.1a1.7 1.7 0 00.3-1.8 1.7 1.7 0 00-1.5-1H3a2 2 0 010-4h.1a1.7 1.7 0 001.5-1.1 1.7 1.7 0 00-.3-1.8l-.1-.1a2 2 0 112.8-2.8l.1.1a1.7 1.7 0 001.8.3H9a1.7 1.7 0 001-1.5V3a2 2 0 014 0v.1a1.7 1.7 0 001 1.5 1.7 1.7 0 001.8-.3l.1-.1a2 2 0 112.8 2.8l-.1.1a1.7 1.7 0 00-.3 1.8V9a1.7 1.7 0 001.5 1H21a2 2 0 010 4h-.1a1.7 1.7 0 00-1.5 1z"/></svg>`,
    check: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"/><path d="M8 12.5l2.7 2.7L16 9.8"/></svg>`,
    arrow: `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12h14M13 6l6 6-6 6"/></svg>`,
    signin: `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M15 3h4a2 2 0 012 2v14a2 2 0 01-2 2h-4M10 17l5-5-5-5M15 12H3"/></svg>`,
    pothole: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M3 19L9 5M21 19L15 5M12 6v2M12 11v2"/><path d="M7 17c1.5-1.2 3-1 4 .2 1-1.2 3-1.6 4.5 0 .6.6.9 1.2 1 1.8H6.5c0-.7.2-1.4.5-2z"/></svg>`,
    drop: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M12 3s6 6.6 6 11a6 6 0 01-12 0c0-4.4 6-11 6-11z"/><path d="M9.5 15a2.5 2.5 0 002.5 2.5"/></svg>`,
    streetlight: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M8 21V7a3 3 0 013-3h4a2 2 0 012 2v1"/><path d="M14.5 7h5l-1 2.5h-3z"/><path d="M5.5 21h5"/></svg>`,
    trash: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M4 7h16M9 7V4h6v3M6 7l1 13a1 1 0 001 1h8a1 1 0 001-1l1-13M10 11v6M14 11v6"/></svg>`,
    home: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 11l9-7 9 7v9a1 1 0 01-1 1h-5v-6H9v6H4a1 1 0 01-1-1z"/></svg>`,
    list: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01"/></svg>`,
    plus: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="9"/><path d="M12 8v8M8 12h8"/></svg>`,
    inbox: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 12h-6l-2 3h-4l-2-3H2"/><path d="M5.5 5h13L22 12v6a2 2 0 01-2 2H4a2 2 0 01-2-2v-6z"/></svg>`,
    user: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="8" r="4"/><path d="M4 21c0-4 4-6 8-6s8 2 8 6"/></svg>`,
    users: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="9" cy="8" r="3.5"/><path d="M2 20c0-3.5 3-5.5 7-5.5s7 2 7 5.5M16 4.5a3.5 3.5 0 010 7M22 20c0-2.6-1.6-4.4-4-5.1"/></svg>`,
    report: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 3H6a2 2 0 00-2 2v14a2 2 0 002 2h12a2 2 0 002-2V9z"/><path d="M14 3v6h6M8 13h8M8 17h5"/></svg>`,
    chart: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 3v18h18"/><path d="M7 15l4-4 3 3 5-6"/></svg>`,
    mail: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="5" width="18" height="14" rx="2"/><path d="M3 7l9 6 9-6"/></svg>`,
    lock: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="4" y="11" width="16" height="10" rx="2"/><path d="M8 11V7a4 4 0 018 0v4"/></svg>`,
    search: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="7"/><path d="M21 21l-4.3-4.3"/></svg>`,
    back: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 12H5M12 19l-7-7 7-7"/></svg>`,
    logout: `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9"/></svg>`,
    logs: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 5h16M4 10h16M4 15h10M4 20h7"/><circle cx="18" cy="18" r="3"/></svg>`,
    menu: `<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 6h18M3 12h18M3 18h18"/></svg>`
  };

  // ---------- Session & shell ----------
  const session = { user: store.get("user") };
  // Visitors who aren't signed in are "Guest": they can see the dashboard, and are asked to sign in for anything else.
  const role = () => session.user ? appRole(session.user.role) : "Guest";
  function signIn(user) {
    session.user = user; store.set("user", user);
    // Send them back to whatever they were trying to open before signing in, if their role can open it.
    const back = store.get("returnTo"); store.set("returnTo", null);
    const page = back ? back.split("/")[1] : "";
    location.hash = page && ACCESS[page] && ACCESS[page].includes(appRole(user.role)) ? back : HOME[appRole(user.role)];
  }
  // Sign out: clear the saved user, then reload the page on the dashboard so everything renders as a guest.
  function signOut() {
    session.user = null; store.set("user", null); store.set("returnTo", null);
    location.replace(location.pathname + location.search + "#/dashboard");
    location.reload();
  }

  // Ask a guest to sign in. "Sign in" remembers where they were heading and opens the sign-in screen.
  function promptSignIn(target, why) {
    if (document.querySelector(".modal-backdrop")) return;
    const m = modal(`<div class="modal-icon signin-icon">${I.lock}</div><h2>Sign in to continue</h2>
      <p class="muted">${esc(why || "You need to be signed in to do that.")}</p>
      <div class="actions-row"><button class="btn" data-no>Not now</button><button class="btn btn-primary" data-yes>Sign in</button></div>
      <p class="switch-auth">Don't have an account? <a href="#/register" data-reg>Register</a></p>`);
    m.el.querySelector("[data-no]").onclick = m.close;
    m.el.querySelector("[data-reg]").onclick = () => { store.set("returnTo", target); m.close(); };
    const yes = m.el.querySelector("[data-yes]"); yes.focus();
    yes.onclick = () => { store.set("returnTo", target); m.close(); location.hash = "#/login"; };
  }
  const GUEST_REASON = {
    "my-requests": "Sign in to see and track the requests you've submitted.",
    "submit": "Sign in to submit a service request so you can track its progress.",
    "request": "Sign in to view this request."
  };

  const NAV = {
    // Everyone (signed in or not) starts with the same dashboard and can report issues; role pages come after.
    Requester: [["#/dashboard", "Dashboard", I.home], ["#/my-requests", "My Requests", I.list], ["#/submit", "Submit Request", I.plus]]
  };
  NAV.Staff = [...NAV.Requester, ["#/queue", "Request Queue", I.inbox], ["#/assignments", "My Assignments", I.user], ["#/reports", "Reports", I.report]];
  NAV.Manager = [...NAV.Requester, ["#/management", "Management", I.clipboard], ["#/queue", "Request Queue", I.inbox], ["#/reports", "Reports", I.report], ["#/analytics", "Analytics", I.chart], ["#/users", "Users", I.users]];
  NAV.Dev = [...NAV.Manager, ["#/logs", "Logs", I.logs]];
  NAV.Guest = NAV.Requester;
  const HOME = { Requester: "#/dashboard", Staff: "#/dashboard", Manager: "#/dashboard", Dev: "#/dashboard" };

  function shell(inner) {
    const u = session.user, hash = location.hash;
    const userBox = u ? `<div class="user-box">
          <div class="avatar">${initials(u.fullName)}</div>
          <div><div class="name">${esc(u.fullName)}</div><div class="role">${esc(ROLE_LABEL[role()])}</div></div>
          <button id="logout" title="Sign out" aria-label="Sign out">${I.logout}</button>
        </div>` : `<div class="user-box guest">
          <a href="#/login" class="btn btn-primary btn-block">${I.signin} Sign in</a>
          <p>New here? <a href="#/register">Create an account</a></p>
        </div>`;
    const nav = NAV[role()].map(([href, label, icon]) => {
      const active = hash === href || hash.startsWith(href + "/") ||
        (href === "#/my-requests" && hash.startsWith("#/request/")) || (href === "#/queue" && hash.startsWith("#/manage/"));
      return `<a href="${href}" class="${active ? "active" : ""}">${icon}<span>${label}</span></a>`;
    }).join("");
    return `<div class="shell" id="shell">
      <aside class="sidebar">
        <div class="brand">${I.logo}<span>Civic<em>Connect</em></span></div>
        <nav class="nav">${nav}</nav>
        ${userBox}
      </aside>
      <div class="main">
        <header class="topbar">
          <button class="menu-btn" id="menu" aria-label="Open menu">${I.menu}</button>
          ${u ? `<div class="avatar" title="${esc(u.fullName)}">${initials(u.fullName)}</div>` : `<a href="#/login" class="btn btn-sm btn-primary">Sign in</a>`}
        </header>
        <main class="content">${inner}</main>
      </div>
    </div>`;
  }

  function render(inner) {
    $("#app").innerHTML = shell(inner);
    if ($("#logout")) $("#logout").onclick = signOut;
    $("#menu").onclick = e => { e.stopPropagation(); $("#shell").classList.toggle("nav-open"); };
    $("#shell").addEventListener("click", e => {
      if (!e.target.closest(".sidebar") && !e.target.closest("#menu")) $("#shell").classList.remove("nav-open");
    });
    bindRows();
    window.scrollTo(0, 0);
  }
  const loading = () => render(`<div class="card empty">Loading…</div>`);

  // ---------- Tables ----------
  function requestTable(rows, cols, linkBase, emptyText = "No requests to show.") {
    if (!rows.length) return `<div class="empty">${emptyText}</div>`;
    const head = cols.map(c => `<th>${c[0]}</th>`).join("");
    const body = rows.map(r => `<tr class="clickable" data-href="${linkBase}${r.requestId}" tabindex="0">${cols.map(c => `<td>${c[1](r)}</td>`).join("")}</tr>`).join("");
    return `<div class="table-wrap"><table><thead><tr>${head}</tr></thead><tbody>${body}</tbody></table></div>`;
  }
  const COL = {
    ticket: ["Ticket #", r => `<span class="ticket">${esc(r.ticketNumber)}</span>`],
    title: ["Title", r => esc(r.title)],
    category: ["Category", r => esc(r.categoryName)],
    status: ["Status", r => statusBadge(r.requestStatus)],
    outcome: ["Outcome", r => outcomeBadge(r.requestStatus)],
    closed: ["Closed", r => `<span class="muted">${fmtDate(r.updatedAt)}</span>`],
    requester: ["Requester", r => esc(r.requesterName)],
    date: ["Date", r => `<span class="muted">${fmtDate(r.requestDate)}</span>`],
    assigned: ["Assigned to", r => r.assignment ? esc(r.assignment.staffName) : `<span class="muted">Unassigned</span>`]
  };
  function bindRows(root = document) {
    root.querySelectorAll("tr[data-href]").forEach(tr => {
      tr.onclick = () => (location.hash = tr.dataset.href);
      tr.onkeydown = e => { if (e.key === "Enter") location.hash = tr.dataset.href; };
    });
  }
  const stat = (n, l, color) => `<div class="card stat"><div class="num">${n}</div><div class="lbl">${color ? `<span class="dot" style="background:${color}"></span>` : ""}${l}</div></div>`;

  // ---------- Login / Register ----------
  function authCard(inner) {
    $("#app").innerHTML = `<div class="login-wrap"><a class="login-back" href="#/dashboard">${I.back} Back to dashboard</a>
      <div class="login-card"><div class="logo">${I.logo}</div>${inner}</div></div>`;
  }

  function viewLogin() {
    const prompted = !!store.get("returnTo");
    authCard(`<h1>CivicConnect</h1>
      <p class="tag">Community Service Request Management</p>
      ${prompted ? `<p class="login-note">Please sign in to continue.</p>` : ""}
      <form id="loginForm" novalidate>
        <div id="loginErr"></div>
        <div class="input-icon">${I.mail}<input type="text" id="email" placeholder="Email" autocomplete="username"></div>
        <div class="input-icon">${I.lock}<input type="password" id="pw" placeholder="Password" autocomplete="current-password"></div>
        <button class="btn btn-primary btn-block" type="submit" id="loginBtn">Login</button>
      </form>
      <a href="#/login" class="forgot" id="forgot">Forgot password?</a>
      <p class="switch-auth">Don't have an account? <a href="#/register">Register</a></p>
      <p class="switch-auth">Signed up but not confirmed? <a href="#/confirm">Enter your code</a></p>`);
    $("#forgot").onclick = e => { e.preventDefault(); toast("Password reset isn't available yet — contact your administrator."); };
    $("#loginForm").onsubmit = async e => {
      e.preventDefault();
      const email = $("#email").value.trim(), pw = $("#pw").value;
      if (!email || !pw) { $("#loginErr").innerHTML = `<div class="error-msg">Please enter your email and password.</div>`; return; }
      $("#loginBtn").disabled = true;
      try { signIn(await api.login(email, pw)); }
      catch (err) { $("#loginErr").innerHTML = `<div class="error-msg">${esc(err.message)}</div>`; $("#loginBtn").disabled = false; }
    };
  }

  const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

  // Sign-up: the server only creates the account if it could email the confirmation code.
  function viewRegister() {
    authCard(`<h1>Create an account</h1>
      <p class="tag">Register to submit and track service requests</p>
      <form id="regForm" novalidate>
        <div id="regErr"></div>
        <div class="input-icon">${I.user}<input type="text" id="rname" placeholder="Full name" autocomplete="name" maxlength="150"></div>
        <div class="input-icon">${I.mail}<input type="email" id="remail" placeholder="Email" autocomplete="email" maxlength="255"></div>
        <div class="input-icon">${I.lock}<input type="password" id="rpw" placeholder="Password (min. 10 characters)" autocomplete="new-password" maxlength="128"></div>
        <div class="input-icon">${I.lock}<input type="password" id="rpw2" placeholder="Confirm password" autocomplete="new-password" maxlength="128"></div>
        <p class="hint-small">We'll email you a 6-digit code to confirm your address.</p>
        <button class="btn btn-primary btn-block" type="submit" id="regBtn">Register</button>
      </form>
      <p class="switch-auth">Already have an account? <a href="#/login">Log in</a></p>
      <p class="switch-auth">Have a code? <a href="#/confirm">Confirm your email</a></p>`);
    $("#regForm").onsubmit = async e => {
      e.preventDefault();
      const name = $("#rname").value.trim(), email = $("#remail").value.trim().toLowerCase(), pw = $("#rpw").value, pw2 = $("#rpw2").value;
      const err = !name ? "Please enter your full name." :
        !EMAIL_RE.test(email) ? "Please enter a valid email address." :
        pw.length < 10 ? "Password must be at least 10 characters." :
        pw !== pw2 ? "Passwords don't match." : "";
      if (err) { $("#regErr").innerHTML = `<div class="error-msg">${esc(err)}</div>`; return; }
      $("#regBtn").disabled = true; $("#regBtn").textContent = "Sending code…";
      try {
        await api.register(name, email, pw);
        viewConfirm(email, true);
      } catch (ex) {
        $("#regErr").innerHTML = `<div class="error-msg">${esc(ex.message)}</div>`;
        $("#regBtn").disabled = false; $("#regBtn").textContent = "Register";
      }
    };
  }

  // Enter the emailed code. Reached straight after sign-up, or later from #/confirm.
  function viewConfirm(email = "", justSent = false) {
    authCard(`<h1>Confirm your email</h1>
      <p class="tag">${justSent ? `We've emailed a 6-digit code to <strong>${esc(email)}</strong>. It expires in 10 minutes.` : "Enter the email you signed up with and the 6-digit code we sent you."}</p>
      <form id="confirmForm" novalidate>
        <div id="confirmErr"></div>
        <div class="input-icon" ${justSent ? 'hidden' : ""}>${I.mail}<input type="email" id="cemail" placeholder="Email" autocomplete="email" value="${esc(email)}"></div>
        <div class="input-icon">${I.lock}<input type="text" id="confirmationCode" inputmode="numeric" autocomplete="one-time-code" maxlength="6" pattern="\\d{6}" placeholder="6-digit code"></div>
        <button class="btn btn-primary btn-block" type="submit" id="confirmBtn">Confirm account</button>
      </form>
      <button class="btn btn-block resend-btn" type="button" id="resendBtn">Email me a new code</button>
      <p class="switch-auth">Can't find it? Check your spam folder.</p>
      <p class="switch-auth"><a href="#/login">Back to login</a></p>`);
    $(justSent ? "#confirmationCode" : "#cemail").focus();
    const readEmail = () => $("#cemail").value.trim().toLowerCase();
    const showErr = m => ($("#confirmErr").innerHTML = m ? `<div class="error-msg">${esc(m)}</div>` : "");
    $("#confirmForm").onsubmit = async e => {
      e.preventDefault();
      const addr = readEmail(), code = $("#confirmationCode").value.trim();
      if (!EMAIL_RE.test(addr)) return showErr("Please enter the email address you signed up with.");
      if (!/^\d{6}$/.test(code)) return showErr("Enter the 6-digit code from the email.");
      $("#confirmBtn").disabled = true;
      try {
        await api.confirm(addr, code);
        authCard(`<h1>Account confirmed</h1>
          <p class="tag">Your account is ready. Log in to continue.</p>
          <a class="btn btn-primary btn-block" href="#/login">Go to login</a>`);
      } catch (ex) {
        showErr(ex.message);
        $("#confirmBtn").disabled = false;
      }
    };
    $("#resendBtn").onclick = async () => {
      const addr = readEmail();
      if (!EMAIL_RE.test(addr)) return showErr("Please enter the email address you signed up with.");
      showErr("");
      $("#resendBtn").disabled = true;
      try {
        await api.resendConfirmation(addr);
        toast("If that email has an unconfirmed account, a new code is on its way.");
      } catch (ex) {
        showErr(ex.message);
      } finally {
        // Short cool-down so the button isn't hammered (the server also limits it).
        setTimeout(() => { if ($("#resendBtn")) $("#resendBtn").disabled = false; }, 30000);
      }
    };
  }

  // ---------- Requester ----------
  // Things people can report, shown as shortcuts on the dashboard. `match` picks a category whose name contains one of the words.
  const REPORT_TYPES = [
    { key: "pothole", label: "Potholes", title: "Pothole", icon: I.pothole, tone: "slate", match: ["pothole", "road"] },
    { key: "water-leak", label: "Water leaks", title: "Water leak", icon: I.drop, tone: "blue", match: ["water", "leak"] },
    { key: "streetlight", label: "Streetlights", title: "Streetlight not working", icon: I.streetlight, tone: "amber", match: ["streetlight", "light", "electric"] },
    { key: "waste", label: "Waste collection", title: "Missed waste collection", icon: I.trash, tone: "green", match: ["waste", "refuse", "rubbish"] }
  ];

  const greeting = () => { const h = new Date().getHours(); return h < 12 ? "Good morning" : h < 18 ? "Good afternoon" : "Good evening"; };
  const statCard = (n, label, icon, tone) => `<div class="card stat2 tone-${tone}">
      <div class="stat2-icon">${icon}</div><div class="stat2-lbl">${label}</div><div class="stat2-num">${n}</div><span class="stat2-bar"></span></div>`;

  // Requester dashboard. Guests see the same page, without any request data.
  async function viewRequesterDashboard() {
    const u = session.user;
    const mine = u ? (await loadRequests()).filter(r => r.requesterId === u.userId) : null;
    const c = s => mine ? mine.filter(r => r.requestStatus === s).length : "–";
    const first = u ? String(u.fullName || "").split(" ")[0] : "";
    const recent = !mine
      ? `<div class="empty guest-empty">${I.lock}<p>Sign in to see and track the requests you've submitted.</p>
          <a href="#/login" class="btn btn-primary btn-sm" data-signin>Sign in</a></div>`
      : requestTable(mine.slice(0, 5), [COL.ticket, COL.title, COL.status, COL.date,
          ["Actions", () => `<span class="row-link">View details ${I.arrow}</span>`]], "#/request/", "You haven't submitted any requests yet.");
    render(`<div class="dash-head">
        <div><h1>${u ? `${greeting()}, ${esc(first)}` : "Welcome to CivicConnect"}</h1>
          <p class="muted">Track local issues and help improve your community.</p></div>
        <span class="dash-loc">${I.pin} South Africa</span>
      </div>
      <section class="hero">
        <div class="hero-text">
          <h2>Better communities start with a report.</h2>
          <p>Report issues, track progress and help make our neighbourhoods safer, cleaner and stronger.</p>
          <a href="#/submit" class="btn btn-primary">${I.plus.replace("<svg", '<svg width="18" height="18"')} Submit a request</a>
        </div>
      </section>
      <div class="stats">
        ${statCard(mine ? mine.length : "–", "Total Requests", I.clipboard, "teal")}${statCard(c("Pending"), "Pending", I.clock, "amber")}
        ${statCard(c("In Progress"), "In Progress", I.gear, "blue")}${statCard(c("Resolved"), "Completed", I.check, "green")}
      </div>
      <div class="dash-grid">
        <section class="card">
          <div class="card-head"><h2>Recent requests</h2><a href="#/my-requests" class="row-link">View all requests ${I.arrow}</a></div>
          <div class="card-body">${recent}</div>
        </section>
        <section class="card card-pad report-card">
          <h2>What you can report</h2>
          <p class="muted small">Help us keep your community running smoothly by reporting issues in your area.</p>
          <div class="report-grid">${REPORT_TYPES.map(t => `<a class="report-tile tone-${t.tone}" href="#/submit/${t.key}"><span class="report-ico">${t.icon}</span>${t.label}</a>`).join("")}</div>
        </section>
      </div>`);
    const s = document.querySelector("[data-signin]");
    if (s) s.onclick = () => store.set("returnTo", "#/dashboard");
  }

  async function viewMyRequests() {
    const mine = (await loadRequests()).filter(r => r.requesterId === session.user.userId);
    const open = mine.filter(r => !isClosed(r));
    const past = mine.filter(isClosed).sort((a, b) => toDate(b.updatedAt) - toDate(a.updatedAt));
    render(`<div class="page-head"><h1>My Requests</h1><a href="#/submit" class="btn btn-primary">New request</a></div>
      <h2>Current Requests <span class="count">${open.length}</span></h2>
      <div class="card">${requestTable(open, [COL.ticket, COL.title, COL.category, COL.status, COL.date], "#/request/",
        mine.length ? "You have no open requests." : "You haven't submitted any requests yet.")}</div>
      <h2 class="section-gap">Request History <span class="count">${past.length}</span></h2>
      <div class="card">${requestTable(past, [COL.ticket, COL.title, COL.category, COL.outcome, COL.closed], "#/request/",
        "Completed and rejected requests will appear here.")}</div>`);
  }

  async function viewSubmit(typeKey) {
    await loadRef();
    const type = REPORT_TYPES.find(t => t.key === typeKey);
    render(`<div class="page-head"><h1>Submit a Service Request</h1></div>
      <div class="card card-pad"><form class="form" id="submitForm" novalidate>
        <div id="formErr"></div>
        <div class="field"><label for="cat">Category <span class="req">*</span></label>
          <select id="cat"><option value="">Select a category…</option>${ref.categories.map(c => `<option value="${c.categoryId}">${esc(c.categoryName)}</option>`).join("")}</select></div>
        <div class="field"><label for="title">Title <span class="req">*</span></label>
          <input type="text" id="title" maxlength="200" placeholder="e.g. Streetlight out on Main Road"></div>
        <div class="field"><label for="desc">Description <span class="req">*</span></label>
          <textarea id="desc" placeholder="What's the problem, and since when?"></textarea></div>
        <div class="field"><label for="loc">Location <span class="req">*</span></label>
          <input type="text" id="loc" maxlength="255" placeholder="Street address or landmark">
          <span class="hint">Be as specific as you can so staff can find it.</span></div>
        <div class="form-actions"><a href="#/dashboard" class="btn">Cancel</a><button class="btn btn-primary" type="submit" id="submitBtn">Submit</button></div>
      </form></div>`);
    if (type) { // came from a "What you can report" shortcut
      $("#title").value = type.title;
      const cat = ref.categories.find(c => type.match.some(w => c.categoryName.toLowerCase().includes(w)));
      if (cat) $("#cat").value = String(cat.categoryId);
      $("#desc").focus();
    }
    $("#submitForm").onsubmit = async e => {
      e.preventDefault();
      const body = {
        requesterId: session.user.userId, categoryId: Number($("#cat").value) || 0,
        title: $("#title").value.trim(), description: $("#desc").value.trim(), location: $("#loc").value.trim()
      };
      const missing = [!body.categoryId && "category", !body.title && "title", !body.description && "description", !body.location && "location"].filter(Boolean);
      if (missing.length) { $("#formErr").innerHTML = `<div class="error-msg">Please fill in the ${missing.join(", ")}.</div>`; return; }
      $("#submitBtn").disabled = true;
      try {
        const res = await api.createRequest(body);
        toast("Request submitted.");
        location.hash = `#/request/${res.requestId ?? res.RequestId}`;
      } catch (err) {
        $("#formErr").innerHTML = `<div class="error-msg">${esc(err.message)}</div>`;
        $("#submitBtn").disabled = false;
      }
    };
  }

  // ---------- Request details (requester view, or staff view with controls) ----------
  const historyText = h => {
    if (h.actionType === "RequestCreated") return "Request submitted";
    if (h.actionType === "StatusChanged") return `Status changed${h.oldStatus ? ` from ${h.oldStatus}` : ""} to ${h.newStatus}`;
    return h.actionType.replace(/([a-z])([A-Z])/g, "$1 $2");
  };

  // Banner on a closed request telling the requester the outcome and the staff note.
  function closedBanner(r, history) {
    const last = history.find(h => h.newStatus === r.requestStatus);
    const rejected = r.requestStatus === "Rejected";
    return `<div class="closed-banner ${rejected ? "is-rejected" : "is-completed"}">${outcomeBadge(r.requestStatus)}
      <span>${rejected ? "This request was rejected" : "This request was completed"} on ${fmtDate(last ? last.changedAt : r.updatedAt)}.${last && last.notes ? ` <strong>${rejected ? "Reason" : "Note"}:</strong> ${esc(last.notes)}` : ""}</span></div>`;
  }

  async function viewDetails(id, staffMode) {
    const backHref = staffMode ? "#/queue" : "#/my-requests", backLbl = staffMode ? "Back to Queue" : "Back to My Requests";
    let r;
    try { r = await api.request(id); }
    catch (err) {
      render(`<a class="back" href="${backHref}">${I.back} ${backLbl}</a><div class="card empty">${esc(err.status === 404 ? `Request #${id} was not found.` : err.message)}</div>`);
      return;
    }
    if (!staffMode && r.requesterId !== session.user.userId) {
      render(`<a class="back" href="${backHref}">${I.back} ${backLbl}</a><div class="card empty">You can only view your own requests.</div>`); return;
    }
    const [history, comments, assigns, staff] = await Promise.all([
      api.history(id), api.comments(id), api.assignments().catch(() => []),
      staffMode ? Promise.all([api.users("Staff"), api.users("Manager")]).then(([a, b]) => [...a, ...b]) : Promise.resolve([]),
      staffMode ? loadRef() : null
    ]);
    const assignment = assigns.find(a => a.requestId === id) || null;
    const tab = viewDetails.tab || (staffMode ? "details" : "history");

    const details = `<p class="desc">${esc(r.description)}</p>
      ${staffMode ? `
        <div class="assign-grid">
          <div class="field form"><label for="assignee">Assign to</label>
            <select id="assignee"><option value="">Unassigned</option>${staff.map(s => `<option value="${s.userId}" ${assignment && s.userId === assignment.staffUserId ? "selected" : ""}>${esc(s.fullName)}</option>`).join("")}</select></div>
          <div class="field form"><label for="status">Status</label>
            <select id="status">${ref.statuses.map(s => `<option ${s.statusName === r.requestStatus ? "selected" : ""}>${esc(s.statusName)}</option>`).join("")}</select></div>
        </div>
        <div class="form note-box" style="max-width:none">
          <div class="field"><label for="notes">Notes <span class="hint">(required when resolving or rejecting — the requester can see the reason in History)</span></label>
          <textarea id="notes" style="min-height:80px" placeholder="What was done?"></textarea></div></div>
        <div id="actErr" style="margin-top:12px"></div>
        <div class="actions-row"><button class="btn" id="saveBtn">Update Status</button>
          <button class="btn btn-danger" id="rejectBtn" ${isClosed(r) ? "disabled" : ""}>Reject</button>
          <button class="btn btn-primary" id="resolveBtn" ${isClosed(r) ? "disabled" : ""}>Resolve</button></div>` : ""}`;

    const historyHtml = history.length ? `<ul class="timeline">${history.map(h => `<li><div class="when">${fmtDateTime(h.changedAt)} · ${esc(h.changedByName || `User #${h.changedByUserId}`)}</div>
        <div class="what">${esc(historyText(h))}</div>${h.notes ? `<div class="note">${esc(h.notes)}</div>` : ""}</li>`).join("")}</ul>`
      : `<div class="empty">No history recorded yet.</div>`;

    const commentsHtml = `<div class="comments">${comments.length ? comments.map(c => `<div class="comment"><div class="avatar">${initials(c.userName)}</div>
        <div class="bubble"><div class="who">${esc(c.userName || `User #${c.userId}`)}<span>${fmtDateTime(c.createdAt)}</span></div>${esc(c.commentText)}</div></div>`).join("")
        : `<div class="empty">No comments yet.</div>`}</div>
      <form class="comment-form" id="commentForm"><textarea id="commentText" placeholder="Add a comment…"></textarea><button class="btn btn-primary" id="commentBtn">Post</button></form>`;

    render(`<a class="back" href="${backHref}">${I.back} ${backLbl}</a>
      <div class="page-head" style="border:0;padding-bottom:0;margin-bottom:16px"><h1>Ticket ${esc(r.ticketNumber)}</h1>${statusBadge(r.requestStatus)}</div>
      <div class="card card-pad"><h2 style="font-size:17px">${esc(r.title)}</h2><dl class="meta" style="margin:0">
        <div><dt>Category</dt><dd>${esc(r.categoryName)}</dd></div>
        <div><dt>Status</dt><dd>${esc(r.requestStatus)}</dd></div>
        <div><dt>Location</dt><dd>${esc(r.location)}</dd></div>
        <div><dt>Submitted</dt><dd>${fmtDateTime(r.requestDate)}${staffMode ? ` by ${esc(r.requesterName)}` : ""}</dd></div>
        <div><dt>Assigned to</dt><dd>${assignment ? esc(assignment.staffName) : "Unassigned"}</dd></div>
      </dl></div>
      ${!staffMode && isClosed(r) ? closedBanner(r, history) : ""}
      <div class="tabs" role="tablist">${["details", "history", "comments"].map(t => `<button class="tab ${t === tab ? "active" : ""}" data-tab="${t}" role="tab">${t[0].toUpperCase() + t.slice(1)}</button>`).join("")}</div>
      <div class="card card-pad tab-panel">${{ details, history: historyHtml, comments: commentsHtml }[tab]}</div>`);

    document.querySelectorAll(".tab").forEach(b => b.onclick = () => { viewDetails.tab = b.dataset.tab; viewDetails(id, staffMode); });

    const cf = $("#commentForm");
    if (cf) cf.onsubmit = async e => {
      e.preventDefault();
      const text = $("#commentText").value.trim(); if (!text) return;
      $("#commentBtn").disabled = true;
      try { await api.addComment(id, session.user.userId, text); viewDetails(id, staffMode); }
      catch (err) { toast(err.message, true); $("#commentBtn").disabled = false; }
    };

    if (staffMode && $("#saveBtn")) {
      const btns = ["#saveBtn", "#rejectBtn", "#resolveBtn"];
      const apply = async action => { // action: "save" | "resolve" | "reject"
        const newStatus = action === "resolve" ? "Resolved" : action === "reject" ? "Rejected" : $("#status").value;
        const notes = $("#notes").value.trim();
        const assignee = Number($("#assignee").value) || null;
        $("#actErr").innerHTML = "";
        if (newStatus === "Resolved" && !notes) { $("#actErr").innerHTML = `<div class="error-msg">Resolution notes are required when resolving a request.</div>`; $("#notes").focus(); return; }
        if (newStatus === "Rejected" && !notes) { $("#actErr").innerHTML = `<div class="error-msg">Please give a reason for rejecting this request.</div>`; $("#notes").focus(); return; }
        if (action === "reject" && !confirmReject()) return;
        btns.forEach(b => ($(b).disabled = true));
        try {
          if (assignee !== (assignment ? assignment.staffUserId : null))
            await api.assign(id, assignee, session.user.userId);
          if (newStatus !== r.requestStatus)
            await api.updateStatus(id, { newStatus, changedByUserId: session.user.userId, expectedVersion: r.version, notes: notes || null });
          toast(action === "resolve" ? "Request resolved." : action === "reject" ? "Request rejected." : "Request updated.");
          viewDetails(id, true);
        } catch (err) {
          $("#actErr").innerHTML = `<div class="error-msg">${esc(err.message)}</div>`;
          $("#saveBtn").disabled = false;
          $("#rejectBtn").disabled = $("#resolveBtn").disabled = isClosed(r);
        }
      };
      // Second click confirms, so a reject is never one accidental click (no browser dialogs).
      const confirmReject = () => {
        const b = $("#rejectBtn");
        if (b.dataset.armed) return true;
        b.dataset.armed = "1"; b.textContent = "Click again to reject";
        setTimeout(() => { if (b.isConnected) { delete b.dataset.armed; b.textContent = "Reject"; } }, 4000);
        return false;
      };
      $("#saveBtn").onclick = () => apply("save");
      $("#rejectBtn").onclick = () => apply("reject");
      $("#resolveBtn").onclick = () => apply("resolve");
    }
  }

  // ---------- Staff queue / My Assignments ----------
  async function viewQueue(assignedOnly) {
    const [all] = await Promise.all([loadRequests(), loadRef()]);
    const f = viewQueue.f || (viewQueue.f = { q: "", cat: "", status: "" });
    const rowsFor = () => all.filter(r =>
      (!assignedOnly || (r.assignment && r.assignment.staffUserId === session.user.userId)) &&
      (!f.cat || r.categoryId === Number(f.cat)) && (!f.status || r.requestStatus === f.status) &&
      (!f.q || `${r.ticketNumber} ${r.title} ${r.location} ${r.requesterName}`.toLowerCase().includes(f.q.toLowerCase())));
    const cols = [COL.ticket, COL.category, COL.title, COL.status, ...(assignedOnly ? [] : [COL.assigned]), COL.date];
    const empty = assignedOnly ? "No requests are assigned to you." : "No requests match.";
    render(`<div class="page-head"><h1>${assignedOnly ? "My Assignments" : "Request Queue"}</h1>
        <div class="search">${I.search}<input type="search" id="q" placeholder="Search..." value="${esc(f.q)}"></div></div>
      <div class="filters">
        <select id="fcat"><option value="">All Categories</option>${ref.categories.map(c => `<option value="${c.categoryId}" ${String(c.categoryId) === f.cat ? "selected" : ""}>${esc(c.categoryName)}</option>`).join("")}</select>
        <select id="fstatus"><option value="">All Statuses</option>${ref.statuses.map(s => `<option ${s.statusName === f.status ? "selected" : ""}>${esc(s.statusName)}</option>`).join("")}</select>
        <button class="btn" id="fbtn">Filter</button>
      </div>
      <div class="card" id="tbl">${requestTable(rowsFor(), cols, "#/manage/", empty)}</div>`);
    const refresh = () => { $("#tbl").innerHTML = requestTable(rowsFor(), cols, "#/manage/", empty); bindRows($("#tbl")); };
    $("#q").oninput = e => { f.q = e.target.value; refresh(); };
    $("#fbtn").onclick = () => { f.cat = $("#fcat").value; f.status = $("#fstatus").value; refresh(); };
  }

  // ---------- Management dashboard ----------
  async function viewManagement() {
    const [all] = await Promise.all([loadRequests(), loadRef()]);
    const by = s => all.filter(r => r.requestStatus === s).length;
    const palette = ["#0f7a6c", "#1f5fbf", "#c27c0e", "#7a4fb5", "#8a97a6", "#b4446c", "#4d8f2f"];
    const cats = ref.categories.map((c, i) => ({ name: c.categoryName, v: all.filter(r => r.categoryId === c.categoryId).length, color: palette[i % palette.length] }));
    const total = all.length || 1;
    let acc = 0;
    const arcs = cats.filter(c => c.v).map(c => {
      const len = (c.v / total) * 100, seg = `<circle r="15.915" cx="20" cy="20" fill="none" stroke="${c.color}" stroke-width="7" stroke-dasharray="${len} ${100 - len}" stroke-dashoffset="${25 - acc}"><title>${esc(c.name)}: ${c.v}</title></circle>`;
      acc += len; return seg;
    }).join("");
    const statusRows = ref.statuses.map(s => ({ n: s.statusName, c: statusColor(s.statusName), v: by(s.statusName) }));
    const overdue = all.filter(isOverdue).length;
    const maxS = Math.max(1, ...statusRows.map(s => s.v), overdue);
    const approvals = isAdminRole(session.user.role) ? await api.adminApprovals().catch(err => ({ error: err.message })) : [];
    render(`<div class="page-head"><h1>Management Dashboard</h1></div>
      ${approvalsPanel(approvals)}
      <div class="stats">${stat(all.length, "Total Requests")}${stat(by("Resolved"), "Resolved", "var(--resolved)")}${stat(by("In Progress"), "In Progress", "var(--progress)")}${stat(overdue, `Overdue (&gt;${OVERDUE_DAYS} days)`, "var(--danger)")}</div>
      <div class="grid-2">
        <div class="card card-pad"><h2>Requests by Category</h2><div class="donut-wrap">
          <svg class="donut" viewBox="0 0 40 40" role="img" aria-label="Requests by category"><circle r="15.915" cx="20" cy="20" fill="none" stroke="#eef1f4" stroke-width="7"/>${arcs}
            <text x="20" y="21.5" text-anchor="middle" font-size="7" font-weight="700" fill="var(--ink)">${all.length}</text></svg>
          <ul class="legend">${cats.map(c => `<li><span class="sw" style="background:${c.color}"></span>${esc(c.name)}<span class="v">${c.v}</span></li>`).join("")}</ul></div></div>
        <div class="card card-pad"><h2>Requests by Status</h2><div class="bars">
          ${[...statusRows, { n: "Overdue", c: "var(--danger)", v: overdue }].map(s => `<div class="bar-row"><span>${esc(s.n)}</span><div class="bar-track"><div class="bar-fill" style="width:${(s.v / maxS) * 100}%;background:${s.c}"></div></div><span class="v">${s.v}</span></div>`).join("")}
        </div></div>
      </div>
      <h2>Recent Requests</h2>
      <div class="card">${requestTable(all.slice(0, 8), [COL.ticket, COL.category, COL.title, COL.requester, COL.status, COL.date], "#/manage/")}</div>
      ${isAdminRole(session.user.role) ? manageUsersSection() : ""}`);
    if (isAdminRole(session.user.role)) { bindApprovals(); bindManageUsers(); }
  }

  // ---------- Admin / Dev: user management ----------
  // Every promote / remove-access, and every new Admin, waits for a second admin.
  function describeApproval(a) {
    const who = esc(a.targetName || a.newFullName || "a user");
    if (a.actionType === "Promote") return `Promote <strong>${who}</strong> from ${esc(roleLabel(a.oldRole))} to <strong>${esc(roleLabel(a.newRole))}</strong>`;
    if (a.actionType === "RemoveAccess") return `Remove access for <strong>${who}</strong> (${esc(roleLabel(a.oldRole))})`;
    if (a.actionType === "CreateUser") return `Add <strong>${who}</strong> (${esc(a.newEmail)}) as <strong>${esc(roleLabel(a.newRole))}</strong>`;
    return esc(a.actionType);
  }

  function approvalsPanel(approvals) {
    if (approvals && approvals.error) return `<div class="error-msg" style="margin-bottom:20px">${esc(approvals.error)}</div>`;
    if (!approvals || !approvals.length) return "";
    return `<section class="card card-pad approvals" id="approvals">
      <h2>Waiting for approval <span class="count">${approvals.length}</span></h2>
      <ul class="approval-list">${approvals.map(a => {
        const mine = a.requestedByUserId === session.user.userId;
        return `<li>
          <div><div>${describeApproval(a)}</div>
            <div class="muted small">Requested by ${esc(mine ? "you" : a.requestedByName)} · ${fmtDateTime(a.requestedAt)}</div></div>
          <div class="approval-actions">${mine
            ? `<span class="muted small">Waiting for another admin</span><button class="btn btn-sm" data-decline="${a.approvalId}">Cancel</button>`
            : `<button class="btn btn-sm" data-decline="${a.approvalId}">Decline</button><button class="btn btn-sm btn-primary" data-approve="${a.approvalId}">Approve</button>`}
          </div></li>`;
      }).join("")}</ul></section>`;
  }

  function bindApprovals() {
    document.querySelectorAll("[data-approve]").forEach(b => b.onclick = () =>
      confirmModal("Approve this change?", "It will be applied to the database straight away.", "Yes, approve", async () => {
        const res = await api.adminApprove(Number(b.dataset.approve)); toast(res.message); route();
      }));
    document.querySelectorAll("[data-decline]").forEach(b => b.onclick = () =>
      confirmModal("Decline this request?", "Nothing will be changed.", "Yes, decline", async () => {
        const res = await api.adminDecline(Number(b.dataset.decline)); toast(res.message); route();
      }));
  }

  const ROLE_FILTERS = [["", "All roles"], ["Requester", "Requester"], ["Staff", "Worker"], ["Manager", "Admin"], ["Dev", "Dev"]];

  function manageUsersSection() {
    const f = manageUsersSection.f || (manageUsersSection.f = { name: "", role: "" });
    return `<section class="section-gap" id="manageUsers">
      <h2>Manage Users</h2>
      <div class="card card-pad">
        <form class="user-search" id="userSearch">
          <div class="search">${I.search}<input type="search" id="uName" placeholder="Search by name…" value="${esc(f.name)}" aria-label="Name"></div>
          <select id="uRole" aria-label="Role">${ROLE_FILTERS.map(([v, l]) => `<option value="${v}" ${v === f.role ? "selected" : ""}>${l}</option>`).join("")}</select>
          <button class="btn btn-primary" type="submit">Search</button>
        </form>
        <div id="userResults" class="user-results"><p class="muted small">Search by name, pick a role, or leave both empty and press Search to list everyone.</p></div>
      </div>
      <a href="#/add-user" class="btn add-user-btn">${I.plus.replace("<svg", '<svg width="16" height="16"')} Add a worker</a>
    </section>`;
  }

  function bindManageUsers() {
    const f = manageUsersSection.f;
    const run = async () => {
      f.name = $("#uName").value.trim(); f.role = $("#uRole").value;
      const box = $("#userResults"); box.innerHTML = `<p class="muted small">Searching…</p>`;
      try {
        const users = await api.adminSearch(f.name, f.role);
        box.innerHTML = users.length ? `<ul class="user-list">${users.map(userRow).join("")}</ul>` : `<p class="muted small">No users found.</p>`;
        box.querySelectorAll("[data-promote]").forEach(b => b.onclick = () => askChange("promote", users.find(u => u.userId === Number(b.dataset.promote))));
        box.querySelectorAll("[data-remove]").forEach(b => b.onclick = () => askChange("remove", users.find(u => u.userId === Number(b.dataset.remove))));
      } catch (err) { box.innerHTML = `<div class="error-msg">${esc(err.message)}</div>`; }
    };
    $("#userSearch").onsubmit = e => { e.preventDefault(); run(); };
    if (f.searched) run();
    $("#userSearch").addEventListener("submit", () => (f.searched = true));
  }

  const NEXT_ROLE = { Requester: "Staff", Staff: "Manager" };
  function userRow(u) {
    const self = u.userId === session.user.userId, active = u.isActive !== false;
    const next = NEXT_ROLE[appRole(u.role)];
    const why = self ? "You can't change your own access" : !active ? "Access already removed" : "";
    return `<li class="user-row">
      <span class="avatar sm">${initials(u.fullName)}</span>
      <span class="user-name">${esc(u.fullName)} <span class="muted small">${esc(roleLabel(u.role))}</span>${active ? "" : ` <span class="tag-off">No access</span>`}</span>
      <span class="user-actions">
        <button class="btn btn-sm" data-promote="${u.userId}" ${self || !active || !next ? "disabled" : ""} title="${esc(why || (next ? `Promote to ${ROLE_LABEL[next]}` : "Can't be promoted further"))}">Promote</button>
        <button class="btn btn-sm btn-danger" data-remove="${u.userId}" ${self || !active ? "disabled" : ""} title="${esc(why || "Remove access")}">Remove access</button>
      </span></li>`;
  }

  function askChange(kind, u) {
    if (!u) return;
    const next = ROLE_LABEL[NEXT_ROLE[appRole(u.role)]];
    const title = kind === "promote" ? `Promote ${u.fullName} to ${next}?` : `Remove access for ${u.fullName}?`;
    const body = kind === "promote"
      ? `They're currently a ${roleLabel(u.role)}. Another admin will need to approve this before it takes effect.`
      : `They won't be able to sign in. Another admin will need to approve this before it takes effect.`;
    confirmModal(title, body, "Yes", async () => {
      const res = kind === "promote" ? await api.adminPromote(u.userId) : await api.adminRemoveAccess(u.userId);
      return () => infoModal("Waiting for approval", res.message).then(route);
    });
  }

  // ---------- Modals (no browser dialogs) ----------
  function modal(html) {
    const wrap = document.createElement("div");
    wrap.className = "modal-backdrop";
    wrap.innerHTML = `<div class="modal" role="dialog" aria-modal="true">${html}</div>`;
    document.body.appendChild(wrap);
    const close = () => wrap.remove();
    wrap.addEventListener("click", e => { if (e.target === wrap) close(); });
    document.addEventListener("keydown", function esc(e) { if (e.key === "Escape") { close(); document.removeEventListener("keydown", esc); } });
    return { el: wrap, close };
  }

  // Are-you-sure popup with Yes / No. onYes may throw; the error shows in the popup.
  function confirmModal(title, body, yesLabel, onYes) {
    const m = modal(`<h2>${esc(title)}</h2><p class="muted">${esc(body)}</p><div class="modal-err"></div>
      <div class="actions-row"><button class="btn" data-no>No</button><button class="btn btn-primary" data-yes>${esc(yesLabel)}</button></div>`);
    m.el.querySelector("[data-no]").onclick = m.close;
    const yes = m.el.querySelector("[data-yes]");
    yes.focus();
    yes.onclick = async () => {
      yes.disabled = true;
      try { const next = await onYes(); m.close(); if (typeof next === "function") next(); }
      catch (err) { m.el.querySelector(".modal-err").innerHTML = `<div class="error-msg">${esc(err.message)}</div>`; yes.disabled = false; }
    };
  }

  function infoModal(title, body) {
    return new Promise(resolve => {
      const m = modal(`<div class="modal-icon">⏳</div><h2>${esc(title)}</h2><p class="muted">${esc(body)}</p>
        <p class="muted small">The other admins have been notified.</p>
        <div class="actions-row"><button class="btn btn-primary" data-ok>OK</button></div>`);
      const ok = m.el.querySelector("[data-ok]"); ok.focus();
      ok.onclick = () => { m.close(); resolve(); };
      m.el.addEventListener("click", e => { if (e.target === m.el) resolve(); });
    });
  }

  // ---------- Dev: logs ----------
  const SEV_CLASS = { Error: "b-rejected", Warning: "b-pending", Information: "b-progress" };
  const ACTION_TEXT = {
    SERVER_EXCEPTION: "Server crash", SERVER_ERROR: "Server error", ACCESS_DENIED: "Access denied",
    BROWSER_ERROR: "Browser error", EMAIL_SEND_FAILED: "Email not sent", CREATE: "Request created", UPDATE_STATUS: "Status changed"
  };

  async function viewLogs() {
    const f = viewLogs.f || (viewLogs.f = { severity: "Error", search: "" });
    render(`<div class="page-head"><h1>Logs</h1><button class="btn" id="logRefresh">Refresh</button></div>
      <div class="stats" id="logStats"></div>
      <form class="user-search log-filters" id="logForm">
        <div class="search">${I.search}<input type="search" id="logSearch" placeholder="Search action, user, page or message…" value="${esc(f.search)}"></div>
        <select id="logSev">${[["", "All severities"], ["Error", "Errors"], ["Warning", "Warnings"], ["Information", "Information"]].map(([v, l]) => `<option value="${v}" ${v === f.severity ? "selected" : ""}>${l}</option>`).join("")}</select>
        <button class="btn btn-primary" type="submit">Search</button>
      </form>
      <div class="card" id="logList"><div class="empty">Loading…</div></div>
      <p class="muted small" style="margin-top:10px">Newest first, up to 300 entries. Times are shown in your local time zone.</p>`);

    const load = async () => {
      f.search = $("#logSearch").value.trim(); f.severity = $("#logSev").value;
      $("#logList").innerHTML = `<div class="empty">Loading…</div>`;
      try {
        const data = await api.logs(f.severity, f.search);
        const count = sev => (data.last24Hours.find(c => c.severity === sev) || { count: 0 }).count;
        $("#logStats").innerHTML = stat(count("Error"), "Errors (24h)", "var(--danger)") + stat(count("Warning"), "Warnings (24h)", "var(--pending)") +
          stat(count("Information"), "Info (24h)", "var(--progress)") + stat(data.entries.length, "Shown below");
        $("#logList").innerHTML = data.entries.length ? `<ul class="log-list">${data.entries.map(logRow).join("")}</ul>`
          : `<div class="empty">No log entries match.${f.severity === "Error" ? " No errors — nice." : ""}</div>`;
        $("#logList").querySelectorAll(".log-row").forEach(row => row.onclick = e => {
          if (e.target.closest("pre")) return;
          row.classList.toggle("open");
        });
      } catch (err) { $("#logList").innerHTML = `<div class="empty">${esc(err.message)}</div>`; }
    };
    $("#logForm").onsubmit = e => { e.preventDefault(); load(); };
    $("#logSev").onchange = load;
    $("#logRefresh").onclick = load;
    load();
  }

  function logRow(l) {
    const first = String(l.details || "").split("\n")[0];
    const ticket = l.requestId ? ` · <a href="#/manage/${l.requestId}">request #${l.requestId}</a>` : "";
    return `<li class="log-row" tabindex="0">
      <div class="log-head">
        <span class="badge ${SEV_CLASS[l.severity] || "b-progress"}">${esc(l.severity)}</span>
        <span class="log-action">${esc(ACTION_TEXT[l.action] || l.action)}</span>
        <span class="log-msg">${esc(first)}</span>
        <span class="log-meta muted small">${fmtDateTime(l.loggedAt)} · ${esc(l.actor)}${l.entityId ? ` · ${esc(l.entityId)}` : ""}${ticket}</span>
      </div>
      <pre class="log-details">${esc(l.details || "(no details)")}</pre>
    </li>`;
  }

  // ---------- Add a worker ----------
  function viewAddUser() {
    render(`<a class="back" href="#/management">${I.back} Back to Management</a>
      <div class="page-head"><h1>Add a worker</h1></div>
      <div class="card card-pad"><form class="form" id="addUserForm" novalidate>
        <div id="addErr"></div>
        <div class="field"><label for="nName">Name <span class="req">*</span></label>
          <input type="text" id="nName" maxlength="150" autocomplete="off"></div>
        <div class="field"><label for="nEmail">Email <span class="req">*</span></label>
          <input type="text" id="nEmail" maxlength="255" autocomplete="off"></div>
        <div class="field"><label for="nPw">Temporary password <span class="req">*</span></label>
          <input type="password" id="nPw" autocomplete="new-password">
          <span class="hint">At least 8 characters. Share it with them securely; they can sign in straight away.</span></div>
        <div class="field"><label>Role <span class="req">*</span></label>
          <div class="role-choice">
            ${[["Staff", "Worker", "Handles service requests"], ["Dev", "Dev", "Developer access"], ["Manager", "Admin", "Needs a second admin's approval"]]
              .map(([v, l, d], i) => `<label><input type="radio" name="nRole" value="${v}" ${i === 0 ? "checked" : ""}><span><strong>${l}</strong><small>${d}</small></span></label>`).join("")}
          </div></div>
        <div class="form-actions"><a href="#/management" class="btn">Cancel</a><button class="btn btn-primary" type="submit" id="addBtn">Add</button></div>
      </form></div>`);
    $("#addUserForm").onsubmit = e => {
      e.preventDefault();
      const body = { fullName: $("#nName").value.trim(), email: $("#nEmail").value.trim(), password: $("#nPw").value, role: $("input[name=nRole]:checked").value };
      const err = !body.fullName ? "Please enter a name." :
        !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(body.email) ? "Please enter a valid email address." :
        body.password.length < 8 ? "Password must be at least 8 characters." : "";
      if (err) { $("#addErr").innerHTML = `<div class="error-msg">${esc(err)}</div>`; return; }
      $("#addErr").innerHTML = "";
      const label = ROLE_LABEL[body.role];
      confirmModal(`Add ${body.fullName} as ${label}?`,
        body.role === "Manager" ? "Another admin will need to approve this before the account is created." : `They'll be able to sign in as a ${label} straight away.`,
        "Yes", async () => {
          const res = await api.adminCreateUser(body);
          location.hash = "#/management";
          if (res.pending) return () => infoModal("Waiting for approval", res.message);
          toast(res.message);
        });
    };
  }

  async function viewUsers() {
    const users = await api.users();
    render(`<div class="page-head"><h1>Users</h1></div><div class="card"><div class="table-wrap"><table>
      <thead><tr><th>Name</th><th>Email</th><th>Role</th><th>Joined</th></tr></thead>
      <tbody>${users.map(u => `<tr><td>${esc(u.fullName)}</td><td class="muted">${esc(u.email)}</td><td>${esc(roleLabel(u.role))}${u.isActive === false ? ` <span class="tag-off">No access</span>` : ""}</td><td class="muted">${fmtDate(u.createdAt)}</td></tr>`).join("") || `<tr><td colspan="4" class="empty">No users yet.</td></tr>`}</tbody></table></div></div>
      <p class="muted" style="margin-top:12px">New sign-ups are Requesters. Promote people or remove their access from the Dashboard.</p>`);
  }

  const viewPlaceholder = (title, text) => render(`<div class="page-head"><h1>${title}</h1></div><div class="card empty">${text}</div>`);

  // ---------- Router ----------
  const ACCESS = {
    "dashboard": ["Requester", "Staff", "Manager", "Dev"], "my-requests": ["Requester", "Staff", "Manager", "Dev"],
    "submit": ["Requester", "Staff", "Manager", "Dev"], "request": ["Requester", "Staff", "Manager", "Dev"],
    "queue": ["Staff", "Manager", "Dev"], "assignments": ["Staff"], "manage": ["Staff", "Manager", "Dev"], "reports": ["Staff", "Manager", "Dev"],
    "management": ["Manager", "Dev"], "analytics": ["Manager", "Dev"], "users": ["Manager", "Dev"], "add-user": ["Manager", "Dev"], "logs": ["Dev"]
  };

  async function route() {
    const [, page = "", param] = location.hash.split("/");
    if (!/^#\/(request|manage)\//.test(location.hash)) viewDetails.tab = null;

    if (!session.user || !session.user.userId) {
      session.user = null;
      if (page === "register") return viewRegister();
      if (page === "login") return viewLogin();
      if (page === "confirm") return viewConfirm();
      // Guests land on the dashboard. Pages that need an account show it behind a sign-in prompt.
      const target = location.hash;
      const needsSignIn = GUEST_REASON[page];
      if (!needsSignIn) store.set("returnTo", null);
      if (page !== "dashboard") history.replaceState(null, "", "#/dashboard");
      if (!$(".hero")) { loading(); await viewRequesterDashboard(); }
      if (needsSignIn) promptSignIn(target, needsSignIn);
      return;
    }
    if (page === "login" || page === "register" || page === "confirm" || !page || !ACCESS[page] || !ACCESS[page].includes(role())) {
      location.hash = HOME[role()]; return;
    }
    loading();
    try {
      switch (page) {
        case "dashboard": return await viewRequesterDashboard();
        case "my-requests": return await viewMyRequests();
        case "submit": return await viewSubmit(param);
        case "request": return await viewDetails(Number(param), false);
        case "manage": return await viewDetails(Number(param), true);
        case "queue": return await viewQueue(false);
        case "assignments": return await viewQueue(true);
        case "management": return await viewManagement();
        case "users": return await viewUsers();
        case "add-user": return viewAddUser();
        case "logs": return await viewLogs();
        case "reports": return viewPlaceholder("Reports", "Exportable reports will appear here once a reporting endpoint is added.");
        case "analytics": return viewPlaceholder("Analytics", "Trends over time will appear here in a later milestone.");
      }
    } catch (err) {
      if (!err.status) reportError(`Page ${location.hash} failed: ${err.message}`, err.stack);
      render(`<div class="card empty">${esc(err.message || "Something went wrong.")}</div>`);
    }
  }

  window.addEventListener("hashchange", route);
  route();
})();

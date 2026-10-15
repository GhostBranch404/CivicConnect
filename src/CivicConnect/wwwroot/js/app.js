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
  const statusBadge = s => `<span class="badge ${s === "Resolved" ? "b-resolved" : s === "In Progress" ? "b-progress" : "b-pending"}">${esc(s)}</span>`;
  const isOverdue = r => r.requestStatus !== "Resolved" && (Date.now() - toDate(r.requestDate)) / 864e5 > OVERDUE_DAYS;
  const store = {
    get(k) { try { return JSON.parse(localStorage.getItem("cc." + k)); } catch { return null; } },
    set(k, v) { try { v == null ? localStorage.removeItem("cc." + k) : localStorage.setItem("cc." + k, JSON.stringify(v)); } catch { /* ignore */ } }
  };

  // Map database role names onto the three areas of the app.
  function appRole(role) {
    const r = String(role || "").toLowerCase();
    if (r === "staff") return "Staff";
    if (r === "manager" || r === "management" || r === "admin") return "Manager";
    return "Requester"; // Requester, Citizen, anything else
  }

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
        headers: body ? { "Content-Type": "application/json", Accept: "application/json" } : { Accept: "application/json" },
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
    assign: (id, staffUserId, changedByUserId) => http("PUT", `/api/requests/${id}/assignment`, { staffUserId, changedByUserId })
  };

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
    logo: `<svg viewBox="0 0 40 40" fill="none"><rect width="40" height="40" rx="10" fill="#0f7a6c"/><path d="M11 25.5c0-6.4 4-10.5 9-10.5s9 4.1 9 10.5" stroke="#fff" stroke-width="2.6" stroke-linecap="round"/><path d="M8 26h24" stroke="#fff" stroke-width="2.6" stroke-linecap="round"/><circle cx="20" cy="11" r="2.4" fill="#9fe3d6"/><path d="M16 26v-5M20 26v-6M24 26v-5" stroke="#fff" stroke-width="2.2" stroke-linecap="round"/></svg>`,
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
    menu: `<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 6h18M3 12h18M3 18h18"/></svg>`
  };

  // ---------- Session & shell ----------
  const session = { user: store.get("user") };
  const role = () => appRole(session.user && session.user.role);
  function signIn(user) { session.user = user; store.set("user", user); location.hash = HOME[appRole(user.role)]; }
  function signOut() { session.user = null; store.set("user", null); location.hash = "#/login"; }

  const NAV = {
    Requester: [["#/dashboard", "Dashboard", I.home], ["#/my-requests", "My Requests", I.list], ["#/submit", "Submit Request", I.plus]],
    Staff: [["#/queue", "Request Queue", I.inbox], ["#/assignments", "My Assignments", I.user], ["#/reports", "Reports", I.report]],
    Manager: [["#/management", "Dashboard", I.home], ["#/queue", "Request Queue", I.inbox], ["#/reports", "Reports", I.report], ["#/analytics", "Analytics", I.chart], ["#/users", "Users", I.users]]
  };
  const HOME = { Requester: "#/dashboard", Staff: "#/queue", Manager: "#/management" };

  function shell(inner) {
    const u = session.user, hash = location.hash;
    const nav = NAV[role()].map(([href, label, icon]) => {
      const active = hash === href || hash.startsWith(href + "/") ||
        (href === "#/my-requests" && hash.startsWith("#/request/")) || (href === "#/queue" && hash.startsWith("#/manage/"));
      return `<a href="${href}" class="${active ? "active" : ""}">${icon}<span>${label}</span></a>`;
    }).join("");
    return `<div class="shell" id="shell">
      <aside class="sidebar">
        <div class="brand">${I.logo}<span>CivicConnect</span></div>
        <nav class="nav">${nav}</nav>
        <div class="user-box">
          <div class="avatar">${initials(u.fullName)}</div>
          <div><div class="name">${esc(u.fullName)}</div><div class="role">${esc(role())}</div></div>
          <button id="logout" title="Sign out" aria-label="Sign out">${I.logout}</button>
        </div>
      </aside>
      <div class="main">
        <header class="topbar">
          <button class="menu-btn" id="menu" aria-label="Open menu">${I.menu}</button>
          <div class="avatar" title="${esc(u.fullName)}">${initials(u.fullName)}</div>
        </header>
        <main class="content">${inner}</main>
      </div>
    </div>`;
  }

  function render(inner) {
    $("#app").innerHTML = shell(inner);
    $("#logout").onclick = signOut;
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
    $("#app").innerHTML = `<div class="login-wrap"><div class="login-card"><div class="logo">${I.logo}</div>${inner}</div></div>`;
  }

  function viewLogin() {
    authCard(`<h1>CivicConnect</h1>
      <p class="tag">Community Service Request Management</p>
      <form id="loginForm" novalidate>
        <div id="loginErr"></div>
        <div class="input-icon">${I.mail}<input type="text" id="email" placeholder="Email" autocomplete="username"></div>
        <div class="input-icon">${I.lock}<input type="password" id="pw" placeholder="Password" autocomplete="current-password"></div>
        <button class="btn btn-primary btn-block" type="submit" id="loginBtn">Login</button>
      </form>
      <a href="#/login" class="forgot" id="forgot">Forgot password?</a>
      <p class="switch-auth">Don't have an account? <a href="#/register">Register</a></p>`);
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

  function viewRegister() {
    authCard(`<h1>Create an account</h1>
      <p class="tag">Register to submit and track service requests</p>
      <form id="regForm" novalidate>
        <div id="regErr"></div>
        <div class="input-icon">${I.user}<input type="text" id="rname" placeholder="Full name" autocomplete="name"></div>
        <div class="input-icon">${I.mail}<input type="text" id="remail" placeholder="Email" autocomplete="email"></div>
        <div class="input-icon">${I.lock}<input type="password" id="rpw" placeholder="Password (min. 8 characters)" autocomplete="new-password"></div>
        <div class="input-icon">${I.lock}<input type="password" id="rpw2" placeholder="Confirm password" autocomplete="new-password"></div>
        <button class="btn btn-primary btn-block" type="submit" id="regBtn">Register</button>
      </form>
      <p class="switch-auth">Already have an account? <a href="#/login">Log in</a></p>`);
    $("#regForm").onsubmit = async e => {
      e.preventDefault();
      const name = $("#rname").value.trim(), email = $("#remail").value.trim(), pw = $("#rpw").value, pw2 = $("#rpw2").value;
      const err = !name ? "Please enter your full name." :
        !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) ? "Please enter a valid email address." :
        pw.length < 8 ? "Password must be at least 8 characters." :
        pw !== pw2 ? "Passwords don't match." : "";
      if (err) { $("#regErr").innerHTML = `<div class="error-msg">${esc(err)}</div>`; return; }
      $("#regBtn").disabled = true;
      try {
        const user = await api.register(name, email, pw);
        toast(`Welcome, ${name.split(" ")[0]}! Your account has been created.`);
        signIn(user);
      } catch (ex) {
        $("#regErr").innerHTML = `<div class="error-msg">${esc(ex.message)}</div>`;
        $("#regBtn").disabled = false;
      }
    };
  }

  // ---------- Requester ----------
  async function viewRequesterDashboard() {
    const mine = (await loadRequests()).filter(r => r.requesterId === session.user.userId);
    const c = s => mine.filter(r => r.requestStatus === s).length;
    render(`<div class="page-head"><h1>Dashboard</h1><a href="#/submit" class="btn btn-primary">New request</a></div>
      <div class="stats">
        ${stat(mine.length, "Total Requests")}${stat(c("Pending"), "Pending", "var(--pending)")}
        ${stat(c("In Progress"), "In Progress", "var(--progress)")}${stat(c("Resolved"), "Resolved", "var(--resolved)")}
      </div>
      <h2>My Recent Requests</h2>
      <div class="card">${requestTable(mine.slice(0, 5), [COL.ticket, COL.title, COL.status, COL.date], "#/request/", "You haven't submitted any requests yet.")}</div>`);
  }

  async function viewMyRequests() {
    const mine = (await loadRequests()).filter(r => r.requesterId === session.user.userId);
    render(`<div class="page-head"><h1>My Requests</h1><a href="#/submit" class="btn btn-primary">New request</a></div>
      <div class="card">${requestTable(mine, [COL.ticket, COL.title, COL.category, COL.status, COL.date], "#/request/", "You haven't submitted any requests yet.")}</div>`);
  }

  async function viewSubmit() {
    await loadRef();
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
          <div class="field"><label for="notes">Notes <span class="hint">(required when resolving)</span></label>
          <textarea id="notes" style="min-height:80px" placeholder="What was done?"></textarea></div></div>
        <div id="actErr" style="margin-top:12px"></div>
        <div class="actions-row"><button class="btn" id="saveBtn">Update Status</button>
          <button class="btn btn-primary" id="resolveBtn" ${r.requestStatus === "Resolved" ? "disabled" : ""}>Resolve</button></div>` : ""}`;

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
      const apply = async resolve => {
        const newStatus = resolve ? "Resolved" : $("#status").value, notes = $("#notes").value.trim();
        const assignee = Number($("#assignee").value) || null;
        $("#actErr").innerHTML = "";
        if (resolve && !notes) { $("#actErr").innerHTML = `<div class="error-msg">Resolution notes are required when resolving a request.</div>`; return; }
        $("#saveBtn").disabled = $("#resolveBtn").disabled = true;
        try {
          if (assignee !== (assignment ? assignment.staffUserId : null))
            await api.assign(id, assignee, session.user.userId);
          if (newStatus !== r.requestStatus || (resolve && notes))
            await api.updateStatus(id, { newStatus, changedByUserId: session.user.userId, expectedVersion: r.version, notes: notes || null });
          toast(resolve ? "Request resolved." : "Request updated.");
          viewDetails(id, true);
        } catch (err) {
          $("#actErr").innerHTML = `<div class="error-msg">${esc(err.message)}</div>`;
          $("#saveBtn").disabled = false; $("#resolveBtn").disabled = r.requestStatus === "Resolved";
        }
      };
      $("#saveBtn").onclick = () => apply(false);
      $("#resolveBtn").onclick = () => apply(true);
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
    const colorFor = n => n === "Resolved" ? "var(--resolved)" : n === "In Progress" ? "var(--progress)" : "var(--pending)";
    const statusRows = ref.statuses.map(s => ({ n: s.statusName, c: colorFor(s.statusName), v: by(s.statusName) }));
    const overdue = all.filter(isOverdue).length;
    const maxS = Math.max(1, ...statusRows.map(s => s.v), overdue);
    render(`<div class="page-head"><h1>Management Dashboard</h1></div>
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
      <div class="card">${requestTable(all.slice(0, 8), [COL.ticket, COL.category, COL.title, COL.requester, COL.status, COL.date], "#/manage/")}</div>`);
  }

  async function viewUsers() {
    const users = await api.users();
    render(`<div class="page-head"><h1>Users</h1></div><div class="card"><div class="table-wrap"><table>
      <thead><tr><th>Name</th><th>Email</th><th>Role</th><th>Joined</th></tr></thead>
      <tbody>${users.map(u => `<tr><td>${esc(u.fullName)}</td><td class="muted">${esc(u.email)}</td><td>${esc(u.role)}</td><td class="muted">${fmtDate(u.createdAt)}</td></tr>`).join("") || `<tr><td colspan="4" class="empty">No users yet.</td></tr>`}</tbody></table></div></div>
      <p class="muted" style="margin-top:12px">New sign-ups are Requesters. To make someone Staff or a Manager, update their Role in the Users table.</p>`);
  }

  const viewPlaceholder = (title, text) => render(`<div class="page-head"><h1>${title}</h1></div><div class="card empty">${text}</div>`);

  // ---------- Router ----------
  const ACCESS = {
    "dashboard": ["Requester"], "my-requests": ["Requester"], "submit": ["Requester"], "request": ["Requester", "Staff", "Manager"],
    "queue": ["Staff", "Manager"], "assignments": ["Staff"], "manage": ["Staff", "Manager"], "reports": ["Staff", "Manager"],
    "management": ["Manager"], "analytics": ["Manager"], "users": ["Manager"]
  };

  async function route() {
    const [, page = "", param] = location.hash.split("/");
    if (!/^#\/(request|manage)\//.test(location.hash)) viewDetails.tab = null;

    if (!session.user || !session.user.userId) {
      session.user = null;
      return page === "register" ? viewRegister() : viewLogin();
    }
    if (page === "login" || page === "register" || !page || !ACCESS[page] || !ACCESS[page].includes(role())) {
      location.hash = HOME[role()]; return;
    }
    loading();
    try {
      switch (page) {
        case "dashboard": return await viewRequesterDashboard();
        case "my-requests": return await viewMyRequests();
        case "submit": return await viewSubmit();
        case "request": return await viewDetails(Number(param), false);
        case "manage": return await viewDetails(Number(param), true);
        case "queue": return await viewQueue(false);
        case "assignments": return await viewQueue(true);
        case "management": return await viewManagement();
        case "users": return await viewUsers();
        case "reports": return viewPlaceholder("Reports", "Exportable reports will appear here once a reporting endpoint is added.");
        case "analytics": return viewPlaceholder("Analytics", "Trends over time will appear here in a later milestone.");
      }
    } catch (err) {
      render(`<div class="card empty">${esc(err.message || "Something went wrong.")}</div>`);
    }
  }

  window.addEventListener("hashchange", route);
  route();
})();

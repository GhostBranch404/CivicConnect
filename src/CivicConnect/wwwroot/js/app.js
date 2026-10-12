/* CivicConnect front end
 * Talks to the ASP.NET API at /api/requests (GET, GET {id}, POST, PATCH {id}/status).
 * If the API (or its database) can't be reached, it switches to demo mode with sample data,
 * so the screens can still be reviewed.
 *
 * Not in the API yet (kept in the browser for now): sign-in, users, priority, assignment,
 * comments and reading request history. See the TODO notes near the `extras` store.
 */
(() => {
  "use strict";

  // ---------- Reference data (mirrors 001_initial_schema.sql seeds) ----------
  const CATEGORIES = [
    { id: 1, name: "Maintenance" }, { id: 2, name: "IT Support" }, { id: 3, name: "Security" },
    { id: 4, name: "Facilities" }, { id: 5, name: "Other" }
  ];
  const STATUSES = [
    { id: 1, name: "Pending" }, { id: 2, name: "In Progress" }, { id: 3, name: "Resolved" }
  ];
  const PRIORITIES = ["Low", "Medium", "High"];
  const OVERDUE_DAYS = 7; // open requests older than this count as overdue on the management view

  // Demo users. Replace with the Users table once an auth/users endpoint exists.
  const USERS = [
    { id: 1, name: "John Doe", email: "john@civicconnect.local", role: "Requester" },
    { id: 2, name: "Sarah Smith", email: "sarah@civicconnect.local", role: "Staff" },
    { id: 4, name: "Thabo Mokoena", email: "thabo@civicconnect.local", role: "Staff" },
    { id: 3, name: "Admin User", email: "admin@civicconnect.local", role: "Manager" }
  ];
  const STAFF = USERS.filter(u => u.role === "Staff");
  const userName = id => (USERS.find(u => u.id === id) || {}).name || `User #${id}`;

  // ---------- Storage helpers ----------
  const store = {
    get(k, d) { try { const v = localStorage.getItem("cc." + k); return v ? JSON.parse(v) : d; } catch { return d; } },
    set(k, v) { try { localStorage.setItem("cc." + k, JSON.stringify(v)); } catch { /* ignore */ } }
  };

  // TODO(API): priority, assignment, comments and history have tables in the schema
  // (Assignments, RequestComments, RequestHistory) but no endpoints yet. Until then they
  // are kept per browser here, keyed by RequestId.
  const extras = {
    all() { return store.get("extras", {}); },
    of(id) { return this.all()[id] || { priority: "Medium", assignedTo: null, comments: [], history: [] }; },
    save(id, data) { const a = this.all(); a[id] = data; store.set("extras", a); }
  };

  // ---------- API layer with demo fallback ----------
  const api = {
    mode: "unknown", // "live" | "demo"
    async probe() {
      try {
        const r = await fetch("/api/requests", { headers: { Accept: "application/json" } });
        if (!r.ok) throw new Error();
        this.mode = "live";
        this.cache = (await r.json()).map(normalise);
      } catch {
        this.mode = "demo";
        seedDemo();
      }
    },
    async list() {
      if (this.mode === "live") {
        const r = await fetch("/api/requests");
        if (!r.ok) throw new Error("Could not load requests.");
        return (this.cache = (await r.json()).map(normalise));
      }
      return store.get("demoRequests", []).map(normalise);
    },
    async get(id) {
      if (this.mode === "live") {
        const r = await fetch(`/api/requests/${id}`);
        if (r.status === 404) return null;
        if (!r.ok) throw new Error(await errText(r));
        return normalise(await r.json());
      }
      return (await this.list()).find(x => x.requestId === id) || null;
    },
    async create(body) {
      if (this.mode === "live") {
        const r = await fetch("/api/requests", {
          method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body)
        });
        if (!r.ok) throw new Error(await errText(r));
        const d = await r.json();
        return d.requestId ?? d.RequestId;
      }
      // Mirror RequestService.RequestSubmission validation
      for (const [k, m] of [["title", "A title is required."], ["description", "A description is required."], ["location", "A location is required."]])
        if (!String(body[k] || "").trim()) throw new Error(m);
      if (!(body.categoryId > 0)) throw new Error("A valid category is required.");
      const list = store.get("demoRequests", []);
      const id = Math.max(0, ...list.map(x => x.requestId)) + 1;
      const now = new Date().toISOString();
      list.push({ ...body, requestId: id, ticketNumber: `CC-${String(id + 120).padStart(5, "0")}`, statusId: 1,
        requestDate: now, updatedAt: now, version: 1 });
      store.set("demoRequests", list);
      return id;
    },
    async updateStatus(id, payload) {
      if (this.mode === "live") {
        const r = await fetch(`/api/requests/${id}/status`, {
          method: "PATCH", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload)
        });
        if (!r.ok) throw new Error(await errText(r));
        return true;
      }
      if (payload.newStatus === "Resolved" && !String(payload.notes || "").trim())
        throw new Error("Resolution notes are required when resolving a request.");
      const list = store.get("demoRequests", []);
      const req = list.find(x => x.requestId === id);
      if (!req) throw new Error(`Request #${id} was not found.`);
      if (req.version !== payload.expectedVersion)
        throw new Error("This request was changed by someone else. Reload and try again.");
      req.statusId = STATUSES.find(s => s.name === payload.newStatus).id;
      req.version++; req.updatedAt = new Date().toISOString();
      store.set("demoRequests", list);
      return true;
    }
  };

  async function errText(r) {
    try { const d = await r.json(); return d.message || d.Message || d.title || `Request failed (${r.status}).`; }
    catch { return `Request failed (${r.status}).`; }
  }

  // Accept PascalCase or camelCase from the API and fill in display names.
  function normalise(x) {
    const g = (a, b) => x[a] !== undefined ? x[a] : x[b];
    const o = {
      requestId: g("requestId", "RequestId"), ticketNumber: g("ticketNumber", "TicketNumber"),
      requesterId: g("requesterId", "RequesterId"), categoryId: g("categoryId", "CategoryId"),
      statusId: g("statusId", "StatusId"), title: g("title", "Title"), description: g("description", "Description"),
      location: g("location", "Location"), requestDate: g("requestDate", "RequestDate"),
      updatedAt: g("updatedAt", "UpdatedAt"), version: g("version", "Version"),
      requesterName: g("requesterName", "RequesterName"), categoryName: g("categoryName", "CategoryName"),
      requestStatus: g("requestStatus", "RequestStatus")
    };
    o.categoryName ||= (CATEGORIES.find(c => c.id === o.categoryId) || {}).name || "—";
    o.requestStatus ||= (STATUSES.find(s => s.id === o.statusId) || {}).name || "Pending";
    o.requesterName ||= userName(o.requesterId);
    o.ticketNumber ||= `#${o.requestId}`;
    const ex = extras.of(o.requestId);
    o.priority = ex.priority; o.assignedTo = ex.assignedTo;
    return o;
  }

  function seedDemo() {
    if (store.get("demoSeeded3", false)) return;
    const d = n => new Date(Date.now() - n * 864e5).toISOString();
    const rows = [
      [1, 1, 2, "Streetlight out on Main Road", "The streetlight outside no. 42 has been off for three nights. The corner is very dark.", "42 Main Road", 12, "High", 2],
      [1, 4, 1, "Community hall booking for youth day", "Requesting the hall on the 16th from 09:00–14:00 for a youth workshop (about 40 people).", "Ward 5 Community Hall", 9, "Low", null],
      [1, 2, 3, "Library Wi-Fi not working", "Public Wi-Fi at the library drops every few minutes, so people can't finish online applications.", "Central Library", 6, "Medium", 4],
      [1, 1, 3, "Burst water pipe near park entrance", "Water has been running across the pavement since yesterday morning.", "Oak Park, east gate", 4, "High", 2],
      [1, 3, 1, "Broken gate lock at clinic", "The side gate lock is broken and the gate can't be closed overnight.", "Riverside Clinic", 1, "High", null],
      [5, 5, 2, "Illegal dumping behind shops", "Large pile of building rubble dumped behind the shopping centre.", "Maple Shopping Centre", 10, "Medium", 4],
      [5, 1, 1, "Pothole on Church Street", "Deep pothole in the left lane; two cars have had flat tyres.", "Church St & 3rd Ave", 2, "Medium", null],
      [6, 4, 3, "Toilets out of order at sports field", "Both public toilets are blocked.", "Greenfield Sports Ground", 15, "Low", 2]
    ];
    const reqs = [], ex = {};
    rows.forEach((r, i) => {
      const id = i + 1, [requesterId, categoryId, statusId, title, description, location, age, priority, staff] = r;
      reqs.push({ requestId: id, ticketNumber: `CC-${String(id + 120).padStart(5, "0")}`, requesterId, categoryId, statusId,
        title, description, location, requestDate: d(age), updatedAt: d(Math.max(0, age - 2)), version: statusId });
      const h = [{ at: d(age), by: requesterId, action: "Created", note: "Request submitted." }];
      if (staff) h.push({ at: d(age - 1), by: 3, action: "Assigned", note: `Assigned to ${userName(staff)}.` });
      if (statusId >= 2) h.push({ at: d(age - 1.5), by: staff || 2, action: "Status changed", note: "Pending → In Progress" });
      if (statusId === 3) h.push({ at: d(age - 2), by: staff || 2, action: "Resolved", note: "Work completed and checked on site." });
      ex[id] = { priority, assignedTo: staff, history: h,
        comments: staff ? [{ at: d(age - 1), by: staff, text: "Thanks — a team has been scheduled to look at this." }] : [] };
    });
    USERS.push({ id: 5, name: "Lerato Dube", role: "Requester" }, { id: 6, name: "Peter Naidoo", role: "Requester" });
    store.set("demoRequests", reqs); store.set("extras", ex); store.set("demoSeeded3", true);
  }

  // ---------- Utilities ----------
  const $ = (s, el = document) => el.querySelector(s);
  const esc = s => String(s ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
  const fmtDate = s => s ? new Date(s).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" }) : "—";
  const fmtDateTime = s => s ? new Date(s).toLocaleString(undefined, { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" }) : "—";
  const initials = n => n.split(" ").map(p => p[0]).slice(0, 2).join("").toUpperCase();
  const statusBadge = s => `<span class="badge ${s === "Resolved" ? "b-resolved" : s === "In Progress" ? "b-progress" : "b-pending"}">${esc(s)}</span>`;
  const priorityBadge = p => `<span class="badge p-${(p || "medium").toLowerCase()}">${esc(p || "Medium")}</span>`;
  const isOverdue = r => r.requestStatus !== "Resolved" && (Date.now() - new Date(r.requestDate)) / 864e5 > OVERDUE_DAYS;

  function toast(msg, err) {
    const t = $("#toast"); t.textContent = msg; t.className = "toast show" + (err ? " err" : "");
    clearTimeout(toast.t); toast.t = setTimeout(() => (t.className = "toast"), 3200);
  }

  function addHistory(id, action, note) {
    const ex = extras.of(id);
    ex.history = [...(ex.history || []), { at: new Date().toISOString(), by: session.user.id, action, note }];
    extras.save(id, ex);
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
    lock: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="4" y="11" width="16" height="10" rx="2"/><path d="M8 11V7a4 4 0 018 0v4"/></svg>`,
    search: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="7"/><path d="M21 21l-4.3-4.3"/></svg>`,
    back: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 12H5M12 19l-7-7 7-7"/></svg>`,
    logout: `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9"/></svg>`,
    menu: `<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 6h18M3 12h18M3 18h18"/></svg>`
  };

  // ---------- Session & navigation ----------
  const session = { user: store.get("user", null) };

  const NAV = {
    Requester: [["#/dashboard", "Dashboard", I.home], ["#/my-requests", "My Requests", I.list], ["#/submit", "Submit Request", I.plus]],
    Staff: [["#/queue", "Request Queue", I.inbox], ["#/assignments", "My Assignments", I.user], ["#/reports", "Reports", I.report]],
    Manager: [["#/management", "Dashboard", I.home], ["#/reports", "Reports", I.report], ["#/analytics", "Analytics", I.chart], ["#/users", "Users", I.users]]
  };
  const HOME = { Requester: "#/dashboard", Staff: "#/queue", Manager: "#/management" };

  function shell(inner) {
    const u = session.user, hash = location.hash;
    const nav = NAV[u.role].map(([href, label, icon]) => {
      const active = hash === href || hash.startsWith(href + "/") ||
        (href === "#/my-requests" && hash.startsWith("#/request/")) || (href === "#/queue" && hash.startsWith("#/manage/"));
      return `<a href="${href}" class="${active ? "active" : ""}">${icon}<span>${label}</span></a>`;
    }).join("");
    return `<div class="shell" id="shell">
      <aside class="sidebar">
        <div class="brand">${I.logo}<span>CivicConnect</span></div>
        <nav class="nav">${nav}</nav>
        <div class="user-box">
          <div class="avatar">${initials(u.name)}</div>
          <div><div class="name">${esc(u.name)}</div><div class="role">${esc(u.role)}</div></div>
          <button id="logout" title="Sign out" aria-label="Sign out">${I.logout}</button>
        </div>
      </aside>
      <div class="main">
        <header class="topbar">
          <button class="menu-btn" id="menu" aria-label="Open menu">${I.menu}</button>
          <span class="api-pill ${api.mode === "demo" ? "demo" : ""}" title="${api.mode === "demo" ? "The API could not be reached, so sample data is shown." : "Connected to /api/requests"}">${api.mode === "demo" ? "Demo data" : "Live API"}</span>
          <div class="avatar" title="${esc(u.name)}">${initials(u.name)}</div>
        </header>
        <main class="content">${inner}</main>
      </div>
    </div>`;
  }

  function bindShell() {
    $("#logout").onclick = () => { session.user = null; store.set("user", null); location.hash = "#/login"; };
    $("#menu").onclick = e => { e.stopPropagation(); $("#shell").classList.toggle("nav-open"); };
    $("#shell").addEventListener("click", e => {
      if (!e.target.closest(".sidebar") && !e.target.closest("#menu")) $("#shell").classList.remove("nav-open");
    });
  }

  function requestTable(rows, cols, linkBase) {
    if (!rows.length) return `<div class="empty">No requests to show.</div>`;
    const head = cols.map(c => `<th>${c[0]}</th>`).join("");
    const body = rows.map(r => `<tr class="clickable" data-href="${linkBase}${r.requestId}" tabindex="0">${cols.map(c => `<td>${c[1](r)}</td>`).join("")}</tr>`).join("");
    return `<div class="table-wrap"><table><thead><tr>${head}</tr></thead><tbody>${body}</tbody></table></div>`;
  }
  const COL = {
    ticket: ["Ticket #", r => `<span class="ticket">${esc(r.ticketNumber)}</span>`],
    title: ["Title", r => esc(r.title)],
    category: ["Category", r => esc(r.categoryName)],
    status: ["Status", r => statusBadge(r.requestStatus)],
    priority: ["Priority", r => priorityBadge(r.priority)],
    date: ["Date", r => `<span class="muted">${fmtDate(r.requestDate)}</span>`],
    assigned: ["Assigned to", r => r.assignedTo ? esc(userName(r.assignedTo)) : `<span class="muted">Unassigned</span>`]
  };
  function bindRows(root = document) {
    root.querySelectorAll("tr[data-href]").forEach(tr => {
      tr.onclick = () => (location.hash = tr.dataset.href);
      tr.onkeydown = e => { if (e.key === "Enter") location.hash = tr.dataset.href; };
    });
  }

  // ---------- Screens ----------

  // 1. Login
  function viewLogin() {
    $("#app").innerHTML = `<div class="login-wrap"><div class="login-card">
      <div class="logo">${I.logo}</div>
      <h1>CivicConnect</h1>
      <p class="tag">Community Service Request Management</p>
      <form id="loginForm" autocomplete="on">
        <div class="input-icon">${I.user}<input type="text" id="email" placeholder="Username or Email" required value="john@civicconnect.local"></div>
        <div class="input-icon">${I.lock}<input type="password" id="pw" placeholder="Password" value="demo"></div>
        <div>
          <div class="field-label" style="margin-bottom:6px">Sign in as (demo)</div>
          <div class="role-pick">
            ${["Requester", "Staff", "Manager"].map((r, i) => `<label><input type="radio" name="role" value="${r}" ${i === 0 ? "checked" : ""}><span>${r}</span></label>`).join("")}
          </div>
        </div>
        <button class="btn btn-primary btn-block" type="submit">Login</button>
      </form>
      <a href="#/login" class="forgot" id="forgot">Forgot password?</a>
    </div></div>`;
    const emails = { Requester: "john@civicconnect.local", Staff: "sarah@civicconnect.local", Manager: "admin@civicconnect.local" };
    document.querySelectorAll("input[name=role]").forEach(r => r.onchange = () => ($("#email").value = emails[r.value]));
    $("#forgot").onclick = e => { e.preventDefault(); toast("Password reset isn't available yet — contact your administrator."); };
    $("#loginForm").onsubmit = e => {
      e.preventDefault();
      // TODO(API): replace with a real auth endpoint. For now the selected role picks a demo user.
      const role = $("input[name=role]:checked").value;
      const email = $("#email").value.trim().toLowerCase();
      const user = USERS.find(u => u.email === email && u.role === role) || USERS.find(u => u.role === role);
      session.user = user; store.set("user", user);
      location.hash = HOME[role];
    };
  }

  // 2. Requester dashboard
  async function viewRequesterDashboard() {
    const all = await api.list();
    const mine = all.filter(r => r.requesterId === session.user.id).sort((a, b) => new Date(b.requestDate) - new Date(a.requestDate));
    const c = s => mine.filter(r => r.requestStatus === s).length;
    render(`<div class="page-head"><h1>Dashboard</h1><a href="#/submit" class="btn btn-primary">${I.plus.replace("<svg", '<svg width="16" height="16"')} New request</a></div>
      <div class="stats">
        ${stat(mine.length, "Total Requests")}${stat(c("Pending"), "Pending", "var(--pending)")}
        ${stat(c("In Progress"), "In Progress", "var(--progress)")}${stat(c("Resolved"), "Resolved", "var(--resolved)")}
      </div>
      <h2>My Recent Requests</h2>
      <div class="card">${requestTable(mine.slice(0, 5), [COL.ticket, COL.title, COL.status, COL.date], "#/request/")}</div>`);
  }
  const stat = (n, l, color) => `<div class="card stat"><div class="num">${n}</div><div class="lbl">${color ? `<span class="dot" style="background:${color}"></span>` : ""}${l}</div></div>`;

  async function viewMyRequests() {
    const mine = (await api.list()).filter(r => r.requesterId === session.user.id).sort((a, b) => new Date(b.requestDate) - new Date(a.requestDate));
    render(`<div class="page-head"><h1>My Requests</h1><a href="#/submit" class="btn btn-primary">New request</a></div>
      <div class="card">${requestTable(mine, [COL.ticket, COL.title, COL.category, COL.status, COL.date], "#/request/")}</div>`);
  }

  // 3. Submit
  function viewSubmit() {
    render(`<div class="page-head"><h1>Submit a Service Request</h1></div>
      <div class="card card-pad"><form class="form" id="submitForm" novalidate>
        <div id="formErr"></div>
        <div class="field"><label for="cat">Category <span class="req">*</span></label>
          <select id="cat" required><option value="">Select a category…</option>${CATEGORIES.map(c => `<option value="${c.id}">${c.name}</option>`).join("")}</select></div>
        <div class="field"><label for="title">Title <span class="req">*</span></label>
          <input type="text" id="title" maxlength="200" placeholder="e.g. Streetlight out on Main Road" required></div>
        <div class="field"><label for="desc">Description <span class="req">*</span></label>
          <textarea id="desc" required placeholder="What's the problem, and since when?"></textarea></div>
        <div class="field"><label for="loc">Location <span class="req">*</span></label>
          <input type="text" id="loc" maxlength="255" placeholder="Street address or landmark" required>
          <span class="hint">Be as specific as you can so staff can find it.</span></div>
        <div class="form-actions"><a href="#/dashboard" class="btn">Cancel</a><button class="btn btn-primary" type="submit" id="submitBtn">Submit</button></div>
      </form></div>`);
    $("#submitForm").onsubmit = async e => {
      e.preventDefault();
      const body = {
        requesterId: session.user.id, categoryId: Number($("#cat").value) || 0,
        title: $("#title").value.trim(), description: $("#desc").value.trim(), location: $("#loc").value.trim()
      };
      const missing = [!body.categoryId && "category", !body.title && "title", !body.description && "description", !body.location && "location"].filter(Boolean);
      if (missing.length) { $("#formErr").innerHTML = `<div class="error-msg">Please fill in the ${missing.join(", ")}.</div>`; return; }
      $("#submitBtn").disabled = true;
      try {
        const id = await api.create(body);
        addHistory(id, "Created", "Request submitted.");
        toast("Request submitted.");
        location.hash = `#/request/${id}`;
      } catch (err) {
        $("#formErr").innerHTML = `<div class="error-msg">${esc(err.message)}</div>`;
        $("#submitBtn").disabled = false;
      }
    };
  }

  // 4 & 6. Request details (requester view, or staff view with assignment controls)
  async function viewDetails(id, staffMode) {
    const r = await api.get(id);
    const backHref = staffMode ? "#/queue" : "#/my-requests", backLbl = staffMode ? "Back to Queue" : "Back to My Requests";
    if (!r) { render(`<a class="back" href="${backHref}">${I.back} ${backLbl}</a><div class="card empty">Request #${id} was not found.</div>`); return; }
    if (!staffMode && session.user.role === "Requester" && r.requesterId !== session.user.id) {
      render(`<a class="back" href="${backHref}">${I.back} ${backLbl}</a><div class="card empty">You can only view your own requests.</div>`); return;
    }
    const ex = extras.of(id);
    const tab = viewDetails.tab || (staffMode ? "details" : "history");

    const details = `<p class="desc">${esc(r.description)}</p>
      ${staffMode ? `
        <div class="assign-grid">
          <div class="field form"><label for="prio">Priority</label>
            <select id="prio">${PRIORITIES.map(p => `<option ${p === ex.priority ? "selected" : ""}>${p}</option>`).join("")}</select></div>
          <div class="field form"><label for="assignee">Assign to</label>
            <select id="assignee"><option value="">Unassigned</option>${STAFF.map(s => `<option value="${s.id}" ${s.id === ex.assignedTo ? "selected" : ""}>${esc(s.name)}</option>`).join("")}</select></div>
        </div>
        <div class="form note-box" style="max-width:none"><div class="field"><label for="status">Status</label>
          <select id="status">${STATUSES.map(s => `<option ${s.name === r.requestStatus ? "selected" : ""}>${s.name}</option>`).join("")}</select></div>
        <div class="field"><label for="notes">Notes <span class="hint">(required when resolving)</span></label>
          <textarea id="notes" style="min-height:80px" placeholder="What was done?"></textarea></div></div>
        <div id="actErr" style="margin-top:12px"></div>
        <div class="actions-row"><button class="btn" id="saveBtn">Update Status</button>
          <button class="btn btn-primary" id="resolveBtn" ${r.requestStatus === "Resolved" ? "disabled" : ""}>Resolve</button></div>` : ""}`;

    const hist = (ex.history || []).slice().reverse();
    const history = hist.length ? `<ul class="timeline">${hist.map(h => `<li><div class="when">${fmtDateTime(h.at)} · ${esc(userName(h.by))}</div>
        <div class="what">${esc(h.action)}</div>${h.note ? `<div class="note">${esc(h.note)}</div>` : ""}</li>`).join("")}</ul>`
      : `<div class="empty">No history recorded yet.</div>`;

    const comments = `<div class="comments">${(ex.comments || []).length ? ex.comments.map(c => `<div class="comment"><div class="avatar">${initials(userName(c.by))}</div>
        <div class="bubble"><div class="who">${esc(userName(c.by))}<span>${fmtDateTime(c.at)}</span></div>${esc(c.text)}</div></div>`).join("")
        : `<div class="empty">No comments yet.</div>`}</div>
      <form class="comment-form" id="commentForm"><textarea id="commentText" placeholder="Add a comment…" required></textarea><button class="btn btn-primary">Post</button></form>`;

    render(`<a class="back" href="${backHref}">${I.back} ${backLbl}</a>
      <div class="page-head" style="border:0;padding-bottom:0;margin-bottom:16px"><h1>Ticket ${esc(r.ticketNumber)}</h1>${statusBadge(r.requestStatus)}</div>
      <div class="card card-pad"><h2 style="font-size:17px">${esc(r.title)}</h2><dl class="meta" style="margin:0">
        <div><dt>Category</dt><dd>${esc(r.categoryName)}</dd></div>
        <div><dt>Status</dt><dd>${esc(r.requestStatus)}</dd></div>
        <div><dt>Location</dt><dd>${esc(r.location)}</dd></div>
        <div><dt>Submitted</dt><dd>${fmtDateTime(r.requestDate)}${staffMode ? ` by ${esc(r.requesterName)}` : ""}</dd></div>
        ${staffMode ? `<div><dt>Priority</dt><dd>${priorityBadge(ex.priority)}</dd></div><div><dt>Assigned to</dt><dd>${ex.assignedTo ? esc(userName(ex.assignedTo)) : "Unassigned"}</dd></div>` : ""}
      </dl></div>
      <div class="tabs" role="tablist">${["details", "history", "comments"].map(t => `<button class="tab ${t === tab ? "active" : ""}" data-tab="${t}" role="tab">${t[0].toUpperCase() + t.slice(1)}</button>`).join("")}</div>
      <div class="card card-pad tab-panel">${{ details, history, comments }[tab]}</div>`);

    document.querySelectorAll(".tab").forEach(b => b.onclick = () => { viewDetails.tab = b.dataset.tab; viewDetails(id, staffMode); });

    const cf = $("#commentForm");
    if (cf) cf.onsubmit = e => {
      e.preventDefault();
      const text = $("#commentText").value.trim(); if (!text) return;
      const ex2 = extras.of(id); ex2.comments = [...(ex2.comments || []), { at: new Date().toISOString(), by: session.user.id, text }];
      extras.save(id, ex2); viewDetails(id, staffMode);
    };

    if (staffMode && $("#saveBtn")) {
      const apply = async resolve => {
        const newStatus = resolve ? "Resolved" : $("#status").value, notes = $("#notes").value.trim();
        const prio = $("#prio").value, assignee = Number($("#assignee").value) || null;
        $("#actErr").innerHTML = "";
        // Save priority / assignment locally (no endpoint yet)
        const ex2 = extras.of(id);
        if (prio !== ex2.priority) { addHistory(id, "Priority changed", `${ex2.priority} → ${prio}`); }
        if (assignee !== ex2.assignedTo) { addHistory(id, "Assigned", assignee ? `Assigned to ${userName(assignee)}.` : "Unassigned."); }
        const ex3 = extras.of(id); ex3.priority = prio; ex3.assignedTo = assignee; extras.save(id, ex3);
        if (newStatus !== r.requestStatus || (resolve && notes)) {
          try {
            await api.updateStatus(id, { newStatus, changedByUserId: session.user.id, expectedVersion: r.version, notes: notes || null });
            addHistory(id, resolve ? "Resolved" : "Status changed", resolve ? notes : `${r.requestStatus} → ${newStatus}${notes ? ` — ${notes}` : ""}`);
          } catch (err) { $("#actErr").innerHTML = `<div class="error-msg">${esc(err.message)}</div>`; return; }
        }
        toast(resolve ? "Request resolved." : "Request updated.");
        viewDetails(id, true);
      };
      $("#saveBtn").onclick = () => apply(false);
      $("#resolveBtn").onclick = () => apply(true);
    }
  }

  // 5. Staff queue (also used for My Assignments)
  async function viewQueue(assignedOnly) {
    const all = (await api.list()).sort((a, b) => new Date(b.requestDate) - new Date(a.requestDate));
    const f = viewQueue.f || (viewQueue.f = { q: "", cat: "", status: "" });
    const rowsFor = () => all.filter(r =>
      (!assignedOnly || r.assignedTo === session.user.id) &&
      (!f.cat || r.categoryId === Number(f.cat)) && (!f.status || r.requestStatus === f.status) &&
      (!f.q || `${r.ticketNumber} ${r.title} ${r.location} ${r.requesterName}`.toLowerCase().includes(f.q.toLowerCase())));
    const cols = [COL.ticket, COL.category, COL.title, COL.status, COL.priority, assignedOnly ? COL.date : COL.assigned, ...(assignedOnly ? [] : [COL.date])];
    render(`<div class="page-head"><h1>${assignedOnly ? "My Assignments" : "Request Queue"}</h1>
        <div class="search">${I.search}<input type="search" id="q" placeholder="Search..." value="${esc(f.q)}"></div></div>
      <div class="filters">
        <select id="fcat"><option value="">All Categories</option>${CATEGORIES.map(c => `<option value="${c.id}" ${String(c.id) === f.cat ? "selected" : ""}>${c.name}</option>`).join("")}</select>
        <select id="fstatus"><option value="">All Statuses</option>${STATUSES.map(s => `<option ${s.name === f.status ? "selected" : ""}>${s.name}</option>`).join("")}</select>
        <button class="btn" id="fbtn">Filter</button>
      </div>
      <div class="card" id="tbl">${requestTable(rowsFor(), cols, "#/manage/")}</div>`);
    const refresh = () => { $("#tbl").innerHTML = requestTable(rowsFor(), cols, "#/manage/"); bindRows($("#tbl")); };
    $("#q").oninput = e => { f.q = e.target.value; refresh(); };
    $("#fbtn").onclick = () => { f.cat = $("#fcat").value; f.status = $("#fstatus").value; refresh(); };
  }

  // 7. Management dashboard
  async function viewManagement() {
    const all = (await api.list()).sort((a, b) => new Date(b.requestDate) - new Date(a.requestDate));
    const by = s => all.filter(r => r.requestStatus === s).length;
    const palette = ["#0f7a6c", "#1f5fbf", "#c27c0e", "#7a4fb5", "#8a97a6"];
    const cats = CATEGORIES.map((c, i) => ({ name: c.name, v: all.filter(r => r.categoryId === c.id).length, color: palette[i] }));
    const total = all.length || 1;
    let acc = 0;
    const arcs = cats.filter(c => c.v).map(c => {
      const len = (c.v / total) * 100, seg = `<circle r="15.915" cx="20" cy="20" fill="none" stroke="${c.color}" stroke-width="7" stroke-dasharray="${len} ${100 - len}" stroke-dashoffset="${25 - acc}"><title>${c.name}: ${c.v}</title></circle>`;
      acc += len; return seg;
    }).join("");
    const statusRows = [["Pending", "var(--pending)"], ["In Progress", "var(--progress)"], ["Resolved", "var(--resolved)"]].map(([n, c]) => ({ n, c, v: by(n) }));
    const overdue = all.filter(isOverdue).length;
    const maxS = Math.max(1, ...statusRows.map(s => s.v), overdue);
    render(`<div class="page-head"><h1>Management Dashboard</h1></div>
      <div class="stats">${stat(all.length, "Total Requests")}${stat(by("Resolved"), "Resolved", "var(--resolved)")}${stat(by("In Progress"), "In Progress", "var(--progress)")}${stat(overdue, `Overdue (&gt;${OVERDUE_DAYS} days)`, "var(--danger)")}</div>
      <div class="grid-2">
        <div class="card card-pad"><h2>Requests by Category</h2><div class="donut-wrap">
          <svg class="donut" viewBox="0 0 40 40" role="img" aria-label="Requests by category"><circle r="15.915" cx="20" cy="20" fill="none" stroke="#eef1f4" stroke-width="7"/>${arcs}
            <text x="20" y="21.5" text-anchor="middle" font-size="7" font-weight="700" fill="var(--ink)">${all.length}</text></svg>
          <ul class="legend">${cats.map(c => `<li><span class="sw" style="background:${c.color}"></span>${c.name}<span class="v">${c.v}</span></li>`).join("")}</ul></div></div>
        <div class="card card-pad"><h2>Requests by Status</h2><div class="bars">
          ${[...statusRows, { n: "Overdue", c: "var(--danger)", v: overdue }].map(s => `<div class="bar-row"><span>${s.n}</span><div class="bar-track"><div class="bar-fill" style="width:${(s.v / maxS) * 100}%;background:${s.c}"></div></div><span class="v">${s.v}</span></div>`).join("")}
        </div></div>
      </div>
      <h2>Recent Requests</h2>
      <div class="card">${requestTable(all.slice(0, 8), [COL.ticket, COL.category, COL.title, COL.status, COL.date], "#/manage/")}</div>`);
  }

  function viewPlaceholder(title, text) {
    render(`<div class="page-head"><h1>${title}</h1></div><div class="card empty">${text}</div>`);
  }

  async function viewUsers() {
    render(`<div class="page-head"><h1>Users</h1></div><div class="card"><div class="table-wrap"><table>
      <thead><tr><th>Name</th><th>Email</th><th>Role</th></tr></thead>
      <tbody>${USERS.map(u => `<tr><td>${esc(u.name)}</td><td class="muted">${esc(u.email || "—")}</td><td>${esc(u.role)}</td></tr>`).join("")}</tbody></table></div></div>
      <p class="muted" style="margin-top:12px">Sample users. These will come from the Users table once a users endpoint is added.</p>`);
  }

  // ---------- Router ----------
  function render(inner) { $("#app").innerHTML = shell(inner); bindShell(); bindRows(); window.scrollTo(0, 0); }

  const ACCESS = {
    "dashboard": ["Requester"], "my-requests": ["Requester"], "submit": ["Requester"], "request": ["Requester", "Staff", "Manager"],
    "queue": ["Staff", "Manager"], "assignments": ["Staff"], "manage": ["Staff", "Manager"], "reports": ["Staff", "Manager"],
    "management": ["Manager"], "analytics": ["Manager"], "users": ["Manager"]
  };

  async function route() {
    const [, page = "", param] = location.hash.split("/");
    if (!session.user || page === "login" || !page) {
      if (session.user && !page) { location.hash = HOME[session.user.role]; return; }
      if (!session.user) return viewLogin();
      return viewLogin();
    }
    if (!ACCESS[page] || !ACCESS[page].includes(session.user.role)) { location.hash = HOME[session.user.role]; return; }
    if (!location.hash.startsWith("#/request/") && !location.hash.startsWith("#/manage/")) viewDetails.tab = null;
    try {
      switch (page) {
        case "dashboard": return await viewRequesterDashboard();
        case "my-requests": return await viewMyRequests();
        case "submit": return viewSubmit();
        case "request": return await viewDetails(Number(param), false);
        case "manage": return await viewDetails(Number(param), true);
        case "queue": return await viewQueue(false);
        case "assignments": return await viewQueue(true);
        case "management": return await viewManagement();
        case "users": return await viewUsers();
        case "reports": return viewPlaceholder("Reports", "Exportable reports will appear here once a reporting endpoint is added.");
        case "analytics": return viewPlaceholder("Analytics", "Trends over time will appear here once request history can be queried from the API.");
      }
    } catch (err) {
      render(`<div class="card empty">${esc(err.message || "Something went wrong.")}</div>`);
    }
  }

  window.addEventListener("hashchange", () => { if (!/^#\/(request|manage)\//.test(location.hash)) viewDetails.tab = null; route(); });
  api.probe().then(() => {
    if (api.mode === "demo") USERS.push(...[{ id: 5, name: "Lerato Dube", role: "Requester" }, { id: 6, name: "Peter Naidoo", role: "Requester" }].filter(u => !USERS.some(x => x.id === u.id)));
    route();
  });
})();

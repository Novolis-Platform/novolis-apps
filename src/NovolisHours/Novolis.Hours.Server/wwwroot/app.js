(() => {
  "use strict";

  let csrfToken = "";
  let currentUser = null;

  const byId = id => document.getElementById(id);
  const value = id => byId(id).value;
  const status = text => { byId("status").textContent = text; };
  const hours = byId("hours");
  const signIn = byId("sign-in");
  const administrator = byId("administrator");
  const escapeHtml = text => String(text).replace(/[&<>"']/g, character => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;"
  })[character]);
  const format = duration => duration || "0:00:00";

  async function api(path, options = {}) {
    const headers = { ...(options.headers || {}) };
    if (options.method && options.method !== "GET") {
      headers["X-Novolis-Hours-CSRF"] = csrfToken;
    }
    if (options.body) {
      headers["Content-Type"] = "application/json";
    }

    const response = await fetch(path, { credentials: "same-origin", ...options, headers });
    if (!response.ok) {
      throw new Error(await response.text() || `${response.status} ${response.statusText}`);
    }

    return response.status === 204 ? null : response.json();
  }

  async function getToken() {
    csrfToken = (await api("/api/auth/antiforgery")).token;
  }

  function renderUsers(users) {
    byId("users").innerHTML = users.length === 0
      ? ""
      : `<h3>Role profiles</h3><table><thead><tr><th>Employee</th><th>Name</th><th>Login</th><th>Role</th><th>Preset</th><th>Fraction</th></tr></thead><tbody>${users.map(user =>
          `<tr><td>${escapeHtml(user.employeeId)}</td><td>${escapeHtml(user.displayName)}</td><td>${escapeHtml(user.login)}</td><td>${escapeHtml(user.role)}</td><td>${escapeHtml(user.legalPresetId || "")}</td><td>${escapeHtml(user.workFraction)}</td></tr>`
        ).join("")}</tbody></table>`;
  }

  async function loadAdministrator() {
    const [users, presets] = await Promise.all([
      api("/api/admin/users"),
      api("/api/legal/preset-catalog")
    ]);
    const selector = byId("settings-legal-preset");
    const selected = selector.value;
    selector.innerHTML = presets.map(preset =>
      `<option value="${escapeHtml(preset.id)}">${escapeHtml(preset.id)} · ${escapeHtml(preset.countryCode)} · ${escapeHtml(preset.reviewState)}</option>`
    ).join("");
    if (selected && presets.some(preset => preset.id === selected)) {
      selector.value = selected;
    }
    renderUsers(users);
  }

  async function refresh() {
    currentUser = await api("/api/auth/me");
    byId("who").textContent = `${currentUser.displayName} (${currentUser.role})`;
    if (currentUser.role === "Employee") {
      byId("employee-id").value = currentUser.employeeId;
    }

    const employee = value("employee-id");
    const view = await api(`/api/employees/${encodeURIComponent(employee)}/view`);
    byId("summary").textContent = `Flex saldo: ${format(view.flexSaldo)} · ${view.entries.length} recorded day(s)`;
    byId("entries").innerHTML = view.entries.map(entry =>
      `<tr><td>${escapeHtml(entry.day)}</td><td>${escapeHtml(entry.startedAt)}–${escapeHtml(entry.endedAt)}</td><td>${escapeHtml(format(entry.expectedDuration))}</td><td>${escapeHtml(format(entry.actualDuration))}</td><td>${escapeHtml(format(entry.flexDelta))}</td><td>${escapeHtml(entry.comment)}</td></tr>`
    ).join("");
    byId("anomalies").innerHTML = view.anomalies.map(item =>
      `<div class="notice"><strong>${escapeHtml(item.code)}</strong>: ${escapeHtml(item.message)}</div>`
    ).join("");

    const isAdministrator = currentUser.role === "Administrator";
    administrator.classList.toggle("hidden", !isAdministrator);
    if (isAdministrator) {
      await loadAdministrator();
    }
  }

  byId("login-form").addEventListener("submit", async event => {
    event.preventDefault();
    try {
      await api("/api/auth/login", {
        method: "POST",
        body: JSON.stringify({ login: value("login"), password: value("password") })
      });
      await getToken();
      signIn.classList.add("hidden");
      hours.classList.remove("hidden");
      await refresh();
      status("Signed in.");
    } catch (error) {
      status(`Sign-in failed: ${error.message}`);
    }
  });

  byId("work-form").addEventListener("submit", async event => {
    event.preventDefault();
    try {
      await api("/api/work", {
        method: "POST",
        body: JSON.stringify({
          employeeId: value("employee-id"),
          day: value("day"),
          startedAt: value("started"),
          endedAt: value("ended"),
          breakStartedAt: value("break-started") || null,
          breakEndedAt: value("break-ended") || null,
          financialCompensationSlices: [],
          comment: value("comment"),
          managerAgreementRecorded: false
        })
      });
      await refresh();
      status("Worktime was appended to the journal.");
    } catch (error) {
      status(`Registration failed: ${error.message}`);
    }
  });

  byId("user-form").addEventListener("submit", async event => {
    event.preventDefault();
    try {
      await api("/api/admin/users", {
        method: "POST",
        body: JSON.stringify({
          employeeId: value("new-employee-id"),
          login: value("new-login"),
          password: value("new-password"),
          displayName: value("new-display-name"),
          role: value("new-role")
        })
      });
      byId("settings-employee-id").value = value("new-employee-id");
      byId("user-form").reset();
      await loadAdministrator();
      status("Role profile created. Configure its worktime settings before recording work.");
    } catch (error) {
      status(`User creation failed: ${error.message}`);
    }
  });

  byId("settings-form").addEventListener("submit", async event => {
    event.preventDefault();
    try {
      const employeeId = value("settings-employee-id");
      await api(`/api/admin/users/${encodeURIComponent(employeeId)}/worktime-settings`, {
        method: "PUT",
        body: JSON.stringify({
          legalPresetId: value("settings-legal-preset"),
          workFraction: Number(value("settings-work-fraction")),
          expectedIntervalOverrideStart: value("settings-start") || null,
          expectedIntervalOverrideEnd: value("settings-end") || null
        })
      });
      await loadAdministrator();
      status("Worktime settings saved.");
    } catch (error) {
      status(`Settings could not be saved: ${error.message}`);
    }
  });

  byId("logout").addEventListener("click", async () => {
    await api("/api/auth/logout", { method: "POST" });
    currentUser = null;
    administrator.classList.add("hidden");
    hours.classList.add("hidden");
    signIn.classList.remove("hidden");
    status("Signed out.");
  });

  byId("employee-id").addEventListener("change", () => {
    refresh().catch(error => status(error.message));
  });
  byId("day").value = new Date().toISOString().slice(0, 10);
  getToken().catch(error => status(`Could not initialize security token: ${error.message}`));
})();

(function () {
  const tokenInput = document.querySelector("#permiso-usuario-token input[name='__RequestVerificationToken']");
  const antiForgeryToken = tokenInput ? tokenInput.value : "";

  const dom = {
    userSelect: document.querySelector("[data-user-select]"),
    filterSearch: document.querySelector("[data-filter-search]"),
    filterModulo: document.querySelector("[data-filter-modulo]"),
    filterEffective: document.querySelector("[data-filter-effective]"),
    filterDirect: document.querySelector("[data-filter-direct]"),
    filterClear: document.querySelector("[data-filter-clear]"),
    body: document.querySelector("[data-permisos-usuario-body]")
  };

  if (!dom.body) {
    return;
  }

  const state = {
    selectedUserId: "",
    users: [],
    permissions: [],
    filters: {
      search: "",
      modulo: "",
      effective: "",
      direct: ""
    }
  };

  function escapeHtml(value) {
    return String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#039;");
  }

  function showToast(message, type) {
    window.AppToast?.show(message, type || "success", { delay: 3200 });
  }

  async function parseResponse(response) {
    const contentType = response.headers.get("content-type") || "";
    if (contentType.includes("application/json")) {
      return await response.json();
    }

    const text = await response.text();
    if (!text) return {};

    try {
      return JSON.parse(text);
    } catch {
      return { message: text };
    }
  }

  async function apiGet(url) {
    const response = await fetch(url, { credentials: "same-origin" });
    const payload = await parseResponse(response);
    if (!response.ok) {
      throw new Error(payload?.message || "No se pudo procesar la solicitud.");
    }
    return payload;
  }

  async function apiPost(url, payload) {
    const response = await fetch(url, {
      method: "POST",
      credentials: "same-origin",
      headers: {
        "Content-Type": "application/json",
        RequestVerificationToken: antiForgeryToken
      },
      body: JSON.stringify(payload)
    });

    const body = await parseResponse(response);
    if (!response.ok) {
      throw new Error(body?.message || "No se pudo procesar la solicitud.");
    }
    return body;
  }

  function setUserOptions() {
    if (!dom.userSelect) return;

    const current = dom.userSelect.value || "";
    const options = [`<option value="">Selecciona usuario</option>`]
      .concat((state.users || []).map((user) => {
        return `<option value="${escapeHtml(user.id)}">${escapeHtml(user.userName)}</option>`;
      }));

    dom.userSelect.innerHTML = options.join("");

    if (current && (state.users || []).some((x) => x.id === current)) {
      dom.userSelect.value = current;
      state.selectedUserId = current;
    }
  }

  function setModuloOptions() {
    if (!dom.filterModulo) return;

    const current = dom.filterModulo.value || "";
    const modules = Array.from(new Set((state.permissions || [])
      .map((item) => (item.modulo || "").trim())
      .filter((value) => value)))
      .sort((a, b) => a.localeCompare(b));

    dom.filterModulo.innerHTML = `<option value="">Todos</option>${modules
      .map((modulo) => `<option value="${escapeHtml(modulo)}">${escapeHtml(modulo)}</option>`)
      .join("")}`;

    dom.filterModulo.value = modules.includes(current) ? current : "";
    state.filters.modulo = dom.filterModulo.value;
  }

  function resolveStatusLabel(value) {
    if (value === "allow") return "Permitido";
    if (value === "deny") return "Denegado";
    return "Sin asignacion";
  }

  function getFilteredPermissions() {
    const search = (state.filters.search || "").trim().toLowerCase();
    const modulo = state.filters.modulo || "";
    const effective = state.filters.effective || "";
    const direct = state.filters.direct || "";

    return (state.permissions || []).filter((item) => {
      if (modulo && item.modulo !== modulo) return false;
      if (effective && item.effectiveAssignment !== effective) return false;
      if (direct && item.directAssignment !== direct) return false;

      if (!search) return true;
      const text = `${item.codigoPermiso || ""} ${item.nombrePermiso || ""}`.toLowerCase();
      return text.includes(search);
    });
  }

  function renderEmpty(message) {
    dom.body.innerHTML = `
      <tr>
        <td colspan="7" class="text-center text-muted py-4">${escapeHtml(message)}</td>
      </tr>
    `;
  }

  function renderPermissions() {
    if (!state.selectedUserId) {
      renderEmpty("Selecciona un usuario para gestionar permisos.");
      return;
    }

    const rows = getFilteredPermissions();
    if (!rows.length) {
      renderEmpty("No hay permisos para los filtros actuales.");
      return;
    }

    dom.body.innerHTML = rows.map((item) => {
      const roleChips = (item.roleNames || []).length
        ? `<div class="permiso-usuario-role-chips">${(item.roleNames || [])
            .map((role) => `<span class="permiso-usuario-role-chip">${escapeHtml(role)}</span>`)
            .join("")}</div>`
        : `<span class="text-muted">-</span>`;

      return `
        <tr class="permiso-usuario-row" data-permiso-id="${escapeHtml(item.permisoAccesoId)}">
          <td><span class="permiso-usuario-code">${escapeHtml(item.codigoPermiso)}</span></td>
          <td>${escapeHtml(item.nombrePermiso)}</td>
          <td>${escapeHtml(item.modulo)}</td>
          <td>${roleChips}</td>
          <td>
            <span class="permiso-usuario-status" data-state="${escapeHtml(item.directAssignment)}">
              ${resolveStatusLabel(item.directAssignment)}
            </span>
          </td>
          <td>
            <span class="permiso-usuario-status" data-state="${escapeHtml(item.effectiveAssignment)}">
              ${resolveStatusLabel(item.effectiveAssignment)}
            </span>
          </td>
          <td class="text-end">
            <div class="permiso-usuario-direct-wrap justify-content-end">
              <select class="form-select form-select-sm permiso-usuario-direct-input" data-direct-select>
                <option value="none" ${item.directAssignment === "none" ? "selected" : ""}>Sin asignacion</option>
                <option value="allow" ${item.directAssignment === "allow" ? "selected" : ""}>Permitido</option>
                <option value="deny" ${item.directAssignment === "deny" ? "selected" : ""}>Denegado</option>
              </select>
              <button type="button" class="btn btn-sm btn-primary" data-direct-apply>Aplicar</button>
            </div>
          </td>
        </tr>
      `;
    }).join("");
  }

  async function loadUsers() {
    const users = await apiGet("/PermisoAcceso/UserCatalog");
    state.users = Array.isArray(users) ? users : [];
    setUserOptions();
  }

  async function loadUserPermissions() {
    if (!state.selectedUserId) {
      state.permissions = [];
      setModuloOptions();
      renderPermissions();
      return;
    }

    const list = await apiGet(`/PermisoAcceso/UserPermissions?userId=${encodeURIComponent(state.selectedUserId)}`);
    state.permissions = Array.isArray(list) ? list : [];
    setModuloOptions();
    renderPermissions();
  }

  async function applyDirectAssignment(permisoId, mode) {
    if (!state.selectedUserId || !permisoId) return;

    if (mode === "none") {
      await apiPost(`/PermisoAcceso/UnassignUser/${encodeURIComponent(permisoId)}`, {
        userId: state.selectedUserId
      });
      showToast("Asignacion directa removida.", "success");
      return;
    }

    await apiPost(`/PermisoAcceso/AssignUser/${encodeURIComponent(permisoId)}`, {
      userId: state.selectedUserId,
      esDenegado: mode === "deny"
    });
    showToast("Asignacion directa actualizada.", "success");
  }

  function syncFilters() {
    state.filters.search = dom.filterSearch?.value || "";
    state.filters.modulo = dom.filterModulo?.value || "";
    state.filters.effective = dom.filterEffective?.value || "";
    state.filters.direct = dom.filterDirect?.value || "";
    renderPermissions();
  }

  if (dom.userSelect) {
    dom.userSelect.addEventListener("change", async () => {
      state.selectedUserId = dom.userSelect.value || "";
      try {
        await loadUserPermissions();
      } catch (error) {
        showToast(error.message || "No se pudieron cargar permisos del usuario.", "danger");
        renderEmpty("No se pudieron cargar permisos del usuario.");
      }
    });
  }

  if (dom.filterSearch) dom.filterSearch.addEventListener("input", syncFilters);
  if (dom.filterModulo) dom.filterModulo.addEventListener("change", syncFilters);
  if (dom.filterEffective) dom.filterEffective.addEventListener("change", syncFilters);
  if (dom.filterDirect) dom.filterDirect.addEventListener("change", syncFilters);

  if (dom.filterClear) {
    dom.filterClear.addEventListener("click", () => {
      if (dom.filterSearch) dom.filterSearch.value = "";
      if (dom.filterModulo) dom.filterModulo.value = "";
      if (dom.filterEffective) dom.filterEffective.value = "";
      if (dom.filterDirect) dom.filterDirect.value = "";
      syncFilters();
    });
  }

  dom.body.addEventListener("click", async (event) => {
    const target = event.target instanceof Element ? event.target : null;
    if (!target) return;

    const applyBtn = target.closest("[data-direct-apply]");
    if (!applyBtn) return;

    const row = applyBtn.closest("tr[data-permiso-id]");
    if (!row) return;

    const permisoId = row.getAttribute("data-permiso-id") || "";
    const select = row.querySelector("[data-direct-select]");
    const mode = select instanceof HTMLSelectElement ? select.value : "none";

    try {
      await applyDirectAssignment(permisoId, mode);
      await loadUserPermissions();
    } catch (error) {
      showToast(error.message || "No se pudo actualizar la asignacion directa.", "danger");
    }
  });

  (async function init() {
    try {
      await loadUsers();
      renderPermissions();
    } catch (error) {
      showToast(error.message || "No se pudieron cargar usuarios.", "danger");
      renderEmpty("No se pudieron cargar usuarios.");
    }
  })();
})();

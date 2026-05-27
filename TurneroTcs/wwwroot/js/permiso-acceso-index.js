(function () {
  const tokenInput = document.querySelector("#permiso-acceso-token input[name='__RequestVerificationToken']");
  const antiForgeryToken = tokenInput ? tokenInput.value : "";

  const dom = {
    body: document.querySelector("[data-permisos-body]"),
    filterSearch: document.querySelector("[data-filter-search]"),
    filterModulo: document.querySelector("[data-filter-modulo]"),
    pageSize: document.querySelector("[data-page-size]"),
    filterClear: document.querySelector("[data-filter-clear]"),
    footer: document.querySelector("[data-permisos-footer]"),
    footerSummary: document.querySelector("[data-permisos-summary]"),
    footerPageMeta: document.querySelector("[data-permisos-page-meta]"),
    pagination: document.querySelector("[data-permisos-pagination]")
  };

  if (!dom.body) {
    return;
  }

  const state = {
    permisos: [],
    filters: {
      search: "",
      modulo: ""
    },
    editor: {
      permisoId: null,
      rowElement: null,
      triggerRow: null,
      roles: []
    },
    pagination: {
      page: 1,
      pageSize: 15
    }
  };

  const ALLOWED_PAGE_SIZES = [15, 25, 50, 100];

  if (dom.pageSize) {
    const initialPageSize = Number.parseInt(dom.pageSize.value || "", 10);
    if (ALLOWED_PAGE_SIZES.includes(initialPageSize)) {
      state.pagination.pageSize = initialPageSize;
    } else {
      dom.pageSize.value = String(state.pagination.pageSize);
    }
  }

  function escapeHtml(value) {
    return String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#039;");
  }

  function showToast(message, type) {
    window.AppToast?.show(message, type || "success", { delay: 3000 });
  }

  function buildRequestOptions(method, payload) {
    const headers = {
      RequestVerificationToken: antiForgeryToken
    };

    if (payload !== undefined) {
      headers["Content-Type"] = "application/json";
    }

    const options = {
      method,
      headers,
      credentials: "same-origin"
    };

    if (payload !== undefined) {
      options.body = JSON.stringify(payload);
    }

    return options;
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
    const response = await fetch(url, buildRequestOptions("POST", payload));
    const body = await parseResponse(response);
    if (!response.ok) {
      throw new Error(body?.message || "No se pudo procesar la solicitud.");
    }
    return body;
  }

  function getPermisoById(permisoId) {
    return state.permisos.find((item) => item.permisoAccesoId === permisoId) || null;
  }

  function setModuloOptions() {
    if (!dom.filterModulo) return;
    const current = dom.filterModulo.value || "";
    const modules = Array.from(new Set((state.permisos || [])
      .map((item) => (item.modulo || "").trim())
      .filter((x) => x)))
      .sort((a, b) => a.localeCompare(b));

    dom.filterModulo.innerHTML = `<option value="">Todos</option>${modules
      .map((m) => `<option value="${escapeHtml(m)}">${escapeHtml(m)}</option>`)
      .join("")}`;

    dom.filterModulo.value = modules.includes(current) ? current : "";
    state.filters.modulo = dom.filterModulo.value;
  }

  function getFilteredPermisos() {
    const search = (state.filters.search || "").trim().toLowerCase();
    const modulo = state.filters.modulo || "";

    return (state.permisos || []).filter((item) => {
      if (modulo && item.modulo !== modulo) return false;

      if (!search) return true;
      const text = `${item.codigoPermiso || ""} ${item.nombrePermiso || ""} ${item.descripcion || ""}`.toLowerCase();
      return text.includes(search);
    });
  }

  function getVisiblePageNumbers(currentPage, totalPages) {
    const pages = [];
    for (let page = 1; page <= totalPages; page += 1) {
      if (page === 1 || page === totalPages || Math.abs(page - currentPage) <= 1) {
        pages.push(page);
      }
    }
    return pages;
  }

  function getPaginatedPermisos() {
    const filtered = getFilteredPermisos();
    const pageSize = ALLOWED_PAGE_SIZES.includes(state.pagination.pageSize)
      ? state.pagination.pageSize
      : 15;
    const totalPages = filtered.length ? Math.ceil(filtered.length / pageSize) : 0;
    const currentPage = totalPages ? Math.min(Math.max(state.pagination.page, 1), totalPages) : 1;
    const startIndex = filtered.length ? (currentPage - 1) * pageSize : 0;
    const rows = filtered.slice(startIndex, startIndex + pageSize);

    state.pagination.page = currentPage;
    state.pagination.pageSize = pageSize;

    if (dom.pageSize && dom.pageSize.value !== String(pageSize)) {
      dom.pageSize.value = String(pageSize);
    }

    return {
      filtered,
      rows,
      currentPage,
      pageSize,
      totalPages,
      startIndex
    };
  }

  function renderPagination(totalCount, currentPage, totalPages, pageSize, startIndex, visibleCount) {
    if (!dom.footer || !dom.pagination || !dom.footerSummary || !dom.footerPageMeta) {
      return;
    }

    if (!totalCount) {
      dom.footer.hidden = true;
      dom.pagination.innerHTML = "";
      dom.footerSummary.textContent = "";
      dom.footerPageMeta.textContent = "";
      return;
    }

    const startItem = startIndex + 1;
    const endItem = startIndex + visibleCount;
    dom.footer.hidden = false;
    dom.footerSummary.textContent = `Mostrando ${startItem}-${endItem} de ${totalCount} permisos`;
    dom.footerPageMeta.textContent = `Pagina ${currentPage} de ${totalPages}`;

    if (totalPages <= 1) {
      dom.pagination.innerHTML = "";
      return;
    }

    const pageNumbers = getVisiblePageNumbers(currentPage, totalPages);
    let previousVisible = null;
    const pageLinks = pageNumbers.map((page) => {
      const gap = previousVisible && page - previousVisible > 1
        ? `<span class="app-data-page-gap" aria-hidden="true">...</span>`
        : "";
      previousVisible = page;
      return `${gap}
        <button type="button"
                class="app-data-page-link ${page === currentPage ? "is-active" : ""}"
                data-page="${page}"
                ${page === currentPage ? 'aria-current="page"' : ""}>
          ${page}
        </button>`;
    }).join("");

    dom.pagination.innerHTML = `
      <button type="button"
              class="app-data-page-nav app-data-page-nav-edge ${currentPage === 1 ? "is-disabled" : ""}"
              data-page="1"
              ${currentPage === 1 ? "disabled" : ""}>
        <span>Primera</span>
      </button>
      <button type="button"
              class="app-data-page-nav ${currentPage === 1 ? "is-disabled" : ""}"
              data-page="${Math.max(1, currentPage - 1)}"
              ${currentPage === 1 ? "disabled" : ""}>
        <span class="app-data-page-nav-icon" aria-hidden="true">&lsaquo;</span>
        <span>Anterior</span>
      </button>
      <div class="app-data-page-links">${pageLinks}</div>
      <button type="button"
              class="app-data-page-nav ${currentPage === totalPages ? "is-disabled" : ""}"
              data-page="${Math.min(totalPages, currentPage + 1)}"
              ${currentPage === totalPages ? "disabled" : ""}>
        <span>Siguiente</span>
        <span class="app-data-page-nav-icon" aria-hidden="true">&rsaquo;</span>
      </button>
      <button type="button"
              class="app-data-page-nav app-data-page-nav-edge ${currentPage === totalPages ? "is-disabled" : ""}"
              data-page="${totalPages}"
              ${currentPage === totalPages ? "disabled" : ""}>
        <span>Ultima</span>
      </button>
    `;
  }

  function renderPermisos() {
    closeEditor();
    const { filtered, rows, currentPage, pageSize, totalPages, startIndex } = getPaginatedPermisos();
    if (!rows.length) {
      dom.body.innerHTML = `
        <tr>
          <td colspan="4" class="text-center text-muted py-4">
            No hay permisos para los filtros actuales.
          </td>
        </tr>
      `;
      renderPagination(0, 1, 0, pageSize, 0, 0);
      return;
    }

    dom.body.innerHTML = rows.map((item) => {
      const rolesHtml = (item.roleNames || [])
        .slice(0, 3)
        .map((name) => `<span class="permiso-chip">${escapeHtml(name)}</span>`)
        .join("");
      const roleMore = (item.roleNames || []).length > 3
        ? `<span class="permiso-chip">+${(item.roleNames || []).length - 3}</span>`
        : "";
      const hasRoles = (item.roleNames || []).length > 0;

      return `
        <tr class="permiso-row app-data-row" data-permiso-id="${escapeHtml(item.permisoAccesoId)}">
          <td>
            <div class="permiso-code-wrap">
              <button type="button"
                      class="btn btn-outline-secondary btn-sm permiso-expand-btn"
                      data-edit-permiso="${escapeHtml(item.permisoAccesoId)}"
                      aria-label="Expandir fila"
                      aria-expanded="false"
                      aria-controls="permiso-edit-${escapeHtml(item.permisoAccesoId)}">
                <svg aria-hidden="true" viewBox="0 0 24 24" width="14" height="14" fill="currentColor">
                  <path d="M7.4 8.6 12 13.2l4.6-4.6 1.4 1.4-6 6-6-6z"></path>
                </svg>
              </button>
              <span class="permiso-code">${escapeHtml(item.codigoPermiso)}</span>
            </div>
          </td>
          <td>${escapeHtml(item.nombrePermiso)}</td>
          <td>${escapeHtml(item.modulo)}</td>
          <td><div class="permiso-chips">${hasRoles ? `${rolesHtml}${roleMore}` : '<span class="text-muted">-</span>'}</div></td>
        </tr>
      `;
    }).join("");

    renderPagination(filtered.length, currentPage, totalPages, pageSize, startIndex, rows.length);
  }

  function setRowEditingState(row, isOpen) {
    if (!row) return;
    row.classList.toggle("is-editing", isOpen);
    const toggleButton = row.querySelector("[data-edit-permiso]");
    if (!toggleButton) return;
    toggleButton.setAttribute("aria-expanded", isOpen ? "true" : "false");
    toggleButton.classList.toggle("is-open", isOpen);
  }

  function closeEditor() {
    if (state.editor.triggerRow) {
      setRowEditingState(state.editor.triggerRow, false);
    }
    if (state.editor.rowElement && state.editor.rowElement.parentNode) {
      state.editor.rowElement.parentNode.removeChild(state.editor.rowElement);
    }
    state.editor.permisoId = null;
    state.editor.rowElement = null;
    state.editor.triggerRow = null;
    state.editor.roles = [];
  }

  function renderEditor(permission) {
    if (!state.editor.rowElement || !permission) return;

    const rolesHtml = (state.editor.roles || []).map((role) => `
      <label class="permiso-role-pill">
        <input type="checkbox" data-role-toggle data-role-id="${escapeHtml(role.id)}" ${role.assigned ? "checked" : ""} />
        <span>${escapeHtml(role.name || role.id)}</span>
      </label>
    `).join("");

    state.editor.rowElement.innerHTML = `
      <td colspan="4">
        <div class="permiso-edit-panel">
          <div class="permiso-edit-top">
            <div>
              <label class="form-label permiso-label">Codigo</label>
              <input type="text" class="form-control permiso-edit-input" data-editor-codigo value="${escapeHtml(permission.codigoPermiso)}" />
            </div>
            <div>
              <label class="form-label permiso-label">Nombre</label>
              <input type="text" class="form-control permiso-edit-input" data-editor-nombre value="${escapeHtml(permission.nombrePermiso)}" />
            </div>
            <div>
              <label class="form-label permiso-label">Modulo</label>
              <input type="text" class="form-control permiso-edit-input" data-editor-modulo value="${escapeHtml(permission.modulo)}" />
            </div>
            <div>
              <label class="form-label permiso-label">Descripcion</label>
              <input type="text" class="form-control permiso-edit-input" data-editor-descripcion value="${escapeHtml(permission.descripcion || "")}" />
            </div>
          </div>
          <div class="permiso-assign-grid">
            <section class="permiso-assign-card">
              <h6 class="permiso-assign-title">Asignacion por rol</h6>
              <div class="permiso-role-list">${rolesHtml || '<span class="text-muted small">Sin roles.</span>'}</div>
            </section>
          </div>
          <div class="permiso-edit-actions">
            <button type="button" class="btn btn-outline-secondary btn-sm" data-editor-cancel>Cerrar</button>
            <button type="button" class="btn btn-primary btn-sm" data-editor-save>Guardar cambios</button>
          </div>
          <div class="permiso-status" data-editor-status></div>
        </div>
      </td>
    `;
  }

  async function loadPermisos() {
    const list = await apiGet("/PermisoAcceso/List");
    state.permisos = Array.isArray(list) ? list : [];
    setModuloOptions();
    renderPermisos();
  }

  async function refreshAndKeepEditor(permisoId) {
    await loadPermisos();
    if (permisoId) {
      await openEditor(permisoId);
    }
  }

  function setEditorStatus(message, isError) {
    if (!state.editor.rowElement) return;
    const target = state.editor.rowElement.querySelector("[data-editor-status]");
    if (!target) return;
    target.textContent = message || "";
    target.classList.remove("ok", "error");
    if (message) {
      target.classList.add(isError ? "error" : "ok");
    }
  }

  async function openEditor(permisoId) {
    if (!permisoId) return;

    if (state.editor.permisoId === permisoId) {
      closeEditor();
      return;
    }

    closeEditor();

    const safeId = String(permisoId).replaceAll('"', '\\"');
    const row = dom.body.querySelector(`tr[data-permiso-id="${safeId}"]`);
    const permission = getPermisoById(permisoId);
    if (!row || !permission) return;

    setRowEditingState(row, true);

    const editorRow = document.createElement("tr");
    editorRow.id = `permiso-edit-${permisoId}`;
    editorRow.className = "permiso-edit-row";
    editorRow.innerHTML = `<td colspan="4" class="text-center text-muted py-3">Cargando detalle...</td>`;
    row.insertAdjacentElement("afterend", editorRow);

    state.editor.permisoId = permisoId;
    state.editor.rowElement = editorRow;
    state.editor.triggerRow = row;

    try {
      const roles = await apiGet(`/PermisoAcceso/Roles?id=${encodeURIComponent(permisoId)}`);

      if (state.editor.permisoId !== permisoId) {
        return;
      }

      state.editor.roles = Array.isArray(roles) ? roles : [];
      renderEditor(permission);
    } catch (error) {
      editorRow.innerHTML = `
        <td colspan="4" class="text-center text-danger py-3">
          ${escapeHtml(error.message || "No se pudo cargar el detalle.")}
        </td>
      `;
    }
  }

  async function onSaveEditor() {
    const permisoId = state.editor.permisoId;
    if (!permisoId || !state.editor.rowElement) return;

    const codigoInput = state.editor.rowElement.querySelector("[data-editor-codigo]");
    const nombreInput = state.editor.rowElement.querySelector("[data-editor-nombre]");
    const moduloInput = state.editor.rowElement.querySelector("[data-editor-modulo]");
    const descInput = state.editor.rowElement.querySelector("[data-editor-descripcion]");

    try {
      await apiPost(`/PermisoAcceso/Patch/${encodeURIComponent(permisoId)}`, {
        codigoPermiso: codigoInput?.value?.trim() ?? null,
        nombrePermiso: nombreInput?.value?.trim() ?? null,
        modulo: moduloInput?.value?.trim() ?? null,
        descripcion: descInput?.value ?? null
      });
      showToast("Permiso actualizado correctamente.", "success");
      await refreshAndKeepEditor(permisoId);
      setEditorStatus("Cambios guardados.", false);
    } catch (error) {
      setEditorStatus(error.message || "No se pudo actualizar el permiso.", true);
      showToast(error.message || "No se pudo actualizar el permiso.", "danger");
    }
  }

  async function onToggleRole(roleId, checked) {
    const permisoId = state.editor.permisoId;
    if (!permisoId || !roleId) return;

    const endpoint = checked ? "AssignRole" : "UnassignRole";
    try {
      await apiPost(`/PermisoAcceso/${endpoint}/${encodeURIComponent(permisoId)}`, {
        roleId
      });
      showToast(`Rol ${checked ? "asignado" : "retirado"} correctamente.`, "success");
      await refreshAndKeepEditor(permisoId);
    } catch (error) {
      showToast(error.message || "No se pudo actualizar el rol.", "danger");
      await refreshAndKeepEditor(permisoId);
    }
  }

  dom.body.addEventListener("click", async (event) => {
    const target = event.target instanceof Element ? event.target : null;
    if (!target) return;

    const editBtn = target.closest("[data-edit-permiso]");
    if (editBtn) {
      await openEditor(editBtn.getAttribute("data-edit-permiso"));
      return;
    }

    if (!state.editor.rowElement || !state.editor.rowElement.contains(target)) {
      return;
    }

    if (target.closest("[data-editor-cancel]")) {
      closeEditor();
      return;
    }

    if (target.closest("[data-editor-save]")) {
      await onSaveEditor();
      return;
    }
  });

  dom.body.addEventListener("change", async (event) => {
    const target = event.target instanceof Element ? event.target : null;
    if (!target || !state.editor.rowElement || !state.editor.rowElement.contains(target)) {
      return;
    }

    const roleToggle = target.closest("[data-role-toggle]");
    if (roleToggle instanceof HTMLInputElement) {
      const roleId = roleToggle.getAttribute("data-role-id") || "";
      await onToggleRole(roleId, roleToggle.checked);
    }
  });

  const syncFilters = () => {
    state.filters.search = dom.filterSearch?.value || "";
    state.filters.modulo = dom.filterModulo?.value || "";
    state.pagination.page = 1;
    renderPermisos();
  };

  if (dom.filterSearch) {
    dom.filterSearch.addEventListener("input", syncFilters);
  }

  if (dom.filterModulo) {
    dom.filterModulo.addEventListener("change", syncFilters);
  }

  if (dom.filterClear) {
    dom.filterClear.addEventListener("click", () => {
      if (dom.filterSearch) dom.filterSearch.value = "";
      if (dom.filterModulo) dom.filterModulo.value = "";
      syncFilters();
    });
  }

  if (dom.pageSize) {
    dom.pageSize.addEventListener("change", () => {
      const selected = Number.parseInt(dom.pageSize.value || "", 10);
      state.pagination.pageSize = ALLOWED_PAGE_SIZES.includes(selected) ? selected : 15;
      state.pagination.page = 1;
      renderPermisos();
    });
  }

  if (dom.pagination) {
    dom.pagination.addEventListener("click", (event) => {
      const target = event.target instanceof Element ? event.target.closest("[data-page]") : null;
      if (!(target instanceof HTMLButtonElement) || target.disabled) {
        return;
      }

      const page = Number.parseInt(target.getAttribute("data-page") || "", 10);
      if (!Number.isInteger(page) || page < 1 || page === state.pagination.page) {
        return;
      }

      state.pagination.page = page;
      renderPermisos();
    });
  }

  loadPermisos().catch((error) => {
    showToast(error.message || "No se pudieron cargar los permisos.", "danger");
    if (dom.footer) {
      dom.footer.hidden = true;
    }
    dom.body.innerHTML = `
      <tr>
        <td colspan="4" class="text-center text-danger py-4">
          No se pudieron cargar los permisos.
        </td>
      </tr>
    `;
  });
})();

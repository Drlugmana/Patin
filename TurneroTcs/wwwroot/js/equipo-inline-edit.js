const equipoTable = document.querySelector(".equipo-table");
const equipoToken = document.querySelector("#equipo-inline-token input[name='__RequestVerificationToken']");
const equipoRequestToken = equipoToken?.value || "";
const equipoDeleteModal = document.querySelector("#equipo-delete-modal");
const equipoDeleteForm = document.querySelector("#equipo-delete-form");
const equipoDeleteName = document.querySelector("[data-equipo-delete-name]");

if (equipoDeleteModal && equipoDeleteForm) {
  equipoDeleteModal.addEventListener("show.bs.modal", (event) => {
    const trigger = event.relatedTarget;
    if (!(trigger instanceof HTMLElement)) return;
    const equipoId = trigger.getAttribute("data-equipo-id");
    const equipoNombre = trigger.getAttribute("data-equipo-nombre");
    const idInput = equipoDeleteForm.querySelector('input[name="id"]');
    if (idInput) {
      idInput.value = equipoId || "";
    }
    if (equipoDeleteName) {
      equipoDeleteName.textContent = equipoNombre || "este equipo";
    }
  });
}

const setEquipoStatus = (row, message) => {
  const status = row.querySelector(".equipo-inline-status");
  if (!status) return;
  status.textContent = message;
  status.classList.add("is-visible");
  setTimeout(() => status.classList.remove("is-visible"), 1800);
};

const showEquipoToast = (message, variant = "success") => {
  window.AppToast?.show(message, variant);
};

const getEquipoEditRow = (row) => {
  const id = row.dataset.equipoId;
  if (!id) return null;
  return equipoTable?.querySelector(`[data-equipo-edit-for="${id}"]`);
};

const getSelectedTurnoIds = (editRow) => {
  if (!editRow) return [];
  return Array.from(editRow.querySelectorAll('input[name="tipoTurnoIds"]:checked'))
    .map((input) => input.value)
    .filter((value) => value && value.trim().length > 0)
    .sort();
};

const serializeTurnoIds = (ids) => ids.join(",");

const captureTurnosSnapshot = (row) => {
  const editRow = getEquipoEditRow(row);
  if (!editRow) return;
  editRow.dataset.turnosSnapshot = serializeTurnoIds(getSelectedTurnoIds(editRow));
};

const resetTurnosInputs = (row) => {
  const editRow = getEquipoEditRow(row);
  if (!editRow) return;
  const snapshot = (editRow.dataset.turnosSnapshot || "")
    .split(",")
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
  const selected = new Set(snapshot);
  editRow.querySelectorAll('input[name="tipoTurnoIds"]').forEach((input) => {
    input.checked = selected.has(input.value);
  });
};

const hasTurnosChanges = (row) => {
  const editRow = getEquipoEditRow(row);
  if (!editRow) return false;
  const checkboxes = editRow.querySelectorAll('input[name="tipoTurnoIds"]');
  if (checkboxes.length === 0) return false;
  const current = serializeTurnoIds(getSelectedTurnoIds(editRow));
  const original = editRow.dataset.turnosSnapshot || "";
  return current !== original;
};

const resetEquipoInputs = (row) => {
  const editRow = getEquipoEditRow(row);
  if (!editRow) return;
  editRow.querySelector('[data-field="NombreEquipo"]').value = row.dataset.nombre || "";
  const activeInput = editRow.querySelector('[data-field="Activo"]');
  if (activeInput) {
    const isActive = String(row.dataset.activo).toLowerCase() === "true";
    activeInput.checked = isActive;
    const label = activeInput.closest(".equipo-switch")?.querySelector("span");
    if (label) {
      label.textContent = isActive ? "Activo" : "Inactivo";
    }
  }
  const tipoGeneracionSelect = editRow.querySelector('[data-field="TipoGeneracion"]');
  if (tipoGeneracionSelect) {
    tipoGeneracionSelect.value = row.dataset.tipoGeneracion || "Rotacion";
  }
};

const updateEquipoToggleState = (row, isOpen) => {
  const toggleButton = row.querySelector(".equipo-inline-edit");
  if (!toggleButton) return;
  toggleButton.setAttribute("aria-expanded", isOpen ? "true" : "false");
  toggleButton.classList.toggle("is-open", isOpen);
};

const enterEquipoEdit = (row) => {
  captureTurnosSnapshot(row);
  resetEquipoInputs(row);
  const editRow = getEquipoEditRow(row);
  row.classList.add("is-editing");
  if (editRow) {
    editRow.classList.add("is-open");
  }
  updateEquipoToggleState(row, true);
};

const exitEquipoEdit = (row) => {
  row.classList.remove("is-editing");
  const editRow = getEquipoEditRow(row);
  if (editRow) {
    editRow.classList.remove("is-open");
  }
  updateEquipoToggleState(row, false);
};

const buildEquipoChanges = (row) => {
  const editRow = getEquipoEditRow(row);
  if (!editRow) {
    return { changes: {}, inputs: {} };
  }
  const original = {
    NombreEquipo: (row.dataset.nombre || "").trim(),
    Activo: String(row.dataset.activo).toLowerCase() === "true",
    TipoGeneracion: (row.dataset.tipoGeneracion || "Rotacion").trim()
  };

  const inputs = {
    NombreEquipo: editRow.querySelector('[data-field="NombreEquipo"]').value.trim(),
    Activo: editRow.querySelector('[data-field="Activo"]').checked,
    TipoGeneracion: editRow.querySelector('[data-field="TipoGeneracion"]').value.trim()
  };

  const changes = {};
  if (inputs.NombreEquipo !== original.NombreEquipo) {
    changes.NombreEquipo = inputs.NombreEquipo;
  }
  if (inputs.Activo !== original.Activo) {
    changes.Activo = inputs.Activo;
  }
  if (inputs.TipoGeneracion !== original.TipoGeneracion) {
    changes.TipoGeneracion = inputs.TipoGeneracion;
  }

  return { changes, inputs };
};

const updateEquipoBadge = (row, isActive) => {
  const badge = row.querySelector('[data-field-display="Activo"]');
  if (!badge) return;
  badge.textContent = isActive ? "Activo" : "Inactivo";
  badge.classList.toggle("bg-success-subtle", isActive);
  badge.classList.toggle("text-success", isActive);
  badge.classList.toggle("bg-secondary-subtle", !isActive);
  badge.classList.toggle("text-secondary", !isActive);
};

const saveEquipoRow = async (row) => {
  const id = row.dataset.equipoId;
  const { changes, inputs } = buildEquipoChanges(row);
  const changeKeys = Object.keys(changes);
  const turnosChanged = hasTurnosChanges(row);

  if (changeKeys.length === 0 && !turnosChanged) {
    exitEquipoEdit(row);
    setEquipoStatus(row, "Sin cambios");
    return;
  }

  const formData = new URLSearchParams();
  formData.append("id", id);
  changeKeys.forEach((key) => {
    formData.append(key, key === "Activo" ? String(changes[key]) : changes[key]);
  });
  if (equipoRequestToken) {
    formData.append("__RequestVerificationToken", equipoRequestToken);
  }

  try {
    if (changeKeys.length > 0) {
      const response = await fetch(`/Equipo/Patch?id=${encodeURIComponent(id)}`, {
        method: "POST",
        body: formData,
        credentials: "same-origin",
        headers: {
          "X-Requested-With": "XMLHttpRequest",
          "RequestVerificationToken": equipoRequestToken,
          "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"
        }
      });

      if (!response.ok) {
        throw new Error("Request failed");
      }

      row.dataset.nombre = inputs.NombreEquipo;
      row.dataset.activo = String(inputs.Activo);
      row.dataset.tipoGeneracion = inputs.TipoGeneracion;

      const nombreDisplay = row.querySelector('[data-field-display="NombreEquipo"]');
      if (nombreDisplay) {
        nombreDisplay.textContent = inputs.NombreEquipo;
      }
      updateEquipoBadge(row, inputs.Activo);

      const activeLabel = getEquipoEditRow(row)?.querySelector(".equipo-switch span");
      if (activeLabel) {
        activeLabel.textContent = inputs.Activo ? "Activo" : "Inactivo";
      }
    }

    if (turnosChanged) {
      const editRow = getEquipoEditRow(row);
      const selectedTurnos = getSelectedTurnoIds(editRow);
      const turnosFormData = new URLSearchParams();
      turnosFormData.append("equipoId", id);
      selectedTurnos.forEach((tipoTurnoId) => {
        turnosFormData.append("tipoTurnoIds", tipoTurnoId);
      });
      if (equipoRequestToken) {
        turnosFormData.append("__RequestVerificationToken", equipoRequestToken);
      }

      const turnosResponse = await fetch("/Equipo/UpdateTipoTurnos", {
        method: "POST",
        body: turnosFormData,
        credentials: "same-origin",
        headers: {
          "X-Requested-With": "XMLHttpRequest",
          "RequestVerificationToken": equipoRequestToken,
          "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"
        }
      });

      const turnosData = await turnosResponse.json().catch(() => ({}));
      if (!turnosResponse.ok || turnosData.success === false) {
        throw new Error(turnosData.error || "No se pudieron actualizar los turnos.");
      }

      captureTurnosSnapshot(row);
    }

    exitEquipoEdit(row);
    showEquipoToast("Cambios guardados.", "success");
  } catch {
    setEquipoStatus(row, "Error");
  }
};

if (equipoTable) {
  equipoTable.addEventListener("click", (event) => {
    const target = event.target instanceof Element ? event.target : null;
    if (!target) return;
    let row = target.closest(".equipo-row");
    if (!row) {
      const editRow = target.closest(".equipo-edit-row");
      const equipoId = editRow?.getAttribute("data-equipo-edit-for");
      if (equipoId) {
        row = equipoTable.querySelector(`[data-equipo-id="${equipoId}"]`);
      }
    }
    if (!row) return;

    if (target.closest(".equipo-inline-edit")) {
      event.preventDefault();
      if (row.classList.contains("is-editing")) {
        resetEquipoInputs(row);
        resetTurnosInputs(row);
        exitEquipoEdit(row);
        return;
      }
      enterEquipoEdit(row);
      return;
    }

    if (target.closest(".equipo-inline-cancel")) {
      const { changes } = buildEquipoChanges(row);
      const turnosChanged = hasTurnosChanges(row);
      resetEquipoInputs(row);
      resetTurnosInputs(row);
      exitEquipoEdit(row);
      if (Object.keys(changes).length > 0 || turnosChanged) {
        showEquipoToast("Cambios descartados.", "info");
      }
      return;
    }

    if (target.closest(".equipo-inline-save")) {
      saveEquipoRow(row);
    }
  });

  equipoTable.addEventListener("change", (event) => {
    const target = event.target instanceof HTMLElement ? event.target : null;
    if (!target) return;
    if (target.dataset.field === "Activo" && target instanceof HTMLInputElement) {
      const label = target.closest(".equipo-switch")?.querySelector("span");
      if (label) {
        label.textContent = target.checked ? "Activo" : "Inactivo";
      }
    }
  });
}

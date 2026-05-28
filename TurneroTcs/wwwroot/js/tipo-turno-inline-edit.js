const turnoTable = document.querySelector(".turno-table");
const turnoToken = document.querySelector("#turno-inline-token input[name='__RequestVerificationToken']");
const turnoRequestToken = turnoToken?.value || "";
const turnoDeleteModal = document.querySelector("#turno-delete-modal");
const turnoDeleteForm = document.querySelector("#turno-delete-form");
const turnoDeleteName = document.querySelector("[data-turno-delete-name]");

if (turnoDeleteModal && turnoDeleteForm) {
  turnoDeleteModal.addEventListener("show.bs.modal", (event) => {
    const trigger = event.relatedTarget;
    if (!(trigger instanceof HTMLElement)) return;
    const turnoId = trigger.getAttribute("data-turno-id");
    const turnoNombre = trigger.getAttribute("data-turno-nombre");
    const idInput = turnoDeleteForm.querySelector('input[name="id"]');
    if (idInput) {
      idInput.value = turnoId || "";
    }
    if (turnoDeleteName) {
      turnoDeleteName.textContent = turnoNombre || "este turno";
    }
  });
}

if (turnoDeleteModal) {
  document.addEventListener("click", (event) => {
    const target = event.target instanceof Element ? event.target : null;
    const deleteButton = target?.closest(".turno-delete-btn");
    if (!deleteButton) return;
    event.preventDefault();
    const turnoId = deleteButton.getAttribute("data-turno-id");
    const turnoNombre = deleteButton.getAttribute("data-turno-nombre");
    const idInput = turnoDeleteForm?.querySelector('input[name="id"]');
    if (idInput) {
      idInput.value = turnoId || "";
    }
    if (turnoDeleteName) {
      turnoDeleteName.textContent = turnoNombre || "este turno";
    }
    if (window.bootstrap?.Modal) {
      const modalInstance = bootstrap.Modal.getOrCreateInstance(turnoDeleteModal);
      modalInstance.show();
    }
  });
}

const setTurnoStatus = (row, message) => {
  const status = row.querySelector(".turno-inline-status");
  if (!status) return;
  status.textContent = message;
  status.classList.add("is-visible");
  setTimeout(() => status.classList.remove("is-visible"), 1800);
};

const showTurnoToast = (message, variant = "success") => {
  window.AppToast?.show(message, variant);
};

const getTurnoEditRow = (row) => {
  const id = row.dataset.turnoId;
  if (!id) return null;
  return turnoTable?.querySelector(`[data-turno-edit-for="${id}"]`);
};

const resetTurnoInputs = (row) => {
  const editRow = getTurnoEditRow(row);
  if (!editRow) return;
  editRow.querySelector('[data-field="NombreTurno"]').value = row.dataset.nombre || "";
  editRow.querySelector('[data-field="HoraInicio"]').value = row.dataset.horaInicio || "";
  editRow.querySelector('[data-field="HoraFin"]').value = row.dataset.horaFin || "";
  const activeInput = editRow.querySelector('[data-field="Activo"]');
  if (activeInput) {
    const isActive = String(row.dataset.activo).toLowerCase() === "true";
    activeInput.checked = isActive;
    const label = activeInput.closest(".turno-switch")?.querySelector("span");
    if (label) {
      label.textContent = isActive ? "Activo" : "Inactivo";
    }
  }
};

const updateTurnoToggleState = (row, isOpen) => {
  const toggleButton = row.querySelector(".turno-inline-edit");
  if (!toggleButton) return;
  toggleButton.setAttribute("aria-expanded", isOpen ? "true" : "false");
  toggleButton.classList.toggle("is-open", isOpen);
};

const enterTurnoEdit = (row) => {
  resetTurnoInputs(row);
  const editRow = getTurnoEditRow(row);
  row.classList.add("is-editing");
  if (editRow) {
    editRow.classList.add("is-open");
  }
  updateTurnoToggleState(row, true);
};

const exitTurnoEdit = (row) => {
  row.classList.remove("is-editing");
  const editRow = getTurnoEditRow(row);
  if (editRow) {
    editRow.classList.remove("is-open");
  }
  updateTurnoToggleState(row, false);
};

const buildTurnoChanges = (row) => {
  const editRow = getTurnoEditRow(row);
  if (!editRow) {
    return { changes: {}, inputs: {} };
  }

  const original = {
    NombreTurno: (row.dataset.nombre || "").trim(),
    HoraInicio: (row.dataset.horaInicio || "").trim(),
    HoraFin: (row.dataset.horaFin || "").trim(),
    Activo: String(row.dataset.activo).toLowerCase() === "true"
  };

  const inputs = {
    NombreTurno: editRow.querySelector('[data-field="NombreTurno"]').value.trim(),
    HoraInicio: editRow.querySelector('[data-field="HoraInicio"]').value,
    HoraFin: editRow.querySelector('[data-field="HoraFin"]').value,
    Activo: editRow.querySelector('[data-field="Activo"]').checked
  };

  const changes = {};
  if (inputs.NombreTurno !== original.NombreTurno) {
    changes.NombreTurno = inputs.NombreTurno;
  }
  if (inputs.HoraInicio !== original.HoraInicio) {
    changes.HoraInicio = inputs.HoraInicio;
  }
  if (inputs.HoraFin !== original.HoraFin) {
    changes.HoraFin = inputs.HoraFin;
  }
  if (inputs.Activo !== original.Activo) {
    changes.Activo = inputs.Activo;
  }

  return { changes, inputs };
};

const updateTurnoBadge = (row, isActive) => {
  const badge = row.querySelector('[data-field-display="Activo"]');
  if (!badge) return;
  badge.textContent = isActive ? "Activo" : "Inactivo";
  badge.classList.toggle("bg-success-subtle", isActive);
  badge.classList.toggle("text-success", isActive);
  badge.classList.toggle("bg-secondary-subtle", !isActive);
  badge.classList.toggle("text-secondary", !isActive);
};

const updateTurnoHorario = (row, inicio, fin) => {
  const horario = row.querySelector('[data-field-display="Horario"]');
  if (!horario) return;
  if (!inicio || !fin) {
    horario.textContent = "-";
    return;
  }
  horario.textContent = `${inicio} - ${fin}`;
};

const saveTurnoRow = async (row) => {
  const id = row.dataset.turnoId;
  const { changes, inputs } = buildTurnoChanges(row);
  const changeKeys = Object.keys(changes);
  if (changeKeys.length === 0) {
    exitTurnoEdit(row);
    setTurnoStatus(row, "Sin cambios");
    return;
  }

  const formData = new URLSearchParams();
  formData.append("id", id);
  changeKeys.forEach((key) => {
    formData.append(key, key === "Activo" ? String(changes[key]) : changes[key]);
  });
  if (turnoRequestToken) {
    formData.append("__RequestVerificationToken", turnoRequestToken);
  }

  try {
    const response = await fetch(`/TipoTurno/Patch?id=${encodeURIComponent(id)}`, {
      method: "POST",
      body: formData,
      credentials: "same-origin",
      headers: {
        "X-Requested-With": "XMLHttpRequest",
        "RequestVerificationToken": turnoRequestToken,
        "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"
      }
    });

    if (!response.ok) {
      throw new Error("Request failed");
    }

    row.dataset.nombre = inputs.NombreTurno;
    row.dataset.horaInicio = inputs.HoraInicio;
    row.dataset.horaFin = inputs.HoraFin;
    row.dataset.activo = String(inputs.Activo);

    const nombreDisplay = row.querySelector('[data-field-display="NombreTurno"]');
    if (nombreDisplay) {
      nombreDisplay.textContent = inputs.NombreTurno;
    }
    updateTurnoHorario(row, inputs.HoraInicio, inputs.HoraFin);
    updateTurnoBadge(row, inputs.Activo);

    const activeLabel = getTurnoEditRow(row)?.querySelector(".turno-switch span");
    if (activeLabel) {
      activeLabel.textContent = inputs.Activo ? "Activo" : "Inactivo";
    }

    exitTurnoEdit(row);
    showTurnoToast("Cambios guardados.", "success");
  } catch {
    setTurnoStatus(row, "Error");
  }
};

if (turnoTable) {
  turnoTable.addEventListener("click", (event) => {
    const target = event.target instanceof Element ? event.target : null;
    if (!target) return;
    let row = target.closest(".turno-row");
    if (!row) {
      const editRow = target.closest(".turno-edit-row");
      const turnoId = editRow?.getAttribute("data-turno-edit-for");
      if (turnoId) {
        row = turnoTable.querySelector(`[data-turno-id="${turnoId}"]`);
      }
    }
    if (!row) return;

    if (target.closest(".turno-inline-edit")) {
      event.preventDefault();
      if (row.classList.contains("is-editing")) {
        resetTurnoInputs(row);
        exitTurnoEdit(row);
        return;
      }
      enterTurnoEdit(row);
      return;
    }

    if (target.closest(".turno-inline-cancel")) {
      const { changes } = buildTurnoChanges(row);
      resetTurnoInputs(row);
      exitTurnoEdit(row);
      if (Object.keys(changes).length > 0) {
        showTurnoToast("Cambios descartados.", "info");
      }
      return;
    }

    if (target.closest(".turno-inline-save")) {
      saveTurnoRow(row);
    }
  });

  turnoTable.addEventListener("change", (event) => {
    const target = event.target instanceof HTMLElement ? event.target : null;
    if (!target) return;
    if (target.dataset.field === "Activo" && target instanceof HTMLInputElement) {
      const label = target.closest(".turno-switch")?.querySelector("span");
      if (label) {
        label.textContent = target.checked ? "Activo" : "Inactivo";
      }
    }
  });
}

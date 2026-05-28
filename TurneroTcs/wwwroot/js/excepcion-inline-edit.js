const excepcionTable = document.querySelector(".excepcion-table");
const excepcionToken = document.querySelector("#excepcion-inline-token input[name='__RequestVerificationToken']");
const excepcionRequestToken = excepcionToken?.value || "";
const excepcionDeleteModal = document.querySelector("#excepcion-delete-modal");
const excepcionDeleteForm = document.querySelector("#excepcion-delete-form");
const excepcionDeleteName = document.querySelector("[data-excepcion-delete-name]");

const showExcepcionToast = (message, variant = "success") => {
  window.AppToast?.show(message, variant, { title: "Excepciones" });
};

const setExcepcionStatus = (row, message) => {
  const status = row.querySelector(".excepcion-inline-status");
  if (!status) return;
  status.textContent = message;
  status.classList.add("is-visible");
  setTimeout(() => status.classList.remove("is-visible"), 1800);
};

const getExcepcionEditRow = (row) => {
  const id = row.dataset.excepcionId;
  if (!id) return null;
  return excepcionTable?.querySelector(`[data-excepcion-edit-for="${id}"]`);
};

const getExcepcionRowFromTarget = (target) => {
  if (!(target instanceof Element)) return null;

  const row = target.closest(".excepcion-row");
  if (row) return row;

  const editRow = target.closest(".excepcion-edit-row");
  const excepcionId = editRow?.getAttribute("data-excepcion-edit-for");
  if (!excepcionId || !excepcionTable) return null;

  return excepcionTable.querySelector(`[data-excepcion-id="${excepcionId}"]`);
};

const syncValue = (input, value) => {
  if (!input) return;
  input.value = value || "";
};

const resetInputs = (row) => {
  const editRow = getExcepcionEditRow(row);
  if (!editRow) return;
  syncValue(editRow.querySelector('[data-field="PersonaId"]'), row.dataset.personaId || "");
  syncValue(editRow.querySelector('[data-field="TipoTurnoId"]'), row.dataset.tipoTurnoId || "");
  syncValue(editRow.querySelector('[data-field="MotivoExcepcion"]'), row.dataset.motivo || "");
  syncValue(editRow.querySelector('[data-field="FechaInicio"]'), row.dataset.inicio || "");
  syncValue(editRow.querySelector('[data-field="FechaFin"]'), row.dataset.fin || "");

  // Reset dias checkboxes
  const diasCsv = row.dataset.dias || "";
  const selectedDias = new Set();
  if (diasCsv && diasCsv.trim()) {
    diasCsv.split(',').forEach(d => {
      const val = d.trim();
      if (val) selectedDias.add(val);
    });
  }
  const checkboxes = editRow.querySelectorAll('[data-field="DiasSemana"]');
  checkboxes.forEach(cb => {
    cb.checked = selectedDias.has(cb.value);
  });
};

const toggleEditState = (row, isOpen) => {
  const toggle = row.querySelector(".excepcion-inline-edit");
  if (!toggle) return;
  toggle.setAttribute("aria-expanded", isOpen ? "true" : "false");
  toggle.classList.toggle("is-open", isOpen);
};

const enterEdit = (row) => {
  resetInputs(row);
  const editRow = getExcepcionEditRow(row);
  row.classList.add("is-editing");
  editRow?.classList.add("is-open");
  toggleEditState(row, true);
};

const exitEdit = (row) => {
  row.classList.remove("is-editing");
  const editRow = getExcepcionEditRow(row);
  editRow?.classList.remove("is-open");
  toggleEditState(row, false);
};

const buildChanges = (row) => {
  const editRow = getExcepcionEditRow(row);
  if (!editRow) return { changes: {}, values: {} };

  const original = {
    PersonaId: (row.dataset.personaId || "").trim(),
    TipoTurnoId: (row.dataset.tipoTurnoId || "").trim(),
    MotivoExcepcion: (row.dataset.motivo || "").trim(),
    DiasSemana: (row.dataset.dias || "").trim(),
    FechaInicio: (row.dataset.inicio || "").trim(),
    FechaFin: (row.dataset.fin || "").trim()
  };

  // Collect selected days
  const selectedDias = Array.from(editRow.querySelectorAll('[data-field="DiasSemana"]:checked'))
    .map(cb => cb.value)
    .sort();
  const diasValue = selectedDias.join(',');

  const values = {
    PersonaId: editRow.querySelector('[data-field="PersonaId"]').value.trim(),
    TipoTurnoId: editRow.querySelector('[data-field="TipoTurnoId"]').value.trim(),
    MotivoExcepcion: editRow.querySelector('[data-field="MotivoExcepcion"]').value.trim(),
    DiasSemana: diasValue,
    FechaInicio: editRow.querySelector('[data-field="FechaInicio"]').value,
    FechaFin: editRow.querySelector('[data-field="FechaFin"]').value
  };

  const changes = {};
  Object.keys(values).forEach((key) => {
    if (values[key] !== original[key]) {
      changes[key] = values[key];
    }
  });

  return { changes, values };
};

const refreshDisplayValues = (row, values) => {
  const personaDisplay = row.querySelector('[data-field-display="Persona"]');
  if (personaDisplay) {
    personaDisplay.textContent = editRowSelectedText(row, '[data-field="PersonaId"]') || personaDisplay.textContent;
  }
  const tipoDisplay = row.querySelector('[data-field-display="TipoTurno"]');
  if (tipoDisplay) {
    tipoDisplay.textContent = editRowSelectedText(row, '[data-field="TipoTurnoId"]') || tipoDisplay.textContent;
  }
  const motivoDisplay = row.querySelector('[data-field-display="MotivoExcepcion"]');
  if (motivoDisplay) motivoDisplay.textContent = values.MotivoExcepcion;

  const diasDisplay = row.querySelector('[data-field-display="DiasSemana"]');
  if (diasDisplay) {
    const diasNombres = ["Lun", "Mar", "Mié", "Jue", "Vie", "Sab", "Dom"];
    if (!values.DiasSemana || values.DiasSemana.trim() === "") {
      diasDisplay.textContent = "Todos";
    } else {
      const nombres = values.DiasSemana.split(',').map(d => {
        const idx = parseInt(d.trim());
        return !isNaN(idx) && idx >= 0 && idx < diasNombres.length ? diasNombres[idx] : null;
      }).filter(n => n !== null);
      diasDisplay.textContent = nombres.length > 0 ? nombres.join(", ") : "Todos";
    }
  }

  const inicioDisplay = row.querySelector('[data-field-display="FechaInicio"]');
  if (inicioDisplay) inicioDisplay.textContent = values.FechaInicio;
  const finDisplay = row.querySelector('[data-field-display="FechaFin"]');
  if (finDisplay) finDisplay.textContent = values.FechaFin;
};

const saveRow = async (row) => {
  const id = row.dataset.excepcionId;
  const equipoId = document.querySelector("input[name='EquipoId']")?.value
    || document.querySelector("input[name='equipoId']")?.value
    || window.location.search.match(/equipoId=([^&]+)/)?.[1]
    || "";
  const { changes, values } = buildChanges(row);
  if (Object.keys(changes).length === 0) {
    exitEdit(row);
    setExcepcionStatus(row, "Sin cambios");
    return;
  }

  const formData = new URLSearchParams();
  formData.append("id", id);
  formData.append("equipoId", equipoId);
  Object.entries(changes).forEach(([key, value]) => formData.append(key, value));
  if (excepcionRequestToken) {
    formData.append("__RequestVerificationToken", excepcionRequestToken);
  }

  try {
    const response = await fetch(`/Excepcion/Patch?equipoId=${encodeURIComponent(equipoId)}&id=${encodeURIComponent(id)}`, {
      method: "POST",
      body: formData,
      credentials: "same-origin",
      headers: {
        "X-Requested-With": "XMLHttpRequest",
        "RequestVerificationToken": excepcionRequestToken,
        "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"
      }
    });

    if (!response.ok) {
      throw new Error("Request failed");
    }

    row.dataset.personaId = values.PersonaId;
    row.dataset.tipoTurnoId = values.TipoTurnoId;
    row.dataset.motivo = values.MotivoExcepcion;
    row.dataset.dias = values.DiasSemana;
    row.dataset.inicio = values.FechaInicio;
    row.dataset.fin = values.FechaFin;
    refreshDisplayValues(row, values);

    exitEdit(row);
    showExcepcionToast("Cambios guardados.", "success");
  } catch {
    setExcepcionStatus(row, "Error");
  }
};

const editRowSelectedText = (row, selector) => {
  const editRow = getExcepcionEditRow(row);
  if (!editRow) return "";
  const select = editRow.querySelector(selector);
  if (!(select instanceof HTMLSelectElement)) return "";
  return select.options[select.selectedIndex]?.textContent?.trim() || "";
};

if (excepcionDeleteModal && excepcionDeleteForm) {
  excepcionDeleteModal.addEventListener("show.bs.modal", (event) => {
    const trigger = event.relatedTarget;
    if (!(trigger instanceof HTMLElement)) return;
    const excepcionId = trigger.getAttribute("data-excepcion-id");
    const excepcionPersona = trigger.getAttribute("data-excepcion-persona");
    const idInput = excepcionDeleteForm.querySelector('input[name="id"]');
    if (idInput) idInput.value = excepcionId || "";
    if (excepcionDeleteName) excepcionDeleteName.textContent = excepcionPersona || "esta persona";
  });
}

const openDeleteModal = (button) => {
  if (!(button instanceof HTMLElement)) return;
  const excepcionId = button.getAttribute("data-excepcion-id");
  const excepcionPersona = button.getAttribute("data-excepcion-persona");
  const idInput = excepcionDeleteForm?.querySelector('input[name="id"]');
  if (idInput) idInput.value = excepcionId || "";
  if (excepcionDeleteName) excepcionDeleteName.textContent = excepcionPersona || "esta persona";

  if (window.bootstrap?.Modal && excepcionDeleteModal) {
    const modal = window.bootstrap.Modal.getOrCreateInstance(excepcionDeleteModal);
    modal.show();
  }
};

if (excepcionTable) {
  excepcionTable.addEventListener("click", (event) => {
    const target = event.target instanceof Element ? event.target : null;
    if (!target) return;
    const row = getExcepcionRowFromTarget(target);
    if (!row) return;

    if (target.closest(".excepcion-inline-edit")) {
      event.preventDefault();
      if (row.classList.contains("is-editing")) {
        resetInputs(row);
        exitEdit(row);
        return;
      }
      enterEdit(row);
      return;
    }

    if (target.closest(".excepcion-inline-cancel")) {
      resetInputs(row);
      exitEdit(row);
      return;
    }

    if (target.closest(".excepcion-inline-save")) {
      saveRow(row);
      return;
    }

    if (target.closest(".excepcion-delete-btn")) {
      event.preventDefault();
      openDeleteModal(target.closest(".excepcion-delete-btn"));
    }
  });
}

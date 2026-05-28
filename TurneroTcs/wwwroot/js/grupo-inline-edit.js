const grupoTable = document.querySelector(".grupo-table");
const grupoToken = document.querySelector("#grupo-inline-token input[name='__RequestVerificationToken']");
const grupoRequestToken = grupoToken?.value || "";
const grupoDeleteModal = document.querySelector("#grupo-delete-modal");
const grupoDeleteForm = document.querySelector("#grupo-delete-form");
const grupoDeleteName = document.querySelector("[data-grupo-delete-name]");
const grupoDeleteDefaultAction = grupoDeleteForm?.getAttribute("action") || "/Grupo/Delete";
let grupoDeleteSubmitting = false;

const setGrupoDeleteTarget = (trigger) => {
  if (!grupoDeleteForm || !(trigger instanceof HTMLElement)) return;

  const grupoId = trigger.getAttribute("data-grupo-id") || "";
  const grupoNombre = trigger.getAttribute("data-grupo-nombre") || "";
  const idInput = grupoDeleteForm.querySelector('input[name="id"]');
  if (idInput) {
    idInput.value = grupoId;
  }
  if (grupoDeleteName) {
    grupoDeleteName.textContent = grupoNombre || "este grupo";
  }
  grupoDeleteForm.dataset.grupoId = grupoId;

  const action = new URL(grupoDeleteDefaultAction, window.location.origin);
  if (grupoId) {
    action.searchParams.set("id", grupoId);
  } else {
    action.searchParams.delete("id");
  }
  grupoDeleteForm.setAttribute("action", `${action.pathname}${action.search}`);
};

if (grupoDeleteModal && grupoDeleteForm) {
  grupoDeleteModal.addEventListener("show.bs.modal", (event) => {
    setGrupoDeleteTarget(event.relatedTarget);
  });

  grupoDeleteForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (grupoDeleteSubmitting) return;

    const grupoId = grupoDeleteForm.dataset.grupoId || grupoDeleteForm.querySelector('input[name="id"]')?.value || "";
    const row = grupoTable?.querySelector(`[data-grupo-id="${CSS.escape(grupoId)}"]`);
    if (!grupoId || !row) {
      showGrupoToast("No se pudo identificar el grupo.", "danger");
      return;
    }

    grupoDeleteSubmitting = true;
    const submitButton = grupoDeleteForm.querySelector('button[type="submit"]');
    if (submitButton) {
      submitButton.disabled = true;
    }

    try {
      const response = await fetch(grupoDeleteForm.action, {
        method: "POST",
        body: new FormData(grupoDeleteForm),
        credentials: "same-origin",
        headers: {
          "X-Requested-With": "XMLHttpRequest",
          "RequestVerificationToken": grupoRequestToken
        }
      });

      if (!response.ok) {
        let message = "No se pudo desactivar el grupo.";
        try {
          const payload = await response.json();
          message = payload.message || payload.error || message;
        } catch {
          // Keep default message when response is not JSON.
        }
        throw new Error(message);
      }

      row.dataset.activo = "False";
      updateGrupoBadge(row, false);
      resetGrupoInputs(row);
      const deleteButton = row.querySelector(".grupo-delete-btn");
      if (deleteButton) {
        deleteButton.remove();
      }

      const modal = window.bootstrap?.Modal.getInstance(grupoDeleteModal);
      modal?.hide();
      showGrupoToast("Grupo desactivado.", "success");
    } catch (error) {
      showGrupoToast(error.message || "No se pudo desactivar el grupo.", "danger");
    } finally {
      grupoDeleteSubmitting = false;
      if (submitButton) {
        submitButton.disabled = false;
      }
    }
  });
}

const setGrupoStatus = (row, message) => {
  const status = row.querySelector(".grupo-inline-status");
  if (!status) return;
  status.textContent = message;
  status.classList.add("is-visible");
  setTimeout(() => status.classList.remove("is-visible"), 1800);
};

const showGrupoToast = (message, variant = "success") => {
  window.AppToast?.show(message, variant);
};

const getGrupoEditRow = (row) => {
  const id = row.dataset.grupoId;
  if (!id) return null;
  return grupoTable?.querySelector(`[data-grupo-edit-for="${id}"]`);
};

const resetGrupoInputs = (row) => {
  const editRow = getGrupoEditRow(row);
  if (!editRow) return;
  editRow.querySelector('[data-field="NombreGrupo"]').value = row.dataset.nombre || "";
  const equipoSelect = editRow.querySelector('[data-field="EquipoId"]');
  if (equipoSelect) {
    equipoSelect.value = row.dataset.equipoId || "";
  }
  const activeInput = editRow.querySelector('[data-field="Activo"]');
  if (activeInput) {
    const isActive = String(row.dataset.activo).toLowerCase() === "true";
    activeInput.checked = isActive;
    const label = activeInput.closest(".grupo-switch")?.querySelector("span");
    if (label) {
      label.textContent = isActive ? "Activo" : "Inactivo";
    }
  }
};

const updateGrupoToggleState = (row, isOpen) => {
  const toggleButton = row.querySelector(".grupo-inline-edit");
  if (!toggleButton) return;
  toggleButton.setAttribute("aria-expanded", isOpen ? "true" : "false");
  toggleButton.classList.toggle("is-open", isOpen);
};

const enterGrupoEdit = (row) => {
  resetGrupoInputs(row);
  const editRow = getGrupoEditRow(row);
  row.classList.add("is-editing");
  if (editRow) {
    editRow.classList.add("is-open");
  }
  updateGrupoToggleState(row, true);
};

const exitGrupoEdit = (row) => {
  row.classList.remove("is-editing");
  const editRow = getGrupoEditRow(row);
  if (editRow) {
    editRow.classList.remove("is-open");
  }
  updateGrupoToggleState(row, false);
};

const buildGrupoChanges = (row) => {
  const editRow = getGrupoEditRow(row);
  if (!editRow) {
    return { changes: {}, inputs: {} };
  }

  const original = {
    NombreGrupo: (row.dataset.nombre || "").trim(),
    EquipoId: (row.dataset.equipoId || "").trim(),
    Activo: String(row.dataset.activo).toLowerCase() === "true"
  };

  const inputs = {
    NombreGrupo: editRow.querySelector('[data-field="NombreGrupo"]').value.trim(),
    EquipoId: editRow.querySelector('[data-field="EquipoId"]')?.value.trim() || "",
    Activo: editRow.querySelector('[data-field="Activo"]').checked
  };

  const changes = {};
  if (inputs.NombreGrupo !== original.NombreGrupo) {
    changes.NombreGrupo = inputs.NombreGrupo;
  }
  if (inputs.EquipoId !== original.EquipoId) {
    changes.EquipoId = inputs.EquipoId;
  }
  if (inputs.Activo !== original.Activo) {
    changes.Activo = inputs.Activo;
  }

  return { changes, inputs };
};

const updateGrupoBadge = (row, isActive) => {
  const badge = row.querySelector('[data-field-display="Activo"]');
  if (!badge) return;
  badge.textContent = isActive ? "Activo" : "Inactivo";
  badge.classList.toggle("bg-success-subtle", isActive);
  badge.classList.toggle("text-success", isActive);
  badge.classList.toggle("bg-secondary-subtle", !isActive);
  badge.classList.toggle("text-secondary", !isActive);
};

const saveGrupoRow = async (row) => {
  const id = row.dataset.grupoId;
  const { changes, inputs } = buildGrupoChanges(row);
  const changeKeys = Object.keys(changes);
  if (changeKeys.length === 0) {
    exitGrupoEdit(row);
    setGrupoStatus(row, "Sin cambios");
    return;
  }

  const formData = new URLSearchParams();
  formData.append("id", id);
  changeKeys.forEach((key) => {
    formData.append(key, key === "Activo" ? String(changes[key]) : changes[key]);
  });
  if (grupoRequestToken) {
    formData.append("__RequestVerificationToken", grupoRequestToken);
  }

  try {
    const response = await fetch(`/Grupo/Patch?id=${encodeURIComponent(id)}`, {
      method: "POST",
      body: formData,
      credentials: "same-origin",
      headers: {
        "X-Requested-With": "XMLHttpRequest",
        "RequestVerificationToken": grupoRequestToken,
        "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"
      }
    });

    if (!response.ok) {
      throw new Error("Request failed");
    }

    row.dataset.nombre = inputs.NombreGrupo;
    row.dataset.equipoId = inputs.EquipoId;
    row.dataset.activo = String(inputs.Activo);

    const nombreDisplay = row.querySelector('[data-field-display="NombreGrupo"]');
    if (nombreDisplay) {
      nombreDisplay.textContent = inputs.NombreGrupo;
    }
    const equipoDisplay = row.querySelector('[data-field-display="Equipo"]');
    if (equipoDisplay) {
      const select = getGrupoEditRow(row)?.querySelector('[data-field="EquipoId"]');
      equipoDisplay.textContent = select?.selectedOptions[0]?.textContent?.trim() || "-";
    }
    updateGrupoBadge(row, inputs.Activo);

    const activeLabel = getGrupoEditRow(row)?.querySelector(".grupo-switch span");
    if (activeLabel) {
      activeLabel.textContent = inputs.Activo ? "Activo" : "Inactivo";
    }

    exitGrupoEdit(row);
    showGrupoToast("Cambios guardados.", "success");
  } catch {
    setGrupoStatus(row, "Error");
  }
};

if (grupoTable) {
  grupoTable.addEventListener("click", (event) => {
    const target = event.target instanceof Element ? event.target : null;
    if (!target) return;

    const deleteTrigger = target.closest(".grupo-delete-btn");
    if (deleteTrigger) {
      setGrupoDeleteTarget(deleteTrigger);
      return;
    }

    if (target.closest(".grupo-create-cancel")) {
      const createRow = grupoTable.querySelector("[data-grupo-create]");
      if (createRow) {
        const form = createRow.querySelector(".grupo-create-form");
        form?.reset();
        createRow.classList.remove("is-open");
      }
      return;
    }

    let row = target.closest(".grupo-row");
    if (!row) {
      const editRow = target.closest(".grupo-edit-row");
      const grupoId = editRow?.getAttribute("data-grupo-edit-for");
      if (grupoId) {
        row = grupoTable.querySelector(`[data-grupo-id="${grupoId}"]`);
      }
    }
    if (!row) return;

    if (target.closest(".grupo-inline-edit")) {
      event.preventDefault();
      if (row.classList.contains("is-editing")) {
        resetGrupoInputs(row);
        exitGrupoEdit(row);
        return;
      }
      enterGrupoEdit(row);
      return;
    }

    if (target.closest(".grupo-inline-cancel")) {
      const { changes } = buildGrupoChanges(row);
      resetGrupoInputs(row);
      exitGrupoEdit(row);
      if (Object.keys(changes).length > 0) {
        showGrupoToast("Cambios descartados.", "info");
      }
      return;
    }

    if (target.closest(".grupo-inline-save")) {
      saveGrupoRow(row);
    }
  });

  grupoTable.addEventListener("change", (event) => {
    const target = event.target instanceof HTMLElement ? event.target : null;
    if (!target) return;
    if (target.dataset.field === "Activo" && target instanceof HTMLInputElement) {
      const label = target.closest(".grupo-switch")?.querySelector("span");
      if (label) {
        label.textContent = target.checked ? "Activo" : "Inactivo";
      }
    }
  });
}

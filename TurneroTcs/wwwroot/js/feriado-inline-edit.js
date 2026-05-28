const feriadoTable = document.querySelector(".feriado-table");
const feriadoToken = document.querySelector("#feriado-inline-token input[name='__RequestVerificationToken']");
const feriadoRequestToken = feriadoToken?.value || "";
const feriadoDeleteModal = document.querySelector("#feriado-delete-modal");
const feriadoDeleteForm = document.querySelector("#feriado-delete-form");
const feriadoDeleteName = document.querySelector("[data-feriado-delete-name]");

const initFeriadoDatePickers = (root = document) => {
  if (window.AppCalendar && typeof window.AppCalendar.refresh === "function") {
    window.AppCalendar.refresh(root);
  }
};

const syncFeriadoDateValue = (input, value) => {
  if (!(input instanceof HTMLInputElement)) return;
  if (input._flatpickr) {
    input._flatpickr.setDate(value || null, true, "Y-m-d");
    return;
  }
  input.value = value || "";
};

if (feriadoDeleteModal && feriadoDeleteForm) {
  feriadoDeleteModal.addEventListener("show.bs.modal", (event) => {
    const trigger = event.relatedTarget;
    if (!(trigger instanceof HTMLElement)) return;
    const feriadoId = trigger.getAttribute("data-feriado-id");
    const feriadoNombre = trigger.getAttribute("data-feriado-nombre");
    const idInput = feriadoDeleteForm.querySelector('input[name="id"]');
    if (idInput) {
      idInput.value = feriadoId || "";
    }
    if (feriadoDeleteName) {
      feriadoDeleteName.textContent = feriadoNombre || "este feriado";
    }
  });
}

const setFeriadoStatus = (row, message) => {
  const status = row.querySelector(".feriado-inline-status");
  if (!status) return;
  status.textContent = message;
  status.classList.add("is-visible");
  setTimeout(() => status.classList.remove("is-visible"), 1800);
};

const getFeriadoEditRow = (row) => {
  const id = row.dataset.feriadoId;
  if (!id) return null;
  return feriadoTable?.querySelector(`[data-feriado-edit-for="${id}"]`);
};

const resetFeriadoInputs = (row) => {
  const editRow = getFeriadoEditRow(row);
  if (!editRow) return;
  editRow.querySelector('[data-field="NombreFeriado"]').value = row.dataset.nombre || "";
  syncFeriadoDateValue(editRow.querySelector('[data-field="InicioFeriado"]'), row.dataset.inicio || "");
  syncFeriadoDateValue(editRow.querySelector('[data-field="FinFeriado"]'), row.dataset.fin || "");
};

const updateFeriadoToggleState = (row, isOpen) => {
  const toggleButton = row.querySelector(".feriado-inline-edit");
  if (!toggleButton) return;
  toggleButton.setAttribute("aria-expanded", isOpen ? "true" : "false");
  toggleButton.classList.toggle("is-open", isOpen);
};

const enterFeriadoEdit = (row) => {
  resetFeriadoInputs(row);
  const editRow = getFeriadoEditRow(row);
  row.classList.add("is-editing");
  if (editRow) {
    editRow.classList.add("is-open");
  }
  updateFeriadoToggleState(row, true);
};

const exitFeriadoEdit = (row) => {
  row.classList.remove("is-editing");
  const editRow = getFeriadoEditRow(row);
  if (editRow) {
    editRow.classList.remove("is-open");
  }
  updateFeriadoToggleState(row, false);
};

const buildFeriadoChanges = (row) => {
  const editRow = getFeriadoEditRow(row);
  if (!editRow) {
    return { changes: {}, inputs: {} };
  }

  const original = {
    NombreFeriado: (row.dataset.nombre || "").trim(),
    InicioFeriado: (row.dataset.inicio || "").trim(),
    FinFeriado: (row.dataset.fin || "").trim()
  };

  const inputs = {
    NombreFeriado: editRow.querySelector('[data-field="NombreFeriado"]').value.trim(),
    InicioFeriado: editRow.querySelector('[data-field="InicioFeriado"]').value,
    FinFeriado: editRow.querySelector('[data-field="FinFeriado"]').value
  };

  const changes = {};
  if (inputs.NombreFeriado !== original.NombreFeriado) {
    changes.NombreFeriado = inputs.NombreFeriado;
  }
  if (inputs.InicioFeriado !== original.InicioFeriado) {
    changes.InicioFeriado = inputs.InicioFeriado;
  }
  if (inputs.FinFeriado !== original.FinFeriado) {
    changes.FinFeriado = inputs.FinFeriado;
  }

  return { changes, inputs };
};

const saveFeriadoRow = async (row) => {
  const id = row.dataset.feriadoId;
  const { changes, inputs } = buildFeriadoChanges(row);
  const changeKeys = Object.keys(changes);
  if (changeKeys.length === 0) {
    exitFeriadoEdit(row);
    setFeriadoStatus(row, "Sin cambios");
    return;
  }

  const formData = new URLSearchParams();
  formData.append("id", id);
  changeKeys.forEach((key) => {
    formData.append(key, changes[key]);
  });
  if (feriadoRequestToken) {
    formData.append("__RequestVerificationToken", feriadoRequestToken);
  }

  try {
    const response = await fetch(`/Feriado/Patch?id=${encodeURIComponent(id)}`, {
      method: "POST",
      body: formData,
      credentials: "same-origin",
      headers: {
        "X-Requested-With": "XMLHttpRequest",
        "RequestVerificationToken": feriadoRequestToken,
        "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"
      }
    });

    if (!response.ok) {
      throw new Error("Request failed");
    }

    row.dataset.nombre = inputs.NombreFeriado;
    row.dataset.inicio = inputs.InicioFeriado;
    row.dataset.fin = inputs.FinFeriado;

    const nombreDisplay = row.querySelector('[data-field-display="NombreFeriado"]');
    if (nombreDisplay) {
      nombreDisplay.textContent = inputs.NombreFeriado;
    }

    const inicioDisplay = row.querySelector('[data-field-display="InicioFeriado"]');
    if (inicioDisplay) {
      inicioDisplay.textContent = inputs.InicioFeriado;
    }

    const finDisplay = row.querySelector('[data-field-display="FinFeriado"]');
    if (finDisplay) {
      finDisplay.textContent = inputs.FinFeriado;
    }

    exitFeriadoEdit(row);
  } catch {
    setFeriadoStatus(row, "Error");
  }
};

if (feriadoTable) {
  initFeriadoDatePickers(feriadoTable);

  feriadoTable.addEventListener("click", (event) => {
    const target = event.target instanceof Element ? event.target : null;
    if (!target) return;
    let row = target.closest(".feriado-row");
    if (!row) {
      const editRow = target.closest(".feriado-edit-row");
      const feriadoId = editRow?.getAttribute("data-feriado-edit-for");
      if (feriadoId) {
        row = feriadoTable.querySelector(`[data-feriado-id="${feriadoId}"]`);
      }
    }
    if (!row) return;

    if (target.closest(".feriado-inline-edit")) {
      event.preventDefault();
      if (row.classList.contains("is-editing")) {
        resetFeriadoInputs(row);
        exitFeriadoEdit(row);
        return;
      }
      enterFeriadoEdit(row);
      return;
    }

    if (target.closest(".feriado-inline-cancel")) {
      resetFeriadoInputs(row);
      exitFeriadoEdit(row);
      return;
    }

    if (target.closest(".feriado-inline-save")) {
      saveFeriadoRow(row);
    }
  });

}

initFeriadoDatePickers();

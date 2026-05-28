const table = document.querySelector(".persona-table");
const tokenInput = document.querySelector("#persona-inline-token input[name='__RequestVerificationToken']");
const requestToken = tokenInput?.value || "";
const deleteModal = document.querySelector("#persona-delete-modal");
const deleteForm = document.querySelector("#persona-delete-form");
const deleteName = document.querySelector("[data-persona-delete-name]");
const deleteBaseMessage = document.querySelector("[data-persona-delete-base-message]");
const deleteQuestion = document.querySelector("[data-persona-delete-question]");
const deleteFutureWarning = document.querySelector("[data-persona-delete-future-warning]");
const deleteConfirmButton = deleteForm?.querySelector('button[type="submit"]');
const restoreModal = document.querySelector("#persona-restore-modal");
const restoreForm = document.querySelector("#persona-restore-form");
const restoreName = document.querySelector("[data-persona-restore-name]");
const resetModal = document.querySelector("#persona-reset-modal");
const resetName = document.querySelector("[data-persona-reset-name]");
const resetPasswordInput = document.querySelector("#persona-reset-password");
const resetPasswordConfirmInput = document.querySelector("#persona-reset-password-confirm");
const resetConfirmButton = document.querySelector("#persona-reset-confirm");
const resetStrengthText = document.querySelector("[data-persona-reset-strength-text]");
const resetStrengthFill = document.querySelector("[data-persona-reset-strength-fill]");
const resetMatchText = document.querySelector("[data-persona-reset-match-text]");
const permisosModal = document.querySelector("#persona-permisos-modal");
const permisosPersonaNombre = document.querySelector("[data-persona-permisos-nombre]");
const permisosList = document.querySelector("[data-persona-permisos-list]");
const permisosSearch = document.querySelector("[data-permiso-search]");
const permisosModulo = document.querySelector("[data-permiso-modulo]");
const permisosReglaPersonal = document.querySelector("[data-permiso-directo]");
const permisosClear = document.querySelector("[data-permiso-clear]");
let resetPersonaId = "";
let deleteImpactRequestId = 0;

const permisosState = {
  userId: "",
  permisos: [],
  filters: {
    search: "",
    modulo: "",
    reglaPersonal: ""
  }
};

const setDeleteModalDisabled = (disabled) => {
  if (deleteConfirmButton instanceof HTMLButtonElement) {
    deleteConfirmButton.disabled = disabled;
  }
};

const setDeleteModalContent = ({
  personaId = "",
  personaNombre = "esta persona",
  loading = false,
  futureTurnosCount = 0,
  firstFutureTurnoDate = "",
  lastFutureTurnoDate = "",
  hasLinkedFutureTurnos = false
} = {}) => {
  const idInput = deleteForm?.querySelector('input[name="id"]');
  if (idInput) {
    idInput.value = personaId;
  }

  if (deleteName) {
    deleteName.textContent = personaNombre || "esta persona";
  }

  if (deleteBaseMessage) {
    deleteBaseMessage.textContent = "Esta accion marcara la persona como borrada y bloqueara su acceso.";
  }

  if (deleteQuestion) {
    deleteQuestion.innerHTML = `Sus registros historicos se conservaran. Deseas continuar con <strong>${escapeHtml(personaNombre || "esta persona")}</strong>?`;
  }

  if (deleteFutureWarning) {
    deleteFutureWarning.classList.add("d-none");
    deleteFutureWarning.textContent = "";
  }

  if (loading) {
    if (deleteFutureWarning) {
      deleteFutureWarning.classList.remove("d-none");
      deleteFutureWarning.classList.remove("alert-danger");
      deleteFutureWarning.classList.add("alert-warning");
      deleteFutureWarning.textContent = "Revisando turnos desde hoy en adelante...";
    }
    setDeleteModalDisabled(true);
    return;
  }

  if (futureTurnosCount > 0 && deleteFutureWarning) {
    const countLabel = futureTurnosCount === 1 ? "1 turno futuro" : `${futureTurnosCount} turnos futuros`;
    const dateRange = firstFutureTurnoDate && lastFutureTurnoDate
      ? firstFutureTurnoDate === lastFutureTurnoDate
        ? ` con fecha ${firstFutureTurnoDate}`
        : ` entre ${firstFutureTurnoDate} y ${lastFutureTurnoDate}`
      : "";
    deleteFutureWarning.classList.remove("d-none");

    if (hasLinkedFutureTurnos) {
      deleteFutureWarning.classList.remove("alert-warning");
      deleteFutureWarning.classList.add("alert-danger");
      deleteFutureWarning.textContent = `Esta persona tiene ${countLabel}${dateRange} vinculados a solicitudes o reemplazos. Debes resolver esos turnos antes de borrarla.`;
      setDeleteModalDisabled(true);
      return;
    }

    deleteFutureWarning.classList.remove("alert-danger");
    deleteFutureWarning.classList.add("alert-warning");
    deleteFutureWarning.textContent = `Esta persona tiene ${countLabel}${dateRange}. Si continuas, esos turnos se eliminaran permanentemente.`;
  }

  setDeleteModalDisabled(false);
};

if (deleteModal) {
  deleteModal.addEventListener("hidden.bs.modal", () => {
    deleteImpactRequestId++;
    setDeleteModalContent();
    setDeleteModalDisabled(false);
  });

  document.addEventListener("click", async (event) => {
    const target = event.target instanceof Element ? event.target : null;
    const deleteButton = target?.closest(".persona-delete-btn");
    if (!deleteButton) return;
    event.preventDefault();
    event.stopPropagation();

    const personaId = deleteButton.getAttribute("data-persona-id") || "";
    const personaNombre = deleteButton.getAttribute("data-persona-nombre") || "esta persona";
    const requestId = ++deleteImpactRequestId;

    setDeleteModalContent({
      personaId,
      personaNombre,
      loading: true
    });

    if (window.bootstrap?.Modal) {
      const modalInstance = bootstrap.Modal.getOrCreateInstance(deleteModal);
      modalInstance.show();
    }

    try {
      const impact = await apiGetJson(`/Persona/DeleteImpact?id=${encodeURIComponent(personaId)}`);
      if (requestId !== deleteImpactRequestId) return;

      setDeleteModalContent({
        personaId,
        personaNombre,
        futureTurnosCount: Number(impact?.futureTurnosCount || 0),
        firstFutureTurnoDate: impact?.firstFutureTurnoDate || "",
        lastFutureTurnoDate: impact?.lastFutureTurnoDate || "",
        hasLinkedFutureTurnos: Boolean(impact?.hasLinkedFutureTurnos)
      });
    } catch (error) {
      if (requestId !== deleteImpactRequestId) return;

      setDeleteModalContent({
        personaId,
        personaNombre
      });
      showToast(error?.message || "No se pudo revisar el impacto del borrado.", "danger");
    }
  });
}

if (restoreModal && restoreForm) {
  restoreModal.addEventListener("show.bs.modal", (event) => {
    const trigger = event.relatedTarget;
    if (!(trigger instanceof HTMLElement)) return;
    const personaId = trigger.getAttribute("data-persona-id");
    const personaNombre = trigger.getAttribute("data-persona-nombre");
    const idInput = restoreForm.querySelector('input[name="id"]');
    if (idInput) {
      idInput.value = personaId || "";
    }
    if (restoreName) {
      restoreName.textContent = personaNombre || "esta persona";
    }
  });
}

if (resetModal) {
  resetModal.addEventListener("show.bs.modal", (event) => {
    const trigger = event.relatedTarget;
    if (!(trigger instanceof HTMLElement)) return;
    resetPersonaId = trigger.getAttribute("data-persona-id") || "";
    const personaNombre = trigger.getAttribute("data-persona-nombre");
    if (resetName) {
      resetName.textContent = personaNombre || "esta persona";
    }
    if (resetPasswordInput) {
      resetPasswordInput.value = "";
    }
    if (resetPasswordConfirmInput) {
      resetPasswordConfirmInput.value = "";
    }
    document.querySelectorAll("[data-password-toggle]").forEach((button) => {
      const selector = button.getAttribute("data-password-toggle") || "";
      const targetInput = selector ? document.querySelector(selector) : null;
      if (targetInput instanceof HTMLInputElement) {
        targetInput.type = "password";
      }
      button.setAttribute("aria-label", "Mostrar contrasena");
      button.classList.remove("is-visible");
    });
    updateResetPasswordFeedback();
  });
}

if (resetConfirmButton) {
  resetConfirmButton.addEventListener("click", (event) => {
    event.preventDefault();
    resetPassword();
  });
}

const getPasswordStrength = (value) => {
  const password = value || "";
  if (!password) {
    return { level: "empty", label: "Sin evaluar", progress: 0 };
  }

  let score = 0;
  if (password.length >= 8) score += 1;
  if (/[a-z]/.test(password) && /[A-Z]/.test(password)) score += 1;
  if (/\d/.test(password)) score += 1;
  if (/[^A-Za-z0-9]/.test(password)) score += 1;

  if (score <= 1) {
    return { level: "low", label: "Seguridad baja", progress: 34 };
  }
  if (score <= 3) {
    return { level: "medium", label: "Seguridad media", progress: 68 };
  }
  return { level: "high", label: "Seguridad alta", progress: 100 };
};

const updateResetPasswordFeedback = () => {
  const newPassword = resetPasswordInput?.value || "";
  const confirmPassword = resetPasswordConfirmInput?.value || "";
  const strength = getPasswordStrength(newPassword);

  if (resetStrengthText) {
    resetStrengthText.textContent = strength.label;
    resetStrengthText.classList.remove("is-empty", "is-low", "is-medium", "is-high");
    resetStrengthText.classList.add(`is-${strength.level}`);
  }

  if (resetStrengthFill) {
    resetStrengthFill.style.width = `${strength.progress}%`;
    resetStrengthFill.classList.remove("is-empty", "is-low", "is-medium", "is-high");
    resetStrengthFill.classList.add(`is-${strength.level}`);
  }

  let isMatchValid = false;
  if (resetMatchText) {
    resetMatchText.classList.remove("is-empty", "is-match", "is-mismatch");

    if (!confirmPassword) {
      resetMatchText.textContent = "Confirma la contrasena para validar coincidencia.";
      resetMatchText.classList.add("is-empty");
    } else if (newPassword === confirmPassword) {
      resetMatchText.textContent = "Las contrasenas coinciden.";
      resetMatchText.classList.add("is-match");
      isMatchValid = true;
    } else {
      resetMatchText.textContent = "Las contrasenas no coinciden.";
      resetMatchText.classList.add("is-mismatch");
    }
  }

  if (resetConfirmButton instanceof HTMLButtonElement) {
    resetConfirmButton.disabled = !newPassword || !confirmPassword || !isMatchValid;
  }
};

if (resetPasswordInput) {
  resetPasswordInput.addEventListener("input", updateResetPasswordFeedback);
}

if (resetPasswordConfirmInput) {
  resetPasswordConfirmInput.addEventListener("input", updateResetPasswordFeedback);
}

document.querySelectorAll("[data-password-toggle]").forEach((button) => {
  button.addEventListener("click", () => {
    const selector = button.getAttribute("data-password-toggle") || "";
    const targetInput = selector ? document.querySelector(selector) : null;
    if (!(targetInput instanceof HTMLInputElement)) return;

    const nextType = targetInput.type === "password" ? "text" : "password";
    targetInput.type = nextType;
    const isVisible = nextType === "text";
    button.classList.toggle("is-visible", isVisible);
    button.setAttribute("aria-label", isVisible ? "Ocultar contrasena" : "Mostrar contrasena");
  });
});

const normalizeText = (value) => (value || "").trim();
const escapeHtml = (value) => String(value ?? "")
  .replaceAll("&", "&amp;")
  .replaceAll("<", "&lt;")
  .replaceAll(">", "&gt;")
  .replaceAll('"', "&quot;")
  .replaceAll("'", "&#039;");
const setStatus = (row, message) => {
  const status = row.querySelector(".persona-inline-status");
  if (!status) return;
  status.textContent = message;
  status.classList.add("is-visible");
  setTimeout(() => status.classList.remove("is-visible"), 1800);
};

const showToast = (message, variant = "success") => {
  window.AppToast?.show(message, variant);
};

const resetPassword = async () => {
  if (!resetPersonaId) {
    showToast("Persona invalida.", "info");
    return;
  }
  const newPassword = resetPasswordInput?.value.trim() || "";
  const confirmPassword = resetPasswordConfirmInput?.value.trim() || "";
  if (!newPassword) {
    showToast("Ingresa una contrasena.", "info");
    return;
  }
  if (newPassword !== confirmPassword) {
    showToast("Las contrasenas no coinciden.", "info");
    return;
  }

  const formData = new URLSearchParams();
  formData.append("id", resetPersonaId);
  formData.append("newPassword", newPassword);
  if (requestToken) {
    formData.append("__RequestVerificationToken", requestToken);
  }

  try {
    const response = await fetch("/Persona/ResetPassword", {
      method: "POST",
      body: formData,
      credentials: "same-origin",
      headers: {
        "X-Requested-With": "XMLHttpRequest",
        "RequestVerificationToken": requestToken,
        "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"
      }
    });

    if (!response.ok) {
      const data = await response.json().catch(() => ({}));
      throw new Error(data?.message || "Error");
    }

    showToast("Contrasena restablecida.", "success");
    if (window.bootstrap?.Modal && resetModal) {
      bootstrap.Modal.getOrCreateInstance(resetModal).hide();
    }
  } catch (error) {
    showToast(error?.message || "No se pudo restablecer la contrasena.", "info");
  }
};

const updateFullName = (row, values) => {
  const parts = [values.Nombre, values.SegundoNombre, values.Apellido, values.SegundoApellido]
    .map(normalizeText)
    .filter((value) => value.length > 0);
  const fullName = parts.join(" ");
  const display = row.querySelector('[data-field-display="NombreCompleto"]');
  if (display) {
    display.textContent = fullName || "-";
  }
};

const applyGrupoFilterTo = (editRow, field, checkSelector, equipoId) => {
  const grupoSelect = editRow.querySelector(`[data-field="${field}"]`);
  if (grupoSelect) {
    const options = Array.from(grupoSelect.options);
    options.forEach((option) => {
      const optionEquipo = option.dataset.equipoId || "";
      const allowed = !equipoId || optionEquipo === equipoId;
      option.hidden = !allowed;
      option.disabled = !allowed;
      if (!allowed) {
        option.selected = false;
      }
    });
  }
  editRow.querySelectorAll(checkSelector).forEach((input) => {
    const optionEquipo = input.closest("[data-equipo-id]")?.getAttribute("data-equipo-id") || "";
    const allowed = !equipoId || optionEquipo === equipoId;
    input.disabled = !allowed;
    const label = input.closest(".grupo-option");
    if (label) {
      label.classList.toggle("is-hidden", !allowed);
    }
    if (!allowed) {
      input.checked = false;
    }
  });
};

const applyGrupoFilter = (editRow, equipoId) => {
  if (!editRow) return;
  applyGrupoFilterTo(editRow, "GrupoIds", "[data-grupo-check]", equipoId);
  applyGrupoFilterTo(editRow, "GrupoIdsSecundarios", "[data-grupo-sec-check]", equipoId);
};

const updateEquipoDisplay = (row, equipoId, equipoName) => {
  row.dataset.equipoId = equipoId || "";
  const display = row.querySelector('[data-field-display="Equipo"]');
  if (display) {
    display.textContent = equipoName || "-";
  }
};

const updateGruposDisplay = (row, grupoNames, grupoIds) => {
  row.dataset.grupoIds = (grupoIds || []).join(",");
  const display = row.querySelector('[data-field-display="Grupos"]');
  if (!display) return;
  display.innerHTML = "";
  if (!grupoNames || grupoNames.length === 0) {
    const placeholder = document.createElement("span");
    placeholder.className = "text-muted";
    placeholder.textContent = "-";
    display.appendChild(placeholder);
    return;
  }
  grupoNames.forEach((name) => {
    const chip = document.createElement("span");
    chip.className = "grupo-chip";
    chip.textContent = name;
    display.appendChild(chip);
  });
};

const updateGruposSecundariosDisplay = (row, grupoNames, grupoIds) => {
  row.dataset.grupoSecIds = (grupoIds || []).join(",");
  const display = row.querySelector('[data-field-display="GruposSecundarios"]');
  if (!display) return;
  display.innerHTML = "";
  if (!grupoNames || grupoNames.length === 0) {
    const placeholder = document.createElement("span");
    placeholder.className = "text-muted";
    placeholder.textContent = "-";
    display.appendChild(placeholder);
    return;
  }
  grupoNames.forEach((name) => {
    const chip = document.createElement("span");
    chip.className = "grupo-chip";
    chip.textContent = name;
    display.appendChild(chip);
  });
};

const resetInputs = (row) => {
  const editRow = getEditRow(row);
  if (!editRow) return;
  editRow.querySelector('[data-field="Ultimatix"]').value = row.dataset.ultimatix || "";
  editRow.querySelector('[data-field="Nombre"]').value = row.dataset.nombre || "";
  editRow.querySelector('[data-field="SegundoNombre"]').value = row.dataset.segundonombre || "";
  editRow.querySelector('[data-field="Apellido"]').value = row.dataset.apellido || "";
  editRow.querySelector('[data-field="SegundoApellido"]').value = row.dataset.segundoapellido || "";
  const colorInput = editRow.querySelector('[data-field="ColorUsuario"]');
  if (colorInput) {
    colorInput.value = row.dataset.color || "#1f4d46";
  }
  const equipoId = row.dataset.equipoId || "";
  const equipoSelect = editRow.querySelector('[data-field="EquipoId"]');
  if (equipoSelect) {
    equipoSelect.value = equipoId;
  }
  applyGrupoFilter(editRow, equipoId);
  const grupoIds = (row.dataset.grupoIds || "").split(",").filter((value) => value);
  const primarySet = new Set(grupoIds);
  const grupoSelect = editRow.querySelector('[data-field="GrupoIds"]');
  if (grupoSelect) {
    Array.from(grupoSelect.options).forEach((option) => {
      option.selected = grupoIds.includes(option.value);
    });
  }
  const grupoSecIds = (row.dataset.grupoSecIds || "")
    .split(",")
    .filter((value) => value && !primarySet.has(value));
  const grupoSecSelect = editRow.querySelector('[data-field="GrupoIdsSecundarios"]');
  if (grupoSecSelect) {
    Array.from(grupoSecSelect.options).forEach((option) => {
      option.selected = grupoSecIds.includes(option.value);
    });
  }
  syncGrupoChecks(editRow);
  updateGrupoSummary(editRow);
  syncGrupoSecondaryChecks(editRow);
  updateGrupoSecondarySummary(editRow);
};

const updateInlineToggleState = (row, isOpen) => {
  const toggleButton = row.querySelector(".persona-inline-edit");
  if (!toggleButton) return;
  toggleButton.setAttribute("aria-expanded", isOpen ? "true" : "false");
  toggleButton.classList.toggle("is-open", isOpen);
};

const enterEdit = (row) => {
  resetInputs(row);
  const editRow = getEditRow(row);
  row.classList.add("is-editing");
  if (editRow) {
    editRow.classList.add("is-open");
  }
  updateInlineToggleState(row, true);
};

const exitEdit = (row) => {
  row.classList.remove("is-editing");
  const editRow = getEditRow(row);
  if (editRow) {
    editRow.classList.remove("is-open");
  }
  updateInlineToggleState(row, false);
};

const buildChanges = (row) => {
  const editRow = getEditRow(row);
  if (!editRow) {
    return { changes: {}, inputs: {} };
  }
  const changes = {};
  const original = {
    Ultimatix: normalizeText(row.dataset.ultimatix),
    Nombre: normalizeText(row.dataset.nombre),
    SegundoNombre: normalizeText(row.dataset.segundonombre),
    Apellido: normalizeText(row.dataset.apellido),
    SegundoApellido: normalizeText(row.dataset.segundoapellido),
    EquipoId: normalizeText(row.dataset.equipoId),
    ColorUsuario: normalizeText(row.dataset.color),
    GrupoIds: (row.dataset.grupoIds || "").split(",").filter((value) => value),
    GrupoIdsSecundarios: (row.dataset.grupoSecIds || "").split(",").filter((value) => value)
  };

  const grupoSelect = editRow.querySelector('[data-field="GrupoIds"]');
  const selectedGrupoIds = grupoSelect
    ? Array.from(grupoSelect.selectedOptions).map((option) => option.value)
    : [];
  const primarySet = new Set(selectedGrupoIds);
  const grupoSecSelect = editRow.querySelector('[data-field="GrupoIdsSecundarios"]');
  const selectedGrupoSecIdsRaw = grupoSecSelect
    ? Array.from(grupoSecSelect.selectedOptions).map((option) => option.value)
    : [];
  const selectedGrupoSecIds = selectedGrupoSecIdsRaw.filter((value) => !primarySet.has(value));

  const inputs = {
    Ultimatix: normalizeText(editRow.querySelector('[data-field="Ultimatix"]').value),
    Nombre: normalizeText(editRow.querySelector('[data-field="Nombre"]').value),
    SegundoNombre: normalizeText(editRow.querySelector('[data-field="SegundoNombre"]').value),
    Apellido: normalizeText(editRow.querySelector('[data-field="Apellido"]').value),
    SegundoApellido: normalizeText(editRow.querySelector('[data-field="SegundoApellido"]').value),
    EquipoId: normalizeText(editRow.querySelector('[data-field="EquipoId"]')?.value),
    ColorUsuario: normalizeText(editRow.querySelector('[data-field="ColorUsuario"]')?.value),
    GrupoIds: selectedGrupoIds,
    GrupoIdsSecundarios: selectedGrupoSecIds
  };

  Object.keys(inputs).forEach((key) => {
    if (key === "GrupoIds" || key === "GrupoIdsSecundarios") {
      const originalSet = original[key].slice().sort().join(",");
      const inputSet = inputs[key].slice().sort().join(",");
      if (originalSet !== inputSet) {
        changes[key] = inputs[key];
      }
      return;
    }
    if (inputs[key] !== original[key]) {
      changes[key] = inputs[key];
    }
  });

  return { changes, inputs };
};

const updateGrupoSummary = (editRow) => {
  if (!editRow) return;
  const summary = editRow.querySelector("[data-grupo-summary]");
  if (!summary) return;
  const checked = Array.from(editRow.querySelectorAll("[data-grupo-check]"))
    .filter((input) => input.checked && !input.disabled)
    .map((input) => input.nextElementSibling?.textContent?.trim())
    .filter((value) => value);
  if (checked.length === 0) {
    summary.textContent = "Seleccionar grupos";
    return;
  }
  if (checked.length <= 2) {
    summary.textContent = checked.join(", ");
    return;
  }
  summary.textContent = `${checked.length} grupos`;
};

const updateGrupoSecondarySummary = (editRow) => {
  if (!editRow) return;
  const summary = editRow.querySelector("[data-grupo-sec-summary]");
  if (!summary) return;
  const checked = Array.from(editRow.querySelectorAll("[data-grupo-sec-check]"))
    .filter((input) => input.checked && !input.disabled)
    .map((input) => input.nextElementSibling?.textContent?.trim())
    .filter((value) => value);
  if (checked.length === 0) {
    summary.textContent = "Seleccionar grupos";
    return;
  }
  if (checked.length <= 2) {
    summary.textContent = checked.join(", ");
    return;
  }
  summary.textContent = `${checked.length} grupos`;
};

const syncGrupoChecks = (editRow) => {
  if (!editRow) return;
  const grupoSelect = editRow.querySelector('[data-field="GrupoIds"]');
  if (!grupoSelect) return;
  const selected = new Set(
    Array.from(grupoSelect.selectedOptions).map((option) => option.value)
  );
  editRow.querySelectorAll("[data-grupo-check]").forEach((input) => {
    input.checked = selected.has(input.value);
  });
};

const syncGrupoSecondaryChecks = (editRow) => {
  if (!editRow) return;
  const grupoSelect = editRow.querySelector('[data-field="GrupoIdsSecundarios"]');
  if (!grupoSelect) return;
  const selected = new Set(
    Array.from(grupoSelect.selectedOptions).map((option) => option.value)
  );
  editRow.querySelectorAll("[data-grupo-sec-check]").forEach((input) => {
    input.checked = selected.has(input.value);
  });
};

const saveRow = async (row) => {
  const id = row.dataset.personaId;
  const { changes, inputs } = buildChanges(row);
  const changeKeys = Object.keys(changes);
  if (changeKeys.length === 0) {
    exitEdit(row);
    setStatus(row, "Sin cambios");
    return;
  }

  const formData = new URLSearchParams();
  formData.append("id", id);
  changeKeys.forEach((key) => {
    if (key === "GrupoIds") {
      const values = Array.isArray(changes[key]) ? changes[key] : [];
      if (values.length === 0) {
        formData.append("GrupoIds", "");
      } else {
        values.forEach((value) => formData.append("GrupoIds", value));
      }
      return;
    }
    if (key === "GrupoIdsSecundarios") {
      const values = Array.isArray(changes[key]) ? changes[key] : [];
      if (values.length === 0) {
        formData.append("GrupoIdsSecundarios", "");
      } else {
        values.forEach((value) => formData.append("GrupoIdsSecundarios", value));
      }
      return;
    }
    formData.append(key, changes[key]);
  });
  if (requestToken) {
    formData.append("__RequestVerificationToken", requestToken);
  }

  try {
    const response = await fetch(`/Persona/Patch?id=${encodeURIComponent(id)}`, {
      method: "POST",
      body: formData,
      credentials: "same-origin",
      headers: {
        "X-Requested-With": "XMLHttpRequest",
        "RequestVerificationToken": requestToken,
        "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"
      }
    });

    if (!response.ok) {
      throw new Error("Request failed");
    }

    row.dataset.ultimatix = inputs.Ultimatix;
    row.dataset.nombre = inputs.Nombre;
    row.dataset.segundonombre = inputs.SegundoNombre;
    row.dataset.apellido = inputs.Apellido;
    row.dataset.segundoapellido = inputs.SegundoApellido;
    row.dataset.color = inputs.ColorUsuario || "";
    row.dataset.equipoId = inputs.EquipoId;
    row.dataset.grupoIds = inputs.GrupoIds.join(",");
    row.dataset.grupoSecIds = inputs.GrupoIdsSecundarios.join(",");

    const ultimatixDisplay = row.querySelector('[data-field-display="Ultimatix"]');
    if (ultimatixDisplay) {
      ultimatixDisplay.textContent = inputs.Ultimatix;
    }

    updateFullName(row, inputs);
    const colorSwatch = row.querySelector('[data-field-display="ColorUsuario"]');
    if (colorSwatch) {
      colorSwatch.style.backgroundColor = inputs.ColorUsuario || "#e0e5e8";
    }
    const equipoSelect = getEditRow(row)?.querySelector('[data-field="EquipoId"]');
    const selectedEquipoName = equipoSelect?.selectedOptions[0]?.textContent?.trim() || "-";
    updateEquipoDisplay(row, inputs.EquipoId, selectedEquipoName);
    const grupoSelect = getEditRow(row)?.querySelector('[data-field="GrupoIds"]');
    const grupoNames = grupoSelect
      ? Array.from(grupoSelect.selectedOptions).map((option) => option.textContent?.trim()).filter((value) => value)
      : [];
    updateGruposDisplay(row, grupoNames, inputs.GrupoIds);
    const grupoSecSelect = getEditRow(row)?.querySelector('[data-field="GrupoIdsSecundarios"]');
    const grupoSecNames = grupoSecSelect
      ? Array.from(grupoSecSelect.selectedOptions).map((option) => option.textContent?.trim()).filter((value) => value)
      : [];
    updateGruposSecundariosDisplay(row, grupoSecNames, inputs.GrupoIdsSecundarios);

    exitEdit(row);
    showToast("Cambios guardados.", "success");
  } catch {
    setStatus(row, "Error");
  }
};

const getEditRow = (row) => {
  const id = row.dataset.personaId;
  if (!id) return null;
  return table?.querySelector(`[data-persona-edit-for="${id}"]`);
};

const parseJsonResponse = async (response) => {
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
};

const apiGetJson = async (url) => {
  const response = await fetch(url, { credentials: "same-origin" });
  const body = await parseJsonResponse(response);
  if (!response.ok) {
    throw new Error(body?.message || "No se pudo procesar la solicitud.");
  }
  return body;
};

const apiPostJson = async (url, payload) => {
  const response = await fetch(url, {
    method: "POST",
    credentials: "same-origin",
    headers: {
      "Content-Type": "application/json",
      "RequestVerificationToken": requestToken
    },
    body: JSON.stringify(payload)
  });
  const body = await parseJsonResponse(response);
  if (!response.ok) {
    throw new Error(body?.message || "No se pudo procesar la solicitud.");
  }
  return body;
};

const renderPersonaPermisosEmpty = (message) => {
  if (!permisosList) return;
  permisosList.innerHTML = `<div class="text-muted text-center py-4">${escapeHtml(message)}</div>`;
};

const resolvePermisoLabel = (state) => {
  if (state === "allow") return "Permitido";
  if (state === "deny") return "Denegado";
  return "Sin asignacion";
};

const setPermisoModuloOptions = () => {
  if (!permisosModulo) return;
  const current = permisosModulo.value || "";
  const modules = Array.from(new Set((permisosState.permisos || [])
    .map((item) => (item.modulo || "").trim())
    .filter((x) => x)))
    .sort((a, b) => a.localeCompare(b));

  permisosModulo.innerHTML = `<option value="">Todos</option>${modules
    .map((m) => `<option value="${escapeHtml(m)}">${escapeHtml(m)}</option>`)
    .join("")}`;

  permisosModulo.value = modules.includes(current) ? current : "";
  permisosState.filters.modulo = permisosModulo.value;
};

const getFilteredPermisos = () => {
  const search = (permisosState.filters.search || "").trim().toLowerCase();
  const modulo = permisosState.filters.modulo || "";
  const reglaPersonal = permisosState.filters.reglaPersonal || "";

  return (permisosState.permisos || []).filter((item) => {
    if (modulo && item.modulo !== modulo) return false;
    if (reglaPersonal && item.directAssignment !== reglaPersonal) return false;
    if (!search) return true;
    const haystack = `${item.codigoPermiso || ""} ${item.nombrePermiso || ""}`.toLowerCase();
    return haystack.includes(search);
  });
};

const renderPersonaPermisos = () => {
  if (!permisosList) return;

  if (!permisosState.userId) {
    renderPersonaPermisosEmpty("Selecciona una persona para gestionar permisos.");
    return;
  }

  const filtered = getFilteredPermisos();
  if (!filtered.length) {
    renderPersonaPermisosEmpty("No hay permisos para los filtros actuales.");
    return;
  }

  permisosList.innerHTML = filtered.map((item) => {
    const roleNames = Array.isArray(item.roleNames) ? item.roleNames : [];
    const roleChips = roleNames.length
      ? `<div class="persona-permiso-rolechips">${roleNames.map((roleName) => `<span class="persona-permiso-rolechip">${escapeHtml(roleName)}</span>`).join("")}</div>`
      : `<span class="text-muted small">Sin roles</span>`;

    return `
      <article class="persona-permiso-item" data-permiso-id="${escapeHtml(item.permisoAccesoId)}">
        <div class="persona-permiso-top">
          <div class="persona-permiso-name">${escapeHtml(item.nombrePermiso)}</div>
          <div class="persona-permiso-module">${escapeHtml(item.modulo || "-")}</div>
          ${roleChips}
        </div>
        <div class="persona-permiso-bottom">
          <div class="persona-permiso-statuses">
            <span class="persona-permiso-status" data-state="${escapeHtml(item.directAssignment)}">Regla personal: ${resolvePermisoLabel(item.directAssignment)}</span>
            <span class="persona-permiso-status" data-state="${escapeHtml(item.effectiveAssignment)}">Acceso actual: ${resolvePermisoLabel(item.effectiveAssignment)}</span>
          </div>
          <div class="persona-permiso-assignment">
            <select class="form-select form-select-sm persona-edit-input" data-permiso-direct-select>
              <option value="none" ${item.directAssignment === "none" ? "selected" : ""}>Sin asignacion</option>
              <option value="allow" ${item.directAssignment === "allow" ? "selected" : ""}>Permitido</option>
              <option value="deny" ${item.directAssignment === "deny" ? "selected" : ""}>Denegado</option>
            </select>
            <button type="button" class="btn btn-sm btn-primary persona-permiso-apply" data-permiso-apply>Aplicar</button>
          </div>
        </div>
      </article>
    `;
  }).join("");
};

const loadPersonaPermisos = async () => {
  if (!permisosState.userId) {
    permisosState.permisos = [];
    setPermisoModuloOptions();
    renderPersonaPermisos();
    return;
  }

  const list = await apiGetJson(`/PermisoAcceso/UserPermissions?userId=${encodeURIComponent(permisosState.userId)}`);
  permisosState.permisos = Array.isArray(list) ? list : [];
  setPermisoModuloOptions();
  renderPersonaPermisos();
};

const applyPersonaPermiso = async (permisoId, mode) => {
  if (!permisoId || !permisosState.userId) return;

  if (mode === "none") {
    await apiPostJson(`/PermisoAcceso/UnassignUser/${encodeURIComponent(permisoId)}`, {
      userId: permisosState.userId
    });
    return;
  }

  await apiPostJson(`/PermisoAcceso/AssignUser/${encodeURIComponent(permisoId)}`, {
    userId: permisosState.userId,
    esDenegado: mode === "deny"
  });
};

const syncPermisosFilters = () => {
  permisosState.filters.search = permisosSearch?.value || "";
  permisosState.filters.modulo = permisosModulo?.value || "";
  permisosState.filters.reglaPersonal = permisosReglaPersonal?.value || "";
  renderPersonaPermisos();
};

if (permisosModal) {
  permisosModal.addEventListener("show.bs.modal", async (event) => {
    const trigger = event.relatedTarget;
    if (!(trigger instanceof HTMLElement)) return;

    permisosState.userId = trigger.getAttribute("data-persona-user-id") || "";
    const personaNombre = trigger.getAttribute("data-persona-nombre") || "";
    if (permisosPersonaNombre) {
      permisosPersonaNombre.textContent = personaNombre || "-";
    }

    if (permisosSearch) permisosSearch.value = "";
    if (permisosModulo) permisosModulo.value = "";
    if (permisosReglaPersonal) permisosReglaPersonal.value = "";
    permisosState.filters = { search: "", modulo: "", reglaPersonal: "" };
    renderPersonaPermisosEmpty("Cargando permisos...");

    try {
      await loadPersonaPermisos();
    } catch (error) {
      renderPersonaPermisosEmpty("No se pudieron cargar los permisos del usuario.");
      showToast(error?.message || "No se pudieron cargar los permisos del usuario.", "danger");
    }
  });

  permisosModal.addEventListener("hidden.bs.modal", () => {
    permisosState.userId = "";
    permisosState.permisos = [];
    renderPersonaPermisosEmpty("Selecciona una persona para gestionar permisos.");
  });
}

if (permisosSearch) permisosSearch.addEventListener("input", syncPermisosFilters);
if (permisosModulo) permisosModulo.addEventListener("change", syncPermisosFilters);
if (permisosReglaPersonal) permisosReglaPersonal.addEventListener("change", syncPermisosFilters);
if (permisosClear) {
  permisosClear.addEventListener("click", () => {
    if (permisosSearch) permisosSearch.value = "";
    if (permisosModulo) permisosModulo.value = "";
    if (permisosReglaPersonal) permisosReglaPersonal.value = "";
    syncPermisosFilters();
  });
}

if (permisosList) {
  permisosList.addEventListener("click", async (event) => {
    const target = event.target instanceof Element ? event.target : null;
    if (!target) return;

    const applyButton = target.closest("[data-permiso-apply]");
    if (!applyButton) return;

    const card = applyButton.closest("[data-permiso-id]");
    if (!card) return;
    const permisoId = card.getAttribute("data-permiso-id") || "";
    const select = card.querySelector("[data-permiso-direct-select]");
    const mode = select instanceof HTMLSelectElement ? select.value : "none";

    try {
      await applyPersonaPermiso(permisoId, mode);
      showToast("Permiso actualizado correctamente.", "success");
      await loadPersonaPermisos();
    } catch (error) {
      showToast(error?.message || "No se pudo actualizar el permiso.", "danger");
    }
  });
}

if (table) {
  table.addEventListener("click", (event) => {
    const target = event.target instanceof Element ? event.target : null;
    if (!target) return;
    let row = target.closest(".persona-row");
    if (!row) {
      const editRow = target.closest(".persona-edit-row");
      const personaId = editRow?.getAttribute("data-persona-edit-for");
      if (personaId) {
        row = table.querySelector(`[data-persona-id="${personaId}"]`);
      }
    }
    if (!row) return;

    if (target.closest(".persona-inline-edit")) {
      event.preventDefault();
      if (row.classList.contains("is-editing")) {
        resetInputs(row);
        exitEdit(row);
        return;
      }
      enterEdit(row);
      return;
    }

    if (target.closest(".persona-inline-cancel")) {
      const { changes } = buildChanges(row);
      resetInputs(row);
      exitEdit(row);
      if (Object.keys(changes).length > 0) {
        showToast("Cambios descartados.", "info");
      }
      return;
    }

    if (target.closest(".persona-inline-save")) {
      saveRow(row);
    }
  });

  table.addEventListener("change", (event) => {
    const target = event.target instanceof HTMLElement ? event.target : null;
    if (!target) return;
    if (target.dataset.field === "EquipoId") {
      const editRow = target.closest(".persona-edit-row");
      const rowId = editRow?.getAttribute("data-persona-edit-for");
      if (!rowId) return;
      const row = table.querySelector(`[data-persona-id="${rowId}"]`);
      applyGrupoFilter(editRow, target.value);
      if (row && !target.value) {
        row.dataset.grupoIds = "";
        row.dataset.grupoSecIds = "";
      }
      syncGrupoChecks(editRow);
      updateGrupoSummary(editRow);
      syncGrupoSecondaryChecks(editRow);
      updateGrupoSecondarySummary(editRow);
      return;
    }
    if (target.matches("[data-grupo-check]")) {
      const editRow = target.closest(".persona-edit-row");
      if (!editRow) return;
      const grupoSelect = editRow.querySelector('[data-field="GrupoIds"]');
      if (!grupoSelect) return;
      Array.from(grupoSelect.options).forEach((option) => {
        if (option.value === target.value) {
          option.selected = target.checked;
        }
      });
      if (target.checked) {
        editRow.querySelectorAll("[data-grupo-sec-check]").forEach((input) => {
          if (input.value === target.value && input.checked) {
            input.checked = false;
            const secSelect = editRow.querySelector('[data-field="GrupoIdsSecundarios"]');
            if (secSelect) {
              Array.from(secSelect.options).forEach((option) => {
                if (option.value === input.value) {
                  option.selected = false;
                }
              });
            }
          }
        });
      }
      updateGrupoSummary(editRow);
      updateGrupoSecondarySummary(editRow);
    }
    if (target.matches("[data-grupo-sec-check]")) {
      const editRow = target.closest(".persona-edit-row");
      if (!editRow) return;
      const primaryChecked = Array.from(editRow.querySelectorAll("[data-grupo-check]"))
        .some((input) => input.value === target.value && input.checked);
      if (primaryChecked) {
        target.checked = false;
        const grupoSecSelect = editRow.querySelector('[data-field="GrupoIdsSecundarios"]');
        if (grupoSecSelect) {
          Array.from(grupoSecSelect.options).forEach((option) => {
            if (option.value === target.value) {
              option.selected = false;
            }
          });
        }
        updateGrupoSecondarySummary(editRow);
        return;
      }
      const grupoSecSelect = editRow.querySelector('[data-field="GrupoIdsSecundarios"]');
      if (!grupoSecSelect) return;
      Array.from(grupoSecSelect.options).forEach((option) => {
        if (option.value === target.value) {
          option.selected = target.checked;
        }
      });
      updateGrupoSecondarySummary(editRow);
    }
  });

  table.addEventListener("click", (event) => {
    const target = event.target instanceof Element ? event.target : null;
    if (!target) return;
    const toggle = target.closest("[data-grupo-toggle]");
    if (toggle) {
      const menu = toggle.closest(".grupo-multi")?.querySelector("[data-grupo-menu]");
      if (!menu) return;
      const isOpen = menu.classList.contains("is-open");
      document.querySelectorAll("[data-grupo-menu].is-open").forEach((openMenu) => {
        openMenu.classList.remove("is-open");
      });
      if (!isOpen) {
        menu.classList.add("is-open");
      }
      return;
    }
  });

  document.addEventListener("click", (event) => {
    const target = event.target instanceof Element ? event.target : null;
    if (!target) return;
    if (target.closest(".grupo-multi")) return;
    document.querySelectorAll("[data-grupo-menu].is-open").forEach((menu) => {
      menu.classList.remove("is-open");
    });
  });
}

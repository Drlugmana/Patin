(() => {
  const days = Array.from({ length: 9 }, (_, index) => ({
    id: `d${index}`,
    label: ""
  }));

  const monthNames = [
    "Enero",
    "Febrero",
    "Marzo",
    "Abril",
    "Mayo",
    "Junio",
    "Julio",
    "Agosto",
    "Septiembre",
    "Octubre",
    "Noviembre",
    "Diciembre"
  ];

  const dom = {
    board: document.querySelector(".calendar-board"),
    grid: document.querySelector("[data-calendar-grid]"),
    legend: document.querySelector("[data-legend]"),
    statusList: document.querySelector("[data-status-list]"),
    pendingBar: document.querySelector("[data-pending-bar]"),
    pendingCount: document.querySelector("[data-pending-count]"),
    pendingConfirmAllBtn: document.querySelector("[data-pending-confirm-all]"),
    pendingCancelAllBtn: document.querySelector("[data-pending-cancel-all]"),
    calamidadReplaceBar: document.querySelector("[data-calamidad-replace-bar]"),
    calamidadReplaceTitle: document.querySelector("[data-calamidad-replace-title]"),
    calamidadReplaceStatus: document.querySelector("[data-calamidad-replace-status]"),
    calamidadSaveReplacements: document.querySelector("[data-calamidad-save-replacements]"),
    calamidadConfirmNoReplacements: document.querySelector("[data-calamidad-confirm-no-replacements]"),
    calamidadCancelReplacements: document.querySelector("[data-calamidad-cancel-replacements]"),
    calamidadModeButtons: document.querySelectorAll("[data-calamidad-mode-btn]"),
    calamidadSourceTools: document.querySelector("[data-calamidad-source-tools]"),
    calamidadSourceSelect: document.querySelector("[data-calamidad-source-select]"),
    calamidadSourceAssignBtn: document.querySelector("[data-calamidad-source-assign]"),
    calamidadNewTools: document.querySelector("[data-calamidad-new-tools]"),
    calamidadNewPersonSelect: document.querySelector("[data-calamidad-new-person]"),
    calamidadNewAssignBtn: document.querySelector("[data-calamidad-new-assign]"),
    assignCount: document.querySelector("[data-assign-count]"),
    navRange: document.querySelector("[data-week-range]"),
    calendarShell: document.querySelector(".calendar-shell"),
    monthSelect: document.querySelector("[data-month-select]"),
    yearSelect: document.querySelector("[data-year-select]"),
    downloadBtn: document.querySelector("[data-download-calendar]"),
    filterMineToggle: document.querySelector("[data-filter-mine-toggle]"),
    filterAbsentToggle: document.querySelector("[data-filter-absent-toggle]"),
    filterPersonaSelect: document.querySelector("[data-filter-persona]"),
    filterGrupoSelect: document.querySelector("[data-filter-grupo]"),
    filterClearBtn: document.querySelector("[data-filter-clear]"),
    personaSelect: document.querySelector("#personaId"),
    grupoSelect: document.querySelector("#grupoId"),
    turnoSelect: document.querySelector("#tipoTurnoIds"),
    fechaInicioInput: document.querySelector("#fechaInicio"),
    fechaFinInput: document.querySelector("#fechaFin"),
    previewBtn: document.querySelector("[data-preview-btn]"),
    clearBtn: document.querySelector("[data-clear-btn]"),
    clearPreviewsBtn: document.querySelector("[data-clear-previews]"),
    savePreviewsBtn: document.querySelector("[data-save-previews]"),
    deleteCurrentWeekBtn: document.querySelector("[data-delete-current-week]"),
    deleteSelectedTurnosBtn: document.querySelector("[data-delete-selected-turnos]"),
    saveSplit: document.querySelector("[data-save-split]"),
    saveToggleBtn: document.querySelector("[data-save-toggle]"),
    saveMenu: document.querySelector("[data-save-menu]"),
    panel: document.querySelector("[data-panel]"),
    panelScrim: document.querySelector("[data-panel-scrim]"),
    panelOpen: document.querySelector("[data-panel-open]"),
    panelClose: document.querySelector("[data-panel-close]"),
    vacationPanel: document.querySelector("[data-vacation-panel]"),
    vacationScrim: document.querySelector("[data-vacation-scrim]"),
    vacationOpen: document.querySelector("[data-vacation-open]"),
    vacationClose: document.querySelector("[data-vacation-close]"),
    vacationYear: document.querySelector("[data-vacation-year]"),
    vacationCalendar: document.querySelector("[data-vacation-calendar]"),
    vacationSummary: document.querySelector("[data-vacation-summary]"),
    vacationApply: document.querySelector("[data-vacation-apply]"),
    vacationClear: document.querySelector("[data-vacation-clear]"),
    vacationReview: document.querySelector("[data-vacation-review]"),
    vacationReviewRange: document.querySelector("[data-vacation-review-range]"),
    vacationReviewDays: document.querySelector("[data-vacation-review-days]"),
    vacationSubmit: document.querySelector("[data-vacation-submit]"),
    permisoPanel: document.querySelector("[data-permiso-panel]"),
    permisoScrim: document.querySelector("[data-permiso-scrim]"),
    permisoOpen: document.querySelector("[data-permiso-open]"),
    permisoClose: document.querySelector("[data-permiso-close]"),
    permisoYear: document.querySelector("[data-permiso-year]"),
    permisoCalendar: document.querySelector("[data-permiso-calendar]"),
    permisoSummary: document.querySelector("[data-permiso-summary]"),
    permisoApply: document.querySelector("[data-permiso-apply]"),
    permisoClear: document.querySelector("[data-permiso-clear]"),
    permisoReview: document.querySelector("[data-permiso-review]"),
    permisoReviewDate: document.querySelector("[data-permiso-review-date]"),
    permisoReviewTime: document.querySelector("[data-permiso-review-time]"),
    permisoSubmit: document.querySelector("[data-permiso-submit]"),
    permisoInicio: document.querySelector("[data-permiso-inicio]"),
    permisoFin: document.querySelector("[data-permiso-fin]"),
    permisoMotivo: document.querySelector("[data-permiso-motivo]"),
    emptyState: document.querySelector("[data-calendar-empty]"),
    vistaAmpliaToggle: document.querySelector("[data-vista-amplia-toggle]"),
    gridAmplia: document.querySelector("[data-calendar-grid-amplia]"),
    legendPersonas: document.querySelector("[data-legend-personas]"),
    calamidadPersona: document.querySelector("[data-calamidad-persona]"),
    calamidadFechaInicio: document.querySelector("[data-calamidad-fecha-inicio]"),
    calamidadFechaFin: document.querySelector("[data-calamidad-fecha-fin]"),
    calamidadMotivo: document.querySelector("[data-calamidad-motivo]"),
    calamidadConfirm: document.querySelector("[data-calamidad-confirm]"),
    calamidadDecisionTitle: document.querySelector("[data-calamidad-decision-title]"),
    calamidadDecisionMessage: document.querySelector("[data-calamidad-decision-message]"),
    calamidadDecisionConfirm: document.querySelector("[data-calamidad-decision-confirm]"),
    calamidadDecisionDeny: document.querySelector("[data-calamidad-decision-deny]")
  };

  const normalizedRole = `${dom.calendarShell?.dataset.userRole || "Usuario"}`.toLowerCase();
  const isAdminLikeRole = normalizedRole === "admin" || normalizedRole === "superadmin";
  const isLiderRole = normalizedRole === "lider";
  const parseCapability = (value, fallbackValue = false) => {
    if (value === "true") return true;
    if (value === "false") return false;
    return fallbackValue;
  };

  const state = {
    shifts: [],
    assignments: [],
    absentAssignments: [],
    previewAssignments: [],
    weekOffset: 0,
    personaGroups: {},
    dayLabelsById: {},
    actionHistory: new Map(),
    lastActionByCard: new Map(),
    actionIdsByCard: new Map(),
    actionCounter: 0,
    previewCounter: 0,
    selectedPreviewCells: new Set(),
    selectedPersistedTurnos: new Set(),
    originalPositions: new Map(),
    currentPositions: new Map(),
    filterMyShifts: false,
    showAbsentPeople: false,
    planificacionPreviewActive: false,
    planificacionPreviewEquipoId: "",
    ignorePlanificacionPreviewOnce: false,
    filterPersonaId: "",
    filterGrupoId: "",
    filteredCount: null,
    holidaysByDay: {},
    vacationsByDay: {},
    calamitiesByDay: {},
    validationWeekCache: new Map(),
    vistaAmpliaActive: false,
    legendPersonaIds: new Set(),
    currentMonth: new Date().getMonth(),
    currentYear: new Date().getFullYear(),
    currentPersonaId: dom.calendarShell?.dataset.currentPersonaId || "",
    currentUserRole: dom.calendarShell?.dataset.userRole || "Usuario",
    canAdd: parseCapability(dom.calendarShell?.dataset.canAdd, isAdminLikeRole || isLiderRole),
    // If server capability flags are not present yet, use legacy role behavior as fallback.
    canEditAnyShift: parseCapability(dom.calendarShell?.dataset.canEditAnyShift, isAdminLikeRole),
    canDeleteShift: parseCapability(dom.calendarShell?.dataset.canDeleteShift, isAdminLikeRole || isLiderRole),
    canRequestCambioTurno: parseCapability(dom.calendarShell?.dataset.canRequestCambioTurno, isAdminLikeRole || isLiderRole),
    canRequestPermiso: parseCapability(dom.calendarShell?.dataset.canRequestPermiso, true),
    canCreateCalamidad: parseCapability(dom.calendarShell?.dataset.canCreateCalamidad, isAdminLikeRole || isLiderRole),
    dragGuardrailsShown: new Set(),
    cambioTipoSolicitudId: dom.calendarShell?.dataset.cambioTipoId || "",
    calamidadTipoSolicitudId: dom.calendarShell?.dataset.calamidadTipoId || ""
  };

  if (isAdminLikeRole) {
    state.canAdd = true;
    state.canEditAnyShift = true;
    state.canDeleteShift = true;
    state.canRequestCambioTurno = true;
    state.canCreateCalamidad = true;
  }

  const selectedEquipoId = dom.calendarShell?.dataset.equipoId || "";
  const gruposIds =
    dom.calendarShell?.dataset.gruposIds?.split(",").map((value) => value.trim()).filter(Boolean) || [];
  const storageKey = `weeklyCalendarState:${selectedEquipoId || "default"}`;

  const padNumber = (value) => value.toString().padStart(2, "0");

  const formatDate = (date) => {
    return `${date.getDate()} ${monthNames[date.getMonth()]} ${date.getFullYear()}`;
  };

  const getMondayForDate = (date) => {
    const dayIndex = (date.getDay() + 6) % 7;
    const monday = new Date(date);
    monday.setDate(date.getDate() - dayIndex);
    return monday;
  };

  const getWeeksBetween = (start, target) => {
    const diffMs = target.getTime() - start.getTime();
    return Math.round(diffMs / (7 * 24 * 60 * 60 * 1000));
  };

  const baseDate = getMondayForDate(new Date());

  const formatIsoDate = (date) => {
    return date.toISOString().split("T")[0];
  };

  const loadStoredPositions = () => {
    try {
      const raw = localStorage.getItem(storageKey);
      return raw ? JSON.parse(raw) : {};
    } catch {
      return {};
    }
  };

  const saveStoredPositions = (sourceMap) => {
    const payload = {};
    sourceMap.forEach((value, key) => {
      payload[key] = value;
    });
    try {
      localStorage.setItem(storageKey, JSON.stringify(payload));
    } catch {
      // Ignora errores de almacenamiento local
    }
  };

  const updateCurrentPosition = (card, cell) => {
    if (!card || !cell) return;
    const id = card.dataset.assignment;
    if (!id) return;
    state.currentPositions.set(id, {
      day: cell.dataset.day || "",
      shift: cell.dataset.shift || "",
      date: cell.dataset.date || ""
    });
  };

  const isAtOrigin = (card) => {
    const id = card?.dataset.assignment;
    if (!id) return false;
    const original = state.originalPositions.get(id);
    const current = state.currentPositions.get(id);
    if (!original || !current) return false;
    return original.day === current.day && original.shift === current.shift;
  };

  window.WeeklyCalendar = {
    days,
    monthNames,
    dom,
    state,
    selectedEquipoId,
    gruposIds,
    storageKey,
    utils: {
      padNumber,
      formatDate,
      getMondayForDate,
      getWeeksBetween,
      baseDate,
      formatIsoDate
    },
    storage: {
      loadStoredPositions,
      saveStoredPositions
    },
    helpers: {
      updateCurrentPosition,
      isAtOrigin
    }
  };
})();

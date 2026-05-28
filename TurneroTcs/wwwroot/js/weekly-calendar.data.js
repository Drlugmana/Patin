(() => {
  const api = window.WeeklyCalendar;
  if (!api) return;

  const { dom, state, days, utils, selectedEquipoId } = api;
  const { renderLegend, renderGrid, renderAssignments, updateEmptyState, resetCalendarState, getWeekDates } =
    api.render;

  const parseDayIndex = (dayId) => {
    const dayIndex = Number.parseInt(`${dayId || ""}`.replace("d", ""), 10);
    return Number.isNaN(dayIndex) ? -1 : dayIndex;
  };

  const getDateForAssignmentDay = (weekStartDate, dayId) => {
    if (!(weekStartDate instanceof Date) || Number.isNaN(weekStartDate.getTime())) {
      return null;
    }
    const dayIndex = parseDayIndex(dayId);
    if (dayIndex < 0) {
      return null;
    }

    const date = new Date(weekStartDate);
    date.setDate(date.getDate() + dayIndex);
    return date;
  };

  const normalizeValidationAssignments = (items, weekStartDate) => {
    if (!Array.isArray(items) || !(weekStartDate instanceof Date) || Number.isNaN(weekStartDate.getTime())) {
      return [];
    }

    return items.map((item) => {
      const date = getDateForAssignmentDay(weekStartDate, item?.day);
      return {
        ...item,
        absoluteDateIso: date ? utils.formatIsoDate(date) : ""
      };
    });
  };

  const cacheValidationWeekData = (weekStartDate, assignments, previewAssignments) => {
    if (!(weekStartDate instanceof Date) || Number.isNaN(weekStartDate.getTime())) {
      return;
    }

    const weekKey = utils.formatIsoDate(weekStartDate);
    state.validationWeekCache.set(weekKey, {
      assignments: normalizeValidationAssignments(assignments, weekStartDate),
      previewAssignments: normalizeValidationAssignments(previewAssignments, weekStartDate)
    });
  };

  const fetchWeekDataSnapshot = async (weekStartDate, { clearPreview = false } = {}) => {
    const query = new URLSearchParams({
      weekStart: utils.formatIsoDate(weekStartDate)
    });
    if (selectedEquipoId) {
      query.set("equipoId", selectedEquipoId);
    }
    if (clearPreview) {
      query.set("clearPreview", "true");
    }

    const response = await fetch(`/Calendario/WeekData?${query.toString()}`, {
      headers: { Accept: "application/json" }
    });
    if (!response.ok) {
      throw new Error("Bad response");
    }

    return response.json();
  };

  const prefetchValidationWeekData = async (weekStartDate) => {
    if (!(weekStartDate instanceof Date) || Number.isNaN(weekStartDate.getTime())) {
      return;
    }

    try {
      const data = await fetchWeekDataSnapshot(weekStartDate);
      cacheValidationWeekData(
        weekStartDate,
        Array.isArray(data?.assignments) ? data.assignments : [],
        Array.isArray(data?.previewAssignments) ? data.previewAssignments : []
      );
    } catch {
      // Background validation prefetch is best-effort only.
    }
  };

  const getPreviewDebugRows = () => {
    const weekDates = getWeekDates(state.weekOffset);
    return (Array.isArray(state.previewAssignments) ? state.previewAssignments : [])
      .filter((item) => item && item.preview === true)
      .map((item) => {
        const dayId = typeof item.day === "string" ? item.day : "";
        const dayIndex = dayId.startsWith("d") ? Number.parseInt(dayId.slice(1), 10) : -1;
        const date = dayIndex >= 0 && dayIndex < weekDates.length
          ? utils.formatIsoDate(weekDates[dayIndex])
          : "";
        return {
          id: item.id || "",
          fecha: date,
          day: dayId,
          shift: item.shift || "",
          personaId: item.personaId || "",
          nombre: item.title || "",
          ultimatix: item.meta || "",
          grupoId: item.grupoId || "",
          grupo: item.group || "",
          color: item.color || ""
        };
      });
  };

  const logGeneratedTurnos = (source = "manual") => {
    const rows = getPreviewDebugRows();
    console.groupCollapsed(
      `[Calendar Debug] Turnos generados (${source}) - ${rows.length} registros`
    );
    if (rows.length > 0 && typeof console.table === "function") {
      console.table(rows);
    } else {
      console.log(rows);
    }
    console.groupEnd();
    return rows;
  };

  const getSelectedTurnos = () => {
    if (!dom.turnoSelect) return [];
    return Array.from(dom.turnoSelect.selectedOptions).map((option) => option.value);
  };

  const buildPreviewAssignments = () => {
    if (!dom.personaSelect || !dom.fechaInicioInput || !dom.fechaFinInput) {
      return [];
    }
    const personaId = dom.personaSelect.value;
    const selectedGrupo = dom.grupoSelect?.selectedOptions[0];
    const personaColor = dom.personaSelect.selectedOptions[0]?.dataset?.color || "";
    const grupoName = selectedGrupo?.dataset?.name || "";
    const grupoId = selectedGrupo?.value || "";
    const turnoIds = getSelectedTurnos();
    const startValue = dom.fechaInicioInput.value;
    const endValue = dom.fechaFinInput.value;
    if (!personaId || turnoIds.length === 0 || !startValue || !endValue) {
      return [];
    }

    const startDate = new Date(`${startValue}T00:00:00`);
    const endDate = new Date(`${endValue}T00:00:00`);
    if (Number.isNaN(startDate.getTime()) || Number.isNaN(endDate.getTime())) {
      return [];
    }
    if (endDate < startDate) {
      return [];
    }

    const personaName =
      dom.personaSelect.selectedOptions[0]?.textContent?.trim() || "Persona";
    const shiftIdSet = new Set(state.shifts.map((shift) => shift.id));
    const weekDates = getWeekDates(state.weekOffset);
    const weekMap = new Map(
      weekDates.map((date, index) => [utils.formatIsoDate(date), days[index].id])
    );

    const preview = [];
    let rotationIndex = 0;
    for (let date = new Date(startDate); date <= endDate; date.setDate(date.getDate() + 1)) {
      const turnoId = turnoIds[rotationIndex % turnoIds.length];
      rotationIndex += 1;
      const isoDate = utils.formatIsoDate(date);
      const dayKey = weekMap.get(isoDate);
      if (!dayKey || !shiftIdSet.has(turnoId)) {
        continue;
      }
      state.previewCounter += 1;
      preview.push({
        id: `preview-${isoDate}-${turnoId}-${personaId}-${state.previewCounter}`,
        title: personaName,
        meta: "Previsualizacion",
        day: dayKey,
        shift: turnoId,
        preview: true,
        color: personaColor || undefined,
        group: grupoName || undefined,
        personaId: personaId,
        grupoId: grupoId || undefined
      });
    }

    return preview;
  };

  const updateSaveVisibility = () => {
    if (!dom.saveSplit) return;
    const visible = state.planificacionPreviewActive || state.previewAssignments.length > 0;
    if (dom.clearPreviewsBtn) {
      dom.clearPreviewsBtn.hidden = !visible;
    }
    if (dom.saveMenu) {
      dom.saveMenu.hidden = !visible;
      if (!visible) {
        dom.saveMenu.classList.remove("show");
      }
    }
    if (dom.saveToggleBtn) {
      dom.saveToggleBtn.setAttribute("aria-expanded", "false");
    }
    if (dom.savePreviewsBtn) {
      dom.savePreviewsBtn.hidden = !visible;
    }
    if (dom.saveSplit) {
      dom.saveSplit.hidden = !visible;
    }
  };

  const applyPreview = ({ append = false } = {}) => {
    const nextPreview = buildPreviewAssignments();
    if (!append) {
      state.previewAssignments = nextPreview;
    } else {
      const merged = new Map(state.previewAssignments.map((item) => [item.id, item]));
      nextPreview.forEach((item) => {
        merged.set(item.id, item);
      });
      state.previewAssignments = Array.from(merged.values());
    }
    api.interactions?.capturePendingChangesBeforeRender?.();
    renderLegend();
    renderGrid();
    renderAssignments();
    updateEmptyState();
    updateSaveVisibility();
  };

  const loadWeekData = async ({ skipPreview = false, clearPreview = false } = {}) => {
    api.interactions?.capturePendingChangesBeforeRender?.();

    if (clearPreview) {
      state.planificacionPreviewActive = false;
      state.planificacionPreviewEquipoId = "";
      state.previewAssignments = [];
      state.validationWeekCache.clear();
    }

    const weekStart = getWeekDates(state.weekOffset)[0];

    try {
      const data = await fetchWeekDataSnapshot(weekStart, { clearPreview });
      state.shifts = data.shifts || [];
      state.assignments = data.assignments || [];
      state.absentAssignments = Array.isArray(data.absentAssignments) ? data.absentAssignments : [];
      state.personaGroups = data.personaGroups || {};
      const weekDates = getWeekDates(state.weekOffset);
      const weekMap = new Map(
        weekDates.map((date, index) => [utils.formatIsoDate(date), days[index].id])
      );
      const holidayByDay = {};
      const holidays = Array.isArray(data.holidays) ? data.holidays : [];
      holidays.forEach((item) => {
        const dayId = weekMap.get(item?.date || "");
        if (!dayId) return;
        const names = Array.isArray(item?.names)
          ? item.names.filter((value) => typeof value === "string" && value.trim().length > 0)
          : [];
        if (names.length === 0) return;
        const existing = holidayByDay[dayId] || [];
        const merged = new Set(existing.concat(names));
        holidayByDay[dayId] = Array.from(merged);
      });

      const vacationByDay = {};
      const vacations = Array.isArray(data.vacations) ? data.vacations : [];
      vacations.forEach((item) => {
        const dayId = weekMap.get(item?.date || "");
        if (!dayId) return;
        const names = Array.isArray(item?.people)
          ? item.people.filter((value) => typeof value === "string" && value.trim().length > 0)
          : [];
        if (names.length === 0) return;
        const existing = vacationByDay[dayId] || [];
        const merged = new Set(existing.concat(names));
        vacationByDay[dayId] = Array.from(merged);
      });

      const calamityByDay = {};
      const calamities = Array.isArray(data.calamities) ? data.calamities : [];
      calamities.forEach((item) => {
        const dayId = weekMap.get(item?.date || "");
        if (!dayId) return;
        const names = Array.isArray(item?.people)
          ? item.people.filter((value) => typeof value === "string" && value.trim().length > 0)
          : [];
        if (names.length === 0) return;
        const existing = calamityByDay[dayId] || [];
        const merged = new Set(existing.concat(names));
        calamityByDay[dayId] = Array.from(merged);
      });

      if (clearPreview || state.ignorePlanificacionPreviewOnce) {
        state.planificacionPreviewActive = false;
        state.planificacionPreviewEquipoId = "";
        state.previewAssignments = skipPreview ? [] : buildPreviewAssignments();
        state.ignorePlanificacionPreviewOnce = false;
      } else {
        state.planificacionPreviewActive = data.previewActive === true;
        state.planificacionPreviewEquipoId = data.previewEquipoId || "";

        const serverPreview = Array.isArray(data.previewAssignments)
          ? data.previewAssignments
          : [];
        const localPreview = skipPreview ? [] : buildPreviewAssignments();
        if (localPreview.length === 0) {
          state.previewAssignments = serverPreview;
        } else {
          const merged = new Map(serverPreview.map((item) => [item.id, item]));
          localPreview.forEach((item) => {
            merged.set(item.id, item);
          });
          state.previewAssignments = Array.from(merged.values());
        }
      }
      cacheValidationWeekData(weekStart, state.assignments, state.previewAssignments);
      const previousWeekStart = new Date(weekStart);
      previousWeekStart.setDate(previousWeekStart.getDate() - 7);
      void prefetchValidationWeekData(previousWeekStart);
      resetCalendarState();
      state.holidaysByDay = holidayByDay;
      state.vacationsByDay = vacationByDay;
      state.calamitiesByDay = calamityByDay;
      if (typeof api.interactions?.syncSelectedPersistedTurnos === "function") {
        api.interactions.syncSelectedPersistedTurnos();
      }
      renderLegend();
      renderGrid();
      renderAssignments();
      updateEmptyState();
      updateSaveVisibility();
      if (data.previewActive === true && state.previewAssignments.length > 0) {
        logGeneratedTurnos("week-data");
      }
      if (state.vistaAmpliaActive && api.render.renderVistaAmplia) {
        await api.render.renderVistaAmplia();
      }
    } catch {
      if (clearPreview) {
        state.planificacionPreviewActive = false;
        state.planificacionPreviewEquipoId = "";
        state.previewAssignments = [];
        state.validationWeekCache.clear();
      }
      state.holidaysByDay = {};
      state.vacationsByDay = {};
      state.calamitiesByDay = {};
      state.absentAssignments = [];
      if (state.selectedPersistedTurnos instanceof Set) {
        state.selectedPersistedTurnos.clear();
      }
      if (typeof api.interactions?.syncSelectedPersistedTurnos === "function") {
        api.interactions.syncSelectedPersistedTurnos();
      }
      renderLegend();
      renderGrid();
      renderAssignments();
      updateEmptyState();
      updateSaveVisibility();
      if (state.vistaAmpliaActive && api.render.renderVistaAmplia) {
        await api.render.renderVistaAmplia();
      }
    }
  };

  api.data = {
    applyPreview,
    updateSaveVisibility,
    loadWeekData,
    buildPreviewAssignments,
    logGeneratedTurnos
  };

  api.debug = {
    ...(api.debug || {}),
    logGeneratedTurnos
  };
})();

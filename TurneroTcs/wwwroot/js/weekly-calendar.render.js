(() => {
  const api = window.WeeklyCalendar;
  if (!api) return;

  const { dom, state, days, monthNames, utils, storage, helpers } = api;
  let vistaAmpliaTooltipEl = null;
  let currentTooltipTarget = null;
  const tooltipBoundRoots = new WeakSet();

  const shouldUseDarkText = (color) => {
    if (!color) return false;
    const value = `${color}`.trim();
    if (!value.startsWith("#")) return false;
    const hex = value.replace("#", "");
    if (hex.length !== 6) return false;
    const r = parseInt(hex.slice(0, 2), 16);
    const g = parseInt(hex.slice(2, 4), 16);
    const b = parseInt(hex.slice(4, 6), 16);
    if (Number.isNaN(r) || Number.isNaN(g) || Number.isNaN(b)) return false;
    const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
    return luminance > 0.62;
  };
  
  const renderLegend = () => {
    if (!dom.legend) return;
    dom.legend.innerHTML = state.shifts
      .map(
        (shift) =>
          `<span class="legend-chip">${shift.shortLabel ?? shift.short} - ${shift.label}</span>`
      )
      .join("");
  };

  const renderWeekRange = (weekDates) => {
    if (!dom.navRange) return;
    const start = weekDates[0];
    const end = weekDates[weekDates.length - 1];
    dom.navRange.textContent = `${utils.formatDate(start)} - ${utils.formatDate(end)}`;
  };

  const getWeekDates = (offset) => {
    const start = new Date(utils.baseDate);
    start.setDate(start.getDate() + offset * 7);
    return days.map((_, index) => {
      const dayDate = new Date(start);
      dayDate.setDate(start.getDate() + index);
      return dayDate;
    });
  };

  const weekdayLabels = [
    "Domingo",
    "Lunes",
    "Martes",
    "Miercoles",
    "Jueves",
    "Viernes",
    "Sabado"
  ];

  const getCambioVisualConfig = (stateValue, customLabel) => {
    const normalized = `${stateValue || ""}`.trim().toLowerCase();
    if (!normalized) return null;

    if (normalized === "requested" || normalized === "pendiente") {
      return {
        key: "requested",
        badgeText: "Solicitado",
        ariaLabel: customLabel || "Cambio solicitado"
      };
    }

    if (
      normalized === "inreview" ||
      normalized === "en_aprobacion" ||
      normalized === "aprobado_lider"
    ) {
      return {
        key: "inreview",
        badgeText: "1 aprobado",
        ariaLabel: customLabel || "Cambio con una aprobacion"
      };
    }

    if (
      normalized === "approved" ||
      normalized === "aprobado" ||
      normalized === "aprobado_final"
    ) {
      return {
        key: "approved",
        badgeText: "Aprobado",
        ariaLabel: customLabel || "Cambio aprobado"
      };
    }

    if (normalized === "rejected" || normalized === "rechazado") {
      return {
        key: "rejected",
        badgeText: "Rechazado",
        ariaLabel: customLabel || "Cambio rechazado"
      };
    }

    if (normalized === "cancelled" || normalized === "cancelado") {
      return {
        key: "cancelled",
        badgeText: "Cancelado",
        ariaLabel: customLabel || "Cambio cancelado"
      };
    }

    return null;
  };

  const isPendingCambioState = (stateKey) =>
    stateKey === "requested" || stateKey === "inreview" || stateKey === "rejected" || stateKey === "cancelled";

  const getCambioHiddenClasses = (assignment, cambioVisual, assignmentLookup) => {
    if (!cambioVisual) return "";
    const role = `${assignment?.cambioRole || ""}`.toLowerCase();
    if (role === "destino" && isPendingCambioState(cambioVisual.key)) {
      const relatedId = `${assignment?.cambioRelatedTurnoId || ""}`.trim();
      const relatedAssignment = relatedId ? assignmentLookup.get(relatedId) : null;
      const isSamePersonaMove =
        !relatedAssignment ||
        `${relatedAssignment.personaId || ""}`.trim() === `${assignment?.personaId || ""}`.trim();

      if (isSamePersonaMove) {
        return " is-cambio-pending-hidden is-cambio-hidden";
      }
    }

    if (role === "origen" && cambioVisual.key === "approved") {
      return " is-cambio-approved-hidden is-cambio-hidden";
    }

    return "";
  };

  const renderGrid = () => {
    if (!dom.grid) return;

    const weekDates = getWeekDates(state.weekOffset);
    renderWeekRange(weekDates);

    const headerCells = days
      .map((day, index) => {
        const dateLabel = weekDates[index];
        const dayLabel = weekdayLabels[dateLabel.getDay()];
        const isNextWeekStart = index === 7;
        const holidayNames = Array.isArray(state.holidaysByDay?.[day.id])
          ? state.holidaysByDay[day.id]
          : [];
        const vacationNames = Array.isArray(state.vacationsByDay?.[day.id])
          ? state.vacationsByDay[day.id]
          : [];
        const calamityNames = Array.isArray(state.calamitiesByDay?.[day.id])
          ? state.calamitiesByDay[day.id]
          : [];
        const isHoliday = holidayNames.length > 0;
        const hasVacation = vacationNames.length > 0;
        const hasCalamity = calamityNames.length > 0;
        const hasAbsences = hasVacation || hasCalamity;
        const holidayTitle = isHoliday
          ? ` title="${escapeHtmlAttribute(`Feriado: ${holidayNames.join(", ")}`)}"`
          : "";
        const holidayBadge = isHoliday ? `<small class="holiday-badge">Feriado</small>` : "";
        const vacationTooltip = (() => {
          if (!hasAbsences) return "";

          const formatSection = (label, names) => {
            if (!Array.isArray(names) || names.length === 0) return "";
            const maxNames = 6;
            const visibleNames = names.slice(0, maxNames).join(", ");
            const hidden = names.length - maxNames;
            const suffix = hidden > 0 ? `, +${hidden} mas` : "";
            return `${label} (${names.length}): ${visibleNames}${suffix}`;
          };

          const sections = [
            formatSection("Vacaciones", vacationNames),
            formatSection("Calamidad", calamityNames)
          ].filter((value) => value.length > 0);

          return escapeHtmlAttribute(sections.join(" | "));
        })();
        const absenceCount = new Set(vacationNames.concat(calamityNames)).size;
        const vacationBadge = hasAbsences
          ? `<small class="vacation-badge" data-tooltip="${vacationTooltip}" aria-label="${vacationTooltip}">Ausencias ${absenceCount}</small>`
          : "";
        const weekBreakClass = isNextWeekStart ? " is-next-week-start" : "";
        state.dayLabelsById[day.id] = dayLabel;
        return `
        <div class="grid-header${isHoliday ? " is-holiday" : ""}${hasAbsences ? " has-vacations" : ""}${weekBreakClass}"${holidayTitle}>
          ${dayLabel}
          <span>${dateLabel.getDate()} ${monthNames[dateLabel.getMonth()]}</span>
          ${holidayBadge}
          ${vacationBadge}
        </div>
      `;
      })
      .join("");

    let rowsHtml = `
    <div class="grid-header">Turno</div>
    ${headerCells}
  `;

    state.shifts.forEach((shift) => {
      rowsHtml += `
    <div class="shift-label">
      ${shift.shortLabel ?? shift.short}
      <span>${shift.label}</span>
    </div>
    `;

      days.forEach((day, index) => {
        const key = `${day.id}:${shift.id}`;
        const selectedClass = state.selectedPreviewCells?.has(key) ? " is-selected" : "";
        const hasHoliday = Array.isArray(state.holidaysByDay?.[day.id]) &&
          state.holidaysByDay[day.id].length > 0;
        const weekBreakClass = index === 7 ? " is-next-week-start" : "";
        rowsHtml += `
        <div class="shift-cell${selectedClass}${hasHoliday ? " is-holiday-column" : ""}${weekBreakClass}" data-day="${day.id}" data-shift="${shift.id}"></div>
      `;
      }); 
    });

    dom.grid.innerHTML = rowsHtml;
  };

  const renderAssignments = () => {
    if (!dom.grid) return;

    const storedPositions = storage.loadStoredPositions();
    const absentAssignments = state.showAbsentPeople
      ? Array.isArray(state.absentAssignments)
        ? state.absentAssignments
        : []
      : [];
    const allAssignments = state.assignments
      .concat(state.previewAssignments)
      .concat(absentAssignments);
    const filteredAssignments = allAssignments;
    const assignmentLookup = new Map(
      filteredAssignments
        .filter((assignment) => assignment?.id)
        .map((assignment) => [assignment.id, assignment])
    );
    const cellsTouched = new Set();
    filteredAssignments.forEach((assignment) => {
      const isPreview = assignment.preview === true;
      const isAbsent = assignment.calamidadAusente === true;
      const reemplazoNombre = `${assignment.calamidadReemplazaNombre || ""}`.trim();
      const isCalamidadReplacement = reemplazoNombre.length > 0;
      const role = `${state.currentUserRole || ""}`.toLowerCase();
      const isAdminLike = role === "admin" || role === "superadmin";
      const isLider = role === "lider";
      if (!isPreview && !isAbsent && !state.originalPositions.has(assignment.id)) {
        const stored = storedPositions[assignment.id];
        state.originalPositions.set(assignment.id, {
          day: stored?.day || assignment.day,
          shift: stored?.shift || assignment.shift
        });
      }

      const card = document.createElement("div");
      card.className = "shift-card";
      card.classList.add(`shift-type-${assignment.shift}`);
      if (isCalamidadReplacement) {
        card.classList.add("is-calamidad-replacement");
        card.dataset.calamidadReplacement = "true";
      }
      if (isPreview) {
        card.classList.add("is-preview");
      } else if (isAbsent) {
        card.classList.add("is-calamidad-absent", "is-locked");
        card.dataset.calamidadAusente = "true";
        card.setAttribute("draggable", "false");
        card.setAttribute("aria-disabled", "true");
        card.style.cursor = "default";
      } else {
        const canEditAnyShift = state.canEditAnyShift === true || isAdminLike;
        const canRequestCambioTurno =
          state.canRequestCambioTurno === true || isAdminLike || isLider;
        const canDrag =
          !isCalamidadReplacement &&
          (canEditAnyShift ||
          (canRequestCambioTurno && assignment.personaId === state.currentPersonaId));
        if (canDrag) {
          card.setAttribute("draggable", "true");
          card.classList.remove("is-locked");
          card.removeAttribute("aria-disabled");
          card.style.cursor = "";
        } else {
          card.setAttribute("draggable", "false");
          card.classList.add("is-locked");
          card.setAttribute("aria-disabled", "true");
          card.style.cursor = "not-allowed";
        }
      }
      const cambioVisual = !isPreview
        ? getCambioVisualConfig(assignment.cambioState, assignment.cambioLabel)
        : null;
      if (cambioVisual) {
        card.classList.add("has-cambio", `cambio-state-${cambioVisual.key}`);
        card.dataset.cambioState = cambioVisual.key;
        card.dataset.cambioLabel = cambioVisual.ariaLabel;
      }
      if (assignment.cambioRelatedTurnoId) {
        card.dataset.cambioRelatedAssignment = assignment.cambioRelatedTurnoId;
      }
      if (assignment.cambioRole) {
        const role = `${assignment.cambioRole}`.toLowerCase();
        card.dataset.cambioRole = role;
        card.classList.add(`cambio-role-${role}`);
        const hiddenClasses = getCambioHiddenClasses(assignment, cambioVisual, assignmentLookup);
        if (hiddenClasses) {
          hiddenClasses
            .trim()
            .split(/\s+/)
            .forEach((cls) => card.classList.add(cls));
        }
      }
      if (assignment.personaId) {
        card.dataset.personaId = assignment.personaId;
      }
      if (assignment.grupoId) {
        card.dataset.grupoId = assignment.grupoId;
      }
      if (assignment.esTurnoExtra === true) {
        card.dataset.esTurnoExtra = "true";
      } else {
        card.dataset.esTurnoExtra = "false";
      }
      if (state.filterMyShifts && state.currentPersonaId) {
        if (assignment.personaId === state.currentPersonaId) {
          card.classList.add("is-highlighted-user");
        } else {
          card.classList.add("is-dimmed");
        }
      }
      if (isAbsent && assignment.color) {
        card.style.backgroundColor = assignment.color;
        card.style.color = shouldUseDarkText(assignment.color) ? "#1e252b" : "#fff";
      } else if (!isPreview && assignment.color) {
        card.style.backgroundColor = assignment.color;
        card.style.color = shouldUseDarkText(assignment.color) ? "#1e252b" : "#fff";
      }
      card.dataset.assignment = assignment.id;
      const rawName = `${assignment.title ?? ""}`.trim();
      const displayName = (() => {
        if (!rawName) return "";
        const parts = rawName.split(/\s+/);
        if (parts.length >= 3) return `${parts[0]} ${parts[2]}`;
        if (parts.length === 2) return `${parts[0]} ${parts[1]}`;
        return parts[0];
      })();
      const tooltipParts = [];
      const cambioTooltip = `${cambioVisual?.ariaLabel || ""}`.trim();
      if (cambioTooltip) {
        tooltipParts.push(cambioTooltip);
      }
      if (isAbsent) {
        tooltipParts.push("Ausente por calamidad");
        const reemplazadoPorNombre = `${assignment.calamidadReemplazadoPorNombre || ""}`.trim();
        if (reemplazadoPorNombre) {
          tooltipParts.push(`Reemplazado por ${reemplazadoPorNombre}`);
        }
      }
      if (reemplazoNombre) {
        tooltipParts.push(`Reemplaza a ${reemplazoNombre}`);
      }
      const tooltipText = tooltipParts.join(" | ").trim();
      if (tooltipText) {
        card.setAttribute("aria-label", tooltipText);
        card.dataset.tooltip = tooltipText;
      }
      const canDeletePersisted =
        !isPreview &&
        !isAbsent &&
        (state.canDeleteShift === true || isAdminLike || isLider);
      if (canDeletePersisted) {
        card.classList.add("has-delete");
      }
      if (!isPreview && !isAbsent && state.selectedPersistedTurnos?.has(assignment.id)) {
        card.classList.add("is-selected");
      }
      const cambioBadge = cambioVisual
        ? `<span class="card-status card-status-cambio card-status-cambio-${cambioVisual.key}">${cambioVisual.badgeText}</span>`
        : "";
      const extraBadge = assignment.esTurnoExtra === true
        ? `<span class="card-extra-badge">EXTRA</span>`
        : "";
      card.innerHTML = `
      ${cambioBadge}
      <div class="card-title">${displayName}</div>
      ${extraBadge}
      ${assignment.group ? `<span class="card-group">${assignment.group}</span>` : ""}
      ${isPreview ? `<button type="button" class="preview-remove" aria-label="Quitar previsualizacion">&times;</button>` : ""}
      ${canDeletePersisted ? `<button type="button" class="saved-remove" aria-label="Eliminar turno">&times;</button>` : ""}
    `;

      const stored = !isPreview ? storedPositions[assignment.id] : null;
      const targetDay = stored?.day || assignment.day;
      const targetShift = stored?.shift || assignment.shift;
      if (targetDay) {
        card.dataset.day = targetDay;
      }
      if (targetShift) {
        card.dataset.shift = targetShift;
      }
      let cell = dom.grid.querySelector(
        `[data-day="${targetDay}"][data-shift="${targetShift}"]`
      );
      if (!cell && !isPreview) {
        cell = dom.grid.querySelector(
          `[data-day="${assignment.day}"][data-shift="${assignment.shift}"]`
        );
      }
      if (!cell) return;

      cell.appendChild(card);
      cellsTouched.add(cell);
      if (!isPreview && !isAbsent) {
        helpers.updateCurrentPosition(card, cell);
      }
    });

    cellsTouched.forEach((cell) => {
      const count = cell.querySelectorAll(".shift-card:not(.is-cambio-hidden)").length;
      let size = 0.66;
      if (count <= 1) size = 0.74;
      else if (count === 2) size = 0.7;
      else if (count === 3) size = 0.66;
      else if (count === 4) size = 0.62;
      else size = 0.58;
      cell.style.setProperty("--card-font-size", `${size}rem`);
    });

    const role = `${state.currentUserRole || ""}`.toLowerCase();
    const isAdminLike = role === "admin" || role === "superadmin";
    const isLider = role === "lider";
    const canAdd = state.canAdd === true || isAdminLike || isLider;
    dom.grid.querySelectorAll(".shift-cell").forEach((cell) => {
      if (cell.querySelector(".shift-card")) {
        cell.classList.add("has-card");
        if (canAdd && !cell.querySelector(".cell-add-btn")) {
          const addBtn = document.createElement("button");
          addBtn.type = "button";
          addBtn.className = "cell-add-btn";
          addBtn.setAttribute("aria-label", "Agregar turno");
          addBtn.textContent = "+";
          cell.appendChild(addBtn);
        }
        if (!canAdd) {
          const existing = cell.querySelector(".cell-add-btn");
          if (existing) {
            existing.remove();
          }
        }
      }
    });

    if (dom.assignCount) {
      dom.assignCount.textContent = state.assignments.length.toString();
    }

    bindCardTooltip(dom.grid);
    if (typeof api.interactions?.onAssignmentsRendered === "function") {
      api.interactions.onAssignmentsRendered();
    }
    applyAssignmentFilters();
  };

  const applyAssignmentFilters = () => {
    const grids = [dom.grid, dom.gridAmplia].filter(Boolean);
    if (grids.length === 0) return;
    const personaId = state.filterPersonaId || "";
    const grupoId = state.filterGrupoId || "";
    const legendPersonaIds = state.legendPersonaIds instanceof Set ? state.legendPersonaIds : null;
    const hasLegendFilter = state.vistaAmpliaActive && legendPersonaIds && legendPersonaIds.size > 0;
    let visibleCount = 0;
    grids.forEach((grid) => {
      grid.querySelectorAll(".shift-card").forEach((card) => {
        const matchesPersona = !personaId || card.dataset.personaId === personaId;
        const matchesGrupo = !grupoId || card.dataset.grupoId === grupoId;
        const matchesLegend = !hasLegendFilter || legendPersonaIds.has(card.dataset.personaId || "");
        const matches = matchesPersona && matchesGrupo && matchesLegend;
        card.classList.toggle("is-filtered-out", !matches);
        if (matches) visibleCount += 1;
      });

      grid.querySelectorAll(".shift-cell").forEach((cell) => {
        const count = cell.querySelectorAll(".shift-card:not(.is-filtered-out):not(.is-cambio-hidden)").length;
        let size = 0.66;
        if (count <= 1) size = 0.74;
        else if (count === 2) size = 0.7;
        else if (count === 3) size = 0.66;
        else if (count === 4) size = 0.62;
        else if (count > 4) size = 0.58;
        cell.style.setProperty("--card-font-size", `${size}rem`);
      });
    });
    state.filteredCount = visibleCount;
  };

  const resetCalendarState = () => {
    state.originalPositions.clear();
    state.currentPositions.clear();
    state.actionHistory.clear();
    state.lastActionByCard.clear();
    state.actionIdsByCard.clear();
    state.actionCounter = 0;
    state.planificacionPreviewActive = false;
    state.planificacionPreviewEquipoId = "";
    state.filteredCount = null;
    if (dom.statusList) {
      dom.statusList.innerHTML = "";
    }
  };

  const updateEmptyState = () => {
    if (!dom.emptyState || !dom.grid) return;
    const absentCount = state.showAbsentPeople && Array.isArray(state.absentAssignments)
      ? state.absentAssignments.length
      : 0;
    const total =
      typeof state.filteredCount === "number"
        ? state.filteredCount
        : state.assignments.length + state.previewAssignments.length + absentCount;
    const hasData = total > 0;
    if (!hasData) {
      dom.emptyState.hidden = true;
      dom.grid.hidden = false;
      if (dom.statusList) {
        dom.statusList.innerHTML = "";
      }
    } else {
      dom.emptyState.hidden = true;
      dom.grid.hidden = false;
    }
  };

   const getWeeksInMonth = (month, year) => {
    const firstDay = new Date(year, month, 1);
    const lastDay = new Date(year, month + 1, 0);

    const start = utils.getMondayForDate(firstDay);
    const weeks = [];
    let current = new Date(start);

    while (current <= lastDay) {
      weeks.push(new Date(current));
      current.setDate(current.getDate() + 7);
    }

    return weeks;
  };

  const renderLegendPersonasFromAssignments = (assignments) => {
    if (!dom.legendPersonas) return;

    const uniquePersonas = new Map();
    assignments.forEach((assignment) => {
      if (!assignment.personaId || !assignment.title) return;
      if (!uniquePersonas.has(assignment.personaId)) {
        uniquePersonas.set(assignment.personaId, {
          id: assignment.personaId,
          name: assignment.title,
          color: assignment.color || "#999"
        });
      }
    });

    if (uniquePersonas.size === 0) {
      dom.legendPersonas.hidden = true;
      dom.legendPersonas.innerHTML = "";
      if (state.legendPersonaIds instanceof Set) {
        state.legendPersonaIds.clear();
      }
      return;
    }

    if (state.legendPersonaIds instanceof Set && state.legendPersonaIds.size > 0) {
      Array.from(state.legendPersonaIds).forEach((personaId) => {
        if (!uniquePersonas.has(personaId)) {
          state.legendPersonaIds.delete(personaId);
        }
      });
    }

    dom.legendPersonas.innerHTML = Array.from(uniquePersonas.values())
      .map(
        (persona) => {
          const isSelected = state.legendPersonaIds instanceof Set
            ? state.legendPersonaIds.has(persona.id)
            : false;
          return `
            <button type="button" class="legend-persona-item${isSelected ? " is-selected" : ""}" data-persona-id="${escapeHtmlAttribute(persona.id)}" aria-pressed="${isSelected}">
              <span class="legend-persona-color" style="background-color: ${persona.color}"></span>
              <span class="legend-persona-name">${persona.name}</span>
            </button>
          `;
        }
      )
      .join("");

    dom.legendPersonas.hidden = false;
  };

  const escapeHtmlAttribute = (value) => {
    return `${value ?? ""}`
      .replace(/&/g, "&amp;")
      .replace(/"/g, "&quot;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;");
  };

  const ensureVistaAmpliaTooltipElement = () => {
    if (vistaAmpliaTooltipEl) return vistaAmpliaTooltipEl;
    const el = document.createElement("div");
    el.className = "vista-amplia-tooltip";
    el.hidden = true;
    document.body.appendChild(el);
    vistaAmpliaTooltipEl = el;
    return el;
  };

  const updateVistaAmpliaTooltipPosition = (event) => {
    if (!vistaAmpliaTooltipEl || vistaAmpliaTooltipEl.hidden) return;
    const offset = 14;
    const rect = vistaAmpliaTooltipEl.getBoundingClientRect();
    const maxLeft = window.innerWidth - rect.width - 8;
    const maxTop = window.innerHeight - rect.height - 8;

    const left = Math.min(Math.max(8, event.clientX + offset), Math.max(8, maxLeft));
    const top = Math.min(Math.max(8, event.clientY + offset), Math.max(8, maxTop));

    vistaAmpliaTooltipEl.style.left = `${left}px`;
    vistaAmpliaTooltipEl.style.top = `${top}px`;
  };

  const hideVistaAmpliaTooltip = () => {
    if (!vistaAmpliaTooltipEl) return;
    vistaAmpliaTooltipEl.hidden = true;
    currentTooltipTarget = null;
  };

  const bindCardTooltip = (gridEl) => {
    if (!gridEl || tooltipBoundRoots.has(gridEl)) return;
    ensureVistaAmpliaTooltipElement();

    gridEl.addEventListener("mouseover", (event) => {
      const target = event.target instanceof Element ? event.target : null;
      const tooltipSource = target?.closest("[data-tooltip]");
      if (!tooltipSource || !gridEl.contains(tooltipSource) || !vistaAmpliaTooltipEl) {
        hideVistaAmpliaTooltip();
        return;
      }

      const text = tooltipSource.getAttribute("data-tooltip") || "";
      if (!text.trim()) {
        hideVistaAmpliaTooltip();
        return;
      }

      currentTooltipTarget = tooltipSource;
      vistaAmpliaTooltipEl.textContent = text;
      vistaAmpliaTooltipEl.hidden = false;
      updateVistaAmpliaTooltipPosition(event);
    });

    gridEl.addEventListener("mousemove", (event) => {
      if (!currentTooltipTarget) return;
      updateVistaAmpliaTooltipPosition(event);
    });

    gridEl.addEventListener("mouseout", (event) => {
      const from = event.target instanceof Element ? event.target.closest("[data-tooltip]") : null;
      const to = event.relatedTarget instanceof Element ? event.relatedTarget.closest("[data-tooltip]") : null;
      if (from && from === to) return;
      if (!to) {
        hideVistaAmpliaTooltip();
      }
    });

    gridEl.addEventListener("mouseleave", () => {
      hideVistaAmpliaTooltip();
    });

    tooltipBoundRoots.add(gridEl);
  };

  const getShiftTimeLabel = (shift) => {
    const source = `${shift?.label ?? shift?.name ?? ""}`.trim();

    const normalizeHourToken = (value) => {
      const token = `${value ?? ""}`.trim().toLowerCase();
      if (!token) return "";

      const amPmToken = token.match(/^(\d{1,2})(?::(\d{2}))?\s?(am|pm)$/i);
      if (amPmToken) {
        const hour = parseInt(amPmToken[1], 10);
        const mins = amPmToken[2] || "00";
        const period = amPmToken[3].toLowerCase();
        if (mins === "00") {
          return `${hour}${period}`;
        }
        return `${hour}:${mins}${period}`;
      }

      const hour24Token = token.match(/^(\d{1,2}):(\d{2})$/);
      if (hour24Token) {
        const hour24 = parseInt(hour24Token[1], 10);
        const mins = hour24Token[2];
        const period = hour24 >= 12 ? "pm" : "am";
        let hour12 = hour24 % 12;
        if (hour12 === 0) hour12 = 12;
        if (mins === "00") {
          return `${hour12}${period}`;
        }
        return `${hour12}:${mins}${period}`;
      }

      return token.replace(/\s+/g, "");
    };

    const amPmMatch = source.match(/(\d{1,2}\s?(?:am|pm)).{0,10}(\d{1,2}\s?(?:am|pm))/i);
    if (amPmMatch) {
      const from = normalizeHourToken(amPmMatch[1]);
      const to = normalizeHourToken(amPmMatch[2]);
      return `${from} a ${to}`;
    }

    const hourMatch = source.match(/(\d{1,2}:\d{2}).{0,10}(\d{1,2}:\d{2})/);
    if (hourMatch) {
      return `${normalizeHourToken(hourMatch[1])} a ${normalizeHourToken(hourMatch[2])}`;
    }

    return shift?.shortLabel ?? shift?.short ?? "";
  };

  const renderWeekColumn = (weekStart, weekNumber, allAssignments) => {
    const weekDates = Array.from({ length: 7 }, (_, index) => {
      const date = new Date(weekStart);
      date.setDate(date.getDate() + index);
      return date;
    });
    const assignmentLookup = new Map(
      allAssignments
        .filter((assignment) => assignment?.id)
        .map((assignment) => [assignment.id, assignment])
    );

    const headerCells = weekDates
      .map((date) => {
        const dayLabel = weekdayLabels[date.getDay()];
        return `
          <div class="grid-header">
            ${dayLabel}
            <span>${date.getDate()}</span>
          </div>
        `;
      })
      .join("");

    let rowsHtml = `<div class="shift-label shift-label-mini">T</div>${headerCells}`;

    state.shifts.forEach((shift) => {
      rowsHtml += `<div class="shift-label shift-label-mini">${getShiftTimeLabel(shift)}</div>`;

      weekDates.forEach((date, dayIndex) => {
        const dateStr = utils.formatIsoDate(date);
        const assignments = allAssignments.filter(
          (assignment) => assignment.shift === shift.id && assignment.dateStr === dateStr
        );

        rowsHtml += `<div class="shift-cell" data-day="d${dayIndex}" data-shift="${shift.id}" data-date="${dateStr}">`;
        assignments.forEach((assignment) => {
          const draggable = "false";
          const lockedClass = " is-locked";
          const isAbsent = assignment.calamidadAusente === true;
          const reemplazoNombre = `${assignment.calamidadReemplazaNombre || ""}`.trim();
          const replacementClass = reemplazoNombre ? " is-calamidad-replacement" : "";
          const bgColor = assignment.color || "#999";
          const raw = `${assignment.title ?? ""}`.trim();
          const parts = raw.split(/\s+/);
          const displayName = parts.length >= 3
            ? `${parts[0]} ${parts[2]}`
            : parts.length === 2
              ? `${parts[0]} ${parts[1]}`
              : (parts[0] || "");
          const groupHtml = assignment.group
            ? `<span class="card-group">${assignment.group}</span>`
            : "";
          const cambioVisual = getCambioVisualConfig(assignment.cambioState, assignment.cambioLabel);
          const tooltipParts = [];
          const cambioTooltip = `${cambioVisual?.ariaLabel || ""}`.trim();
          if (cambioTooltip) {
            tooltipParts.push(cambioTooltip);
          }
          if (isAbsent) {
            tooltipParts.push("Ausente por calamidad");
            const reemplazadoPorNombre = `${assignment.calamidadReemplazadoPorNombre || ""}`.trim();
            if (reemplazadoPorNombre) {
              tooltipParts.push(`Reemplazado por ${reemplazadoPorNombre}`);
            }
          }
          if (reemplazoNombre) {
            tooltipParts.push(`Reemplaza a ${reemplazoNombre}`);
          }
          const tooltipRaw = tooltipParts.join(" | ").trim();
          const tooltipName = escapeHtmlAttribute(tooltipRaw);
          const tooltipAttrs = tooltipName
            ? ` aria-label="${tooltipName}" data-tooltip="${tooltipName}"`
            : "";
          const cambioRole = `${assignment.cambioRole || ""}`.toLowerCase();
          const hiddenClasses = getCambioHiddenClasses(assignment, cambioVisual, assignmentLookup);
          const cambioClass = cambioVisual ? ` has-cambio cambio-state-${cambioVisual.key}${hiddenClasses}` : "";
          const absentClass = isAbsent ? " is-calamidad-absent" : "";
          const selectedClass = !isAbsent && state.selectedPersistedTurnos?.has(assignment.id)
            ? " is-selected"
            : "";
          const cardStyle = isAbsent
            ? `background-color: ${bgColor}; color: ${shouldUseDarkText(bgColor) ? "#1e252b" : "#fff"};`
            : `background-color: ${bgColor}`;

          rowsHtml += `
            <div class="shift-card${lockedClass}${replacementClass}${absentClass}${selectedClass}${cambioClass}${assignment.cambioRole ? ` cambio-role-${`${assignment.cambioRole}`.toLowerCase()}` : ""}" style="${cardStyle}" draggable="${draggable}"${tooltipAttrs} data-assignment="${assignment.id}" data-persona-id="${assignment.personaId || ""}" data-grupo-id="${assignment.grupoId || ""}" data-day="d${dayIndex}" data-shift="${shift.id}" data-date="${dateStr}"${cambioVisual ? ` data-cambio-state="${cambioVisual.key}" data-cambio-label="${cambioVisual.ariaLabel}"` : ""}${assignment.cambioRelatedTurnoId ? ` data-cambio-related-assignment="${assignment.cambioRelatedTurnoId}"` : ""}${assignment.cambioRole ? ` data-cambio-role="${`${assignment.cambioRole}`.toLowerCase()}"` : ""}${isAbsent ? ' data-calamidad-ausente="true"' : ""}${reemplazoNombre ? ' data-calamidad-replacement="true"' : ""}>
              <div class="card-title">${displayName}</div>
              ${groupHtml}
            </div>
          `;
        });
        rowsHtml += "</div>";
      });
    });

    const weekStartStr = utils.formatDate(weekStart);
    const weekEnd = new Date(weekStart);
    weekEnd.setDate(weekEnd.getDate() + 6);
    const weekEndStr = utils.formatDate(weekEnd);

    return `
      <div class="week-column">
        <div class="week-column-header">Semana ${weekNumber}<br/><small>${weekStartStr} - ${weekEndStr}</small></div>
        <div class="week-mini-grid">${rowsHtml}</div>
      </div>
    `;
  };

  const renderVistaAmplia = async () => {
    if (!dom.gridAmplia) return;

    if (dom.navRange) {
      dom.navRange.textContent = `${monthNames[state.currentMonth]} ${state.currentYear}`;
    }

    const weeks = getWeeksInMonth(state.currentMonth, state.currentYear);
    if (weeks.length === 0) {
      dom.gridAmplia.innerHTML = "";
      return;
    }

    const allAssignmentsMap = new Map();

    for (const weekStart of weeks) {
      const query = new URLSearchParams({
        equipoId: api.selectedEquipoId || "",
        weekStart: utils.formatIsoDate(weekStart)
      });

      try {
        const response = await fetch(`/Calendario/WeekData?${query.toString()}`, {
          headers: { Accept: "application/json" }
        });

        if (!response.ok) continue;

        const data = await response.json();
        const baseAssignments = Array.isArray(data.assignments) ? data.assignments : [];
        const absentAssignments =
          state.showAbsentPeople && Array.isArray(data.absentAssignments)
            ? data.absentAssignments
            : [];
        const assignments = baseAssignments.concat(absentAssignments);

        if (state.shifts.length === 0) {
          state.shifts = data.shifts || [];
        }

        const weekDates = Array.from({ length: 7 }, (_, index) => {
          const date = new Date(weekStart);
          date.setDate(date.getDate() + index);
          return date;
        });

        assignments.forEach((assignment) => {
          const dayIndex = parseInt((assignment.day || "").replace("d", ""), 10);
          if (Number.isNaN(dayIndex) || dayIndex < 0 || dayIndex > 6) return;

          const dateStr = utils.formatIsoDate(weekDates[dayIndex]);
          const key = `${dateStr}:${assignment.shift}:${assignment.id}`;
          allAssignmentsMap.set(key, {
            ...assignment,
            dateStr
          });
        });
      } catch {
        // Ignora errores de una semana puntual y continua con el resto
      }
    }

    const allAssignments = Array.from(allAssignmentsMap.values());
    const columnsHtml = weeks
      .map((weekStart, index) => renderWeekColumn(weekStart, index + 1, allAssignments))
      .join("");

    dom.gridAmplia.innerHTML = columnsHtml;
    bindCardTooltip(dom.gridAmplia);
    renderLegendPersonasFromAssignments(allAssignments);
  };

  api.render = {
    renderLegend,
    renderGrid,
    renderAssignments,
    applyAssignmentFilters,
    resetCalendarState,
    updateEmptyState,
    getWeekDates,
    renderVistaAmplia
  };
})();

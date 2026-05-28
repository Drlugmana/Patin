(() => {
  const api = window.WeeklyCalendar;
  if (!api) return;

  const { dom, state, days, monthNames, storage, helpers, utils, gruposIds, selectedEquipoId } = api;
  const { renderLegend, renderGrid, renderAssignments, updateEmptyState, getWeekDates } = api.render;
  const { loadWeekData, applyPreview, updateSaveVisibility } = api.data;
  let isGeneratingTurnos = false;
  let isBulkStatusProcessing = false;
  let calamidadReplacementContext = null;
  let pendingRenderActionSnapshots = [];

  const removeActionFromCard = (cardId, actionId) => {
    const list = state.actionIdsByCard.get(cardId) || [];
    state.actionIdsByCard.set(
      cardId,
      list.filter((id) => id !== actionId)
    );
  };

  const removePill = (actionId) => {
    const pill = dom.statusList?.querySelector(`[data-action-id="${actionId}"]`);
    if (pill) {
      pill.remove();
    }
    const action = state.actionHistory.get(actionId);
    if (action?.movedCard?.dataset.assignment) {
      removeActionFromCard(action.movedCard.dataset.assignment, actionId);
    }
    if (action?.swappedCard?.dataset.assignment) {
      removeActionFromCard(action.swappedCard.dataset.assignment, actionId);
    }
    state.actionHistory.delete(actionId);
    updatePendingBar();
  };

  const reconcileCardHistory = (card) => {
    const cardId = card?.dataset.assignment;
    if (!cardId) return;
    const actionIds = state.actionIdsByCard.get(cardId) || [];
    actionIds.forEach((actionId) => {
      const action = state.actionHistory.get(actionId);
      if (!action) return;
    if (action.type === "swap") {
      if (helpers.isAtOrigin(action.movedCard) && helpers.isAtOrigin(action.swappedCard)) {
        removePill(actionId);
      }
    } else if (action.type === "swap-persona") {
      return;
    } else if (helpers.isAtOrigin(action.movedCard)) {
      removePill(actionId);
    }
    });
    if ((state.actionIdsByCard.get(cardId) || []).length === 0) {
      state.lastActionByCard.delete(cardId);
    }
  };

  const confirmCardPosition = (card) => {
    if (!card) return;
    const id = card.dataset.assignment;
    if (!id) return;
    const current = state.currentPositions.get(id);
    if (!current) return;
    state.originalPositions.set(id, { ...current });
  };

  const getCardIdentity = (card) => {
    if (!card) return { title: "", personaId: "" };
    const title = card.querySelector(".card-title")?.textContent?.trim() || "";
    const personaId = card.dataset.personaId || "";
    return { title, personaId };
  };

  const setCardIdentity = (card, identity) => {
    if (!card) return;
    const titleEl = card.querySelector(".card-title");
    if (titleEl) {
      titleEl.textContent = identity.title || "";
    }
    if (identity.personaId) {
      card.dataset.personaId = identity.personaId;
    } else {
      delete card.dataset.personaId;
    }
  };

  const isReverseMove = (prevAction, newAction) => {
    if (!prevAction || !newAction) return false;
    if (prevAction.type !== newAction.type) return false;
    if (prevAction.type === "swap") {
      return (
        prevAction.fromCell === newAction.toCell &&
        prevAction.toCell === newAction.fromCell &&
        prevAction.movedCard === newAction.movedCard &&
        prevAction.swappedCard === newAction.swappedCard
      );
    }
    if (prevAction.type === "swap-persona") {
      return false;
    }
    return (
      prevAction.fromCell === newAction.toCell &&
      prevAction.toCell === newAction.fromCell &&
      prevAction.movedCard === newAction.movedCard
    );
  };

  const isReadOnlyCard = (card) => {
    if (!card) return true;
    return (
      card.classList.contains("is-preview") ||
      card.classList.contains("is-calamidad-absent") ||
      card.dataset.calamidadAusente === "true"
    );
  };

  const showToast = (message, variant = "success") => {
    window.AppToast?.show(message, variant, { title: "Turnos" });
  };

  const setGenerandoTurnosUI = (isLoading, options = {}) => {
    isGeneratingTurnos = isLoading;
    const loadingMessage = typeof options.message === "string" && options.message.trim().length > 0
      ? options.message.trim()
      : "Generando turnos...";

    const board = dom.board || dom.grid?.closest(".calendar-board");
    const generarTurnosOpen = document.querySelector("[data-generar-turnos-open]");
    const generarConfirmBtn = document.getElementById("btnConfirmarGenerarTurnosCalendar");

    if (generarTurnosOpen) {
      generarTurnosOpen.disabled = isLoading;
    }

    if (generarConfirmBtn) {
      if (!generarConfirmBtn.dataset.defaultLabel) {
        generarConfirmBtn.dataset.defaultLabel = generarConfirmBtn.textContent?.trim() || "Confirmar y generar";
      }
      generarConfirmBtn.disabled = isLoading;
      generarConfirmBtn.textContent = isLoading
        ? "Generando..."
        : (generarConfirmBtn.dataset.defaultLabel || "Confirmar y generar");
    }

    if (!board) return;

    board.classList.toggle("is-generating-turnos", isLoading);
    board.setAttribute("aria-busy", isLoading ? "true" : "false");

    const existing = board.querySelector("[data-calendar-generating]");
    if (!isLoading) {
      existing?.remove();
      return;
    }

    if (existing) {
      const messageNode = existing.querySelector("[data-calendar-generating-message]");
      if (messageNode) {
        messageNode.textContent = loadingMessage;
      }
      return;
    }

    const overlay = document.createElement("div");
    overlay.className = "calendar-generating-overlay";
    overlay.setAttribute("data-calendar-generating", "");
    overlay.setAttribute("aria-live", "polite");
    overlay.innerHTML = `
      <div class="calendar-generating-content">
        <span class="calendar-generating-spinner" aria-hidden="true"></span>
        <span data-calendar-generating-message>${loadingMessage}</span>
      </div>
    `;
    board.appendChild(overlay);
  };

  const createGenerationOperationId = () => {
    if (window.crypto?.randomUUID) {
      return window.crypto.randomUUID();
    }
    return `gen-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
  };

  const startGenerationProgressPolling = (operationId) => {
    if (!operationId) {
      return () => {};
    }

    let stopped = false;
    let timeoutId = 0;

    const poll = async () => {
      if (stopped) return;

      try {
        const response = await fetch(`/Planificacion/GeneracionTurnosProgress?operationId=${encodeURIComponent(operationId)}&t=${Date.now()}`, {
          cache: "no-store"
        });

        if (response.ok) {
          const data = await response.json().catch(() => null);
          if (data?.success) {
            setGenerandoTurnosUI(true, {
              message: data.message || `Generando turnos... ${data.percentage ?? 0}%`
            });

            if (data.status === "completed" || data.status === "failed") {
              return;
            }
          }
        }
      } catch {
      }

      if (!stopped) {
        timeoutId = window.setTimeout(poll, 700);
      }
    };

    poll();

    return () => {
      stopped = true;
      if (timeoutId) {
        window.clearTimeout(timeoutId);
      }
    };
  };

  const generarTurnosCalendar = async (
    numeroSemanas,
    fechaInicio,
    autorizarSobrecupoSemanalFeriado = false,
    nivelUsoDescanso7Horas = "low",
    nivelEvitarFinesSemanaConsecutivos = "low",
    balancearHorasSemanales = true
  ) => {
    if (!Array.isArray(gruposIds) || gruposIds.length === 0) {
      showToast("No hay grupos disponibles para este equipo.", "danger");
      return;
    }
    if (isGeneratingTurnos) return;

    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    const token = tokenInput?.value;
    const operationId = createGenerationOperationId();
    const stopProgressPolling = startGenerationProgressPolling(operationId);
    let reintentarConSobrecupoFeriado = false;
    setGenerandoTurnosUI(true, { message: "Generando turnos... 0%" });
    try {
      const response = await fetch("/Planificacion/GenerarTurnosPreview", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          RequestVerificationToken: token || ""
        },
        body: JSON.stringify({
          equipoId: selectedEquipoId || "",
          grupoId: "",
          gruposIds,
          numeroSemanas,
          fechaInicio,
          operationId,
          autorizarSobrecupoSemanalFeriado,
          nivelUsoDescanso7Horas,
          nivelEvitarFinesSemanaConsecutivos,
          balancearHorasSemanales
        })
      });

      const result = await response.json().catch(() => ({}));
      if (!response.ok || result.success === false) {
        if (result.requiresHolidayOvertimeApproval === true && !autorizarSobrecupoSemanalFeriado) {
          const decision = await openCalamidadDecisionModal({
            title: "Autorizar sobrecupo por feriado",
            message: result.approvalMessage ||
              "La generacion requiere permitir mas turnos en una semana con feriado. Deseas autorizarlo?",
            confirmLabel: "Autorizar y generar",
            confirmClass: "btn-warning"
          });
          reintentarConSobrecupoFeriado = decision.action === "confirm";
          return;
        }

        showToast(result.error || "Error al generar turnos.", "danger");
        return;
      }

      showToast("Previsualizacion lista. Revisa y guarda los turnos.", "success");
      await loadWeekData();
      api.debug?.logGeneratedTurnos?.("preview-generated");
    } catch {
      showToast("Error al generar turnos.", "danger");
    } finally {
      stopProgressPolling();
      setGenerandoTurnosUI(false);

      if (reintentarConSobrecupoFeriado) {
        await generarTurnosCalendar(
          numeroSemanas,
          fechaInicio,
          true,
          nivelUsoDescanso7Horas,
          nivelEvitarFinesSemanaConsecutivos,
          balancearHorasSemanales
        );
      }
    }
  };

  const validarGeneracionTurnosCalendar = async (numeroSemanas, fechaInicio) => {
    if (!Array.isArray(gruposIds) || gruposIds.length === 0) {
      showToast("No hay grupos disponibles para este equipo.", "danger");
      return false;
    }

    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    const token = tokenInput?.value;

    try {
      const response = await fetch("/Planificacion/ValidarGeneracionTurnosPreview", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          RequestVerificationToken: token || ""
        },
        body: JSON.stringify({
          equipoId: selectedEquipoId || "",
          grupoId: "",
          gruposIds,
          numeroSemanas,
          fechaInicio
        })
      });

      const result = await response.json().catch(() => ({}));
      if (!response.ok || result.success === false) {
        showToast(result.error || "La configuración actual no permite generar turnos.", "danger");
        return false;
      }

      return true;
    } catch {
      showToast("No se pudo validar la configuración antes de generar.", "danger");
      return false;
    }
  };

  const downloadCalendarImage = async () => {
    if (!dom.grid) return;
    if (typeof window.html2canvas !== "function") {
      showToast("No se pudo preparar la descarga.", "danger");
      return;
    }

    const weekdayLabels = [
      "Domingo",
      "Lunes",
      "Martes",
      "Miercoles",
      "Jueves",
      "Viernes",
      "Sabado"
    ];
    const pad = (value) => value.toString().padStart(2, "0");
    const formatCompactDate = (date) => `${pad(date.getDate())}/${pad(date.getMonth() + 1)}`;
    const shouldUseDarkText = (color) => {
      if (!color) return false;
      const value = `${color}`.trim();

      if (value.startsWith("#")) {
        const hex = value.slice(1);
        if (hex.length !== 6) return false;
        const r = parseInt(hex.slice(0, 2), 16);
        const g = parseInt(hex.slice(2, 4), 16);
        const b = parseInt(hex.slice(4, 6), 16);
        if (Number.isNaN(r) || Number.isNaN(g) || Number.isNaN(b)) return false;
        const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
        return luminance > 0.62;
      }

      const rgbMatch = value.match(/^rgba?\((\d+),\s*(\d+),\s*(\d+)/i);
      if (!rgbMatch) return false;
      const r = Number(rgbMatch[1]);
      const g = Number(rgbMatch[2]);
      const b = Number(rgbMatch[3]);
      if ([r, g, b].some((n) => Number.isNaN(n))) return false;
      const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
      return luminance > 0.62;
    };
    const exportDays = days.slice(0, 7);
    const filterMineActive = state.filterMyShifts === true;
    const selectedGrupoName =
      dom.filterGrupoSelect?.selectedOptions?.[0]?.textContent?.trim() || "";
    const selectedPersonaName =
      dom.filterPersonaSelect?.selectedOptions?.[0]?.textContent?.trim() || "";
    const showAbsentActive = state.showAbsentPeople === true;
    const activeFilters = [];
    if (filterMineActive) {
      activeFilters.push("Mis turnos");
    }
    if (dom.filterGrupoSelect?.value) {
      activeFilters.push(`Grupo: ${selectedGrupoName || dom.filterGrupoSelect.value}`);
    }
    if (dom.filterPersonaSelect?.value) {
      activeFilters.push(`Persona: ${selectedPersonaName || dom.filterPersonaSelect.value}`);
    }
    if (showAbsentActive) {
      activeFilters.push("Ausentes");
    }

    const weekDates = getWeekDates(state.weekOffset).slice(0, 7);
    const absentAssignments = showAbsentActive
      ? Array.isArray(state.absentAssignments)
        ? state.absentAssignments
        : []
      : [];
    const assignmentLookup = new Map(
      state.assignments
        .concat(state.previewAssignments)
        .concat(absentAssignments)
        .map((item) => [String(item.id || ""), item])
    );

    const tableData = [];
    state.shifts.forEach((shift) => {
      const row = {
        shift,
        cells: []
      };
      exportDays.forEach((day) => {
        const safeDay = escapeSelectorValue(day.id);
        const safeShift = escapeSelectorValue(shift.id);
        const cell = dom.grid.querySelector(`[data-day="${safeDay}"][data-shift="${safeShift}"]`);
        const cards = cell
          ? Array.from(
            cell.querySelectorAll(".shift-card:not(.is-filtered-out):not(.is-cambio-hidden)")
          )
          : [];

        const entries = cards.map((card) => {
          const assignmentId = card.dataset.assignment || "";
          const assignment = assignmentLookup.get(assignmentId);
          const personaName = `${assignment?.title || card.querySelector(".card-title")?.textContent || ""}`.trim();
          const groupName = `${assignment?.group || card.querySelector(".card-group")?.textContent || ""}`.trim();
          const isPreview =
            card.classList.contains("is-preview") ||
            card.classList.contains("is-calamidad-absent") ||
            assignment?.preview === true ||
            assignment?.calamidadAusente === true;
          const color = assignment?.color || card.style.backgroundColor || "";
          return { personaName, groupName, isPreview, color };
        });
        row.cells.push(entries);
      });
      tableData.push(row);
    });

    const rangeLabel = dom.navRange?.textContent?.trim() || "semana";
    const safeLabel = rangeLabel.replace(/\s+/g, "_").replace(/[^\w-]/g, "");
    const selectedEquipoName = dom.calendarShell?.dataset.equipo?.trim() || "Equipo";

    const dayColumnWidth = 182;
    const shiftColumnWidth = 170;
    const horizontalPadding = 44;
    const reportWidth =
      shiftColumnWidth + dayColumnWidth * exportDays.length + horizontalPadding * 2;

    const exportRoot = document.createElement("div");
    exportRoot.setAttribute("data-export-weekly-calendar", "");
    exportRoot.style.position = "fixed";
    exportRoot.style.left = "-10000px";
    exportRoot.style.top = "0";
    exportRoot.style.width = `${reportWidth}px`;
    exportRoot.style.padding = "24px";
    exportRoot.style.boxSizing = "border-box";
    exportRoot.style.background =
      "linear-gradient(180deg, #f4f7fb 0%, #eef3f8 46%, #f8fbff 100%)";
    exportRoot.style.color = "#1f2f3d";
    exportRoot.style.fontFamily = "'Space Grotesk', 'Segoe UI', Arial, sans-serif";

    const card = document.createElement("div");
    card.style.backgroundColor = "#ffffff";
    card.style.border = "1px solid #d7e2ee";
    card.style.borderRadius = "14px";
    card.style.overflow = "hidden";
    card.style.boxShadow = "0 18px 46px rgba(22, 52, 78, 0.11)";
    exportRoot.appendChild(card);

    const header = document.createElement("div");
    header.style.padding = "18px 20px";
    header.style.background = "linear-gradient(120deg, #0f4369 0%, #1d628f 58%, #2f7eab 100%)";
    header.style.color = "#ffffff";
    card.appendChild(header);

    const titleEl = document.createElement("div");
    titleEl.textContent = "Calendario semanal";
    titleEl.style.fontSize = "22px";
    titleEl.style.fontWeight = "700";
    titleEl.style.letterSpacing = "0.2px";
    header.appendChild(titleEl);

    const subTitleEl = document.createElement("div");
    subTitleEl.textContent = `${selectedEquipoName} | ${rangeLabel}`;
    subTitleEl.style.marginTop = "4px";
    subTitleEl.style.opacity = "0.95";
    subTitleEl.style.fontSize = "13px";
    header.appendChild(subTitleEl);

    const generatedEl = document.createElement("div");
    generatedEl.textContent = `Generado: ${new Date().toLocaleString("es-ES", { hour12: false })}`;
    generatedEl.style.marginTop = "6px";
    generatedEl.style.opacity = "0.9";
    generatedEl.style.fontSize = "12px";
    header.appendChild(generatedEl);

    const filtersEl = document.createElement("div");
    filtersEl.style.padding = "10px 18px 14px";
    filtersEl.style.fontSize = "11px";
    filtersEl.style.color = "#4f6071";
    filtersEl.textContent = `Filtros: ${activeFilters.length > 0 ? activeFilters.join(" | ") : "Sin filtros activos"}`;
    card.appendChild(filtersEl);

    const tableWrap = document.createElement("div");
    tableWrap.style.padding = "0 16px 16px";
    card.appendChild(tableWrap);

    const table = document.createElement("table");
    table.style.width = "100%";
    table.style.borderCollapse = "separate";
    table.style.borderSpacing = "0";
    table.style.tableLayout = "fixed";
    table.style.border = "1px solid #d8e1ec";
    table.style.borderRadius = "10px";
    table.style.overflow = "hidden";
    tableWrap.appendChild(table);

    const colgroup = document.createElement("colgroup");
    const shiftCol = document.createElement("col");
    shiftCol.style.width = `${shiftColumnWidth}px`;
    colgroup.appendChild(shiftCol);
    exportDays.forEach(() => {
      const col = document.createElement("col");
      col.style.width = `${dayColumnWidth}px`;
      colgroup.appendChild(col);
    });
    table.appendChild(colgroup);

    const thead = document.createElement("thead");
    table.appendChild(thead);
    const headRow = document.createElement("tr");
    thead.appendChild(headRow);

    const shiftHead = document.createElement("th");
    shiftHead.textContent = "Turno";
    shiftHead.style.padding = "10px 9px";
    shiftHead.style.backgroundColor = "#1d628f";
    shiftHead.style.color = "#ffffff";
    shiftHead.style.fontSize = "12px";
    shiftHead.style.textAlign = "left";
    shiftHead.style.borderRight = "1px solid #2c769d";
    headRow.appendChild(shiftHead);

    exportDays.forEach((day, index) => {
      const date = weekDates[index];
      const dayName = state.dayLabelsById?.[day.id] || weekdayLabels[date.getDay()];
      const holidayCount = Array.isArray(state.holidaysByDay?.[day.id])
        ? state.holidaysByDay[day.id].length
        : 0;
      const vacationCount = Array.isArray(state.vacationsByDay?.[day.id])
        ? state.vacationsByDay[day.id].length
        : 0;
      const headerCell = document.createElement("th");
      headerCell.style.padding = "8px 8px 9px";
      headerCell.style.backgroundColor = "#1d628f";
      headerCell.style.color = "#ffffff";
      headerCell.style.textAlign = "left";
      headerCell.style.borderLeft = "1px solid #2c769d";

      const dayTitle = document.createElement("div");
      dayTitle.textContent = dayName;
      dayTitle.style.fontSize = "12px";
      dayTitle.style.fontWeight = "700";
      headerCell.appendChild(dayTitle);

      const dayDate = document.createElement("div");
      dayDate.textContent = formatCompactDate(date);
      dayDate.style.fontSize = "11px";
      dayDate.style.opacity = "0.9";
      dayDate.style.marginTop = "1px";
      headerCell.appendChild(dayDate);

      if (holidayCount > 0 || vacationCount > 0) {
        const badges = document.createElement("div");
        badges.style.marginTop = "5px";
        badges.style.display = "flex";
        badges.style.gap = "4px";
        badges.style.flexWrap = "wrap";
        if (holidayCount > 0) {
          const holidayBadge = document.createElement("span");
          holidayBadge.textContent = `Feriado ${holidayCount}`;
          holidayBadge.style.fontSize = "10px";
          holidayBadge.style.padding = "2px 5px";
          holidayBadge.style.borderRadius = "999px";
          holidayBadge.style.backgroundColor = "#ffdfb1";
          holidayBadge.style.color = "#744100";
          badges.appendChild(holidayBadge);
        }
        if (vacationCount > 0) {
          const vacationBadge = document.createElement("span");
          vacationBadge.textContent = `Ausencias ${vacationCount}`;
          vacationBadge.style.fontSize = "10px";
          vacationBadge.style.padding = "2px 5px";
          vacationBadge.style.borderRadius = "999px";
          vacationBadge.style.backgroundColor = "#d4ecff";
          vacationBadge.style.color = "#0b4f7a";
          badges.appendChild(vacationBadge);
        }
        headerCell.appendChild(badges);
      }

      headRow.appendChild(headerCell);
    });

    const tbody = document.createElement("tbody");
    table.appendChild(tbody);
    tableData.forEach((row, rowIndex) => {
      const tr = document.createElement("tr");
      tr.style.backgroundColor = rowIndex % 2 === 0 ? "#ffffff" : "#f9fbfd";
      tbody.appendChild(tr);

      const shiftCell = document.createElement("th");
      shiftCell.style.padding = "9px";
      shiftCell.style.textAlign = "left";
      shiftCell.style.verticalAlign = "top";
      shiftCell.style.borderTop = "1px solid #e2e9f0";
      shiftCell.style.borderRight = "1px solid #d7e0ea";
      shiftCell.style.backgroundColor = "#eff5fb";

      const shiftLabel = document.createElement("div");
      shiftLabel.textContent = row.shift.shortLabel || row.shift.short || row.shift.label || "-";
      shiftLabel.style.fontSize = "12px";
      shiftLabel.style.fontWeight = "700";
      shiftLabel.style.color = "#183852";
      shiftCell.appendChild(shiftLabel);

      const shiftTime = document.createElement("div");
      shiftTime.textContent = row.shift.label || "";
      shiftTime.style.marginTop = "2px";
      shiftTime.style.fontSize = "11px";
      shiftTime.style.color = "#4f6274";
      shiftCell.appendChild(shiftTime);
      tr.appendChild(shiftCell);

      row.cells.forEach((entries) => {
        const td = document.createElement("td");
        td.style.padding = "7px 6px";
        td.style.verticalAlign = "top";
        td.style.borderTop = "1px solid #e2e9f0";
        td.style.borderLeft = "1px solid #edf2f7";
        td.style.minHeight = "80px";
        td.style.backgroundColor = "#ffffff";

        if (entries.length === 0) {
          const emptyText = document.createElement("div");
          emptyText.textContent = "-";
          emptyText.style.fontSize = "11px";
          emptyText.style.color = "#8a99a8";
          td.appendChild(emptyText);
        } else {
          entries.forEach((entry) => {
            const item = document.createElement("div");
            item.style.padding = "5px 6px";
            item.style.borderRadius = "6px";
            item.style.marginBottom = "5px";
            item.style.backgroundColor = entry.isPreview
              ? "#edf8ef"
              : (entry.color || "#f1f6ff");
            item.style.border = entry.isPreview
              ? "1px solid #c8e5cd"
              : `1px solid ${entry.color ? "rgba(0, 0, 0, 0.08)" : "#d8e6fd"}`;

            const nameEl = document.createElement("div");
            nameEl.textContent = entry.personaName || "Sin nombre";
            nameEl.style.fontSize = "11px";
            nameEl.style.fontWeight = "700";
            nameEl.style.color = entry.color
              ? (shouldUseDarkText(entry.color) ? "#1e252b" : "#ffffff")
              : "#1c2f42";
            item.appendChild(nameEl);

            if (entry.groupName || entry.isPreview) {
              const metaEl = document.createElement("div");
              metaEl.textContent = entry.groupName
                ? entry.groupName
                : "Previsualizacion";
              metaEl.style.fontSize = "10px";
              metaEl.style.marginTop = "2px";
              metaEl.style.color = entry.color
                ? (shouldUseDarkText(entry.color) ? "#34495e" : "rgba(255, 255, 255, 0.92)")
                : "#5a6d82";
              item.appendChild(metaEl);
            }

            td.appendChild(item);
          });
        }

        tr.appendChild(td);
      });
    });

    document.body.appendChild(exportRoot);
    try {
      const canvas = await window.html2canvas(exportRoot, {
        backgroundColor: "#f4f7fb",
        scale: 2,
        width: reportWidth,
        windowWidth: reportWidth
      });
      const link = document.createElement("a");
      link.download = `calendario_${safeLabel || "semana"}.png`;
      link.href = canvas.toDataURL("image/png");
      link.click();
      showToast("Imagen descargada.", "success");
    } catch {
      showToast("No se pudo generar la imagen.", "danger");
    } finally {
      exportRoot.remove();
    }
  };

  let highlightedActionId = null;

  const clearCardHighlights = () => {
    document.querySelectorAll(".shift-card.is-highlighted").forEach((card) => {
      card.classList.remove("is-highlighted");
    });
    document.querySelectorAll(".status-pill.is-highlighted").forEach((pill) => {
      pill.classList.remove("is-highlighted");
    });
  };

  const highlightAction = (actionId) => {
    const action = state.actionHistory.get(actionId);
    if (!action) return;
    if (action.movedCard) {
      action.movedCard.classList.add("is-highlighted");
    }
    if (action.swappedCard) {
      action.swappedCard.classList.add("is-highlighted");
    }
  };

  const canSwapByGroup = (firstCard, secondCard) => {
    const normalize = (value) => (value || "").toString().trim().toLowerCase();
    const firstPersona = normalize(firstCard?.dataset.personaId);
    const secondPersona = normalize(secondCard?.dataset.personaId);
    const firstGrupo = normalize(firstCard?.dataset.grupoId);
    const secondGrupo = normalize(secondCard?.dataset.grupoId);
    if (!firstPersona || !secondPersona) return false;
    if (!firstGrupo && !secondGrupo) return true;
    if (!firstGrupo || !secondGrupo) return false;

    const groupsMap = state.personaGroups || {};
    const resolveGroups = (personaId) => {
      const direct = groupsMap[personaId];
      if (direct) return direct;
      const key = Object.keys(groupsMap).find((k) => normalize(k) === personaId);
      return key ? groupsMap[key] : [];
    };

    const firstGroups = new Set(resolveGroups(firstPersona).map(normalize));
    const secondGroups = new Set(resolveGroups(secondPersona).map(normalize));
    if (firstGroups.size === 0 || secondGroups.size === 0) return false;

    const firstHasFirst = firstGroups.has(firstGrupo);
    const firstHasSecond = firstGroups.has(secondGrupo);
    const secondHasFirst = secondGroups.has(firstGrupo);
    const secondHasSecond = secondGroups.has(secondGrupo);

    if (firstGrupo === secondGrupo) {
      return firstHasFirst && secondHasFirst;
    }

    return firstHasFirst && firstHasSecond && secondHasFirst && secondHasSecond;
  };

  const getPendingPills = () => {
    if (!dom.statusList) return [];
    return Array.from(
      dom.statusList.querySelectorAll(".status-pill[data-action-id]:not(.pill-locked)")
    );
  };

  const updatePendingBar = () => {
    const pendingCount = getPendingPills().length;
    if (dom.pendingCount) {
      dom.pendingCount.textContent = `${pendingCount} cambio${pendingCount === 1 ? "" : "s"} pendiente${pendingCount === 1 ? "" : "s"}`;
    }
    if (dom.pendingBar) {
      dom.pendingBar.hidden = pendingCount === 0;
    }
    if (dom.pendingConfirmAllBtn) {
      dom.pendingConfirmAllBtn.disabled = pendingCount === 0 || isBulkStatusProcessing;
    }
    if (dom.pendingCancelAllBtn) {
      dom.pendingCancelAllBtn.disabled = pendingCount === 0 || isBulkStatusProcessing;
    }
  };

  const updateActionState = (pill, stateText) => {
    const stateEl = pill.querySelector(".pill-state");
    if (stateEl) {
      stateEl.textContent = stateText;
    }
    pill.classList.add("pill-locked");
    pill.querySelectorAll("button").forEach((button) => {
      button.disabled = true;
    });
  };

  const addStatus = (messageHtml, action, toastText) => {
    if (!dom.statusList) return;
    const movedKey = action?.movedCard?.dataset.assignment || "";
    const swappedKey = action?.swappedCard?.dataset.assignment || "";
    const prevId = movedKey ? state.lastActionByCard.get(movedKey) : null;

    if (prevId) {
      removePill(prevId);
    }

    const actionId = `action-${++state.actionCounter}`;
    if (movedKey) {
      state.lastActionByCard.set(movedKey, actionId);
      state.actionIdsByCard.set(movedKey, [actionId]);
    }
    if (swappedKey && swappedKey !== movedKey) {
      state.actionIdsByCard.set(swappedKey, [actionId]);
    }
    const pill = document.createElement("span");
    pill.className = "status-pill";
    pill.dataset.actionId = actionId;
    state.actionHistory.set(actionId, { ...action, toastText });
    pill.innerHTML = `
    <span class="status-text">${messageHtml}</span>
    <span class="status-actions">
      <button type="button" class="pill-btn pill-confirm" data-action="confirm" aria-label="Confirmar">
        <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
          <path d="M9.2 16.2 4.9 12l-1.4 1.4 5.7 5.7L20.5 7.8 19.1 6.4z"></path>
        </svg>
      </button>
      <button type="button" class="pill-btn pill-cancel" data-action="cancel" aria-label="Cancelar">
        <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
          <path d="M18.3 5.7 12 12l6.3 6.3-1.4 1.4L10.6 13.4 4.3 19.7 2.9 18.3 9.2 12 2.9 5.7 4.3 4.3l6.3 6.3 6.3-6.3z"></path>
        </svg>
      </button>
      <span class="pill-state" aria-live="polite"></span>
    </span>
  `;
    dom.statusList.appendChild(pill);
    dom.statusList.scrollTo({ left: dom.statusList.scrollWidth, behavior: "smooth" });
    updatePendingBar();
  };

  const buildStatusInfoHtml = (card, dayId, shiftId, includeName) => {
    const name = card?.querySelector(".card-title")?.textContent?.trim() || "Sin asignar";
    const day = state.dayLabelsById[dayId] || dayId || "Dia";
    const shift = state.shifts.find((item) => item.id === shiftId);
    const shiftShort = shift?.shortLabel ?? shift?.short ?? "Turno";
    const shiftLabel = shift?.label ?? "";
    const nameHtml = includeName
      ? `<span class="status-sep"> - </span><span class="status-name">${name}</span>`
      : "";
    return `
      <span class="status-day">${day}</span>
      <span class="status-sep"> - </span>
      <span class="status-shift">${shiftShort}${shiftLabel ? ` (${shiftLabel})` : ""}</span>
      ${nameHtml}
    `;
  };

  const buildStatusInfoText = (card, dayId, shiftId) => {
    const name = card?.querySelector(".card-title")?.textContent?.trim() || "Sin asignar";
    const day = state.dayLabelsById[dayId] || dayId || "Dia";
    const shift = state.shifts.find((item) => item.id === shiftId);
    const shiftLabel = shift?.label ?? "";
    return `${day} ${shiftLabel} ${name}`.trim();
  };

  const findRenderedCardByAssignmentId = (assignmentId) => {
    if (!assignmentId || !dom.grid) return null;
    return Array.from(dom.grid.querySelectorAll(".shift-card")).find(
      (card) => card.dataset.assignment === assignmentId
    ) || null;
  };

  const findRenderedCell = (dayId, shiftId) => {
    if (!dayId || !shiftId || !dom.grid) return null;
    return dom.grid.querySelector(`.shift-cell[data-day="${dayId}"][data-shift="${shiftId}"]`);
  };

  const capturePendingChangesBeforeRender = () => {
    pendingRenderActionSnapshots = getPendingPills()
      .map((pill) => {
        const actionId = pill?.dataset?.actionId || "";
        const action = actionId ? state.actionHistory.get(actionId) : null;
        if (!action) return null;

        if (action.type === "move") {
          const movedAssignmentId = action.movedCard?.dataset.assignment || "";
          const current = movedAssignmentId ? state.currentPositions.get(movedAssignmentId) : null;
          const fromDay = action.fromCell?.dataset.day || "";
          const fromShift = action.fromCell?.dataset.shift || "";
          const toDay = current?.day || action.toCell?.dataset.day || "";
          const toShift = current?.shift || action.toCell?.dataset.shift || "";
          if (!movedAssignmentId || !fromDay || !fromShift || !toDay || !toShift) {
            return null;
          }
          return {
            type: "move",
            movedAssignmentId,
            fromDay,
            fromShift,
            toDay,
            toShift,
            toastText: action.toastText || ""
          };
        }

        if (action.type === "swap-persona") {
          const movedAssignmentId = action.movedCard?.dataset.assignment || "";
          const swappedAssignmentId = action.swappedCard?.dataset.assignment || "";
          if (!movedAssignmentId || !swappedAssignmentId) {
            return null;
          }
          return {
            type: "swap-persona",
            movedAssignmentId,
            swappedAssignmentId,
            movedData: { ...action.movedData },
            swappedData: { ...action.swappedData },
            movedDay: action.movedCard?.dataset.day || "",
            movedShift: action.movedCard?.dataset.shift || "",
            swappedDay: action.swappedCard?.dataset.day || "",
            swappedShift: action.swappedCard?.dataset.shift || "",
            toastText: action.toastText || ""
          };
        }

        return null;
      })
      .filter(Boolean);

    return pendingRenderActionSnapshots.length;
  };

  const restorePendingChangesAfterRender = () => {
    if (!Array.isArray(pendingRenderActionSnapshots) || pendingRenderActionSnapshots.length === 0) {
      return;
    }

    const snapshots = pendingRenderActionSnapshots;
    pendingRenderActionSnapshots = [];

    clearCardHighlights();
    highlightedActionId = null;
    if (dom.statusList) {
      dom.statusList.innerHTML = "";
    }
    state.actionHistory.clear();
    state.lastActionByCard.clear();
    state.actionIdsByCard.clear();

    snapshots.forEach((snapshot) => {
      if (snapshot.type === "move") {
        const movedCard = findRenderedCardByAssignmentId(snapshot.movedAssignmentId);
        const fromCell = findRenderedCell(snapshot.fromDay, snapshot.fromShift);
        const toCell = findRenderedCell(snapshot.toDay, snapshot.toShift);
        if (!movedCard || !fromCell || !toCell) return;

        toCell.appendChild(movedCard);
        helpers.updateCurrentPosition(movedCard, toCell);

        const fromHtml = buildStatusInfoHtml(movedCard, snapshot.fromDay, snapshot.fromShift, false);
        const toHtml = buildStatusInfoHtml(movedCard, snapshot.toDay, snapshot.toShift, false);
        const fromText = buildStatusInfoText(movedCard, snapshot.fromDay, snapshot.fromShift);
        const toText = buildStatusInfoText(movedCard, snapshot.toDay, snapshot.toShift);
        const displayName = movedCard.querySelector(".card-title")?.textContent?.trim() || "Sin asignar";

        addStatus(
          `${fromHtml}<span class="status-arrow">-&gt;</span>${toHtml}<span class="status-sep"> - </span><span class="status-name">${displayName}</span>`,
          {
            type: "move",
            fromCell,
            toCell,
            movedCard
          },
          snapshot.toastText || `Cambio confirmado: ${fromText} -> ${toText}`
        );
        return;
      }

      if (snapshot.type === "swap-persona") {
        const movedCard = findRenderedCardByAssignmentId(snapshot.movedAssignmentId);
        const swappedCard = findRenderedCardByAssignmentId(snapshot.swappedAssignmentId);
        if (!movedCard || !swappedCard) return;

        setCardIdentity(movedCard, snapshot.swappedData);
        setCardIdentity(swappedCard, snapshot.movedData);

        const fromHtml = buildStatusInfoHtml(movedCard, snapshot.movedDay, snapshot.movedShift, true);
        const toHtml = buildStatusInfoHtml(swappedCard, snapshot.swappedDay, snapshot.swappedShift, true);
        const fromText = buildStatusInfoText(movedCard, snapshot.movedDay, snapshot.movedShift);
        const toText = buildStatusInfoText(swappedCard, snapshot.swappedDay, snapshot.swappedShift);

        addStatus(
          `${fromHtml}<span class="status-arrow">&lt;-&gt;</span>${toHtml}`,
          {
            type: "swap-persona",
            movedCard,
            swappedCard,
            movedData: { ...snapshot.movedData },
            swappedData: { ...snapshot.swappedData }
          },
          snapshot.toastText || `Cambio confirmado: ${fromText} <-> ${toText}`
        );
      }
    });
  };

  const rerenderCalendarPreservingPendingChanges = ({ includeLegend = false, includeSaveVisibility = false } = {}) => {
    capturePendingChangesBeforeRender();
    if (includeLegend) {
      renderLegend();
    }
    renderGrid();
    renderAssignments();
    updateEmptyState();
    if (includeSaveVisibility) {
      updateSaveVisibility();
    }
  };

  const escapeSelectorValue = (value) =>
    `${value ?? ""}`.replace(/([\\.#:[\],=])/g, "\\$1");

  const clearCambioLinkedHover = () => {
    document
      .querySelectorAll(
        ".shift-card.is-cambio-hover-source, .shift-card.is-cambio-hover-target, .shift-card.is-cambio-hover-origin, .shift-card.is-cambio-preview-visible, .shift-card.is-cambio-context-dimmed"
      )
      .forEach((card) => {
        card.classList.remove("is-cambio-hover-source", "is-cambio-hover-target", "is-cambio-hover-origin", "is-cambio-preview-visible", "is-cambio-context-dimmed");
      });
  };

  const bindCambioLinkedHover = (gridEl) => {
    if (!gridEl) return;
    let currentSource = null;

    const applyHover = (sourceCard) => {
      clearCambioLinkedHover();
      currentSource = null;
      if (!sourceCard || isReadOnlyCard(sourceCard)) return;

      const sourceRole = `${sourceCard.dataset.cambioRole || ""}`.toLowerCase();
      const relatedId = sourceCard.dataset.cambioRelatedAssignment || "";
      if (!relatedId || (sourceRole !== "destino" && sourceRole !== "origen")) return;

      const safeRelatedId = escapeSelectorValue(relatedId);
      const relatedCards = Array.from(
        document.querySelectorAll(`.shift-card[data-assignment="${safeRelatedId}"]`)
      ).filter((card) => card !== sourceCard);

      if (relatedCards.length === 0) return;

      const focusCards = new Set([sourceCard, ...relatedCards]);
      gridEl.querySelectorAll(".shift-card").forEach((card) => {
        if (!focusCards.has(card)) {
          card.classList.add("is-cambio-context-dimmed");
        }
      });

      sourceCard.classList.add("is-cambio-hover-source");
      relatedCards.forEach((card) => {
        card.classList.add("is-cambio-hover-target");
        const relatedRole = `${card.dataset.cambioRole || ""}`.toLowerCase();
        if (relatedRole === "origen") {
          card.classList.add("is-cambio-hover-origin");
        }
        if (
          card.classList.contains("is-cambio-hidden") ||
          card.classList.contains("is-cambio-pending-hidden") ||
          card.classList.contains("is-cambio-approved-hidden")
        ) {
          card.classList.add("is-cambio-preview-visible");
        }
      });
      currentSource = sourceCard;
    };

    gridEl.addEventListener("mouseover", (event) => {
      const target = event.target instanceof HTMLElement ? event.target : null;
      const card = target?.closest(".shift-card");
      if (!card || !gridEl.contains(card)) return;
      if (currentSource === card) return;
      applyHover(card);
    });

    gridEl.addEventListener("mouseout", (event) => {
      if (!currentSource) return;
      const from = event.target instanceof HTMLElement ? event.target.closest(".shift-card") : null;
      if (!from || from !== currentSource) return;
      const to = event.relatedTarget instanceof HTMLElement ? event.relatedTarget.closest(".shift-card") : null;
      if (to === currentSource) return;
      clearCambioLinkedHover();
      currentSource = null;
    });

    gridEl.addEventListener("mouseleave", () => {
      clearCambioLinkedHover();
      currentSource = null;
    });
  };

  const bindDragEvents = () => {
    if (!dom.grid) return;
    let draggingCard = null;
    const showReplacementDragBlocked = (message) => {
      const guardKey = "calamidad-replacement-drag";
      if (state.dragGuardrailsShown.has(guardKey)) return;
      showToast(message, "info");
      state.dragGuardrailsShown.add(guardKey);
    };
    const clearHighlights = () => {
      dom.grid.querySelectorAll(".drop-target, .drop-add, .drop-swap").forEach((cell) => {
        cell.classList.remove("drop-target", "drop-add", "drop-swap");
      });
      dom.grid.querySelectorAll(".swap-target").forEach((card) => {
        card.classList.remove("swap-target");
      });
    };

    dom.grid.addEventListener("dragstart", (event) => {
      const target = event.target;
      if (!(target instanceof HTMLElement) || !target.classList.contains("shift-card")) {
        return;
      }
      if (hasActiveCambioCard(target)) {
        event.preventDefault();
        showToast(getActiveCambioBlockedMessage(), "info");
        return;
      }
      if (target.getAttribute("draggable") !== "true") {
        if (isExistingCalamidadReplacementCard(target)) {
          showReplacementDragBlocked("No se puede mover un turno que ya esta actuando como reemplazo.");
        }
        event.preventDefault();
        return;
      }
      if (isExistingCalamidadReplacementCard(target)) {
        event.preventDefault();
        showReplacementDragBlocked("No se puede mover un turno que ya esta actuando como reemplazo.");
        return;
      }
      if (!canOperateShiftCard(target)) {
        event.preventDefault();
        const guardKey = canRequestCambioTurno() ? "own-turnos" : "move-turnos-permission";
        if (!state.dragGuardrailsShown.has(guardKey)) {
          showToast(
            canRequestCambioTurno()
              ? "Solo puedes mover tus propios turnos."
              : "No tienes permiso para mover turnos.",
            "info"
          );
          state.dragGuardrailsShown.add(guardKey);
        }
        return;
      }
      draggingCard = target;
      target.classList.add("dragging");
      event.dataTransfer?.setData("text/plain", target.dataset.assignment || "");
      event.dataTransfer?.setDragImage(target, 20, 20);
    });

    dom.grid.addEventListener("dragend", () => {
      if (!draggingCard) return;
      draggingCard.classList.remove("dragging");
      draggingCard = null;
      clearHighlights();
    });

    dom.grid.addEventListener("dragover", (event) => {
      const cell = event.target instanceof HTMLElement
        ? event.target.closest(".shift-cell")
        : null;
      if (!cell) return;
      event.preventDefault();
      clearHighlights();
      cell.classList.add("drop-target");
      const targetCard = (() => {
        if (!(event.target instanceof HTMLElement)) return null;
        const directCard = event.target.closest(".shift-card");
        if (directCard && directCard !== draggingCard && !directCard.classList.contains("is-preview")) {
          return directCard;
        }

        return null;
      })();
      if (targetCard && targetCard !== draggingCard) {
        if (isExistingCalamidadReplacementCard(targetCard)) {
          return;
        }
        if (hasActiveCambioCard(targetCard)) {
          return;
        }
        cell.classList.add("drop-swap");
        targetCard.classList.add("swap-target");
      } else {
        cell.classList.add("drop-add");
      }
    });

    dom.grid.addEventListener("dragleave", (event) => {
      const cell = event.target instanceof HTMLElement
        ? event.target.closest(".shift-cell")
        : null;
      if (!cell) return;
      clearHighlights();
    });

    const buildInfoHtml = (card, dayId, shiftId, includeName) => {
      const name = card?.querySelector(".card-title")?.textContent?.trim() || "Sin asignar";
      const day = state.dayLabelsById[dayId] || dayId || "Dia";
      const shift = state.shifts.find((item) => item.id === shiftId);
      const shiftShort = shift?.shortLabel ?? shift?.short ?? "Turno";
      const shiftLabel = shift?.label ?? "";
      const nameHtml = includeName
        ? `<span class="status-sep"> - </span><span class="status-name">${name}</span>`
        : "";
      return `
        <span class="status-day">${day}</span>
        <span class="status-sep"> - </span>
        <span class="status-shift">${shiftShort}${shiftLabel ? ` (${shiftLabel})` : ""}</span>
        ${nameHtml}
      `;
    };

    const buildInfoText = (card, dayId, shiftId) => {
      const name = card?.querySelector(".card-title")?.textContent?.trim() || "Sin asignar";
      const day = state.dayLabelsById[dayId] || dayId || "Dia";
      const shift = state.shifts.find((item) => item.id === shiftId);
      const shiftLabel = shift?.label ?? "";
      return `${day} ${shiftLabel} ${name}`.trim();
    };

    const collectValidationForPlacement = ({
      personaId,
      dayId,
      shiftId,
      excludeAssignmentIds = []
    }) => {
      if (!personaId || !dayId || !shiftId) {
        return { blocking: [], warnings: [] };
      }

      const targets = [{ dayId, shiftId }];
      const conflicts = buildTurnoConflictDetails({
        personaId,
        targets,
        excludeAssignmentIds
      });
      const blocking = conflicts
        .filter((item) => item?.blocking)
        .map((item) => item?.text || "")
        .filter(Boolean);
      const warnings = conflicts
        .filter((item) => !item?.blocking)
        .map((item) => item?.text || "")
        .filter(Boolean);
      const overloadWarnings = buildSobrecargaDetails({
        personaId,
        targets,
        excludeAssignmentIds
      })
        .map((item) => (typeof item === "string" ? item : item?.text || ""))
        .filter(Boolean);
      const holidayWarnings = buildFeriadoWarningDetails({ targets });

      return {
        blocking,
        warnings: warnings.concat(overloadWarnings, holidayWarnings)
      };
    };

    dom.grid.addEventListener("drop", async (event) => {
      event.preventDefault();
      const targetElement = event.target instanceof HTMLElement ? event.target : null;
      const cell = targetElement?.closest(".shift-cell");
      if (!cell || !draggingCard) return;
      const draggedCard = draggingCard;

      if (isExistingCalamidadReplacementCard(draggedCard)) {
        showReplacementDragBlocked("No se puede mover un turno que ya esta actuando como reemplazo.");
        return;
      }

      if (!canOperateShiftCard(draggedCard)) {
        const guardKey = canRequestCambioTurno() ? "own-turnos" : "move-turnos-permission";
        if (!state.dragGuardrailsShown.has(guardKey)) {
          showToast(
            canRequestCambioTurno()
              ? "Solo puedes mover tus propios turnos."
              : "No tienes permiso para mover turnos.",
            "info"
          );
          state.dragGuardrailsShown.add(guardKey);
        }
        return;
      }

      clearHighlights();
      cell.classList.add("drop-flash");
      setTimeout(() => cell.classList.remove("drop-flash"), 450);

      const originCell = draggedCard.closest(".shift-cell");
      const targetCard = (() => {
        const directCard = targetElement?.closest(".shift-card");
        if (directCard && directCard !== draggedCard && !directCard.classList.contains("is-preview")) {
          return directCard;
        }

        return null;
      })();
      const originDay = originCell?.dataset.day || "";
      const originShift = originCell?.dataset.shift || "";
      const targetDay = cell.dataset.day || "";
      const targetShift = cell.dataset.shift || "";

      if (originCell === cell) {
        return;
      }

      if (targetCard && targetCard !== draggedCard && originCell) {
        if (isExistingCalamidadReplacementCard(targetCard)) {
          showReplacementDragBlocked("No se puede intercambiar con un turno que ya esta actuando como reemplazo.");
          return;
        }
        if (hasActiveCambioCard(targetCard)) {
          showToast(getActiveCambioBlockedMessage(), "info");
          return;
        }

        if (!canSwapByGroup(draggedCard, targetCard)) {
          const guardKey = "grupo-swap";
          if (!state.dragGuardrailsShown.has(guardKey)) {
            showToast("No se puede intercambiar: grupos no autorizados.");
            state.dragGuardrailsShown.add(guardKey);
          }
          return;
        }

        const movedAssignmentId = draggedCard.dataset.assignment || "";
        const swappedAssignmentId = targetCard.dataset.assignment || "";
        const movedPersonaId = draggedCard.dataset.personaId || "";
        const swappedPersonaId = targetCard.dataset.personaId || "";
        const movedPersonaName = draggedCard.querySelector(".card-title")?.textContent?.trim() || "Persona";
        const swappedPersonaName = targetCard.querySelector(".card-title")?.textContent?.trim() || "Persona";

        const movedValidation = collectValidationForPlacement({
          personaId: movedPersonaId,
          dayId: targetDay,
          shiftId: targetShift,
          excludeAssignmentIds: [movedAssignmentId]
        });
        const swappedValidation = collectValidationForPlacement({
          personaId: swappedPersonaId,
          dayId: originDay,
          shiftId: originShift,
          excludeAssignmentIds: [swappedAssignmentId]
        });

        const prefixedBlocking = [
          ...movedValidation.blocking.map((text) => `${movedPersonaName}: ${text}`),
          ...swappedValidation.blocking.map((text) => `${swappedPersonaName}: ${text}`)
        ];
        if (prefixedBlocking.length > 0) {
          await openSobrecargaModal({
            title: "Intercambio no permitido",
            message:
              "No se puede completar el intercambio porque generaria turnos duplicados para la misma persona en el mismo dia y tipo.",
            details: prefixedBlocking,
            confirmLabel: "Entendido",
            allowProceed: false
          });
          return;
        }

        const prefixedWarnings = [
          ...movedValidation.warnings.map((text) => `${movedPersonaName}: ${text}`),
          ...swappedValidation.warnings.map((text) => `${swappedPersonaName}: ${text}`)
        ];
        if (prefixedWarnings.length > 0) {
          const accepted = await openSobrecargaModal({
            title: "Validacion de intercambio",
            message:
              "El intercambio puede generar sobrecarga, mover turnos a dia feriado y/o incumplir el descanso minimo de 8 horas.",
            details: prefixedWarnings,
            confirmLabel: "Continuar intercambio",
            allowProceed: true
          });
          if (!accepted) {
            return;
          }
        }

        const movedData = getCardIdentity(draggedCard);
        const swappedData = getCardIdentity(targetCard);
        setCardIdentity(draggedCard, swappedData);
        setCardIdentity(targetCard, movedData);
        const action = {
          type: "swap-persona",
          movedCard: draggedCard,
          swappedCard: targetCard,
          movedData,
          swappedData
        };
        const fromHtml = buildInfoHtml(draggedCard, originDay, originShift, true);
        const toHtml = buildInfoHtml(targetCard, targetDay, targetShift, true);
        const fromText = buildInfoText(draggedCard, originDay, originShift);
        const toText = buildInfoText(targetCard, targetDay, targetShift);
        addStatus(
          `${fromHtml}<span class="status-arrow">&lt;-&gt;</span>${toHtml}`,
          action,
          `Cambio confirmado: ${fromText}  ${toText}`
        );
      } else {
        const action = {
          type: "move",
          fromCell: originCell,
          toCell: cell,
          movedCard: draggedCard
        };
        const movedKey = draggedCard.dataset.assignment || "";
        const prevId = movedKey ? state.lastActionByCard.get(movedKey) : null;
        const prevAction = prevId ? state.actionHistory.get(prevId) : null;

        if (prevId && isReverseMove(prevAction, action)) {
          cell.appendChild(draggedCard);
          helpers.updateCurrentPosition(draggedCard, cell);
          removePill(prevId);
          reconcileCardHistory(draggedCard);
          showToast("Cambio revertido.");
          return;
        }

        const personaId = draggedCard.dataset.personaId || "";
        const personaName = draggedCard.querySelector(".card-title")?.textContent?.trim() || "Persona";
        const validation = collectValidationForPlacement({
          personaId,
          dayId: targetDay,
          shiftId: targetShift,
          excludeAssignmentIds: [movedKey]
        });

        if (validation.blocking.length > 0) {
          await openSobrecargaModal({
            title: "Movimiento no permitido",
            message:
              "No se puede mover este turno porque generaria un turno duplicado (misma persona, mismo dia y mismo tipo).",
            details: validation.blocking.map((text) => `${personaName}: ${text}`),
            confirmLabel: "Entendido",
            allowProceed: false
          });
          return;
        }

        if (validation.warnings.length > 0) {
          const accepted = await openSobrecargaModal({
            title: "Validacion de movimiento",
            message:
              `${personaName} puede quedar con sobrecarga, ser movido a un dia feriado y/o sin descanso minimo de 8 horas con este movimiento.`,
            details: validation.warnings.map((text) => `${personaName}: ${text}`),
            confirmLabel: "Mover de todas formas",
            allowProceed: true
          });
          if (!accepted) {
            return;
          }
        }

        cell.appendChild(draggedCard);
        helpers.updateCurrentPosition(draggedCard, cell);
        if (helpers.isAtOrigin(draggedCard)) {
          reconcileCardHistory(draggedCard);
          return;
        }
        const fromHtml = buildInfoHtml(draggedCard, originDay, originShift, false);
        const toHtml = buildInfoHtml(draggedCard, targetDay, targetShift, false);
        const displayName = draggedCard.querySelector(".card-meta")?.textContent?.trim() || "Sin asignar";
        const fromText = buildInfoText(draggedCard, originDay, originShift);
        const toText = buildInfoText(draggedCard, targetDay, targetShift);
      addStatus(
        `${fromHtml}<span class="status-arrow">-&gt;</span>${toHtml}<span class="status-sep"> - </span><span class="status-name">${displayName}</span>`,
          action,
          `Cambio confirmado: ${fromText}  ${toText}`
        );
      }
    });
  };

  const processStatusAction = async (pill, actionType) => {
    const actionId = pill?.dataset?.actionId || "";
    if (!actionId) return;
    const action = state.actionHistory.get(actionId);
    if (!action) return;

    if (actionType === "confirm") {
      const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
      const token = tokenInput?.value;

      try {
        let payload = null;
        if (action.type === "move") {
          const movedId = action.movedCard?.dataset.assignment || "";
          const current = state.currentPositions.get(movedId);
          if (!movedId || !current) {
            showToast("No se pudo confirmar el movimiento.", "danger");
            return;
          }
          const dayIndex = parseInt((current.day || "").replace("d", ""), 10);
          const weekDates = getWeekDates(state.weekOffset);
          const targetDate = weekDates[dayIndex];
          if (!targetDate) {
            showToast("No se pudo confirmar el movimiento.", "danger");
            return;
          }
          const hasDirectEditPermission = canEditAnyShift();
          const canRequestCambio = canRequestCambioTurno();

          if (!hasDirectEditPermission) {
            if (!canRequestCambio) {
              action.fromCell?.appendChild(action.movedCard);
              helpers.updateCurrentPosition(action.movedCard, action.fromCell);
              removePill(actionId);
              showToast("No tienes permisos para confirmar cambios de turnos.", "danger");
              return;
            }
            const dialog = await openCambioModal({
              message: "El cambio de turno sera enviado para revision de tus superiores.",
              confirmLabel: "Enviar solicitud"
            });
            if (!dialog.confirmed) {
              return;
            }
            if (!dialog.motivo) {
              showToast("Debe indicar un motivo para la solicitud.", "info");
              return;
            }
            const tipoSolicitudId = state.cambioTipoSolicitudId;
            if (!tipoSolicitudId) {
              showToast("Tipo de solicitud no configurado.", "danger");
              return;
            }

            const response = await fetch("/Solicitudes/CreateCambioTurno", {
              method: "POST",
              headers: {
                "Content-Type": "application/json",
                RequestVerificationToken: token || ""
              },
              body: JSON.stringify({
                tipoSolicitudId,
                turnoOrigenId: movedId,
                turnoDestinoId: "",
                fechaDestino: utils.formatIsoDate(targetDate),
                tipoTurnoDestinoId: current.shift || "",
                motivo: dialog.motivo
              })
            });

            if (!response.ok) {
              const data = await response.json().catch(() => ({}));
              action.fromCell?.appendChild(action.movedCard);
              helpers.updateCurrentPosition(action.movedCard, action.fromCell);
              removePill(actionId);
              showToast(data.message || "No se pudo crear la solicitud.", "danger");
              return;
            }

            action.fromCell?.appendChild(action.movedCard);
            helpers.updateCurrentPosition(action.movedCard, action.fromCell);
            removePill(actionId);
            showToast("Solicitud de cambio enviada.", "success");
            await loadWeekData();
            return;
          }

          payload = {
            type: "move",
            move: {
              turnoId: movedId,
              fechaTurno: utils.formatIsoDate(targetDate),
              tipoTurnoId: current.shift || ""
            }
          };
        } else if (action.type === "swap-persona") {
          const hasDirectEditPermission = canEditAnyShift();
          const canRequestCambio = canRequestCambioTurno();
          let message = "El cambio de turno sera enviado para revision de tus superiores.";
          let confirmLabel = "Enviar solicitud";
          if (hasDirectEditPermission) {
            message = "El cambio de turno se aprobara automaticamente en ambas aprobaciones.";
            confirmLabel = "Confirmar cambio";
          } else if (!canRequestCambio) {
            showToast("No tienes permisos para confirmar intercambios.", "danger");
            return;
          }

          const dialog = await openCambioModal({ message, confirmLabel });
          if (!dialog.confirmed) {
            return;
          }

          const firstId = action.movedCard?.dataset.assignment || "";
          const secondId = action.swappedCard?.dataset.assignment || "";
          const firstPersona = action.movedCard?.dataset.personaId || "";
          const secondPersona = action.swappedCard?.dataset.personaId || "";
          if (!firstId || !secondId || !firstPersona || !secondPersona) {
            showToast("No se pudo confirmar el intercambio.", "danger");
            return;
          }

          if (hasDirectEditPermission) {
            payload = {
              type: "swap-persona",
              swap: {
                first: { turnoId: firstId, personaId: firstPersona },
                second: { turnoId: secondId, personaId: secondPersona }
              }
            };
          } else {
            if (!dialog.motivo) {
              showToast("Debe indicar un motivo para la solicitud.", "info");
              return;
            }
            const tipoSolicitudId = state.cambioTipoSolicitudId;
            if (!tipoSolicitudId) {
              showToast("Tipo de solicitud no configurado.", "danger");
              return;
            }

            const response = await fetch("/Solicitudes/CreateCambioTurno", {
              method: "POST",
              headers: {
                "Content-Type": "application/json",
                RequestVerificationToken: token || ""
              },
              body: JSON.stringify({
                tipoSolicitudId,
                turnoOrigenId: firstId,
                turnoDestinoId: secondId,
                fechaDestino: "",
                tipoTurnoDestinoId: "",
                motivo: dialog.motivo
              })
            });

            if (!response.ok) {
              const data = await response.json().catch(() => ({}));
              setCardIdentity(action.movedCard, action.movedData);
              setCardIdentity(action.swappedCard, action.swappedData);
              removePill(actionId);
              showToast(data.message || "No se pudo crear la solicitud.", "danger");
              return;
            }

            setCardIdentity(action.movedCard, action.movedData);
            setCardIdentity(action.swappedCard, action.swappedData);
            removePill(actionId);
            showToast("Solicitud de cambio enviada.", "success");
            await loadWeekData();
            return;
          }
        } else {
          showToast("Accion no soportada.", "danger");
          return;
        }

        const response = await fetch("/RegistroTurno/ConfirmChange", {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            RequestVerificationToken: token || ""
          },
          body: JSON.stringify(payload)
        });

        if (!response.ok) {
          const data = await response.json().catch(() => ({}));
          showToast(data.message || "No se pudo confirmar el cambio.", "danger");
          return;
        }

        showToast(action.toastText || "Cambio confirmado.", "success");
        if (action.type === "move") {
          confirmCardPosition(action.movedCard);
        } else if (action.type === "swap-persona") {
          confirmCardPosition(action.movedCard);
          confirmCardPosition(action.swappedCard);
        }
        removePill(actionId);
      } catch {
        showToast("No se pudo confirmar el cambio.", "danger");
      }
      return;
    }

    if (actionType === "cancel") {
      if (action.type === "swap") {
        action.fromCell?.appendChild(action.movedCard);
        action.toCell?.appendChild(action.swappedCard);
        helpers.updateCurrentPosition(action.movedCard, action.fromCell);
        helpers.updateCurrentPosition(action.swappedCard, action.toCell);
        reconcileCardHistory(action.movedCard);
        reconcileCardHistory(action.swappedCard);
      } else if (action.type === "swap-persona") {
        setCardIdentity(action.movedCard, action.movedData);
        setCardIdentity(action.swappedCard, action.swappedData);
        reconcileCardHistory(action.movedCard);
        reconcileCardHistory(action.swappedCard);
      } else if (action.type === "move") {
        action.fromCell?.appendChild(action.movedCard);
        helpers.updateCurrentPosition(action.movedCard, action.fromCell);
        reconcileCardHistory(action.movedCard);
      }
      removePill(actionId);
      showToast("Cambio cancelado.");
    }
  };

  const handleStatusClick = async (event) => {
    const target = event.target instanceof Element ? event.target : null;
    if (!target || !dom.statusList) return;

    const button = target.closest("button[data-action]");
    const pill = target.closest(".status-pill");
    const actionId = pill?.dataset.actionId;
    if (!pill || !actionId) return;

    if (button) {
      const actionType = button.getAttribute("data-action");
      await processStatusAction(pill, actionType);
      clearCardHighlights();
      highlightedActionId = null;
      updatePendingBar();
      return;
    }

    if (highlightedActionId === actionId) {
      clearCardHighlights();
      highlightedActionId = null;
      return;
    }

    clearCardHighlights();
    highlightedActionId = actionId;
    pill.classList.add("is-highlighted");
    highlightAction(actionId);
  };

  const runBulkStatusAction = async (actionType) => {
    if (isBulkStatusProcessing) return;
    const pills = getPendingPills();
    if (pills.length === 0) {
      updatePendingBar();
      return;
    }

    isBulkStatusProcessing = true;
    updatePendingBar();
    try {
      for (const pill of pills) {
        if (!pill.isConnected || pill.classList.contains("pill-locked")) {
          continue;
        }
        await processStatusAction(pill, actionType);
      }
    } finally {
      isBulkStatusProcessing = false;
      clearCardHighlights();
      highlightedActionId = null;
      updatePendingBar();
    }
  };

  const initMonthSelectors = () => {
    if (!dom.monthSelect || !dom.yearSelect) return;

    dom.monthSelect.innerHTML = api.monthNames
      .map((month, index) => `<option value="${index}">${month}</option>`)
      .join("");

    const years = [];
    for (let year = 2025; year <= 2028; year += 1) {
      years.push(year);
    }
    dom.yearSelect.innerHTML = years
      .map((year) => `<option value="${year}">${year}</option>`)
      .join("");

    dom.monthSelect.value = utils.baseDate.getMonth().toString();
    dom.yearSelect.value = utils.baseDate.getFullYear().toString();
  };

  const updateWeekForMonthSelection = async () => {
    if (!dom.monthSelect || !dom.yearSelect) return;
    const monthIndex = parseInt(dom.monthSelect.value, 10);
    const year = parseInt(dom.yearSelect.value, 10);
    const firstOfMonth = new Date(year, monthIndex, 1);
    const monday = utils.getMondayForDate(firstOfMonth);
    state.weekOffset = utils.getWeeksBetween(utils.baseDate, monday);
    state.currentMonth = monthIndex;
    state.currentYear = year;
    await loadWeekData();
  };

  const updateWeekNavLabels = () => {
    const prevButton = document.querySelector('[data-week-nav="prev"]');
    const nextButton = document.querySelector('[data-week-nav="next"]');
    const prevLabel = state.vistaAmpliaActive ? "Mes anterior" : "Semana anterior";
    const nextLabel = state.vistaAmpliaActive ? "Mes siguiente" : "Semana siguiente";
    if (prevButton) {
      prevButton.setAttribute("aria-label", prevLabel);
    }
    if (nextButton) {
      nextButton.setAttribute("aria-label", nextLabel);
    }
  };

  const navigateByMonth = async (delta) => {
    const baseMonth = Number.isNaN(state.currentMonth)
      ? utils.baseDate.getMonth()
      : state.currentMonth;
    const baseYear = Number.isNaN(state.currentYear)
      ? utils.baseDate.getFullYear()
      : state.currentYear;
    const nextDate = new Date(baseYear, baseMonth + delta, 1);

    state.currentMonth = nextDate.getMonth();
    state.currentYear = nextDate.getFullYear();

    const firstOfMonth = new Date(state.currentYear, state.currentMonth, 1);
    const monday = utils.getMondayForDate(firstOfMonth);
    state.weekOffset = utils.getWeeksBetween(utils.baseDate, monday);

    await loadWeekData();
  };

  const isInteractiveKeyboardTarget = (target) => {
    if (!(target instanceof HTMLElement)) return false;
    return !!target.closest(
      'input, textarea, select, button, a, [contenteditable=""], [contenteditable="true"], .flatpickr-calendar, .cell-popover, .context-menu'
    );
  };

  const bindKeyboardWeekNavigation = () => {
    document.addEventListener("keydown", async (event) => {
      if (event.defaultPrevented) return;
      if (event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return;
      if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") return;
      if (document.querySelector(".modal.show")) return;
      if (isInteractiveKeyboardTarget(event.target)) return;

      event.preventDefault();
      if (state.vistaAmpliaActive) {
        await navigateByMonth(event.key === "ArrowRight" ? 1 : -1);
        return;
      }

      state.weekOffset += event.key === "ArrowRight" ? 1 : -1;
      await loadWeekData();
    });
  };

  const bindPreviewAutoUpdate = (element) => {
    if (!element) return;
    element.addEventListener("change", () => {
      applyPreview();
    });
  };

  let cellPopover = null;
  let cellPopoverCleanup = null;

  const toggleSelectedCell = (cell) => {
    if (!cell) return;
    const key = `${cell.dataset.day || ""}:${cell.dataset.shift || ""}`;
    if (!key || key === ":") return;
    if (state.selectedPreviewCells.has(key)) {
      state.selectedPreviewCells.delete(key);
    } else {
      state.selectedPreviewCells.add(key);
    }
    rerenderCalendarPreservingPendingChanges();
  };

  const clearSelectedCells = () => {
    if (state.selectedPreviewCells.size === 0) return;
    state.selectedPreviewCells.clear();
    rerenderCalendarPreservingPendingChanges();
  };

  const updateBulkDeleteButton = () => {
    const count = state.selectedPersistedTurnos?.size ?? 0;
    if (dom.deleteCurrentWeekBtn) {
      dom.deleteCurrentWeekBtn.hidden = count > 0;
    }
    if (dom.deleteSelectedTurnosBtn) {
      dom.deleteSelectedTurnosBtn.hidden = count === 0;
      dom.deleteSelectedTurnosBtn.disabled = count === 0;
      dom.deleteSelectedTurnosBtn.setAttribute(
        "aria-label",
        count > 0 ? `Eliminar ${count} turno(s) seleccionados` : "Eliminar turnos seleccionados"
      );
      dom.deleteSelectedTurnosBtn.setAttribute(
        "title",
        count > 0 ? `Eliminar ${count} turno(s) seleccionados` : "Selecciona turnos con Ctrl para borrarlos"
      );
    }
  };

  const toggleSelectedPersistedTurno = (card) => {
    if (!card || isReadOnlyCard(card)) return;
    const turnoId = `${card.dataset.assignment || ""}`.trim();
    if (!turnoId) return;
    if (state.selectedPersistedTurnos.has(turnoId)) {
      state.selectedPersistedTurnos.delete(turnoId);
    } else {
      state.selectedPersistedTurnos.add(turnoId);
    }
    rerenderCalendarPreservingPendingChanges();
    updateBulkDeleteButton();
  };

  const clearSelectedPersistedTurnos = () => {
    if (!(state.selectedPersistedTurnos instanceof Set) || state.selectedPersistedTurnos.size === 0) return;
    state.selectedPersistedTurnos.clear();
    rerenderCalendarPreservingPendingChanges();
    updateBulkDeleteButton();
  };

  const syncSelectedPersistedTurnos = () => {
    if (!(state.selectedPersistedTurnos instanceof Set)) return;
    const validIds = new Set(
      (Array.isArray(state.assignments) ? state.assignments : [])
        .map((item) => `${item?.id || ""}`.trim())
        .filter(Boolean)
    );
    state.selectedPersistedTurnos.forEach((turnoId) => {
      if (!validIds.has(turnoId)) {
        state.selectedPersistedTurnos.delete(turnoId);
      }
    });
    updateBulkDeleteButton();
  };

  const getSelectedCells = () => {
    return Array.from(state.selectedPreviewCells)
      .map((key) => {
        const [dayId, shiftId] = key.split(":");
        return { dayId, shiftId };
      })
      .filter((item) => item.dayId && item.shiftId);
  };

  const closeCellPopover = () => {
    if (!cellPopover) return;
    if (cellPopoverCleanup) {
      cellPopoverCleanup();
    }
    cellPopover.remove();
    cellPopover = null;
    cellPopoverCleanup = null;
  };

  const positionCellPopover = (popover, cell) => {
    const rect = cell.getBoundingClientRect();
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;
    const popoverRect = popover.getBoundingClientRect();
    const margin = 12;

    let left = rect.right + margin;
    if (left + popoverRect.width > viewportWidth - margin) {
      left = rect.left - popoverRect.width - margin;
    }
    if (left < margin) {
      left = rect.left + margin;
    }

    let top = rect.top + rect.height / 2 - popoverRect.height / 2;
    if (top + popoverRect.height > viewportHeight - margin) {
      top = viewportHeight - popoverRect.height - margin;
    }
    if (top < margin) {
      top = margin;
    }

    popover.style.left = `${left + window.scrollX}px`;
    popover.style.top = `${top + window.scrollY}px`;
  };

  const fetchGruposForPersona = async (personaId) => {
    if (!personaId) return [];
    const response = await fetch(`/RegistroTurno/GruposPorPersona?personaId=${encodeURIComponent(personaId)}`, {
      headers: { Accept: "application/json" }
    });
    if (!response.ok) return [];
    return response.json();
  };

  const fillGrupoOptions = (select, grupos) => {
    if (!select) return;
    select.innerHTML = "";
    const empty = document.createElement("option");
    empty.value = "";
    empty.textContent = "Sin grupo";
    select.appendChild(empty);
    grupos.forEach((grupo) => {
      const option = document.createElement("option");
      option.value = grupo.value;
      option.textContent = grupo.text;
      select.appendChild(option);
    });
    select.disabled = grupos.length === 0;
  };

  const setTurnoSelection = (shiftId) => {
    if (!dom.turnoSelect) return;
    Array.from(dom.turnoSelect.options).forEach((option) => {
      option.selected = option.value === shiftId;
    });
  };

  const setDateRange = (dayId) => {
    if (!dom.fechaInicioInput || !dom.fechaFinInput) return;
    const dayIndex = parseInt(dayId.replace("d", ""), 10);
    if (Number.isNaN(dayIndex)) return;
    const weekDates = getWeekDates(state.weekOffset);
    const target = weekDates[dayIndex];
    if (!target) return;
    const iso = utils.formatIsoDate(target);
    dom.fechaInicioInput.value = iso;
    dom.fechaFinInput.value = iso;
  };

  const applyPopoverPreview = async ({ personaId, grupoId, shiftId, dayId }) => {
    if (!personaId) return;
    const targets = state.selectedPreviewCells.size > 0
      ? getSelectedCells()
      : [{ dayId, shiftId }];
    if (targets.length === 0) return;

    const personaName = getPersonaDisplayName(personaId);
    const conflictDetails = buildTurnoConflictDetails({ personaId, targets });
    const blockingConflicts = conflictDetails
      .filter((item) => item?.blocking)
      .map((item) => item.text)
      .filter(Boolean);
    const warningConflicts = conflictDetails
      .filter((item) => !item?.blocking)
      .map((item) => item.text)
      .filter(Boolean);
    const sobrecargaDetails = buildSobrecargaDetails({ personaId, targets });
    const feriadoDetails = buildFeriadoWarningDetails({ targets });
    if (blockingConflicts.length > 0) {
      await openSobrecargaModal({
        title: "Asignacion no permitida",
        message:
          "No se puede asignar este turno porque la persona ya tiene el mismo tipo de turno en el mismo dia.",
        details: blockingConflicts,
        confirmLabel: "Entendido",
        allowProceed: false
      });
      return;
    }

    const warningDetails = warningConflicts.concat(
      sobrecargaDetails.map((item) => (typeof item === "string" ? item : item.text || "")),
      feriadoDetails
    ).filter(Boolean);

    if (warningDetails.length > 0) {
      const hasRuleWarnings = warningConflicts.length > 0;
      const hasHolidayWarnings = feriadoDetails.length > 0;
      const accepted = await openSobrecargaModal({
        title: hasRuleWarnings || hasHolidayWarnings ? "Validacion de turno" : "Turno con sobrecarga",
        message:
          hasRuleWarnings || hasHolidayWarnings
            ? `${personaName} tiene validaciones en esta asignacion (turnos del mismo dia, descanso minimo de 8 horas o dia feriado). Revisa los detalles antes de continuar.`
            : `${personaName} ya tiene sus turnos completos en esta semana. Si agregas este turno sera un sobrecargo.`,
        details: warningDetails,
        confirmLabel: hasRuleWarnings || hasHolidayWarnings ? "Agregar de todas formas" : "Agregar con sobrecarga",
        allowProceed: true
      });
      if (!accepted) return;
    }

    if (dom.personaSelect) {
      dom.personaSelect.value = personaId;
    }
    await loadGruposForPersona(personaId);
    if (dom.grupoSelect) {
      dom.grupoSelect.value = grupoId || "";
    }
    for (const target of targets) {
      setTurnoSelection(target.shiftId);
      setDateRange(target.dayId);
      applyPreview({ append: true });
    }
    clearSelectedCells();
    closeCellPopover();
  };

  const openCellPopover = (cell, { force } = {}) => {
    closeCellPopover();
    if (!cell) return;
    if (!force && cell.querySelector(".shift-card")) return;
    if (state.selectedPreviewCells.size > 0 && !state.selectedPreviewCells.has(`${cell.dataset.day}:${cell.dataset.shift}`)) {
      clearSelectedCells();
    }
    const dayId = cell.dataset.day || "";
    const shiftId = cell.dataset.shift || "";

    const personaOptions = dom.personaSelect
      ? Array.from(dom.personaSelect.options)
          .filter((option) => option.value)
          .map(
            (option) =>
              `<option value="${option.value}">${option.textContent?.trim() ?? ""}</option>`
          )
          .join("")
      : "";

    const popover = document.createElement("div");
    popover.className = "cell-popover";
    popover.innerHTML = `
      <div class="cell-popover-header">
        <strong>Asignar turno</strong>
        <button type="button" class="cell-popover-close" aria-label="Cerrar">×</button>
      </div>
      <div class="cell-popover-body">
        <label class="cell-popover-field">
          <span>Persona</span>
          <select class="cell-popover-persona">
            <option value="">Seleccionar persona</option>
            ${personaOptions}
          </select>
        </label>
        <label class="cell-popover-field">
          <span>Grupo</span>
          <select class="cell-popover-grupo" disabled>
            <option value="">Sin grupo</option>
          </select>
        </label>
        <div class="cell-popover-extra" hidden>
          <span class="cell-popover-extra-check" aria-hidden="true">✓</span>
          <span>Este turno se marcara como turno extra</span>
        </div>
        <button type="button" class="btn-primary cell-popover-assign" disabled>Asignar</button>
      </div>
    `;

    document.body.appendChild(popover);
    positionCellPopover(popover, cell);

    const personaSelect = popover.querySelector(".cell-popover-persona");
    const grupoSelect = popover.querySelector(".cell-popover-grupo");
    const extraHint = popover.querySelector(".cell-popover-extra");
    const assignBtn = popover.querySelector(".cell-popover-assign");
    const closeButton = popover.querySelector(".cell-popover-close");
    const isPopoverInteractionTarget = (target) =>
      target instanceof Node && (popover.contains(target) || cell.contains(target));
    const isPopoverSelectActive = () => {
      const active = document.activeElement;
      return active instanceof HTMLElement &&
        popover.contains(active) &&
        (active.matches("select") || active.closest("select"));
    };

    const updateExtraHint = () => {
      const personaId = personaSelect?.value || "";
      if (!personaId || !extraHint) {
        if (extraHint) {
          extraHint.hidden = true;
        }
        return;
      }

      const isHolidayDay =
        Array.isArray(state.holidaysByDay?.[dayId]) &&
        state.holidaysByDay[dayId].length > 0;
      if (isHolidayDay) {
        extraHint.hidden = true;
        return;
      }

      const projectedCount = getProjectedCountForTarget({
        personaId,
        target: { dayId, shiftId }
      });

      extraHint.hidden = projectedCount < 6;
    };

    const updateAssignButton = () => {
      if (!assignBtn) {
        return;
      }
      const personaId = personaSelect?.value || "";
      assignBtn.disabled = !personaId;
    };

    const handlePersonaChange = async (event) => {
      const personaId = event.target.value;
      try {
        const grupos = await fetchGruposForPersona(personaId);
        fillGrupoOptions(grupoSelect, grupos);
      } catch {
        fillGrupoOptions(grupoSelect, []);
      } finally {
        updateExtraHint();
        updateAssignButton();
      }
    };

    if (personaSelect) {
      personaSelect.addEventListener("change", handlePersonaChange);
    }

    if (grupoSelect) {
      grupoSelect.addEventListener("change", () => {
        updateExtraHint();
        updateAssignButton();
      });
    }

    assignBtn?.addEventListener("click", async () => {
      const personaId = personaSelect?.value || "";
      const grupoId = grupoSelect?.value || "";
      if (!personaId) {
        return;
      }

      await applyPopoverPreview({ personaId, grupoId, shiftId, dayId });
    });

    updateExtraHint();
    updateAssignButton();

    popover.addEventListener("mousedown", (event) => {
      event.stopPropagation();
    });
    popover.addEventListener("click", (event) => {
      event.stopPropagation();
    });

    const handleDocumentClick = (event) => {
      const target = event.target;
      if (isPopoverInteractionTarget(target)) return;
      if (isPopoverSelectActive()) return;
      closeCellPopover();
    };

    const handleKeyDown = (event) => {
      if (event.key === "Escape") {
        closeCellPopover();
        clearSelectedCells();
      }
    };

    closeButton?.addEventListener("click", closeCellPopover);
    document.addEventListener("click", handleDocumentClick);
    document.addEventListener("keydown", handleKeyDown);

    cellPopover = popover;
    cellPopoverCleanup = () => {
      document.removeEventListener("click", handleDocumentClick);
      document.removeEventListener("keydown", handleKeyDown);
    };
  };

  const clearGrupos = () => {
    if (!dom.grupoSelect) return;
    dom.grupoSelect.innerHTML = "";
    const option = document.createElement("option");
    option.value = "";
    option.textContent = "Sin grupo";
    dom.grupoSelect.appendChild(option);
    dom.grupoSelect.disabled = true;
  };

  const refreshBootstrapTooltip = (element) => {
    const TooltipCtor = window.bootstrap?.Tooltip;
    if (!TooltipCtor || !element) return;
    try {
      const existing = TooltipCtor.getInstance?.(element);
      if (existing && typeof existing.dispose === "function") {
        existing.dispose();
      }
      new TooltipCtor(element);
    } catch {
      // Keep UI usable even if tooltip plugin fails in this browser session.
    }
  };

  const updateMineToggleButton = () => {
    if (!dom.filterMineToggle) return;
    const active = state.filterMyShifts === true;
    const tooltip = active ? "Mostrar todos los turnos" : "Mostrar solo mis turnos";

    dom.filterMineToggle.classList.toggle("is-active", active);
    dom.filterMineToggle.setAttribute("aria-pressed", active ? "true" : "false");
    dom.filterMineToggle.setAttribute("aria-label", tooltip);
    dom.filterMineToggle.setAttribute("data-bs-title", tooltip);
    dom.filterMineToggle.setAttribute("title", tooltip);

    refreshBootstrapTooltip(dom.filterMineToggle);
  };

  const updateAbsentToggleButton = () => {
    if (!dom.filterAbsentToggle) return;
    const active = state.showAbsentPeople === true;
    const tooltip = active
      ? "Ocultar personas ausentes por calamidad"
      : "Mostrar personas ausentes por calamidad";

    dom.filterAbsentToggle.classList.toggle("is-active", active);
    dom.filterAbsentToggle.setAttribute("aria-pressed", active ? "true" : "false");
    dom.filterAbsentToggle.setAttribute("aria-label", tooltip);
    dom.filterAbsentToggle.setAttribute("data-bs-title", tooltip);
    dom.filterAbsentToggle.setAttribute("title", tooltip);

    refreshBootstrapTooltip(dom.filterAbsentToggle);
  };

  const updateVistaAmpliaToggleButton = () => {
    if (!dom.vistaAmpliaToggle) return;
    const active = state.vistaAmpliaActive === true;
    const label = active ? "Vista semanal" : "Vista amplia";
    const tooltip = active ? "Cambiar a vista semanal" : "Cambiar a vista amplia";
    const labelEl = dom.vistaAmpliaToggle.querySelector("[data-vista-amplia-label]");

    dom.vistaAmpliaToggle.classList.toggle("is-active", active);
    dom.vistaAmpliaToggle.setAttribute("aria-pressed", active ? "true" : "false");
    dom.vistaAmpliaToggle.setAttribute("aria-label", tooltip);
    dom.vistaAmpliaToggle.setAttribute("data-bs-title", tooltip);
    dom.vistaAmpliaToggle.setAttribute("title", tooltip);

    if (labelEl) {
      labelEl.textContent = label;
    } else {
      dom.vistaAmpliaToggle.textContent = label;
    }

    refreshBootstrapTooltip(dom.vistaAmpliaToggle);
  };

  const filterPersonasByGrupo = () => {
    if (!dom.filterPersonaSelect) return;
    const grupoId = dom.filterGrupoSelect?.value || "";
    Array.from(dom.filterPersonaSelect.options).forEach((option) => {
      if (!option.value) {
        option.hidden = false;
        option.disabled = false;
        return;
      }
      const grupos = (option.dataset.grupos || "")
        .split(",")
        .map((value) => value.trim())
        .filter(Boolean);
      const matches = !grupoId || grupos.includes(grupoId);
      option.hidden = !matches;
      option.disabled = !matches;
    });

    if (
      dom.filterPersonaSelect.value &&
      dom.filterPersonaSelect.selectedOptions[0]?.disabled
    ) {
      dom.filterPersonaSelect.value = "";
    }
  };

  const applyAssignmentFilters = () => {
    if (typeof api.render?.applyAssignmentFilters === "function") {
      api.render.applyAssignmentFilters();
      updateEmptyState();
    }
  };

  const loadGruposForPersona = async (personaId) => {
    if (!dom.grupoSelect) return;
    if (!personaId) {
      clearGrupos();
      return;
    }

    try {
      const response = await fetch(`/RegistroTurno/GruposPorPersona?personaId=${encodeURIComponent(personaId)}`, {
        headers: { Accept: "application/json" }
      });
      if (!response.ok) {
        throw new Error("Bad response");
      }
      const grupos = await response.json();
      dom.grupoSelect.innerHTML = "";
      const empty = document.createElement("option");
      empty.value = "";
      empty.textContent = "Sin grupo";
      dom.grupoSelect.appendChild(empty);
      grupos.forEach((grupo) => {
        const option = document.createElement("option");
        option.value = grupo.value;
        option.textContent = grupo.text;
        if (grupo.color) {
          option.dataset.color = grupo.color;
        }
        option.dataset.name = grupo.text;
        dom.grupoSelect.appendChild(option);
      });
      dom.grupoSelect.disabled = grupos.length === 0;
    } catch {
      clearGrupos();
    }
  };

  const openPanel = () => {
    if (!dom.panel || !dom.panelScrim) return;
    dom.panel.hidden = false;
    dom.panelScrim.hidden = false;
    requestAnimationFrame(() => {
      dom.panel.classList.add("is-open");
      dom.panelScrim.classList.add("is-open");
    });
  };

  const openCambioModal = ({ message, confirmLabel }) => {
    const modalEl = document.getElementById("cambioTurnoModal");
    if (!modalEl) {
      return Promise.resolve({ confirmed: window.confirm(message), motivo: "" });
    }

    const messageEl = modalEl.querySelector("[data-cambio-message]");
    const motivoInput = modalEl.querySelector("[data-cambio-motivo]");
    const confirmBtn = modalEl.querySelector("[data-cambio-confirm]");
    if (messageEl) {
      messageEl.textContent = message;
    }
    if (confirmBtn && confirmLabel) {
      confirmBtn.textContent = confirmLabel;
    }
    if (motivoInput) {
      motivoInput.value = "";
    }

    const modal = window.bootstrap?.Modal
      ? new window.bootstrap.Modal(modalEl)
      : null;

    return new Promise((resolve) => {
      const handleConfirm = () => {
        const motivo = (motivoInput?.value || "").trim();
        cleanup();
        resolve({ confirmed: true, motivo });
        modal?.hide();
      };

      const handleHidden = () => {
        cleanup();
        resolve({ confirmed: false, motivo: "" });
      };

      const cleanup = () => {
        confirmBtn?.removeEventListener("click", handleConfirm);
        modalEl.removeEventListener("hidden.bs.modal", handleHidden);
      };

      confirmBtn?.addEventListener("click", handleConfirm);
      modalEl.addEventListener("hidden.bs.modal", handleHidden, { once: true });
      if (modal) {
        modal.show();
      } else {
        const confirmed = window.confirm(message);
        cleanup();
        resolve({ confirmed, motivo: "" });
      }
    });
  };

  const openDeleteTurnoModal = ({ title, message, confirmLabel }) => {
    const modalEl = document.getElementById("deleteTurnoModal");
    if (!modalEl) {
      return Promise.resolve(window.confirm(message));
    }

    const titleEl = modalEl.querySelector("[data-delete-title]");
    const messageEl = modalEl.querySelector("[data-delete-message]");
    const confirmBtn = modalEl.querySelector("[data-delete-confirm]");
    const defaultTitle = titleEl?.dataset.defaultTitle
      || titleEl?.textContent?.trim()
      || "Eliminar turno";
    const defaultConfirmLabel = confirmBtn?.dataset.defaultLabel
      || confirmBtn?.textContent?.trim()
      || "Eliminar";
    if (titleEl && !titleEl.dataset.defaultTitle) {
      titleEl.dataset.defaultTitle = defaultTitle;
    }
    if (confirmBtn && !confirmBtn.dataset.defaultLabel) {
      confirmBtn.dataset.defaultLabel = defaultConfirmLabel;
    }
    if (titleEl) {
      titleEl.textContent = title || defaultTitle;
    }
    if (messageEl) {
      messageEl.textContent = message;
    }
    if (confirmBtn) {
      confirmBtn.textContent = confirmLabel || defaultConfirmLabel;
    }

    const modal = window.bootstrap?.Modal
      ? new window.bootstrap.Modal(modalEl)
      : null;

    return new Promise((resolve) => {
      let settled = false;

      const cleanup = () => {
        confirmBtn?.removeEventListener("click", handleConfirm);
        modalEl.removeEventListener("keydown", handleKeyDown);
        modalEl.removeEventListener("shown.bs.modal", handleShown);
        modalEl.removeEventListener("hidden.bs.modal", handleHidden);
      };

      const settle = (confirmed) => {
        if (settled) return;
        settled = true;
        cleanup();
        resolve(confirmed);
      };

      const handleConfirm = () => {
        settle(true);
        modal?.hide();
      };

      const handleShown = () => {
        confirmBtn?.focus();
      };

      const handleKeyDown = (event) => {
        if (event.key !== "Enter" || event.defaultPrevented || event.isComposing) {
          return;
        }

        const target = event.target;
        if (target instanceof HTMLElement) {
          if (target.closest('[data-bs-dismiss="modal"]')) {
            return;
          }
          if (target !== confirmBtn && target.closest("button, a, input, textarea, select")) {
            return;
          }
        }

        event.preventDefault();
        handleConfirm();
      };

      const handleHidden = () => {
        settle(false);
      };

      confirmBtn?.addEventListener("click", handleConfirm);
      modalEl.addEventListener("keydown", handleKeyDown);
      modalEl.addEventListener("shown.bs.modal", handleShown, { once: true });
      modalEl.addEventListener("hidden.bs.modal", handleHidden, { once: true });

      if (modal) {
        modal.show();
      } else {
        settle(window.confirm(message));
      }
    });
  };

  const getPersonaDisplayName = (personaId) => {
    if (!personaId || !dom.personaSelect) {
      return "Esta persona";
    }
    const option = Array.from(dom.personaSelect.options).find((item) => item.value === personaId);
    return option?.textContent?.trim() || "Esta persona";
  };

  const getShiftDisplayLabel = (shiftId) => {
    const shift = state.shifts.find((item) => item.id === shiftId);
    if (!shift) {
      return shiftId || "Turno";
    }
    const shortLabel = shift.shortLabel ?? shift.short ?? shift.id;
    const rangeLabel = shift.label ? ` (${shift.label})` : "";
    return `${shortLabel}${rangeLabel}`;
  };

  const getDateForDayId = (dayId) => {
    const dayIndex = parseInt((dayId || "").replace("d", ""), 10);
    if (Number.isNaN(dayIndex)) {
      return null;
    }
    const weekDates = getWeekDates(state.weekOffset);
    return weekDates[dayIndex] || null;
  };

  const parseIsoDate = (value) => {
    const match = `${value || ""}`.trim().match(/^(\d{4})-(\d{2})-(\d{2})$/);
    if (!match) {
      return null;
    }

    const year = parseInt(match[1], 10);
    const month = parseInt(match[2], 10);
    const day = parseInt(match[3], 10);
    if ([year, month, day].some((part) => Number.isNaN(part))) {
      return null;
    }

    return new Date(year, month - 1, day);
  };

  const getAssignmentDate = (assignment) => {
    if (!assignment) {
      return null;
    }

    const explicitDate =
      parseIsoDate(assignment.absoluteDateIso) ||
      parseIsoDate(assignment.dateIso) ||
      parseIsoDate(assignment.date);
    if (explicitDate) {
      return explicitDate;
    }

    return getDateForDayId(assignment.day || "");
  };

  const getWeekKeyForDate = (date) => {
    if (!(date instanceof Date) || Number.isNaN(date.getTime())) {
      return "";
    }
    const monday = utils.getMondayForDate(date);
    return utils.formatIsoDate(monday);
  };

  const formatShortDate = (date) => {
    if (!(date instanceof Date) || Number.isNaN(date.getTime())) {
      return "";
    }
    return `${date.getDate()} ${monthNames[date.getMonth()] || ""}`.trim();
  };

  const parseTimeToMinutes = (value) => {
    const match = (value || "").trim().match(/^(\d{1,2}):(\d{2})$/);
    if (!match) {
      return null;
    }
    const hours = parseInt(match[1], 10);
    const minutes = parseInt(match[2], 10);
    if (Number.isNaN(hours) || Number.isNaN(minutes)) {
      return null;
    }
    if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59) {
      return null;
    }
    return hours * 60 + minutes;
  };

  const parseShiftWindow = (shiftId) => {
    const shift = state.shifts.find((item) => item.id === shiftId);
    if (!shift) {
      return null;
    }

    const rawLabel = shift.label || "";
    const parts = rawLabel.split("-").map((item) => item.trim());
    if (parts.length !== 2) {
      return null;
    }

    const startMinutes = parseTimeToMinutes(parts[0]);
    const endMinutes = parseTimeToMinutes(parts[1]);
    if (startMinutes === null || endMinutes === null) {
      return null;
    }

    const spansNextDay = endMinutes <= startMinutes;
    return {
      shiftId,
      shortLabel: shift.shortLabel ?? shift.short ?? shiftId,
      label: shift.label || "",
      startMinutes,
      endMinutes,
      spansNextDay
    };
  };

  const buildTurnoInterval = ({ dayId, shiftId, date }) => {
    const dayDate = date instanceof Date && !Number.isNaN(date.getTime())
      ? new Date(date.getFullYear(), date.getMonth(), date.getDate())
      : getDateForDayId(dayId);
    const shiftWindow = parseShiftWindow(shiftId);
    if (!dayDate || !shiftWindow) {
      return null;
    }

    const start = new Date(dayDate);
    start.setHours(
      Math.floor(shiftWindow.startMinutes / 60),
      shiftWindow.startMinutes % 60,
      0,
      0
    );

    const end = new Date(dayDate);
    if (shiftWindow.spansNextDay) {
      end.setDate(end.getDate() + 1);
    }
    end.setHours(
      Math.floor(shiftWindow.endMinutes / 60),
      shiftWindow.endMinutes % 60,
      0,
      0
    );

    return {
      dayId,
      dayLabel: state.dayLabelsById[dayId] || weekdayLabels[dayDate.getDay()] || "Dia",
      date: dayDate,
      shiftId,
      shiftLabel: getShiftDisplayLabel(shiftId),
      start,
      end
    };
  };

  const buildTurnoConflictDetails = ({ personaId, targets, excludeAssignmentIds = [] }) => {
    if (!personaId || !Array.isArray(targets) || targets.length === 0) {
      return [];
    }

    const excludedIds = new Set((excludeAssignmentIds || []).filter(Boolean));
    const validationAssignments = [];
    const validationAssignmentKeys = new Set();
    const registerValidationAssignments = (assignments, scopeKey) => {
      if (!Array.isArray(assignments)) {
        return;
      }

      assignments.forEach((assignment, index) => {
        if (!assignment) {
          return;
        }

        const assignmentDate = getAssignmentDate(assignment);
        const fallbackDateKey = assignmentDate ? utils.formatIsoDate(assignmentDate) : (assignment.day || "");
        const assignmentKey = assignment.id
          ? `id:${assignment.id}`
          : `anon:${scopeKey}:${assignment.personaId || ""}:${assignment.shift || ""}:${fallbackDateKey}:${index}`;

        if (validationAssignmentKeys.has(assignmentKey)) {
          return;
        }

        validationAssignmentKeys.add(assignmentKey);
        validationAssignments.push(assignment);
      });
    };

    registerValidationAssignments(state.assignments, "current-assignments");
    registerValidationAssignments(state.previewAssignments, "current-preview");

    const currentWeekStart = getWeekDates(state.weekOffset)[0];
    const currentWeekKey = currentWeekStart ? utils.formatIsoDate(currentWeekStart) : "";
    if (state.validationWeekCache instanceof Map) {
      state.validationWeekCache.forEach((cachedWeek, weekKey) => {
        if (!cachedWeek || weekKey === currentWeekKey) {
          return;
        }

        registerValidationAssignments(cachedWeek.assignments, `${weekKey}:assignments`);
        registerValidationAssignments(cachedWeek.previewAssignments, `${weekKey}:preview`);
      });
    }

    const personaAssignments = validationAssignments
      .filter(
        (assignment) =>
          (assignment.personaId || "") === personaId &&
          !excludedIds.has(assignment.id || "")
      )
      .map((assignment) =>
        buildTurnoInterval({
          dayId: assignment.day,
          shiftId: assignment.shift,
          date: getAssignmentDate(assignment)
        })
      )
      .filter(Boolean);

    const uniqueTargets = Array.from(
      new Map(
        targets
          .filter((target) => target?.dayId && target?.shiftId)
          .map((target) => [`${target.dayId}:${target.shiftId}`, target])
      ).values()
    );

    const targetIntervals = uniqueTargets
      .map((target) => buildTurnoInterval({ dayId: target.dayId, shiftId: target.shiftId }))
      .filter(Boolean)
      .sort((a, b) => a.start.getTime() - b.start.getTime());

    const ms8Hours = 8 * 60 * 60 * 1000;
    const detailKeys = new Set();
    const details = [];
    const simulated = personaAssignments.slice();
    const getDateKey = (interval) =>
      interval?.date instanceof Date && !Number.isNaN(interval.date.getTime())
        ? utils.formatIsoDate(interval.date)
        : (interval?.dayId || "");

    targetIntervals.forEach((targetInterval) => {
      simulated.forEach((existingInterval) => {
        const existingDateKey = getDateKey(existingInterval);
        const targetDateKey = getDateKey(targetInterval);
        const sameDay = existingDateKey && targetDateKey && existingDateKey === targetDateKey;
        if (sameDay) {
          const key = `same:${targetDateKey}:${existingInterval.shiftId}:${targetInterval.shiftId}`;
          if (!detailKeys.has(key)) {
            detailKeys.add(key);
            const sameShift = existingInterval.shiftId === targetInterval.shiftId;
            details.push({
              text: sameShift
                ? `${targetInterval.dayLabel} ${formatShortDate(targetInterval.date)}: ya tiene ${existingInterval.shiftLabel}. No se puede repetir el mismo turno en el mismo dia.`
                : `${targetInterval.dayLabel} ${formatShortDate(targetInterval.date)}: ya tiene ${existingInterval.shiftLabel}. Nuevo: ${targetInterval.shiftLabel}.`,
              blocking: sameShift
            });
          }
          // Same-day conflicts are already covered above.
          // Avoid adding additional overlap/rest messages for the same date.
          return;
        }

        const overlap =
          targetInterval.start.getTime() < existingInterval.end.getTime() &&
          targetInterval.end.getTime() > existingInterval.start.getTime();
        if (overlap) {
          const key = `overlap:${existingDateKey}:${existingInterval.shiftId}:${targetDateKey}:${targetInterval.shiftId}`;
          if (!detailKeys.has(key)) {
            detailKeys.add(key);
            details.push({
              text: `Descanso menor a 8h entre ${existingInterval.shiftLabel} (${existingInterval.dayLabel} ${formatShortDate(existingInterval.date)}) y ${targetInterval.shiftLabel} (${targetInterval.dayLabel} ${formatShortDate(targetInterval.date)}).`,
              blocking: false
            });
          }
          return;
        }

        const gapAfterExisting = targetInterval.start.getTime() - existingInterval.end.getTime();
        if (gapAfterExisting > 0 && gapAfterExisting < ms8Hours) {
          const key = `rest-after:${existingDateKey}:${existingInterval.shiftId}:${targetDateKey}:${targetInterval.shiftId}`;
          if (!detailKeys.has(key)) {
            detailKeys.add(key);
            details.push({
              text: `Descanso menor a 8h entre ${existingInterval.shiftLabel} (${existingInterval.dayLabel} ${formatShortDate(existingInterval.date)}) y ${targetInterval.shiftLabel} (${targetInterval.dayLabel} ${formatShortDate(targetInterval.date)}).`,
              blocking: false
            });
          }
        }

        const gapBeforeExisting = existingInterval.start.getTime() - targetInterval.end.getTime();
        if (gapBeforeExisting > 0 && gapBeforeExisting < ms8Hours) {
          const key = `rest-before:${targetDateKey}:${targetInterval.shiftId}:${existingDateKey}:${existingInterval.shiftId}`;
          if (!detailKeys.has(key)) {
            detailKeys.add(key);
            details.push({
              text: `Descanso menor a 8h entre ${targetInterval.shiftLabel} (${targetInterval.dayLabel} ${formatShortDate(targetInterval.date)}) y ${existingInterval.shiftLabel} (${existingInterval.dayLabel} ${formatShortDate(existingInterval.date)}).`,
              blocking: false
            });
          }
        }
      });

      simulated.push(targetInterval);
    });

    return details;
  };

  const buildSobrecargaDetails = ({ personaId, targets, excludeAssignmentIds = [] }) => {
    if (!personaId || !Array.isArray(targets) || targets.length === 0) {
      return [];
    }

    const weekDates = getWeekDates(state.weekOffset);
    const countsByWeek = new Map();
    const excludedIds = new Set((excludeAssignmentIds || []).filter(Boolean));
    const allAssignments = state.assignments.concat(state.previewAssignments);

    allAssignments.forEach((assignment) => {
      if (excludedIds.has(assignment.id || "")) {
        return;
      }
      if ((assignment.personaId || "") !== personaId) {
        return;
      }
      const dayIndex = parseInt((assignment.day || "").replace("d", ""), 10);
      if (Number.isNaN(dayIndex) || dayIndex < 0 || dayIndex >= weekDates.length) {
        return;
      }
      const weekKey = getWeekKeyForDate(weekDates[dayIndex]);
      if (!weekKey) {
        return;
      }
      countsByWeek.set(weekKey, (countsByWeek.get(weekKey) || 0) + 1);
    });

    const uniqueTargets = Array.from(
      new Map(
        targets
          .filter((target) => target?.dayId && target?.shiftId)
          .map((target) => [`${target.dayId}:${target.shiftId}`, target])
      ).values()
    );

    const overloadDetails = [];
    uniqueTargets.forEach((target) => {
      const targetDate = getDateForDayId(target.dayId);
      if (!targetDate) {
        return;
      }
      const weekKey = getWeekKeyForDate(targetDate);
      if (!weekKey) {
        return;
      }

      const projectedCount = (countsByWeek.get(weekKey) || 0) + 1;
      countsByWeek.set(weekKey, projectedCount);
      if (projectedCount <= 5) {
        return;
      }

      const dayLabel = state.dayLabelsById[target.dayId] || "Dia";
      const dateLabel = `${targetDate.getDate()} ${monthNames[targetDate.getMonth()] || ""}`.trim();
      overloadDetails.push({
        text: `${dayLabel} ${dateLabel} - ${getShiftDisplayLabel(target.shiftId)}`,
        projectedCount
      });
    });

    return overloadDetails;
  };

  const getProjectedCountForTarget = ({ personaId, target, excludeAssignmentIds = [] }) => {
    if (!personaId || !target?.dayId || !target?.shiftId) {
      return 0;
    }

    const weekDates = getWeekDates(state.weekOffset);
    const countsByWeek = new Map();
    const excludedIds = new Set((excludeAssignmentIds || []).filter(Boolean));
    const allAssignments = state.assignments.concat(state.previewAssignments);

    allAssignments.forEach((assignment) => {
      if (excludedIds.has(assignment.id || "")) {
        return;
      }
      if ((assignment.personaId || "") !== personaId) {
        return;
      }
      const dayIndex = parseInt((assignment.day || "").replace("d", ""), 10);
      if (Number.isNaN(dayIndex) || dayIndex < 0 || dayIndex >= weekDates.length) {
        return;
      }
      const weekKey = getWeekKeyForDate(weekDates[dayIndex]);
      if (!weekKey) {
        return;
      }
      countsByWeek.set(weekKey, (countsByWeek.get(weekKey) || 0) + 1);
    });

    const targetDate = getDateForDayId(target.dayId);
    if (!targetDate) {
      return 0;
    }

    const weekKey = getWeekKeyForDate(targetDate);
    if (!weekKey) {
      return 0;
    }

    return (countsByWeek.get(weekKey) || 0) + 1;
  };

  const buildFeriadoWarningDetails = ({ targets }) => {
    if (!Array.isArray(targets) || targets.length === 0) {
      return [];
    }

    const dayIds = Array.from(
      new Set(
        targets
          .map((target) => target?.dayId || "")
          .filter(Boolean)
      )
    );

    const details = [];
    dayIds.forEach((dayId) => {
      const holidayNames = Array.isArray(state.holidaysByDay?.[dayId])
        ? state.holidaysByDay[dayId].filter((name) => typeof name === "string" && name.trim().length > 0)
        : [];
      if (holidayNames.length === 0) {
        return;
      }

      const date = getDateForDayId(dayId);
      const dayLabel = state.dayLabelsById[dayId] || "Dia";
      const dateLabel = date ? formatShortDate(date) : "";
      const feriadoNames = holidayNames.join(", ");
      details.push(
        `${dayLabel}${dateLabel ? ` ${dateLabel}` : ""} es feriado${feriadoNames ? ` (${feriadoNames})` : ""}. Si asignas este turno generara sobrecargo.`
      );
    });

    return details;
  };

  const openSobrecargaModal = ({ title, message, details, confirmLabel, allowProceed = true }) => {
    const modalEl = document.getElementById("sobrecargoTurnoModal");
    const fallbackMessage =
      message ||
      "Esta persona ya tiene sus turnos completos en esta semana. Si agregas este turno sera un sobrecargo. Continuar?";
    if (!modalEl) {
      if (!allowProceed) {
        window.alert(fallbackMessage);
        return Promise.resolve(false);
      }
      return Promise.resolve(window.confirm(fallbackMessage));
    }

    const titleEl = modalEl.querySelector("[data-overload-title]");
    const messageEl = modalEl.querySelector("[data-overload-message]");
    const listEl = modalEl.querySelector("[data-overload-list]");
    const confirmBtn = modalEl.querySelector("[data-overload-confirm]");

    if (titleEl) {
      titleEl.textContent = title || "Turno con sobrecarga";
    }
    if (messageEl) {
      messageEl.textContent =
        message ||
        "Esta persona ya tiene sus turnos completos en esta semana. Si agregas este turno sera un sobrecargo.";
    }
    if (listEl) {
      listEl.innerHTML = "";
      (details || []).forEach((item) => {
        const li = document.createElement("li");
        li.textContent = typeof item === "string" ? item : (item?.text || "");
        listEl.appendChild(li);
      });
    }
    if (confirmBtn) {
      confirmBtn.textContent = confirmLabel || "Agregar con sobrecarga";
      confirmBtn.classList.remove("btn-warning", "btn-primary", "btn-secondary");
      confirmBtn.classList.add(allowProceed ? "btn-warning" : "btn-primary");
    }

    const modal = window.bootstrap?.Modal
      ? new window.bootstrap.Modal(modalEl)
      : null;

    return new Promise((resolve) => {
      let settled = false;

      const cleanup = () => {
        confirmBtn?.removeEventListener("click", handleConfirm);
        modalEl.removeEventListener("hidden.bs.modal", handleHidden);
      };

      const settle = (confirmed) => {
        if (settled) return;
        settled = true;
        cleanup();
        resolve(confirmed);
      };

      const handleConfirm = () => {
        settle(allowProceed);
        modal?.hide();
      };

      const handleHidden = () => {
        settle(false);
      };

      confirmBtn?.addEventListener("click", handleConfirm);
      modalEl.addEventListener("hidden.bs.modal", handleHidden, { once: true });

      if (modal) {
        modal.show();
      } else {
        settle(window.confirm(fallbackMessage));
      }
    });
  };

  const isAdminLikeRole = () => {
    const role = `${state.currentUserRole || ""}`.toLowerCase();
    return role === "admin" || role === "superadmin";
  };
  const isLiderRole = () => `${state.currentUserRole || ""}`.toLowerCase() === "lider";
  const isCambioStateActive = (stateValue) => {
    const normalized = `${stateValue || ""}`.trim().toLowerCase();
    return normalized === "requested" || normalized === "inreview";
  };
  const hasActiveCambioCard = (card) =>
    !!card && isCambioStateActive(card.dataset?.cambioState || "");
  const getActiveCambioBlockedMessage = () =>
    isAdminLikeRole() || isLiderRole()
      ? "Este turno ya esta con proceso de cambio activo. Resuelvelo antes de ingresar una nueva solicitud."
      : "Este turno esta en proceso de cambio, no puedes realizar mas cambios.";
  const canEditAnyShift = () => state.canEditAnyShift === true || isAdminLikeRole();
  const canRequestCambioTurno = () =>
    state.canRequestCambioTurno === true || isAdminLikeRole() || isLiderRole();
  const canRequestPermiso = () => state.canRequestPermiso === true || isAdminLikeRole() || isLiderRole();
  const canCreateCalamidad = () => state.canCreateCalamidad === true || isAdminLikeRole() || isLiderRole();
  const canOperateShiftCard = (card) => {
    if (!card) return false;
    if (isExistingCalamidadReplacementCard(card)) return false;
    if (canEditAnyShift()) return true;
    if (!canRequestCambioTurno()) return false;
    const personaId = `${card.dataset.personaId || ""}`.trim();
    return !!personaId && personaId === state.currentPersonaId;
  };

  const getAssignmentById = (assignmentId) => {
    if (!assignmentId) return null;
    const pools = [state.assignments, state.absentAssignments, state.previewAssignments];
    for (const pool of pools) {
      if (!Array.isArray(pool)) continue;
      const found = pool.find((item) => item?.id === assignmentId);
      if (found) return found;
    }

    if (Array.isArray(state.absentAssignments)) {
      const bySourceTurno = state.absentAssignments.find(
        (item) => item?.turnoAusenteId === assignmentId
      );
      if (bySourceTurno) return bySourceTurno;
    }

    return null;
  };

  const getCalamidadSourceTurnoId = (assignment) => {
    if (!assignment) return "";
    return `${assignment.turnoAusenteId || assignment.id || ""}`.trim();
  };

  const isExistingCalamidadReplacementShift = (assignment) => {
    if (!assignment) return false;
    return `${assignment.calamidadReemplazaNombre || ""}`.trim().length > 0;
  };

  const isExistingCalamidadReplacementCard = (card) => {
    if (!card) return false;
    if (`${card.dataset.calamidadReplacement || ""}`.toLowerCase() === "true") {
      return true;
    }

    const assignment = getAssignmentById(card.dataset.assignment || "");
    if (isExistingCalamidadReplacementShift(assignment)) {
      return true;
    }

    const tooltipText = `${card.dataset.tooltip || card.getAttribute("aria-label") || ""}`
      .trim()
      .toLowerCase();
    return tooltipText.includes("reemplaza a");
  };

  const findContextAbsentSource = (sourceTurnoId, context, startDate, endDate) => {
    if (!sourceTurnoId || !context || !Array.isArray(state.absentAssignments)) {
      return null;
    }

    const match = state.absentAssignments.find((item) => {
      const itemSourceId = getCalamidadSourceTurnoId(item);
      return (
        itemSourceId === sourceTurnoId &&
        (item?.personaId || "") === (context.personaId || "")
      );
    });

    if (!match) return null;
    const matchDate = resolveAssignmentDate(match);
    return isDateInRange(matchDate, startDate, endDate) ? match : null;
  };

  const resolveAssignmentDate = (assignment) => {
    return getAssignmentDate(assignment);
  };

  const isDateInRange = (date, start, end) => {
    if (!(date instanceof Date) || !(start instanceof Date) || !(end instanceof Date)) {
      return false;
    }
    const safeDate = new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
    const safeStart = new Date(start.getFullYear(), start.getMonth(), start.getDate()).getTime();
    const safeEnd = new Date(end.getFullYear(), end.getMonth(), end.getDate()).getTime();
    return safeDate >= safeStart && safeDate <= safeEnd;
  };

  const cleanupCalamidadPreviewTooltip = (card) => {
    if (!card) return;
    const baseTooltip = card.dataset.calamidadBaseTooltip;
    if (typeof baseTooltip === "string") {
      if (baseTooltip.trim()) {
        card.dataset.tooltip = baseTooltip;
      } else {
        card.removeAttribute("data-tooltip");
      }
      delete card.dataset.calamidadBaseTooltip;
    }
  };

  const applyCalamidadPreviewTooltip = (card, text) => {
    if (!card || !text) return;
    if (typeof card.dataset.calamidadBaseTooltip === "undefined") {
      card.dataset.calamidadBaseTooltip = card.dataset.tooltip || "";
    }
    const baseTooltip = card.dataset.calamidadBaseTooltip || "";
    card.dataset.tooltip = baseTooltip ? `${baseTooltip} | ${text}` : text;
  };

  const getCalamidadPersonaOptions = (excludePersonaId) => {
    const options = [];
    const seen = new Set();
    if (dom.filterPersonaSelect) {
      Array.from(dom.filterPersonaSelect.options).forEach((option) => {
        const personaId = `${option.value || ""}`.trim();
        const personaNombre = `${option.textContent || ""}`.trim();
        if (!personaId || !personaNombre || personaId === excludePersonaId || seen.has(personaId)) {
          return;
        }
        seen.add(personaId);
        options.push({ personaId, personaNombre });
      });
    }

    if (options.length === 0) {
      state.assignments.forEach((assignment) => {
        const personaId = `${assignment.personaId || ""}`.trim();
        const personaNombre = `${assignment.title || ""}`.trim();
        if (!personaId || !personaNombre || personaId === excludePersonaId || seen.has(personaId)) {
          return;
        }
        seen.add(personaId);
        options.push({ personaId, personaNombre });
      });
    }

    options.sort((a, b) => a.personaNombre.localeCompare(b.personaNombre, "es", { sensitivity: "base" }));
    return options;
  };

  const getCalamidadSourceOptions = (context, startDate, endDate) => {
    if (!context || !(startDate instanceof Date) || !(endDate instanceof Date)) {
      return [];
    }

    const sourceMap = new Map();
    const candidates = state.assignments.concat(
      Array.isArray(state.absentAssignments) ? state.absentAssignments : []
    );

    candidates.forEach((assignment) => {
      if ((assignment?.personaId || "") !== (context.personaId || "")) {
        return;
      }

      const assignmentDate = resolveAssignmentDate(assignment);
      if (!isDateInRange(assignmentDate, startDate, endDate)) {
        return;
      }

      const sourceTurnoId = getCalamidadSourceTurnoId(assignment);
      if (!sourceTurnoId || sourceMap.has(sourceTurnoId)) {
        return;
      }

      const shiftLabel = getShiftDisplayLabel(assignment.shift || "");
      const dateLabel = formatShortDate(assignmentDate);
      const label = `${dateLabel} - ${shiftLabel}`;
      const sortTime =
        assignmentDate instanceof Date && !Number.isNaN(assignmentDate.getTime())
          ? assignmentDate.getTime()
          : Number.MAX_SAFE_INTEGER;

      sourceMap.set(sourceTurnoId, {
        sourceTurnoId,
        label,
        sortTime
      });
    });

    return Array.from(sourceMap.values()).sort((a, b) => {
      if (a.sortTime !== b.sortTime) {
        return a.sortTime - b.sortTime;
      }
      return a.label.localeCompare(b.label, "es", { sensitivity: "base" });
    });
  };

  const syncCalamidadSourceTools = (context, startDate, endDate) => {
    if (!dom.calamidadSourceTools || !dom.calamidadSourceSelect) return;

    if (!context || !(startDate instanceof Date) || !(endDate instanceof Date)) {
      dom.calamidadSourceTools.hidden = true;
      dom.calamidadSourceSelect.innerHTML = '<option value="">Selecciona turno ausente</option>';
      if (dom.calamidadSourceAssignBtn) {
        dom.calamidadSourceAssignBtn.disabled = true;
      }
      return;
    }

    dom.calamidadSourceTools.hidden = false;
    const sourceOptions = getCalamidadSourceOptions(context, startDate, endDate);
    context.sourceOptions = sourceOptions;

    const previousSelected = `${context.selectedSourceTurnoId || dom.calamidadSourceSelect.value || ""}`.trim();
    dom.calamidadSourceSelect.innerHTML = "";

    const placeholder = document.createElement("option");
    placeholder.value = "";
    placeholder.textContent =
      sourceOptions.length > 0
        ? "Selecciona turno ausente"
        : "No hay turnos ausentes en el rango";
    dom.calamidadSourceSelect.appendChild(placeholder);

    sourceOptions.forEach((item) => {
      const option = document.createElement("option");
      option.value = item.sourceTurnoId;
      option.textContent = item.label;
      dom.calamidadSourceSelect.appendChild(option);
    });

    const canKeepSelection = sourceOptions.some((item) => item.sourceTurnoId === previousSelected);
    if (canKeepSelection) {
      dom.calamidadSourceSelect.value = previousSelected;
      context.selectedSourceTurnoId = previousSelected;
    } else {
      dom.calamidadSourceSelect.value = "";
      context.selectedSourceTurnoId = "";
    }

    if (dom.calamidadSourceAssignBtn) {
      dom.calamidadSourceAssignBtn.disabled = !dom.calamidadSourceSelect.value;
    }
  };

  const setCalamidadModeButtonsState = (context) => {
    if (!dom.calamidadModeButtons) return;
    dom.calamidadModeButtons.forEach((button) => {
      const isActive = (button.dataset.calamidadModeBtn || "") === context.modoReemplazo;
      button.classList.toggle("is-active", isActive);
      button.classList.toggle("btn-primary", isActive);
      button.classList.toggle("btn-outline-primary", !isActive);
      button.setAttribute("aria-pressed", isActive ? "true" : "false");
    });
  };

  const syncCalamidadNewTools = (context) => {
    if (!dom.calamidadNewTools || !dom.calamidadNewPersonSelect) return;
    const isNewShiftMode = context.modoReemplazo === "NEW_SHIFT";
    dom.calamidadNewTools.hidden = !isNewShiftMode;
    if (!isNewShiftMode) {
      dom.calamidadNewPersonSelect.value = "";
      if (dom.calamidadNewAssignBtn) {
        dom.calamidadNewAssignBtn.disabled = true;
      }
      return;
    }

    context.availablePersonas = getCalamidadPersonaOptions(context.personaId || "");
    const selectedValue = `${dom.calamidadNewPersonSelect.value || ""}`.trim();
    dom.calamidadNewPersonSelect.innerHTML = "";
    const placeholderOption = document.createElement("option");
    placeholderOption.value = "";
    placeholderOption.textContent = "Selecciona persona para turno nuevo";
    dom.calamidadNewPersonSelect.appendChild(placeholderOption);
    context.availablePersonas.forEach((item) => {
      const option = document.createElement("option");
      option.value = item.personaId;
      option.textContent = item.personaNombre;
      dom.calamidadNewPersonSelect.appendChild(option);
    });
    if (selectedValue && context.availablePersonas.some((item) => item.personaId === selectedValue)) {
      dom.calamidadNewPersonSelect.value = selectedValue;
    }

    if (dom.calamidadNewAssignBtn) {
      dom.calamidadNewAssignBtn.disabled =
        !context.selectedSourceTurnoId || !dom.calamidadNewPersonSelect.value;
    }
  };

  const resolveCalamidadPersonaNombre = (context, personaId, fallbackName = "Persona") => {
    if (!context || !personaId) return fallbackName;
    const list = Array.isArray(context.availablePersonas) ? context.availablePersonas : [];
    const found = list.find((item) => item.personaId === personaId);
    if (found?.personaNombre) return found.personaNombre;
    return fallbackName;
  };

  const updateCalamidadBar = () => {
    if (!dom.calamidadReplaceBar || !dom.calamidadReplaceTitle || !dom.calamidadReplaceStatus) return;
    const context = calamidadReplacementContext;
    if (!context) {
      dom.calamidadReplaceBar.hidden = true;
      syncCalamidadSourceTools(null, null, null);
      if (dom.calamidadSaveReplacements) {
        dom.calamidadSaveReplacements.disabled = true;
      }
      return;
    }

    const startDate = parseIsoDate(context.fechaInicio);
    const endDate = parseIsoDate(context.fechaFin);
    const startText = startDate ? formatShortDate(startDate) : context.fechaInicio;
    const endText = endDate ? formatShortDate(endDate) : context.fechaFin;
    dom.calamidadReplaceTitle.textContent = `Reemplazos para ${context.personaNombre} (${startText} - ${endText})`;
    syncCalamidadSourceTools(context, startDate, endDate);
    const hasSourceOptions = Array.isArray(context.sourceOptions) && context.sourceOptions.length > 0;

    if (context.selectedSourceTurnoId) {
      dom.calamidadReplaceStatus.textContent =
        context.modoReemplazo === "NEW_SHIFT"
          ? "Paso 2: el turno ausente ya esta marcado. Haz clic en una tarjeta de otra persona para crear el turno nuevo."
          : "Paso 2: el turno ausente ya esta marcado. Haz clic en el turno existente que hara el reemplazo.";
    } else if (!hasSourceOptions) {
      dom.calamidadReplaceStatus.textContent = "No hay turnos ausentes disponibles en el rango seleccionado.";
    } else if (context.replacements.size > 0) {
      dom.calamidadReplaceStatus.textContent = `${context.replacements.size} reemplazo(s) listos para guardar.`;
    } else {
      dom.calamidadReplaceStatus.textContent =
        "Paso 1: haz clic en una tarjeta de la persona ausente para marcar el turno que se debe reemplazar.";
    }

    setCalamidadModeButtonsState(context);
    syncCalamidadNewTools(context);

    if (dom.calamidadSaveReplacements) {
      dom.calamidadSaveReplacements.disabled = context.replacements.size === 0;
    }

    dom.calamidadReplaceBar.hidden = false;
  };

  const refreshCalamidadReplacementUi = () => {
    if (!dom.grid) return;

    dom.grid.querySelectorAll(".shift-card").forEach((card) => {
      card.classList.remove(
        "is-calamidad-target",
        "is-calamidad-selected",
        "is-calamidad-replacement-preview",
        "is-calamidad-target-linked"
      );
      cleanupCalamidadPreviewTooltip(card);
    });

    const context = calamidadReplacementContext;
    if (!context) {
      updateCalamidadBar();
      return;
    }

    const startDate = parseIsoDate(context.fechaInicio);
    const endDate = parseIsoDate(context.fechaFin);
    if (!startDate || !endDate) {
      updateCalamidadBar();
      return;
    }

    const safePersonaId = context.personaId || "";
    const sourceAssignments = state.assignments
      .concat(Array.isArray(state.absentAssignments) ? state.absentAssignments : [])
      .filter((assignment) => {
        if ((assignment?.personaId || "") !== safePersonaId) return false;
        const assignmentDate = resolveAssignmentDate(assignment);
        return isDateInRange(assignmentDate, startDate, endDate);
      });

    const targetCardIds = new Set(sourceAssignments.map((assignment) => assignment.id));
    const targetSourceTurnoIds = new Set(
      sourceAssignments
        .map((assignment) => getCalamidadSourceTurnoId(assignment))
        .filter(Boolean)
    );

    state.assignments
      .filter((assignment) => targetSourceTurnoIds.has(getCalamidadSourceTurnoId(assignment)))
      .forEach((assignment) => {
        targetCardIds.add(assignment.id);
      });

    if (
      context.selectedSourceTurnoId &&
      !targetSourceTurnoIds.has(context.selectedSourceTurnoId)
    ) {
      context.selectedSourceTurnoId = "";
    }

    targetCardIds.forEach((cardId) => {
      const selector = escapeSelectorValue(cardId);
      dom.grid
        .querySelectorAll(`.shift-card[data-assignment="${selector}"]`)
        .forEach((card) => {
          card.classList.add("is-calamidad-target");
        });
    });

    if (context.selectedSourceTurnoId) {
      sourceAssignments
        .filter((assignment) => getCalamidadSourceTurnoId(assignment) === context.selectedSourceTurnoId)
        .forEach((assignment) => {
          const selector = escapeSelectorValue(assignment.id);
          dom.grid
            .querySelectorAll(`.shift-card[data-assignment="${selector}"]`)
            .forEach((card) => {
              card.classList.add("is-calamidad-selected");
            });
        });

      const selectedSourceSelector = escapeSelectorValue(context.selectedSourceTurnoId);
      dom.grid
        .querySelectorAll(`.shift-card[data-assignment="${selectedSourceSelector}"]`)
        .forEach((card) => {
          card.classList.add("is-calamidad-selected");
        });
    }

    context.replacements.forEach((replacement, sourceTurnoId) => {
      const sourceSelector = escapeSelectorValue(sourceTurnoId);
      dom.grid
        .querySelectorAll(`.shift-card[data-assignment="${sourceSelector}"]`)
        .forEach((card) => {
          card.classList.add("is-calamidad-target-linked");
          const replacementLabel = replacement?.personaNombre
            ? `${replacement.personaNombre} (${replacement.modoReemplazo === "NEW_SHIFT" ? "turno nuevo" : "intercambio"})`
            : "Reemplazo listo";
          applyCalamidadPreviewTooltip(card, `Reemplazo: ${replacementLabel}`);
        });

      sourceAssignments
        .filter((assignment) => getCalamidadSourceTurnoId(assignment) === sourceTurnoId)
        .forEach((assignment) => {
          const assignmentSelector = escapeSelectorValue(assignment.id);
          dom.grid
            .querySelectorAll(`.shift-card[data-assignment="${assignmentSelector}"]`)
            .forEach((card) => {
              card.classList.add("is-calamidad-target-linked");
              const replacementLabel = replacement?.personaNombre
                ? `${replacement.personaNombre} (${replacement.modoReemplazo === "NEW_SHIFT" ? "turno nuevo" : "intercambio"})`
                : "Reemplazo listo";
              applyCalamidadPreviewTooltip(card, `Reemplazo: ${replacementLabel}`);
            });
        });

      if (replacement?.modoReemplazo === "SWAP" && replacement.turnoReemplazoId) {
        const replacementSelector = escapeSelectorValue(replacement.turnoReemplazoId);
        dom.grid
          .querySelectorAll(`.shift-card[data-assignment="${replacementSelector}"]`)
          .forEach((card) => {
            card.classList.add("is-calamidad-replacement-preview");
            applyCalamidadPreviewTooltip(card, `Reemplaza a ${context.personaNombre}`);
          });
        return;
      }

      if (replacement?.modoReemplazo === "NEW_SHIFT" && replacement.personaReemplazoId) {
        const safePersonaId = escapeSelectorValue(replacement.personaReemplazoId);
        const personaCard = dom.grid.querySelector(`.shift-card[data-persona-id="${safePersonaId}"]`);
        if (personaCard) {
          personaCard.classList.add("is-calamidad-replacement-preview");
          applyCalamidadPreviewTooltip(personaCard, `Turno nuevo para reemplazar a ${context.personaNombre}`);
        }
      }
    });

    updateCalamidadBar();
  };

  const stopCalamidadReplacementMode = ({ force = false } = {}) => {
    if (!calamidadReplacementContext) {
      return true;
    }

    if (!force && calamidadReplacementContext.replacements.size > 0) {
      return false;
    }

    calamidadReplacementContext = null;
    refreshCalamidadReplacementUi();
    return true;
  };

  const startCalamidadReplacementMode = ({ turnoId, personaId, personaNombre, fechaInicio, fechaFin, motivo }) => {
    if (!turnoId || !personaId || !fechaInicio || !fechaFin || !motivo) {
      return;
    }

    if (calamidadReplacementContext) {
      stopCalamidadReplacementMode({ force: true });
    }

    calamidadReplacementContext = {
      turnoId,
      personaId,
      personaNombre: personaNombre || "Persona",
      fechaInicio,
      fechaFin,
      motivo,
      modoReemplazo: "SWAP",
      selectedSourceTurnoId: "",
      sourceOptions: [],
      availablePersonas: [],
      replacements: new Map()
    };

    refreshCalamidadReplacementUi();
    showToast("La calamidad aun no se ha guardado. Selecciona reemplazos o confirma que no son necesarios.", "info");
  };

  const submitCalamidad = async ({
    turnoId,
    personaId,
    fechaInicio,
    fechaFin,
    motivo,
    reemplazos,
    sinReemplazosConfirmado
  }) => {
    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    const token = tokenInput?.value;

    const response = await fetch("/Solicitudes/CreateCalamidad", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        RequestVerificationToken: token || ""
      },
      body: JSON.stringify({
        turnoId,
        personaId,
        fechaInicio,
        fechaFin,
        motivo,
        sinReemplazosConfirmado,
        reemplazos
      })
    });

    const data = await response.json().catch(() => ({}));
    return { response, data };
  };

  const saveCalamidadReplacements = async () => {
    const context = calamidadReplacementContext;
    if (!context) {
      return;
    }

    const reemplazos = Array.from(context.replacements.entries()).map(([turnoAusenteId, replacement]) => ({
      turnoAusenteId,
      turnoReemplazoId: replacement?.modoReemplazo === "SWAP" ? replacement?.turnoReemplazoId || null : null,
      personaReemplazoId: replacement?.modoReemplazo === "NEW_SHIFT" ? replacement?.personaReemplazoId || null : null,
      modoReemplazo: replacement?.modoReemplazo || "SWAP"
    }));

    if (reemplazos.length === 0) {
      showToast("No hay reemplazos por guardar.", "info");
      return;
    }

    try {
      const { response, data } = await submitCalamidad({
        turnoId: context.turnoId,
        personaId: context.personaId,
        fechaInicio: context.fechaInicio,
        fechaFin: context.fechaFin,
        motivo: context.motivo,
        reemplazos,
        sinReemplazosConfirmado: false
      });

      if (!response.ok) {
        showToast(data.message || "No se pudo registrar la calamidad con reemplazos.", "danger");
        return;
      }

      showToast(data.message || "Calamidad registrada con reemplazos.", "success");
      stopCalamidadReplacementMode({ force: true });
      await loadWeekData({ skipPreview: false });
    } catch {
      showToast("No se pudo registrar la calamidad con reemplazos.", "danger");
    }
  };

  const confirmCalamidadWithoutReplacements = async () => {
    const context = calamidadReplacementContext;
    if (!context) {
      return;
    }

    if (context.replacements.size > 0) {
      const decision = await openCalamidadDecisionModal({
        title: "Guardar sin reemplazos",
        message: "Ya seleccionaste reemplazos. Si continuas, se ignoraran y la calamidad quedara guardada sin reemplazos.",
        confirmLabel: "Guardar sin reemplazos",
        confirmClass: "btn-primary"
      });
      if (decision.action !== "confirm") {
        return;
      }
    }

    try {
      const { response, data } = await submitCalamidad({
        turnoId: context.turnoId,
        personaId: context.personaId,
        fechaInicio: context.fechaInicio,
        fechaFin: context.fechaFin,
        motivo: context.motivo,
        reemplazos: [],
        sinReemplazosConfirmado: true
      });

      if (!response.ok) {
        showToast(data.message || "No se pudo registrar la calamidad.", "danger");
        return;
      }

      showToast(data.message || "Calamidad registrada sin reemplazos.", "success");
      stopCalamidadReplacementMode({ force: true });
      await loadWeekData({ skipPreview: false });
    } catch {
      showToast("No se pudo registrar la calamidad.", "danger");
    }
  };

  const assignCalamidadNewShiftReplacement = () => {
    const context = calamidadReplacementContext;
    if (!context || context.modoReemplazo !== "NEW_SHIFT") {
      return false;
    }

    if (!context.selectedSourceTurnoId) {
      showToast("Primero selecciona el turno ausente que quieres cubrir.", "info");
      return false;
    }

    const personaReemplazoId = `${dom.calamidadNewPersonSelect?.value || ""}`.trim();
    if (!personaReemplazoId) {
      showToast("Selecciona una persona para crear el turno nuevo.", "info");
      return false;
    }

    if (personaReemplazoId === context.personaId) {
      showToast("La persona ausente no puede ser su propio reemplazo.", "info");
      return false;
    }

    const personaNombre = resolveCalamidadPersonaNombre(context, personaReemplazoId);
    context.replacements.set(context.selectedSourceTurnoId, {
      modoReemplazo: "NEW_SHIFT",
      personaReemplazoId,
      personaNombre
    });
    context.selectedSourceTurnoId = "";
    if (dom.calamidadNewPersonSelect) {
      dom.calamidadNewPersonSelect.value = "";
    }
    showToast("Reemplazo asignado en modo turno nuevo.", "success");
    refreshCalamidadReplacementUi();
    return true;
  };

  const bindCalamidadReplacementBarEvents = () => {
    if (dom.calamidadSaveReplacements && dom.calamidadSaveReplacements.dataset.bound !== "true") {
      dom.calamidadSaveReplacements.dataset.bound = "true";
      dom.calamidadSaveReplacements.addEventListener("click", async () => {
        await saveCalamidadReplacements();
      });
    }

    if (dom.calamidadConfirmNoReplacements && dom.calamidadConfirmNoReplacements.dataset.bound !== "true") {
      dom.calamidadConfirmNoReplacements.dataset.bound = "true";
      dom.calamidadConfirmNoReplacements.addEventListener("click", async () => {
        await confirmCalamidadWithoutReplacements();
      });
    }

    if (dom.calamidadCancelReplacements && dom.calamidadCancelReplacements.dataset.bound !== "true") {
      dom.calamidadCancelReplacements.dataset.bound = "true";
      dom.calamidadCancelReplacements.addEventListener("click", async () => {
        if (calamidadReplacementContext?.replacements?.size > 0) {
          const decision = await openCalamidadDecisionModal({
            title: "Descartar reemplazos",
            message: "Hay reemplazos sin guardar. Si continuas se perderan los cambios.",
            confirmLabel: "Descartar",
            confirmClass: "btn-danger"
          });
          if (decision.action !== "confirm") {
            return;
          }
        }

        const closed = stopCalamidadReplacementMode();
        if (closed) {
          showToast("Modo reemplazos cancelado.", "info");
        }
      });
    }

    if (dom.calamidadModeButtons && dom.calamidadModeButtons.length > 0) {
      dom.calamidadModeButtons.forEach((button) => {
        if (button.dataset.bound === "true") return;
        button.dataset.bound = "true";
        button.addEventListener("click", () => {
          const context = calamidadReplacementContext;
          if (!context) {
            showToast("Primero inicia el flujo de calamidad para configurar reemplazos.", "info");
            return;
          }

          const requestedMode = (button.dataset.calamidadModeBtn || "").toUpperCase();
          const nextMode = requestedMode === "NEW_SHIFT" ? "NEW_SHIFT" : "SWAP";
          if (context.modoReemplazo === nextMode) {
            return;
          }

          context.modoReemplazo = nextMode;
          context.selectedSourceTurnoId = "";
          if (dom.calamidadSourceSelect) {
            dom.calamidadSourceSelect.value = "";
          }
          if (dom.calamidadNewPersonSelect) {
            dom.calamidadNewPersonSelect.value = "";
          }
          refreshCalamidadReplacementUi();
        });
      });
    }

    if (dom.calamidadSourceSelect && dom.calamidadSourceSelect.dataset.bound !== "true") {
      dom.calamidadSourceSelect.dataset.bound = "true";
      dom.calamidadSourceSelect.addEventListener("change", () => {
        const context = calamidadReplacementContext;
        if (!context) return;

        const selectedSourceTurnoId = `${dom.calamidadSourceSelect.value || ""}`.trim();
        if (!selectedSourceTurnoId) {
          context.selectedSourceTurnoId = "";
          if (dom.calamidadSourceAssignBtn) {
            dom.calamidadSourceAssignBtn.disabled = true;
          }
          refreshCalamidadReplacementUi();
          return;
        }

        context.selectedSourceTurnoId = selectedSourceTurnoId;
        if (dom.calamidadSourceAssignBtn) {
          dom.calamidadSourceAssignBtn.disabled = false;
        }
        showToast("Turno ausente seleccionado desde el panel.", "info");
        refreshCalamidadReplacementUi();
      });
    }

    if (dom.calamidadSourceAssignBtn && dom.calamidadSourceAssignBtn.dataset.bound !== "true") {
      dom.calamidadSourceAssignBtn.dataset.bound = "true";
      dom.calamidadSourceAssignBtn.addEventListener("click", () => {
        const context = calamidadReplacementContext;
        if (!context || !dom.calamidadSourceSelect) return;
        const selectedSourceTurnoId = `${dom.calamidadSourceSelect.value || ""}`.trim();
        if (!selectedSourceTurnoId) {
          showToast("Selecciona un turno ausente en el selector.", "info");
          return;
        }

        context.selectedSourceTurnoId = selectedSourceTurnoId;
        showToast("Turno ausente seleccionado.", "info");
        refreshCalamidadReplacementUi();
      });
    }

    if (dom.calamidadNewPersonSelect && dom.calamidadNewPersonSelect.dataset.bound !== "true") {
      dom.calamidadNewPersonSelect.dataset.bound = "true";
      dom.calamidadNewPersonSelect.addEventListener("change", () => {
        const context = calamidadReplacementContext;
        if (!context || context.modoReemplazo !== "NEW_SHIFT") {
          return;
        }
        if (dom.calamidadNewAssignBtn) {
          dom.calamidadNewAssignBtn.disabled =
            !context.selectedSourceTurnoId || !dom.calamidadNewPersonSelect.value;
        }
      });
    }

    if (dom.calamidadNewAssignBtn && dom.calamidadNewAssignBtn.dataset.bound !== "true") {
      dom.calamidadNewAssignBtn.dataset.bound = "true";
      dom.calamidadNewAssignBtn.addEventListener("click", () => {
        assignCalamidadNewShiftReplacement();
      });
    }
  };

  const handleCalamidadCardClick = (card) => {
    const context = calamidadReplacementContext;
    if (!context || !card) {
      return false;
    }

    const clickedTurnoId = card.dataset.assignment || "";
    const clickedAssignment = getAssignmentById(clickedTurnoId);
    if (!clickedAssignment) {
      return false;
    }

    const startDate = parseIsoDate(context.fechaInicio);
    const endDate = parseIsoDate(context.fechaFin);
    const clickedDate = resolveAssignmentDate(clickedAssignment);
    const isWithinRange = isDateInRange(clickedDate, startDate, endDate);
    const clickedSourceTurnoId = getCalamidadSourceTurnoId(clickedAssignment) || clickedTurnoId;
    const absentSource = findContextAbsentSource(
      clickedSourceTurnoId,
      context,
      startDate,
      endDate
    );
    const isAbsentTurno =
      (
        (clickedAssignment.personaId || "") === context.personaId &&
        isWithinRange
      ) ||
      absentSource !== null;

    if (isReadOnlyCard(card) && !isAbsentTurno) {
      return false;
    }

    if (isAbsentTurno) {
      if (context.selectedSourceTurnoId === clickedSourceTurnoId) {
        context.selectedSourceTurnoId = "";
        showToast("Turno ausente deseleccionado.", "info");
      } else {
        context.selectedSourceTurnoId = clickedSourceTurnoId;
        showToast("Turno ausente seleccionado. Ahora elige quien lo reemplaza.", "info");
      }
      refreshCalamidadReplacementUi();
      return true;
    }

    if (!context.selectedSourceTurnoId) {
      showToast("Primero selecciona un turno de la persona ausente.", "info");
      return true;
    }

    const sourceAssignment = getAssignmentById(context.selectedSourceTurnoId);
    if (!sourceAssignment) {
      context.selectedSourceTurnoId = "";
      refreshCalamidadReplacementUi();
      return true;
    }

    if (clickedSourceTurnoId === context.selectedSourceTurnoId) {
      context.selectedSourceTurnoId = "";
      refreshCalamidadReplacementUi();
      return true;
    }

    if ((clickedAssignment.personaId || "") === context.personaId) {
      showToast("Selecciona otra persona como reemplazo.", "info");
      return true;
    }

    if (context.modoReemplazo === "SWAP" && isExistingCalamidadReplacementShift(clickedAssignment)) {
      showToast("Ese turno ya esta cubriendo otra calamidad y no puede usarse como reemplazo.", "info");
      return true;
    }

    if (context.modoReemplazo === "NEW_SHIFT") {
      context.replacements.set(context.selectedSourceTurnoId, {
        modoReemplazo: "NEW_SHIFT",
        personaReemplazoId: clickedAssignment.personaId || "",
        personaNombre: `${clickedAssignment.title || card.querySelector(".card-title")?.textContent || "Persona"}`.trim()
      });
      context.selectedSourceTurnoId = "";
      showToast("Reemplazo asignado en modo turno nuevo.", "success");
      refreshCalamidadReplacementUi();
      return true;
    }

    const alreadyAssignedSource = Array.from(context.replacements.entries())
      .find(([, replacement]) => replacement?.modoReemplazo === "SWAP" && replacement?.turnoReemplazoId === clickedTurnoId);

    if (alreadyAssignedSource && alreadyAssignedSource[0] !== context.selectedSourceTurnoId) {
      showToast("Ese turno ya esta asignado como reemplazo de otra ausencia.", "info");
      return true;
    }

    context.replacements.set(context.selectedSourceTurnoId, {
      modoReemplazo: "SWAP",
      turnoReemplazoId: clickedTurnoId,
      personaReemplazoId: clickedAssignment.personaId || "",
      personaNombre: `${clickedAssignment.title || card.querySelector(".card-title")?.textContent || "Persona"}`.trim()
    });
    context.selectedSourceTurnoId = "";
    showToast("Reemplazo asignado usando turno existente.", "success");
    refreshCalamidadReplacementUi();
    return true;
  };

  const bindCalamidadGridReplacementClick = () => {
    if (!dom.grid) return;
    if (dom.grid.dataset.calamidadReplaceClickBound === "true") return;
    dom.grid.dataset.calamidadReplaceClickBound = "true";

    dom.grid.addEventListener("click", (event) => {
      if (!calamidadReplacementContext) return;
      const target = event.target instanceof HTMLElement ? event.target : null;
      if (!target) return;
      if (
        target.closest(".preview-remove") ||
        target.closest(".saved-remove") ||
        target.closest(".cell-add-btn")
      ) {
        return;
      }

      let card = target.closest(".shift-card");
      if (!card) {
        const cell = target.closest(".shift-cell");
        if (cell) {
          card = cell.querySelector(".shift-card:not(.is-preview)") || null;
        }
      }
      if (!card) return;

      let handled = handleCalamidadCardClick(card);
      if (!handled && isReadOnlyCard(card)) {
        const cell = card.closest(".shift-cell");
        const fallbackCard = cell?.querySelector(
          '.shift-card:not(.is-preview):not(.is-calamidad-absent):not([data-calamidad-ausente="true"])'
        );
        if (fallbackCard && fallbackCard !== card) {
          handled = handleCalamidadCardClick(fallbackCard);
        }
      }
      if (handled) {
        event.preventDefault();
        event.stopPropagation();
      }
    });
  };

  const openCalamidadDecisionModal = ({
    title,
    message,
    confirmLabel = "Confirmar",
    denyLabel = "",
    confirmClass = "btn-primary",
    denyClass = "btn-outline-primary",
    showDeny = false
  }) => {
    const modalEl = document.getElementById("calamidadDecisionModal");
    if (!modalEl) {
      return Promise.resolve({ action: "cancel" });
    }

    const titleEl = dom.calamidadDecisionTitle;
    const messageEl = dom.calamidadDecisionMessage;
    const confirmBtn = dom.calamidadDecisionConfirm;
    const denyBtn = dom.calamidadDecisionDeny;

    if (titleEl) {
      titleEl.textContent = title || "Confirmar accion";
    }

    if (messageEl) {
      messageEl.textContent = message || "";
    }

    if (confirmBtn) {
      confirmBtn.textContent = confirmLabel || "Confirmar";
      confirmBtn.classList.remove("btn-primary", "btn-danger", "btn-warning", "btn-success", "btn-outline-primary");
      confirmBtn.classList.add(confirmClass || "btn-primary");
    }

    if (denyBtn) {
      denyBtn.hidden = !showDeny;
      denyBtn.textContent = denyLabel || "";
      denyBtn.classList.remove("btn-primary", "btn-danger", "btn-warning", "btn-success", "btn-outline-primary");
      denyBtn.classList.add(denyClass || "btn-outline-primary");
    }

    const modal = window.bootstrap?.Modal
      ? new window.bootstrap.Modal(modalEl)
      : null;

    return new Promise((resolve) => {
      let settled = false;

      const cleanup = () => {
        confirmBtn?.removeEventListener("click", handleConfirm);
        denyBtn?.removeEventListener("click", handleDeny);
        modalEl.removeEventListener("hidden.bs.modal", handleHidden);
      };

      const settle = (action) => {
        if (settled) return;
        settled = true;
        cleanup();
        resolve({ action });
      };

      const handleConfirm = () => {
        settle("confirm");
        modal?.hide();
      };

      const handleDeny = () => {
        settle("deny");
        modal?.hide();
      };

      const handleHidden = () => {
        settle("cancel");
      };

      confirmBtn?.addEventListener("click", handleConfirm);
      denyBtn?.addEventListener("click", handleDeny);
      modalEl.addEventListener("hidden.bs.modal", handleHidden, { once: true });

      if (modal) {
        modal.show();
      } else {
        settle("cancel");
      }
    });
  };

  const openCalamidadModal = ({ personaNombre, fechaInicioIso }) => {
    const modalEl = document.getElementById("calamidadModal");
    if (!modalEl) {
      return Promise.resolve({ confirmed: false, fechaFin: "", motivo: "" });
    }

    const personaEl = dom.calamidadPersona;
    const inicioEl = dom.calamidadFechaInicio;
    const fechaFinInput = dom.calamidadFechaFin;
    const motivoInput = dom.calamidadMotivo;
    const confirmBtn = dom.calamidadConfirm;

    if (personaEl) {
      personaEl.textContent = personaNombre || "-";
    }

    const startDate = parseIsoDate(fechaInicioIso);
    if (inicioEl) {
      inicioEl.textContent = startDate ? formatShortDate(startDate) : fechaInicioIso;
    }

    if (fechaFinInput) {
      fechaFinInput.value = fechaInicioIso;
      fechaFinInput.min = fechaInicioIso;
    }

    if (motivoInput) {
      motivoInput.value = "";
    }

    const modal = window.bootstrap?.Modal
      ? new window.bootstrap.Modal(modalEl)
      : null;

    return new Promise((resolve) => {
      let settled = false;

      const cleanup = () => {
        confirmBtn?.removeEventListener("click", handleConfirm);
        modalEl.removeEventListener("hidden.bs.modal", handleHidden);
      };

      const settle = (result) => {
        if (settled) return;
        settled = true;
        cleanup();
        resolve(result);
      };

      const handleConfirm = () => {
        const fechaFin = (fechaFinInput?.value || "").trim();
        const motivo = (motivoInput?.value || "").trim();
        settle({ confirmed: true, fechaFin, motivo });
        modal?.hide();
      };

      const handleHidden = () => {
        settle({ confirmed: false, fechaFin: "", motivo: "" });
      };

      confirmBtn?.addEventListener("click", handleConfirm);
      modalEl.addEventListener("hidden.bs.modal", handleHidden, { once: true });

      if (modal) {
        modal.show();
      } else {
        settle({ confirmed: false, fechaFin: "", motivo: "" });
      }
    });
  };

  const createCalamidadFromCard = async (card) => {
    const assignmentId = card?.dataset?.assignment || "";
    const assignment = getAssignmentById(assignmentId);
    if (!assignment) {
      showToast("No se encontro el turno seleccionado.", "danger");
      return;
    }

    const personaId = assignment.personaId || card?.dataset?.personaId || "";
    if (!personaId) {
      showToast("No se encontro la persona ausente.", "danger");
      return;
    }

    if (isExistingCalamidadReplacementShift(assignment)) {
      showToast("No puedes ingresar calamidad sobre un turno que ya esta actuando como reemplazo.", "info");
      return;
    }

    const assignmentDate = resolveAssignmentDate(assignment);
    if (!assignmentDate) {
      showToast("No se pudo resolver la fecha del turno.", "danger");
      return;
    }

    const fechaInicioIso = utils.formatIsoDate(assignmentDate);
    const personaNombre = card.querySelector(".card-title")?.textContent?.trim() || "Persona";
    const modalResult = await openCalamidadModal({
      personaNombre,
      fechaInicioIso
    });

    if (!modalResult?.confirmed) {
      return;
    }

    const fechaFinIso = (modalResult.fechaFin || "").trim();
    const motivo = (modalResult.motivo || "").trim();
    if (!fechaFinIso) {
      showToast("Selecciona la fecha fin de la calamidad.", "info");
      return;
    }

    if (!motivo) {
      showToast("Ingresa el motivo de la calamidad.", "info");
      return;
    }

    const fechaInicio = parseIsoDate(fechaInicioIso);
    const fechaFin = parseIsoDate(fechaFinIso);
    if (!fechaInicio || !fechaFin || fechaFin.getTime() < fechaInicio.getTime()) {
      showToast("La fecha fin no puede ser menor que la fecha inicio.", "info");
      return;
    }

    startCalamidadReplacementMode({
      turnoId: assignmentId,
      personaId,
      personaNombre,
      fechaInicio: fechaInicioIso,
      fechaFin: fechaFinIso,
      motivo
    });
  };

  const openCambiarGrupoModal = ({ personaNombre, grupoActual, fechaTurno, personaId }) => {
    const modalEl = document.getElementById("cambiarGrupoModal");
    if (!modalEl) {
      return Promise.resolve({ confirmed: false, grupoId: "" });
    }

    const personaEl = modalEl.querySelector("[data-cambiar-grupo-persona]");
    const grupoActualEl = modalEl.querySelector("[data-cambiar-grupo-actual]");
    const fechaEl = modalEl.querySelector("[data-cambiar-grupo-fecha]");
    const grupoSelect = modalEl.querySelector("[data-cambiar-grupo-select]");
    const confirmBtn = modalEl.querySelector("[data-cambiar-grupo-confirm]");

    if (personaEl) {
      personaEl.textContent = personaNombre || "-";
    }

    if (grupoActualEl) {
      grupoActualEl.textContent = grupoActual || "Sin grupo";
    }

    if (fechaEl) {
      fechaEl.textContent = fechaTurno || "-";
    }

    if (grupoSelect) {
      grupoSelect.value = "";
      grupoSelect.innerHTML = '<option value="">Selecciona nuevo grupo</option>';
    }

    const modal = window.bootstrap?.Modal
      ? new window.bootstrap.Modal(modalEl)
      : null;

    return new Promise((resolve) => {
      let settled = false;

      const cleanup = () => {
        confirmBtn?.removeEventListener("click", handleConfirm);
        modalEl.removeEventListener("hidden.bs.modal", handleHidden);
      };

      const settle = (result) => {
        if (settled) return;
        settled = true;
        cleanup();
        resolve(result);
      };

      const handleConfirm = () => {
        const grupoId = (grupoSelect?.value || "").trim();
        settle({ confirmed: true, grupoId });
        modal?.hide();
      };

      const handleHidden = () => {
        settle({ confirmed: false, grupoId: "" });
      };

      confirmBtn?.addEventListener("click", handleConfirm);
      modalEl.addEventListener("hidden.bs.modal", handleHidden, { once: true });

      // Load grupos for persona before showing modal
      if (personaId && grupoSelect) {
        loadGruposForPersonaModal(personaId, grupoSelect).then(() => {
          if (modal) {
            modal.show();
          }
        });
      } else {
        if (modal) {
          modal.show();
        }
      }
    });
  };

  const loadGruposForPersonaModal = async (personaId, grupoSelect) => {
    if (!grupoSelect || !personaId) return;

    try {
      const response = await fetch(`/RegistroTurno/GruposPorPersona?personaId=${encodeURIComponent(personaId)}`, {
        headers: { Accept: "application/json" }
      });
      if (!response.ok) {
        throw new Error("Bad response");
      }
      const grupos = await response.json();
      grupoSelect.innerHTML = '<option value="">Selecciona nuevo grupo</option>';
      grupos.forEach((grupo) => {
        const option = document.createElement("option");
        option.value = grupo.value;
        option.textContent = grupo.text;
        if (grupo.color) {
          option.dataset.color = grupo.color;
        }
        grupoSelect.appendChild(option);
      });
    } catch {
      grupoSelect.innerHTML = '<option value="">Error al cargar grupos</option>';
    }
  };

  const openCambiarGrupoFromCard = async (card) => {
    const assignmentId = card?.dataset?.assignment || "";
    const assignment = getAssignmentById(assignmentId);
    if (!assignment) {
      showToast("No se encontro el turno seleccionado.", "danger");
      return;
    }

    const personaId = assignment.personaId || card?.dataset?.personaId || "";
    const grupoId = assignment.grupoId || card?.dataset?.grupoId || "";
    if (!personaId) {
      showToast("No se encontro la persona del turno.", "danger");
      return;
    }

    const assignmentDate = resolveAssignmentDate(assignment);
    if (!assignmentDate) {
      showToast("No se pudo resolver la fecha del turno.", "danger");
      return;
    }

    const personaNombre = card.querySelector(".card-title")?.textContent?.trim() || "Persona";
    const fechaTurnoFormatted = formatShortDate(assignmentDate);
    
    // Get current group name - load grupos first to get the name
    let grupoActual = "Sin grupo";
    if (grupoId) {
      try {
        const response = await fetch(`/RegistroTurno/GruposPorPersona?personaId=${encodeURIComponent(personaId)}`, {
          headers: { Accept: "application/json" }
        });
        if (response.ok) {
          const grupos = await response.json();
          const encontrado = grupos.find(g => g.value === grupoId);
          if (encontrado) {
            grupoActual = encontrado.text;
          }
        }
      } catch {
        grupoActual = grupoId;
      }
    }

    const modalResult = await openCambiarGrupoModal({
      personaNombre,
      grupoActual,
      fechaTurno: fechaTurnoFormatted,
      personaId
    });

    if (!modalResult?.confirmed || !modalResult?.grupoId) {
      return;
    }

    const nuevoGrupoId = modalResult.grupoId;
    if (nuevoGrupoId === grupoId) {
      showToast("Selecciona un grupo diferente.", "info");
      return;
    }

    // Call API to change group
    await cambiarGrupoTurno(assignmentId, personaId, nuevoGrupoId);
  };

  const cambiarGrupoTurno = async (assignmentId, personaId, nuevoGrupoId) => {
    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    const token = tokenInput?.value;

    try {
      const response = await fetch("/Calendario/CambiarGrupoTurno", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          RequestVerificationToken: token || ""
        },
        body: JSON.stringify({
          turnoId: assignmentId,
          personaId,
          nuevoGrupoId
        })
      });

      if (!response.ok) {
        const data = await response.json().catch(() => ({}));
        showToast(data.message || "No se pudo cambiar el grupo del turno.", "danger");
        return;
      }

      showToast("Grupo del turno cambiado exitosamente.", "success");
      await loadWeekData();
    } catch (error) {
      console.error("Error al cambiar grupo:", error);
      showToast("No se pudo cambiar el grupo del turno.", "danger");
    }
  };

  const bindCardContextMenu = () => {
    bindCalamidadReplacementBarEvents();
    bindCalamidadGridReplacementClick();

    const menu = document.querySelector("[data-card-context]");
    if (!menu) return;
    if (menu.dataset.bound === "true") return;
    menu.dataset.bound = "true";

    const permisoItem = menu.querySelector('[data-context-action="permiso"]');
    const calamidadItem = menu.querySelector('[data-context-action="calamidad"]');
    const extraEditor = menu.querySelector("[data-context-extra-editor]");
    const extraCheckbox = menu.querySelector("[data-context-extra-checkbox]");
    const extraSaveBtn = menu.querySelector("[data-context-extra-save]");
    const extraInfo = menu.querySelector("[data-context-extra-info]");
    let activeCard = null;

    const getCurrentWeeklyCountForCard = (card) => {
      const personaId = card?.dataset?.personaId || "";
      const dayId = card?.dataset?.day || "";
      if (!personaId || !dayId) {
        return 0;
      }

      const cardDate = getDateForDayId(dayId);
      if (!cardDate) {
        return 0;
      }

      const targetWeekKey = getWeekKeyForDate(cardDate);
      if (!targetWeekKey) {
        return 0;
      }

      return state.assignments
        .filter((assignment) => (assignment.personaId || "") === personaId)
        .filter((assignment) => {
          const assignmentDate = getDateForDayId(assignment.day || "");
          if (!assignmentDate) {
            return false;
          }
          return getWeekKeyForDate(assignmentDate) === targetWeekKey;
        })
        .length;
    };

    const getMenuCapabilities = (card) => {
      if (!card || isReadOnlyCard(card)) {
        return { canPermiso: false, canCalamidad: false };
      }

      const isOwnShift = (card.dataset.personaId || "") === state.currentPersonaId;
      const isReplacementShift = isExistingCalamidadReplacementCard(card);

      return {
        // Permiso must be enabled in permission table and created on own shift.
        canPermiso: canRequestPermiso() && isOwnShift,
        // Calamidad is driven by permission flags from server.
        canCalamidad: canCreateCalamidad() && !isReplacementShift
      };
    };

    const hideMenu = () => {
      menu.hidden = true;
      activeCard = null;
    };

    const positionMenu = (anchorRect, x, y) => {
      const margin = 12;
      const menuRect = menu.getBoundingClientRect();
      let left = anchorRect.right + 8;
      let top = anchorRect.top + anchorRect.height / 2 - menuRect.height / 2;

      if (left + menuRect.width > window.innerWidth - margin) {
        left = anchorRect.left - menuRect.width - 8;
      }
      if (top + menuRect.height > window.innerHeight - margin) {
        top = window.innerHeight - menuRect.height - margin;
      }
      if (left < margin) left = margin;
      if (top < margin) top = margin;

      menu.style.left = `${left + window.scrollX}px`;
      menu.style.top = `${top + window.scrollY}px`;
    };

    const handleContextMenu = (event) => {
      const target = event.target instanceof Element ? event.target : null;
      if (!target) return;
      let card = target.closest(".shift-card");
      if (!card) {
        const cell = target.closest(".shift-cell");
        if (cell) {
          card =
            cell.querySelector(".shift-card:not(.is-preview):not(.is-calamidad-absent)") ||
            cell.querySelector(".shift-card");
        }
      }
      if (!card) return;
      const gridRoot = card.closest("[data-calendar-grid], [data-calendar-grid-amplia]");
      if (!gridRoot) return;

      const { canPermiso, canCalamidad } = getMenuCapabilities(card);

      if (permisoItem) {
        permisoItem.hidden = false;
        permisoItem.disabled = !canPermiso;
        permisoItem.setAttribute("aria-disabled", canPermiso ? "false" : "true");
      }
      if (calamidadItem) {
        const isReplacementShift = isExistingCalamidadReplacementCard(card);
        calamidadItem.hidden = false;
        calamidadItem.disabled = !canCalamidad;
        calamidadItem.setAttribute("aria-disabled", canCalamidad ? "false" : "true");
        calamidadItem.title = isReplacementShift
          ? "Este turno ya esta actuando como reemplazo."
          : "";
      }

      if (extraEditor && extraCheckbox && extraSaveBtn) {
        const isExtra = `${card.dataset.esTurnoExtra || ""}`.toLowerCase() === "true";
        const weeklyCount = getCurrentWeeklyCountForCard(card);
        const isHolidayDay =
          Array.isArray(state.holidaysByDay?.[card.dataset.day || ""]) &&
          state.holidaysByDay[card.dataset.day || ""].length > 0;
        const canToggleOn = canEditAnyShift() && weeklyCount >= 6 && !isHolidayDay;
        const canToggleOff = canEditAnyShift() && isExtra;
        const canEditExtra = canToggleOn || canToggleOff;

        extraEditor.hidden = false;
        extraCheckbox.checked = isExtra;
        extraCheckbox.disabled = !canEditExtra;
        extraSaveBtn.disabled = !canEditExtra;

        if (extraInfo) {
          extraInfo.hidden = false;
          if (!canEditAnyShift()) {
            extraInfo.textContent = "No tienes permisos para editar turno extra.";
          } else if (isHolidayDay && !isExtra) {
            extraInfo.textContent = "En feriado no se puede marcar turno extra.";
          } else {
            extraInfo.textContent = "";
          }
          extraInfo.hidden = !extraInfo.textContent;
          extraInfo.classList.toggle("is-extra-active", isExtra);
        }
      }

      event.preventDefault();
      activeCard = card;
      menu.hidden = false;
      positionMenu(card.getBoundingClientRect(), event.clientX, event.clientY);
    };

    // Capture phase makes this robust even if nested handlers/overlays exist.
    document.addEventListener("contextmenu", handleContextMenu, true);

    menu.addEventListener("click", (event) => {
      const target = event.target instanceof HTMLElement ? event.target : null;

      if (target?.matches("[data-context-extra-save]")) {
        if (!activeCard || !extraCheckbox || !extraSaveBtn) {
          return;
        }

        const turnoId = activeCard.dataset.assignment || "";
        if (!turnoId) {
          showToast("No se encontro el turno.", "danger");
          hideMenu();
          return;
        }

        const saveExtra = async () => {
          const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
          const token = tokenInput?.value;

          extraSaveBtn.disabled = true;
          try {
            const response = await fetch("/RegistroTurno/SetTurnoExtra", {
              method: "POST",
              headers: {
                "Content-Type": "application/json",
                RequestVerificationToken: token || ""
              },
              body: JSON.stringify({
                turnoId,
                esTurnoExtra: extraCheckbox.checked
              })
            });

            if (!response.ok) {
              const data = await response.json().catch(() => ({}));
              showToast(data.message || "No se pudo actualizar el turno extra.", "danger");
              return;
            }

            showToast("Turno extra actualizado.", "success");
            hideMenu();
            await loadWeekData();
          } catch {
            showToast("No se pudo actualizar el turno extra.", "danger");
          } finally {
            if (extraSaveBtn) {
              extraSaveBtn.disabled = false;
            }
          }
        };

        saveExtra();
        return;
      }

      const action = target?.dataset?.contextAction;
      if (!action || !activeCard) return;

      if (target instanceof HTMLButtonElement && target.disabled) {
        if (action === "permiso") {
          showToast("No tienes permiso para solicitar permiso sobre este turno.", "info");
        } else if (action === "calamidad") {
          const message = isExistingCalamidadReplacementCard(activeCard)
            ? "No puedes ingresar calamidad sobre un turno que ya esta actuando como reemplazo."
            : "No tienes permiso para ingresar calamidad.";
          showToast(message, "info");
        }
        hideMenu();
        return;
      }

      if (action === "permiso") {
        if (!canRequestPermiso()) {
          showToast("No tienes permiso para solicitar permisos.", "info");
          hideMenu();
          return;
        }
        const assignmentId = activeCard.dataset.assignment;
        const assignment = state.assignments.find((item) => item.id === assignmentId);
        if (!assignment) {
          showToast("No se encontro el turno.", "danger");
          hideMenu();
          return;
        }
        const personaId = activeCard.dataset.personaId || "";
        if (personaId !== state.currentPersonaId) {
          showToast("Solo puedes solicitar permiso para tus turnos.", "info");
          hideMenu();
          return;
        }

        hideMenu();
        openPermisoFromShift(assignment);
        return;
      }

      if (action === "calamidad") {
        if (!canCreateCalamidad()) {
          showToast("No tienes permiso para ingresar calamidad.", "info");
          hideMenu();
          return;
        }
        if (isExistingCalamidadReplacementCard(activeCard)) {
          showToast("No puedes ingresar calamidad sobre un turno que ya esta actuando como reemplazo.", "info");
          hideMenu();
          return;
        }
        const selectedCard = activeCard;
        hideMenu();
        createCalamidadFromCard(selectedCard);
      }

      if (action === "cambiar-grupo") {
        if (!canEditAnyShift()) {
          showToast("No tienes permiso para cambiar grupo.", "info");
          hideMenu();
          return;
        }
        const selectedCard = activeCard;
        hideMenu();
        openCambiarGrupoFromCard(selectedCard);
      }
    });

    document.addEventListener("click", (event) => {
      const target = event.target;
      if (!(target instanceof Node)) return;
      if (!menu.hidden && !menu.contains(target)) {
        hideMenu();
      }
    });


    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        hideMenu();
      }
    });
  };
  const closePanel = () => {
    if (!dom.panel || !dom.panelScrim) return;
    dom.panel.classList.remove("is-open");
    dom.panelScrim.classList.remove("is-open");
    setTimeout(() => {
      dom.panel.hidden = true;
      dom.panelScrim.hidden = true;
    }, 220);
  };

  let vacationRange = {
    start: null,
    end: null,
    year: new Date().getFullYear()
  };

  let permisoSelection = {
    date: new Date(),
    year: new Date().getFullYear()
  };

  const parseTimeValue = (value) => {
    if (!value) return null;
    const [hours, minutes] = value.split(":").map(Number);
    if (Number.isNaN(hours) || Number.isNaN(minutes)) return null;
    return hours * 60 + minutes;
  };

  const parseShiftRange = (label) => {
    if (!label) return { start: "", end: "" };
    const parts = label.split("-").map((item) => item.trim());
    if (parts.length < 2) return { start: "", end: "" };
    return { start: parts[0], end: parts[1] };
  };

  const setVacationSummary = () => {
    if (!dom.vacationSummary) return;
    const summaryText = dom.vacationSummary.querySelector("span");
    if (!summaryText) return;
    if (!vacationRange.start) {
      summaryText.textContent = "Selecciona fechas para continuar.";
      if (dom.vacationApply) {
        dom.vacationApply.disabled = true;
      }
      return;
    }
    if (!vacationRange.end) {
      summaryText.textContent = `Inicio: ${utils.formatDate(vacationRange.start)}.`;
      if (dom.vacationApply) {
        dom.vacationApply.disabled = true;
      }
      return;
    }

    const diffMs = vacationRange.end.getTime() - vacationRange.start.getTime();
    const days = Math.floor(diffMs / (24 * 60 * 60 * 1000)) + 1;
    summaryText.textContent = `Del ${utils.formatDate(vacationRange.start)} al ${utils.formatDate(vacationRange.end)} (${days} dias).`;
    if (dom.vacationApply) {
      dom.vacationApply.disabled = false;
    }
  };

  const setVacationReview = () => {
    if (!dom.vacationReview || !dom.vacationReviewRange || !dom.vacationReviewDays) return;
    if (!vacationRange.start || !vacationRange.end) {
      dom.vacationReview.hidden = true;
      return;
    }
    const diffMs = vacationRange.end.getTime() - vacationRange.start.getTime();
    const days = Math.floor(diffMs / (24 * 60 * 60 * 1000)) + 1;
    dom.vacationReviewRange.textContent =
      `Del ${utils.formatDate(vacationRange.start)} al ${utils.formatDate(vacationRange.end)}.`;
    dom.vacationReviewDays.textContent = `${days} dias de vacaciones.`;
    dom.vacationReview.hidden = false;
  };

  const isInVacationRange = (date) => {
    if (!vacationRange.start || !vacationRange.end) return false;
    return date >= vacationRange.start && date <= vacationRange.end;
  };

  const renderVacationCalendar = () => {
    if (!dom.vacationCalendar) return;
    const year = vacationRange.year;
    const weekdayLabels = ["Lu", "Ma", "Mi", "Ju", "Vi", "Sa", "Do"];
    const months = api.monthNames;

    const monthHtml = months
      .map((month, index) => {
        const firstDay = new Date(year, index, 1);
        const lastDay = new Date(year, index + 1, 0);
        const totalDays = lastDay.getDate();
        const offset = (firstDay.getDay() + 6) % 7;
        const daysHtml = [];

        for (let i = 0; i < offset; i += 1) {
          daysHtml.push('<button type="button" class="vacation-day is-muted" disabled> </button>');
        }

        for (let day = 1; day <= totalDays; day += 1) {
          const date = new Date(year, index, day);
          const iso = utils.formatIsoDate(date);
          const isSelectedStart =
            vacationRange.start &&
            utils.formatIsoDate(vacationRange.start) === iso;
          const isSelectedEnd =
            vacationRange.end &&
            utils.formatIsoDate(vacationRange.end) === iso;
          const classes = ["vacation-day"];
          if (isSelectedStart || isSelectedEnd) {
            classes.push("is-selected");
          } else if (isInVacationRange(date)) {
            classes.push("is-in-range");
          }
          daysHtml.push(
            `<button type="button" class="${classes.join(" ")}" data-vacation-date="${iso}">${day}</button>`
          );
        }

        return `
        <div class="vacation-month">
          <h4>${month}</h4>
          <div class="vacation-weekdays">
            ${weekdayLabels.map((label) => `<span>${label}</span>`).join("")}
          </div>
          <div class="vacation-days">
            ${daysHtml.join("")}
          </div>
        </div>
      `;
      })
      .join("");

    dom.vacationCalendar.innerHTML = monthHtml;
  };

  const handleVacationDateClick = (event) => {
    const target = event.target instanceof HTMLElement ? event.target : null;
    if (!target) return;
    const button = target.closest("[data-vacation-date]");
    if (!button) return;
    const dateValue = button.getAttribute("data-vacation-date");
    if (!dateValue) return;
    const nextDate = new Date(`${dateValue}T00:00:00`);
    if (Number.isNaN(nextDate.getTime())) return;

    if (!vacationRange.start || (vacationRange.start && vacationRange.end)) {
      vacationRange = { ...vacationRange, start: nextDate, end: null };
    } else if (vacationRange.start && !vacationRange.end) {
      if (nextDate < vacationRange.start) {
        vacationRange = { ...vacationRange, end: vacationRange.start, start: nextDate };
      } else {
        vacationRange = { ...vacationRange, end: nextDate };
      }
    }

    renderVacationCalendar();
    setVacationSummary();
  };

  const resetVacationRange = () => {
    vacationRange = { ...vacationRange, start: null, end: null };
    renderVacationCalendar();
    setVacationSummary();
    if (dom.vacationReview) {
      dom.vacationReview.hidden = true;
    }
  };

  const setPermisoSummary = () => {
    if (!dom.permisoSummary) return;
    const summaryText = dom.permisoSummary.querySelector("span");
    if (!summaryText) return;
    if (!permisoSelection.date) {
      summaryText.textContent = "Selecciona un dia para continuar.";
      if (dom.permisoApply) {
        dom.permisoApply.disabled = true;
      }
      return;
    }
    summaryText.textContent = `Dia: ${utils.formatDate(permisoSelection.date)}.`;
    if (dom.permisoApply) {
      const start = parseTimeValue(dom.permisoInicio?.value || "");
      const end = parseTimeValue(dom.permisoFin?.value || "");
      dom.permisoApply.disabled = !(start !== null && end !== null && end > start);
    }
  };

  const setPermisoReview = () => {
    if (!dom.permisoReview || !dom.permisoReviewDate || !dom.permisoReviewTime) return;
    if (!permisoSelection.date) {
      dom.permisoReview.hidden = true;
      return;
    }
    const startValue = dom.permisoInicio?.value || "";
    const endValue = dom.permisoFin?.value || "";
    if (!startValue || !endValue) {
      dom.permisoReview.hidden = true;
      return;
    }
    dom.permisoReviewDate.textContent = `Dia: ${utils.formatDate(permisoSelection.date)}.`;
    dom.permisoReviewTime.textContent = `Horario: ${startValue} - ${endValue}.`;
    dom.permisoReview.hidden = false;
  };

  const renderPermisoCalendar = () => {
    if (!dom.permisoCalendar) return;
    const year = permisoSelection.year;
    const weekdayLabels = ["Lu", "Ma", "Mi", "Ju", "Vi", "Sa", "Do"];
    const months = api.monthNames;

    const selectedIso = permisoSelection.date
      ? utils.formatIsoDate(permisoSelection.date)
      : "";

    const monthHtml = months
      .map((month, index) => {
        const firstDay = new Date(year, index, 1);
        const lastDay = new Date(year, index + 1, 0);
        const totalDays = lastDay.getDate();
        const offset = (firstDay.getDay() + 6) % 7;
        const daysHtml = [];

        for (let i = 0; i < offset; i += 1) {
          daysHtml.push('<button type="button" class="vacation-day is-muted" disabled> </button>');
        }

        for (let day = 1; day <= totalDays; day += 1) {
          const date = new Date(year, index, day);
          const iso = utils.formatIsoDate(date);
          const classes = ["vacation-day"];
          if (iso === selectedIso) {
            classes.push("is-selected");
          }
          daysHtml.push(
            `<button type="button" class="${classes.join(" ")}" data-permiso-date="${iso}">${day}</button>`
          );
        }

        return `
        <div class="vacation-month">
          <h4>${month}</h4>
          <div class="vacation-weekdays">
            ${weekdayLabels.map((label) => `<span>${label}</span>`).join("")}
          </div>
          <div class="vacation-days">
            ${daysHtml.join("")}
          </div>
        </div>
      `;
      })
      .join("");

    dom.permisoCalendar.innerHTML = monthHtml;
  };

  const handlePermisoDateClick = (event) => {
    const target = event.target instanceof HTMLElement ? event.target : null;
    if (!target) return;
    const button = target.closest("[data-permiso-date]");
    if (!button) return;
    const dateValue = button.getAttribute("data-permiso-date");
    if (!dateValue) return;
    const nextDate = new Date(`${dateValue}T00:00:00`);
    if (Number.isNaN(nextDate.getTime())) return;
    permisoSelection = { ...permisoSelection, date: nextDate };
    renderPermisoCalendar();
    setPermisoSummary();
  };

  const resetPermisoSelection = () => {
    permisoSelection = { ...permisoSelection, date: null };
    if (dom.permisoInicio) dom.permisoInicio.value = "";
    if (dom.permisoFin) dom.permisoFin.value = "";
    if (dom.permisoMotivo) dom.permisoMotivo.value = "";
    renderPermisoCalendar();
    setPermisoSummary();
    if (dom.permisoReview) {
      dom.permisoReview.hidden = true;
    }
  };

  const openPermisoPanel = () => {
    if (!dom.permisoPanel || !dom.permisoScrim) return;
    dom.permisoPanel.hidden = false;
    dom.permisoScrim.hidden = false;
    requestAnimationFrame(() => {
      dom.permisoPanel.classList.add("is-open");
      dom.permisoScrim.classList.add("is-open");
    });
  };

  const closePermisoPanel = () => {
    if (!dom.permisoPanel || !dom.permisoScrim) return;
    dom.permisoPanel.classList.remove("is-open");
    dom.permisoScrim.classList.remove("is-open");
    dom.permisoPanel.classList.remove("is-shift");
    setTimeout(() => {
      dom.permisoPanel.hidden = true;
      dom.permisoScrim.hidden = true;
    }, 220);
  };

  const initPermisoYearSelect = () => {
    if (!dom.permisoYear) return;
    const currentYear = new Date().getFullYear();
    const years = [];
    for (let year = currentYear - 1; year <= currentYear + 2; year += 1) {
      years.push(year);
    }
    dom.permisoYear.innerHTML = years
      .map((year) => `<option value="${year}">${year}</option>`)
      .join("");
    dom.permisoYear.value = permisoSelection.year.toString();
    dom.permisoYear.addEventListener("change", () => {
      permisoSelection = {
        ...permisoSelection,
        year: parseInt(dom.permisoYear.value, 10),
        date: null
      };
      renderPermisoCalendar();
      setPermisoSummary();
    });
  };

  const openPermisoFromShift = (assignment) => {
    if (!assignment || !dom.permisoPanel) return;
    const shift = state.shifts.find((item) => item.id === assignment.shift);
    const range = parseShiftRange(shift?.label);
    const dayIndex = parseInt((assignment.day || "").replace("d", ""), 10);
    const weekDates = getWeekDates(state.weekOffset);
    const selectedDate = weekDates[dayIndex];
    if (!selectedDate || Number.isNaN(selectedDate.getTime())) {
      showToast("No se pudo obtener la fecha del turno.", "danger");
      return;
    }

    permisoSelection = {
      ...permisoSelection,
      date: selectedDate,
      year: selectedDate.getFullYear()
    };

    if (dom.permisoYear) {
      dom.permisoYear.value = permisoSelection.year.toString();
    }
    if (dom.permisoInicio) dom.permisoInicio.value = range.start;
    if (dom.permisoFin) dom.permisoFin.value = range.end;

    dom.permisoPanel.classList.add("is-shift");
    renderPermisoCalendar();
    setPermisoSummary();
    setPermisoReview();
    openPermisoPanel();
  };

  const openVacationPanel = () => {
    if (!dom.vacationPanel || !dom.vacationScrim) return;
    dom.vacationPanel.hidden = false;
    dom.vacationScrim.hidden = false;
    requestAnimationFrame(() => {
      dom.vacationPanel.classList.add("is-open");
      dom.vacationScrim.classList.add("is-open");
    });
  };

  const closeVacationPanel = () => {
    if (!dom.vacationPanel || !dom.vacationScrim) return;
    dom.vacationPanel.classList.remove("is-open");
    dom.vacationScrim.classList.remove("is-open");
    setTimeout(() => {
      dom.vacationPanel.hidden = true;
      dom.vacationScrim.hidden = true;
    }, 220);
  };

  const initVacationYearSelect = () => {
    if (!dom.vacationYear) return;
    const currentYear = new Date().getFullYear();
    const years = [];
    for (let year = currentYear - 1; year <= currentYear + 2; year += 1) {
      years.push(year);
    }
    dom.vacationYear.innerHTML = years
      .map((year) => `<option value="${year}">${year}</option>`)
      .join("");
    dom.vacationYear.value = vacationRange.year.toString();
    dom.vacationYear.addEventListener("change", () => {
      vacationRange = {
        ...vacationRange,
        year: parseInt(dom.vacationYear.value, 10),
        start: null,
        end: null
      };
      renderVacationCalendar();
      setVacationSummary();
    });
  };

  const clearPreviewAssignments = () => {
    state.previewAssignments = [];
    rerenderCalendarPreservingPendingChanges({
      includeLegend: true,
      includeSaveVisibility: true
    });
  };

  const buildPreviewPayload = () => {
    const weekDates = getWeekDates(state.weekOffset);
    const dayToDate = new Map(
      days.map((day, index) => [day.id, utils.formatIsoDate(weekDates[index])])
    );

    return state.previewAssignments
      .map((assignment) => ({
        personaId: assignment.personaId || "",
        tipoTurnoId: assignment.shift || "",
        fechaTurno: dayToDate.get(assignment.day) || "",
        grupoId: assignment.grupoId || null
      }))
      .filter((item) => item.personaId && item.tipoTurnoId && item.fechaTurno);
  };

  const init = () => {
    if (window.bootstrap?.Tooltip) {
      document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach((el) => {
        new window.bootstrap.Tooltip(el);
      });
    }

    const params = new URLSearchParams(window.location.search);
    const weekStartParam = params.get("weekStart");
    if (weekStartParam) {
      const parsed = new Date(`${weekStartParam}T00:00:00`);
      if (!Number.isNaN(parsed.getTime())) {
        const monday = utils.getMondayForDate(parsed);
        state.weekOffset = utils.getWeeksBetween(utils.baseDate, monday);
      }
    }

    const grupoParam = params.get("grupoId");
    if (grupoParam && dom.filterGrupoSelect) {
      dom.filterGrupoSelect.value = grupoParam;
    }

    const personaParam = params.get("personaId");
    if (personaParam && dom.filterPersonaSelect) {
      dom.filterPersonaSelect.value = personaParam;
    }
    state.filterMyShifts = false;
    state.showAbsentPeople = false;
    updateMineToggleButton();
    updateAbsentToggleButton();
    updateVistaAmpliaToggleButton();
    updateBulkDeleteButton();

    bindDragEvents();
    bindCardContextMenu();
    bindKeyboardWeekNavigation();

    if (dom.filterGrupoSelect || dom.filterPersonaSelect) {
      state.filterGrupoId = dom.filterGrupoSelect?.value || "";
      filterPersonasByGrupo();
      state.filterPersonaId = dom.filterPersonaSelect?.value || "";
    }

    loadWeekData();

    if (dom.statusList) {
      dom.statusList.addEventListener("click", handleStatusClick);
    }
    if (dom.pendingConfirmAllBtn) {
      dom.pendingConfirmAllBtn.addEventListener("click", async () => {
        await runBulkStatusAction("confirm");
      });
    }
    if (dom.pendingCancelAllBtn) {
      dom.pendingCancelAllBtn.addEventListener("click", async () => {
        await runBulkStatusAction("cancel");
      });
    }

    document.querySelectorAll("[data-week-nav]").forEach((button) => {
      button.addEventListener("click", async () => {
        const direction = button.getAttribute("data-week-nav");
        if (state.vistaAmpliaActive) {
          await navigateByMonth(direction === "next" ? 1 : -1);
          return;
        }
        state.weekOffset += direction === "next" ? 1 : -1;
        await loadWeekData();
      });
    });

    if (dom.monthSelect && dom.yearSelect) {
      dom.monthSelect.addEventListener("change", updateWeekForMonthSelection);
      dom.yearSelect.addEventListener("change", updateWeekForMonthSelection);
    }

     if (dom.vistaAmpliaToggle) {
      dom.vistaAmpliaToggle.addEventListener("click", async () => {
        state.vistaAmpliaActive = !state.vistaAmpliaActive;
        updateVistaAmpliaToggleButton();
        updateWeekNavLabels();

        if (state.vistaAmpliaActive) {
          dom.calendarShell?.classList.add("vista-amplia-mode");

          if (dom.monthSelect?.value && dom.yearSelect?.value) {
            state.currentMonth = parseInt(dom.monthSelect.value, 10);
            state.currentYear = parseInt(dom.yearSelect.value, 10);
          }

          if (dom.gridAmplia) {
            dom.gridAmplia.hidden = false;
          }

          await updateWeekForMonthSelection();
        } else {
          dom.calendarShell?.classList.remove("vista-amplia-mode");
          if (dom.gridAmplia) {
            dom.gridAmplia.hidden = true;
          }
          if (dom.legendPersonas) {
            dom.legendPersonas.hidden = true;
          }
        if (state.legendPersonaIds instanceof Set) {
          state.legendPersonaIds.clear();
        }
          rerenderCalendarPreservingPendingChanges();
        }
      });
    }

    if (dom.filterMineToggle) {
      dom.filterMineToggle.addEventListener("click", () => {
        state.filterMyShifts = !state.filterMyShifts;
        updateMineToggleButton();
        rerenderCalendarPreservingPendingChanges({ includeLegend: true });
      });
    }

    if (dom.filterAbsentToggle) {
      dom.filterAbsentToggle.addEventListener("click", async () => {
        state.showAbsentPeople = !state.showAbsentPeople;
        updateAbsentToggleButton();
        rerenderCalendarPreservingPendingChanges({ includeLegend: true });
        if (state.vistaAmpliaActive && api.render.renderVistaAmplia) {
          await api.render.renderVistaAmplia();
        }
      });
    }

    if (dom.filterGrupoSelect) {
      dom.filterGrupoSelect.addEventListener("change", () => {
        state.filterGrupoId = dom.filterGrupoSelect.value;
        filterPersonasByGrupo();
        state.filterPersonaId = dom.filterPersonaSelect?.value || "";
        applyAssignmentFilters();
      });
    }

    if (dom.filterPersonaSelect) {
      dom.filterPersonaSelect.addEventListener("change", () => {
        state.filterPersonaId = dom.filterPersonaSelect.value;
        applyAssignmentFilters();
      });
    }

    if (dom.filterClearBtn) {
      dom.filterClearBtn.addEventListener("click", async () => {
        if (dom.filterGrupoSelect) {
          dom.filterGrupoSelect.value = "";
        }
        if (dom.filterPersonaSelect) {
          dom.filterPersonaSelect.value = "";
        }
        state.filterGrupoId = "";
        state.filterPersonaId = "";
        const hadAbsentFilter = state.showAbsentPeople === true;
        state.showAbsentPeople = false;
        updateAbsentToggleButton();
        filterPersonasByGrupo();

        if (hadAbsentFilter) {
          rerenderCalendarPreservingPendingChanges({ includeLegend: true });
          if (state.vistaAmpliaActive && api.render.renderVistaAmplia) {
            await api.render.renderVistaAmplia();
          }
        } else {
          applyAssignmentFilters();
        }
      });
    }

    if (dom.legendPersonas) {
      dom.legendPersonas.addEventListener("click", (event) => {
        if (!state.vistaAmpliaActive) return;
        const target = event.target instanceof HTMLElement ? event.target : null;
        const item = target?.closest(".legend-persona-item");
        if (!item || !dom.legendPersonas.contains(item)) return;
        const personaId = item.dataset.personaId || "";
        if (!personaId) return;

        if (!(state.legendPersonaIds instanceof Set)) {
          state.legendPersonaIds = new Set();
        }

        if (state.legendPersonaIds.has(personaId)) {
          state.legendPersonaIds.delete(personaId);
        } else {
          if (state.legendPersonaIds.size >= 3) {
            showToast("Puedes seleccionar hasta 3 personas.", "info");
            return;
          }
          state.legendPersonaIds.add(personaId);
        }

        if (state.legendPersonaIds.size === 0) {
          dom.legendPersonas.querySelectorAll(".legend-persona-item.is-selected").forEach((el) => {
            el.classList.remove("is-selected");
          });
        } else {
          item.classList.toggle("is-selected", state.legendPersonaIds.has(personaId));
        }

        applyAssignmentFilters();
      });
    }

    if (dom.previewBtn) {
      dom.previewBtn.addEventListener("click", () => {
        applyPreview();
      });
    }

    if (dom.clearBtn) {
      dom.clearBtn.addEventListener("click", clearPreviewAssignments);
    }

    if (dom.clearPreviewsBtn) {
      dom.clearPreviewsBtn.addEventListener("click", async () => {
        if (state.planificacionPreviewActive) {
          const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
          const token = tokenInput?.value;
          const equipoId = state.planificacionPreviewEquipoId || selectedEquipoId || "";
          try {
            const response = await fetch("/Planificacion/CancelarTurnosPreview", {
              method: "POST",
              headers: {
                "Content-Type": "application/json",
                RequestVerificationToken: token || ""
              },
              body: JSON.stringify({
                equipoId,
                gruposIds
              })
            });
            const data = await response.json().catch(() => ({}));
            if (!response.ok || data.success === false) {
              showToast(data.error || "No se pudo cancelar la previsualizacion.", "danger");
              return;
            }
            showToast("Previsualizacion cancelada.", "info");
          } catch {
            showToast("No se pudo cancelar la previsualizacion.", "danger");
            return;
          }
          state.ignorePlanificacionPreviewOnce = true;
          state.planificacionPreviewActive = false;
          state.planificacionPreviewEquipoId = "";
        }
        clearPreviewAssignments();
        await loadWeekData({ skipPreview: true, clearPreview: true });
      });
    }

    if (dom.savePreviewsBtn) {
      dom.savePreviewsBtn.addEventListener("click", async () => {
        if (state.planificacionPreviewActive) {
          const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
          const token = tokenInput?.value;
          const equipoId = state.planificacionPreviewEquipoId || selectedEquipoId || "";
          try {
            const response = await fetch("/Planificacion/ConfirmarTurnosPreview", {
              method: "POST",
              headers: {
                "Content-Type": "application/json",
                RequestVerificationToken: token || ""
              },
              body: JSON.stringify({
                equipoId,
                gruposIds,
                saveAllWeeks: false,
                weekStart: utils.formatIsoDate(getWeekDates(state.weekOffset)[0])
              })
            });

            const data = await response.json().catch(() => ({}));
            if (!response.ok || data.success === false) {
              showToast(data.error || "No se pudieron guardar los turnos.", "danger");
              return;
            }

            showToast(data.message || "Turnos guardados exitosamente.", "success");
            // Limpieza defensiva: intenta cancelar de todas formas para forzar limpieza de cache.
            try {
              await fetch("/Planificacion/CancelarTurnosPreview", {
                method: "POST",
                headers: {
                  "Content-Type": "application/json",
                  RequestVerificationToken: token || ""
                },
                body: JSON.stringify({
                  equipoId,
                  gruposIds
                })
              });
            } catch {
              // Ignora fallos de limpieza defensiva
            }
            state.ignorePlanificacionPreviewOnce = true;
            state.planificacionPreviewActive = false;
            state.planificacionPreviewEquipoId = "";
            clearPreviewAssignments();
            await loadWeekData({ skipPreview: true, clearPreview: true });
          } catch {
            showToast("No se pudieron guardar los turnos.", "danger");
          }
          return;
        }

        const items = buildPreviewPayload();
        if (items.length === 0) {
          showToast("No hay previsualizaciones para guardar.", "info");
          return;
        }

        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        const token = tokenInput?.value;

        try {
          const response = await fetch("/RegistroTurno/SavePreview", {
            method: "POST",
            headers: {
              "Content-Type": "application/json",
              RequestVerificationToken: token || ""
            },
            body: JSON.stringify({ items })
          });

          if (!response.ok) {
            const data = await response.json().catch(() => ({}));
            showToast(data.message || "No se pudieron guardar los turnos.", "danger");
            return;
          }

          const data = await response.json().catch(() => ({}));
          const created = data.created ?? items.length;
          const skipped = data.skipped ?? 0;
          const summary = skipped > 0
            ? `Turnos guardados: ${created}. Omitidos: ${skipped}.`
            : `Turnos guardados: ${created}.`;
          showToast(summary, "success");
          clearPreviewAssignments();
          await loadWeekData({ skipPreview: true });
        } catch {
          showToast("No se pudieron guardar los turnos.", "danger");
        }
      });
    }

    if (dom.deleteCurrentWeekBtn) {
      dom.deleteCurrentWeekBtn.addEventListener("click", async () => {
        if (!isAdminLikeRole()) {
          showToast("No tienes permisos para eliminar la semana actual.", "danger");
          return;
        }

        const equipoId = selectedEquipoId || dom.calendarShell?.dataset.equipoId || "";
        if (!equipoId) {
          showToast("No se pudo identificar el equipo de la semana actual.", "danger");
          return;
        }

        const weekDates = getWeekDates(state.weekOffset);
        const monday = weekDates[0];
        const sunday = weekDates[6];
        if (!monday || !sunday) {
          showToast("No se pudo identificar el rango semanal visible.", "danger");
          return;
        }

        const shouldDelete = await openDeleteTurnoModal({
          title: "Eliminar semana actual",
          message: `Se eliminaran los turnos guardados del ${utils.formatDate(monday)} al ${utils.formatDate(sunday)}. Solo se borrara de lunes a domingo. Esta accion no se puede deshacer.`,
          confirmLabel: "Eliminar semana"
        });
        if (!shouldDelete) {
          return;
        }

        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        const token = tokenInput?.value;

        try {
          const response = await fetch("/Calendario/DeleteCurrentWeek", {
            method: "POST",
            headers: {
              "Content-Type": "application/json",
              RequestVerificationToken: token || ""
            },
            body: JSON.stringify({
              equipoId,
              weekStart: utils.formatIsoDate(monday)
            })
          });

          const data = await response.json().catch(() => ({}));
          if (!response.ok) {
            showToast(data.message || "No se pudo eliminar la semana actual.", "danger");
            return;
          }

          showToast(
            data.message || "Semana eliminada.",
            data.deletedCount > 0 ? "success" : "info"
          );
          await loadWeekData({ skipPreview: false });
        } catch {
          showToast("No se pudo eliminar la semana actual.", "danger");
        }
      });
    }

    if (dom.deleteSelectedTurnosBtn) {
      dom.deleteSelectedTurnosBtn.addEventListener("click", async () => {
        if (!isAdminLikeRole()) {
          showToast("No tienes permisos para eliminar turnos.", "danger");
          return;
        }

        const turnosSeleccionados = Array.from(state.selectedPersistedTurnos || []).filter(Boolean);
        if (turnosSeleccionados.length === 0) {
          showToast("Selecciona turnos con Ctrl para borrarlos.", "info");
          return;
        }

        const shouldDelete = await openDeleteTurnoModal({
          title: "Eliminar turnos seleccionados",
          message: `Se eliminaran ${turnosSeleccionados.length} turno(s) seleccionados. Esta accion no se puede deshacer.`,
          confirmLabel: "Eliminar turnos"
        });
        if (!shouldDelete) {
          return;
        }

        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        const token = tokenInput?.value;

        try {
          let deletedCount = 0;
          for (const turnoId of turnosSeleccionados) {
            const response = await fetch("/RegistroTurno/DeleteTurno", {
              method: "POST",
              headers: {
                "Content-Type": "application/json",
                RequestVerificationToken: token || ""
              },
              body: JSON.stringify({ turnoId })
            });

            const data = await response.json().catch(() => ({}));
            if (!response.ok) {
              showToast(data.message || "No se pudieron eliminar los turnos seleccionados.", "danger");
              return;
            }

            deletedCount += 1;
          }

          state.selectedPersistedTurnos.clear();
          updateBulkDeleteButton();
          showToast(`Se eliminaron ${deletedCount} turno(s).`, "success");
          await loadWeekData({ skipPreview: false });
        } catch {
          showToast("No se pudieron eliminar los turnos seleccionados.", "danger");
        }
      });
    }

    const saveAllBtn = document.querySelector("[data-save-previews-all]");
    if (saveAllBtn) {
      saveAllBtn.addEventListener("click", async () => {
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        const token = tokenInput?.value;
        const equipoId = state.planificacionPreviewEquipoId || selectedEquipoId || "";

        // "Guardar todas" debe intentar siempre el guardado global (cache de Planificacion).
        // Solo si no existe previsualizacion global hacemos fallback a guardado semanal.
        let shouldFallbackToWeekSave = false;
        try {
          const response = await fetch("/Planificacion/ConfirmarTurnosPreview", {
            method: "POST",
            headers: {
              "Content-Type": "application/json",
              RequestVerificationToken: token || ""
            },
            body: JSON.stringify({
              equipoId,
              gruposIds,
              saveAllWeeks: true,
              weekStart: utils.formatIsoDate(getWeekDates(state.weekOffset)[0])
            })
          });

          const data = await response.json().catch(() => ({}));
          if (response.ok && data.success !== false) {
            showToast(data.message || "Turnos guardados exitosamente.", "success");
            // Limpieza defensiva: intenta cancelar de todas formas para forzar limpieza de cache.
            try {
              await fetch("/Planificacion/CancelarTurnosPreview", {
                method: "POST",
                headers: {
                  "Content-Type": "application/json",
                  RequestVerificationToken: token || ""
                },
                body: JSON.stringify({
                  equipoId,
                  gruposIds
                })
              });
            } catch {
              // Ignora fallos de limpieza defensiva
            }
            state.ignorePlanificacionPreviewOnce = true;
            state.planificacionPreviewActive = false;
            state.planificacionPreviewEquipoId = "";
            clearPreviewAssignments();
            await loadWeekData({ skipPreview: true, clearPreview: true });
            return;
          }

          const errorMessage = (data.error || "").toString();
          const noGlobalPreview =
            errorMessage.toLowerCase().includes("no hay previsualizaciones para guardar");

          if (!noGlobalPreview) {
            showToast(data.error || "No se pudieron guardar los turnos.", "danger");
            return;
          }

          shouldFallbackToWeekSave = true;
        } catch {
          showToast("No se pudieron guardar los turnos.", "danger");
          return;
        }

        if (!shouldFallbackToWeekSave) {
          return;
        }

        const items = buildPreviewPayload();
        if (items.length === 0) {
          showToast("No hay previsualizaciones para guardar.", "info");
          return;
        }

        try {
          const response = await fetch("/RegistroTurno/SavePreview", {
            method: "POST",
            headers: {
              "Content-Type": "application/json",
              RequestVerificationToken: token || ""
            },
            body: JSON.stringify({ items })
          });

          if (!response.ok) {
            const data = await response.json().catch(() => ({}));
            showToast(data.message || "No se pudieron guardar los turnos.", "danger");
            return;
          }

          const data = await response.json().catch(() => ({}));
          const created = data.created ?? items.length;
          const skipped = data.skipped ?? 0;
          const summary = skipped > 0
            ? `Turnos guardados: ${created}. Omitidos: ${skipped}.`
            : `Turnos guardados: ${created}.`;
          showToast(summary, "success");
          clearPreviewAssignments();
          await loadWeekData({ skipPreview: true });
        } catch {
          showToast("No se pudieron guardar los turnos.", "danger");
        }
      });
    }

    if (dom.downloadBtn) {
      dom.downloadBtn.addEventListener("click", () => {
        downloadCalendarImage();
      });
    }

    const generarTurnosOpen = document.querySelector("[data-generar-turnos-open]");
    const generarModalEl = document.getElementById("generarTurnosModalCalendar");
    const generarConfirmBtn = document.getElementById("btnConfirmarGenerarTurnosCalendar");
    const numeroSemanasInput = document.getElementById("numeroSemanasCalendar");
    const nivelDescanso7HorasSelect = document.getElementById("nivelDescanso7HorasCalendar");
    const nivelFinesSemanaConsecutivosSelect = document.getElementById("nivelFinesSemanaConsecutivosCalendar");
    const balancearHorasCheck = document.getElementById("balancearHorasCalendar");
    const fechaInicioInput = document.getElementById("fechaInicioCalendar");
    const generarModal = generarModalEl && window.bootstrap?.Modal
      ? new window.bootstrap.Modal(generarModalEl)
      : null;

    const closeGenerarTurnosDatePicker = () => {
      fechaInicioInput?._flatpickr?.close();
    };

    const toDateInputValue = (date) => {
      const year = date.getFullYear();
      const month = String(date.getMonth() + 1).padStart(2, "0");
      const day = String(date.getDate()).padStart(2, "0");
      return `${year}-${month}-${day}`;
    };

    const parseDateInput = (value) => {
      if (!value) return null;
      const parts = value.split("-").map((item) => Number(item));
      if (parts.length !== 3 || parts.some((n) => Number.isNaN(n))) return null;
      const [year, month, day] = parts;
      return new Date(year, month - 1, day);
    };

    const getWeekMonday = (date) => {
      const safe = new Date(date.getFullYear(), date.getMonth(), date.getDate());
      const day = safe.getDay();
      const diff = (day + 6) % 7;
      safe.setDate(safe.getDate() - diff);
      return safe;
    };

    const lunesActual = getWeekMonday(new Date());
    const lunesActualValue = toDateInputValue(lunesActual);

    if (generarTurnosOpen && generarModalEl) {
      generarTurnosOpen.addEventListener("click", () => {
        if (fechaInicioInput) {
          fechaInicioInput.min = lunesActualValue;
          if (!fechaInicioInput.value) {
            fechaInicioInput.value = lunesActualValue;
          }
        }
        generarModal?.show();
      });

      generarModalEl.addEventListener("keydown", (event) => {
        if (event.key !== "Escape") return;
        if (!fechaInicioInput?._flatpickr?.isOpen) return;
        event.preventDefault();
        event.stopPropagation();
        closeGenerarTurnosDatePicker();
      });

      generarModalEl.addEventListener("hide.bs.modal", () => {
        closeGenerarTurnosDatePicker();
      });
    }

    if (numeroSemanasInput) {
      numeroSemanasInput.addEventListener("input", () => {
        let value = parseInt(numeroSemanasInput.value, 10);
        if (Number.isNaN(value) || value < 1) value = 1;
        if (value > 52) value = 52;
        numeroSemanasInput.value = value.toString();
      });
    }

    if (fechaInicioInput) {
      fechaInicioInput.min = lunesActualValue;
      if (!fechaInicioInput.value) {
        fechaInicioInput.value = lunesActualValue;
      }
      fechaInicioInput.addEventListener("change", () => {
        const selected = parseDateInput(fechaInicioInput.value);
        if (!selected) {
          fechaInicioInput.value = lunesActualValue;
          return;
        }
        let monday = getWeekMonday(selected);
        if (monday < lunesActual) {
          showToast("La semana de inicio no puede ser anterior a la semana actual.", "info");
          monday = lunesActual;
        }
        fechaInicioInput.value = toDateInputValue(monday);
      });
    }

    if (generarConfirmBtn) {
      generarConfirmBtn.addEventListener("click", async () => {
        const value = parseInt(numeroSemanasInput?.value || "0", 10);
        if (Number.isNaN(value) || value < 1 || value > 52) {
          showToast("Ingresa un número válido de semanas (1-52).", "info");
          return;
        }
        const fechaInicio = fechaInicioInput?.value || lunesActualValue;
        const nivelDescanso7Horas = nivelDescanso7HorasSelect?.value || "low";
        const nivelFinesSemanaConsecutivos = nivelFinesSemanaConsecutivosSelect?.value || "low";
        const balancearHoras = balancearHorasCheck?.checked ?? true;
        const validacionOk = await validarGeneracionTurnosCalendar(value, fechaInicio);
        if (!validacionOk) {
          return;
        }
        generarModal?.hide();
        await generarTurnosCalendar(
          value,
          fechaInicio,
          false,
          nivelDescanso7Horas,
          nivelFinesSemanaConsecutivos,
          balancearHoras
        );
      });
    }

    bindPreviewAutoUpdate(dom.personaSelect);
    bindPreviewAutoUpdate(dom.turnoSelect);
    bindPreviewAutoUpdate(dom.fechaInicioInput);
    bindPreviewAutoUpdate(dom.fechaFinInput);

    if (dom.personaSelect) {
      dom.personaSelect.addEventListener("change", (event) => {
        loadGruposForPersona(event.target.value);
      });
    }

    if (dom.panelOpen) {
      dom.panelOpen.addEventListener("click", openPanel);
    }

    if (dom.panelClose) {
      dom.panelClose.addEventListener("click", closePanel);
    }

    if (dom.panelScrim) {
      dom.panelScrim.addEventListener("click", closePanel);
    }

    if (dom.vacationOpen) {
      dom.vacationOpen.addEventListener("click", openVacationPanel);
    }

    if (dom.vacationClose) {
      dom.vacationClose.addEventListener("click", closeVacationPanel);
    }

    if (dom.vacationScrim) {
      dom.vacationScrim.addEventListener("click", closeVacationPanel);
    }

    if (dom.vacationCalendar) {
      dom.vacationCalendar.addEventListener("click", handleVacationDateClick);
    }

    if (dom.vacationClear) {
      dom.vacationClear.addEventListener("click", resetVacationRange);
    }

    if (dom.vacationApply) {
      dom.vacationApply.addEventListener("click", () => {
        if (!vacationRange.start || !vacationRange.end) {
          showToast("Selecciona un rango de fechas.", "info");
          return;
        }
        if (dom.vacationPanel) {
          dom.vacationPanel.dataset.start = utils.formatIsoDate(vacationRange.start);
          dom.vacationPanel.dataset.end = utils.formatIsoDate(vacationRange.end);
        }
        setVacationReview();
        showToast("Rango listo para solicitar vacaciones.", "success");
      });
    }

    if (dom.vacationSubmit) {
      dom.vacationSubmit.addEventListener("click", async () => {
        if (!vacationRange.start || !vacationRange.end) {
          showToast("Selecciona un rango valido.", "info");
          return;
        }

        const tipoSolicitudId = dom.vacationPanel?.dataset.vacationTipoId || "";
        if (!tipoSolicitudId) {
          showToast("Tipo de solicitud no configurado.", "danger");
          return;
        }

        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        const token = tokenInput?.value;
        const payload = {
          tipoSolicitudId,
          vacacion: {
            fechaInicio: utils.formatIsoDate(vacationRange.start),
            fechaFin: utils.formatIsoDate(vacationRange.end)
          }
        };

        try {
          const response = await fetch("/Solicitudes/CreateVacacion", {
            method: "POST",
            headers: {
              "Content-Type": "application/json",
              RequestVerificationToken: token || ""
            },
            body: JSON.stringify(payload)
          });

          if (!response.ok) {
            const data = await response.json().catch(() => ({}));
            showToast(data.message || "No se pudo crear la solicitud.", "danger");
            return;
          }

          showToast("Solicitud de vacaciones enviada.", "success");
          resetVacationRange();
          closeVacationPanel();
        } catch {
          showToast("No se pudo crear la solicitud.", "danger");
        }
      });
    }

    initVacationYearSelect();
    renderVacationCalendar();
    setVacationSummary();

    if (dom.permisoOpen) {
      dom.permisoOpen.addEventListener("click", openPermisoPanel);
    }

    if (dom.permisoClose) {
      dom.permisoClose.addEventListener("click", closePermisoPanel);
    }

    if (dom.permisoScrim) {
      dom.permisoScrim.addEventListener("click", closePermisoPanel);
    }

    if (dom.permisoCalendar) {
      dom.permisoCalendar.addEventListener("click", handlePermisoDateClick);
    }

    if (dom.permisoClear) {
      dom.permisoClear.addEventListener("click", resetPermisoSelection);
    }

    if (dom.permisoInicio) {
      dom.permisoInicio.addEventListener("input", setPermisoSummary);
    }

    if (dom.permisoFin) {
      dom.permisoFin.addEventListener("input", setPermisoSummary);
    }

    if (dom.permisoApply) {
      dom.permisoApply.addEventListener("click", () => {
        if (!permisoSelection.date) {
          showToast("Selecciona un dia.", "info");
          return;
        }
        const start = parseTimeValue(dom.permisoInicio?.value || "");
        const end = parseTimeValue(dom.permisoFin?.value || "");
        if (start === null || end === null || end <= start) {
          showToast("Ingresa un rango horario valido.", "info");
          return;
        }
        setPermisoReview();
        showToast("Permiso listo para solicitar.", "success");
      });
    }

    if (dom.permisoSubmit) {
      dom.permisoSubmit.addEventListener("click", async () => {
        if (!permisoSelection.date) {
          showToast("Selecciona un dia valido.", "info");
          return;
        }
        const startValue = dom.permisoInicio?.value || "";
        const endValue = dom.permisoFin?.value || "";
        const start = parseTimeValue(startValue);
        const end = parseTimeValue(endValue);
        if (start === null || end === null || end <= start) {
          showToast("Ingresa un rango horario valido.", "info");
          return;
        }

        const tipoSolicitudId = dom.permisoPanel?.dataset.permisoTipoId || "";
        if (!tipoSolicitudId) {
          showToast("Tipo de solicitud no configurado.", "danger");
          return;
        }

        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        const token = tokenInput?.value;
        const payload = {
          tipoSolicitudId,
          fecha: utils.formatIsoDate(permisoSelection.date),
          horaInicio: startValue,
          horaFin: endValue,
          motivo: dom.permisoMotivo?.value?.trim() || ""
        };

        try {
          const response = await fetch("/Solicitudes/CreatePermiso", {
            method: "POST",
            headers: {
              "Content-Type": "application/json",
              RequestVerificationToken: token || ""
            },
            body: JSON.stringify(payload)
          });

          if (!response.ok) {
            const data = await response.json().catch(() => ({}));
            showToast(data.message || "No se pudo crear la solicitud.", "danger");
            return;
          }

          showToast("Solicitud de permiso enviada.", "success");
          resetPermisoSelection();
          closePermisoPanel();
        } catch {
          showToast("No se pudo crear la solicitud.", "danger");
        }
      });
    }

    initPermisoYearSelect();
    renderPermisoCalendar();
    setPermisoSummary();

    bindCalamidadReplacementBarEvents();

    if (dom.grid) {
      bindCambioLinkedHover(dom.grid);
      bindCalamidadGridReplacementClick();
      dom.grid.addEventListener("click", (event) => {
        const target = event.target instanceof HTMLElement ? event.target : null;
        if (!target) return;
        const addBtn = target.closest(".cell-add-btn");
        if (!addBtn) return;
        if (!state.canAdd) {
          showToast("No puedes agregar turnos.", "info");
          return;
        }
        event.preventDefault();
        event.stopPropagation();
        const cell = addBtn.closest(".shift-cell");
        if (!cell) return;
        openCellPopover(cell, { force: true });
      });

      dom.grid.addEventListener("click", (event) => {
        const target = event.target instanceof HTMLElement ? event.target : null;
        if (!target) return;
        const removeBtn = target.closest(".preview-remove");
        if (!removeBtn) return;
        event.preventDefault();
        event.stopPropagation();
        const card = removeBtn.closest(".shift-card");
        const previewId = card?.dataset.assignment;
        if (!previewId) return;
        state.previewAssignments = state.previewAssignments.filter((item) => item.id !== previewId);
        rerenderCalendarPreservingPendingChanges({
          includeLegend: true,
          includeSaveVisibility: true
        });
      });

      dom.grid.addEventListener("click", async (event) => {
        const target = event.target instanceof HTMLElement ? event.target : null;
        if (!target) return;
        const deleteBtn = target.closest(".saved-remove");
        if (!deleteBtn) return;

        event.preventDefault();
        event.stopPropagation();

        const card = deleteBtn.closest(".shift-card");
        if (!card || isReadOnlyCard(card)) return;

        const turnoId = card.dataset.assignment;
        if (!turnoId) return;

        const shouldDelete = await openDeleteTurnoModal({
          message: "Eliminar este turno guardado? Esta accion no se puede deshacer.",
          confirmLabel: "Eliminar"
        });
        if (!shouldDelete) return;

        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        const token = tokenInput?.value;

        try {
          const response = await fetch("/RegistroTurno/DeleteTurno", {
            method: "POST",
            headers: {
              "Content-Type": "application/json",
              RequestVerificationToken: token || ""
            },
            body: JSON.stringify({ turnoId })
          });

          const data = await response.json().catch(() => ({}));
          if (!response.ok) {
            showToast(data.message || "No se pudo eliminar el turno.", "danger");
            return;
          }

          showToast(data.message || "Turno eliminado.", "success");
          state.selectedPersistedTurnos.delete(turnoId);
          updateBulkDeleteButton();
          await loadWeekData({ skipPreview: false });
        } catch {
          showToast("No se pudo eliminar el turno.", "danger");
        }
      });

      dom.grid.addEventListener("click", (event) => {
        const target = event.target instanceof HTMLElement ? event.target : null;
        if (!target || !event.ctrlKey) return;
        const card = target.closest(".shift-card");
        if (!card || target.closest(".saved-remove") || target.closest(".preview-remove")) return;
        if (isReadOnlyCard(card)) return;

        event.preventDefault();
        event.stopPropagation();
        clearSelectedCells();
        toggleSelectedPersistedTurno(card);
      });

      dom.grid.addEventListener("click", (event) => {
        const target = event.target instanceof HTMLElement ? event.target : null;
        if (!target) return;
        const card = target.closest(".shift-card");
        const cell = target.closest(".shift-cell");
        if (!cell) return;
        if (event.ctrlKey) {
          if (card && !target.closest(".saved-remove") && !target.closest(".preview-remove")) {
            return;
          }
          clearSelectedPersistedTurnos();
          toggleSelectedCell(cell);
          return;
        }
        clearSelectedPersistedTurnos();
        if (!state.canAdd) {
          const guardKey = "add-turnos";
          if (!state.dragGuardrailsShown.has(guardKey)) {
            showToast("No puedes agregar turnos.", "info");
            state.dragGuardrailsShown.add(guardKey);
          }
          return;
        }
        const key = `${cell.dataset.day || ""}:${cell.dataset.shift || ""}`;
        if (state.selectedPreviewCells.has(key)) {
          openCellPopover(cell, { force: true });
          return;
        }
        if (cell.querySelector(".shift-card")) return;
        openCellPopover(cell);
      });
    }

    if (dom.gridAmplia) {
      bindCambioLinkedHover(dom.gridAmplia);
      dom.gridAmplia.addEventListener("click", (event) => {
        const target = event.target instanceof HTMLElement ? event.target : null;
        if (!target || !event.ctrlKey) return;
        const card = target.closest(".shift-card");
        if (!card) return;
        if (isReadOnlyCard(card)) return;

        event.preventDefault();
        event.stopPropagation();
        clearSelectedCells();
        toggleSelectedPersistedTurno(card);
      });
    }

    initMonthSelectors();
    if (weekStartParam && dom.monthSelect && dom.yearSelect) {
      const parsed = new Date(`${weekStartParam}T00:00:00`);
      if (!Number.isNaN(parsed.getTime())) {
        dom.monthSelect.value = parsed.getMonth().toString();
        dom.yearSelect.value = parsed.getFullYear().toString();
      }
    }

    updateSaveVisibility();

    if (dom.personaSelect?.value) {
      loadGruposForPersona(dom.personaSelect.value);
    } else {
      clearGrupos();
    }
    updateWeekNavLabels();
    updatePendingBar();
  };

  const onAssignmentsRendered = () => {
    restorePendingChangesAfterRender();
    refreshCalamidadReplacementUi();
  };

  api.interactions = {
    init,
    onAssignmentsRendered,
    bindCardContextMenu,
    syncSelectedPersistedTurnos,
    capturePendingChangesBeforeRender
  };
})();

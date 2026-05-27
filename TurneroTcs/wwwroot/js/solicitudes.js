document.addEventListener("DOMContentLoaded", function () {
  var list = Array.from(document.querySelectorAll("[data-request-item]"));
  var detailScope = document.querySelector(".detail-card");
  var detailTitle = detailScope?.querySelector("[data-detail-title]");
  var detailUser = detailScope?.querySelector("[data-detail-user]");
  var detailAvatar = detailScope?.querySelector("[data-detail-avatar]");
  var detailBody = detailScope?.querySelector("[data-detail-body]");
  var detailEmptyState = detailScope?.querySelector("[data-detail-empty-state]");
  var detailEmptyTitle = detailScope?.querySelector("[data-detail-empty-title]");
  var detailEmptyMessage = detailScope?.querySelector("[data-detail-empty-message]");
  var detailUserStrong = detailScope?.querySelectorAll("[data-detail-user-strong]") || [];
  var detailDays = detailScope?.querySelectorAll("[data-detail-days]") || [];
  var detailStatus = detailScope?.querySelectorAll("[data-detail-status]") || [];
  var startMonth = detailScope?.querySelectorAll("[data-detail-start-month]") || [];
  var startDay = detailScope?.querySelectorAll("[data-detail-start-day]") || [];
  var endMonth = detailScope?.querySelectorAll("[data-detail-end-month]") || [];
  var endDay = detailScope?.querySelectorAll("[data-detail-end-day]") || [];
  var detailEquipo = detailScope?.querySelectorAll("[data-detail-equipo]") || [];
  var detailGrupo = detailScope?.querySelectorAll("[data-detail-grupo]") || [];
  var detailAprobador1 = detailScope?.querySelectorAll("[data-detail-aprobador1]") || [];
  var detailAprobador1Fecha = detailScope?.querySelectorAll("[data-detail-aprobador1-fecha]") || [];
  var detailAprobador2 = detailScope?.querySelectorAll("[data-detail-aprobador2]") || [];
  var detailAprobador2Fecha = detailScope?.querySelectorAll("[data-detail-aprobador2-fecha]") || [];
  var permisoDay = detailScope?.querySelectorAll("[data-detail-permiso-day]") || [];
  var permisoMonth = detailScope?.querySelectorAll("[data-detail-permiso-month]") || [];
  var permisoTime = detailScope?.querySelector("[data-detail-permiso-time]");
  var permisoMotivo = detailScope?.querySelector("[data-detail-permiso-motivo]");
  var calamidadMotivo = detailScope?.querySelector("[data-detail-calamidad-motivo]");
  var cambioOrigenNombre = detailScope?.querySelector("[data-detail-cambio-origen-nombre]");
  var cambioOrigenMonth = detailScope?.querySelector("[data-detail-cambio-origen-month]");
  var cambioOrigenDay = detailScope?.querySelector("[data-detail-cambio-origen-day]");
  var cambioOrigenTurno = detailScope?.querySelector("[data-detail-cambio-origen-turno]");
  var cambioDestinoNombre = detailScope?.querySelector("[data-detail-cambio-destino-nombre]");
  var cambioDestinoMonth = detailScope?.querySelector("[data-detail-cambio-destino-month]");
  var cambioDestinoDay = detailScope?.querySelector("[data-detail-cambio-destino-day]");
  var cambioDestinoTurno = detailScope?.querySelector("[data-detail-cambio-destino-turno]");
  var cambioMotivo = detailScope?.querySelector("[data-detail-cambio-motivo]");
  var defaultBlocks = Array.from(document.querySelectorAll("[data-detail-default]"));
  var vacationBlock = document.querySelector("[data-detail-vacation]");
  var permisoBlock = document.querySelector("[data-detail-permiso]");
  var calamidadBlock = document.querySelector("[data-detail-calamidad]");
  var cambioBlock = document.querySelector("[data-detail-cambio]");
  var tabCountPendientes = document.querySelector("[data-tab-count='pendientes']");
  var tabCountHistorial = document.querySelector("[data-tab-count='historial']");
  var searchInput = document.querySelector("[data-panel-search]");
  var filterSelect = document.querySelector("[data-panel-filter]");
  var sortSelect = document.querySelector("[data-panel-sort]");
  var requestListContainer = document.querySelector(".request-list");
  var approveBtns = Array.from(document.querySelectorAll("[data-action-approve]"));
  var rejectBtns = Array.from(document.querySelectorAll("[data-action-reject]"));
  var cancelBtns = Array.from(document.querySelectorAll("[data-action-cancel]"));
  var panelTabs = document.querySelector("[data-panel-tabs]");
  var tabButtons = Array.from(document.querySelectorAll("[data-tab]"));
  var activeTab = tabButtons.find(function (button) {
    return button.classList.contains("is-active");
  })?.dataset?.tab || "pendientes";
  var shell = document.querySelector(".solicitudes-shell");
  var userRole = (shell?.dataset?.userRole || "Usuario").toLowerCase();
  var parseFlag = function (value) {
    return (value || "").toString().toLowerCase() === "true";
  };
  var canApproveVacacion = parseFlag(shell?.dataset?.canApproveVacacion);
  var canRejectVacacion = parseFlag(shell?.dataset?.canRejectVacacion);
  var canApprovePermiso = parseFlag(shell?.dataset?.canApprovePermiso);
  var canRejectPermiso = parseFlag(shell?.dataset?.canRejectPermiso);
  var canApproveCambio = parseFlag(shell?.dataset?.canApproveCambio);
  var canRejectCambio = parseFlag(shell?.dataset?.canRejectCambio);
  var isAdmin = userRole === "admin" || userRole === "superadmin";
  var isLider = userRole === "lider";
  var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
  var detailActions = Array.from(document.querySelectorAll("[data-detail-actions]"));
  var detailNotes = Array.from(document.querySelectorAll("[data-detail-action-note]"));
  var detailHint = detailScope?.querySelector("[data-detail-review-hint]");

  if (!list.length) {
    return;
  }

  list.forEach(function (item, index) {
    item.dataset.order = index.toString();
  });

  var showToast = function (message, variant) {
    window.AppToast?.show(message, variant || "danger", { delay: 2600, title: "Solicitudes" });
  };

  var isCalamidadType = function (typeText) {
    var type = (typeText || "").toLowerCase();
    return type.includes("calam") || type.includes("ausencia");
  };

  var getAvatarConfig = function (typeText) {
    var type = (typeText || "").toLowerCase();
    if (type.includes("vacaciones")) {
      return {
        variant: "is-vacaciones",
        svg: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M8 7V5a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path><rect x="3" y="7" width="18" height="13" rx="3"></rect><path d="M3 12h18"></path></svg>'
      };
    }
    if (type.includes("permiso")) {
      return {
        variant: "is-permiso",
        svg: '<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="8"></circle><path d="M12 8v4l3 2"></path></svg>'
      };
    }
    if (isCalamidadType(type)) {
      return {
        variant: "is-calamidad",
        svg: '<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="8"></circle><path d="M12 8v8"></path><path d="M8 12h8"></path></svg>'
      };
    }
    if (type.includes("cambio")) {
      return {
        variant: "is-cambio",
        svg: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M7 7h11"></path><path d="m14 4 4 3-4 3"></path><path d="M17 17H6"></path><path d="m10 14-4 3 4 3"></path></svg>'
      };
    }

    return {
      variant: "is-default",
      svg: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M8 3h6l4 4v14H6a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z"></path><path d="M14 3v5h5"></path><path d="M9 12h6"></path><path d="M9 16h6"></path></svg>'
    };
  };

  var setDetailAvatar = function (typeText) {
    if (!detailAvatar) return;
    var config = getAvatarConfig(typeText);
    detailAvatar.className = "detail-avatar " + config.variant;
    detailAvatar.innerHTML = config.svg;
  };

  var showDetailEmptyState = function (title, message) {
    if (detailBody) {
      detailBody.hidden = true;
    }
    if (detailEmptyState) {
      detailEmptyState.hidden = false;
    }
    if (detailEmptyTitle) {
      detailEmptyTitle.textContent = title || "Sin solicitudes pendientes";
    }
    if (detailEmptyMessage) {
      detailEmptyMessage.textContent = message || "No hay solicitudes pendientes esperando revision.";
    }
    if (detailTitle) {
      detailTitle.textContent = title || "Sin solicitudes pendientes";
    }
    if (detailUser) {
      detailUser.textContent = message || "No hay solicitudes pendientes esperando revision.";
    }
    setDetailAvatar("");
  };

  var hideDetailEmptyState = function () {
    if (detailEmptyState) {
      detailEmptyState.hidden = true;
    }
    if (detailBody) {
      detailBody.hidden = false;
    }
  };

  var normalizeStatusCode = function (statusCode, statusText) {
    var code = (statusCode || "").toString().trim().toLowerCase();
    if (code === "pending" || code === "inreview" || code === "approved" || code === "rejected" || code === "cancelled") {
      return code;
    }

    var status = (statusText || "").toString().trim().toLowerCase();
    if (status === "aprobado") return "approved";
    if (status === "rechazado") return "rejected";
    if (status === "cancelado") return "cancelled";
    if (status === "en aprobacion") return "inreview";
    return "pending";
  };

  var readData = function (item) {
    var data = item.dataset || {};
    return {
      requesterPersonaId: data.requesterPersonaId || "",
      isOwnedByCurrentPersona: parseFlag(data.requestOwned),
      title: data.title || "Solicitud",
      user: data.user || "Usuario",
      type: data.type || "",
      startMonth: data.startMonth || "-",
      startDay: data.startDay || "-",
      endMonth: data.endMonth || "-",
      endDay: data.endDay || "-",
      days: data.days || "-",
      status: data.status || "Pendiente",
      statusCode: normalizeStatusCode(data.statusCode, data.status),
      equipo: data.equipo || "-",
      grupo: data.grupo || "-",
      aprobador1: data.aprobador1 || "Pendiente",
      aprobador1Fecha: data.aprobador1Fecha || "-",
      aprobador2: data.aprobador2 || "Pendiente",
      aprobador2Fecha: data.aprobador2Fecha || "-",
      permisoHoraInicio: data.permisoHoraInicio || "",
      permisoHoraFin: data.permisoHoraFin || "",
      permisoMotivo: data.permisoMotivo || "-",
      calamidadMotivo: data.calamidadMotivo || "-",
      cambioOrigenNombre: data.cambioOrigenNombre || "Usuario",
      cambioOrigenMonth: data.cambioOrigenMonth || "-",
      cambioOrigenDay: data.cambioOrigenDay || "-",
      cambioOrigenTurno: data.cambioOrigenTurno || "-",
      cambioDestinoNombre: data.cambioDestinoNombre || "Usuario",
      cambioDestinoMonth: data.cambioDestinoMonth || "-",
      cambioDestinoDay: data.cambioDestinoDay || "-",
      cambioDestinoTurno: data.cambioDestinoTurno || "-",
      cambioMotivo: data.cambioMotivo || "-"
    };
  };

  var getSolicitudKind = function (typeText) {
    var type = (typeText || "").toLowerCase();
    if (type.includes("vac")) {
      return "vacacion";
    }
    if (type.includes("permiso")) {
      return "permiso";
    }
    if (type.includes("cambio")) {
      return "cambio";
    }
    return "";
  };

  var hasPermissionForType = function (typeText, action) {
    var kind = getSolicitudKind(typeText);
    if (kind === "vacacion") {
      return action === "approve" ? canApproveVacacion : canRejectVacacion;
    }
    if (kind === "permiso") {
      return action === "approve" ? canApprovePermiso : canRejectPermiso;
    }
    if (kind === "cambio") {
      return action === "approve" ? canApproveCambio : canRejectCambio;
    }
    return false;
  };

  var getDecisionState = function (statusText, statusCode, typeText) {
    var requestData = typeof statusText === "object" && statusText !== null
      ? statusText
      : {
          status: statusText,
          statusCode: statusCode,
          type: typeText,
          isOwnedByCurrentPersona: false
        };
    var resolvedStatusCode = normalizeStatusCode(statusCode, statusText);
    if (typeof statusText === "object" && statusText !== null) {
      resolvedStatusCode = normalizeStatusCode(requestData.statusCode, requestData.status);
    }

    var isApproved = resolvedStatusCode === "approved";
    var isRejected = resolvedStatusCode === "rejected";
    var isCancelled = resolvedStatusCode === "cancelled";
    var isResolved = isApproved || isRejected || isCancelled;
    var canApproveByPermission = hasPermissionForType(requestData.type, "approve");
    var canRejectByPermission = hasPermissionForType(requestData.type, "reject");
    var hasAnyTypePermission = canApproveByPermission || canRejectByPermission;
    var canDecideInStage = resolvedStatusCode === "pending" || (resolvedStatusCode === "inreview" && isAdmin);
    var canCancelInReview =
      !isRejected &&
      !isCancelled &&
      (resolvedStatusCode === "pending" || resolvedStatusCode === "inreview") &&
      (isAdmin || isLider || requestData.isOwnedByCurrentPersona);
    var canCancelApproved = isApproved && isAdmin;

    var canApprove = !isResolved && canDecideInStage && canApproveByPermission;
    var canReject = !isResolved && canDecideInStage && canRejectByPermission;
    var canCancel = canCancelInReview || canCancelApproved;
    var hideActions = !canApprove && !canReject && !canCancel;

    var hint = "";
    if (!hideActions && !canDecideInStage && (canApproveByPermission || canRejectByPermission)) {
      hint = canCancel
        ? "Solo Admin o SuperAdmin pueden aprobar o rechazar en esta etapa."
        : "Solo Admin o SuperAdmin pueden decidir en esta etapa.";
    }

    return {
      canApprove: canApprove,
      canReject: canReject,
      canCancel: canCancel,
      hideActions: hideActions,
      hint: hint
    };
  };

  var updateApprovalUi = function (statusText, statusCode, typeText) {
    var state = getDecisionState(statusText, statusCode, typeText);

    if (detailHint) {
      detailHint.textContent = state.hint;
      detailHint.hidden = !state.hint;
    }

    approveBtns.forEach(function (btn) {
      btn.hidden = !state.canApprove;
      btn.disabled = !state.canApprove;
    });
    rejectBtns.forEach(function (btn) {
      btn.hidden = !state.canReject;
      btn.disabled = !state.canReject;
    });
    cancelBtns.forEach(function (btn) {
      btn.hidden = !state.canCancel;
      btn.disabled = !state.canCancel;
    });

    detailActions.forEach(function (el) {
      el.hidden = state.hideActions;
      el.classList.toggle("is-disabled", !state.canApprove && !state.canReject && !state.canCancel);
    });
    detailNotes.forEach(function (note) {
      note.textContent = state.hint || "";
      note.hidden = state.hideActions || !state.hint;
    });
  };

  var updateApprovalTimeline = function (data) {
    if (!detailScope) return;

    var stepSolicitada = detailScope.querySelector("[data-approval-step='solicitada']");
    var stepAprobador1 = detailScope.querySelector("[data-approval-step='aprobador1']");
    var stepAprobador2 = detailScope.querySelector("[data-approval-step='aprobador2']");
    var stepResultado = detailScope.querySelector("[data-approval-step='resultado']");
    var statusCode = normalizeStatusCode(data.statusCode, data.status);

    [stepSolicitada, stepAprobador1, stepAprobador2, stepResultado].forEach(function (step) {
      if (!step) return;
      step.classList.remove("is-complete", "is-current", "is-pending", "is-approved", "is-rejected", "is-cancelled");
      step.classList.add("is-pending");
    });

    if (stepSolicitada) {
      stepSolicitada.classList.remove("is-pending");
      stepSolicitada.classList.add("is-complete");
    }

    var aprobador1Done = Boolean(data.aprobador1 && data.aprobador1 !== "Pendiente" && data.aprobador1 !== "-");
    var aprobador2Done = Boolean(data.aprobador2 && data.aprobador2 !== "Pendiente" && data.aprobador2 !== "-");

    if (stepAprobador1) {
      stepAprobador1.classList.remove("is-pending");
      if (aprobador1Done) {
        stepAprobador1.classList.add("is-complete");
      } else {
        stepAprobador1.classList.add(statusCode === "pending" ? "is-current" : "is-pending");
      }
    }

    if (stepAprobador2) {
      stepAprobador2.classList.remove("is-pending");
      if (aprobador2Done) {
        stepAprobador2.classList.add("is-complete");
      } else {
        stepAprobador2.classList.add(statusCode === "inreview" ? "is-current" : "is-pending");
      }
    }

    if (stepResultado) {
      stepResultado.classList.remove("is-pending");
      if (statusCode === "approved") {
        stepResultado.classList.add("is-complete", "is-approved");
      } else if (statusCode === "rejected") {
        stepResultado.classList.add("is-complete", "is-rejected");
      } else if (statusCode === "cancelled") {
        stepResultado.classList.add("is-complete", "is-cancelled");
      } else {
        stepResultado.classList.add("is-pending");
      }
    }
  };

  var renderDetail = function (item) {
    var data = readData(item);
    setDetailAvatar(data.type);
    if (detailTitle) detailTitle.textContent = data.title;
    if (detailUser) detailUser.textContent = data.user;
    detailUserStrong.forEach(function (el) {
      el.textContent = data.user;
    });
    detailDays.forEach(function (el) {
      el.textContent = data.days;
    });
    detailStatus.forEach(function (el) {
      el.textContent = data.status;
    });
    startMonth.forEach(function (el) {
      el.textContent = data.startMonth;
    });
    startDay.forEach(function (el) {
      el.textContent = data.startDay;
    });
    endMonth.forEach(function (el) {
      el.textContent = data.endMonth;
    });
    endDay.forEach(function (el) {
      el.textContent = data.endDay;
    });
    detailEquipo.forEach(function (el) {
      el.textContent = data.equipo;
    });
    detailGrupo.forEach(function (el) {
      el.textContent = data.grupo;
    });
    detailAprobador1.forEach(function (el) {
      el.textContent = data.aprobador1;
    });
    detailAprobador1Fecha.forEach(function (el) {
      el.textContent = data.aprobador1Fecha;
    });
    detailAprobador2.forEach(function (el) {
      el.textContent = data.aprobador2;
    });
    detailAprobador2Fecha.forEach(function (el) {
      el.textContent = data.aprobador2Fecha;
    });
    updateApprovalTimeline(data);
    updateApprovalUi(data);
    permisoDay.forEach(function (el) {
      el.textContent = data.startDay;
    });
    permisoMonth.forEach(function (el) {
      el.textContent = data.startMonth;
    });
    if (permisoTime) {
      permisoTime.textContent = data.permisoHoraInicio && data.permisoHoraFin
        ? data.permisoHoraInicio + " - " + data.permisoHoraFin
        : "--";
    }
    if (permisoMotivo) permisoMotivo.textContent = data.permisoMotivo || "-";
    if (calamidadMotivo) calamidadMotivo.textContent = data.calamidadMotivo || "-";
    if (cambioOrigenNombre) cambioOrigenNombre.textContent = data.cambioOrigenNombre;
    if (cambioOrigenMonth) cambioOrigenMonth.textContent = data.cambioOrigenMonth;
    if (cambioOrigenDay) cambioOrigenDay.textContent = data.cambioOrigenDay;
    if (cambioOrigenTurno) cambioOrigenTurno.textContent = data.cambioOrigenTurno;
    if (cambioDestinoNombre) cambioDestinoNombre.textContent = data.cambioDestinoNombre;
    if (cambioDestinoMonth) cambioDestinoMonth.textContent = data.cambioDestinoMonth;
    if (cambioDestinoDay) cambioDestinoDay.textContent = data.cambioDestinoDay;
    if (cambioDestinoTurno) cambioDestinoTurno.textContent = data.cambioDestinoTurno;
    if (cambioMotivo) cambioMotivo.textContent = data.cambioMotivo || "-";

    var typeLower = data.type.toLowerCase();
    if (typeLower.includes("vacaciones")) {
      defaultBlocks.forEach(function (el) {
        el.hidden = true;
      });
      if (vacationBlock) {
        vacationBlock.hidden = false;
      }
      if (permisoBlock) {
        permisoBlock.hidden = true;
      }
      if (calamidadBlock) {
        calamidadBlock.hidden = true;
      }
      if (cambioBlock) {
        cambioBlock.hidden = true;
      }
    } else if (typeLower.includes("permiso")) {
      defaultBlocks.forEach(function (el) {
        el.hidden = true;
      });
      if (vacationBlock) {
        vacationBlock.hidden = true;
      }
      if (permisoBlock) {
        permisoBlock.hidden = false;
      }
      if (calamidadBlock) {
        calamidadBlock.hidden = true;
      }
      if (cambioBlock) {
        cambioBlock.hidden = true;
      }
    } else if (isCalamidadType(typeLower)) {
      defaultBlocks.forEach(function (el) {
        el.hidden = true;
      });
      if (vacationBlock) {
        vacationBlock.hidden = true;
      }
      if (permisoBlock) {
        permisoBlock.hidden = true;
      }
      if (calamidadBlock) {
        calamidadBlock.hidden = false;
      }
      if (cambioBlock) {
        cambioBlock.hidden = true;
      }
    } else if (typeLower.includes("cambio")) {
      defaultBlocks.forEach(function (el) {
        el.hidden = true;
      });
      if (vacationBlock) {
        vacationBlock.hidden = true;
      }
      if (permisoBlock) {
        permisoBlock.hidden = true;
      }
      if (calamidadBlock) {
        calamidadBlock.hidden = true;
      }
      if (cambioBlock) {
        cambioBlock.hidden = false;
      }
    } else {
      defaultBlocks.forEach(function (el) {
        el.hidden = false;
      });
      if (vacationBlock) {
        vacationBlock.hidden = true;
      }
      if (permisoBlock) {
        permisoBlock.hidden = true;
      }
      if (calamidadBlock) {
        calamidadBlock.hidden = true;
      }
      if (cambioBlock) {
        cambioBlock.hidden = true;
      }
    }
  };

  var setActive = function (item) {
    list.forEach(function (el) {
      el.classList.remove("is-active");
    });
    item.classList.add("is-active");
    hideDetailEmptyState();
    renderDetail(item);
  };

  var updateCount = function () {
    // no-op retained for backward compatibility
  };

  var updateTabCounts = function (pendientes, historial) {
    if (tabCountPendientes) {
      tabCountPendientes.textContent = String(pendientes);
    }
    if (tabCountHistorial) {
      tabCountHistorial.textContent = String(historial);
    }
  };

  var getStatusSortWeight = function (statusCode) {
    switch (normalizeStatusCode(statusCode, "")) {
      case "pending":
        return 0;
      case "inreview":
        return 1;
      case "approved":
        return 2;
      case "rejected":
        return 3;
      case "cancelled":
        return 4;
      default:
        return 9;
    }
  };

  var matchesSelectedType = function (typeText, filterType) {
    var normalizedFilter = (filterType || "").toLowerCase().trim();
    if (!normalizedFilter) {
      return true;
    }

    if (normalizedFilter === "ausencia" || normalizedFilter === "calamidad") {
      return isCalamidadType(typeText);
    }

    return (typeText || "").toLowerCase() === normalizedFilter;
  };

  var applySorting = function () {
    if (!requestListContainer) {
      return;
    }

    var sortValue = (sortSelect?.value || "recent").toLowerCase();
    var sorted = list.slice().sort(function (a, b) {
      var dataA = readData(a);
      var dataB = readData(b);
      var orderA = Number.parseInt(a.dataset.order || "0", 10) || 0;
      var orderB = Number.parseInt(b.dataset.order || "0", 10) || 0;

      if (sortValue === "oldest") {
        return orderB - orderA;
      }

      if (sortValue === "status") {
        var byStatus = getStatusSortWeight(dataA.statusCode) - getStatusSortWeight(dataB.statusCode);
        return byStatus !== 0 ? byStatus : orderA - orderB;
      }

      if (sortValue === "type") {
        var byType = (dataA.type || "").localeCompare(dataB.type || "", "es", { sensitivity: "base" });
        return byType !== 0 ? byType : orderA - orderB;
      }

      if (sortValue === "name") {
        var byName = (dataA.user || "").localeCompare(dataB.user || "", "es", { sensitivity: "base" });
        return byName !== 0 ? byName : orderA - orderB;
      }

      // recent (default): server order descending by FechaSolicitud
      return orderA - orderB;
    });

    sorted.forEach(function (item) {
      requestListContainer.appendChild(item);
    });

    list = sorted;
  };

  var updateStatusClasses = function (item, statusText, statusCode) {
    var normalized = normalizeStatusCode(statusCode, statusText);
    item.classList.remove("is-resolved", "is-approved", "is-rejected", "is-cancelled");
    if (normalized === "approved") {
      item.classList.add("is-resolved", "is-approved");
    } else if (normalized === "rejected") {
      item.classList.add("is-resolved", "is-rejected");
    } else if (normalized === "cancelled") {
      item.classList.add("is-resolved", "is-cancelled");
    }
    var pill = item.querySelector(".request-pill");
    if (pill) {
      pill.textContent = statusText;
    }
  };

  var applyFilters = function () {
    var query = (searchInput?.value || "").toLowerCase().trim();
    var type = (filterSelect?.value || "").toLowerCase().trim();
    var hasTabs = !!panelTabs;
    var pendientesCount = 0;
    var historialCount = 0;

    applySorting();

    list.forEach(function (item) {
      var data = readData(item);
      updateStatusClasses(item, data.status, data.statusCode);
      var dateText = (data.startDay + " " + data.startMonth + " - " + data.endDay + " " + data.endMonth).toLowerCase();
      var searchable = [
        data.user,
        data.type,
        data.status,
        data.equipo,
        data.grupo,
        dateText
      ].join(" ").toLowerCase();
      var matchesQuery = !query || searchable.includes(query);
      var matchesType = matchesSelectedType(data.type, type);
      var statusCode = normalizeStatusCode(data.statusCode, data.status);
      var isHistorial = statusCode === "approved" || statusCode === "rejected" || statusCode === "cancelled";
      if (matchesQuery && matchesType) {
        if (isHistorial) {
          historialCount += 1;
        } else {
          pendientesCount += 1;
        }
      }
      var matchesTab = !hasTabs
        || (activeTab === "historial" ? isHistorial : !isHistorial);
      if (matchesQuery && matchesType && matchesTab) {
        item.classList.remove("is-hidden");
      } else {
        item.classList.add("is-hidden");
      }
    });

    updateTabCounts(pendientesCount, historialCount);
    updateCount();

    var active = document.querySelector(".request-card.is-active");
    if (active && !active.classList.contains("is-hidden")) {
      return;
    }

    var firstVisible = list.find(function (item) {
      return !item.classList.contains("is-hidden");
    });
    if (firstVisible) {
      setActive(firstVisible);
      return;
    }

    list.forEach(function (item) {
      item.classList.remove("is-active");
    });

    if (activeTab === "historial") {
      showDetailEmptyState(
        "Sin solicitudes en historial",
        "No hay solicitudes finalizadas para mostrar con los filtros actuales."
      );
      return;
    }

    showDetailEmptyState(
      "Sin solicitudes pendientes",
      "No hay solicitudes pendientes esperando revision."
    );
  };

  list.forEach(function (item) {
    item.addEventListener("click", function () {
      setActive(item);
    });
    item.addEventListener("keydown", function (event) {
      if (event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        setActive(item);
      }
    });
  });

  if (searchInput) {
    searchInput.addEventListener("input", applyFilters);
  }

  if (filterSelect) {
    filterSelect.addEventListener("change", applyFilters);
  }

  if (sortSelect) {
    sortSelect.addEventListener("change", applyFilters);
  }

  tabButtons.forEach(function (button) {
    button.addEventListener("click", function () {
      tabButtons.forEach(function (btn) {
        btn.classList.remove("is-active");
      });
      button.classList.add("is-active");
      activeTab = button.dataset.tab || "pendientes";
      applyFilters();
    });
  });

  var handleDecision = async function (action) {
    var active = document.querySelector(".request-card.is-active");
    if (!active) return;
    var solicitudId = active.dataset.requestId || "";
    if (!solicitudId) return;
    var currentData = readData(active);
    var decisionState = getDecisionState(currentData);
    var canProceed = action === "approve"
      ? decisionState.canApprove
      : action === "reject"
        ? decisionState.canReject
        : decisionState.canCancel;
    if (!canProceed) {
      updateApprovalUi(currentData);
      return;
    }

    var payload = { solicitudId: solicitudId };
    try {
      var endpoint = action === "approve"
        ? "/Solicitudes/Approve"
        : action === "reject"
          ? "/Solicitudes/Reject"
          : "/Solicitudes/Cancel";
      var response = await fetch(endpoint, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          RequestVerificationToken: tokenInput?.value || ""
        },
        body: JSON.stringify(payload)
      });

      if (!response.ok) {
        var errorData = await response.json().catch(function () { return {}; });
        showToast(errorData.message || "No se pudo actualizar la solicitud.", "danger");
        return;
      }

      var data = await response.json().catch(function () { return {}; });
      if (data && data.status) {
        active.dataset.status = data.status;
        active.dataset.statusCode = normalizeStatusCode(data.statusCode, data.status);
        active.dataset.aprobador1 = data.aprobador1 || "";
        active.dataset.aprobador1Fecha = data.aprobador1Fecha || "";
        active.dataset.aprobador2 = data.aprobador2 || "";
        active.dataset.aprobador2Fecha = data.aprobador2Fecha || "";
        updateStatusClasses(active, data.status, active.dataset.statusCode);
        renderDetail(active);
        applyFilters();
        showToast(
          action === "approve"
            ? "Solicitud aprobada."
            : action === "reject"
              ? "Solicitud rechazada."
              : "Solicitud cancelada.",
          "success");
      }
    } catch {
      showToast("No se pudo actualizar la solicitud.", "danger");
    }
  };

  approveBtns.forEach(function (button) {
    button.addEventListener("click", function () {
      handleDecision("approve");
    });
  });

  rejectBtns.forEach(function (button) {
    button.addEventListener("click", function () {
      handleDecision("reject");
    });
  });

  cancelBtns.forEach(function (button) {
    button.addEventListener("click", function () {
      handleDecision("cancel");
    });
  });

  if (searchInput) {
    searchInput.value = "";
  }
  if (filterSelect) {
    filterSelect.value = "";
  }
  applyFilters();

  var initialActive = document.querySelector(".request-card.is-active");
  if (initialActive) {
    renderDetail(initialActive);
  }
});


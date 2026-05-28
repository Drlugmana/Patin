(() => {
  const api = window.WeeklyCalendar;
  if (!api) return;

  const renderApi = api.render || {};
  const dom = api.dom || {};
  const state = api.state || {};

  const refreshFiltersUi = async ({ refreshVistaAmplia = false } = {}) => {
    renderApi.renderLegend?.();
    renderApi.renderGrid?.();
    renderApi.renderAssignments?.();
    renderApi.updateEmptyState?.();
    if (refreshVistaAmplia && state.vistaAmpliaActive && typeof renderApi.renderVistaAmplia === "function") {
      await renderApi.renderVistaAmplia();
    }
  };

  const bindFallbackFilterToggles = () => {
    const updateMineBtn = () => {
      const button = dom.filterMineToggle;
      if (!button) return;
      const active = state.filterMyShifts === true;
      const tooltip = active ? "Mostrar todos los turnos" : "Mostrar solo mis turnos";
      button.classList.toggle("is-active", active);
      button.setAttribute("aria-pressed", active ? "true" : "false");
      button.setAttribute("aria-label", tooltip);
      button.setAttribute("data-bs-title", tooltip);
    };

    const updateAbsentBtn = () => {
      const button = dom.filterAbsentToggle;
      if (!button) return;
      const active = state.showAbsentPeople === true;
      const tooltip = active
        ? "Ocultar personas ausentes por calamidad"
        : "Mostrar personas ausentes por calamidad";
      button.classList.toggle("is-active", active);
      button.setAttribute("aria-pressed", active ? "true" : "false");
      button.setAttribute("aria-label", tooltip);
      button.setAttribute("data-bs-title", tooltip);
    };

    if (dom.filterMineToggle && dom.filterMineToggle.dataset.fallbackBound !== "true") {
      dom.filterMineToggle.dataset.fallbackBound = "true";
      state.filterMyShifts = state.filterMyShifts === true;
      updateMineBtn();
      dom.filterMineToggle.addEventListener("click", async () => {
        state.filterMyShifts = !state.filterMyShifts;
        updateMineBtn();
        await refreshFiltersUi();
      });
    }

    if (dom.filterAbsentToggle && dom.filterAbsentToggle.dataset.fallbackBound !== "true") {
      dom.filterAbsentToggle.dataset.fallbackBound = "true";
      state.showAbsentPeople = state.showAbsentPeople === true;
      updateAbsentBtn();
      dom.filterAbsentToggle.addEventListener("click", async () => {
        state.showAbsentPeople = !state.showAbsentPeople;
        updateAbsentBtn();
        await refreshFiltersUi({ refreshVistaAmplia: true });
      });
    }
  };

  const bindFallbackWeekNav = () => {
    const normalizeOffset = () => {
      const parsed = Number(state.weekOffset);
      state.weekOffset = Number.isFinite(parsed) ? parsed : 0;
    };

    document.querySelectorAll("[data-week-nav]").forEach((button) => {
      if (button.dataset.fallbackWeekNavBound === "true") return;
      button.dataset.fallbackWeekNavBound = "true";

      button.addEventListener("click", async () => {
        const direction = button.getAttribute("data-week-nav");
        if (direction !== "next" && direction !== "prev") return;
        if (typeof api.data?.loadWeekData !== "function") return;

        normalizeOffset();
        state.weekOffset += direction === "next" ? 1 : -1;

        try {
          await api.data.loadWeekData();
        } catch (error) {
          console.error("WeeklyCalendar fallback week navigation failed", error);
        }
      });
    });
  };

  const fallbackLoad = () => {
    bindFallbackFilterToggles();
    bindFallbackWeekNav();
    try {
      api.interactions?.bindCardContextMenu?.();
    } catch (error) {
      console.error("WeeklyCalendar fallback context menu bind failed", error);
    }
    if (typeof api.data?.loadWeekData === "function") {
      api.data.loadWeekData().catch((error) => {
        console.error("WeeklyCalendar fallback load failed", error);
      });
    }
  };

  const boot = () => {
    if (typeof api.interactions?.init !== "function") {
      fallbackLoad();
      return;
    }

    try {
      api.interactions.bindCardContextMenu?.();
      api.interactions.init();
    } catch (error) {
      // Keep page usable and try loading week data even if interactions fail.
      console.error("WeeklyCalendar init failed", error);
      fallbackLoad();
    }
  };

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", boot, { once: true });
  } else {
    boot();
  }
})();

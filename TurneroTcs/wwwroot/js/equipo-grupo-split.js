(() => {
  const equipoRows = Array.from(document.querySelectorAll(".equipo-row"));
  const grupoRows = Array.from(document.querySelectorAll(".grupo-row, .grupo-edit-row"));
  const emptyRow = document.querySelector("[data-grupo-empty]");
  const createRow = document.querySelector("[data-grupo-create]");
  const createTrigger = document.querySelector("#grupo-create-trigger");
  const createCancel = document.querySelector(".grupo-create-cancel");
  const createNombre = createRow?.querySelector('[data-create-field="NombreGrupo"]');
  const createEquipo = createRow?.querySelector('[data-create-field="EquipoId"]');
  const selectedLabel = document.querySelector("[data-equipo-selected]");

  const wireCreateToggle = () => {
    if (!createTrigger || !createRow) return;
    createTrigger.addEventListener("click", () => {
      createRow.classList.add("is-open");
      if (createNombre) {
        createNombre.focus();
      }
    });
    if (createCancel) {
      createCancel.addEventListener("click", () => {
        createRow.classList.remove("is-open");
      });
    }
  };

  wireCreateToggle();

  if (equipoRows.length === 0) {
    if (selectedLabel) {
      selectedLabel.textContent = "Sin equipos";
    }
    return;
  }

  const setSelectedEquipo = (row) => {
    equipoRows.forEach((item) => item.classList.remove("is-selected"));
    row.classList.add("is-selected");
    const equipoId = row.getAttribute("data-equipo-id");
    const equipoNombre = row.getAttribute("data-nombre");
    if (selectedLabel) {
      selectedLabel.textContent = equipoNombre || "Equipo";
    }

    let visibleCount = 0;
    grupoRows.forEach((grupoRow) => {
      const match = grupoRow.getAttribute("data-equipo-id") === equipoId;
      grupoRow.style.display = match ? "" : "none";
      if (match && grupoRow.classList.contains("grupo-row")) {
        visibleCount += 1;
      }
    });

    if (emptyRow) {
      emptyRow.style.display = visibleCount === 0 ? "" : "none";
    }

    if (createRow) {
      createRow.setAttribute("data-selected-equipo-id", equipoId || "");
    }
    if (createEquipo && equipoId) {
      createEquipo.value = equipoId;
    }
  };

  setSelectedEquipo(equipoRows[0]);

  document.addEventListener("click", (event) => {
    const target = event.target instanceof Element ? event.target : null;
    if (!target) return;
    const row = target.closest(".equipo-row");
    if (!row) return;
    if (target.closest(".equipo-inline-edit") || target.closest(".equipo-delete-btn")) {
      return;
    }
    setSelectedEquipo(row);
  });

})();

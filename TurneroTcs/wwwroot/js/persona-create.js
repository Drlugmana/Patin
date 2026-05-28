document.addEventListener("DOMContentLoaded", function () {
  var equipoSelect = document.querySelector("[data-equipo-select]");
  var grupoSelect = document.querySelector("[data-grupo-select]");
  var grupoToggle = document.querySelector("[data-grupo-toggle]");
  var grupoMenu = document.querySelector("[data-grupo-menu]");
  var grupoOptions = document.querySelector("[data-grupo-options]");
  var grupoWrapper = document.querySelector("[data-grupo-wrapper]");

  if (!equipoSelect || !grupoSelect || !grupoOptions || !grupoToggle || !grupoMenu || !grupoWrapper) {
    return;
  }

  var setDisabled = function (disabled) {
    grupoToggle.disabled = disabled;
    grupoSelect.disabled = disabled;
    if (disabled) {
      grupoMenu.classList.remove("is-open");
    }
  };

  var clearGrupos = function () {
    grupoSelect.innerHTML = "";
    grupoOptions.innerHTML = "";
    grupoToggle.textContent = "Selecciona grupos";
    setDisabled(true);
  };

  var updateToggleText = function () {
    var selected = Array.from(grupoSelect.selectedOptions);
    if (selected.length === 0) {
      grupoToggle.textContent = "Selecciona grupos";
      return;
    }
    if (selected.length === 1) {
      grupoToggle.textContent = selected[0].textContent || "1 grupo seleccionado";
      return;
    }
    grupoToggle.textContent = selected.length + " grupos seleccionados";
  };

  var renderGrupos = function (grupos) {
    clearGrupos();
    grupos.forEach(function (grupo) {
      var option = document.createElement("option");
      option.value = grupo.value;
      option.textContent = grupo.text;
      grupoSelect.appendChild(option);

      var item = document.createElement("label");
      item.className = "multi-select-item";
      var checkbox = document.createElement("input");
      checkbox.type = "checkbox";
      checkbox.value = grupo.value;
      checkbox.addEventListener("change", function () {
        option.selected = checkbox.checked;
        updateToggleText();
      });
      var labelText = document.createElement("span");
      labelText.textContent = grupo.text;
      item.appendChild(checkbox);
      item.appendChild(labelText);
      grupoOptions.appendChild(item);
    });
    updateToggleText();
    setDisabled(grupos.length === 0);
  };

  var loadGrupos = function (equipoId) {
    if (!equipoId) {
      clearGrupos();
      return;
    }

    fetch("/Persona/GruposPorEquipo?equipoId=" + encodeURIComponent(equipoId), {
      headers: { "Accept": "application/json" }
    })
      .then(function (response) {
        if (!response.ok) {
          throw new Error("Error al cargar grupos");
        }
        return response.json();
      })
      .then(function (data) {
        renderGrupos(data || []);
      })
      .catch(function () {
        clearGrupos();
      });
  };

  equipoSelect.addEventListener("change", function (event) {
    loadGrupos(event.target.value);
  });

  grupoToggle.addEventListener("click", function () {
    if (grupoToggle.disabled) {
      return;
    }
    grupoMenu.classList.toggle("is-open");
  });

  document.addEventListener("click", function (event) {
    if (!grupoWrapper.contains(event.target)) {
      grupoMenu.classList.remove("is-open");
    }
  });

  if (equipoSelect.value) {
    loadGrupos(equipoSelect.value);
  } else {
    clearGrupos();
  }
});

(function () {
    'use strict';

    const grupoSelect = document.getElementById('grupoSelect');
    const btnGuardar = document.getElementById('btnGuardarPlanificacion');
    const matrizContainer = document.getElementById('planificacionMatrizContainer');
    const coberturaGrupoSection = document.getElementById('planificacionCoberturaGrupoSection');
    const blueprintContainer = document.getElementById('planificacionBlueprintContainer');
    const emptyState = document.getElementById('planificacionEmptyState');
    const blueprintEmptyState = document.getElementById('planificacionBlueprintEmptyState');
    const discardModalEl = document.getElementById('planificacionDiscardModal');
    const discardConfirmBtn = discardModalEl ? discardModalEl.querySelector('[data-discard-confirm]') : null;
    const discardMessage = discardModalEl ? discardModalEl.querySelector('[data-discard-message]') : null;
    const discardModal = discardModalEl ? new bootstrap.Modal(discardModalEl) : null;
    const resumenGrupo = document.querySelector('[data-planificacion-grupo]');
    const resumenPersonas = document.querySelector('[data-planificacion-personas]');
    const resumenPersonasDetalle = document.querySelector('[data-planificacion-personas-detalle]');
    const resumenTotal = document.querySelector('[data-planificacion-total]');
    const resumenTotalDetalle = document.querySelector('[data-planificacion-total-detalle]');
    const resumenBalance = document.querySelector('[data-planificacion-balance]');
    const resumenBalanceDetalle = document.querySelector('[data-planificacion-balance-detalle]');
    const resumenNoches = document.querySelector('[data-planificacion-noches]');
    const resumenFds = document.querySelector('[data-planificacion-fds]');
    const controls = document.querySelector('.planificacion-controls');
    const controlsStats = document.querySelector('.planificacion-controls-stats');
    const contextBadge = document.getElementById('planificacionContextBadge');
    const saveState = document.getElementById('planificacionSaveState');
    const modeInput = document.getElementById('planificacionModeInput');
    const modeTabButtons = document.querySelectorAll('[data-mode-tab]');
    const scopeTabButtons = document.querySelectorAll('[data-scope-tab]');
    const canBlueprint = controls ? controls.dataset.canBlueprint === 'true' : false;
    const feriadoContainer = document.getElementById('planificacionFeriadoContainer');
    const feriadoTurnoConfig = document.querySelector('[data-feriado-turno-config]');
    const feriadoGrupoLabel = document.querySelector('[data-feriado-grupo-label]');
    const apoyoGrupoSection = document.getElementById('planificacionApoyoGrupoSection');
    const apoyoInputs = document.querySelectorAll('.planificacion-apoyo-input');
    const opcionalVacacionSection = document.getElementById('planificacionOpcionalVacacionSection');
    const opcionalVacacionInputs = document.querySelectorAll('.planificacion-opcional-vacacion-input');
    const rowDetailToggleButtons = document.querySelectorAll('[data-row-detail-turno-id]');
    const auxiliarSection = document.getElementById('planificacionAuxiliarSection');
    const auxiliarRows = document.querySelectorAll('[data-aux-turno-row]');
    const diasSemana = ['Lunes', 'Martes', 'Miercoles', 'Jueves', 'Viernes', 'Sabado', 'Domingo'];
    const reemplazosContainer = document.getElementById('planificacionReemplazosContainer');
    const reemplazanteSelect = document.getElementById('reemplazanteSelect');
    const reemplazaTurnoSelect = document.getElementById('reemplazaTurnoSelect');
    const reemplazaDiaSelect = document.getElementById('reemplazaDiaSelect');
    const btnAgregarReemplazo = document.getElementById('btnAgregarReemplazo');
    const reemplazosLista = document.getElementById('reemplazosLista');
    const btnGuardarReemplazos = document.getElementById('btnGuardarReemplazos');
    const reemplazaSaveState = document.getElementById('reemplazaSaveState');
    const reemplazosCount = document.getElementById('reemplazosCount');
    const usaSecundariosWrap = document.getElementById('planificacionUsaSecundariosWrap');
    const chkUsaGruposSecundarios = document.getElementById('chkUsaGruposSecundarios');
    const secundariosConfigWrap = document.getElementById('planificacionSecundariosConfig');
    const chkUsaSoloSecundarios = document.getElementById('chkUsaSoloSecundarios');
    const secundariosExtra = document.getElementById('planificacionSecundariosExtra');
    const grupoFuenteSecundariosSelect = document.getElementById('grupoFuenteSecundariosSelect');
    const personaUnicaWrap = document.getElementById('planificacionPersonaUnicaWrap');
    const chkUsarPersonaUnicaSemana = document.getElementById('chkUsarPersonaUnicaSemana');
    const maxNocturnosMesInput = document.getElementById('maxNocturnosMesInput');
    const maxNocturnosSemanaInput = document.getElementById('maxNocturnosSemanaInput');
    const nocturnosConsecutivosInput = document.getElementById('nocturnosConsecutivosInput');
    const maxFinesSemanaMesInput = document.getElementById('maxFinesSemanaMesInput');
    let grupoActual = null;
    let planificacionData = {};
    let minimosData = {};
    let apoyosData = {};
    let opcionalesVacacionData = {};
    let auxiliaresData = {};
    let configSecundariosData = {
        usaSoloSecundarios: false,
        grupoFuenteSecundariosId: '',
        usarPersonaUnicaPorSemana: false
    };
    let metricasPlanificacion = {
        personasActivas: 0,
        personasEquipoActivas: 0,
        personasPrincipalesGrupo: 0,
        personasSecundariasGrupo: 0
    };
    let modoActual = 'planificacion';
    let scopeActual = 'normal';
    let snapshotContextKey = '';
    let snapshotValue = '';
    let hasUnsavedChanges = false;
    let isSaving = false;
    let justSaved = false;
    let reemplazosData = [];
    let reemplazosOriginalJson = '[]';
    let reemplazosIsSaving = false;

    const normalizarEtiquetas = (raw) => {
        if (!raw) return [];
        return raw
            .split(',')
            .map(item => item.trim())
            .filter(item => item.length > 0);
    };

    const renderChips = (wrapper, etiquetas) => {
        const list = wrapper.querySelector('.planificacion-chip-list');
        const hiddenInput = wrapper.querySelector('.planificacion-blueprint-input');
        if (!list || !hiddenInput) return;

        list.innerHTML = '';
        etiquetas.forEach(etiqueta => {
            const chip = document.createElement('span');
            chip.className = 'planificacion-chip';
            chip.textContent = etiqueta;

            const removeBtn = document.createElement('button');
            removeBtn.type = 'button';
            removeBtn.setAttribute('aria-label', `Quitar ${etiqueta}`);
            removeBtn.textContent = '×';
            chip.appendChild(removeBtn);

            list.appendChild(chip);
        });

        hiddenInput.value = etiquetas.join(', ');
    };

    const syncChipsFromInput = (input) => {
        const wrapper = input.closest('.planificacion-chip-input');
        if (!wrapper) return;
        const etiquetas = normalizarEtiquetas(input.value);
        renderChips(wrapper, etiquetas);
    };

    const addEtiqueta = (wrapper, etiqueta) => {
        const hiddenInput = wrapper.querySelector('.planificacion-blueprint-input');
        if (!hiddenInput) return;

        const existentes = normalizarEtiquetas(hiddenInput.value);
        const normalized = etiqueta.trim();
        if (!normalized) return;

        const exists = existentes.some(item => item.toLowerCase() === normalized.toLowerCase());
        if (!exists) {
            existentes.push(normalized);
            renderChips(wrapper, existentes);
            actualizarResumen();
            refreshDirtyState();
        }
    };

    function getAntiForgeryToken() {
        const tokenInput = document.querySelector('#planificacion-token input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    function mostrarToast(mensaje, tipo = 'info') {
        window.AppToast?.show(mensaje, tipo === 'error' ? 'danger' : tipo, { title: 'Planificacion' });
    }

    function sanitizeCoverageValue(value) {
        const parsed = parseInt(value, 10);
        if (Number.isNaN(parsed) || parsed < 0) return 0;
        return Math.min(parsed, 99);
    }

    function sanitizeMinimoValue(value, cobertura) {
        const minimo = sanitizeCoverageValue(value);
        const coberturaNormalizada = sanitizeCoverageValue(cobertura);
        if (coberturaNormalizada <= 0) {
            return 0;
        }

        return Math.min(minimo, coberturaNormalizada);
    }

    function sanitizeMaxNocturnosMesValue(value) {
        const parsed = parseInt(value, 10);
        if (Number.isNaN(parsed)) return 15;
        return Math.min(20, Math.max(7, parsed));
    }

    function sanitizeMaxNocturnosSemanaValue(value) {
        const parsed = parseInt(value, 10);
        if (Number.isNaN(parsed)) return 2;
        return Math.min(7, Math.max(1, parsed));
    }

    function sanitizeMaxFinesSemanaMesValue(value) {
        const parsed = parseInt(value, 10);
        if (Number.isNaN(parsed)) return 10;
        return Math.min(12, Math.max(1, parsed));
    }

    function normalizeAuxiliarConfig(config) {
        if (!config) {
            return {
                desdeDia: '',
                hastaDia: '',
                maxPorDia: 0,
                grupoIds: []
            };
        }

        return {
            desdeDia: config.desdeDia || '',
            hastaDia: config.hastaDia || '',
            maxPorDia: sanitizeCoverageValue(config.maxPorDia),
            grupoIds: Array.isArray(config.grupoIds)
                ? Array.from(new Set(config.grupoIds
                    .filter(Boolean)
                    .map((id) => `${id}`.trim())
                    .filter(Boolean)))
                    .sort((a, b) => a.localeCompare(b))
                : []
        };
    }

    function isAuxiliarConfigActive(config) {
        return !!config
            && !!config.desdeDia
            && !!config.hastaDia
            && sanitizeCoverageValue(config.maxPorDia) > 0
            && Array.isArray(config.grupoIds)
            && config.grupoIds.length > 0;
    }

    function getAuxiliarConfig(turnoId) {
        return normalizeAuxiliarConfig(auxiliaresData[turnoId]);
    }

    function setAuxiliarConfig(turnoId, config) {
        auxiliaresData[turnoId] = normalizeAuxiliarConfig(config);
    }

    function clearAuxiliarConfig(turnoId) {
        auxiliaresData[turnoId] = {
            desdeDia: '',
            hastaDia: '',
            maxPorDia: 0,
            grupoIds: []
        };
    }

    function collectAuxiliares() {
        return Object.entries(auxiliaresData)
            .map(([tipoTurnoId, config]) => ({
                tipoTurnoId,
                desdeDia: config.desdeDia || '',
                hastaDia: config.hastaDia || '',
                maxPorDia: sanitizeCoverageValue(config.maxPorDia),
                grupoIds: Array.isArray(config.grupoIds) ? [...config.grupoIds].sort((a, b) => a.localeCompare(b)) : []
            }))
            .filter(isAuxiliarConfigActive)
            .sort((a, b) => a.tipoTurnoId.localeCompare(b.tipoTurnoId));
    }

    function setApoyoConfig(dia, turnoId, cantidadApoyo) {
        apoyosData[`${dia}_${turnoId}`] = sanitizeCoverageValue(cantidadApoyo);
    }

    function getApoyoConfig(dia, turnoId) {
        return sanitizeCoverageValue(apoyosData[`${dia}_${turnoId}`] || 0);
    }

    function setPlanificacionConfig(dia, turnoId, numeroPersonas) {
        if (!dia || !turnoId) return;
        planificacionData[`${dia}_${turnoId}`] = sanitizeCoverageValue(numeroPersonas);
    }

    function setMinimoConfig(dia, turnoId, numeroPersonasMinimo) {
        if (!dia || !turnoId) return;
        const key = `${dia}_${turnoId}`;
        const cobertura = sanitizeCoverageValue(planificacionData[key]);
        minimosData[key] = sanitizeMinimoValue(numeroPersonasMinimo, cobertura);
    }

    function collectApoyos() {
        return Object.entries(apoyosData)
            .map(([key, cantidadApoyo]) => {
                const [dia, tipoTurnoId] = key.split('_');
                return {
                    dia: dia || '',
                    tipoTurnoId: tipoTurnoId || '',
                    cantidadApoyo: sanitizeCoverageValue(cantidadApoyo)
                };
            })
            .filter(item => item.dia && item.tipoTurnoId && item.cantidadApoyo > 0)
            .sort((a, b) => {
                const byDia = a.dia.localeCompare(b.dia);
                return byDia !== 0 ? byDia : a.tipoTurnoId.localeCompare(b.tipoTurnoId);
            });
    }

    function setOpcionalVacacionConfig(dia, turnoId, enabled) {
        const key = `${dia}_${turnoId}`;
        if (enabled) {
            opcionalesVacacionData[key] = true;
            return;
        }

        delete opcionalesVacacionData[key];
    }

    function getOpcionalVacacionConfig(dia, turnoId) {
        return !!opcionalesVacacionData[`${dia}_${turnoId}`];
    }

    function setFlexibleToggleState(toggle, enabled) {
        if (!toggle) return;
        const isEnabled = !!enabled;
        toggle.setAttribute('aria-pressed', isEnabled ? 'true' : 'false');
        toggle.classList.toggle('is-on', isEnabled);
    }

    function syncAuxiliarGroupChecks(row, turnoId) {
        if (!row || !turnoId) return;
        const gruposSelect = row.querySelector(`[data-aux-grupos-select-turno-id="${turnoId}"]`);
        if (!gruposSelect) return;

        const selected = new Set(Array.from(gruposSelect.selectedOptions).map((option) => option.value));
        row.querySelectorAll(`[data-aux-grupo-check-turno-id="${turnoId}"]`).forEach((input) => {
            input.checked = selected.has(input.value);
        });
    }

    function updateAuxiliarGroupSummary(row, turnoId) {
        if (!row || !turnoId) return;
        const summary = row.querySelector(`[data-aux-grupo-summary-turno-id="${turnoId}"]`);
        if (!summary) return;

        const checked = Array.from(row.querySelectorAll(`[data-aux-grupo-check-turno-id="${turnoId}"]`))
            .filter((input) => input.checked)
            .map((input) => input.nextElementSibling?.textContent?.trim())
            .filter((value) => value);

        if (checked.length === 0) {
            summary.textContent = 'Seleccionar grupos';
            return;
        }

        if (checked.length <= 2) {
            summary.textContent = checked.join(', ');
            return;
        }

        summary.textContent = `${checked.length} grupos`;
    }

    function positionAuxiliarGroupMenu(turnoId, menu) {
        if (!turnoId || !menu) return;
        const toggle = document.querySelector(`[data-aux-grupo-toggle-turno-id="${turnoId}"]`);
        if (!toggle) return;

        const rect = toggle.getBoundingClientRect();
        const viewportPadding = 8;
        const desiredWidth = rect.width;
        const maxWidth = Math.min(360, window.innerWidth - viewportPadding * 2);
        const width = Math.max(220, Math.min(desiredWidth, maxWidth));
        const left = Math.min(
            Math.max(viewportPadding, rect.left),
            Math.max(viewportPadding, window.innerWidth - width - viewportPadding)
        );
        const maxHeight = Math.min(260, Math.max(140, window.innerHeight - rect.bottom - 16));

        menu.style.position = 'fixed';
        menu.style.top = `${rect.bottom + 6}px`;
        menu.style.left = `${left}px`;
        menu.style.right = 'auto';
        menu.style.width = `${width}px`;
        menu.style.maxHeight = `${maxHeight}px`;
        menu.style.zIndex = '1080';
    }

    function closeAuxiliarGroupMenu(menu) {
        if (!menu) return;
        const turnoId = menu.getAttribute('data-aux-grupo-menu-turno-id') || menu.getAttribute('data-aux-grupo-portal-turno-id');
        menu.classList.remove('is-open');

        if (menu.parentElement === document.body && turnoId) {
            const wrapper = document.querySelector(`[data-aux-grupo-multi-turno-id="${turnoId}"]`);
            if (wrapper) {
                wrapper.appendChild(menu);
            }
        }

        menu.removeAttribute('data-aux-grupo-portal-turno-id');
        menu.style.position = '';
        menu.style.top = '';
        menu.style.left = '';
        menu.style.right = '';
        menu.style.width = '';
        menu.style.maxHeight = '';
        menu.style.zIndex = '';
    }

    function closeAllAuxiliarGroupMenus() {
        document.querySelectorAll('[data-aux-grupo-menu-turno-id].is-open').forEach((openMenu) => {
            closeAuxiliarGroupMenu(openMenu);
        });
    }

    function openAuxiliarGroupMenu(turnoId, menu) {
        if (!turnoId || !menu) return;
        closeAllAuxiliarGroupMenus();

        if (menu.parentElement !== document.body) {
            menu.setAttribute('data-aux-grupo-portal-turno-id', turnoId);
            document.body.appendChild(menu);
        }

        menu.classList.add('is-open');
        positionAuxiliarGroupMenu(turnoId, menu);
    }

    function repositionOpenAuxiliarGroupMenus() {
        document.querySelectorAll('[data-aux-grupo-menu-turno-id].is-open').forEach((openMenu) => {
            const turnoId = openMenu.getAttribute('data-aux-grupo-menu-turno-id') || openMenu.getAttribute('data-aux-grupo-portal-turno-id');
            if (!turnoId) {
                closeAuxiliarGroupMenu(openMenu);
                return;
            }
            positionAuxiliarGroupMenu(turnoId, openMenu);
        });
    }

    function getFlexibleToggleState(toggle) {
        if (!toggle) return false;
        return toggle.getAttribute('aria-pressed') === 'true';
    }

    function collectOpcionalesVacacion() {
        return Object.keys(opcionalesVacacionData)
            .map((key) => {
                const [dia, tipoTurnoId] = key.split('_');
                return {
                    dia: dia || '',
                    tipoTurnoId: tipoTurnoId || ''
                };
            })
            .filter((item) => {
                if (!item.dia || !item.tipoTurnoId) {
                    return false;
                }

                const coberturaInput = matrizContainer
                    ? matrizContainer.querySelector(`.planificacion-input[data-dia="${item.dia}"][data-turno-id="${item.tipoTurnoId}"]`)
                    : null;
                const cobertura = sanitizeCoverageValue(coberturaInput ? coberturaInput.value : 0);
                return cobertura > 0 && !isAuxiliarAvailableForCurrentGroup(getAuxiliarConfig(item.tipoTurnoId));
            })
            .sort((a, b) => {
                const byDia = a.dia.localeCompare(b.dia);
                return byDia !== 0 ? byDia : a.tipoTurnoId.localeCompare(b.tipoTurnoId);
            });
    }

    function isAuxiliarAvailableForCurrentGroup(config) {
        return !!grupoActual
            && !!config
            && Array.isArray(config.grupoIds)
            && config.grupoIds.includes(grupoActual);
    }

    function getEquipoId() {
        return grupoSelect ? (grupoSelect.getAttribute('data-equipo-id') || '') : '';
    }

    function canSaveCurrentContext() {
        if (scopeActual === 'feriado' || modoActual === 'blueprint') {
            return !!grupoActual;
        }

        return !!getEquipoId();
    }

    function getContextKey() {
        return `${modoActual}|${scopeActual}|${grupoActual || `equipo:${getEquipoId()}`}`;
    }

    function serializePlanificacionInputs() {
        if (!matrizContainer) return '';
        const inputs = matrizContainer.querySelectorAll('.planificacion-input[data-dia][data-turno-id]');
        return Array.from(inputs)
            .map((input) => {
                const dia = input.getAttribute('data-dia') || '';
                const turnoId = input.getAttribute('data-turno-id') || '';
                const value = sanitizeCoverageValue(input.value);
                const minInput = matrizContainer.querySelector(`.planificacion-minimo-input[data-min-dia="${dia}"][data-min-turno-id="${turnoId}"]`);
                const minimo = sanitizeMinimoValue(minInput ? minInput.value : value, value);
                return `${dia}|${turnoId}|${value}|${minimo}`;
            })
            .join(';');
    }

    function serializeBlueprintInputs() {
        if (!blueprintContainer) return '';
        const inputs = blueprintContainer.querySelectorAll('.planificacion-blueprint-input');
        const usaSecundarios = chkUsaGruposSecundarios ? chkUsaGruposSecundarios.checked : false;
        const rows = Array.from(inputs)
            .map((input) => {
                const dia = input.getAttribute('data-dia') || '';
                const turnoId = input.getAttribute('data-turno-id') || '';
                const etiquetas = normalizarEtiquetas(input.value).join(',');
                const minInput = blueprintContainer.querySelector(
                    `.planificacion-blueprint-min[data-dia="${dia}"][data-turno-id="${turnoId}"]`);
                const min = minInput ? (minInput.value || '0') : '0';
                return `${dia}|${turnoId}|${etiquetas}|${min}`;
            })
            .join(';');
        return `${rows}||usa_secundarios:${usaSecundarios}`;
    }

    function serializeFeriadoInputs() {
        if (!feriadoTurnoConfig) return '';
        const inputs = feriadoTurnoConfig.querySelectorAll('[data-feriado-turno-id]');
        return Array.from(inputs)
            .map((input) => {
                const turnoId = input.getAttribute('data-feriado-turno-id') || '';
                const value = sanitizeCoverageValue(input.value);
                return `${turnoId}|${value}`;
            })
            .join(';');
    }

    function serializeAuxiliarInputs() {
        return collectAuxiliares()
            .map((item) => `${item.tipoTurnoId}|${item.desdeDia}|${item.hastaDia}|${item.maxPorDia}|${item.grupoIds.join(',')}`)
            .join(';');
    }

    function serializeApoyoInputs() {
        return collectApoyos()
            .map((item) => `${item.dia}|${item.tipoTurnoId}|${item.cantidadApoyo}`)
            .join(';');
    }

    function serializeOpcionalesVacacionInputs() {
        return collectOpcionalesVacacion()
            .map((item) => `${item.dia}|${item.tipoTurnoId}`)
            .join(';');
    }

    function serializeSecundariosConfig() {
        return [
            configSecundariosData.usaSoloSecundarios ? '1' : '0',
            configSecundariosData.grupoFuenteSecundariosId || '',
            configSecundariosData.usarPersonaUnicaPorSemana ? '1' : '0'
        ].join('|');
    }

    function serializeReglasEquipo() {
        return [
            sanitizeMaxNocturnosMesValue(maxNocturnosMesInput ? maxNocturnosMesInput.value : 15),
            sanitizeMaxNocturnosSemanaValue(maxNocturnosSemanaInput ? maxNocturnosSemanaInput.value : 2),
            nocturnosConsecutivosInput ? (nocturnosConsecutivosInput.checked ? '1' : '0') : '0',
            sanitizeMaxFinesSemanaMesValue(maxFinesSemanaMesInput ? maxFinesSemanaMesInput.value : 10)
        ].join('|');
    }

    function buildCurrentSnapshot() {
        const reglasEquipo = serializeReglasEquipo();
        if (scopeActual === 'feriado') {
            return `${serializeFeriadoInputs()}||reglas:${reglasEquipo}`;
        }
        if (modoActual === 'blueprint') {
            return `${serializeBlueprintInputs()}||reglas:${reglasEquipo}`;
        }
        return `${serializePlanificacionInputs()}||${serializeApoyoInputs()}||${serializeOpcionalesVacacionInputs()}||${serializeAuxiliarInputs()}||${serializeSecundariosConfig()}||reglas:${reglasEquipo}`;
    }

    function updateContextBadge() {
        if (!contextBadge) return;
        if (!grupoActual) {
            if (scopeActual === 'normal' && modoActual === 'planificacion' && getEquipoId()) {
                contextBadge.textContent = 'Editando: turnos auxiliares compartidos del equipo';
                return;
            }
            contextBadge.textContent = 'Selecciona un grupo para comenzar.';
            return;
        }

        const grupoNombre = obtenerNombreGrupoSeleccionado();
        if (scopeActual === 'feriado') {
            contextBadge.textContent = `Editando: ${grupoNombre} · Cobertura feriado`;
            return;
        }

        const modoLabel = modoActual === 'blueprint' ? 'Patron semanal' : 'Cobertura por turno';
        contextBadge.textContent = `Editando: ${grupoNombre} · ${modoLabel} · Semana normal`;
    }

    function updateSaveStateUi() {
        if (!btnGuardar) return;
        const canSave = canSaveCurrentContext();
        btnGuardar.disabled = isSaving || !canSave || !hasUnsavedChanges;

        if (!saveState) return;

        saveState.classList.remove('is-dirty', 'is-saving', 'is-saved');
        if (!canSave) {
            saveState.textContent = 'Selecciona un grupo';
            return;
        }
        if (isSaving) {
            saveState.classList.add('is-saving');
            saveState.textContent = 'Guardando...';
            return;
        }
        if (hasUnsavedChanges) {
            saveState.classList.add('is-dirty');
            saveState.textContent = 'Cambios sin guardar';
            return;
        }
        if (justSaved) {
            saveState.classList.add('is-saved');
            saveState.textContent = 'Guardado';
            return;
        }
        saveState.textContent = 'Sin cambios';
    }

    function clearDirtyState() {
        snapshotContextKey = '';
        snapshotValue = '';
        hasUnsavedChanges = false;
        justSaved = false;
        updateSaveStateUi();
    }

    function markCurrentAsSaved(showSavedState = false) {
        snapshotContextKey = getContextKey();
        snapshotValue = buildCurrentSnapshot();
        hasUnsavedChanges = false;
        justSaved = showSavedState;
        updateSaveStateUi();
    }

    function refreshDirtyState() {
        const canSave = canSaveCurrentContext();
        if (!canSave) {
            hasUnsavedChanges = false;
            justSaved = false;
            updateSaveStateUi();
            return;
        }

        if (snapshotContextKey !== getContextKey()) {
            hasUnsavedChanges = false;
            justSaved = false;
            updateSaveStateUi();
            return;
        }

        const currentSnapshot = buildCurrentSnapshot();
        hasUnsavedChanges = currentSnapshot !== snapshotValue;
        if (hasUnsavedChanges) {
            justSaved = false;
        }
        updateSaveStateUi();
    }

    function confirmDiscardChanges() {
        if (!hasUnsavedChanges) {
            return Promise.resolve(true);
        }

        const message = 'Hay cambios sin guardar. Si continuas, se perderan.';
        if (!discardModalEl || !discardModal || !discardConfirmBtn) {
            return Promise.resolve(window.confirm(`${message} ¿Deseas continuar?`));
        }

        if (discardMessage) {
            discardMessage.textContent = message;
        }

        return new Promise((resolve) => {
            let settled = false;

            const cleanup = () => {
                discardConfirmBtn.removeEventListener('click', handleConfirm);
                discardModalEl.removeEventListener('hidden.bs.modal', handleHidden);
            };

            const settle = (confirmed) => {
                if (settled) {
                    return;
                }
                settled = true;
                cleanup();
                resolve(confirmed);
            };

            const handleConfirm = () => {
                settle(true);
                discardModal.hide();
            };

            const handleHidden = () => {
                settle(false);
            };

            discardConfirmBtn.addEventListener('click', handleConfirm);
            discardModalEl.addEventListener('hidden.bs.modal', handleHidden);
            discardModal.show();
        });
    }

    function updateModeTabsUi() {
        modeTabButtons.forEach((btn) => {
            const value = btn.getAttribute('data-mode-tab');
            const isActive = value === modoActual;
            btn.classList.toggle('is-active', isActive);
            btn.setAttribute('aria-selected', isActive ? 'true' : 'false');
        });
        if (modeInput) {
            modeInput.value = modoActual;
        }
        if (usaSecundariosWrap) {
            usaSecundariosWrap.style.display = (modoActual === 'blueprint' && canBlueprint) ? '' : 'none';
        }
        updateSecundariosConfigUi();
    }

    function syncSecundariosStateFromInputs() {
        configSecundariosData.usaSoloSecundarios = !!chkUsaSoloSecundarios?.checked;
        configSecundariosData.grupoFuenteSecundariosId = configSecundariosData.usaSoloSecundarios
            ? (grupoFuenteSecundariosSelect?.value || '')
            : '';
        configSecundariosData.usarPersonaUnicaPorSemana = configSecundariosData.usaSoloSecundarios
            && !!chkUsarPersonaUnicaSemana?.checked;
    }

    function updateSecundariosSourceOptions() {
        if (!grupoFuenteSecundariosSelect) return;
        Array.from(grupoFuenteSecundariosSelect.options).forEach((option) => {
            if (!option.value) {
                option.hidden = false;
                option.disabled = false;
                return;
            }
            const sameGroup = option.value === grupoActual;
            option.hidden = sameGroup;
            option.disabled = sameGroup;
        });

        if (grupoFuenteSecundariosSelect.value === grupoActual) {
            grupoFuenteSecundariosSelect.value = '';
            configSecundariosData.grupoFuenteSecundariosId = '';
        }
    }

    function setSelectValueForAppSelect(select, value) {
        if (!select) return;
        select.value = value || '';
        Array.from(select.options).forEach((option) => {
            const selected = option.value === select.value;
            option.selected = selected;
            option.toggleAttribute('selected', selected);
        });
    }

    function updateSecundariosConfigUi() {
        const visible = !!grupoActual && modoActual === 'planificacion' && scopeActual === 'normal';
        if (secundariosConfigWrap) {
            secundariosConfigWrap.hidden = !visible;
        }

        updateSecundariosSourceOptions();

        if (chkUsaSoloSecundarios) {
            chkUsaSoloSecundarios.checked = visible && configSecundariosData.usaSoloSecundarios;
        }

        if (secundariosExtra) {
            secundariosExtra.hidden = !visible || !configSecundariosData.usaSoloSecundarios;
        }

        if (grupoFuenteSecundariosSelect) {
            setSelectValueForAppSelect(grupoFuenteSecundariosSelect, configSecundariosData.grupoFuenteSecundariosId || '');
        }

        const mostrarPersonaUnica = visible && configSecundariosData.usaSoloSecundarios;
        if (personaUnicaWrap) {
            personaUnicaWrap.hidden = !mostrarPersonaUnica;
        }
        if (!mostrarPersonaUnica) {
            configSecundariosData.usarPersonaUnicaPorSemana = false;
        }
        if (chkUsarPersonaUnicaSemana) {
            chkUsarPersonaUnicaSemana.checked = mostrarPersonaUnica && configSecundariosData.usarPersonaUnicaPorSemana;
        }
    }

    function updateScopeTabsUi() {
        scopeTabButtons.forEach((btn) => {
            const scopeValue = btn.getAttribute('data-scope-tab') || 'normal';
            const isFeriadoTab = scopeValue === 'feriado';
            const isActive = scopeValue === scopeActual;
            btn.classList.toggle('is-active', isActive);
            btn.setAttribute('aria-selected', isActive ? 'true' : 'false');
            btn.disabled = isFeriadoTab && !grupoActual;
        });
    }

    function hasDetailDataForTurno(turnoId) {
        const hasApoyo = Array.from(apoyoInputs).some((input) => {
            const inputTurnoId = input.getAttribute('data-apoyo-turno-id');
            return inputTurnoId === turnoId && sanitizeCoverageValue(input.value) > 0;
        });

        if (hasApoyo) {
            return true;
        }

        return Array.from(opcionalVacacionInputs).some((input) => {
            const inputTurnoId = input.getAttribute('data-opcional-vacacion-turno-id');
            return inputTurnoId === turnoId && getFlexibleToggleState(input);
        });
    }

    function setRowDetailVisibility(turnoId, visible) {
        if (!matrizContainer || !turnoId) {
            return;
        }

        const row = matrizContainer.querySelector(`tr[data-turno-id="${turnoId}"]`);
        if (!row) {
            return;
        }

        row.classList.toggle('planificacion-row-detail-hidden', !visible);

        const toggleButton = row.querySelector(`[data-row-detail-turno-id="${turnoId}"]`);
        if (toggleButton) {
            toggleButton.setAttribute('aria-pressed', visible ? 'true' : 'false');
            toggleButton.textContent = visible ? 'Ocultar' : 'Opciones';
            toggleButton.title = visible ? 'Ocultar' : 'Mostrar opciones';
            const turnoNombre = row.querySelector('.planificacion-turno-nombre')?.textContent?.trim() || 'turno';
            toggleButton.setAttribute('aria-label', `${visible ? 'Ocultar' : 'Mostrar'} opciones del ${turnoNombre}`);
        }
    }

    function applyDefaultRowDetailVisibility() {
        if (!matrizContainer) {
            return;
        }

        const rows = matrizContainer.querySelectorAll('tr[data-turno-id]');
        rows.forEach((row) => {
            const turnoId = row.getAttribute('data-turno-id');
            if (!turnoId) {
                return;
            }

            setRowDetailVisibility(turnoId, hasDetailDataForTurno(turnoId));
        });
    }

    function resetFeriadoCoverageInputs() {
        if (feriadoTurnoConfig) {
            const inputs = feriadoTurnoConfig.querySelectorAll('[data-feriado-turno-id]');
            inputs.forEach((input) => {
                input.value = '0';
            });
        }
    }

    function updateFeriadoGroupLabel() {
        if (!feriadoGrupoLabel) return;
        feriadoGrupoLabel.textContent = obtenerNombreGrupoSeleccionado();
    }

    function obtenerNombreGrupoSeleccionado() {
        if (!grupoSelect) return '-';
        const selected = grupoSelect.selectedOptions[0];
        if (selected && selected.value) {
            return selected.textContent.trim();
        }
        return '-';
    }

    function obtenerNombreGrupoActual() {
        if (!grupoSelect) return 'Sin grupo';
        const selected = grupoSelect.selectedOptions[0];
        if (selected && selected.value) {
            return selected.textContent.trim();
        }
        if (scopeActual === 'normal' && modoActual === 'planificacion' && getEquipoId()) {
            return 'Equipo (solo auxiliares)';
        }
        return 'Sin grupo';
    }

    function setResumenText(element, value) {
        if (element) {
            element.textContent = value;
        }
    }

    function sanitizeMetricCount(value) {
        const parsed = parseInt(value, 10);
        return Number.isNaN(parsed) || parsed < 0 ? 0 : parsed;
    }

    function esFinSemanaDia(dia) {
        return dia === 'Sabado' || dia === 'Domingo';
    }

    function expandirRangoDiasCliente(desdeDia, hastaDia) {
        const desde = diasSemana.findIndex(dia => dia.toLowerCase() === (desdeDia || '').toLowerCase());
        const hasta = diasSemana.findIndex(dia => dia.toLowerCase() === (hastaDia || '').toLowerCase());
        if (desde < 0 || hasta < 0) {
            return [];
        }

        const result = [];
        let index = desde;
        while (true) {
            result.push(diasSemana[index]);
            if (index === hasta) {
                break;
            }

            index = (index + 1) % diasSemana.length;
            if (index === desde) {
                return [];
            }
        }

        return result;
    }

    function isTurnoNocturno(turnoId) {
        if (!turnoId) return false;
        const row = document.querySelector(`tr[data-turno-id="${turnoId}"][data-turno-nocturno]`);
        return row ? row.getAttribute('data-turno-nocturno') === 'true' : false;
    }

    function crearResumenCoberturaBase() {
        return {
            normal: 0,
            auxiliar: 0,
            apoyo: 0,
            flexible: 0,
            noches: 0,
            fds: 0,
            dias: new Set()
        };
    }

    function acumularSlotResumen(resumen, dia, turnoId, value, bucket) {
        const cantidad = sanitizeCoverageValue(value);
        if (cantidad <= 0) return;

        resumen[bucket] += cantidad;
        resumen.dias.add(dia);
        if (isTurnoNocturno(turnoId)) {
            resumen.noches += cantidad;
        }
        if (esFinSemanaDia(dia)) {
            resumen.fds += cantidad;
        }
    }

    function calcularResumenCoberturaNormal() {
        const resumen = crearResumenCoberturaBase();
        if (!matrizContainer) {
            return resumen;
        }

        if (grupoActual) {
            const inputs = matrizContainer.querySelectorAll('.planificacion-input[data-dia][data-turno-id]');
            inputs.forEach(input => {
                const dia = input.getAttribute('data-dia');
                const turnoId = input.getAttribute('data-turno-id');
                if (!dia || !turnoId || isAuxiliarAvailableForCurrentGroup(getAuxiliarConfig(turnoId))) {
                    return;
                }

                acumularSlotResumen(resumen, dia, turnoId, input.value, 'normal');
            });

            collectApoyos().forEach(item => {
                resumen.apoyo += sanitizeCoverageValue(item.cantidadApoyo);
            });

            collectOpcionalesVacacion().forEach(item => {
                const coberturaInput = matrizContainer.querySelector(`.planificacion-input[data-dia="${item.dia}"][data-turno-id="${item.tipoTurnoId}"]`);
                resumen.flexible += sanitizeCoverageValue(coberturaInput ? coberturaInput.value : 0);
            });
        }

        collectAuxiliares().forEach(config => {
            const aplicaAlContexto = grupoActual
                ? config.grupoIds.includes(grupoActual)
                : true;
            if (!aplicaAlContexto) {
                return;
            }

            expandirRangoDiasCliente(config.desdeDia, config.hastaDia).forEach(dia => {
                acumularSlotResumen(resumen, dia, config.tipoTurnoId, config.maxPorDia, 'auxiliar');
            });
        });

        return resumen;
    }

    function actualizarResumenPersonas() {
        const personasActivas = sanitizeMetricCount(metricasPlanificacion.personasActivas);
        setResumenText(resumenPersonas, personasActivas.toString());

        if (!resumenPersonasDetalle) {
            return;
        }

        if (grupoActual) {
            const principales = sanitizeMetricCount(metricasPlanificacion.personasPrincipalesGrupo);
            const secundarias = sanitizeMetricCount(metricasPlanificacion.personasSecundariasGrupo);
            resumenPersonasDetalle.textContent = `Prim ${principales} / Sec ${secundarias}`;
            return;
        }

        resumenPersonasDetalle.textContent = 'Equipo activo';
    }

    function actualizarResumenStatsVisibility() {
        if (!controlsStats) return;
        controlsStats.classList.toggle('is-hidden', !grupoActual);
    }

    function actualizarResumenBalance(resumen) {
        const disponibles = sanitizeMetricCount(resumen.normal + resumen.auxiliar);
        const primarias = grupoActual
            ? sanitizeMetricCount(metricasPlanificacion.personasPrincipalesGrupo)
            : 0;
        const esGrupoEspecial = !!grupoActual && configSecundariosData.usaSoloSecundarios;
        const requeridos = esGrupoEspecial ? disponibles : primarias * 5;
        const diferencia = disponibles - requeridos;
        const card = resumenBalance ? resumenBalance.closest('.planificacion-controls-stat') : null;

        setResumenText(resumenBalance, `${disponibles} / ${requeridos}`);

        if (card) {
            card.classList.remove('is-good', 'is-warn', 'is-empty');
        }

        if (!grupoActual) {
            setResumenText(resumenBalanceDetalle, 'Selecciona grupo');
            card?.classList.add('is-empty');
            return;
        }

        if (esGrupoEspecial) {
            setResumenText(resumenBalanceDetalle, 'Grupo especial con fuente secundaria');
            card?.classList.add('is-good');
            return;
        }

        if (primarias <= 0) {
            setResumenText(resumenBalanceDetalle, 'Sin personas primarias');
            card?.classList.add('is-empty');
            return;
        }

        if (diferencia >= 0) {
            setResumenText(resumenBalanceDetalle, diferencia === 0
                ? 'Balance exacto'
                : `Bien: sobran ${diferencia} cupos`);
            card?.classList.add('is-good');
            return;
        }

        setResumenText(resumenBalanceDetalle, `Faltan ${Math.abs(diferencia)} cupos`);
        card?.classList.add('is-warn');
    }

    function actualizarResumen() {
        actualizarResumenStatsVisibility();

        if (resumenGrupo) {
            resumenGrupo.textContent = obtenerNombreGrupoActual();
        }
        actualizarResumenPersonas();

        if (modoActual === 'blueprint') {
            // Si no hay grupo en modo blueprint, mostrar 0
            if (!grupoActual) {
                setResumenText(resumenTotal, '0');
                setResumenText(resumenTotalDetalle, 'Plantilla sin grupo');
                actualizarResumenBalance(crearResumenCoberturaBase());
                setResumenText(resumenNoches, '0');
                setResumenText(resumenFds, '0');
                return;
            }
            const inputs = blueprintContainer ? blueprintContainer.querySelectorAll('.planificacion-blueprint-input') : [];
            let total = 0;
            let noches = 0;
            let fds = 0;

            inputs.forEach(input => {
                const raw = (input.value || '').trim();
                if (!raw) {
                    return;
                }
                const etiquetas = raw.split(',')
                    .map(x => x.trim())
                    .filter(x => x.length > 0);
                if (etiquetas.length > 0) {
                    total += etiquetas.length;
                    const dia = input.getAttribute('data-dia');
                    const turnoId = input.getAttribute('data-turno-id');
                    if (turnoId && isTurnoNocturno(turnoId)) {
                        noches += etiquetas.length;
                    }
                    if (dia && esFinSemanaDia(dia)) {
                        fds += etiquetas.length;
                    }
                }
            });

            setResumenText(resumenTotal, total.toString());
            setResumenText(resumenTotalDetalle, 'Etiquetas de patron');
            actualizarResumenBalance({
                ...crearResumenCoberturaBase(),
                normal: total
            });
            setResumenText(resumenNoches, noches.toString());
            setResumenText(resumenFds, fds.toString());
            return;
        }

        const resumen = calcularResumenCoberturaNormal();
        const total = resumen.normal + resumen.auxiliar;
        setResumenText(resumenTotal, total.toString());
        setResumenText(resumenTotalDetalle, `Normal ${resumen.normal} / Aux ${resumen.auxiliar} / Apoyo ${resumen.apoyo} / Flex ${resumen.flexible}`);
        actualizarResumenBalance(resumen);
        setResumenText(resumenNoches, resumen.noches.toString());
        setResumenText(resumenFds, resumen.fds.toString());
    }

    function syncAuxiliarStateToView() {
        auxiliarRows.forEach((row) => {
            const turnoId = row.getAttribute('data-turno-id');
            if (!turnoId) {
                return;
            }

            const config = getAuxiliarConfig(turnoId);
            const active = isAuxiliarConfigActive(config);
            const appliesToCurrentGroup = active && isAuxiliarAvailableForCurrentGroup(config);
            const hasPartialConfig = !active && (
                !!config.desdeDia
                || !!config.hastaDia
                || sanitizeCoverageValue(config.maxPorDia) > 0
                || (Array.isArray(config.grupoIds) && config.grupoIds.length > 0));
            const desdeSelect = row.querySelector(`[data-aux-desde-turno-id="${turnoId}"]`);
            const hastaSelect = row.querySelector(`[data-aux-hasta-turno-id="${turnoId}"]`);
            const maxInput = row.querySelector(`[data-aux-max-turno-id="${turnoId}"]`);
            const status = row.querySelector(`[data-aux-status-turno-id="${turnoId}"]`);
            const gruposSelect = row.querySelector(`[data-aux-grupos-select-turno-id="${turnoId}"]`);
            const matrixRow = matrizContainer
                ? matrizContainer.querySelector(`tr[data-turno-id="${turnoId}"]`)
                : null;

            if (desdeSelect) {
                desdeSelect.value = config.desdeDia || '';
            }
            if (hastaSelect) {
                hastaSelect.value = config.hastaDia || '';
            }
            if (maxInput) {
                maxInput.value = sanitizeCoverageValue(config.maxPorDia).toString();
            }
            if (gruposSelect) {
                Array.from(gruposSelect.options).forEach((option) => {
                    option.selected = Array.isArray(config.grupoIds) && config.grupoIds.includes(option.value);
                });
                syncAuxiliarGroupChecks(row, turnoId);
                updateAuxiliarGroupSummary(row, turnoId);
            }

            row.classList.toggle('is-auxiliar-active', active);
            if (status) {
                status.textContent = active
                    ? `${config.grupoIds.length} grupos · ${config.desdeDia} a ${config.hastaDia} · max ${sanitizeCoverageValue(config.maxPorDia)} / dia`
                    : hasPartialConfig
                        ? 'Configuracion incompleta'
                        : 'Sin configurar';
                status.classList.toggle('is-active', active);
            }

            if (matrixRow) {
                matrixRow.classList.toggle('planificacion-row-auxiliar', appliesToCurrentGroup);
                const inputs = matrixRow.querySelectorAll('.planificacion-input');
                inputs.forEach((input) => {
                    input.disabled = appliesToCurrentGroup;
                    input.closest('.planificacion-td-cell')?.classList.toggle('is-auxiliar', appliesToCurrentGroup);
                });
            }
        });

        syncOpcionalVacacionStateToView();
    }

    function syncOpcionalVacacionStateToView() {
        opcionalVacacionInputs.forEach((input) => {
            const dia = input.getAttribute('data-opcional-vacacion-dia');
            const turnoId = input.getAttribute('data-opcional-vacacion-turno-id');
            if (!dia || !turnoId) {
                return;
            }

            const coberturaInput = matrizContainer
                ? matrizContainer.querySelector(`.planificacion-input[data-dia="${dia}"][data-turno-id="${turnoId}"]`)
                : null;
            const cobertura = sanitizeCoverageValue(coberturaInput ? coberturaInput.value : 0);
            const isAuxiliar = isAuxiliarAvailableForCurrentGroup(getAuxiliarConfig(turnoId));
            const enabled = !!grupoActual && cobertura > 0 && !isAuxiliar;

            if (!enabled) {
                setFlexibleToggleState(input, false);
                setOpcionalVacacionConfig(dia, turnoId, false);
            } else {
                setFlexibleToggleState(input, getOpcionalVacacionConfig(dia, turnoId));
            }

            input.disabled = !enabled;
            input.classList.toggle('is-disabled', !enabled);
            input.closest('.planificacion-td-cell')?.classList.toggle('is-auxiliar', isAuxiliar);
        });
    }

    function setModo(modo) {
        modoActual = modo === 'blueprint' && canBlueprint ? 'blueprint' : 'planificacion';
        updateModeTabsUi();
        updateScopeTabsUi();
        updateContextBadge();
        clearDirtyState();

        if (scopeActual === 'feriado') {
            if (!grupoActual) {
                scopeActual = 'normal';
                updateScopeTabsUi();
                updateContextBadge();
                ocultarMatriz();
                return;
            }
            mostrarFeriado();
            cargarFeriadoCobertura();
            return;
        }

        cargarVistaNormal();
    }

    function setScope(scope) {
        const targetScope = scope === 'feriado' ? 'feriado' : 'normal';
        if (targetScope === 'feriado' && !grupoActual) {
            mostrarToast('Selecciona un grupo para configurar feriados.', 'error');
            scopeActual = 'normal';
            updateScopeTabsUi();
            updateContextBadge();
            updateSaveStateUi();
            return;
        }

        scopeActual = targetScope;
        updateScopeTabsUi();
        updateContextBadge();
        clearDirtyState();
        if (scopeActual === 'feriado') {
            mostrarFeriado();
            cargarFeriadoCobertura();
            return;
        }

        cargarVistaNormal();
    }

    function cargarVistaNormal() {
        if (modoActual === 'blueprint') {
            mostrarBlueprint();
            cargarBlueprint(grupoActual);
            return;
        }
        cargarPlanificacion(grupoActual);
    }

    function mostrarPlanificacion() {
        emptyState.style.display = grupoActual ? 'none' : 'block';
        if (feriadoContainer) {
            feriadoContainer.style.display = 'none';
        }
        matrizContainer.style.display = 'block';
        if (auxiliarSection) {
            auxiliarSection.style.display = 'block';
        }
        if (coberturaGrupoSection) {
            coberturaGrupoSection.style.display = grupoActual ? 'block' : 'none';
        }
        if (blueprintContainer) {
            blueprintContainer.style.display = 'none';
        }
        if (apoyoGrupoSection) {
            apoyoGrupoSection.style.display = grupoActual ? 'block' : 'none';
        }
        if (opcionalVacacionSection) {
            opcionalVacacionSection.style.display = grupoActual ? 'block' : 'none';
        }
        applyDefaultRowDetailVisibility();
        syncAuxiliarStateToView();
        updateSecundariosConfigUi();
        actualizarResumen();
        updateContextBadge();
        updateSaveStateUi();
    }

    function mostrarBlueprint() {
        emptyState.style.display = 'none';
        if (feriadoContainer) {
            feriadoContainer.style.display = 'none';
        }
        if (matrizContainer) {
            matrizContainer.style.display = 'none';
        }
        if (auxiliarSection) {
            auxiliarSection.style.display = 'none';
        }
        if (apoyoGrupoSection) {
            apoyoGrupoSection.style.display = 'none';
        }
        if (opcionalVacacionSection) {
            opcionalVacacionSection.style.display = 'none';
        }
        if (blueprintContainer) {
              blueprintContainer.style.display = grupoActual ? 'block' : 'none';
        }
        if (blueprintEmptyState) {
            blueprintEmptyState.style.display = grupoActual ? 'none' : 'block';
        }
        updateSecundariosConfigUi();
        actualizarResumen();
        updateContextBadge();
        updateSaveStateUi();
    }

    function mostrarFeriado() {
        emptyState.style.display = 'none';
        if (matrizContainer) {
            matrizContainer.style.display = 'none';
        }
        if (auxiliarSection) {
            auxiliarSection.style.display = 'none';
        }
        if (apoyoGrupoSection) {
            apoyoGrupoSection.style.display = 'none';
        }
        if (opcionalVacacionSection) {
            opcionalVacacionSection.style.display = 'none';
        }
        updateSecundariosConfigUi();
        if (blueprintContainer) {
            blueprintContainer.style.display = 'none';
        }
        if (feriadoContainer) {
            feriadoContainer.style.display = 'block';
        }
        updateFeriadoGroupLabel();
        updateContextBadge();
        updateSaveStateUi();
    }

    async function cargarPlanificacion(grupoId) {
        try {
            const equipoId = getEquipoId();
            const query = new URLSearchParams();
            if (grupoId) {
                query.set('grupoId', grupoId);
            }
            if (equipoId) {
                query.set('equipoId', equipoId);
            }

            const response = await fetch(`/Planificacion/GetPlanificacion?${query.toString()}`);
            const result = await response.json();

            if (result.success) {
                planificacionData = {};
                minimosData = {};
                apoyosData = {};
                opcionalesVacacionData = {};
                auxiliaresData = {};
                metricasPlanificacion = {
                    personasActivas: result.metricas?.personasActivas ?? 0,
                    personasEquipoActivas: result.metricas?.personasEquipoActivas ?? 0,
                    personasPrincipalesGrupo: result.metricas?.personasPrincipalesGrupo ?? 0,
                    personasSecundariasGrupo: result.metricas?.personasSecundariasGrupo ?? 0
                };
                result.data.forEach(item => {
                    const key = `${item.dia}_${item.tipoTurnoId}`;
                    planificacionData[key] = item.numeroPersonas;
                    minimosData[key] = sanitizeMinimoValue(item.numeroPersonasMinimo, item.numeroPersonas);
                });
                (Array.isArray(result.apoyos) ? result.apoyos : []).forEach((item) => {
                    if (!item || !item.dia || !item.tipoTurnoId) {
                        return;
                    }

                    setApoyoConfig(item.dia, item.tipoTurnoId, item.cantidadApoyo || 0);
                });
                (Array.isArray(result.auxiliares) ? result.auxiliares : []).forEach((item) => {
                    if (!item || !item.tipoTurnoId) {
                        return;
                    }
                    setAuxiliarConfig(item.tipoTurnoId, {
                        desdeDia: item.desdeDia || '',
                        hastaDia: item.hastaDia || '',
                        maxPorDia: item.maxPorDia || 0,
                        grupoIds: Array.isArray(item.grupoIds) ? item.grupoIds : []
                    });
                });
                (Array.isArray(result.opcionalesVacacion) ? result.opcionalesVacacion : []).forEach((item) => {
                    if (!item || !item.dia || !item.tipoTurnoId) {
                        return;
                    }

                    setOpcionalVacacionConfig(item.dia, item.tipoTurnoId, true);
                });
                if (maxNocturnosMesInput) {
                    maxNocturnosMesInput.value = `${sanitizeMaxNocturnosMesValue(result.maximoTurnosNocturnosPorMes)}`;
                }
                if (maxNocturnosSemanaInput) {
                    maxNocturnosSemanaInput.value = `${sanitizeMaxNocturnosSemanaValue(result.maximoTurnosNocturnosPorSemana)}`;
                }
                if (nocturnosConsecutivosInput) {
                    nocturnosConsecutivosInput.checked = !!result.nocturnosConsecutivos;
                }
                if (maxFinesSemanaMesInput) {
                    maxFinesSemanaMesInput.value = `${sanitizeMaxFinesSemanaMesValue(result.maximoSlotsFinSemanaPorMes)}`;
                }
                configSecundariosData = {
                    usaSoloSecundarios: !!result.configSecundarios?.usaSoloSecundarios,
                    grupoFuenteSecundariosId: result.configSecundarios?.grupoFuenteSecundariosId || '',
                    usarPersonaUnicaPorSemana: !!result.configSecundarios?.usarPersonaUnicaPorSemana
                };

                const inputs = matrizContainer.querySelectorAll('.planificacion-input[data-dia][data-turno-id]');
                inputs.forEach(input => {
                    const dia = input.getAttribute('data-dia');
                    const turnoId = input.getAttribute('data-turno-id');
                    const key = `${dia}_${turnoId}`;
                    input.value = planificacionData[key] || 0;
                });

                const minInputs = matrizContainer.querySelectorAll('.planificacion-minimo-input[data-min-dia][data-min-turno-id]');
                minInputs.forEach(input => {
                    const dia = input.getAttribute('data-min-dia');
                    const turnoId = input.getAttribute('data-min-turno-id');
                    const key = `${dia}_${turnoId}`;
                    const cobertura = sanitizeCoverageValue(planificacionData[key] || 0);
                    const minimo = sanitizeMinimoValue(minimosData[key], cobertura);
                    minimosData[key] = minimo;
                    input.value = `${minimo}`;
                });

                apoyoInputs.forEach((input) => {
                    const dia = input.getAttribute('data-apoyo-dia');
                    const turnoId = input.getAttribute('data-apoyo-turno-id');
                    input.value = `${getApoyoConfig(dia, turnoId)}`;
                });
                syncOpcionalVacacionStateToView();

                mostrarPlanificacion();
                updateSecundariosConfigUi();
                actualizarResumen();
                markCurrentAsSaved();
            } else {
                mostrarToast(result.error || 'Error al cargar planificacion', 'error');
            }
        } catch (error) {
            console.error('Error al cargar planificacion:', error);
            mostrarToast('Error al cargar planificacion', 'error');
        }
    }

    async function cargarBlueprint(grupoId) {
        if (!blueprintContainer) return;
        const equipoId = grupoSelect ? grupoSelect.getAttribute('data-equipo-id') : '';
        if (!equipoId) {
            mostrarToast('Equipo no especificado', 'error');
            return;
        }

        if (!grupoId) {
            if (chkUsaGruposSecundarios) chkUsaGruposSecundarios.checked = false;
        }

        try {
            const url = grupoId
                ? `/Planificacion/GetBlueprint?equipoId=${equipoId}&grupoId=${grupoId}`
                : `/Planificacion/GetBlueprint?equipoId=${equipoId}`;
            const response = await fetch(url);
            const result = await response.json();

            if (result.success) {
                const blueprintData = {};
                const minData = {};
                result.data.forEach(item => {
                    const key = `${item.dia}_${item.tipoTurnoId}`;
                    blueprintData[key] = item.etiquetas;
                    minData[key] = item.minPersonasTurno || 0;
                });

                const inputs = blueprintContainer.querySelectorAll('.planificacion-blueprint-input');
                inputs.forEach(input => {
                    const dia = input.getAttribute('data-dia');
                    const turnoId = input.getAttribute('data-turno-id');
                    const key = `${dia}_${turnoId}`;
                    input.value = blueprintData[key] || '';
                    syncChipsFromInput(input);
                });

                const minInputs = blueprintContainer.querySelectorAll('.planificacion-blueprint-min');
                minInputs.forEach(minInput => {
                    const dia = minInput.getAttribute('data-dia');
                    const turnoId = minInput.getAttribute('data-turno-id');
                    const key = `${dia}_${turnoId}`;
                    minInput.value = minData[key] != null ? minData[key] : 0;
                });

                if (chkUsaGruposSecundarios) {
                    chkUsaGruposSecundarios.checked = !!result.usaGruposSecundarios;
                }

                mostrarBlueprint();
                actualizarResumen();
                markCurrentAsSaved();
            } else {
                mostrarToast(result.error || 'Error al cargar blueprint', 'error');
            }
        } catch (error) {
            console.error('Error al cargar blueprint:', error);
            mostrarToast('Error al cargar blueprint', 'error');
        }
    }

    async function cargarFeriadoCobertura() {
        const equipoId = grupoSelect ? grupoSelect.getAttribute('data-equipo-id') : '';
        if (!equipoId || !grupoActual) {
            resetFeriadoCoverageInputs();
            updateFeriadoGroupLabel();
            clearDirtyState();
            return;
        }

        try {
            const query = new URLSearchParams({ equipoId: equipoId, grupoId: grupoActual });

            const response = await fetch(`/Planificacion/GetFeriadoCobertura?${query.toString()}`);
            const result = await response.json();
            if (!result.success) {
                resetFeriadoCoverageInputs();
                return;
            }

            const data = result.data || {};

            const turnos = Array.isArray(data.turnos) ? data.turnos : [];
            const turnosById = new Map(turnos
                .filter(item => item && item.tipoTurnoId)
                .map(item => [item.tipoTurnoId, sanitizeCoverageValue(item.cantidadVisible)]));

            if (feriadoTurnoConfig) {
                const inputs = feriadoTurnoConfig.querySelectorAll('[data-feriado-turno-id]');
                inputs.forEach((input) => {
                    const turnoId = input.getAttribute('data-feriado-turno-id');
                    const value = turnoId && turnosById.has(turnoId) ? turnosById.get(turnoId) : 0;
                    input.value = `${value}`;
                });
            }
            markCurrentAsSaved();
        } catch (error) {
            console.error('Error al cargar cobertura de feriados:', error);
            resetFeriadoCoverageInputs();
            clearDirtyState();
        }
    }

    async function guardarFeriadoCobertura() {
        const equipoId = grupoSelect ? grupoSelect.getAttribute('data-equipo-id') : '';
        if (!equipoId || !grupoActual) {
            mostrarToast('Selecciona un grupo para guardar cobertura de feriados.', 'error');
            return false;
        }

        let payload = {
            equipoId,
            grupoId: grupoActual,
            turnos: []
        };

        const turnos = [];
        if (feriadoTurnoConfig) {
            const inputs = feriadoTurnoConfig.querySelectorAll('[data-feriado-turno-id]');
            inputs.forEach((input) => {
                const turnoId = input.getAttribute('data-feriado-turno-id');
                if (!turnoId) return;
                turnos.push({
                    tipoTurnoId: turnoId,
                    cantidadVisible: sanitizeCoverageValue(input.value)
                });
            });
        }
        payload.turnos = turnos;

        try {
            isSaving = true;
            justSaved = false;
            updateSaveStateUi();

            const response = await fetch('/Planificacion/SaveFeriadoCobertura', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify(payload)
            });

            const result = await response.json();
            if (!result.success) {
                mostrarToast(result.error || 'Error al guardar cobertura de feriados', 'error');
                return false;
            }

            return true;
        } catch (error) {
            console.error('Error al guardar cobertura de feriados:', error);
            mostrarToast('Error al guardar cobertura de feriados', 'error');
            return false;
        } finally {
            isSaving = false;
            updateSaveStateUi();
        }
    }

    function ocultarMatriz() {
        if (modoActual === 'blueprint') {
            emptyState.style.display = 'none';
            if (blueprintEmptyState) {
                blueprintEmptyState.style.display = 'block';
            }
            if (blueprintContainer) {
                blueprintContainer.style.display = 'none';
            }
        } else {
            emptyState.style.display = 'block';
            if (blueprintEmptyState) {
                blueprintEmptyState.style.display = 'none';
            }
            matrizContainer.style.display = 'none';
            if (blueprintContainer) {
                blueprintContainer.style.display = 'none';
            }
        }
        if (apoyoGrupoSection) {
            apoyoGrupoSection.style.display = 'none';
        }
        if (opcionalVacacionSection) {
            opcionalVacacionSection.style.display = 'none';
        }
        if (auxiliarSection) {
            auxiliarSection.style.display = 'none';
        }
        if (feriadoContainer) {
            feriadoContainer.style.display = 'none';
        }
        if (reemplazosContainer) {
            reemplazosContainer.style.display = 'none';
        }
        configSecundariosData = {
            usaSoloSecundarios: false,
            grupoFuenteSecundariosId: '',
            usarPersonaUnicaPorSemana: false
        };
        updateSecundariosConfigUi();
        if (resumenGrupo) resumenGrupo.textContent = 'Sin grupo';
        if (resumenPersonas) resumenPersonas.textContent = '0';
        if (resumenPersonasDetalle) resumenPersonasDetalle.textContent = 'Sin grupo';
        if (resumenTotal) resumenTotal.textContent = '0';
        if (resumenTotalDetalle) resumenTotalDetalle.textContent = 'Normal 0 / Aux 0';
        if (resumenBalance) resumenBalance.textContent = '0 / 0';
        if (resumenBalanceDetalle) {
            resumenBalanceDetalle.textContent = 'Selecciona grupo';
            resumenBalanceDetalle.closest('.planificacion-controls-stat')?.classList.remove('is-good', 'is-warn');
            resumenBalanceDetalle.closest('.planificacion-controls-stat')?.classList.add('is-empty');
        }
        if (resumenNoches) resumenNoches.textContent = '0';
        if (resumenFds) resumenFds.textContent = '0';
        actualizarResumenStatsVisibility();
        updateContextBadge();
        updateSaveStateUi();
    }

    async function guardarPlanificacion() {
        const equipoId = getEquipoId();
        if (!equipoId) {
            mostrarToast('Equipo no especificado', 'error');
            return false;
        }

        const configuracionesIncompletas = Array.from(auxiliarRows)
            .map((row) => row.getAttribute('data-turno-id'))
            .filter((turnoId) => {
                if (!turnoId) {
                    return false;
                }
                const config = getAuxiliarConfig(turnoId);
                return !isAuxiliarConfigActive(config)
                    && (!!config.desdeDia
                        || !!config.hastaDia
                        || sanitizeCoverageValue(config.maxPorDia) > 0
                        || (Array.isArray(config.grupoIds) && config.grupoIds.length > 0));
            });

        if (configuracionesIncompletas.length > 0) {
            mostrarToast('Completa o limpia la configuracion auxiliar antes de guardar.', 'error');
            return false;
        }

        const inputs = matrizContainer.querySelectorAll('.planificacion-input[data-dia][data-turno-id]');
        const planificaciones = [];

        if (grupoActual) {
            inputs.forEach(input => {
                const dia = input.getAttribute('data-dia');
                const turnoId = input.getAttribute('data-turno-id');
                if (turnoId && isAuxiliarAvailableForCurrentGroup(getAuxiliarConfig(turnoId))) {
                    return;
                }
                const numeroPersonas = parseInt(input.value) || 0;
                const minInput = matrizContainer.querySelector(
                    `.planificacion-minimo-input[data-min-dia="${dia}"][data-min-turno-id="${turnoId}"]`);
                const numeroPersonasMinimo = sanitizeMinimoValue(minInput ? minInput.value : numeroPersonas, numeroPersonas);

                planificaciones.push({
                    dia: dia,
                    tipoTurnoId: turnoId,
                    numeroPersonas: numeroPersonas,
                    numeroPersonasMinimo: numeroPersonasMinimo
                });
            });
        }

        const apoyos = [];
        if (grupoActual) {
            const apoyoInvalido = Array.from(apoyoInputs).find((input) => {
                const dia = input.getAttribute('data-apoyo-dia');
                const turnoId = input.getAttribute('data-apoyo-turno-id');
                const apoyo = sanitizeCoverageValue(input.value);
                const coberturaKey = `${dia}_${turnoId}`;
                const cobertura = sanitizeCoverageValue(planificacionData[coberturaKey] ?? matrizContainer.querySelector(`.planificacion-input[data-dia="${dia}"][data-turno-id="${turnoId}"]`)?.value ?? 0);
                return apoyo > cobertura;
            });

            if (apoyoInvalido) {
                mostrarToast('El apoyo eventual no puede exceder la cobertura normal del mismo turno.', 'error');
                return false;
            }

            apoyos.push(...collectApoyos());
        }

        const opcionalesVacacion = grupoActual
            ? collectOpcionalesVacacion()
            : [];

        syncSecundariosStateFromInputs();
        if (configSecundariosData.usaSoloSecundarios && !configSecundariosData.grupoFuenteSecundariosId) {
            mostrarToast('Selecciona el grupo fuente para usar solo secundarios.', 'error');
            return false;
        }

        const data = {
            equipoId: equipoId,
            grupoId: grupoActual || '',
            maximoSlotsFinSemanaPorMes: sanitizeMaxFinesSemanaMesValue(maxFinesSemanaMesInput ? maxFinesSemanaMesInput.value : 10),
            maximoTurnosNocturnosPorMes: sanitizeMaxNocturnosMesValue(maxNocturnosMesInput ? maxNocturnosMesInput.value : 15),
            maximoTurnosNocturnosPorSemana: sanitizeMaxNocturnosSemanaValue(maxNocturnosSemanaInput ? maxNocturnosSemanaInput.value : 2),
            nocturnosConsecutivos: nocturnosConsecutivosInput ? nocturnosConsecutivosInput.checked : false,
            planificaciones: planificaciones,
            apoyos: apoyos,
            opcionalesVacacion: opcionalesVacacion,
            auxiliares: collectAuxiliares(),
            usaSoloSecundarios: configSecundariosData.usaSoloSecundarios,
            grupoFuenteSecundariosId: configSecundariosData.grupoFuenteSecundariosId,
            usarPersonaUnicaPorSemana: configSecundariosData.usarPersonaUnicaPorSemana
        };

        try {
            document.body.classList.add('planificacion-saving');
            isSaving = true;
            justSaved = false;
            updateSaveStateUi();

            const response = await fetch('/Planificacion/SavePlanificacion', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify(data)
            });

            const result = await response.json();

            if (result.success) {
                await cargarPlanificacion(grupoActual);
                markCurrentAsSaved(true);
                return true;
            } else {
                mostrarToast(result.error || 'Error al guardar planificacion', 'error');
                return false;
            }
        } catch (error) {
            console.error('Error al guardar planificacion:', error);
            mostrarToast('Error al guardar planificacion', 'error');
            return false;
        } finally {
            document.body.classList.remove('planificacion-saving');
            isSaving = false;
            updateSaveStateUi();
        }
    }

    async function guardarBlueprint() {
        if (!blueprintContainer) return false;
        const equipoId = grupoSelect ? grupoSelect.getAttribute('data-equipo-id') : '';
        if (!equipoId) {
            mostrarToast('Equipo no especificado', 'error');
            return false;
        }

        const inputs = blueprintContainer.querySelectorAll('.planificacion-blueprint-input');
        const entries = [];
        inputs.forEach(input => {
            const dia = input.getAttribute('data-dia');
            const turnoId = input.getAttribute('data-turno-id');
            const etiquetas = (input.value || '').trim();
            const minInput = blueprintContainer.querySelector(
                `.planificacion-blueprint-min[data-dia="${dia}"][data-turno-id="${turnoId}"]`);
            const minPersonasTurno = minInput ? (parseInt(minInput.value, 10) || 0) : 0;
            if (etiquetas.length > 0 || minPersonasTurno > 0) {
                entries.push({
                    dia: dia,
                    tipoTurnoId: turnoId,
                    etiquetas: etiquetas,
                    minPersonasTurno: minPersonasTurno
                });
            }
        });

        if (entries.length === 0) {
            mostrarToast('Agrega al menos una etiqueta o configura el m\u00ednimo en alg\u00fan turno', 'error');
            return false;
        }

        const data = {
            equipoId: equipoId,
            grupoId: grupoActual || null,
            usaGruposSecundarios: chkUsaGruposSecundarios ? chkUsaGruposSecundarios.checked : false,
            entries: entries
        };

        try {
            document.body.classList.add('planificacion-saving');
            isSaving = true;
            justSaved = false;
            updateSaveStateUi();

            const response = await fetch('/Planificacion/SaveBlueprint', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify(data)
            });

            const result = await response.json();

            if (result.success) {
                await cargarBlueprint(grupoActual);
                return true;
            } else {
                mostrarToast(result.error || 'Error al guardar blueprint', 'error');
                return false;
            }
        } catch (error) {
            console.error('Error al guardar blueprint:', error);
            mostrarToast('Error al guardar blueprint', 'error');
            return false;
        } finally {
            document.body.classList.remove('planificacion-saving');
            isSaving = false;
            updateSaveStateUi();
        }
    }

    async function guardarActual() {
        if (scopeActual === 'feriado') {
            const okCobertura = await guardarFeriadoCobertura();
            if (!okCobertura) return;
            const okReglas = await guardarReglasEquipo({ silent: true });
            if (!okReglas) return;
            markCurrentAsSaved(true);
            mostrarToast('Cambios guardados.', 'success');
            return;
        }

        if (modoActual === 'blueprint') {
            const okBlueprint = await guardarBlueprint();
            if (!okBlueprint) return;
            const okReglas = await guardarReglasEquipo({ silent: true });
            if (!okReglas) return;
            markCurrentAsSaved(true);
            mostrarToast('Cambios guardados.', 'success');
            return;
        }
        const okPlan = await guardarPlanificacion();
        if (!okPlan) return;
        mostrarToast('Cambios guardados.', 'success');
    }

    function popularGruposReemplazantes() {
        if (!reemplazanteSelect || !grupoSelect) return;
        const current = reemplazanteSelect.value;
        reemplazanteSelect.innerHTML = '<option value="">-- Grupo que cubre --</option>';
        Array.from(grupoSelect.options).forEach(option => {
            if (!option.value || option.value === grupoActual) return;
            const opt = document.createElement('option');
            opt.value = option.value;
            opt.textContent = option.text;
            reemplazanteSelect.appendChild(opt);
        });
        if (current) reemplazanteSelect.value = current;
    }

    function getNombreGrupo(grupoId) {
        if (!grupoSelect) return grupoId;
        const option = Array.from(grupoSelect.options).find(o => o.value === grupoId);
        return option ? option.text.trim() : grupoId;
    }

    function getNombreTurno(tipoTurnoId) {
        if (!reemplazaTurnoSelect) return tipoTurnoId;
        const option = Array.from(reemplazaTurnoSelect.options).find(o => o.value === tipoTurnoId);
        return option ? option.text.trim() : tipoTurnoId;
    }

    function updateReemplazaSaveState() {
        const isDirty = JSON.stringify(reemplazosData) !== reemplazosOriginalJson;
        if (reemplazaSaveState) {
            reemplazaSaveState.classList.remove('is-dirty', 'is-saving', 'is-saved');
            if (reemplazosIsSaving) {
                reemplazaSaveState.classList.add('is-saving');
                reemplazaSaveState.textContent = 'Guardando...';
            } else if (isDirty) {
                reemplazaSaveState.classList.add('is-dirty');
                reemplazaSaveState.textContent = 'Cambios sin guardar';
            } else {
                reemplazaSaveState.textContent = 'Sin cambios';
            }
        }
        if (btnGuardarReemplazos) {
            btnGuardarReemplazos.disabled = reemplazosIsSaving || !isDirty;
        }
    }

    function renderReemplazos() {
        if (!reemplazosLista) return;
        if (reemplazosCount) reemplazosCount.textContent = reemplazosData.length.toString();
        if (reemplazosData.length === 0) {
            reemplazosLista.innerHTML = '<p class="text-muted planificacion-reemplazo-empty">Sin reemplazos configurados.</p>';
            return;
        }
        reemplazosLista.innerHTML = '';
        reemplazosData.forEach((entrada, idx) => {
            const item = document.createElement('div');
            item.className = 'planificacion-reemplazo-item';
            const grupoNombre = getNombreGrupo(entrada.grupoReemplazanteId);
            const turnoNombre = getNombreTurno(entrada.tipoTurnoId);
            item.innerHTML =
                `<span class="planificacion-reemplazo-badge planificacion-reemplazo-badge--grupo">${grupoNombre}</span>` +
                `<span class="planificacion-reemplazo-sep">cubre en</span>` +
                `<span class="planificacion-reemplazo-badge planificacion-reemplazo-badge--turno">${turnoNombre}</span>` +
                `<span class="planificacion-reemplazo-sep">el</span>` +
                `<span class="planificacion-reemplazo-badge planificacion-reemplazo-badge--dia">${entrada.dia}</span>` +
                `<button type="button" class="planificacion-reemplazo-delete" data-idx="${idx}" aria-label="Eliminar reemplazo">&times;</button>`;
            reemplazosLista.appendChild(item);
        });
    }

    async function cargarGrupoReemplazo(grupoId) {
        if (!reemplazosContainer) return;
        const equipoId = grupoSelect ? grupoSelect.getAttribute('data-equipo-id') : '';
        if (!grupoId || !equipoId) {
            reemplazosData = [];
            reemplazosOriginalJson = '[]';
            reemplazosContainer.style.display = 'none';
            return;
        }
        try {
            const url = `/Planificacion/GetGrupoReemplazo?equipoId=${encodeURIComponent(equipoId)}&grupoId=${encodeURIComponent(grupoId)}`;
            const response = await fetch(url);
            const result = await response.json();
            if (result.success) {
                reemplazosData = result.data || [];
                reemplazosOriginalJson = JSON.stringify(reemplazosData);
                popularGruposReemplazantes();
                renderReemplazos();
                reemplazosContainer.style.display = 'block';
                updateReemplazaSaveState();
            } else {
                mostrarToast(result.error || 'Error al cargar reemplazos', 'error');
            }
        } catch (e) {
            console.error('Error al cargar reemplazos:', e);
        }
    }

    async function guardarGrupoReemplazo() {
        const equipoId = grupoSelect ? grupoSelect.getAttribute('data-equipo-id') : '';
        if (!equipoId || !grupoActual) {
            mostrarToast('Selecciona un grupo para guardar reemplazos.', 'error');
            return;
        }
        const payload = {
            equipoId,
            grupoId: grupoActual,
            entradas: reemplazosData.map(e => ({
                grupoReemplazanteId: e.grupoReemplazanteId,
                tipoTurnoId: e.tipoTurnoId,
                dia: e.dia
            }))
        };
        try {
            reemplazosIsSaving = true;
            updateReemplazaSaveState();
            const response = await fetch('/Planificacion/SaveGrupoReemplazo', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify(payload)
            });
            const result = await response.json();
            if (result.success) {
                reemplazosOriginalJson = JSON.stringify(reemplazosData);
                mostrarToast('Reemplazos guardados.', 'success');
                updateReemplazaSaveState();
            } else {
                mostrarToast(result.error || 'Error al guardar reemplazos', 'error');
            }
        } catch (e) {
            console.error('Error al guardar reemplazos:', e);
            mostrarToast('Error al guardar reemplazos', 'error');
        } finally {
            reemplazosIsSaving = false;
            updateReemplazaSaveState();
        }
    }

    if (grupoSelect) {
        grupoSelect.addEventListener('change', async function () {
            const grupoId = this.value;
            if ((grupoId || null) === grupoActual) {
                return;
            }
            const canDiscard = await confirmDiscardChanges();
            if (!canDiscard) {
                this.value = grupoActual || '';
                return;
            }
            grupoActual = grupoId || null;
            updateFeriadoGroupLabel();
            updateScopeTabsUi();
            updateContextBadge();
            clearDirtyState();

            cargarGrupoReemplazo(grupoActual);

            if (scopeActual === 'feriado') {
                if (!grupoActual) {
                    scopeActual = 'normal';
                    updateScopeTabsUi();
                    updateContextBadge();
                    cargarVistaNormal();
                    return;
                }
                mostrarFeriado();
                cargarFeriadoCobertura();
                return;
            }

            cargarVistaNormal();
        });
    }

    if (btnGuardar) {
        btnGuardar.addEventListener('click', guardarActual);
    }

    auxiliarRows.forEach((row) => {
        const turnoId = row.getAttribute('data-turno-id');
        if (!turnoId) {
            return;
        }

        const desdeSelect = row.querySelector(`[data-aux-desde-turno-id="${turnoId}"]`);
        const hastaSelect = row.querySelector(`[data-aux-hasta-turno-id="${turnoId}"]`);
        const maxInput = row.querySelector(`[data-aux-max-turno-id="${turnoId}"]`);
        const clearBtn = row.querySelector(`[data-aux-clear-turno-id="${turnoId}"]`);
        const gruposSelect = row.querySelector(`[data-aux-grupos-select-turno-id="${turnoId}"]`);
        const grupoToggle = row.querySelector(`[data-aux-grupo-toggle-turno-id="${turnoId}"]`);
        const grupoMenu = row.querySelector(`[data-aux-grupo-menu-turno-id="${turnoId}"]`);
        const grupoChecks = row.querySelectorAll(`[data-aux-grupo-check-turno-id="${turnoId}"]`);

        const syncFromInputs = () => {
            setAuxiliarConfig(turnoId, {
                desdeDia: desdeSelect ? desdeSelect.value : '',
                hastaDia: hastaSelect ? hastaSelect.value : '',
                maxPorDia: maxInput ? sanitizeCoverageValue(maxInput.value) : 0,
                grupoIds: gruposSelect
                    ? Array.from(gruposSelect.selectedOptions).map((option) => option.value)
                    : []
            });
            syncAuxiliarStateToView();
            updateSecundariosConfigUi();
            actualizarResumen();
            refreshDirtyState();
        };

        if (desdeSelect) {
            desdeSelect.addEventListener('change', syncFromInputs);
        }

        if (hastaSelect) {
            hastaSelect.addEventListener('change', syncFromInputs);
        }

        if (maxInput) {
            maxInput.addEventListener('input', function () {
                this.value = sanitizeCoverageValue(this.value).toString();
                syncFromInputs();
            });
        }

        if (gruposSelect) {
            gruposSelect.addEventListener('change', () => {
                syncAuxiliarGroupChecks(row, turnoId);
                updateAuxiliarGroupSummary(row, turnoId);
                syncFromInputs();
            });
        }

        if (grupoToggle && grupoMenu) {
            grupoToggle.addEventListener('click', () => {
                const isOpen = grupoMenu.classList.contains('is-open');
                if (isOpen) {
                    closeAuxiliarGroupMenu(grupoMenu);
                    return;
                }

                openAuxiliarGroupMenu(turnoId, grupoMenu);
            });
        }

        grupoChecks.forEach((check) => {
            check.addEventListener('change', () => {
                if (gruposSelect) {
                    Array.from(gruposSelect.options).forEach((option) => {
                        if (option.value === check.value) {
                            option.selected = check.checked;
                        }
                    });
                }
                updateAuxiliarGroupSummary(row, turnoId);
                syncFromInputs();
            });
        });

        syncAuxiliarGroupChecks(row, turnoId);
        updateAuxiliarGroupSummary(row, turnoId);

        if (clearBtn) {
            clearBtn.addEventListener('click', () => {
                clearAuxiliarConfig(turnoId);
                syncAuxiliarStateToView();
                updateSecundariosConfigUi();
                actualizarResumen();
                refreshDirtyState();
            });
        }
    });

    document.addEventListener('click', (event) => {
        const target = event.target instanceof Element ? event.target : null;
        if (!target) return;
        if (target.closest('.planificacion-aux-grupo-multi')) return;
        if (target.closest('[data-aux-grupo-menu-turno-id]')) return;
        if (target.closest('[data-aux-grupo-toggle-turno-id]')) return;
        closeAllAuxiliarGroupMenus();
    });

    window.addEventListener('resize', repositionOpenAuxiliarGroupMenus);
    window.addEventListener('scroll', repositionOpenAuxiliarGroupMenus, true);

    const inputs = document.querySelectorAll('.planificacion-input[data-dia][data-turno-id]');
    inputs.forEach(input => {
        input.addEventListener('input', function () {
            let value = parseInt(this.value);

            if (isNaN(value) || value < 0) {
                this.value = 0;
            } else if (value > 99) {
                this.value = 99;
            }
            setPlanificacionConfig(
                this.getAttribute('data-dia'),
                this.getAttribute('data-turno-id'),
                this.value);
            const dia = this.getAttribute('data-dia');
            const turnoId = this.getAttribute('data-turno-id');
            const minInput = matrizContainer
                ? matrizContainer.querySelector(`.planificacion-minimo-input[data-min-dia="${dia}"][data-min-turno-id="${turnoId}"]`)
                : null;
            if (minInput) {
                const minimo = sanitizeMinimoValue(minInput.value, this.value);
                minInput.value = `${minimo}`;
                setMinimoConfig(dia, turnoId, minimo);
            }
            syncOpcionalVacacionStateToView();
            updateSecundariosConfigUi();
            actualizarResumen();
            refreshDirtyState();
        });

        input.addEventListener('blur', function () {
            if (this.value === '' || isNaN(parseInt(this.value))) {
                this.value = 0;
            }
            setPlanificacionConfig(
                this.getAttribute('data-dia'),
                this.getAttribute('data-turno-id'),
                this.value);
            const dia = this.getAttribute('data-dia');
            const turnoId = this.getAttribute('data-turno-id');
            const minInput = matrizContainer
                ? matrizContainer.querySelector(`.planificacion-minimo-input[data-min-dia="${dia}"][data-min-turno-id="${turnoId}"]`)
                : null;
            if (minInput) {
                const minimo = sanitizeMinimoValue(minInput.value, this.value);
                minInput.value = `${minimo}`;
                setMinimoConfig(dia, turnoId, minimo);
            }
            syncOpcionalVacacionStateToView();
            updateSecundariosConfigUi();
            actualizarResumen();
            refreshDirtyState();
        });
    });

    const minimoInputs = document.querySelectorAll('.planificacion-minimo-input[data-min-dia][data-min-turno-id]');
    minimoInputs.forEach((input) => {
        input.addEventListener('input', function () {
            const dia = this.getAttribute('data-min-dia');
            const turnoId = this.getAttribute('data-min-turno-id');
            const coberturaInput = matrizContainer
                ? matrizContainer.querySelector(`.planificacion-input[data-dia="${dia}"][data-turno-id="${turnoId}"]`)
                : null;
            const cobertura = sanitizeCoverageValue(coberturaInput ? coberturaInput.value : 0);
            const minimo = sanitizeMinimoValue(this.value, cobertura);
            this.value = `${minimo}`;
            setMinimoConfig(dia, turnoId, minimo);
            refreshDirtyState();
        });

        input.addEventListener('blur', function () {
            const dia = this.getAttribute('data-min-dia');
            const turnoId = this.getAttribute('data-min-turno-id');
            const coberturaInput = matrizContainer
                ? matrizContainer.querySelector(`.planificacion-input[data-dia="${dia}"][data-turno-id="${turnoId}"]`)
                : null;
            const cobertura = sanitizeCoverageValue(coberturaInput ? coberturaInput.value : 0);
            const minimo = sanitizeMinimoValue(this.value, cobertura);
            this.value = `${minimo}`;
            setMinimoConfig(dia, turnoId, minimo);
            refreshDirtyState();
        });
    });

    apoyoInputs.forEach((input) => {
        input.addEventListener('input', function () {
            this.value = sanitizeCoverageValue(this.value).toString();
            const dia = this.getAttribute('data-apoyo-dia');
            const turnoId = this.getAttribute('data-apoyo-turno-id');
            setApoyoConfig(dia, turnoId, this.value);
            if (sanitizeCoverageValue(this.value) > 0 && turnoId) {
                setRowDetailVisibility(turnoId, true);
            }
            actualizarResumen();
            refreshDirtyState();
        });

        input.addEventListener('blur', function () {
            const dia = this.getAttribute('data-apoyo-dia');
            const turnoId = this.getAttribute('data-apoyo-turno-id');
            const coberturaInput = matrizContainer
                ? matrizContainer.querySelector(`.planificacion-input[data-dia="${dia}"][data-turno-id="${turnoId}"]`)
                : null;
            const cobertura = sanitizeCoverageValue(coberturaInput ? coberturaInput.value : 0);
            const apoyo = sanitizeCoverageValue(this.value);
            if (apoyo > cobertura) {
                this.value = `${cobertura}`;
            }

            setApoyoConfig(dia, turnoId, this.value);
            actualizarResumen();
            refreshDirtyState();
        });
    });

    opcionalVacacionInputs.forEach((input) => {
        input.addEventListener('click', function () {
            const dia = this.getAttribute('data-opcional-vacacion-dia');
            const turnoId = this.getAttribute('data-opcional-vacacion-turno-id');
            if (!dia || !turnoId) {
                return;
            }

            if (this.disabled) {
                setFlexibleToggleState(this, false);
                setOpcionalVacacionConfig(dia, turnoId, false);
            } else {
                const nextState = !getFlexibleToggleState(this);
                setFlexibleToggleState(this, nextState);
                setOpcionalVacacionConfig(dia, turnoId, nextState);
                if (nextState && turnoId) {
                    setRowDetailVisibility(turnoId, true);
                }
            }

            actualizarResumen();
            refreshDirtyState();
        });
    });

    const blueprintInputs = document.querySelectorAll('.planificacion-blueprint-input');
    blueprintInputs.forEach(input => {
        syncChipsFromInput(input);
        input.addEventListener('input', function () {
            actualizarResumen();
            refreshDirtyState();
        });

        input.addEventListener('blur', function () {
            actualizarResumen();
            refreshDirtyState();
        });
    });

    const reemplazoBlueprintChecks = document.querySelectorAll('.planificacion-blueprint-reemplazo');
    reemplazoBlueprintChecks.forEach(check => {
        // checkbox eliminado de la vista; este bloque queda vacío intencionalmente
    });

    const minBlueprintInputs = document.querySelectorAll('.planificacion-blueprint-min');
    minBlueprintInputs.forEach(input => {
        input.addEventListener('input', function () {
            refreshDirtyState();
        });
    });

    const chipWrappers = document.querySelectorAll('.planificacion-chip-input');
    chipWrappers.forEach(wrapper => {
        const entry = wrapper.querySelector('.planificacion-chip-entry');
        const list = wrapper.querySelector('.planificacion-chip-list');
        const hiddenInput = wrapper.querySelector('.planificacion-blueprint-input');
        const addButton = wrapper.querySelector('.planificacion-chip-add');

        if (entry) {
            const commitEntry = () => {
                const value = entry.value.trim();
                if (value.length === 0) return;
                value.split(',').forEach(item => addEtiqueta(wrapper, item));
                entry.value = '';
                refreshDirtyState();
            };

            entry.addEventListener('keydown', (event) => {
                if (event.key === 'Enter' || event.key === ',') {
                    event.preventDefault();
                    commitEntry();
                } else if (event.key === 'Backspace' && entry.value === '') {
                    if (hiddenInput) {
                        const etiquetas = normalizarEtiquetas(hiddenInput.value);
                        etiquetas.pop();
                        renderChips(wrapper, etiquetas);
                        actualizarResumen();
                        refreshDirtyState();
                    }
                }
            });

            entry.addEventListener('blur', commitEntry);
            entry.addEventListener('paste', (event) => {
                const text = event.clipboardData?.getData('text') || '';
                if (text.includes(',')) {
                    event.preventDefault();
                    text.split(',').forEach(item => addEtiqueta(wrapper, item));
                    entry.value = '';
                    refreshDirtyState();
                }
            });
        }

        if (addButton) {
            addButton.addEventListener('click', () => {
                if (!entry) return;
                const value = entry.value.trim();
                if (!value) return;
                value.split(',').forEach(item => addEtiqueta(wrapper, item));
                entry.value = '';
                refreshDirtyState();
            });
        }

        if (list) {
            list.addEventListener('click', (event) => {
                const target = event.target;
                if (target instanceof HTMLButtonElement) {
                    const etiqueta = target.parentElement?.firstChild?.textContent || '';
                    if (hiddenInput) {
                        const etiquetas = normalizarEtiquetas(hiddenInput.value)
                            .filter(item => item.toLowerCase() !== etiqueta.toLowerCase());
                        renderChips(wrapper, etiquetas);
                        actualizarResumen();
                        refreshDirtyState();
                    }
                }
            });
        }
    });

    modeTabButtons.forEach((btn) => {
        btn.addEventListener('click', async () => {
            const value = btn.getAttribute('data-mode-tab') || 'planificacion';
            if (value === modoActual) {
                return;
            }
            const canDiscard = await confirmDiscardChanges();
            if (!canDiscard) {
                return;
            }
            setModo(value);
        });
    });

    if (chkUsaGruposSecundarios) {
        chkUsaGruposSecundarios.addEventListener('change', function () {
            refreshDirtyState();
        });
    }

    if (chkUsaSoloSecundarios) {
        chkUsaSoloSecundarios.addEventListener('change', () => {
            syncSecundariosStateFromInputs();
            updateSecundariosConfigUi();
            refreshDirtyState();
        });
    }

    if (grupoFuenteSecundariosSelect) {
        grupoFuenteSecundariosSelect.addEventListener('change', () => {
            syncSecundariosStateFromInputs();
            updateSecundariosConfigUi();
            refreshDirtyState();
        });
    }

    if (chkUsarPersonaUnicaSemana) {
        chkUsarPersonaUnicaSemana.addEventListener('change', () => {
            syncSecundariosStateFromInputs();
            updateSecundariosConfigUi();
            refreshDirtyState();
        });
    }

    scopeTabButtons.forEach((btn) => {
        btn.addEventListener('click', async () => {
            const scopeValue = btn.getAttribute('data-scope-tab') || 'normal';
            if (scopeValue === scopeActual) {
                return;
            }
            const canDiscard = await confirmDiscardChanges();
            if (!canDiscard) {
                return;
            }
            setScope(scopeValue);
        });
    });

    if (feriadoTurnoConfig) {
        const turnoInputs = feriadoTurnoConfig.querySelectorAll('[data-feriado-turno-id]');
        turnoInputs.forEach((input) => {
            input.addEventListener('input', function () {
                this.value = sanitizeCoverageValue(this.value).toString();
                refreshDirtyState();
            });
        });
    }

    if (btnGuardarReemplazos) {
        btnGuardarReemplazos.addEventListener('click', guardarGrupoReemplazo);
    }

    async function guardarReglasEquipo(options = {}) {
        const silent = !!options.silent;
        const equipoId = getEquipoId();
        if (!equipoId) {
            mostrarToast('Equipo no especificado', 'error');
            return false;
        }

        const data = {
            equipoId,
            maximoSlotsFinSemanaPorMes: sanitizeMaxFinesSemanaMesValue(maxFinesSemanaMesInput ? maxFinesSemanaMesInput.value : 10),
            maximoTurnosNocturnosPorMes: sanitizeMaxNocturnosMesValue(maxNocturnosMesInput ? maxNocturnosMesInput.value : 15),
            maximoTurnosNocturnosPorSemana: sanitizeMaxNocturnosSemanaValue(maxNocturnosSemanaInput ? maxNocturnosSemanaInput.value : 2),
            nocturnosConsecutivos: nocturnosConsecutivosInput ? nocturnosConsecutivosInput.checked : false
        };

        try {
            const response = await fetch('/Planificacion/SaveEquipoPlanificacionConfig', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify(data)
            });
            const result = await response.json();

            if (result.success) {
                if (!silent) {
                    mostrarToast(result.message || 'Reglas del equipo guardadas.', 'success');
                }
                return true;
            }

            mostrarToast(result.error || 'Error al guardar reglas del equipo', 'error');
            return false;
        } catch (error) {
            console.error('Error al guardar reglas del equipo:', error);
            mostrarToast('Error al guardar reglas del equipo', 'error');
            return false;
        }
    }

    if (maxNocturnosMesInput) {
        maxNocturnosMesInput.addEventListener('input', () => {
            maxNocturnosMesInput.value = `${sanitizeMaxNocturnosMesValue(maxNocturnosMesInput.value)}`;
            refreshDirtyState();
        });

        maxNocturnosMesInput.addEventListener('blur', () => {
            maxNocturnosMesInput.value = `${sanitizeMaxNocturnosMesValue(maxNocturnosMesInput.value)}`;
            refreshDirtyState();
        });
    }

    if (maxNocturnosSemanaInput) {
        maxNocturnosSemanaInput.addEventListener('input', () => {
            maxNocturnosSemanaInput.value = `${sanitizeMaxNocturnosSemanaValue(maxNocturnosSemanaInput.value)}`;
            refreshDirtyState();
        });

        maxNocturnosSemanaInput.addEventListener('blur', () => {
            maxNocturnosSemanaInput.value = `${sanitizeMaxNocturnosSemanaValue(maxNocturnosSemanaInput.value)}`;
            refreshDirtyState();
        });
    }

    if (nocturnosConsecutivosInput) {
        nocturnosConsecutivosInput.addEventListener('change', () => {
            refreshDirtyState();
        });
    }

    if (maxFinesSemanaMesInput) {
        maxFinesSemanaMesInput.addEventListener('input', () => {
            maxFinesSemanaMesInput.value = `${sanitizeMaxFinesSemanaMesValue(maxFinesSemanaMesInput.value)}`;
            refreshDirtyState();
        });

        maxFinesSemanaMesInput.addEventListener('blur', () => {
            maxFinesSemanaMesInput.value = `${sanitizeMaxFinesSemanaMesValue(maxFinesSemanaMesInput.value)}`;
            refreshDirtyState();
        });
    }

    rowDetailToggleButtons.forEach((button) => {
        button.addEventListener('click', () => {
            const turnoId = button.getAttribute('data-row-detail-turno-id');
            if (!turnoId) {
                return;
            }

            const isVisible = button.getAttribute('aria-pressed') === 'true';
            setRowDetailVisibility(turnoId, !isVisible);
        });
    });

    if (btnAgregarReemplazo) {
        btnAgregarReemplazo.addEventListener('click', () => {
            if (!reemplazanteSelect || !reemplazaTurnoSelect || !reemplazaDiaSelect) return;
            const grupoReemplazanteId = reemplazanteSelect.value;
            const tipoTurnoId = reemplazaTurnoSelect.value;
            const dia = reemplazaDiaSelect.value;
            if (!grupoReemplazanteId || !tipoTurnoId || !dia) {
                mostrarToast('Completa todos los campos para agregar un reemplazo.', 'error');
                return;
            }
            const ya = reemplazosData.some(e =>
                e.grupoReemplazanteId === grupoReemplazanteId &&
                e.tipoTurnoId === tipoTurnoId &&
                e.dia === dia
            );
            if (ya) {
                mostrarToast('Este reemplazo ya esta configurado.', 'error');
                return;
            }
            reemplazosData.push({ grupoReemplazanteId, tipoTurnoId, dia });
            renderReemplazos();
            updateReemplazaSaveState();
            reemplazanteSelect.value = '';
            reemplazaTurnoSelect.value = '';
            reemplazaDiaSelect.value = '';
        });
    }

    if (reemplazosLista) {
        reemplazosLista.addEventListener('click', (e) => {
            const btn = e.target.closest('.planificacion-reemplazo-delete');
            if (!btn) return;
            const idx = parseInt(btn.getAttribute('data-idx'), 10);
            if (!isNaN(idx) && idx >= 0 && idx < reemplazosData.length) {
                reemplazosData.splice(idx, 1);
                renderReemplazos();
                updateReemplazaSaveState();
            }
        });
    }

    ocultarMatriz();
    const initialMode = modeInput ? modeInput.value : 'planificacion';
    if (canBlueprint && (initialMode === 'blueprint' || initialMode === 'planificacion')) {
        modoActual = initialMode;
    } else {
        modoActual = 'planificacion';
    }
    updateModeTabsUi();
    updateScopeTabsUi();
    updateContextBadge();
    updateFeriadoGroupLabel();
    syncAuxiliarStateToView();
    if (maxNocturnosMesInput) {
        maxNocturnosMesInput.value = `${sanitizeMaxNocturnosMesValue(maxNocturnosMesInput.value)}`;
    }
    if (maxNocturnosSemanaInput) {
        maxNocturnosSemanaInput.value = `${sanitizeMaxNocturnosSemanaValue(maxNocturnosSemanaInput.value)}`;
    }
    if (maxFinesSemanaMesInput) {
        maxFinesSemanaMesInput.value = `${sanitizeMaxFinesSemanaMesValue(maxFinesSemanaMesInput.value)}`;
    }
    actualizarResumen();
    updateSaveStateUi();
    cargarVistaNormal();
})();


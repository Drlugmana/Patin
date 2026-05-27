using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TurneroTcs.Data;
using TurneroTcs.Models;
using TurneroTcs.Services.RotacionV2.Application;
using TurneroTcs.Services.RotacionV2.Domain;
using Xunit;
using Xunit.Abstractions;

namespace TurneroTcs.Tests.RotacionV2;

[Trait("Category", "IntegrationRealDb")]
public sealed class BatchRealDbIntegrationTests
{
    private static readonly DateTime FechaInicioDiagnostico = new(2026, 4, 20, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly string[] GrupoIdsBatchReales = ["56bcfdgZ", "Yp18qPiZ", "pu0ocJh2"];
    private static readonly string[] DiasSemana = ["lunes", "martes", "miercoles", "jueves", "viernes", "sabado", "domingo"];
    private const string DiaConfiguracionNocturnosMes = "__CFG_MAX_NOCTURNOS_MES__";
    private const int MaximoTurnosNocturnosPorMesDefault = 10;
    private const int MaximoTurnosNocturnosPorMesMin = 7;
    private const int MaximoTurnosNocturnosPorMesMax = 20;
    private readonly ITestOutputHelper _salida;

    public BatchRealDbIntegrationTests(ITestOutputHelper salida)
    {
        _salida = salida;
    }

    [Fact]
    public async Task GeneracionRealDb_Batch_DebeReportarTrazabilidadPara6Semanas()
    {
        await EjecutarEscenarioRealAsync(6);
    }

    [Fact]
    public async Task GeneracionRealDb_Batch_DebeReportarTrazabilidadPara7Semanas()
    {
        await EjecutarEscenarioRealAsync(7);
    }

    [Fact]
    public async Task GeneracionRealDb_Batch_DebeReportarTrazabilidadPara8Semanas()
    {
        await EjecutarEscenarioRealAsync(8);
    }

    private async Task EjecutarEscenarioRealAsync(int numeroSemanas)
    {
        if (!TryCreateRealDbContext(out var db, out var motivo))
        {
            _salida.WriteLine($"[SKIP-MANUAL] {motivo}");
            return;
        }

        await using (db)
        {
            try
            {
                if (!await db.Database.CanConnectAsync())
                {
                    _salida.WriteLine("[SKIP-MANUAL] No se pudo conectar a la base local para la prueba real.");
                    return;
                }
            }
            catch (Exception ex)
            {
                _salida.WriteLine($"[SKIP-MANUAL] Conexion a base local no disponible: {ex.Message}");
                return;
            }

            var escenario = await ConstruirEscenarioRealBatchAsync(db, numeroSemanas);
            _salida.WriteLine($"Equipo BD: {escenario.NombreEquipo} ({escenario.EquipoId})");
            _salida.WriteLine("Grupos BD: " + string.Join(
                " | ",
                escenario.GruposSeleccionados.Select(grupo => $"{grupo.NombreGrupo}={grupo.GrupoIdReal}")));
            _salida.WriteLine(escenario.AgendaSemanal);

            Assert.True(
                escenario.Solucion.Estado is EstadoSolucionRotacion.Optima or EstadoSolucionRotacion.Factible,
                $"Estado inesperado: {escenario.Solucion.Estado} / {escenario.Solucion.DetalleEstado}{Environment.NewLine}" +
                $"Equipo BD: {escenario.NombreEquipo} ({escenario.EquipoId}){Environment.NewLine}" +
                $"Grupos BD: {string.Join(" | ", escenario.GruposSeleccionados.Select(grupo => $"{grupo.NombreGrupo}={grupo.GrupoIdReal}"))}{Environment.NewLine}{Environment.NewLine}" +
                escenario.AgendaSemanal);
        }
    }

    private async Task<EscenarioRealBatch> ConstruirEscenarioRealBatchAsync(ApplicationDbContext db, int numeroSemanas)
    {
        var gruposSeleccionados = await ResolverGruposBatchAsync(db);
        var grupoVisualPorIdReal = gruposSeleccionados.ToDictionary(
            grupo => grupo.GrupoIdReal,
            grupo => grupo.NombreGrupo,
            StringComparer.OrdinalIgnoreCase);
        var gruposIdsReales = gruposSeleccionados.Select(grupo => grupo.GrupoIdReal).ToList();
        var equipoId = gruposSeleccionados[0].EquipoId;
        var nombreEquipo = gruposSeleccionados[0].NombreEquipo;

        var auxiliaresEquipo = await db.PlanificacionesAuxiliaresEquipo
            .Include(auxiliar => auxiliar.GruposPermitidos)
            .Where(auxiliar => auxiliar.EquipoId == equipoId
                && !(auxiliar.DesdeDia == DiaConfiguracionNocturnosMes && auxiliar.HastaDia == DiaConfiguracionNocturnosMes))
            .AsNoTracking()
            .ToListAsync();

        var apoyosGrupo = await db.PlanificacionesApoyoGrupo
            .Where(apoyo => gruposIdsReales.Contains(apoyo.GrupoId))
            .AsNoTracking()
            .ToListAsync();

        var turnosOpcionalesVacacionGrupo = await db.PlanificacionesTurnosOpcionalesVacacionGrupo
            .Where(opcional => gruposIdsReales.Contains(opcional.GrupoId))
            .AsNoTracking()
            .ToListAsync();

        var planificacionesPorGrupo = new Dictionary<string, List<Planificacion>>(StringComparer.OrdinalIgnoreCase);
        foreach (var grupoIdReal in gruposIdsReales)
        {
            var planificaciones = await db.Planificaciones
                .Where(planificacion => planificacion.GrupoId == grupoIdReal)
                .AsNoTracking()
                .OrderBy(planificacion => planificacion.Dia)
                .ThenBy(planificacion => planificacion.TipoTurnoId)
                .ToListAsync();

            if (!planificaciones.Any())
            {
                throw new InvalidOperationException($"No hay planificacion configurada para el grupo real {grupoIdReal}.");
            }

            planificacionesPorGrupo[grupoIdReal] = planificaciones;
        }

        var secundariosRaw = await db.PersonaGrupos
            .Where(pg => gruposIdsReales.Contains(pg.GrupoId) && !pg.EsPrincipal)
            .Select(pg => new { pg.PersonaId, pg.GrupoId })
            .ToListAsync();

        var personasPorGrupo = new Dictionary<string, List<Persona>>(StringComparer.OrdinalIgnoreCase);
        var totalPersonas = 0;
        foreach (var grupoIdReal in gruposIdsReales)
        {
            var personas = await db.PersonaGrupos
                .Where(pg => pg.GrupoId == grupoIdReal && pg.EsPrincipal)
                .Include(pg => pg.Persona)
                .Where(pg => pg.Persona != null)
                .Select(pg => pg.Persona!)
                .ToListAsync();

            if (!personas.Any())
            {
                throw new InvalidOperationException($"El grupo real {grupoIdReal} no tiene personas principales asignadas.");
            }

            personasPorGrupo[grupoIdReal] = personas;
            totalPersonas += personas.Count;
        }

        var personasGeneracion = personasPorGrupo.Values
            .SelectMany(personas => personas)
            .GroupBy(persona => persona.PersonaId, StringComparer.OrdinalIgnoreCase)
            .Select(grupo => grupo.First())
            .ToList();

        var aliasPersonaPorIdReal = ConstruirAliasPersonaPorIdReal(personasGeneracion);
        var secundariosPorPersonaId = secundariosRaw
            .Where(item => aliasPersonaPorIdReal.ContainsKey(item.PersonaId))
            .GroupBy(item => item.PersonaId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                grupo => aliasPersonaPorIdReal[grupo.Key],
                grupo => grupo
                    .Select(item => grupoVisualPorIdReal[item.GrupoId])
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        var personaIdsRealesGeneracion = personasGeneracion
            .Select(persona => persona.PersonaId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var reglasGeneracion = await ConstruirReglasRotacionDesdePlanificacionAsync(db, equipoId);
        var vacacionesPorPersonaReal = !reglasGeneracion.Configurables.AplicarVacaciones
            ? new Dictionary<string, HashSet<DateOnly>>(StringComparer.OrdinalIgnoreCase)
            : await ObtenerVacacionesAprobadasPorPersonaAsync(
                db,
                personaIdsRealesGeneracion,
                DateOnly.FromDateTime(FechaInicioDiagnostico.Date),
                DateOnly.FromDateTime(FechaInicioDiagnostico.Date.AddDays((numeroSemanas * 7) - 1)));
        var vacacionesPorPersona = vacacionesPorPersonaReal
            .Where(item => aliasPersonaPorIdReal.ContainsKey(item.Key))
            .ToDictionary(
                item => aliasPersonaPorIdReal[item.Key],
                item => item.Value,
                StringComparer.OrdinalIgnoreCase);

        var turnosObjetivoSemanales = ResolverTurnosObjetivoSemanales(reglasGeneracion.Obligatorias.MinutosObjetivoSemanales, 8 * 60);
        var capacidadSemanalBase = totalPersonas * turnosObjetivoSemanales;

        var gruposSeleccionadosSet = gruposIdsReales.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var auxiliaresPermitidos = reglasGeneracion.Configurables.PermiteTurnosAuxiliares;
        var auxiliaresCompartidos = auxiliaresEquipo
            .Select(auxiliar => new
            {
                auxiliar.TipoTurnoId,
                auxiliar.DesdeDia,
                auxiliar.HastaDia,
                auxiliar.MaxPorDia,
                GruposPermitidos = auxiliar.GruposPermitidos
                    .Select(grupo => grupo.GrupoId)
                    .Where(gruposSeleccionadosSet.Contains)
                    .Select(grupoIdReal => grupoVisualPorIdReal[grupoIdReal])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
            })
            .Where(auxiliar => auxiliaresPermitidos && auxiliar.GruposPermitidos.Count > 0)
            .SelectMany(auxiliar =>
                ExpandirRangoDias(auxiliar.DesdeDia, auxiliar.HastaDia)
                    .Select(dia => new PlanificacionAuxiliarCompartida
                    {
                        SharedKey = $"{auxiliar.TipoTurnoId}|{NormalizarNombreDia(dia)}",
                        Dia = NormalizarNombreDia(dia),
                        TipoHorario = auxiliar.TipoTurnoId,
                        Cantidad = auxiliar.MaxPorDia,
                        GruposPermitidos = new HashSet<string>(auxiliar.GruposPermitidos, StringComparer.OrdinalIgnoreCase)
                    }))
            .ToList();

        var totalPlazasPorSemana = planificacionesPorGrupo.Values
            .SelectMany(planificaciones => planificaciones)
            .Where(planificacion => !planificacion.IsAuxiliar)
            .Sum(planificacion => planificacion.NumeroPersonas);
        var capacidadAuxiliarSemanal = auxiliaresCompartidos.Sum(auxiliar => auxiliar.Cantidad);

        ValidarCapacidadSemanal(totalPlazasPorSemana, capacidadSemanalBase, capacidadAuxiliarSemanal, auxiliaresCompartidos.Count, turnosObjetivoSemanales);

        var tiposTurno = await db.TipoTurnos
            .AsNoTracking()
            .Where(tipoTurno => tipoTurno.Activo)
            .ToDictionaryAsync(tipoTurno => tipoTurno.TipoTurnoId, tipoTurno => tipoTurno);

        var gruposEquipo = new List<GrupoEquipo>();
        var numeroPersonaGlobal = 1;
        foreach (var grupoReal in gruposSeleccionados)
        {
            var personas = personasPorGrupo[grupoReal.GrupoIdReal];
            var planificaciones = planificacionesPorGrupo[grupoReal.GrupoIdReal];
            var planificacionesNormales = planificaciones
                .Where(planificacion => !planificacion.IsAuxiliar)
                .ToList();
            var apoyoPorCelda = apoyosGrupo
                .Where(apoyo => apoyo.GrupoId == grupoReal.GrupoIdReal)
                .GroupBy(apoyo => $"{NormalizarNombreDia(apoyo.Dia)}|{apoyo.TipoTurnoId}", StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    grupo => grupo.Key,
                    grupo => grupo.Last().CantidadApoyo,
                    StringComparer.OrdinalIgnoreCase);
            var opcionalVacacionPorCelda = turnosOpcionalesVacacionGrupo
                .Where(opcional => opcional.GrupoId == grupoReal.GrupoIdReal)
                .GroupBy(opcional => $"{NormalizarNombreDia(opcional.Dia)}|{opcional.TipoTurnoId}", StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    grupo => grupo.Key,
                    _ => true,
                    StringComparer.OrdinalIgnoreCase);

            gruposEquipo.Add(new GrupoEquipo
            {
                GrupoId = grupoReal.NombreGrupo,
                Personas = personas.Select(persona =>
                {
                    var alias = aliasPersonaPorIdReal[persona.PersonaId];
                    return new PersonaTurno
                    {
                        PersonaId = alias,
                        Nombre = alias,
                        Numero = numeroPersonaGlobal++,
                        Grupo = grupoReal.NombreGrupo,
                        GrupoId = grupoReal.NombreGrupo,
                        GruposSecundarios = secundariosPorPersonaId.TryGetValue(alias, out var secundarios)
                            ? new HashSet<string>(secundarios, StringComparer.OrdinalIgnoreCase)
                            : new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    };
                }).ToList(),
                PlanificacionSemanal = planificacionesNormales.Select(planificacion => new PlanificacionTurno
                {
                    Dia = NormalizarNombreDia(planificacion.Dia),
                    TipoHorario = planificacion.TipoTurnoId,
                    Cantidad = planificacion.NumeroPersonas,
                    CantidadApoyo = apoyoPorCelda.TryGetValue($"{NormalizarNombreDia(planificacion.Dia)}|{planificacion.TipoTurnoId}", out var cantidadApoyo)
                        ? cantidadApoyo
                        : 0,
                    PuedeOmitirsePorVacacion = opcionalVacacionPorCelda.ContainsKey($"{NormalizarNombreDia(planificacion.Dia)}|{planificacion.TipoTurnoId}")
                }).ToList()
            });
        }

        var equipoPlantilla = new EquipoPlantilla
        {
            Nombre = nombreEquipo,
            Grupos = gruposEquipo,
            PlanificacionAuxiliarCompartidaSemanal = auxiliaresCompartidos
        };

        var turnosNormales = new List<Turno>();
        var turnosAuxiliares = new List<Turno>();
        var numeroTurnoGlobal = 1;

        foreach (var grupo in equipoPlantilla.Grupos)
        {
            var planificacionOrdenada = grupo.PlanificacionSemanal
                .OrderBy(planificacion => ObtenerIndiceDiaSemana(planificacion.Dia))
                .ThenBy(planificacion => tiposTurno.TryGetValue(planificacion.TipoHorario, out var tipoTurno) ? tipoTurno.HoraInicio : TimeOnly.MinValue)
                .ToList();

            foreach (var planificacion in planificacionOrdenada)
            {
                if (!tiposTurno.TryGetValue(planificacion.TipoHorario, out var tipoTurno))
                {
                    _salida.WriteLine($"ADVERTENCIA TEST: tipo turno {planificacion.TipoHorario} no encontrado.");
                    continue;
                }

                var fechaBase = FechaInicioDiagnostico.Date.AddDays(ObtenerIndiceDiaSemana(planificacion.Dia));
                var inicio = fechaBase.Add(tipoTurno.HoraInicio.ToTimeSpan());
                var fin = fechaBase.Add(tipoTurno.HoraFin.ToTimeSpan());
                if (fin <= inicio)
                {
                    fin = fin.AddDays(1);
                }

                turnosNormales.Add(new Turno
                {
                    NumeroTurno = numeroTurnoGlobal++,
                    GrupoId = grupo.GrupoId,
                    Dia = NormalizarNombreDia(planificacion.Dia),
                    TipoHorario = planificacion.TipoHorario,
                    TipoTurnoId = planificacion.TipoHorario,
                    Inicio = inicio,
                    Fin = fin,
                    MinimoPersTurno = planificacion.Cantidad,
                    CapacidadPlanificada = planificacion.Cantidad,
                    MaximoApoyoCedible = Math.Min(planificacion.Cantidad, Math.Max(0, planificacion.CantidadApoyo)),
                    IsAuxiliar = false,
                    PuedeOmitirsePorVacacion = planificacion.PuedeOmitirsePorVacacion,
                    MinutosTrabajoComputables = CalcularMinutosTrabajoComputables(tipoTurno.HoraInicio, tipoTurno.HoraFin),
                    PersonaTurnoTurno = []
                });
            }

            var auxiliaresGrupo = equipoPlantilla.PlanificacionAuxiliarCompartidaSemanal
                .Where(auxiliar => auxiliar.GruposPermitidos.Contains(grupo.GrupoId))
                .OrderBy(auxiliar => ObtenerIndiceDiaSemana(auxiliar.Dia))
                .ThenBy(auxiliar => tiposTurno.TryGetValue(auxiliar.TipoHorario, out var tipoTurno) ? tipoTurno.HoraInicio : TimeOnly.MinValue)
                .ToList();

            foreach (var auxiliar in auxiliaresGrupo)
            {
                if (!tiposTurno.TryGetValue(auxiliar.TipoHorario, out var tipoTurnoAux))
                {
                    _salida.WriteLine($"ADVERTENCIA TEST: tipo turno auxiliar {auxiliar.TipoHorario} no encontrado.");
                    continue;
                }

                var fechaBaseAux = FechaInicioDiagnostico.Date.AddDays(ObtenerIndiceDiaSemana(auxiliar.Dia));
                var inicioAux = fechaBaseAux.Add(tipoTurnoAux.HoraInicio.ToTimeSpan());
                var finAux = fechaBaseAux.Add(tipoTurnoAux.HoraFin.ToTimeSpan());
                if (finAux <= inicioAux)
                {
                    finAux = finAux.AddDays(1);
                }

                turnosAuxiliares.Add(new Turno
                {
                    NumeroTurno = numeroTurnoGlobal++,
                    GrupoId = grupo.GrupoId,
                    Dia = NormalizarNombreDia(auxiliar.Dia),
                    TipoHorario = auxiliar.TipoHorario,
                    TipoTurnoId = auxiliar.TipoHorario,
                    Inicio = inicioAux,
                    Fin = finAux,
                    MinimoPersTurno = auxiliar.Cantidad,
                    CapacidadPlanificada = auxiliar.Cantidad,
                    MaximoApoyoCedible = 0,
                    IsAuxiliar = true,
                    PuedeOmitirsePorVacacion = false,
                    AuxiliarSharedKey = auxiliar.SharedKey,
                    AuxiliarMaxCompartido = auxiliar.Cantidad,
                    MinutosTrabajoComputables = CalcularMinutosTrabajoComputables(tipoTurnoAux.HoraInicio, tipoTurnoAux.HoraFin),
                    PersonaTurnoTurno = []
                });
            }
        }

        var personasUnificadas = equipoPlantilla.Grupos
            .SelectMany(grupo => grupo.Personas)
            .GroupBy(persona => persona.PersonaId, StringComparer.OrdinalIgnoreCase)
            .Select(grupoPersonas =>
            {
                var basePersona = grupoPersonas.First();
                var secundarios = grupoPersonas
                    .SelectMany(persona => persona.GruposSecundarios)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                return new PersonaTurno
                {
                    PersonaId = basePersona.PersonaId,
                    Nombre = basePersona.Nombre,
                    Numero = basePersona.Numero,
                    Grupo = basePersona.Grupo,
                    GrupoId = basePersona.GrupoId,
                    GruposSecundarios = secundarios
                };
            })
            .OrderBy(persona => persona.Numero)
            .ToList();

        if (personasUnificadas.Count == 0 || turnosNormales.Count == 0)
        {
            throw new InvalidOperationException("No se pudo construir la entrada RotacionV2 para la prueba real.");
        }

        var plantillaV2 = new Plantilla
        {
            Nombre = equipoPlantilla.Nombre,
            GrupoId = equipoId,
            PersonaTurno = personasUnificadas,
            Turnos = turnosNormales,
            TurnosAuxiliares = turnosAuxiliares
        };

        var minutosPorTurnoBase = ResolverMinutosPorTurnoBase(
            tiposTurno,
            turnosNormales.Select(turno => turno.TipoTurnoId));
        turnosObjetivoSemanales = ResolverTurnosObjetivoSemanales(reglasGeneracion.Obligatorias.MinutosObjetivoSemanales, minutosPorTurnoBase);
        capacidadSemanalBase = totalPersonas * turnosObjetivoSemanales;
        ValidarCapacidadSemanal(totalPlazasPorSemana, capacidadSemanalBase, capacidadAuxiliarSemanal, auxiliaresCompartidos.Count, turnosObjetivoSemanales);

        var fechaInicioDateOnly = DateOnly.FromDateTime(FechaInicioDiagnostico.Date);
        var fechaFinDateOnly = DateOnly.FromDateTime(FechaInicioDiagnostico.Date.AddDays((numeroSemanas * 7) - 1));
        var feriadosRango = await ObtenerFeriadosEnRangoAsync(db, fechaInicioDateOnly, fechaFinDateOnly);

        var servicio = new ServicioRotacion();
        var problema = servicio.CrearProblema(
            plantillaV2,
            numeroSemanas,
            FechaInicioDiagnostico,
            vacacionesPorPersona,
            feriadosRango,
            reglasGeneracion);

        if (!DiagnosticarFactibilidadEstructural(problema, out var detalleFactibilidad))
        {
            throw new InvalidOperationException($"Factibilidad estructural fallida: {detalleFactibilidad}");
        }

        var solucion = servicio.Resolver(
            plantillaV2,
            numeroSemanas,
            FechaInicioDiagnostico,
            vacacionesPorPersona,
            feriadosRango,
            reglasGeneracion,
            new OpcionesSolverRotacion
            {
                TiempoMaximoResolucion = TimeSpan.FromSeconds(60),
                CantidadWorkers = Math.Max(1, Environment.ProcessorCount),
                ReportarDiagnostico = mensaje => _salida.WriteLine($"RotacionV2 diag: {mensaje}")
            });

        var configuracionFeriado = await ConstruirConfiguracionVisibilidadFeriadoAsync(db, equipoId, grupoVisualPorIdReal);
        var visibilidadFeriados = HelperVisibilidadFeriados.Calcular(problema, solucion, configuracionFeriado);
        var agendaSemanal = RotacionV2TestHelper.ConstruirAgendaSemanal(problema, solucion, visibilidadFeriados: visibilidadFeriados);

        return new EscenarioRealBatch(
            equipoId,
            nombreEquipo,
            gruposSeleccionados,
            problema,
            solucion,
            agendaSemanal);
    }

    private static Dictionary<string, string> ConstruirAliasPersonaPorIdReal(IReadOnlyCollection<Persona> personas)
    {
        var aliasPorId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var personasPorNombre = personas
            .GroupBy(persona => LimpiarTexto(persona.Nombre), StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var grupoNombre in personasPorNombre)
        {
            if (grupoNombre.Count() == 1)
            {
                var persona = grupoNombre.First();
                aliasPorId[persona.PersonaId] = persona.Nombre.Trim();
                continue;
            }

            var personasPorNombreCompleto = grupoNombre
                .GroupBy(persona => LimpiarTexto(ConstruirNombreVisiblePersona(persona)), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (personasPorNombreCompleto.All(grupo => grupo.Count() == 1))
            {
                foreach (var grupoCompleto in personasPorNombreCompleto)
                {
                    var persona = grupoCompleto.First();
                    aliasPorId[persona.PersonaId] = ConstruirNombreVisiblePersona(persona);
                }

                continue;
            }

            foreach (var persona in grupoNombre)
            {
                aliasPorId[persona.PersonaId] = $"{ConstruirNombreVisiblePersona(persona)} ({persona.Ultimatix})";
            }
        }

        return aliasPorId;
    }

    private static string ConstruirNombreVisiblePersona(Persona persona)
    {
        var nombreCompleto = persona.NombreCompleto?.Trim();
        return string.IsNullOrWhiteSpace(nombreCompleto)
            ? persona.Nombre.Trim()
            : nombreCompleto;
    }

    private static string LimpiarTexto(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor) ? string.Empty : valor.Trim();
    }

    private static void ValidarCapacidadSemanal(
        int totalPlazasPorSemana,
        int capacidadSemanalBase,
        int capacidadAuxiliarSemanal,
        int cantidadAuxiliaresCompartidos,
        int turnosObjetivoSemanales)
    {
        if (totalPlazasPorSemana < capacidadSemanalBase)
        {
            var faltantes = capacidadSemanalBase - totalPlazasPorSemana;
            if (cantidadAuxiliaresCompartidos == 0)
            {
                throw new InvalidOperationException(
                    $"La configuracion semanal deja {faltantes} plazas sin cubrir para completar los {turnosObjetivoSemanales} turnos objetivo por persona.");
            }

            if (capacidadAuxiliarSemanal < faltantes)
            {
                throw new InvalidOperationException(
                    $"La configuracion semanal necesita {faltantes} plazas auxiliares, pero solo aporta {capacidadAuxiliarSemanal}.");
            }
        }
        else if (totalPlazasPorSemana > capacidadSemanalBase)
        {
            var excedente = totalPlazasPorSemana - capacidadSemanalBase;
            throw new InvalidOperationException(
                $"La cobertura semanal excede la capacidad base del equipo en {excedente} plazas.");
        }
    }

    private static async Task<List<GrupoRealSeleccionado>> ResolverGruposBatchAsync(ApplicationDbContext db)
    {
        var grupos = await db.Grupos
            .AsNoTracking()
            .Include(grupo => grupo.Equipo)
            .Where(grupo => GrupoIdsBatchReales.Contains(grupo.GrupoId))
            .ToListAsync();

        var gruposActivos = grupos
            .Where(grupo => grupo.Activo)
            .ToList();

        var faltantes = GrupoIdsBatchReales
            .Where(grupoId => gruposActivos.All(grupo => !string.Equals(grupo.GrupoId, grupoId, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (faltantes.Count > 0)
        {
            throw new InvalidOperationException(
                $"No se encontraron todos los grupos activos requeridos por grupo_id. Faltan: {string.Join(", ", faltantes)}.");
        }

        var gruposConEquipo = gruposActivos
            .Where(grupo => grupo.Equipo != null)
            .ToList();

        var equiposDetectados = gruposConEquipo
            .Select(grupo => grupo.EquipoId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (equiposDetectados.Count != 1)
        {
            throw new InvalidOperationException(
                $"Los grupo_id reales no pertenecen a un unico equipo. Equipos detectados: {string.Join(", ", equiposDetectados)}.");
        }

        var equipoId = equiposDetectados[0];
        var nombreEquipo = gruposConEquipo[0].Equipo?.NombreEquipo ?? equipoId;

        return gruposConEquipo
            .OrderBy(grupo => Array.IndexOf(GrupoIdsBatchReales, grupo.GrupoId))
            .Select(grupo => new GrupoRealSeleccionado(grupo.GrupoId, grupo.NombreGrupo, equipoId, nombreEquipo))
            .ToList();
    }

    private static async Task<ReglasRotacion> ConstruirReglasRotacionDesdePlanificacionAsync(ApplicationDbContext db, string equipoId)
    {
        var permiteTurnosAuxiliares = !string.IsNullOrWhiteSpace(equipoId)
            && await db.PlanificacionesAuxiliaresEquipo
                .AsNoTracking()
                .AnyAsync(auxiliar => auxiliar.EquipoId == equipoId
                    && !(auxiliar.DesdeDia == DiaConfiguracionNocturnosMes && auxiliar.HastaDia == DiaConfiguracionNocturnosMes));
        var balancearCargaFeriados = !string.IsNullOrWhiteSpace(equipoId)
            && await db.FeriadoCoberturaConfigs
                .AsNoTracking()
                .AnyAsync(configuracion => configuracion.EquipoId == equipoId);
        var maximoTurnosNocturnosPorMes = await GetMaximoTurnosNocturnosPorMesAsync(db, equipoId);

        return new ReglasRotacion
        {
            Obligatorias = new ReglasGlobalesObligatorias
            {
                MinutosObjetivoSemanales = 40 * 60,
                MinutosMinimosDescansoEntreTurnos = 8 * 60,
                MinimoDiasDescansoConsecutivosPorSemana = 2
            },
            Configurables = new PoliticasConfigurablesEquipo
            {
                MaximoTurnosPorDia = 1,
                AplicarVacaciones = true,
                PermiteTurnosAuxiliares = permiteTurnosAuxiliares,
                EvitarFinesSemanaConsecutivos = true,
                MaximoFinesSemanaConsecutivos = 2,
                MaximoSlotsFinSemanaPorMes = 4,
                MaximoTurnosNocturnosPorMes = maximoTurnosNocturnosPorMes,
                BalancearHorasSemanales = true,
                BalancearTurnosNocturnos = true,
                BalancearCargaFeriados = balancearCargaFeriados
            }
        };
    }

    private static async Task<int> GetMaximoTurnosNocturnosPorMesAsync(ApplicationDbContext db, string equipoId)
    {
        if (string.IsNullOrWhiteSpace(equipoId))
        {
            return MaximoTurnosNocturnosPorMesDefault;
        }

        var configuracion = await db.PlanificacionesAuxiliaresEquipo
            .AsNoTracking()
            .Where(planificacion => planificacion.EquipoId == equipoId
                && planificacion.DesdeDia == DiaConfiguracionNocturnosMes
                && planificacion.HastaDia == DiaConfiguracionNocturnosMes)
            .OrderByDescending(planificacion => planificacion.PlanificacionAuxiliarEquipoId)
            .FirstOrDefaultAsync();

        return Math.Clamp(
            configuracion?.MaxPorDia ?? MaximoTurnosNocturnosPorMesDefault,
            MaximoTurnosNocturnosPorMesMin,
            MaximoTurnosNocturnosPorMesMax);
    }

    private static async Task<Dictionary<string, HashSet<DateOnly>>> ObtenerVacacionesAprobadasPorPersonaAsync(
        ApplicationDbContext db,
        IReadOnlyCollection<string> personaIds,
        DateOnly fechaInicio,
        DateOnly fechaFin)
    {
        var resultado = new Dictionary<string, HashSet<DateOnly>>(StringComparer.OrdinalIgnoreCase);
        if (personaIds.Count == 0 || fechaFin < fechaInicio)
        {
            return resultado;
        }

        var vacaciones = await (
            from vacacion in db.Vacaciones.AsNoTracking()
            join solicitud in db.Solicitudes.AsNoTracking() on vacacion.SolicitudId equals solicitud.SolicitudId
            where personaIds.Contains(solicitud.PersonaSolicitanteId)
                && solicitud.EstadoSolicitud == SolicitudEstado.AprobadoFinal
                && vacacion.FechaInicio <= fechaFin
                && vacacion.FechaFin >= fechaInicio
            select new
            {
                solicitud.PersonaSolicitanteId,
                vacacion.FechaInicio,
                vacacion.FechaFin
            })
            .ToListAsync();

        foreach (var vacacion in vacaciones)
        {
            if (!resultado.TryGetValue(vacacion.PersonaSolicitanteId, out var dias))
            {
                dias = new HashSet<DateOnly>();
                resultado[vacacion.PersonaSolicitanteId] = dias;
            }

            var inicio = vacacion.FechaInicio > fechaInicio ? vacacion.FechaInicio : fechaInicio;
            var fin = vacacion.FechaFin < fechaFin ? vacacion.FechaFin : fechaFin;
            for (var actual = inicio; actual <= fin; actual = actual.AddDays(1))
            {
                dias.Add(actual);
            }
        }

        return resultado;
    }

    private static async Task<HashSet<DateOnly>> ObtenerFeriadosEnRangoAsync(ApplicationDbContext db, DateOnly fechaInicio, DateOnly fechaFin)
    {
        var resultado = new HashSet<DateOnly>();

        var feriados = await db.Feriados
            .AsNoTracking()
            .Where(feriado => feriado.InicioFeriado <= fechaFin && feriado.FinFeriado >= fechaInicio)
            .Select(feriado => new { feriado.InicioFeriado, feriado.FinFeriado })
            .ToListAsync();

        foreach (var feriado in feriados)
        {
            var inicio = feriado.InicioFeriado > fechaInicio ? feriado.InicioFeriado : fechaInicio;
            var fin = feriado.FinFeriado < fechaFin ? feriado.FinFeriado : fechaFin;
            for (var actual = inicio; actual <= fin; actual = actual.AddDays(1))
            {
                resultado.Add(actual);
            }
        }

        return resultado;
    }

    private static async Task<ConfiguracionVisibilidadFeriado?> ConstruirConfiguracionVisibilidadFeriadoAsync(
        ApplicationDbContext db,
        string equipoId,
        IReadOnlyDictionary<string, string> grupoVisualPorIdReal)
    {
        if (string.IsNullOrWhiteSpace(equipoId))
        {
            return null;
        }

        var filas = await db.FeriadoCoberturaConfigs
            .AsNoTracking()
            .Where(configuracion => configuracion.EquipoId == equipoId)
            .ToListAsync();

        if (filas.Count == 0)
        {
            return null;
        }

        return new ConfiguracionVisibilidadFeriado
        {
            EquipoId = equipoId,
            Coberturas = filas
                .Where(fila =>
                    !string.IsNullOrWhiteSpace(fila.GrupoId) &&
                    !string.IsNullOrWhiteSpace(fila.TipoTurnoId) &&
                    grupoVisualPorIdReal.ContainsKey(fila.GrupoId))
                .Select(fila => new CoberturaVisibilidadFeriado
                {
                    Id = $"{grupoVisualPorIdReal[fila.GrupoId]}|{fila.TipoTurnoId}",
                    GruposIncluidos = new HashSet<string>([grupoVisualPorIdReal[fila.GrupoId]], StringComparer.OrdinalIgnoreCase),
                    TiposTurnoIds = new HashSet<string>([fila.TipoTurnoId], StringComparer.OrdinalIgnoreCase),
                    PersonasVisibles = Math.Max(0, fila.CantidadVisible)
                })
                .ToList()
        };
    }

    private static bool DiagnosticarFactibilidadEstructural(ProblemaRotacion problema, out string detalle)
    {
        var bloqueosVacacion = problema.Ausencias
            .Where(ausencia => string.Equals(ausencia.Motivo, "Vacaciones", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                ausencia => ausencia.EmpleadoId,
                ausencia => ausencia.Fechas,
                StringComparer.OrdinalIgnoreCase);

        var conflictos = new List<string>();
        foreach (var slot in problema.Slots.Where(slot => !slot.EsAuxiliar && slot.MaximoApoyoCedible <= 0))
        {
            var elegibles = problema.Empleados
                .Where(empleado => PuedeCubrirGrupoDiagnostico(empleado, slot.GrupoId))
                .Where(empleado =>
                    !bloqueosVacacion.TryGetValue(empleado.Id, out var fechasBloqueadas)
                    || !fechasBloqueadas.Contains(slot.Fecha))
                .Select(empleado => empleado.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            if (elegibles < slot.EmpleadosRequeridos)
            {
                conflictos.Add($"S{slot.IndiceSemana + 1} {slot.Fecha:yyyy-MM-dd} {slot.GrupoId}/{slot.TipoTurnoId}: requiere {slot.EmpleadosRequeridos} y solo hay {elegibles} elegibles");
            }
        }

        if (conflictos.Count == 0)
        {
            detalle = string.Empty;
            return true;
        }

        var muestra = string.Join(" | ", conflictos.Take(5));
        detalle = conflictos.Count > 5
            ? $"{muestra} | +{conflictos.Count - 5} conflictos mas"
            : muestra;
        return false;
    }

    private static bool PuedeCubrirGrupoDiagnostico(Empleado empleado, string grupoId)
    {
        if (string.IsNullOrWhiteSpace(grupoId))
        {
            return true;
        }

        if (string.Equals(empleado.GrupoPrimarioId, grupoId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return empleado.GruposSecundariosIds.Contains(grupoId);
    }

    private static List<string> ExpandirRangoDias(string desdeDia, string hastaDia)
    {
        var desde = Array.FindIndex(DiasSemana, dia => string.Equals(dia, desdeDia, StringComparison.OrdinalIgnoreCase));
        var hasta = Array.FindIndex(DiasSemana, dia => string.Equals(dia, hastaDia, StringComparison.OrdinalIgnoreCase));

        if (desde < 0 || hasta < 0)
        {
            return [];
        }

        var dias = new List<string>();
        var indice = desde;
        while (true)
        {
            dias.Add(DiasSemana[indice]);
            if (indice == hasta)
            {
                break;
            }

            indice = (indice + 1) % DiasSemana.Length;
            if (indice == desde)
            {
                break;
            }
        }

        return dias;
    }

    private static int CalcularMinutosTrabajoComputables(TimeOnly horaInicio, TimeOnly horaFin)
    {
        var inicio = DateOnly.MinValue.ToDateTime(horaInicio);
        var fin = DateOnly.MinValue.ToDateTime(horaFin);
        if (fin <= inicio)
        {
            fin = fin.AddDays(1);
        }

        var minutos = (int)Math.Round((fin - inicio).TotalMinutes, MidpointRounding.AwayFromZero);
        return minutos >= 9 * 60 ? minutos - 60 : minutos;
    }

    private static int ResolverMinutosPorTurnoBase(
        IReadOnlyDictionary<string, TipoTurno> tiposTurno,
        IEnumerable<string> tipoTurnoIds)
    {
        var minutosDistintos = tipoTurnoIds
            .Where(tipoTurnoId => !string.IsNullOrWhiteSpace(tipoTurnoId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(tipoTurnoId => tiposTurno.TryGetValue(tipoTurnoId, out var tipoTurno)
                ? CalcularMinutosTrabajoComputables(tipoTurno.HoraInicio, tipoTurno.HoraFin)
                : 0)
            .Where(minutos => minutos > 0)
            .Distinct()
            .ToList();

        return minutosDistintos.Count == 1 ? minutosDistintos[0] : 8 * 60;
    }

    private static int ResolverTurnosObjetivoSemanales(int minutosObjetivoSemanales, int minutosPorTurnoBase)
    {
        var divisor = Math.Max(1, minutosPorTurnoBase);
        return Math.Max(1, minutosObjetivoSemanales / divisor);
    }

    private static int ObtenerIndiceDiaSemana(string dia)
    {
        return dia.Trim().ToLowerInvariant() switch
        {
            "lunes" => 0,
            "martes" => 1,
            "miercoles" or "miércoles" => 2,
            "jueves" => 3,
            "viernes" => 4,
            "sabado" or "sábado" => 5,
            "domingo" => 6,
            _ => 0
        };
    }

    private static string NormalizarNombreDia(string dia)
    {
        return dia.Trim().ToLowerInvariant() switch
        {
            "lunes" => "Lunes",
            "martes" => "Martes",
            "miercoles" or "miércoles" => "Miércoles",
            "jueves" => "Jueves",
            "viernes" => "Viernes",
            "sabado" or "sábado" => "Sábado",
            "domingo" => "Domingo",
            _ => dia
        };
    }

    private static bool TryCreateRealDbContext(out ApplicationDbContext db, out string motivo)
    {
        db = null!;
        var repoRoot = EncontrarRaizRepositorio();
        if (repoRoot is null)
        {
            motivo = "No se encontro la raiz del repositorio desde el contexto del test.";
            return false;
        }

        var configPath = Path.Combine(repoRoot, "appsettings.Development.json");
        if (!File.Exists(configPath))
        {
            motivo = "No existe appsettings.Development.json para abrir la base real.";
            return false;
        }

        string? connectionString;
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(configPath));
            connectionString = json.RootElement
                .GetProperty("ConnectionStrings")
                .GetProperty("DefaultConnection")
                .GetString();
        }
        catch (Exception ex)
        {
            motivo = $"No se pudo leer DefaultConnection desde appsettings.Development.json: {ex.Message}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            motivo = "DefaultConnection esta vacia en appsettings.Development.json.";
            return false;
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        db = new ApplicationDbContext(options);
        motivo = string.Empty;
        return true;
    }

    private static string? EncontrarRaizRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "TurneroTcs.csproj")))
            {
                return directorio.FullName;
            }

            directorio = directorio.Parent;
        }

        return null;
    }

    private sealed record GrupoRealSeleccionado(
        string GrupoIdReal,
        string NombreGrupo,
        string EquipoId,
        string NombreEquipo);

    private sealed record EscenarioRealBatch(
        string EquipoId,
        string NombreEquipo,
        IReadOnlyList<GrupoRealSeleccionado> GruposSeleccionados,
        ProblemaRotacion Problema,
        SolucionRotacionCp Solucion,
        string AgendaSemanal);
}

using TurneroTcs.Services.RotacionV2.Application;
using TurneroTcs.Services.RotacionV2.Domain;
using Xunit;
using Xunit.Abstractions;

namespace TurneroTcs.Tests.RotacionV2;

public sealed class AuxiliaresCompartidosPorGrupoTests
{
    private static readonly DateTime FechaInicioLunes = new(2026, 4, 20, 0, 0, 0, DateTimeKind.Unspecified);
    private readonly ITestOutputHelper _salida;

    public AuxiliaresCompartidosPorGrupoTests(ITestOutputHelper salida)
    {
        _salida = salida;
    }

    [Fact]
    public void Resolver_DebePermitirQueControlMApoyeABatch_SinLlenarSiempreSuPropiaCobertura()
    {
        var servicio = new ServicioRotacion();
        var plantilla = CrearPlantillaBatchConControlM();
        var vacaciones = CrearVacacionesEscenarioReal();
        var reglas = CrearReglasBase();
        var problema = servicio.CrearProblema(
            plantilla,
            cantidadSemanas: 6,
            fechaInicio: FechaInicioLunes,
            vacacionesPorPersonaId: vacaciones,
            reglas: reglas);

        var solucion = servicio.Resolver(
            plantilla,
            cantidadSemanas: 6,
            fechaInicio: FechaInicioLunes,
            vacacionesPorPersonaId: vacaciones,
            reglas: reglas,
            opcionesSolver: new OpcionesSolverRotacion
            {
                TiempoMaximoResolucion = TimeSpan.FromSeconds(20),
                CantidadWorkers = 1
            });

        var agendaSemanal = ConstruirAgendaSemanal(problema, solucion);
        _salida.WriteLine(agendaSemanal);

        Assert.True(
            solucion.Estado is EstadoSolucionRotacion.Optima or EstadoSolucionRotacion.Factible,
            $"Estado inesperado: {solucion.Estado} / {solucion.DetalleEstado}{Environment.NewLine}{Environment.NewLine}{agendaSemanal}");

        ValidarTurnosEsperadosPorSemana(problema, solucion, agendaSemanal);
        ValidarApoyoControlMEnBatchDuranteVacacionesDeGiselle(problema, solucion, agendaSemanal);
        ValidarControlMConCoberturaCedida(problema, solucion, agendaSemanal);
        ValidarNoHayVeladasConsecutivasEntreSemanas(problema, solucion, agendaSemanal);
    }

    private static ReglasRotacion CrearReglasBase()
    {
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
                AplicarVacaciones = true,
                PermiteTurnosAuxiliares = true,
                EvitarFinesSemanaConsecutivos = true,
                MaximoFinesSemanaConsecutivos = 2,
                MaximoSlotsFinSemanaPorMes = 4,
                MaximoTurnosNocturnosPorMes = 10,
                BalancearHorasSemanales = true,
                BalancearTurnosNocturnos = true,
                BalancearCargaFeriados = false
            }
        };
    }

    private static Plantilla CrearPlantillaBatchConControlM()
    {
        return new Plantilla
        {
            Nombre = "BatchControlM",
            GrupoId = "Batch",
            PersonaTurno = CrearPersonasEscenarioReal(),
            Turnos = CrearTurnosPrincipales().Concat(CrearTurnosControlM()).ToList(),
            TurnosAuxiliares = CrearTurnosAuxiliaresBatch().ToList()
        };
    }

    private static List<PersonaTurno> CrearPersonasEscenarioReal()
    {
        return
        [
            CrearPersona("Amilcar", 1, "Amilcar", "Batch", "Servidores"),
            CrearPersona("Andrea", 2, "Andrea", "Batch", "Servidores"),
            CrearPersona("Carlos", 3, "Carlos", "Servidores"),
            CrearPersona("Cesar", 4, "Cesar", "Batch", "Servidores"),
            CrearPersona("Dario", 5, "Dario", "Servidores"),
            CrearPersona("Giselle", 6, "Giselle", "Servidores"),
            CrearPersona("Lizbeth", 7, "Lizbeth", "Servidores"),
            CrearPersona("Luis", 8, "Luis", "Batch"),
            CrearPersona("Javier R", 9, "Javier R", "Control M", "Batch"),
            CrearPersona("Xavier M", 10, "Xavier M", "Control M", "Batch")
        ];
    }

    private static PersonaTurno CrearPersona(string id, int numero, string nombre, string grupoPrimario, params string[] gruposPermitidos)
    {
        return new PersonaTurno
        {
            PersonaId = id,
            Nombre = nombre,
            Numero = numero,
            Grupo = grupoPrimario,
            GrupoId = grupoPrimario,
            GruposSecundarios = gruposPermitidos
                .Where(grupo => !string.Equals(grupo, grupoPrimario, StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static IEnumerable<Turno> CrearTurnosPrincipales()
    {
        var turnos = new List<Turno>();
        var diasSemana = new[] { "lunes", "martes", "miercoles", "jueves", "viernes" };
        var diasFinSemana = new[] { "sabado", "domingo" };
        var turnosPrincipales = new[]
        {
            new { TipoTurnoId = "TT03", TipoHorario = "MANANA", Inicio = (7, 0), Fin = (16, 0), GrupoId = "Batch" },
            new { TipoTurnoId = "TT03", TipoHorario = "MANANA", Inicio = (7, 0), Fin = (16, 0), GrupoId = "Servidores" },
            new { TipoTurnoId = "TT01", TipoHorario = "TARDE", Inicio = (15, 0), Fin = (0, 0), GrupoId = "Batch" },
            new { TipoTurnoId = "TT01", TipoHorario = "TARDE", Inicio = (15, 0), Fin = (0, 0), GrupoId = "Servidores" },
            new { TipoTurnoId = "TT02", TipoHorario = "NOCHE", Inicio = (23, 0), Fin = (8, 0), GrupoId = "Batch" },
            new { TipoTurnoId = "TT02", TipoHorario = "NOCHE", Inicio = (23, 0), Fin = (8, 0), GrupoId = "Servidores" }
        };

        var numeroTurno = 1;
        foreach (var dia in diasSemana)
        {
            foreach (var definicion in turnosPrincipales)
            {
                turnos.Add(CrearTurno(
                    numeroTurno++,
                    definicion.GrupoId,
                    dia,
                    definicion.TipoTurnoId,
                    definicion.TipoHorario,
                    definicion.Inicio.Item1,
                    definicion.Inicio.Item2,
                    definicion.Fin.Item1,
                    definicion.Fin.Item2,
                    minimoPersonas: 1,
                    capacidadPlanificada: 1,
                    esAuxiliar: false));
            }
        }

        foreach (var dia in diasFinSemana)
        {
            turnos.Add(CrearTurno(numeroTurno++, string.Empty, dia, "TT03", "MANANA", 7, 0, 16, 0, 1, 1, false));
            turnos.Add(CrearTurno(numeroTurno++, string.Empty, dia, "TT01", "TARDE", 15, 0, 0, 0, 1, 1, false));
            turnos.Add(CrearTurno(numeroTurno++, string.Empty, dia, "TT02", "NOCHE", 23, 0, 8, 0, 1, 1, false));
        }

        return turnos;
    }

    private static IEnumerable<Turno> CrearTurnosControlM()
    {
        var turnos = new List<Turno>();
        var dias = new[] { "lunes", "martes", "miercoles", "jueves", "viernes" };
        var numeroTurno = 500;

        foreach (var dia in dias)
        {
            turnos.Add(CrearTurno(
                numeroTurno++,
                "Control M",
                dia,
                "TT09",
                "CONTROL_MANANA",
                9,
                0,
                18,
                0,
                minimoPersonas: 1,
                capacidadPlanificada: 1,
                esAuxiliar: false,
                maximoApoyoCedible: 1));

            turnos.Add(CrearTurno(
                numeroTurno++,
                "Control M",
                dia,
                "TT01",
                "CONTROL_TARDE",
                15,
                0,
                0,
                0,
                minimoPersonas: 1,
                capacidadPlanificada: 1,
                esAuxiliar: false,
                puedeOmitirsePorVacacion: true));
        }

        return turnos;
    }

    private static IEnumerable<Turno> CrearTurnosAuxiliaresBatch()
    {
        var turnos = new List<Turno>();
        var dias = new[] { "lunes", "martes", "miercoles", "jueves", "viernes" };

        for (var indice = 0; indice < dias.Length; indice++)
        {
            turnos.Add(CrearTurno(
                1000 + indice,
                string.Empty,
                dias[indice],
                "TT05",
                "NOCHE_MADRUGADA",
                18,
                0,
                3,
                0,
                minimoPersonas: 0,
                capacidadPlanificada: 1,
                esAuxiliar: true,
                auxiliarSharedKey: "BATCH_TT05",
                auxiliarMaxCompartido: 1));
        }

        return turnos;
    }

    private static Turno CrearTurno(
        int numeroTurno,
        string grupoId,
        string dia,
        string tipoTurnoId,
        string tipoHorario,
        int horaInicio,
        int minutoInicio,
        int horaFin,
        int minutoFin,
        int minimoPersonas,
        int capacidadPlanificada,
        bool esAuxiliar,
        string auxiliarSharedKey = "",
        int auxiliarMaxCompartido = 0,
        int maximoApoyoCedible = 0,
        bool puedeOmitirsePorVacacion = false)
    {
        return new Turno
        {
            NumeroTurno = numeroTurno,
            GrupoId = grupoId,
            Dia = dia,
            TipoHorario = tipoHorario,
            TipoTurnoId = tipoTurnoId,
            Inicio = new DateTime(2000, 1, 1, horaInicio, minutoInicio, 0, DateTimeKind.Unspecified),
            Fin = new DateTime(2000, 1, 1, horaFin, minutoFin, 0, DateTimeKind.Unspecified),
            PersonaTurnoTurno = [],
            MinimoPersTurno = minimoPersonas,
            CapacidadPlanificada = capacidadPlanificada,
            MaximoApoyoCedible = maximoApoyoCedible,
            IsAuxiliar = esAuxiliar,
            EsReemplazoVacacion = false,
            PuedeOmitirsePorVacacion = esAuxiliar || puedeOmitirsePorVacacion,
            AuxiliarSharedKey = esAuxiliar ? auxiliarSharedKey : string.Empty,
            AuxiliarMaxCompartido = esAuxiliar ? auxiliarMaxCompartido : 0,
            EsOpcional = esAuxiliar,
            MinutosTrabajoComputables = 8 * 60
        };
    }

    private static Dictionary<string, HashSet<DateOnly>> CrearVacacionesEscenarioReal()
    {
        return new Dictionary<string, HashSet<DateOnly>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Giselle"] = CrearRangoFechas(new DateOnly(2026, 4, 20), new DateOnly(2026, 4, 30)),
            ["Xavier M"] = CrearRangoFechas(new DateOnly(2026, 5, 6), new DateOnly(2026, 5, 14))
        };
    }

    private static HashSet<DateOnly> CrearRangoFechas(DateOnly inicio, DateOnly fin)
    {
        var fechas = new HashSet<DateOnly>();
        for (var fecha = inicio; fecha <= fin; fecha = fecha.AddDays(1))
        {
            fechas.Add(fecha);
        }

        return fechas;
    }

    private static void ValidarTurnosEsperadosPorSemana(
        ProblemaRotacion problema,
        SolucionRotacionCp solucion,
        string agendaSemanal)
    {
        var slotsPorId = problema.Slots.ToDictionary(slot => slot.Id);

        foreach (var empleado in problema.Empleados)
        {
            for (var semana = 0; semana < problema.CantidadSemanas; semana++)
            {
                var esperado = CalcularObjetivoSemanalEsperado(problema, empleado, semana);
                var actual = solucion.Asignaciones.Count(asignacion =>
                    asignacion.EmpleadoId == empleado.Id &&
                    slotsPorId[asignacion.IdSlot].IndiceSemana == semana);

                Assert.True(
                    actual == esperado,
                    $"Empleado {empleado.Nombre} semana {semana + 1} esperado={esperado} actual={actual}.{Environment.NewLine}{Environment.NewLine}{agendaSemanal}");
            }
        }
    }

    private static int CalcularObjetivoSemanalEsperado(
        ProblemaRotacion problema,
        Empleado empleado,
        int indiceSemana)
    {
        var fechasBloqueadas = CalcularFechasBloqueadasPorVacacion(problema, empleado.Id);
        var fechasDisponibles = problema.Slots
            .Where(slot => slot.IndiceSemana == indiceSemana)
            .Select(slot => slot.Fecha)
            .Distinct()
            .Where(fecha =>
                !fechasBloqueadas.Contains(fecha) &&
                problema.Slots.Any(slot =>
                    slot.IndiceSemana == indiceSemana &&
                    slot.Fecha == fecha &&
                    PuedeCubrirGrupo(empleado, slot.GrupoId)))
            .ToHashSet();

        var soloFinDeSemanaDisponible = fechasBloqueadas
            .Any(fecha => problema.Slots.Any(slot => slot.IndiceSemana == indiceSemana && slot.Fecha == fecha)) &&
            fechasDisponibles.Count > 0 &&
            fechasDisponibles.All(fecha =>
                problema.Slots.Any(slot =>
                    slot.IndiceSemana == indiceSemana &&
                    slot.Fecha == fecha &&
                    slot.IndiceDia is 5 or 6));

        if (soloFinDeSemanaDisponible)
        {
            return 0;
        }

        return Math.Min(5, CalcularMaximoFechasTrabajables(problema, empleado, indiceSemana, fechasDisponibles));
    }

    private static int CalcularMaximoFechasTrabajables(
        ProblemaRotacion problema,
        Empleado empleado,
        int indiceSemana,
        IReadOnlySet<DateOnly> fechasDisponibles)
    {
        if (fechasDisponibles.Count == 0)
        {
            return 0;
        }

        var fechasOrdenadas = fechasDisponibles
            .OrderBy(fecha => fecha)
            .ToArray();
        var mejor = 0;
        var totalPatrones = 1 << fechasOrdenadas.Length;

        for (var mascara = 0; mascara < totalPatrones; mascara++)
        {
            var fechasTrabajadas = new HashSet<DateOnly>();
            var cantidadTrabajada = 0;

            for (var indice = 0; indice < fechasOrdenadas.Length; indice++)
            {
                if ((mascara & (1 << indice)) == 0)
                {
                    continue;
                }

                fechasTrabajadas.Add(fechasOrdenadas[indice]);
                cantidadTrabajada++;
            }

            if (cantidadTrabajada <= mejor)
            {
                continue;
            }

            if (!CumpleDescansoPosteriorVacacionSemanal(problema, empleado.Id, indiceSemana, fechasTrabajadas))
            {
                continue;
            }

            mejor = cantidadTrabajada;
        }

        return mejor;
    }

    private static bool CumpleDescansoPosteriorVacacionSemanal(
        ProblemaRotacion problema,
        string empleadoId,
        int indiceSemana,
        IReadOnlySet<DateOnly> fechasTrabajadas)
    {
        var fechaInicioSemana = problema.FechaInicio.AddDays(indiceSemana * 7);
        var fechaFinSemana = fechaInicioSemana.AddDays(6);
        var descansosSemana = problema.DescansosPosterioresVacacion
            .Where(descanso =>
                string.Equals(descanso.EmpleadoId, empleadoId, StringComparison.OrdinalIgnoreCase) &&
                descanso.FechaRegreso >= fechaInicioSemana &&
                descanso.FechaRegreso <= fechaFinSemana)
            .OrderBy(descanso => descanso.FechaRegreso)
            .ToArray();

        foreach (var descanso in descansosSemana)
        {
            var cumpleAlguno = false;
            for (var fecha = descanso.FechaRegreso; fecha < fechaFinSemana; fecha = fecha.AddDays(1))
            {
                if (!fechasTrabajadas.Contains(fecha) && !fechasTrabajadas.Contains(fecha.AddDays(1)))
                {
                    cumpleAlguno = true;
                    break;
                }
            }

            if (!cumpleAlguno)
            {
                return false;
            }
        }

        return true;
    }

    private static HashSet<DateOnly> CalcularFechasBloqueadasPorVacacion(ProblemaRotacion problema, string empleadoId)
    {
        return problema.Ausencias
            .Where(ausencia => string.Equals(ausencia.EmpleadoId, empleadoId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(ausencia => ausencia.Fechas)
            .ToHashSet();
    }

    private static bool PuedeCubrirGrupo(Empleado empleado, string grupoId)
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

    private static void ValidarApoyoControlMEnBatchDuranteVacacionesDeGiselle(
        ProblemaRotacion problema,
        SolucionRotacionCp solucion,
        string agendaSemanal)
    {
        var inicio = new DateOnly(2026, 4, 20);
        var fin = new DateOnly(2026, 4, 30);
        var slotsPorId = problema.Slots.ToDictionary(slot => slot.Id);
        var empleadosPorId = problema.Empleados.ToDictionary(empleado => empleado.Id);

        var apoyos = solucion.Asignaciones.Count(asignacion =>
        {
            var slot = slotsPorId[asignacion.IdSlot];
            if (slot.EsAuxiliar || slot.Fecha < inicio || slot.Fecha > fin || !string.Equals(slot.GrupoId, "Batch", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.Equals(empleadosPorId[asignacion.EmpleadoId].GrupoPrimarioId, "Control M", StringComparison.OrdinalIgnoreCase);
        });

        Assert.True(
            apoyos > 0,
            $"No hubo apoyo de Control M hacia Batch durante las vacaciones de Giselle.{Environment.NewLine}{Environment.NewLine}{agendaSemanal}");
    }

    private static void ValidarControlMConCoberturaCedida(
        ProblemaRotacion problema,
        SolucionRotacionCp solucion,
        string agendaSemanal)
    {
        var slotsPorId = problema.Slots.ToDictionary(slot => slot.Id);
        var asignacionesPorSlot = solucion.Asignaciones
            .GroupBy(asignacion => asignacion.IdSlot)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Count());

        var slotsControlMSinCubrir = problema.Slots
            .Where(slot =>
                string.Equals(slot.GrupoId, "Control M", StringComparison.OrdinalIgnoreCase) &&
                slot.Fecha >= new DateOnly(2026, 5, 6) &&
                slot.Fecha <= new DateOnly(2026, 5, 14))
            .Count(slot => !asignacionesPorSlot.ContainsKey(slot.Id));

        Assert.True(
            slotsControlMSinCubrir > 0,
            $"No se detecto cobertura cedida en Control M durante las vacaciones de Xavier M.{Environment.NewLine}{Environment.NewLine}{agendaSemanal}");
    }

    private static void ValidarNoHayVeladasConsecutivasEntreSemanas(
        ProblemaRotacion problema,
        SolucionRotacionCp solucion,
        string agendaSemanal)
    {
        var slotsPorId = problema.Slots.ToDictionary(slot => slot.Id);

        foreach (var empleado in problema.Empleados)
        {
            for (var semana = 0; semana < problema.CantidadSemanas - 1; semana++)
            {
                var domingoNocturno = solucion.Asignaciones.Any(asignacion =>
                    asignacion.EmpleadoId == empleado.Id &&
                    slotsPorId[asignacion.IdSlot].IndiceSemana == semana &&
                    slotsPorId[asignacion.IdSlot].IndiceDia == 6 &&
                    slotsPorId[asignacion.IdSlot].EsTurnoNocturno);

                var lunesNocturno = solucion.Asignaciones.Any(asignacion =>
                    asignacion.EmpleadoId == empleado.Id &&
                    slotsPorId[asignacion.IdSlot].IndiceSemana == semana + 1 &&
                    slotsPorId[asignacion.IdSlot].IndiceDia == 0 &&
                    slotsPorId[asignacion.IdSlot].EsTurnoNocturno);

                Assert.False(
                    domingoNocturno && lunesNocturno,
                    $"Velada consecutiva entre semanas detectada para {empleado.Nombre} entre S{semana + 1} y S{semana + 2}.{Environment.NewLine}{Environment.NewLine}{agendaSemanal}");
            }
        }
    }

    private static string ConstruirAgendaSemanal(ProblemaRotacion problema, SolucionRotacionCp solucion)
    {
        var nombresDias = new[] { "Lun", "Mar", "Mie", "Jue", "Vie", "Sab", "Dom" };
        var empleadoPorId = problema.Empleados.ToDictionary(empleado => empleado.Id);
        var slotsPorId = problema.Slots.ToDictionary(slot => slot.Id);
        var lineas = new List<string>();
        const int anchoEtiqueta = 30;
        const int anchoCelda = 28;
        var separador = "+" + new string('-', anchoEtiqueta + 2) + "+" +
                         string.Join("+", Enumerable.Range(0, 7).Select(_ => new string('-', anchoCelda + 2))) + "+";

        for (var semana = 0; semana < problema.CantidadSemanas; semana++)
        {
            lineas.Add($"AGENDA SEMANAL DE TURNOS - SEMANA {semana + 1}");
            lineas.Add(separador);
            lineas.Add("| " +
                Ajustar("Horario / Grupo", anchoEtiqueta) + " | " +
                string.Join(" | ", nombresDias.Select(dia => Ajustar(dia, anchoCelda))) + " |");
            lineas.Add(separador);

            var slotsSemana = problema.Slots
                .Where(slot => slot.IndiceSemana == semana)
                .OrderBy(slot => slot.InicioLocal.TimeOfDay)
                .ThenBy(slot => slot.GrupoId)
                .ThenBy(slot => slot.CodigoTurno)
                .ToArray();

            var filas = slotsSemana
                .GroupBy(slot => new
                {
                    Horario = $"{slot.InicioLocal:HH:mm}-{slot.FinLocal:HH:mm}",
                    Grupo = string.IsNullOrWhiteSpace(slot.GrupoId) ? (slot.EsAuxiliar ? "aux" : "compartido") : slot.GrupoId
                });

            foreach (var fila in filas)
            {
                var celdasDias = new List<string>();
                for (var dia = 0; dia < 7; dia++)
                {
                    var slotsDia = fila.Where(slot => slot.IndiceDia == dia).ToArray();
                    if (slotsDia.Length == 0)
                    {
                        celdasDias.Add(Ajustar("-", anchoCelda));
                        continue;
                    }

                    var etiquetas = slotsDia
                        .SelectMany(slot => solucion.Asignaciones
                            .Where(asignacion => asignacion.IdSlot == slot.Id)
                            .Select(asignacion => FormatearAsignacion(empleadoPorId[asignacion.EmpleadoId], slot)))
                        .DefaultIfEmpty("-")
                        .ToArray();

                    celdasDias.Add(Ajustar(string.Join(", ", etiquetas), anchoCelda));
                }

                lineas.Add("| " +
                    Ajustar($"{fila.Key.Horario} / {fila.Key.Grupo}", anchoEtiqueta) + " | " +
                    string.Join(" | ", celdasDias) + " |");
                lineas.Add(separador);
            }

            lineas.Add(ConstruirResumenSemanal(problema, solucion, semana, slotsPorId));
            lineas.Add(string.Empty);
        }

        lineas.Add(ConstruirResumenNocturnosMensual(problema, solucion, slotsPorId));

        return string.Join(Environment.NewLine, lineas);
    }

    private static string ConstruirResumenSemanal(
        ProblemaRotacion problema,
        SolucionRotacionCp solucion,
        int semana,
        IReadOnlyDictionary<string, SlotTurno> slotsPorId)
    {
        var resumenes = problema.Empleados
            .OrderBy(empleado => empleado.Numero)
            .Select(empleado =>
            {
                var asignaciones = solucion.Asignaciones
                    .Where(asignacion => asignacion.EmpleadoId == empleado.Id && slotsPorId[asignacion.IdSlot].IndiceSemana == semana)
                    .ToArray();

                var asignacionesAcumuladas = solucion.Asignaciones
                    .Where(asignacion => asignacion.EmpleadoId == empleado.Id && slotsPorId[asignacion.IdSlot].IndiceSemana <= semana)
                    .ToArray();

                var turnos = asignaciones.Length;
                var minutos = asignaciones.Sum(asignacion => slotsPorId[asignacion.IdSlot].MinutosTrabajoComputables);
                var horas = minutos / 60d;
                var turnosAcumulados = asignacionesAcumuladas.Length;
                return $"{empleado.Nombre}={turnos}t/{horas:0.#}h/acum:{turnosAcumulados}t";
            });

        return "Resumen: " + string.Join(" | ", resumenes);
    }

    private static string ConstruirResumenNocturnosMensual(
        ProblemaRotacion problema,
        SolucionRotacionCp solucion,
        IReadOnlyDictionary<string, SlotTurno> slotsPorId)
    {
        var resumenes = problema.Empleados
            .OrderBy(empleado => empleado.Numero)
            .Select(empleado =>
            {
                var turnosNocturnos = solucion.Asignaciones.Count(asignacion =>
                    asignacion.EmpleadoId == empleado.Id &&
                    slotsPorId[asignacion.IdSlot].EsTurnoNocturno);

                return $"{empleado.Nombre}={turnosNocturnos}n";
            });

        return "Resumen nocturnos mes: " + string.Join(" | ", resumenes);
    }

    private static string FormatearAsignacion(Empleado empleado, SlotTurno slot)
    {
        return $"{empleado.Nombre} [{ResolverGrupoVisual(slot, empleado)}]";
    }

    private static string ResolverGrupoVisual(SlotTurno slot, Empleado empleado)
    {
        if (!string.IsNullOrWhiteSpace(slot.GrupoId))
        {
            return slot.GrupoId;
        }

        if (slot.EsAuxiliar && slot.LlaveCompartidaAuxiliar.StartsWith("BATCH", StringComparison.OrdinalIgnoreCase))
        {
            return "Batch";
        }

        return empleado.GrupoPrimarioId;
    }

    private static string Ajustar(string texto, int ancho)
    {
        if (texto.Length > ancho)
        {
            return texto[..(ancho - 3)] + "...";
        }

        return texto.PadRight(ancho);
    }
}

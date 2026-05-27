using System.Globalization;
using System.Text;
using TurneroTcs.Services.RotacionV2.Domain;

namespace TurneroTcs.Services.RotacionV2.Application;

/// <summary>
/// Transforma una <see cref="Plantilla"/> de rotación en un <see cref="ProblemaRotacion"/> de dominio,
/// expandiendo los turnos de la plantilla semanal a todas las semanas del horizonte de planificación
/// y normalizando los nombres de día para garantizar coincidencia insensible a acentos y mayúsculas.
/// </summary>
public sealed class MapeadorRotacion
{
    private static readonly Dictionary<string, int> DesplazamientosDia = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lunes"] = 0,
        ["martes"] = 1,
        ["miercoles"] = 2,
        ["jueves"] = 3,
        ["viernes"] = 4,
        ["sabado"] = 5,
        ["domingo"] = 6
    };

    /// <summary>
    /// Convierte la plantilla y los datos de entrada en un <see cref="ProblemaRotacion"/> listo para el motor.
    /// <para>
    /// Pasos:
    /// <list type="number">
    ///   <item>Construye la lista de <see cref="Empleado"/> a partir de las personas de la plantilla.</item>
    ///   <item>Deduce los grupos a partir de los identificadores de grupo de las personas.</item>
    ///   <item>Expande los turnos regulares y auxiliares de la plantilla a cada semana del horizonte.</item>
    ///   <item>Filtra las vacaciones al rango de fechas del horizonte y las convierte en <see cref="AusenciaEmpleado"/>.</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="plantilla">Plantilla con la estructura de turnos y personas del grupo.</param>
    /// <param name="cantidadSemanas">Número de semanas del horizonte de planificación.</param>
    /// <param name="fechaInicio">Fecha y hora de inicio del horizonte (se usa solo la parte de fecha).</param>
    /// <param name="vacacionesPorPersonaId">
    /// Fechas de vacación por identificador de persona.
    /// <see langword="null"/> si no hay vacaciones en el período.
    /// </param>
    /// <param name="feriados">Fechas feriadas dentro del horizonte; <see langword="null"/> si no hay feriados.</param>
    /// <param name="reglas">Reglas de rotación; <see langword="null"/> para usar los valores predeterminados.</param>
    /// <returns>Problema de rotación completamente inicializado.</returns>
    public ProblemaRotacion CrearProblema(
        Plantilla plantilla,
        int cantidadSemanas,
        DateTime fechaInicio,
        Dictionary<string, HashSet<DateOnly>>? vacacionesPorPersonaId = null,
        HashSet<DateOnly>? feriados = null,
        ReglasRotacion? reglas = null,
        IEnumerable<ExcepcionTurno>? excepciones = null)
    {
        ArgumentNullException.ThrowIfNull(plantilla);

        var primeraFecha = DateOnly.FromDateTime(fechaInicio.Date);
        var empleados = plantilla.PersonaTurno
            .Select(persona => new Empleado
            {
                Id = persona.PersonaId,
                Numero = persona.Numero,
                Nombre = persona.Nombre,
                GrupoPrimarioId = !string.IsNullOrWhiteSpace(persona.GrupoId) ? persona.GrupoId : plantilla.GrupoId,
                GruposSecundariosIds = new HashSet<string>(persona.GruposSecundarios, StringComparer.OrdinalIgnoreCase)
            })
            .OrderBy(persona => persona.Numero)
            .ToList();

        var nombresGrupoPorId = plantilla.PersonaTurno
            .Select(persona => new
            {
                Id = !string.IsNullOrWhiteSpace(persona.GrupoId) ? persona.GrupoId : plantilla.GrupoId,
                persona.Grupo
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo.Select(item => item.Grupo).FirstOrDefault(nombre => !string.IsNullOrWhiteSpace(nombre)) ?? grupo.Key,
                StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(plantilla.GrupoId) && !nombresGrupoPorId.ContainsKey(plantilla.GrupoId))
        {
            nombresGrupoPorId[plantilla.GrupoId] = plantilla.GrupoId;
        }

        var grupos = nombresGrupoPorId
            .OrderBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
            .Select(item => new GrupoTrabajo
            {
                Id = item.Key,
                Nombre = item.Value
            })
            .ToList();

        var slots = ExpandirSlots(plantilla.Turnos, cantidadSemanas, primeraFecha)
            .Concat(ExpandirSlots(plantilla.TurnosAuxiliares, cantidadSemanas, primeraFecha))
            .OrderBy(slot => slot.InicioLocal)
            .ThenBy(slot => slot.GrupoId)
            .ThenBy(slot => slot.CodigoTurno)
            .ToList();

        var fechaFinHorizonteExclusiva = primeraFecha.AddDays(cantidadSemanas * 7);
        var ausencias = new List<AusenciaEmpleado>();
        var excepcionesRotacion = excepciones?.ToList() ?? [];
        var descansosPosterioresVacacion = new List<DescansoPosteriorVacacion>();
        if (vacacionesPorPersonaId != null)
        {
            foreach (var empleado in empleados)
            {
                if (!vacacionesPorPersonaId.TryGetValue(empleado.Id, out var fechas) || fechas.Count == 0)
                {
                    continue;
                }

                var fechasFiltradas = fechas
                    .Where(fecha => fecha >= primeraFecha && fecha < fechaFinHorizonteExclusiva)
                    .ToHashSet();

                if (fechasFiltradas.Count == 0)
                {
                    continue;
                }

                ausencias.Add(new AusenciaEmpleado
                {
                    EmpleadoId = empleado.Id,
                    Motivo = "Vacaciones",
                    Fechas = fechasFiltradas
                });

                descansosPosterioresVacacion.AddRange(
                    ConstruirDescansosPosterioresVacacion(empleado.Id, fechasFiltradas, primeraFecha, fechaFinHorizonteExclusiva));
            }
        }

        return new ProblemaRotacion
        {
            ProblemaId = $"{plantilla.GrupoId}:{primeraFecha:yyyyMMdd}:{cantidadSemanas}",
            FechaInicio = primeraFecha,
            CantidadSemanas = cantidadSemanas,
            Empleados = empleados,
            Grupos = grupos,
            Slots = slots,
            Ausencias = ausencias,
            Excepciones = excepcionesRotacion,
            DescansosPosterioresVacacion = descansosPosterioresVacacion,
            Feriados = feriados ?? [],
            Reglas = reglas ?? new ReglasRotacion()
        };
    }

    private static IEnumerable<DescansoPosteriorVacacion> ConstruirDescansosPosterioresVacacion(
        string empleadoId,
        IReadOnlySet<DateOnly> fechasVacacion,
        DateOnly fechaInicioHorizonte,
        DateOnly fechaFinHorizonteExclusiva)
    {
        foreach (var fechaFinBloque in fechasVacacion
                     .Where(fecha => !fechasVacacion.Contains(fecha.AddDays(1)))
                     .OrderBy(fecha => fecha))
        {
            var fechaRegreso = fechaFinBloque.AddDays(1);
            if (fechaRegreso < fechaInicioHorizonte || fechaRegreso >= fechaFinHorizonteExclusiva)
            {
                continue;
            }

            yield return new DescansoPosteriorVacacion
            {
                EmpleadoId = empleadoId,
                FechaRegreso = fechaRegreso
            };
        }
    }

    private static IEnumerable<SlotTurno> ExpandirSlots(IEnumerable<Turno> turnosPlantilla, int cantidadSemanas, DateOnly primeraFecha)
    {
        foreach (var turnoPlantilla in turnosPlantilla)
        {
            var indiceDia = ResolverDesplazamientoDia(turnoPlantilla.Dia);
            for (var indiceSemana = 0; indiceSemana < cantidadSemanas; indiceSemana++)
            {
                var fechaSlot = primeraFecha.AddDays((indiceSemana * 7) + indiceDia);
                var inicioLocal = ComponerFechaHoraLocal(fechaSlot, turnoPlantilla.Inicio);
                var finLocal = ComponerFechaHoraLocal(fechaSlot, turnoPlantilla.Fin);
                if (finLocal <= inicioLocal)
                {
                    finLocal = finLocal.AddDays(1);
                }

                yield return new SlotTurno
                {
                    Id = $"{indiceSemana}:{fechaSlot:yyyyMMdd}:{turnoPlantilla.NumeroTurno}:{turnoPlantilla.GrupoId}:{turnoPlantilla.TipoHorario}",
                    NumeroTurnoPlantilla = turnoPlantilla.NumeroTurno,
                    IndiceSemana = indiceSemana,
                    IndiceDia = indiceDia,
                    Fecha = fechaSlot,
                    NombreDia = NormalizarNombreDia(turnoPlantilla.Dia),
                    GrupoId = turnoPlantilla.GrupoId,
                    TipoTurnoId = turnoPlantilla.TipoTurnoId,
                    CodigoTurno = turnoPlantilla.TipoHorario,
                    InicioLocal = inicioLocal,
                    FinLocal = finLocal,
                    EmpleadosRequeridos = turnoPlantilla.MinimoPersTurno,
                    CapacidadPlanificada = turnoPlantilla.CapacidadPlanificada,
                    MaximoApoyoCedible = turnoPlantilla.MaximoApoyoCedible,
                    EsAuxiliar = turnoPlantilla.IsAuxiliar,
                    EsReemplazoVacacion = turnoPlantilla.EsReemplazoVacacion,
                    PuedeOmitirsePorVacacion = turnoPlantilla.PuedeOmitirsePorVacacion,
                    LlaveCompartidaAuxiliar = turnoPlantilla.AuxiliarSharedKey,
                    MaximoCompartidoAuxiliar = turnoPlantilla.AuxiliarMaxCompartido,
                    MinutosTrabajoComputables = turnoPlantilla.MinutosTrabajoComputables > 0
                        ? turnoPlantilla.MinutosTrabajoComputables
                        : (int)Math.Round((finLocal - inicioLocal).TotalMinutes)
                };
            }
        }
    }

    private static DateTime ComponerFechaHoraLocal(DateOnly fecha, DateTime fuenteHora)
    {
        return fecha.ToDateTime(TimeOnly.FromDateTime(fuenteHora));
    }

    private static int ResolverDesplazamientoDia(string nombreDia)
    {
        var nombreDiaNormalizado = NormalizarNombreDia(nombreDia);
        if (DesplazamientosDia.TryGetValue(nombreDiaNormalizado, out var desplazamiento))
        {
            return desplazamiento;
        }

        throw new InvalidOperationException($"Dia no soportado en RotacionV2: '{nombreDia}'.");
    }

    private static string NormalizarNombreDia(string nombreDia)
    {
        var textoNormalizado = nombreDia.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var constructor = new StringBuilder();
        foreach (var caracter in textoNormalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                constructor.Append(caracter);
            }
        }

        return constructor.ToString();
    }
}

using System.Text.RegularExpressions;
using TurneroTcs.Services.RotacionV2.Application;
using TurneroTcs.Services.RotacionV2.Domain;

namespace TurneroTcs.Tests.RotacionV2;

internal sealed record EstadoSemanaPruebaRotacionV2
{
    public required int IndiceSemana { get; init; }
    public required EstadoSolucionRotacion Estado { get; init; }
    public required string Detalle { get; init; }
    public bool Resuelta => Estado is EstadoSolucionRotacion.Optima or EstadoSolucionRotacion.Factible;
}

internal static partial class RotacionV2TestHelper
{
    [GeneratedRegex(@"S(?<semana>\d+)=(?<estado>[^/,]+)(?:/(?<detalle>[^,]+))?")]
    private static partial Regex EstadoSemanaRegex();

    public static IReadOnlyDictionary<int, EstadoSemanaPruebaRotacionV2> ObtenerEstadosSemanas(SolucionRotacionCp solucion)
    {
        var estados = new Dictionary<int, EstadoSemanaPruebaRotacionV2>();
        foreach (Match match in EstadoSemanaRegex().Matches(solucion.DetalleEstado ?? string.Empty))
        {
            if (!int.TryParse(match.Groups["semana"].Value, out var semanaHumanizada))
            {
                continue;
            }

            if (!Enum.TryParse<EstadoSolucionRotacion>(match.Groups["estado"].Value, out var estado))
            {
                continue;
            }

            var indiceSemana = semanaHumanizada - 1;
            estados[indiceSemana] = new EstadoSemanaPruebaRotacionV2
            {
                IndiceSemana = indiceSemana,
                Estado = estado,
                Detalle = match.Groups["detalle"].Value
            };
        }

        return estados;
    }

    public static string ConstruirAgendaSemanal(
        ProblemaRotacion problema,
        SolucionRotacionCp solucion,
        Func<SlotTurno, Empleado, string>? resolverGrupoVisual = null,
        ResultadoVisibilidadFeriados? visibilidadFeriados = null)
    {
        var nombresDias = new[] { "Lun", "Mar", "Mie", "Jue", "Vie", "Sab", "Dom" };
        var empleadoPorId = problema.Empleados.ToDictionary(empleado => empleado.Id);
        var slotsPorId = problema.Slots.ToDictionary(slot => slot.Id);
        var estadosSemanas = ObtenerEstadosSemanas(solucion);
        var maximoIndiceSemanaReportado = estadosSemanas.Keys.DefaultIfEmpty(-1).Max();
        var lineas = new List<string>
        {
            $"DETALLE SOLVER: {solucion.Estado} / {solucion.DetalleEstado}"
        };

        const int anchoEtiqueta = 30;
        const int anchoCelda = 28;
        var separador = "+" + new string('-', anchoEtiqueta + 2) + "+" +
                         string.Join("+", Enumerable.Range(0, 7).Select(_ => new string('-', anchoCelda + 2))) + "+";

        for (var semana = 0; semana < problema.CantidadSemanas; semana++)
        {
            var fechaInicioSemana = problema.FechaInicio.AddDays(semana * 7);
            var fechaFinSemana = fechaInicioSemana.AddDays(6);
            var encabezadosDias = Enumerable.Range(0, 7)
                .Select(indiceDia =>
                {
                    var fecha = fechaInicioSemana.AddDays(indiceDia);
                    return $"{nombresDias[indiceDia]} {fecha:MM-dd}";
                })
                .ToArray();

            lineas.Add($"AGENDA SEMANAL DE TURNOS - SEMANA {semana + 1}");
            lineas.Add($"Fechas semana: {fechaInicioSemana:yyyy-MM-dd} -> {fechaFinSemana:yyyy-MM-dd}");
            lineas.Add(ConstruirResumenVacacionesSemanal(problema, fechaInicioSemana, fechaFinSemana));

            if (semana > maximoIndiceSemanaReportado)
            {
                lineas.Add("SEMANA NO EJECUTADA: sin intento por corte previo");
                lineas.Add(string.Empty);
                continue;
            }

            if (estadosSemanas.TryGetValue(semana, out var estadoSemana) && !estadoSemana.Resuelta)
            {
                lineas.Add($"SEMANA NO RESUELTA: {estadoSemana.Estado} / {estadoSemana.Detalle}");
                lineas.Add(string.Empty);
                continue;
            }

            lineas.Add(separador);
            lineas.Add("| " +
                Ajustar("Horario / Grupo", anchoEtiqueta) + " | " +
                string.Join(" | ", encabezadosDias.Select(dia => Ajustar(dia, anchoCelda))) + " |");
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
                            .Where(asignacion => visibilidadFeriados?.DebeMostrar(asignacion.IdSlot, asignacion.EmpleadoId) ?? true)
                            .Select(asignacion => FormatearAsignacion(
                                empleadoPorId[asignacion.EmpleadoId],
                                slot,
                                resolverGrupoVisual)))
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
            if (ConstruirResumenOcultadosFeriado(semana, visibilidadFeriados) is string resumenOcultadosFeriado)
            {
                lineas.Add(resumenOcultadosFeriado);
            }

            lineas.Add(string.Empty);
        }

        lineas.Add(ConstruirResumenNocturnosMensual(problema, solucion, slotsPorId));
        return string.Join(Environment.NewLine, lineas);
    }

    public static int ObtenerUltimaSemanaResuelta(SolucionRotacionCp solucion)
    {
        var estados = ObtenerEstadosSemanas(solucion);
        return estados
            .Where(par => par.Value.Resuelta)
            .Select(par => par.Key)
            .DefaultIfEmpty(-1)
            .Max();
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

    private static string ConstruirResumenVacacionesSemanal(
        ProblemaRotacion problema,
        DateOnly fechaInicioSemana,
        DateOnly fechaFinSemana)
    {
        var resumenes = problema.Ausencias
            .Where(ausencia => string.Equals(ausencia.Motivo, "Vacaciones", StringComparison.OrdinalIgnoreCase))
            .Select(ausencia => new
            {
                ausencia.EmpleadoId,
                Fechas = ausencia.Fechas
                    .Where(fecha => fecha >= fechaInicioSemana && fecha <= fechaFinSemana)
                    .OrderBy(fecha => fecha)
                    .ToArray()
            })
            .Where(item => item.Fechas.Length > 0)
            .OrderBy(item => item.EmpleadoId, StringComparer.OrdinalIgnoreCase)
            .Select(item => $"{item.EmpleadoId}[{string.Join(", ", CompactarFechas(item.Fechas))}]")
            .ToArray();

        return resumenes.Length == 0
            ? "Vacaciones semana: ninguna"
            : "Vacaciones semana: " + string.Join(" | ", resumenes);
    }

    private static string? ConstruirResumenOcultadosFeriado(
        int semana,
        ResultadoVisibilidadFeriados? visibilidadFeriados)
    {
        if (visibilidadFeriados is null ||
            !visibilidadFeriados.OcultamientosPorSemana.TryGetValue(semana, out var ocultadosSemana) ||
            ocultadosSemana.Count == 0)
        {
            return null;
        }

        var tramos = ocultadosSemana
            .GroupBy(item => item.Fecha)
            .OrderBy(grupo => grupo.Key)
            .Select(grupo => $"{grupo.Key:yyyy-MM-dd}: {string.Join(", ", grupo
                .OrderBy(item => item.NumeroEmpleado)
                .Select(item => item.NombreEmpleado)
                .Distinct(StringComparer.OrdinalIgnoreCase))}");

        return "Ocultados por feriado: " + string.Join(" | ", tramos);
    }

    private static IEnumerable<string> CompactarFechas(IReadOnlyList<DateOnly> fechasOrdenadas)
    {
        if (fechasOrdenadas.Count == 0)
        {
            yield break;
        }

        var inicio = fechasOrdenadas[0];
        var fin = fechasOrdenadas[0];

        for (var indice = 1; indice < fechasOrdenadas.Count; indice++)
        {
            var fecha = fechasOrdenadas[indice];
            if (fecha == fin.AddDays(1))
            {
                fin = fecha;
                continue;
            }

            yield return FormatearRangoFechas(inicio, fin);
            inicio = fecha;
            fin = fecha;
        }

        yield return FormatearRangoFechas(inicio, fin);
    }

    private static string FormatearRangoFechas(DateOnly inicio, DateOnly fin)
    {
        return inicio == fin
            ? $"{inicio:yyyy-MM-dd}"
            : $"{inicio:yyyy-MM-dd}..{fin:yyyy-MM-dd}";
    }

    private static string FormatearAsignacion(
        Empleado empleado,
        SlotTurno slot,
        Func<SlotTurno, Empleado, string>? resolverGrupoVisual)
    {
        var grupoVisual = resolverGrupoVisual is null
            ? (string.IsNullOrWhiteSpace(slot.GrupoId)
                ? (slot.EsAuxiliar ? "Aux" : "Compartido")
                : slot.GrupoId)
            : resolverGrupoVisual(slot, empleado);

        return $"{empleado.Nombre} [{grupoVisual}]";
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

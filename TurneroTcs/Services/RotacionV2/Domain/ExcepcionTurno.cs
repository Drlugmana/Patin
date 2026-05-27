namespace TurneroTcs.Services.RotacionV2.Domain;

/// <summary>
/// Excepcion temporal que impide asignar a un empleado a un tipo de turno concreto
/// durante un rango de fechas.
/// </summary>
public sealed record ExcepcionTurno
{
    public required string EmpleadoId { get; init; }

    public required string TipoTurnoId { get; init; }

    public required string Motivo { get; init; }

    public required DateOnly FechaInicio { get; init; }

    public required DateOnly FechaFin { get; init; }

    /// <summary>
    /// Dias de la semana en los que aplica la excepcion. Si el conjunto esta vacio
    /// la excepcion aplica a todos los dias del rango.
    /// </summary>
    public ISet<DayOfWeek> DiasSemana { get; init; } = new HashSet<DayOfWeek>();
    public bool AplicaA(DateOnly fecha, string tipoTurnoId)
    {
        var dia = fecha.DayOfWeek;
        var tipoOk = string.Equals(TipoTurnoId, tipoTurnoId, StringComparison.OrdinalIgnoreCase);
        var fechaOk = fecha >= FechaInicio && fecha <= FechaFin;
        var diasOk = DiasSemana == null || DiasSemana.Count == 0 || DiasSemana.Contains(dia);
        return fechaOk && tipoOk && diasOk;
    }
}

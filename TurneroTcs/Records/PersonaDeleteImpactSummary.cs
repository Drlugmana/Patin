namespace TurneroTcs.Records;

/// <summary>
/// Resumen del impacto que tendría eliminar una persona del sistema.
/// Se utiliza para informar al usuario sobre los turnos futuros que serían afectados
/// antes de confirmar la eliminación.
/// </summary>
/// <param name="FutureTurnosCount">Número total de turnos futuros asignados a la persona.</param>
/// <param name="FirstFutureTurnoDate">Fecha del primer turno futuro; <see langword="null"/> si no hay turnos futuros.</param>
/// <param name="LastFutureTurnoDate">Fecha del último turno futuro; <see langword="null"/> si no hay turnos futuros.</param>
/// <param name="HasLinkedFutureTurnos">
/// <see langword="true"/> si la persona tiene al menos un turno futuro que sería eliminado o afectado;
/// <see langword="false"/> en caso contrario.
/// </param>
public sealed record PersonaDeleteImpactSummary(
    int FutureTurnosCount,
    DateOnly? FirstFutureTurnoDate,
    DateOnly? LastFutureTurnoDate,
    bool HasLinkedFutureTurnos);

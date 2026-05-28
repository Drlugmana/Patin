namespace TurneroTcs.Records;

/// <summary>
/// Representa la vista previa de un turno generado automáticamente por el sistema de planificación,
/// antes de ser confirmado y persistido como registro definitivo.
/// </summary>
/// <param name="PersonaId">Identificador de la persona a quien se asigna el turno.</param>
/// <param name="TipoTurnoId">Identificador del tipo de turno generado.</param>
/// <param name="GrupoId">Identificador del grupo al que pertenece la persona en este turno.</param>
/// <param name="FechaTurno">Fecha en la que se realizará el turno.</param>
/// <param name="EsFeriado">Indica si la fecha del turno corresponde a un día feriado. Por defecto <see langword="false"/>.</param>
/// <param name="NoLaboradoPorFeriado">
/// Indica si el turno no será laborado debido al feriado.
/// Por defecto <see langword="false"/>.
/// </param>
public record TurnoGeneradoPreview(
    string PersonaId,
    string TipoTurnoId,
    string GrupoId,
    DateOnly FechaTurno,
    bool EsFeriado = false,
    bool NoLaboradoPorFeriado = false);

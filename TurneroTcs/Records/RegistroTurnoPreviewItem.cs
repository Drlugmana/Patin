namespace TurneroTcs.Records;

/// <summary>
/// Representa un turno individual dentro de una vista previa de planificación,
/// previo a su confirmación y persistencia definitiva.
/// </summary>
/// <param name="PersonaId">Identificador de la persona a quien se asigna el turno.</param>
/// <param name="TipoTurnoId">Identificador del tipo de turno asignado.</param>
/// <param name="FechaTurno">Fecha en la que se realizará el turno.</param>
/// <param name="GrupoId">Identificador del grupo al que pertenece el turno; puede ser <see langword="null"/> si no aplica.</param>
/// <param name="EsFeriado">Indica si la fecha del turno corresponde a un día feriado. Por defecto <see langword="false"/>.</param>
/// <param name="NoLaboradoPorFeriado">
/// Indica si el turno no fue laborado debido al feriado.
/// Por defecto <see langword="false"/>.
/// </param>
public sealed record RegistroTurnoPreviewItem(
    string PersonaId,
    string TipoTurnoId,
    DateOnly FechaTurno,
    string? GrupoId,
    bool EsFeriado = false,
    bool NoLaboradoPorFeriado = false);

namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para mover un turno registrado a una nueva fecha y/o tipo de turno.
/// Se utiliza cuando el turno de una persona debe reasignarse a un día o jornada diferente.
/// </summary>
/// <param name="TurnoId">Identificador del turno que se desea mover.</param>
/// <param name="FechaTurno">Nueva fecha a la que se moverá el turno.</param>
/// <param name="TipoTurnoId">Identificador del tipo de turno destino.</param>
public sealed record RegistroTurnoMoveRequest(
    string TurnoId,
    DateOnly FechaTurno,
    string TipoTurnoId);

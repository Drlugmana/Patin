namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para crear una petición de cambio de turno.
/// Soporta dos modalidades: intercambio directo entre dos turnos existentes,
/// o reasignación del turno origen a una fecha y tipo de turno específicos.
/// </summary>
/// <param name="TipoSolicitudId">Identificador del tipo de solicitud de cambio de turno.</param>
/// <param name="TurnoOrigenId">Identificador del turno que el solicitante desea cambiar.</param>
/// <param name="TurnoDestinoId">
/// Identificador del turno destino para un intercambio directo.
/// <see langword="null"/> cuando se usa la modalidad de reasignación por fecha.
/// </param>
/// <param name="FechaDestino">
/// Fecha destino en formato de cadena cuando se reasigna el turno a un día específico.
/// <see langword="null"/> cuando se usa la modalidad de intercambio directo.
/// </param>
/// <param name="TipoTurnoDestinoId">
/// Identificador del tipo de turno destino para la reasignación.
/// <see langword="null"/> cuando se usa la modalidad de intercambio directo.
/// </param>
/// <param name="Motivo">Justificación del cambio de turno solicitado; puede ser <see langword="null"/>.</param>
public sealed record CambioTurnoCreateRequest(
    string TipoSolicitudId,
    string TurnoOrigenId,
    string? TurnoDestinoId,
    string? FechaDestino,
    string? TipoTurnoDestinoId,
    string? Motivo);

namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para registrar la decisión (aprobación o rechazo) sobre una solicitud pendiente.
/// </summary>
/// <param name="SolicitudId">Identificador de la solicitud sobre la cual se toma la decisión.</param>
public record SolicitudDecisionRequest(string SolicitudId);

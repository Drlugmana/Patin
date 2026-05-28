namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para crear un permiso de ausencia parcial en una fecha determinada.
/// Define el período de ausencia en horas y el motivo de la solicitud.
/// </summary>
/// <param name="TipoSolicitudId">Identificador del tipo de solicitud asociado al permiso.</param>
/// <param name="Fecha">Fecha en la que se solicita el permiso.</param>
/// <param name="HoraInicio">Hora de inicio de la ausencia en formato de cadena (HH:mm).</param>
/// <param name="HoraFin">Hora de fin de la ausencia en formato de cadena (HH:mm).</param>
/// <param name="Motivo">Justificación o razón del permiso solicitado; puede ser <see langword="null"/>.</param>
public sealed record PermisoCreateRequest(
    string TipoSolicitudId,
    DateOnly Fecha,
    string HoraInicio,
    string HoraFin,
    string? Motivo);

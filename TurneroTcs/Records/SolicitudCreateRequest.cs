using TurneroTcs.Models;

namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para crear una nueva solicitud de tipo vacación, permiso o cambio de turno.
/// Solo uno de los campos opcionales debe estar poblado según el tipo de solicitud indicado.
/// </summary>
/// <param name="TipoSolicitudId">Identificador del tipo de solicitud a crear.</param>
/// <param name="Vacacion">Datos requeridos cuando la solicitud corresponde a vacaciones; <see langword="null"/> en caso contrario.</param>
/// <param name="Permiso">Datos requeridos cuando la solicitud corresponde a un permiso; <see langword="null"/> en caso contrario.</param>
/// <param name="CambioTurno">Datos requeridos cuando la solicitud corresponde a un cambio de turno; <see langword="null"/> en caso contrario.</param>
public sealed record SolicitudCreateRequest(
    string TipoSolicitudId,
    VacacionRequest? Vacacion,
    PermisoRequest? Permiso,
    CambioTurnoRequest? CambioTurno);

/// <summary>
/// Datos específicos para una solicitud de vacaciones.
/// Define el rango de fechas que abarca el período solicitado.
/// </summary>
/// <param name="FechaInicio">Primer día del período de vacaciones.</param>
/// <param name="FechaFin">Último día del período de vacaciones (inclusive).</param>
public sealed record VacacionRequest(DateOnly FechaInicio, DateOnly FechaFin);

/// <summary>
/// Datos específicos para una solicitud de permiso de ausencia parcial en un turno.
/// Define el registro de turno afectado, el intervalo horario y el motivo.
/// </summary>
/// <param name="RegistroTurnoId">Identificador del registro de turno en el que se solicita el permiso.</param>
/// <param name="HoraInicio">Hora de inicio de la ausencia dentro del turno.</param>
/// <param name="HoraFin">Hora de fin de la ausencia dentro del turno.</param>
/// <param name="Motivo">Justificación o razón de la solicitud de permiso.</param>
public sealed record PermisoRequest(string RegistroTurnoId, TimeOnly HoraInicio, TimeOnly HoraFin, string Motivo);

/// <summary>
/// Datos específicos para una solicitud de cambio de turno entre dos registros.
/// </summary>
/// <param name="TurnoOrigenId">Identificador del turno que el solicitante desea ceder o modificar.</param>
/// <param name="TurnoDestinoId">Identificador del turno con el que se desea realizar el cambio.</param>
/// <param name="Motivo">Justificación del cambio de turno solicitado.</param>
public sealed record CambioTurnoRequest(string TurnoOrigenId, string TurnoDestinoId, string Motivo);

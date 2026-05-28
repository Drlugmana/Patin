namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para registrar una calamidad doméstica que genera ausencia de una persona.
/// Permite especificar reemplazos para los turnos afectados durante el período de ausencia.
/// </summary>
/// <param name="PersonaId">Identificador de la persona afectada por la calamidad.</param>
/// <param name="TurnoId">
/// Identificador del turno específico afectado.
/// <see langword="null"/> si la calamidad abarca múltiples turnos en el rango de fechas.
/// </param>
/// <param name="FechaInicio">Fecha de inicio del período de ausencia por calamidad.</param>
/// <param name="FechaFin">Fecha de fin del período de ausencia por calamidad (inclusive).</param>
/// <param name="Motivo">Descripción o justificación de la calamidad.</param>
/// <param name="SinReemplazosConfirmado">
/// <see langword="true"/> si el usuario confirmó explícitamente que no habrá reemplazos
/// para los turnos afectados.
/// </param>
/// <param name="Reemplazos">
/// Lista de reemplazos asignados para los turnos afectados.
/// <see langword="null"/> si no se definen reemplazos.
/// </param>
public sealed record CalamidadCreateRequest(
    string PersonaId,
    string? TurnoId,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string Motivo,
    bool SinReemplazosConfirmado,
    IReadOnlyList<CalamidadReemplazoItem>? Reemplazos);

/// <summary>
/// Solicitud para guardar o actualizar los reemplazos asociados a una calamidad existente.
/// </summary>
/// <param name="SolicitudId">Identificador de la solicitud de calamidad a la que pertenecen los reemplazos.</param>
/// <param name="Items">Lista de ítems de reemplazo a registrar.</param>
public sealed record CalamidadReemplazoSaveRequest(
    string SolicitudId,
    IReadOnlyList<CalamidadReemplazoItem> Items);

/// <summary>
/// Representa un reemplazo individual para un turno afectado por una calamidad.
/// Asocia el turno del ausente con el turno y persona que lo reemplazarán.
/// </summary>
/// <param name="TurnoAusenteId">Identificador del turno que quedará sin cubrir por la persona ausente.</param>
/// <param name="TurnoReemplazoId">
/// Identificador del turno del reemplazante.
/// <see langword="null"/> si el reemplazo aún no tiene turno asignado.
/// </param>
/// <param name="PersonaReemplazoId">
/// Identificador de la persona que cubrirá el turno.
/// <see langword="null"/> si aún no se ha asignado reemplazante.
/// </param>
/// <param name="ModoReemplazo">
/// Modalidad del reemplazo (por ejemplo, <c>"turno_extra"</c>, <c>"intercambio"</c>).
/// <see langword="null"/> si no se especifica modo.
/// </param>
public sealed record CalamidadReemplazoItem(
    string TurnoAusenteId,
    string? TurnoReemplazoId,
    string? PersonaReemplazoId,
    string? ModoReemplazo);

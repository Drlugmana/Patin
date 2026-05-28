namespace TurneroTcs.ViewModels;

/// <summary>
/// Modelo de vista para un ítem de solicitud en la lista de solicitudes.
/// Contiene los datos pre-formateados para presentación directa en la vista,
/// incluyendo campos específicos por tipo de solicitud (permiso, calamidad, cambio de turno).
/// Los campos de fecha y hora son cadenas formateadas para la interfaz, no tipos de dato.
/// </summary>
public class SolicitudListItemViewModel
{
    /// <summary>Identificador único de la solicitud.</summary>
    public string SolicitudId { get; set; } = string.Empty;

    /// <summary>Identificador de la persona que realizó la solicitud.</summary>
    public string PersonaSolicitanteId { get; set; } = string.Empty;

    /// <summary>Indica si la solicitud pertenece a la persona actualmente autenticada.</summary>
    public bool IsOwnedByCurrentPersona { get; set; }

    /// <summary>Título descriptivo de la solicitud para mostrar en la cabecera del ítem.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Nombre completo del usuario que realizó la solicitud.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Nombre legible del tipo de solicitud (por ejemplo, <c>"Vacación"</c>, <c>"Permiso"</c>).</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>Nombre del equipo del solicitante.</summary>
    public string EquipoName { get; set; } = string.Empty;

    /// <summary>Nombre del grupo del solicitante.</summary>
    public string GrupoName { get; set; } = string.Empty;

    /// <summary>Mes de inicio de la solicitud formateado para presentación (por ejemplo, <c>"Ene"</c>).</summary>
    public string StartMonth { get; set; } = string.Empty;

    /// <summary>Día de inicio de la solicitud formateado (por ejemplo, <c>"15"</c>).</summary>
    public string StartDay { get; set; } = string.Empty;

    /// <summary>Mes de fin de la solicitud formateado para presentación.</summary>
    public string EndMonth { get; set; } = string.Empty;

    /// <summary>Día de fin de la solicitud formateado.</summary>
    public string EndDay { get; set; } = string.Empty;

    /// <summary>Número de días que abarca la solicitud, formateado como cadena.</summary>
    public string Days { get; set; } = string.Empty;

    /// <summary>Estado legible de la solicitud (por ejemplo, <c>"Pendiente"</c>, <c>"Aprobada"</c>).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Código interno del estado de la solicitud, usado para aplicar estilos en la vista.</summary>
    public string StatusCode { get; set; } = string.Empty;

    /// <summary>Nombre del primer aprobador que procesó la solicitud.</summary>
    public string Aprobador1Name { get; set; } = string.Empty;

    /// <summary>Fecha de aprobación o rechazo del primer aprobador, formateada para presentación.</summary>
    public string Aprobador1Date { get; set; } = string.Empty;

    /// <summary>Nombre del segundo aprobador que procesó la solicitud.</summary>
    public string Aprobador2Name { get; set; } = string.Empty;

    /// <summary>Fecha de aprobación o rechazo del segundo aprobador, formateada para presentación.</summary>
    public string Aprobador2Date { get; set; } = string.Empty;

    // Campos específicos de solicitudes de permiso de ausencia parcial

    /// <summary>Hora de inicio del permiso de ausencia, formateada para presentación. Vacío si no aplica.</summary>
    public string PermisoHoraInicio { get; set; } = string.Empty;

    /// <summary>Hora de fin del permiso de ausencia, formateada para presentación. Vacío si no aplica.</summary>
    public string PermisoHoraFin { get; set; } = string.Empty;

    /// <summary>Motivo del permiso de ausencia. Vacío si no aplica.</summary>
    public string PermisoMotivo { get; set; } = string.Empty;

    // Campos específicos de solicitudes de calamidad

    /// <summary>Motivo de la calamidad doméstica. Vacío si no aplica.</summary>
    public string CalamidadMotivo { get; set; } = string.Empty;

    // Campos específicos de solicitudes de cambio de turno

    /// <summary>Nombre del empleado cuyo turno origen se solicita cambiar. Vacío si no aplica.</summary>
    public string CambioOrigenNombre { get; set; } = string.Empty;

    /// <summary>Mes del turno origen, formateado. Vacío si no aplica.</summary>
    public string CambioOrigenMonth { get; set; } = string.Empty;

    /// <summary>Día del turno origen, formateado. Vacío si no aplica.</summary>
    public string CambioOrigenDay { get; set; } = string.Empty;

    /// <summary>Nombre del tipo de turno origen. Vacío si no aplica.</summary>
    public string CambioOrigenTurno { get; set; } = string.Empty;

    /// <summary>Nombre del empleado cuyo turno destino participará en el cambio. Vacío si no aplica.</summary>
    public string CambioDestinoNombre { get; set; } = string.Empty;

    /// <summary>Mes del turno destino, formateado. Vacío si no aplica.</summary>
    public string CambioDestinoMonth { get; set; } = string.Empty;

    /// <summary>Día del turno destino, formateado. Vacío si no aplica.</summary>
    public string CambioDestinoDay { get; set; } = string.Empty;

    /// <summary>Nombre del tipo de turno destino. Vacío si no aplica.</summary>
    public string CambioDestinoTurno { get; set; } = string.Empty;

    /// <summary>Motivo del cambio de turno solicitado. Vacío si no aplica.</summary>
    public string CambioMotivo { get; set; } = string.Empty;
}

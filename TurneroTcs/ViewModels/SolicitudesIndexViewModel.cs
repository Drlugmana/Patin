namespace TurneroTcs.ViewModels;

/// <summary>
/// Modelo de vista para la página de gestión de solicitudes (vacaciones, permisos, cambios de turno, calamidades).
/// Expone la lista de solicitudes, la solicitud actualmente seleccionada para detalle
/// y los permisos de aprobación/rechazo del usuario actual por tipo de solicitud.
/// </summary>
public class SolicitudesIndexViewModel
{
    /// <summary>Lista completa de solicitudes visibles para el usuario actual.</summary>
    public IReadOnlyList<SolicitudListItemViewModel> Items { get; set; } = Array.Empty<SolicitudListItemViewModel>();

    /// <summary>Solicitud seleccionada para mostrar en el panel de detalle; <see langword="null"/> si ninguna está seleccionada.</summary>
    public SolicitudListItemViewModel? Selected { get; set; }

    /// <summary>Identificador de la persona autenticada, usado para determinar si una solicitud es propia.</summary>
    public string CurrentPersonaId { get; set; } = string.Empty;

    /// <summary>Indica si el usuario tiene permiso para aprobar solicitudes de vacación.</summary>
    public bool CanApproveVacacion { get; set; }

    /// <summary>Indica si el usuario tiene permiso para rechazar solicitudes de vacación.</summary>
    public bool CanRejectVacacion { get; set; }

    /// <summary>Indica si el usuario tiene permiso para aprobar solicitudes de permiso de ausencia.</summary>
    public bool CanApprovePermiso { get; set; }

    /// <summary>Indica si el usuario tiene permiso para rechazar solicitudes de permiso de ausencia.</summary>
    public bool CanRejectPermiso { get; set; }

    /// <summary>Indica si el usuario tiene permiso para aprobar solicitudes de cambio de turno.</summary>
    public bool CanApproveCambioTurno { get; set; }

    /// <summary>Indica si el usuario tiene permiso para rechazar solicitudes de cambio de turno.</summary>
    public bool CanRejectCambioTurno { get; set; }
}

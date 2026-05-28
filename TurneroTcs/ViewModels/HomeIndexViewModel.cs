namespace TurneroTcs.ViewModels;

/// <summary>
/// Modelo de vista para el dashboard principal de la aplicacion.
/// Agrupa los datos del panel de administracion (solicitudes pendientes, impacto de vacaciones
/// y feriados proximos) y del panel de usuario regular.
/// </summary>
public class HomeIndexViewModel
{
    /// <summary>
    /// Indica si la vista debe mostrar el panel de administracion completo.
    /// Cuando es <see langword="false"/>, se muestra la vista reducida del usuario regular.
    /// </summary>
    public bool IsAdminDashboard { get; set; }

    /// <summary>Numero total de solicitudes pendientes de resolucion.</summary>
    public int PendingSolicitudesCount { get; set; }

    /// <summary>Lista de solicitudes pendientes mas recientes para mostrar en el dashboard.</summary>
    public List<HomePendingSolicitudItemViewModel> PendingSolicitudes { get; set; } = new();

    /// <summary>Fecha de inicio del periodo analizado para el impacto de vacaciones.</summary>
    public DateOnly VacacionesImpactStart { get; set; }

    /// <summary>Fecha de fin del periodo analizado para el impacto de vacaciones.</summary>
    public DateOnly VacacionesImpactEnd { get; set; }

    /// <summary>Resumen del impacto de vacaciones agrupado por equipo y grupo.</summary>
    public List<HomeVacacionesImpactItemViewModel> VacacionesImpacto { get; set; } = new();

    /// <summary>Dias con mayor concentracion de personas en vacaciones dentro del periodo analizado.</summary>
    public List<HomeVacacionesPeakDayItemViewModel> VacacionesPicos { get; set; } = new();

    /// <summary>Fecha de inicio de la ventana de feriados proximos mostrada en el dashboard.</summary>
    public DateOnly FeriadoWindowStart { get; set; }

    /// <summary>Fecha de fin de la ventana de feriados proximos mostrada en el dashboard.</summary>
    public DateOnly FeriadoWindowEnd { get; set; }

    /// <summary>Lista de feriados proximos dentro de la ventana de tiempo configurada.</summary>
    public List<HomeUpcomingFeriadoItemViewModel> FeriadosProximos { get; set; } = new();
}

/// <summary>
/// Elemento de solicitud pendiente para el panel de resumen del dashboard de administracion.
/// </summary>
public class HomePendingSolicitudItemViewModel
{
    /// <summary>Identificador de la solicitud.</summary>
    public string SolicitudId { get; set; } = string.Empty;

    /// <summary>Nombre del tipo de solicitud (vacacion, permiso, cambio de turno, calamidad).</summary>
    public string TipoSolicitud { get; set; } = string.Empty;

    /// <summary>Nombre completo de la persona que realizo la solicitud.</summary>
    public string Solicitante { get; set; } = string.Empty;

    /// <summary>Nombre del equipo al que pertenece el solicitante; <c>"-"</c> si no tiene equipo.</summary>
    public string Equipo { get; set; } = "-";

    /// <summary>Estado legible de la solicitud (por ejemplo, <c>"Pendiente"</c>, <c>"Aprobada"</c>).</summary>
    public string Estado { get; set; } = string.Empty;

    /// <summary>Fecha y hora en que fue creada la solicitud.</summary>
    public DateTime FechaSolicitud { get; set; }

    /// <summary>Numero de dias transcurridos desde la creacion de la solicitud sin resolucion.</summary>
    public int DiasAbierta { get; set; }
}

/// <summary>
/// Resumen del impacto de vacaciones por equipo y grupo durante un periodo de tiempo.
/// </summary>
public class HomeVacacionesImpactItemViewModel
{
    /// <summary>Nombre del equipo afectado; <c>"-"</c> si no aplica.</summary>
    public string Equipo { get; set; } = "-";

    /// <summary>Nombre del grupo afectado; <c>"-"</c> si no aplica.</summary>
    public string Grupo { get; set; } = "-";

    /// <summary>Numero de personas del grupo/equipo con vacaciones en el periodo.</summary>
    public int PersonasAfectadas { get; set; }

    /// <summary>Total de dias-persona de vacacion en el periodo (suma de dias por persona).</summary>
    public int DiasPersona { get; set; }

    /// <summary>Fecha en que mas personas del grupo estan simultaneamente en vacaciones; <see langword="null"/> si no aplica.</summary>
    public DateOnly? DiaPico { get; set; }

    /// <summary>Numero de personas en vacaciones en el dia pico.</summary>
    public int PersonasDiaPico { get; set; }
}

/// <summary>
/// Dia con mayor concentracion de personas en vacaciones dentro del periodo analizado.
/// </summary>
public class HomeVacacionesPeakDayItemViewModel
{
    /// <summary>Fecha del dia pico de vacaciones.</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Numero de personas en vacaciones en ese dia.</summary>
    public int PersonasAfectadas { get; set; }
}

/// <summary>
/// Feriado proximo dentro de la ventana de tiempo configurada en el dashboard.
/// </summary>
public class HomeUpcomingFeriadoItemViewModel
{
    /// <summary>Nombre del feriado.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Fecha de inicio del feriado.</summary>
    public DateOnly Inicio { get; set; }

    /// <summary>Fecha de fin del feriado.</summary>
    public DateOnly Fin { get; set; }

    /// <summary>Indica si el feriado esta actualmente en curso (la fecha actual esta entre inicio y fin).</summary>
    public bool EnCurso { get; set; }

    /// <summary>Numero de dias que dura el feriado (incluyendo inicio y fin).</summary>
    public int Dias { get; set; }
}

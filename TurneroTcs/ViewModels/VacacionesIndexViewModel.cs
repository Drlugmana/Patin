using System.ComponentModel.DataAnnotations;

namespace TurneroTcs.ViewModels;

/// <summary>
/// Modelo de vista para la página de gestión de vacaciones.
/// Combina el listado paginado de registros de vacaciones con los formularios
/// de carga individual y carga masiva embebidos en la misma vista.
/// </summary>
public class VacacionesIndexViewModel
{
    /// <summary>Indica si se debe mostrar la columna de equipo en la tabla de resultados.</summary>
    public bool ShowEquipoColumn { get; set; }

    /// <summary>Indica si se debe mostrar la columna de grupo en la tabla de resultados.</summary>
    public bool ShowGrupoColumn { get; set; }

    /// <summary>Indica si el usuario tiene permiso para crear nuevos registros de vacaciones.</summary>
    public bool CanCreate { get; set; }

    /// <summary>Texto de búsqueda libre; <see langword="null"/> si no se aplicó búsqueda.</summary>
    public string? Search { get; set; }

    /// <summary>Año seleccionado como filtro; <see langword="null"/> si no se filtra por año.</summary>
    public int? Year { get; set; }

    /// <summary>Mes seleccionado como filtro (1–12); <see langword="null"/> si no se filtra por mes.</summary>
    public int? Month { get; set; }

    /// <summary>Página actual de la paginación. Por defecto 1.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Número de ítems por página. Por defecto 15.</summary>
    public int PageSize { get; set; } = 15;

    /// <summary>Total de registros que coinciden con los filtros aplicados.</summary>
    public int TotalCount { get; set; }

    /// <summary>Total de páginas calculado a partir de <see cref="TotalCount"/> y <see cref="PageSize"/>.</summary>
    public int TotalPages { get; set; }

    /// <summary>Años disponibles para el filtro de año, ordenados descendentemente.</summary>
    public IReadOnlyList<int> YearOptions { get; set; } = Array.Empty<int>();

    /// <summary>Formulario de carga individual de vacaciones para una sola persona.</summary>
    public VacacionCargaViewModel Create { get; set; } = new();

    /// <summary>Formulario de carga masiva de vacaciones para múltiples personas a la vez.</summary>
    public VacacionCargaMasivaViewModel BulkCreate { get; set; } = new();

    /// <summary>Personas disponibles para seleccionar en los formularios de carga.</summary>
    public IReadOnlyList<VacacionPersonaOptionViewModel> PersonasDisponibles { get; set; } = Array.Empty<VacacionPersonaOptionViewModel>();

    /// <summary>Lista paginada de registros de vacaciones correspondientes a la página actual.</summary>
    public IReadOnlyList<VacacionListItemViewModel> Items { get; set; } = Array.Empty<VacacionListItemViewModel>();
}

/// <summary>
/// Elemento de la lista de vacaciones con los datos de presentación de un período de vacación.
/// </summary>
public class VacacionListItemViewModel
{
    /// <summary>Identificador único del registro de vacación.</summary>
    public string VacacionId { get; set; } = string.Empty;

    /// <summary>Identificador de la solicitud de vacación asociada.</summary>
    public string SolicitudId { get; set; } = string.Empty;

    /// <summary>Identificador de la persona a quien pertenece la vacación.</summary>
    public string PersonaId { get; set; } = string.Empty;

    /// <summary>Nombre completo de la persona.</summary>
    public string PersonaNombre { get; set; } = string.Empty;

    /// <summary>Nombre del equipo de la persona; <c>"-"</c> si no tiene equipo.</summary>
    public string EquipoNombre { get; set; } = "-";

    /// <summary>Nombre del grupo de la persona; <c>"-"</c> si no tiene grupo.</summary>
    public string GrupoNombre { get; set; } = "-";

    /// <summary>Fecha de inicio del período de vacaciones.</summary>
    public DateOnly FechaInicio { get; set; }

    /// <summary>Fecha de fin del período de vacaciones (inclusive).</summary>
    public DateOnly FechaFin { get; set; }

    /// <summary>Número de días del período de vacaciones.</summary>
    public int Dias { get; set; }

    /// <summary>Estado legible de la vacación (por ejemplo, <c>"Aprobada"</c>, <c>"Pendiente"</c>).</summary>
    public string Estado { get; set; } = string.Empty;

    /// <summary>Fecha y hora en que se realizó la solicitud de vacación.</summary>
    public DateTime FechaSolicitud { get; set; }

    /// <summary>Indica si el usuario actual tiene permiso para gestionar (editar/eliminar) este registro.</summary>
    public bool CanManage { get; set; }
}

/// <summary>
/// Formulario de carga individual de vacaciones para una sola persona y período.
/// </summary>
public class VacacionCargaViewModel
{
    /// <summary>Identificador de la persona a quien se asigna la vacación.</summary>
    [Required(ErrorMessage = "La persona es requerida.")]
    [Display(Name = "Persona")]
    public string PersonaId { get; set; } = string.Empty;

    /// <summary>Fecha de inicio del período de vacaciones.</summary>
    [Required(ErrorMessage = "La fecha inicio es requerida.")]
    [Display(Name = "Inicio")]
    public DateOnly? FechaInicio { get; set; }

    /// <summary>Fecha de fin del período de vacaciones (inclusive).</summary>
    [Required(ErrorMessage = "La fecha fin es requerida.")]
    [Display(Name = "Fin")]
    public DateOnly? FechaFin { get; set; }
}

/// <summary>
/// Opción de persona disponible para los selectores de los formularios de carga de vacaciones.
/// </summary>
public class VacacionPersonaOptionViewModel
{
    /// <summary>Identificador de la persona.</summary>
    public string PersonaId { get; set; } = string.Empty;

    /// <summary>Nombre de la persona formateado para mostrar en el selector (por ejemplo, nombre completo + Ultimatix).</summary>
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// Formulario de carga masiva de vacaciones que permite registrar períodos para múltiples personas a la vez.
/// </summary>
public class VacacionCargaMasivaViewModel
{
    /// <summary>Lista de ítems individuales de vacación a crear en la operación masiva.</summary>
    [Display(Name = "Personas")]
    public List<VacacionCargaItemViewModel> Items { get; set; } = new();
}

/// <summary>
/// Ítem individual dentro de una carga masiva de vacaciones.
/// </summary>
public class VacacionCargaItemViewModel
{
    /// <summary>Identificador de la persona a quien se asigna la vacación.</summary>
    [Required(ErrorMessage = "La persona es requerida.")]
    public string PersonaId { get; set; } = string.Empty;

    /// <summary>Fecha de inicio del período de vacaciones.</summary>
    [Required(ErrorMessage = "La fecha inicio es requerida.")]
    public DateOnly? FechaInicio { get; set; }

    /// <summary>Fecha de fin del período de vacaciones (inclusive).</summary>
    [Required(ErrorMessage = "La fecha fin es requerida.")]
    public DateOnly? FechaFin { get; set; }
}

/// <summary>
/// Formulario para editar las fechas de un registro de vacación existente.
/// </summary>
public class VacacionEditViewModel
{
    /// <summary>Identificador del registro de vacación a editar.</summary>
    [Required]
    public string VacacionId { get; set; } = string.Empty;

    /// <summary>Nueva fecha de inicio del período de vacaciones.</summary>
    [Required(ErrorMessage = "La fecha inicio es requerida.")]
    public DateOnly? FechaInicio { get; set; }

    /// <summary>Nueva fecha de fin del período de vacaciones (inclusive).</summary>
    [Required(ErrorMessage = "La fecha fin es requerida.")]
    public DateOnly? FechaFin { get; set; }
}

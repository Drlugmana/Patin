using Microsoft.AspNetCore.Mvc.Rendering;

namespace TurneroTcs.ViewModels;

/// <summary>
/// Modelo de vista para el reporte de horas trabajadas por persona en un período determinado.
/// Soporta filtros por equipo, grupo y persona, paginación, y desglosa las horas
/// por categoría (normales, extras, nocturnas, feriados, fin de semana).
/// </summary>
public class HorasMensualesViewModel
{
    /// <summary>Fecha de inicio del período en formato de cadena para los campos del formulario.</summary>
    public string FromDate { get; set; } = string.Empty;

    /// <summary>Fecha de fin del período en formato de cadena para los campos del formulario.</summary>
    public string ToDate { get; set; } = string.Empty;

    /// <summary>Fecha de inicio del período como valor de dominio.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Fecha de fin del período como valor de dominio.</summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>Etiqueta legible del período (por ejemplo, <c>"Enero 2025"</c> o <c>"01/01/2025 – 31/01/2025"</c>).</summary>
    public string PeriodLabel { get; set; } = string.Empty;

    /// <summary>Identificador del equipo seleccionado como filtro; <see langword="null"/> si no se filtra.</summary>
    public string? EquipoId { get; set; }

    /// <summary>Identificador del grupo seleccionado como filtro; <see langword="null"/> si no se filtra.</summary>
    public string? GrupoId { get; set; }

    /// <summary>Identificador de la persona seleccionada como filtro; <see langword="null"/> si no se filtra.</summary>
    public string? PersonaId { get; set; }

    /// <summary>Indica si se deben mostrar los filtros avanzados (grupo, persona) en la vista.</summary>
    public bool ShowAdvancedFilters { get; set; }

    /// <summary>Página actual de la paginación. Por defecto 1.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Número de ítems por página. Por defecto 15.</summary>
    public int PageSize { get; set; } = 15;

    /// <summary>Total de registros que coinciden con los filtros aplicados.</summary>
    public int TotalCount { get; set; }

    /// <summary>Total de páginas calculado a partir de <see cref="TotalCount"/> y <see cref="PageSize"/>.</summary>
    public int TotalPages { get; set; }

    /// <summary>Lista de equipos disponibles para el filtro.</summary>
    public IEnumerable<SelectListItem> Equipos { get; set; } = Enumerable.Empty<SelectListItem>();

    /// <summary>Lista de grupos disponibles para el filtro avanzado.</summary>
    public IEnumerable<SelectListItem> Grupos { get; set; } = Enumerable.Empty<SelectListItem>();

    /// <summary>Lista de personas disponibles para el filtro avanzado.</summary>
    public IEnumerable<SelectListItem> Personas { get; set; } = Enumerable.Empty<SelectListItem>();

    /// <summary>Ítems de la página actual con el desglose de horas por persona.</summary>
    public List<PersonaHorasMensualesItem> Items { get; set; } = new();
}

/// <summary>
/// Desglose de horas trabajadas por una persona en el período analizado,
/// segmentado por categoría de hora y con detalle semanal.
/// </summary>
public class PersonaHorasMensualesItem
{
    /// <summary>Identificador de la persona.</summary>
    public string PersonaId { get; set; } = string.Empty;

    /// <summary>Nombre completo de la persona.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Nombre del equipo de la persona; <c>"-"</c> si no tiene equipo.</summary>
    public string Equipo { get; set; } = "-";

    /// <summary>Nombre del grupo de la persona; <c>"-"</c> si no tiene grupo.</summary>
    public string Grupo { get; set; } = "-";

    /// <summary>Total de horas trabajadas en el período (normales + extras + nocturnas + feriados).</summary>
    public double HorasTotales { get; set; }

    /// <summary>Horas trabajadas en horario normal (no nocturno, no feriado, no fin de semana).</summary>
    public double HorasNormales { get; set; }

    /// <summary>Horas extras trabajadas en el período.</summary>
    public double HorasExtras { get; set; }

    /// <summary>Horas trabajadas en días feriados laborables.</summary>
    public double HorasFeriadoTrabajadas { get; set; }

    /// <summary>Total de horas trabajadas en ventana nocturna (18:00–06:00).</summary>
    public double HorasNocturnas { get; set; }

    /// <summary>Número de turnos clasificados como nocturnos (más del 70 % en ventana nocturna).</summary>
    public int TurnosNocturnos { get; set; }

    /// <summary>Horas trabajadas en la ventana nocturna de 20:00 a 06:00.</summary>
    public double HorasNocturnas20a6 { get; set; }

    /// <summary>Versión redondeada de <see cref="HorasNocturnas20a6"/> para mostrar en tabla sin decimales.</summary>
    public int HorasNocturnas20a6Display => (int)Math.Round(HorasNocturnas20a6, MidpointRounding.AwayFromZero);

    /// <summary>Porcentaje de horas de fin de semana sobre el total de horas trabajadas.</summary>
    public double PorcentajeFinSemana { get; set; }

    /// <summary>Horas trabajadas en sábado o domingo.</summary>
    public double HorasFinSemana { get; set; }

    /// <summary>Porcentaje de horas nocturnas sobre el total de horas trabajadas.</summary>
    public double PorcentajeNocturnas { get; set; }

    /// <summary>Desglose de horas por semana dentro del período analizado.</summary>
    public List<SemanaHorasItem> Semanas { get; set; } = new();
}

/// <summary>
/// Desglose de horas trabajadas en una semana específica dentro del período analizado.
/// </summary>
public class SemanaHorasItem
{
    /// <summary>Fecha del lunes de la semana.</summary>
    public DateOnly WeekStart { get; set; }

    /// <summary>Fecha del domingo de la semana.</summary>
    public DateOnly WeekEnd { get; set; }

    /// <summary>Total de horas trabajadas en la semana.</summary>
    public double HorasTotales { get; set; }

    /// <summary>Horas normales trabajadas en la semana.</summary>
    public double HorasNormales { get; set; }

    /// <summary>Horas extras trabajadas en la semana.</summary>
    public double HorasExtras { get; set; }
}

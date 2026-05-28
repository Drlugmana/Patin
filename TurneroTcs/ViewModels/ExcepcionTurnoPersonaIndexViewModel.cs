using TurneroTcs.Models;

namespace TurneroTcs.ViewModels;

/// <summary>
/// Modelo de vista para la administracion inline de excepciones de turnos por equipo.
/// </summary>
public class ExcepcionTurnoPersonaIndexViewModel
{
    public string EquipoId { get; set; } = string.Empty;

    public string? NombreEquipo { get; set; }

    public IReadOnlyList<ExcepcionTurnoPersona> Excepciones { get; set; } = Array.Empty<ExcepcionTurnoPersona>();

    public IReadOnlyList<Persona> Personas { get; set; } = Array.Empty<Persona>();

    public IReadOnlyList<TipoTurno> TipoTurnos { get; set; } = Array.Empty<TipoTurno>();

    public ExcepcionTurnoPersonaCreateViewModel Create { get; set; } = new();
}

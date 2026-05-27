using TurneroTcs.Models;

namespace TurneroTcs.ViewModels;

/// <summary>
/// Modelo de vista para la página de gestión de feriados.
/// Combina el listado de feriados existentes con el formulario de creación embebido en la misma vista.
/// </summary>
public class FeriadoIndexViewModel
{
    /// <summary>Lista de feriados registrados en el sistema.</summary>
    public IReadOnlyList<Feriado> Feriados { get; set; } = Array.Empty<Feriado>();

    /// <summary>Datos del formulario de creación de un nuevo feriado, compartido en la misma vista.</summary>
    public FeriadoCrearViewModel Create { get; set; } = new();
}

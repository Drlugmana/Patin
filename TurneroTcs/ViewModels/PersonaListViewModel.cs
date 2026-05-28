using System.Linq;

namespace TurneroTcs.ViewModels;

/// <summary>
/// Modelo de vista que representa una persona en la lista de personas del sistema.
/// Contiene los datos de presentación necesarios para la tabla de listado y las acciones disponibles.
/// </summary>
public class PersonaListViewModel
{
    /// <summary>Identificador único de la persona en el sistema.</summary>
    public string PersonaId { get; set; } = null!;

    /// <summary>Identificador del usuario de ASP.NET Identity asociado a la persona.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Primer nombre de la persona.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Segundo nombre de la persona; <see langword="null"/> si no fue registrado.</summary>
    public string? SegundoNombre { get; set; }

    /// <summary>Apellido paterno de la persona.</summary>
    public string Apellido { get; set; } = string.Empty;

    /// <summary>Apellido materno de la persona; <see langword="null"/> si no fue registrado.</summary>
    public string? SegundoApellido { get; set; }

    /// <summary>
    /// Nombre completo calculado concatenando los campos de nombre y apellido no vacíos,
    /// en el orden: nombre, segundo nombre, apellido, segundo apellido.
    /// </summary>
    public string NombreCompleto =>
        string.Join(" ", new[] { Nombre, SegundoNombre, Apellido, SegundoApellido }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

    /// <summary>Número Ultimatix del empleado (identificador corporativo de 7 dígitos).</summary>
    public string Ultimatix { get; set; } = string.Empty;

    /// <summary>Nombre del rol de sistema asignado al usuario; <see langword="null"/> si no tiene rol.</summary>
    public string? NombreRol { get; set; }

    /// <summary>Nombre del equipo al que pertenece la persona; <see langword="null"/> si no tiene equipo asignado.</summary>
    public string? NombreEquipo { get; set; }

    /// <summary>Color hexadecimal de identificación visual en la interfaz de calendario; <see langword="null"/> si no fue configurado.</summary>
    public string? ColorUsuario { get; set; }

    /// <summary>Identificador del equipo al que pertenece la persona; <see langword="null"/> si no tiene equipo.</summary>
    public string? EquipoId { get; set; }

    /// <summary>Nombres de los grupos primarios de la persona, concatenados para presentación en tabla.</summary>
    public string? GruposNombres { get; set; }

    /// <summary>Identificadores de los grupos primarios de la persona.</summary>
    public IReadOnlyList<string> GrupoIds { get; set; } = Array.Empty<string>();

    /// <summary>Nombres de los grupos secundarios de la persona, concatenados para presentación en tabla.</summary>
    public string? GruposSecundariosNombres { get; set; }

    /// <summary>Identificadores de los grupos secundarios de la persona.</summary>
    public IReadOnlyList<string> GrupoIdsSecundarios { get; set; } = Array.Empty<string>();

    /// <summary>Fecha y hora UTC en la que fue creado el registro de la persona.</summary>
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    /// <summary>Indica si la persona está activa y puede ser asignada a turnos.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Indica si el registro de la persona ha sido eliminado lógicamente.</summary>
    public bool Borrado { get; set; }

    /// <summary>Nombre del usuario que realizó la eliminación lógica; <see langword="null"/> si no fue borrada.</summary>
    public string? BorradoPor { get; set; }

    /// <summary>Fecha y hora UTC en que fue eliminada lógicamente; <see langword="null"/> si no fue borrada.</summary>
    public DateTime? BorradoEn { get; set; }
}

using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Tabla de unión que habilita un <see cref="TipoTurno"/> para un <see cref="Equipo"/> específico.
/// Solo los tipos de turno vinculados a un equipo pueden ser usados en su planificación.
/// </summary>
[Table("equipo_tipo_turno")]
public class EquipoTipoTurno
{
    /// <summary>Identificador del <see cref="Equipo"/> que habilita el tipo de turno.</summary>
    [Column("equipo_id")]
    public string EquipoId { get; set; } = null!;

    /// <summary>Identificador del <see cref="TipoTurno"/> habilitado para el equipo.</summary>
    [Column("tipo_turno_id")]
    public string TipoTurnoId { get; set; } = null!;

    /// <summary>Equipo que tiene habilitado este tipo de turno.</summary>
    public Equipo? Equipo { get; set; }

    /// <summary>Tipo de turno habilitado para el equipo.</summary>
    public TipoTurno? TipoTurno { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Configura cuántas personas de apoyo externas puede recibir un <see cref="Grupo"/>
/// en un día y tipo de turno determinados.
/// El personal de apoyo proviene de otros grupos del mismo equipo para cubrir
/// necesidades puntuales sin alterar la rotación base.
/// </summary>
[Table("planificacion_apoyo_grupo")]
public class PlanificacionApoyoGrupo
{
    /// <summary>Identificador único de la configuración de apoyo.</summary>
    [Key]
    [Required]
    [Column("planificacion_apoyo_grupo_id", TypeName = "varchar(12)")]
    public string PlanificacionApoyoGrupoId { get; set; } = null!;

    /// <summary>Identificador del <see cref="Grupo"/> receptor del apoyo.</summary>
    [Required]
    [Column("grupo_id")]
    public string GrupoId { get; set; } = null!;

    /// <summary>Grupo que recibe el personal de apoyo.</summary>
    [ForeignKey(nameof(GrupoId))]
    public Grupo? Grupo { get; set; }

    /// <summary>
    /// Día de la semana en que aplica el apoyo (ej. <c>"Lunes"</c>, <c>"Domingo"</c>).
    /// </summary>
    [Required]
    [Column("dia")]
    public string Dia { get; set; } = null!;

    /// <summary>Identificador del <see cref="TipoTurno"/> en el que se requiere el apoyo.</summary>
    [Required]
    [Column("tipo_turno_id")]
    public string TipoTurnoId { get; set; } = null!;

    /// <summary>Tipo de turno en el que se recibe el personal de apoyo.</summary>
    [ForeignKey(nameof(TipoTurnoId))]
    public TipoTurno? TipoTurno { get; set; }

    /// <summary>Número de personas de apoyo externas que puede recibir el grupo en este slot.</summary>
    [Required]
    [Column("cantidad_apoyo")]
    public int CantidadApoyo { get; set; }
}

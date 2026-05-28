using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Unidad organizativa de trabajo que agrupa personas, grupos y configuraciones de planificación.
/// Es el nivel más alto de la jerarquía de turnos: un equipo tiene varios grupos,
/// cada uno con su propia plantilla de rotación.
/// </summary>
[Table("equipo")]
public class Equipo
{
    /// <summary>Identificador único del equipo.</summary>
    [Key]
    [Required]
    public string EquipoId { get; set; } = null!;

    /// <summary>Nombre del equipo. Máximo 50 caracteres.</summary>
    [Required]
    [Column("nombre_equipo", TypeName = "varchar(50)")]
    public string NombreEquipo { get; set; } = string.Empty;

    /// <summary>
    /// Indica si el equipo está operativo.
    /// Los equipos inactivos no participan en la generación de turnos.
    /// </summary>
    [Required]
    [Column("activo")]
    public bool Activo { get; set; } = true;

    /// <summary>Fecha y hora en que se creó el registro del equipo (UTC).</summary>
    [Required]
    [Column("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Algoritmo de generación de turnos utilizado por este equipo.
    /// Valor por defecto: <c>"Rotacion"</c>.
    /// </summary>
    [Required]
    [Column("tipo_generacion", TypeName = "varchar(20)")]
    public string TipoGeneracion { get; set; } = "Rotacion";

    /// <summary>Personas que pertenecen a este equipo.</summary>
    public ICollection<Persona> Personas { get; set; } = new List<Persona>();

    /// <summary>Grupos de trabajo que componen este equipo.</summary>
    public ICollection<Grupo> Grupos { get; set; } = new List<Grupo>();

    /// <summary>Tipos de turno habilitados para este equipo.</summary>
    public ICollection<EquipoTipoTurno> EquipoTipoTurnos { get; set; } = new List<EquipoTipoTurno>();

    /// <summary>Configuraciones de cobertura mínima para días feriados.</summary>
    public ICollection<FeriadoCoberturaConfig> FeriadoCoberturaConfigs { get; set; } = new List<FeriadoCoberturaConfig>();

    /// <summary>Configuraciones de límites de planificación (nocturnos, fines de semana, etc.).</summary>
    public ICollection<EquipoPlanificacionConfig> PlanificacionConfigs { get; set; } = new List<EquipoPlanificacionConfig>();

    /// <summary>Planificaciones auxiliares definidas a nivel de equipo.</summary>
    public ICollection<PlanificacionAuxiliarEquipo> PlanificacionesAuxiliaresEquipo { get; set; } = new List<PlanificacionAuxiliarEquipo>();
}

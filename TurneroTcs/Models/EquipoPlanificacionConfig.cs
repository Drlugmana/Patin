using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Configuración de límites de planificación para un <see cref="Equipo"/>.
/// Establece restricciones opcionales sobre la cantidad máxima de turnos nocturnos
/// y slots de fin de semana que puede acumular un empleado del equipo en un mes.
/// </summary>
[Table("equipo_planificacion_config")]
public class EquipoPlanificacionConfig
{
    /// <summary>Identificador único de la configuración de planificación del equipo.</summary>
    [Key]
    [Required]
    [Column("equipo_planificacion_config_id", TypeName = "varchar(12)")]
    public string EquipoPlanificacionConfigId { get; set; } = null!;

    /// <summary>Identificador del <see cref="Equipo"/> al que aplica esta configuración.</summary>
    [Required]
    [Column("equipo_id")]
    public string EquipoId { get; set; } = null!;

    /// <summary>Equipo al que aplica esta configuración.</summary>
    [ForeignKey(nameof(EquipoId))]
    public Equipo? Equipo { get; set; }

    /// <summary>
    /// Número máximo de slots de fin de semana que puede acumular un empleado por mes.
    /// <see langword="null"/> indica que no se aplica límite.
    /// </summary>
    [Column("maximo_slots_fin_semana_por_mes")]
    public int? MaximoSlotsFinSemanaPorMes { get; set; }

    /// <summary>
    /// Número máximo de turnos nocturnos que puede acumular un empleado por mes.
    /// <see langword="null"/> indica que no se aplica límite.
    /// </summary>
    [Column("maximo_turnos_nocturnos_por_mes")]
    public int? MaximoTurnosNocturnosPorMes { get; set; }

    /// <summary>
    /// Numero maximo de turnos nocturnos que puede acumular un empleado por semana.
    /// <see langword="null"/> indica que no se aplica limite.
    /// </summary>
    [Column("maximo_turnos_nocturnos_por_semana")]
    public int? MaximoTurnosNocturnosPorSemana { get; set; }
}

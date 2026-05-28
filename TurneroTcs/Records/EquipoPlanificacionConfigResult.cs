namespace TurneroTcs.Records;

/// <summary>
/// Contiene los limites de configuracion de planificacion para un equipo.
/// Se utiliza para validar y controlar la asignacion de turnos dentro de un periodo mensual y semanal.
/// </summary>
/// <param name="MaximoSlotsFinSemanaPorMes">Maximo de slots de fin de semana que puede tener un miembro del equipo por mes.</param>
/// <param name="MaximoTurnosNocturnosPorMes">Maximo de turnos nocturnos asignables a un miembro del equipo por mes.</param>
/// <param name="MaximoTurnosNocturnosPorSemana">Maximo de turnos nocturnos asignables a un miembro del equipo por semana.</param>
/// <param name="NocturnosConsecutivos">Indica si los nocturnos pueden agruparse en secuencias de hasta 3 consecutivos seguidos de descanso.</param>
public sealed record EquipoPlanificacionConfigResult(
    int MaximoSlotsFinSemanaPorMes,
    int MaximoTurnosNocturnosPorMes,
    int MaximoTurnosNocturnosPorSemana,
    bool NocturnosConsecutivos = false);

using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Cuando la opcion de nocturnos consecutivos esta habilitada, limita las rachas de turnos
/// nocturnos consecutivos: un empleado puede acumular como maximo N noches consecutivas de
/// calendario (donde N = MaximoTurnosNocturnosPorSemana configurado), tras lo cual debe
/// descansar al menos un dia (sin turno nocturno) antes de poder reiniciar la racha.
/// Reemplaza completamente la restriccion de maximo N noches por semana.
/// </summary>
public static class RestriccionNocturnasConsecutivas
{
    /// <summary>
    /// Registra en el modelo la restriccion de maximo N noches consecutivas por empleado,
    /// donde N proviene de <see cref="Domain.PoliticasConfigurablesEquipo.MaximoTurnosNocturnosPorSemana"/>.
    /// Solo se aplica cuando <see cref="Domain.PoliticasConfigurablesEquipo.NocturnosConsecutivos"/> es <see langword="true"/>.
    /// </summary>
    public static void Aplicar(ContextoModeloCp contexto)
    {
        if (!contexto.Problema.Reglas.Configurables.NocturnosConsecutivos)
        {
            return;
        }

        var maxConsecutivas = contexto.Problema.Reglas.Configurables.MaximoTurnosNocturnosPorSemana;
        if (maxConsecutivas is null or <= 0)
        {
            return;
        }

        // Ventana deslizante de (N+1) dias: si la suma supera N, habria (N+1) consecutivas.
        var ventana = maxConsecutivas.Value + 1;

        // Reune todos los slots nocturnos e indexa por fecha de calendario
        var slotsPorFecha = contexto.Problema.Slots
            .Where(slot => slot.EsTurnoNocturno)
            .GroupBy(slot => slot.Fecha)
            .ToDictionary(g => g.Key, g => g.ToList());

        if (slotsPorFecha.Count < ventana)
        {
            return;
        }

        var fechasOrdenadas = slotsPorFecha.Keys.OrderBy(f => f).ToList();

        foreach (var empleado in contexto.Problema.Empleados)
        {
            // Para cada ventana de (N+1) dias calendario consecutivos con slots nocturnos,
            // la suma de asignaciones nocturnas no puede superar N.
            for (int i = 0; i <= fechasOrdenadas.Count - ventana; i++)
            {
                // Verificar que los (N+1) dias son estrictamente consecutivos en el calendario
                bool sonConsecutivos = true;
                for (int j = 1; j < ventana; j++)
                {
                    if (fechasOrdenadas[i + j] != fechasOrdenadas[i].AddDays(j))
                    {
                        sonConsecutivos = false;
                        break;
                    }
                }

                if (!sonConsecutivos)
                {
                    continue;
                }

                var varsVentana = new List<BoolVar>();

                for (int j = 0; j < ventana; j++)
                {
                    foreach (var slot in slotsPorFecha[fechasOrdenadas[i + j]])
                    {
                        varsVentana.Add(contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id));
                    }
                }

                if (varsVentana.Count >= ventana)
                {
                    contexto.Modelo.Add(LinearExpr.Sum(varsVentana) <= maxConsecutivas.Value);
                }
            }
        }
    }
}

using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Objectives;

/// <summary>
/// Ensambla la función objetivo multicriterio del modelo de optimización de rotación.
/// <para>
/// Reúne las penalizaciones de todos los objetivos individuales y construye una suma ponderada
/// que el motor minimiza. Los pesos reflejan la prioridad relativa de cada criterio:
/// <list type="bullet">
///   <item>Balanceo de horas totales (peso 16)</item>
///   <item>Balanceo de turnos nocturnos (peso 22)</item>
///   <item>Balanceo de turnos de fin de semana (peso 22)</item>
///   <item>Balanceo de carga en feriados (peso 18)</item>
///   <item>Balanceo de recargos compuestos nocturno+feriado+fin de semana (peso 30)</item>
///   <item>Cobertura prioritaria de vacaciones (peso 1)</item>
///   <item>Evitar turnos opcionales de vacación innecesarios (peso 12 000)</item>
///   <item>Minimizar descansos de exactamente 7 horas (peso 250, solo si está habilitado)</item>
///   <item>Rotación equitativa de grupos especiales (peso 600)</item>
///   <item>Penalización de asignaciones auxiliares (peso 10 000)</item>
/// </list>
/// Los objetivos individuales devuelven <see langword="null"/> cuando no aplican al problema actual,
/// por lo que la función objetivo solo incluye los criterios relevantes.
/// </para>
/// </summary>
public static class ConstructorObjetivoRotacion
{
    /// <summary>
    /// Registra en el modelo la función objetivo de minimización ponderada de penalizaciones.
    /// Si ningún objetivo aplica al problema, no se registra ninguna función objetivo.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema y las variables de decisión.</param>
    public static void Aplicar(ContextoModeloCp contexto)
    {
        var variablesObjetivo = new List<IntVar>();
        var pesos = new List<long>();

        if (ObjetivoBalanceHorasTotales.CrearPenalizacion(contexto) is IntVar penalizacionHorasTotales)
        {
            variablesObjetivo.Add(penalizacionHorasTotales);
            pesos.Add(16);
        }

        if (ObjetivoBalanceNocturno.CrearPenalizacion(contexto) is IntVar penalizacionNocturnos)
        {
            variablesObjetivo.Add(penalizacionNocturnos);
            pesos.Add(22);
        }

        if (ObjetivoBalanceFinDeSemana.CrearPenalizacion(contexto) is IntVar penalizacionFinDeSemana)
        {
            variablesObjetivo.Add(penalizacionFinDeSemana);
            pesos.Add(22);
        }

        if (ObjetivoBalanceFeriados.CrearPenalizacion(contexto) is IntVar penalizacionFeriados)
        {
            variablesObjetivo.Add(penalizacionFeriados);
            pesos.Add(18);
        }

        if (ObjetivoBalanceRecargosCompuestos.CrearPenalizacion(contexto) is IntVar penalizacionRecargosCompuestos)
        {
            variablesObjetivo.Add(penalizacionRecargosCompuestos);
            pesos.Add(30);
        }

        if (ObjetivoCoberturaPrioritariaVacaciones.CrearPenalizacion(contexto) is IntVar penalizacionCoberturaVacaciones)
        {
            variablesObjetivo.Add(penalizacionCoberturaVacaciones);
            pesos.Add(1);
        }

        if (ObjetivoEvitarTurnosOpcionalesVacacion.CrearPenalizacion(contexto) is IntVar penalizacionTurnosOpcionalesVacacion)
        {
            variablesObjetivo.Add(penalizacionTurnosOpcionalesVacacion);
            pesos.Add(12_000);
        }

        if (ObjetivoMinimizarDescansos7Horas.CrearPenalizacion(contexto) is IntVar penalizacionDescansos7Horas)
        {
            variablesObjetivo.Add(penalizacionDescansos7Horas);
            pesos.Add(250);
        }

        if (ObjetivoEvitarFinesSemanaConsecutivos.CrearPenalizacion(contexto) is IntVar penalizacionFinesSemanaConsecutivos)
        {
            variablesObjetivo.Add(penalizacionFinesSemanaConsecutivos);
            pesos.Add(ResolverPesoFinesSemanaConsecutivos(contexto));
        }

        if (ObjetivoRotacionGruposEspeciales.CrearPenalizacion(contexto) is IntVar penalizacionGruposEspeciales)
        {
            variablesObjetivo.Add(penalizacionGruposEspeciales);
            pesos.Add(600);
        }

        if (ObjetivoPenalizarSecundarios.CrearPenalizacion(contexto) is IntVar penalizacionSecundarios)
        {
            variablesObjetivo.Add(penalizacionSecundarios);
            // Penalización extremadamente alta: usar secundarios debe ser último recurso (casi prohibido)
            pesos.Add(10_000_000);
        }

        if (ObjetivoPenalizarConcentracionSecundarios.CrearPenalizacion(contexto) is IntVar penalizacionConcentracion)
        {
            variablesObjetivo.Add(penalizacionConcentracion);
            // Penalización adicional para concentración: saturation in one week is heavily penalized
            pesos.Add(1_000);
        }

        var variablesAuxiliares = contexto.Problema.Slots
            .Where(slot => slot.EsAuxiliar)
            .SelectMany(slot => contexto.Problema.Empleados.Select(empleado => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id)))
            .ToArray();

        if (variablesAuxiliares.Length > 0)
        {
            var penalizacionAuxiliares = contexto.Modelo.NewIntVar(0, variablesAuxiliares.Length, "penalizacion_auxiliares");
            contexto.Modelo.Add(penalizacionAuxiliares == LinearExpr.Sum(variablesAuxiliares));
            variablesObjetivo.Add(penalizacionAuxiliares);
            pesos.Add(10000);
        }

        if (variablesObjetivo.Count == 0)
        {
            return;
        }

        contexto.Modelo.Minimize(LinearExpr.WeightedSum(variablesObjetivo.ToArray(), pesos.ToArray()));
    }

    private static long ResolverPesoFinesSemanaConsecutivos(ContextoModeloCp contexto)
    {
        return contexto.Problema.Reglas.Configurables.NivelEvitarFinesSemanaConsecutivos switch
        {
            Domain.NivelEvitarFinesSemanaConsecutivos.Alto => 3600,
            Domain.NivelEvitarFinesSemanaConsecutivos.Medio => 1400,
            Domain.NivelEvitarFinesSemanaConsecutivos.Bajo => 450,
            _ => 0
        };
    }
}

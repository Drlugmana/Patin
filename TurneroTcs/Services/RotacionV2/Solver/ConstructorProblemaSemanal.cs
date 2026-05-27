using TurneroTcs.Services.RotacionV2.Domain;

namespace TurneroTcs.Services.RotacionV2.Solver;

/// <summary>
/// Crea subproblemas acotados a una ventana de una o más semanas a partir del problema de rotación completo.
/// Permite que el resolvedor secuencial trate cada semana (o par de semanas) como un problema independiente,
/// reduciendo el tamaño del espacio de búsqueda por iteración.
/// </summary>
public static class ConstructorProblemaSemanal
{
    /// <summary>
    /// Crea un subproblema acotado a una única semana.
    /// </summary>
    /// <param name="problemaBase">Problema de rotación completo del horizonte.</param>
    /// <param name="indiceSemana">Índice de la semana a extraer (base cero).</param>
    /// <returns>Subproblema de rotación con los slots, ausencias y feriados filtrados a esa semana.</returns>
    public static ProblemaRotacion Crear(ProblemaRotacion problemaBase, int indiceSemana)
    {
        return CrearVentana(problemaBase, indiceSemana, 1);
    }

    /// <summary>
    /// Crea un subproblema acotado a una ventana de varias semanas consecutivas.
    /// Se usa cuando hay vacaciones activas en semanas adyacentes y conviene resolver más de una semana a la vez
    /// para mejorar la calidad de la cobertura.
    /// </summary>
    /// <param name="problemaBase">Problema de rotación completo del horizonte.</param>
    /// <param name="indiceSemanaInicio">Índice de la primera semana de la ventana (base cero).</param>
    /// <param name="cantidadSemanasVentana">Número de semanas que abarca la ventana.</param>
    /// <returns>
    /// Subproblema de rotación con los slots y ausencias filtrados a la ventana,
    /// y con los índices de semana reindexados desde cero para el motor de optimización.
    /// </returns>
    public static ProblemaRotacion CrearVentana(ProblemaRotacion problemaBase, int indiceSemanaInicio, int cantidadSemanasVentana)
    {
        var semanasVentana = Math.Max(1, Math.Min(cantidadSemanasVentana, problemaBase.CantidadSemanas - indiceSemanaInicio));
        var fechaInicioSemana = problemaBase.FechaInicio.AddDays(indiceSemanaInicio * 7);
        var fechaFinExclusiva = fechaInicioSemana.AddDays(semanasVentana * 7);

        var slotsSemana = problemaBase.Slots
            .Where(slot => slot.Fecha >= fechaInicioSemana && slot.Fecha < fechaFinExclusiva)
            .Select(slot => slot with { IndiceSemana = slot.IndiceSemana - indiceSemanaInicio })
            .ToList();

        var ausenciasSemana = problemaBase.Ausencias
            .Select(ausencia => new AusenciaEmpleado
            {
                EmpleadoId = ausencia.EmpleadoId,
                Motivo = ausencia.Motivo,
                Fechas = ausencia.Fechas
                    .Where(fecha => fecha >= fechaInicioSemana && fecha < fechaFinExclusiva)
                    .ToHashSet()
            })
            .Where(ausencia => ausencia.Fechas.Count > 0)
            .ToList();
        var descansosPosterioresVacacionSemana = problemaBase.DescansosPosterioresVacacion
            .Where(descanso =>
                descanso.FechaRegreso >= fechaInicioSemana &&
                descanso.FechaRegreso < fechaFinExclusiva)
            .ToList();

        var excepcionesSemana = problemaBase.Excepciones
            .Where(excepcion =>
                excepcion.FechaFin >= fechaInicioSemana &&
                excepcion.FechaInicio < fechaFinExclusiva)
            .ToList();

        return new ProblemaRotacion
        {
            ProblemaId = $"{problemaBase.ProblemaId}:ventana:{indiceSemanaInicio}:{semanasVentana}",
            FechaInicio = fechaInicioSemana,
            CantidadSemanas = semanasVentana,
            Empleados = problemaBase.Empleados,
            Grupos = problemaBase.Grupos,
            Slots = slotsSemana,
            Ausencias = ausenciasSemana,
            Excepciones = excepcionesSemana,
            DescansosPosterioresVacacion = descansosPosterioresVacacionSemana,
            Feriados = problemaBase.Feriados
                .Where(fecha => fecha >= fechaInicioSemana && fecha < fechaFinExclusiva)
                .ToHashSet(),
            Reglas = problemaBase.Reglas
        };
    }
}

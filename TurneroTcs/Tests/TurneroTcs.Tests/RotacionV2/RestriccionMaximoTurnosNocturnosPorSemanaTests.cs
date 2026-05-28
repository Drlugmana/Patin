using TurneroTcs.Services.RotacionV2.Application;
using TurneroTcs.Services.RotacionV2.Domain;
using Xunit;

namespace TurneroTcs.Tests.RotacionV2;

public sealed class RestriccionMaximoTurnosNocturnosPorSemanaTests
{
    private static readonly DateTime FechaInicioLunes = new(2026, 4, 6, 0, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void Resolver_DebeRespetarMaximoNocturnosPorSemana()
    {
        var servicio = new ServicioRotacion();
        var plantilla = CrearPlantillaNocturna();
        var reglas = CrearReglas(maximoNocturnosPorSemana: 2);

        var problema = servicio.CrearProblema(
            plantilla,
            cantidadSemanas: 1,
            fechaInicio: FechaInicioLunes,
            reglas: reglas);
        var solucion = servicio.Resolver(
            plantilla,
            cantidadSemanas: 1,
            fechaInicio: FechaInicioLunes,
            reglas: reglas,
            opcionesSolver: new OpcionesSolverRotacion
            {
                TiempoMaximoResolucion = TimeSpan.FromSeconds(10),
                CantidadWorkers = 1
            });

        Assert.True(solucion.Estado is EstadoSolucionRotacion.Optima or EstadoSolucionRotacion.Factible);

        var slotsPorId = problema.Slots.ToDictionary(slot => slot.Id);
        var maximoAsignado = solucion.Asignaciones
            .Where(asignacion => slotsPorId[asignacion.IdSlot].EsTurnoNocturno)
            .GroupBy(asignacion => new
            {
                asignacion.EmpleadoId,
                slotsPorId[asignacion.IdSlot].IndiceSemana
            })
            .Select(grupo => grupo.Count())
            .DefaultIfEmpty(0)
            .Max();

        Assert.True(maximoAsignado <= 2);
    }

    [Fact]
    public void Resolver_DebeFallarCuandoMaximoNocturnosPorSemanaEsInsuficiente()
    {
        var servicio = new ServicioRotacion();
        var plantilla = CrearPlantillaNocturna();
        var reglas = CrearReglas(maximoNocturnosPorSemana: 1);

        var solucion = servicio.Resolver(
            plantilla,
            cantidadSemanas: 1,
            fechaInicio: FechaInicioLunes,
            reglas: reglas,
            opcionesSolver: new OpcionesSolverRotacion
            {
                TiempoMaximoResolucion = TimeSpan.FromSeconds(10),
                CantidadWorkers = 1
            });

        Assert.True(solucion.Estado is not EstadoSolucionRotacion.Optima and not EstadoSolucionRotacion.Factible);
    }

    private static ReglasRotacion CrearReglas(int maximoNocturnosPorSemana)
    {
        return new ReglasRotacion
        {
            Obligatorias = new ReglasGlobalesObligatorias
            {
                MinutosObjetivoSemanales = 16 * 60,
                MinutosMinimosDescansoEntreTurnos = 8 * 60,
                MinimoDiasDescansoConsecutivosPorSemana = 0
            },
            Configurables = new PoliticasConfigurablesEquipo
            {
                MaximoTurnosNocturnosPorMes = 10,
                MaximoTurnosNocturnosPorSemana = maximoNocturnosPorSemana,
                EvitarFinesSemanaConsecutivos = false,
                BalancearHorasSemanales = false,
                BalancearTurnosNocturnos = false,
                BalancearCargaFeriados = false,
                BalancearRecargosCompuestos = false
            }
        };
    }

    private static Plantilla CrearPlantillaNocturna()
    {
        var dias = new[] { "Lunes", "Martes", "Miercoles", "Jueves" };
        return new Plantilla
        {
            Nombre = "Nocturnos",
            GrupoId = "G1",
            PersonaTurno =
            [
                new PersonaTurno
                {
                    PersonaId = "P1",
                    Nombre = "Persona 1",
                    Numero = 1,
                    Grupo = "G1",
                    GrupoId = "G1"
                },
                new PersonaTurno
                {
                    PersonaId = "P2",
                    Nombre = "Persona 2",
                    Numero = 2,
                    Grupo = "G1",
                    GrupoId = "G1"
                }
            ],
            Turnos = dias
                .Select((dia, index) => new Turno
                {
                    NumeroTurno = index + 1,
                    GrupoId = "G1",
                    Dia = dia,
                    TipoHorario = "NOCHE",
                    TipoTurnoId = "N",
                    Inicio = new DateTime(2000, 1, 1, 23, 0, 0),
                    Fin = new DateTime(2000, 1, 1, 7, 0, 0),
                    PersonaTurnoTurno = [],
                    MinimoPersTurno = 1,
                    CapacidadPlanificada = 1,
                    MinutosTrabajoComputables = 8 * 60
                })
                .ToList(),
            TurnosAuxiliares = []
        };
    }
}

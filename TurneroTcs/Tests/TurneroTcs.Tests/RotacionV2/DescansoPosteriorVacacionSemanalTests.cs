using TurneroTcs.Services.RotacionV2.Application;
using TurneroTcs.Services.RotacionV2.Domain;
using Xunit;

namespace TurneroTcs.Tests.RotacionV2;

public sealed class DescansoPosteriorVacacionSemanalTests
{
    private static readonly DateTime FechaInicioLunes = new(2026, 4, 6, 0, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void Resolver_DebePermitirTrabajarAntesDelDescansoDobleEnSemanaDeRegreso()
    {
        var servicio = new ServicioRotacion();
        var plantilla = CrearPlantillaRetornoJueves();
        var vacaciones = new Dictionary<string, HashSet<DateOnly>>(StringComparer.OrdinalIgnoreCase)
        {
            ["P1"] =
            [
                new DateOnly(2026, 4, 6),
                new DateOnly(2026, 4, 7),
                new DateOnly(2026, 4, 8)
            ]
        };

        var problema = servicio.CrearProblema(
            plantilla,
            cantidadSemanas: 1,
            fechaInicio: FechaInicioLunes,
            vacacionesPorPersonaId: vacaciones,
            reglas: CrearReglas());
        var solucion = servicio.ResolverProblema(
            problema,
            new OpcionesSolverRotacion
            {
                TiempoMaximoResolucion = TimeSpan.FromSeconds(10),
                CantidadWorkers = 1
            });

        Assert.True(solucion.Estado is EstadoSolucionRotacion.Optima or EstadoSolucionRotacion.Factible);

        var slotsPorId = problema.Slots.ToDictionary(slot => slot.Id);
        var fechasP1 = solucion.Asignaciones
            .Where(asignacion => asignacion.EmpleadoId == "P1")
            .Select(asignacion => slotsPorId[asignacion.IdSlot].Fecha)
            .OrderBy(fecha => fecha)
            .ToArray();

        Assert.Equal(
            [new DateOnly(2026, 4, 9), new DateOnly(2026, 4, 10)],
            fechasP1);
        Assert.Contains(
            problema.DescansosPosterioresVacacion,
            descanso => descanso.EmpleadoId == "P1" && descanso.FechaRegreso == new DateOnly(2026, 4, 9));
    }

    private static ReglasRotacion CrearReglas()
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
                EvitarFinesSemanaConsecutivos = false,
                BalancearHorasSemanales = false,
                BalancearTurnosNocturnos = false,
                BalancearCargaFeriados = false,
                BalancearRecargosCompuestos = false
            }
        };
    }

    private static Plantilla CrearPlantillaRetornoJueves()
    {
        return new Plantilla
        {
            Nombre = "RetornoVacacionesSemanal",
            GrupoId = "G1",
            PersonaTurno =
            [
                new PersonaTurno
                {
                    PersonaId = "P1",
                    Nombre = "Persona 1",
                    Numero = 1,
                    Grupo = "Grupo 1",
                    GrupoId = "G1",
                    GruposSecundarios = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "G2" }
                },
                new PersonaTurno
                {
                    PersonaId = "P2",
                    Nombre = "Persona 2",
                    Numero = 2,
                    Grupo = "Grupo 2",
                    GrupoId = "G2"
                }
            ],
            Turnos =
            [
                CrearTurno(1, "Jueves", "G1"),
                CrearTurno(2, "Viernes", "G1"),
                CrearTurno(3, "Sabado", "G2"),
                CrearTurno(4, "Domingo", "G2")
            ],
            TurnosAuxiliares = []
        };
    }

    private static Turno CrearTurno(int numeroTurno, string dia, string grupoId)
    {
        return new Turno
        {
            NumeroTurno = numeroTurno,
            GrupoId = grupoId,
            Dia = dia,
            TipoHorario = "DIA",
            TipoTurnoId = "D",
            Inicio = new DateTime(2000, 1, 1, 8, 0, 0),
            Fin = new DateTime(2000, 1, 1, 16, 0, 0),
            PersonaTurnoTurno = [],
            MinimoPersTurno = 1,
            CapacidadPlanificada = 1,
            MinutosTrabajoComputables = 8 * 60
        };
    }
}

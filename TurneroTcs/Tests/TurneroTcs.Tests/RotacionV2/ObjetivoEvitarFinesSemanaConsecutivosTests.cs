using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Application;
using TurneroTcs.Services.RotacionV2.Domain;
using TurneroTcs.Services.RotacionV2.Model;
using TurneroTcs.Services.RotacionV2.Objectives;
using TurneroTcs.Services.RotacionV2.Solver;
using Xunit;

namespace TurneroTcs.Tests.RotacionV2;

public sealed class ObjetivoEvitarFinesSemanaConsecutivosTests
{
    private static readonly DateTime FechaInicioLunes = new(2026, 4, 6, 0, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void CrearPenalizacion_DebeContar_ParConsecutivo_DentroDeLaVentana()
    {
        var contexto = CrearContexto(
            cantidadSemanas: 2,
            nivel: NivelEvitarFinesSemanaConsecutivos.Alto);
        var penalizacion = ObjetivoEvitarFinesSemanaConsecutivos.CrearPenalizacion(contexto);

        Assert.NotNull(penalizacion);

        foreach (var slot in contexto.Problema.Slots)
        {
            contexto.Modelo.Add(contexto.ObtenerVariableAsignacion("E1", slot.Id) == 1);
        }

        var solver = new CpSolver();
        var estado = solver.Solve(contexto.Modelo);

        Assert.True(estado is CpSolverStatus.Optimal or CpSolverStatus.Feasible, $"Estado inesperado: {estado}");
        Assert.Equal(1, solver.Value(penalizacion!));
    }

    [Fact]
    public void CrearPenalizacion_DebeContar_CruceConSemanaAnterior()
    {
        var estadoPrevio = new EstadoResolucionSemanal();
        estadoPrevio.EmpleadosConFinSemanaAnterior.Add("E1");

        var contexto = CrearContexto(
            cantidadSemanas: 1,
            nivel: NivelEvitarFinesSemanaConsecutivos.Alto,
            estadoSemanal: estadoPrevio);
        var penalizacion = ObjetivoEvitarFinesSemanaConsecutivos.CrearPenalizacion(contexto);

        Assert.NotNull(penalizacion);

        foreach (var slot in contexto.Problema.Slots)
        {
            contexto.Modelo.Add(contexto.ObtenerVariableAsignacion("E1", slot.Id) == 1);
        }

        var solver = new CpSolver();
        var estado = solver.Solve(contexto.Modelo);

        Assert.True(estado is CpSolverStatus.Optimal or CpSolverStatus.Feasible, $"Estado inesperado: {estado}");
        Assert.Equal(1, solver.Value(penalizacion!));
    }

    [Fact]
    public void CrearPenalizacion_DebeOmitirse_CuandoNivelEsNoUsar()
    {
        var contexto = CrearContexto(
            cantidadSemanas: 2,
            nivel: NivelEvitarFinesSemanaConsecutivos.NoUsar);

        var penalizacion = ObjetivoEvitarFinesSemanaConsecutivos.CrearPenalizacion(contexto);

        Assert.Null(penalizacion);
    }

    [Fact]
    public void Resolver_DebeBloquear_FinesDeSemanaConsecutivos_CuandoNivelEsNoUsar()
    {
        var servicio = new ServicioRotacion();
        var reglas = new ReglasRotacion
        {
            Obligatorias = new ReglasGlobalesObligatorias
            {
                MinutosObjetivoSemanales = 8 * 60,
                MinutosMinimosDescansoEntreTurnos = 8 * 60,
                MinimoDiasDescansoConsecutivosPorSemana = 2
            },
            Configurables = new PoliticasConfigurablesEquipo
            {
                AplicarVacaciones = false,
                PermiteTurnosAuxiliares = false,
                EvitarFinesSemanaConsecutivos = true,
                MaximoFinesSemanaConsecutivos = 2,
                BalancearHorasSemanales = false,
                BalancearTurnosNocturnos = false,
                BalancearCargaFeriados = false,
                BalancearRecargosCompuestos = false
            }
        };

        var solucion = servicio.Resolver(
            CrearPlantillaSabado(),
            cantidadSemanas: 2,
            fechaInicio: FechaInicioLunes,
            reglas: reglas,
            opcionesSolver: new OpcionesSolverRotacion
            {
                TiempoMaximoResolucion = TimeSpan.FromSeconds(5),
                CantidadWorkers = 1,
                NivelEvitarFinesSemanaConsecutivos = NivelEvitarFinesSemanaConsecutivos.NoUsar
            });

        Assert.Equal(EstadoSolucionRotacion.Infactible, solucion.Estado);
    }

    private static ContextoModeloCp CrearContexto(
        int cantidadSemanas,
        NivelEvitarFinesSemanaConsecutivos nivel,
        EstadoResolucionSemanal? estadoSemanal = null)
    {
        var servicio = new ServicioRotacion();
        var constructor = new ConstructorModeloCp();
        var problema = servicio.CrearProblema(
            CrearPlantillaSabado(),
            cantidadSemanas,
            FechaInicioLunes,
            reglas: new ReglasRotacion
            {
                Obligatorias = new ReglasGlobalesObligatorias
                {
                    MinutosObjetivoSemanales = 8 * 60,
                    MinutosMinimosDescansoEntreTurnos = 8 * 60,
                    MinimoDiasDescansoConsecutivosPorSemana = 2
                },
                Configurables = new PoliticasConfigurablesEquipo
                {
                    AplicarVacaciones = false,
                    PermiteTurnosAuxiliares = false,
                    EvitarFinesSemanaConsecutivos = true,
                    MaximoFinesSemanaConsecutivos = 2,
                    BalancearHorasSemanales = false,
                    BalancearTurnosNocturnos = false,
                    BalancearCargaFeriados = false,
                    BalancearRecargosCompuestos = false,
                    NivelEvitarFinesSemanaConsecutivos = nivel
                }
            });

        return constructor.Construir(
            problema,
            incluirObjetivo: false,
            estadoSemanalAcumulado: estadoSemanal);
    }

    private static Plantilla CrearPlantillaSabado()
    {
        return new Plantilla
        {
            Nombre = "SoloSabado",
            GrupoId = "G1",
            PersonaTurno =
            [
                new PersonaTurno
                {
                    PersonaId = "E1",
                    Nombre = "Empleado 1",
                    Numero = 1,
                    Grupo = "G1",
                    GrupoId = "G1"
                }
            ],
            Turnos =
            [
                new Turno
                {
                    NumeroTurno = 1,
                    GrupoId = "G1",
                    Dia = "sabado",
                    TipoHorario = "MANANA",
                    TipoTurnoId = "TT1",
                    Inicio = new DateTime(2000, 1, 1, 7, 0, 0, DateTimeKind.Unspecified),
                    Fin = new DateTime(2000, 1, 1, 15, 0, 0, DateTimeKind.Unspecified),
                    PersonaTurnoTurno = [],
                    MinimoPersTurno = 1,
                    CapacidadPlanificada = 1,
                    MaximoApoyoCedible = 0,
                    IsAuxiliar = false,
                    EsReemplazoVacacion = false,
                    PuedeOmitirsePorVacacion = false,
                    AuxiliarSharedKey = string.Empty,
                    AuxiliarMaxCompartido = 0,
                    EsOpcional = false,
                    MinutosTrabajoComputables = 8 * 60
                }
            ],
            TurnosAuxiliares = []
        };
    }
}

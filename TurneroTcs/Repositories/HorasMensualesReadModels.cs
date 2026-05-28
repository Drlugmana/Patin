using TurneroTcs.Models;

namespace TurneroTcs.Repositories;

public sealed class HorasMensualesTurnoRow
{
    public string TurnoId { get; set; } = string.Empty;
    public string PersonaId { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Equipo { get; set; } = string.Empty;
    public string EquipoNombre { get; set; } = "-";
    public string GrupoNombre { get; set; } = "-";
    public DateOnly Fecha { get; set; }
    public bool EsFeriado { get; set; }
    public bool NoLaboradoPorFeriado { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
}

public sealed record HorasMensualesPersonaGrupoRow(
    string PersonaId,
    string GrupoId,
    string GrupoNombre);

public sealed record HorasMensualesCambioTurnoRow(
    string TurnoOrigenId,
    string TurnoDestinoId,
    SolicitudEstado EstadoSolicitud,
    DateTime ActualizadoEn);

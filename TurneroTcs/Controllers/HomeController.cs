using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using TurneroTcs.Data;
using TurneroTcs.Models;
using TurneroTcs.ViewModels;

namespace TurneroTcs.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;

    public HomeController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var model = new HomeIndexViewModel();
        var isAdminDashboard = User.Identity?.IsAuthenticated == true
            && (User.IsInRole("SuperAdmin") || User.IsInRole("Admin"));

        model.IsAdminDashboard = isAdminDashboard;
        if (!isAdminDashboard)
        {
            return View(model);
        }

        var today = DateOnly.FromDateTime(DateTime.Today);

        await PopulatePendingSolicitudesAsync(model, today);
        await PopulateVacacionesImpactoAsync(model, today);
        await PopulateFeriadosProximosAsync(model, today);

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private async Task PopulatePendingSolicitudesAsync(HomeIndexViewModel model, DateOnly today)
    {
        var pendingQuery = _db.Solicitudes
            .AsNoTracking()
            .Where(s => s.EstadoSolicitud == SolicitudEstado.Pendiente || s.EstadoSolicitud == SolicitudEstado.AprobadoLider);

        model.PendingSolicitudesCount = await pendingQuery.CountAsync();

        var pendingItems = await pendingQuery
            .OrderBy(s => s.FechaSolicitud)
            .Select(s => new
            {
                s.SolicitudId,
                s.FechaSolicitud,
                s.EstadoSolicitud,
                TipoSolicitud = s.TipoSolicitud != null ? s.TipoSolicitud.NombreSolicitud : s.TipoSolicitudId,
                Nombre = s.PersonaSolicitante != null ? s.PersonaSolicitante.Nombre : null,
                SegundoNombre = s.PersonaSolicitante != null ? s.PersonaSolicitante.SegundoNombre : null,
                Apellido = s.PersonaSolicitante != null ? s.PersonaSolicitante.Apellido : null,
                SegundoApellido = s.PersonaSolicitante != null ? s.PersonaSolicitante.SegundoApellido : null,
                Equipo = s.PersonaSolicitante != null && s.PersonaSolicitante.Equipo != null
                    ? s.PersonaSolicitante.Equipo.NombreEquipo
                    : "-"
            })
            .Take(8)
            .ToListAsync();

        model.PendingSolicitudes = pendingItems
            .Select(item =>
            {
                var solicitudDate = DateOnly.FromDateTime(item.FechaSolicitud.ToLocalTime().Date);
                return new HomePendingSolicitudItemViewModel
                {
                    SolicitudId = item.SolicitudId,
                    TipoSolicitud = item.TipoSolicitud,
                    Solicitante = BuildNombreCompleto(item.Nombre, item.SegundoNombre, item.Apellido, item.SegundoApellido),
                    Equipo = string.IsNullOrWhiteSpace(item.Equipo) ? "-" : item.Equipo,
                    Estado = item.EstadoSolicitud == SolicitudEstado.AprobadoLider ? "En aprobacion" : "Pendiente",
                    FechaSolicitud = item.FechaSolicitud,
                    DiasAbierta = Math.Max(0, today.DayNumber - solicitudDate.DayNumber)
                };
            })
            .ToList();
    }

    private async Task PopulateVacacionesImpactoAsync(HomeIndexViewModel model, DateOnly today)
    {
        model.VacacionesImpactStart = today;
        model.VacacionesImpactEnd = today.AddDays(13);

        var vacaciones = await _db.Vacaciones
            .AsNoTracking()
            .Where(v => v.FechaInicio <= model.VacacionesImpactEnd && v.FechaFin >= model.VacacionesImpactStart)
            .Where(v => v.Solicitud != null && v.Solicitud.EstadoSolicitud == SolicitudEstado.AprobadoFinal)
            .Select(v => new
            {
                PersonaId = v.Solicitud!.PersonaSolicitanteId,
                v.FechaInicio,
                v.FechaFin
            })
            .ToListAsync();

        if (vacaciones.Count == 0)
        {
            return;
        }

        var personaIds = vacaciones
            .Select(v => v.PersonaId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var personas = await _db.Personas
            .AsNoTracking()
            .Where(p => personaIds.Contains(p.PersonaId))
            .Select(p => new
            {
                p.PersonaId,
                Equipo = p.Equipo != null ? p.Equipo.NombreEquipo : "-"
            })
            .ToListAsync();

        var grupos = await _db.PersonaGrupos
            .AsNoTracking()
            .Where(pg => personaIds.Contains(pg.PersonaId) && pg.EsPrincipal)
            .Select(pg => new
            {
                pg.PersonaId,
                Grupo = pg.Grupo != null ? pg.Grupo.NombreGrupo : "-"
            })
            .ToListAsync();

        var equipoByPersona = personas.ToDictionary(x => x.PersonaId, x => string.IsNullOrWhiteSpace(x.Equipo) ? "-" : x.Equipo, StringComparer.OrdinalIgnoreCase);
        var grupoByPersona = grupos
            .GroupBy(x => x.PersonaId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => string.IsNullOrWhiteSpace(g.First().Grupo) ? "-" : g.First().Grupo, StringComparer.OrdinalIgnoreCase);

        var impactAccumulators = new Dictionary<(string Equipo, string Grupo), VacacionesImpactAccumulator>();
        var globalByDay = new Dictionary<DateOnly, HashSet<string>>();

        foreach (var vacacion in vacaciones)
        {
            var equipo = equipoByPersona.TryGetValue(vacacion.PersonaId, out var teamName) ? teamName : "-";
            var grupo = grupoByPersona.TryGetValue(vacacion.PersonaId, out var groupName) ? groupName : "-";
            var key = (equipo, grupo);

            if (!impactAccumulators.TryGetValue(key, out var accumulator))
            {
                accumulator = new VacacionesImpactAccumulator();
                impactAccumulators[key] = accumulator;
            }

            var overlapStart = vacacion.FechaInicio > model.VacacionesImpactStart ? vacacion.FechaInicio : model.VacacionesImpactStart;
            var overlapEnd = vacacion.FechaFin < model.VacacionesImpactEnd ? vacacion.FechaFin : model.VacacionesImpactEnd;

            if (overlapEnd < overlapStart)
            {
                continue;
            }

            accumulator.Personas.Add(vacacion.PersonaId);
            accumulator.DiasPersona += overlapEnd.DayNumber - overlapStart.DayNumber + 1;

            for (var day = overlapStart; day <= overlapEnd; day = day.AddDays(1))
            {
                if (!accumulator.PersonasPorDia.TryGetValue(day, out var personasEnDia))
                {
                    personasEnDia = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    accumulator.PersonasPorDia[day] = personasEnDia;
                }
                personasEnDia.Add(vacacion.PersonaId);

                if (!globalByDay.TryGetValue(day, out var globalSet))
                {
                    globalSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    globalByDay[day] = globalSet;
                }
                globalSet.Add(vacacion.PersonaId);
            }
        }

        model.VacacionesImpacto = impactAccumulators
            .Select(item =>
            {
                var peak = item.Value.PersonasPorDia
                    .OrderByDescending(day => day.Value.Count)
                    .ThenBy(day => day.Key)
                    .FirstOrDefault();

                return new HomeVacacionesImpactItemViewModel
                {
                    Equipo = item.Key.Equipo,
                    Grupo = item.Key.Grupo,
                    PersonasAfectadas = item.Value.Personas.Count,
                    DiasPersona = item.Value.DiasPersona,
                    DiaPico = peak.Key == default ? null : peak.Key,
                    PersonasDiaPico = peak.Value?.Count ?? 0
                };
            })
            .OrderByDescending(x => x.PersonasAfectadas)
            .ThenByDescending(x => x.DiasPersona)
            .ThenBy(x => x.Equipo)
            .ThenBy(x => x.Grupo)
            .ToList();

        model.VacacionesPicos = globalByDay
            .Select(x => new HomeVacacionesPeakDayItemViewModel
            {
                Fecha = x.Key,
                PersonasAfectadas = x.Value.Count
            })
            .OrderByDescending(x => x.PersonasAfectadas)
            .ThenBy(x => x.Fecha)
            .Take(5)
            .ToList();
    }

    private async Task PopulateFeriadosProximosAsync(HomeIndexViewModel model, DateOnly today)
    {
        model.FeriadoWindowStart = today;
        model.FeriadoWindowEnd = today.AddDays(59);

        var feriados = await _db.Feriados
            .AsNoTracking()
            .ToListAsync();

        model.FeriadosProximos = feriados
            .SelectMany(feriado => ExpandFeriadoOccurrences(feriado, model.FeriadoWindowStart, model.FeriadoWindowEnd)
                .Select(occurrence => new HomeUpcomingFeriadoItemViewModel
                {
                    Nombre = feriado.NombreFeriado,
                    Inicio = occurrence.Inicio,
                    Fin = occurrence.Fin,
                    EnCurso = occurrence.Inicio <= today && occurrence.Fin >= today,
                    Dias = occurrence.Fin.DayNumber - occurrence.Inicio.DayNumber + 1
                }))
            .OrderBy(x => x.Inicio)
            .ThenBy(x => x.Nombre)
            .Take(8)
            .ToList();
    }

    private static IEnumerable<(DateOnly Inicio, DateOnly Fin)> ExpandFeriadoOccurrences(
        Feriado feriado,
        DateOnly windowStart,
        DateOnly windowEnd)
    {
        if (RangesOverlap(feriado.InicioFeriado, feriado.FinFeriado, windowStart, windowEnd))
        {
            yield return (feriado.InicioFeriado, feriado.FinFeriado);
        }
    }

    private static bool RangesOverlap(DateOnly aStart, DateOnly aEnd, DateOnly bStart, DateOnly bEnd)
    {
        return aStart <= bEnd && aEnd >= bStart;
    }

    private static string BuildNombreCompleto(string? nombre, string? segundoNombre, string? apellido, string? segundoApellido)
    {
        var fullName = string.Join(" ", new[] { nombre, segundoNombre, apellido, segundoApellido }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        return string.IsNullOrWhiteSpace(fullName) ? "Sin nombre" : fullName;
    }

    private sealed class VacacionesImpactAccumulator
    {
        public HashSet<string> Personas { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int DiasPersona { get; set; }
        public Dictionary<DateOnly, HashSet<string>> PersonasPorDia { get; } = new();
    }
}

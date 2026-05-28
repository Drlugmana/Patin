using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TurneroTcs.Data;
using TurneroTcs.Models;
using TurneroTcs.Security;

namespace TurneroTcs.Seeders;

public class PermisoAccesoSeeder : IPermisoAccesoSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<PermisoAccesoSeeder> _logger;

    public PermisoAccesoSeeder(
        ApplicationDbContext db,
        RoleManager<IdentityRole> roleManager,
        ILogger<PermisoAccesoSeeder> logger)
    {
        _db = db;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        var catalogo = BuildCatalogo();

        var existingByCode = await _db.PermisosAcceso
            .ToDictionaryAsync(p => p.CodigoPermiso, StringComparer.OrdinalIgnoreCase);

        foreach (var item in catalogo)
        {
            if (!existingByCode.TryGetValue(item.Codigo, out var current))
            {
                _db.PermisosAcceso.Add(new PermisoAcceso
                {
                    CodigoPermiso = item.Codigo,
                    NombrePermiso = item.Nombre,
                    Modulo = item.Modulo,
                    Descripcion = item.Descripcion,
                    EsSistema = true
                });
                continue;
            }

            current.NombrePermiso = item.Nombre;
            current.Modulo = item.Modulo;
            current.Descripcion = item.Descripcion;
            current.EsSistema = true;
        }

        await _db.SaveChangesAsync();

        var permisosByCode = await _db.PermisosAcceso
            .AsNoTracking()
            .ToDictionaryAsync(p => p.CodigoPermiso, p => p.PermisoAccesoId, StringComparer.OrdinalIgnoreCase);

        var roleMap = BuildRoleMap();

        foreach (var kvp in roleMap)
        {
            var roleName = kvp.Key;
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                _logger.LogWarning("Rol {RoleName} no existe para asignacion de permisos.", roleName);
                continue;
            }

            foreach (var permisoCode in kvp.Value)
            {
                if (!permisosByCode.TryGetValue(permisoCode, out var permisoId))
                {
                    _logger.LogWarning("Permiso {PermisoCode} no existe en catalogo.", permisoCode);
                    continue;
                }

                var exists = await _db.RolesPermisosAcceso
                    .AnyAsync(rp => rp.RoleId == role.Id && rp.PermisoAccesoId == permisoId);
                if (exists)
                {
                    continue;
                }

                _db.RolesPermisosAcceso.Add(new RolPermisoAcceso
                {
                    RoleId = role.Id,
                    PermisoAccesoId = permisoId
                });
            }
        }

        await _db.SaveChangesAsync();
    }

    private static IReadOnlyList<(string Codigo, string Nombre, string Modulo, string Descripcion)> BuildCatalogo()
    {
        return new List<(string, string, string, string)>
        {
            (PermisosAccesoCodigos.PersonaCrear, "Crear persona", "Persona", "Permite crear personas."),
            (PermisosAccesoCodigos.PersonaVer, "Ver personas", "Persona", "Permite ver personas."),
            (PermisosAccesoCodigos.PersonaEditar, "Editar persona", "Persona", "Permite editar personas."),
            (PermisosAccesoCodigos.PersonaEliminar, "Eliminar persona", "Persona", "Permite eliminar personas."),

            (PermisosAccesoCodigos.EquipoCrear, "Crear equipo", "Equipo", "Permite crear equipos."),
            (PermisosAccesoCodigos.EquipoVer, "Ver equipos", "Equipo", "Permite ver equipos."),
            (PermisosAccesoCodigos.EquipoEditar, "Editar equipo", "Equipo", "Permite editar equipos."),
            (PermisosAccesoCodigos.EquipoEliminar, "Eliminar equipo", "Equipo", "Permite eliminar equipos."),

            (PermisosAccesoCodigos.TipoTurnoCrear, "Crear tipo turno", "TipoTurno", "Permite crear tipos de turno."),
            (PermisosAccesoCodigos.TipoTurnoVer, "Ver tipos turno", "TipoTurno", "Permite ver tipos de turno."),
            (PermisosAccesoCodigos.TipoTurnoEditar, "Editar tipo turno", "TipoTurno", "Permite editar tipos de turno."),
            (PermisosAccesoCodigos.TipoTurnoEliminar, "Eliminar tipo turno", "TipoTurno", "Permite eliminar tipos de turno."),

            (PermisosAccesoCodigos.GrupoCrear, "Crear grupo", "Grupo", "Permite crear grupos."),
            (PermisosAccesoCodigos.GrupoVer, "Ver grupos", "Grupo", "Permite ver grupos."),
            (PermisosAccesoCodigos.GrupoEditar, "Editar grupo", "Grupo", "Permite editar grupos."),
            (PermisosAccesoCodigos.GrupoEliminar, "Eliminar grupo", "Grupo", "Permite eliminar grupos."),

            (PermisosAccesoCodigos.VacacionCrear, "Crear vacaciones", "Vacacion", "Permite crear solicitudes de vacaciones."),
            (PermisosAccesoCodigos.VacacionVer, "Ver vacaciones", "Vacacion", "Permite ver vacaciones."),
            (PermisosAccesoCodigos.VacacionEditar, "Editar vacaciones", "Vacacion", "Permite editar vacaciones."),
            (PermisosAccesoCodigos.VacacionEliminar, "Eliminar vacaciones", "Vacacion", "Permite eliminar vacaciones."),

            (PermisosAccesoCodigos.PermisoCrear, "Crear permisos", "Permiso", "Permite crear solicitudes de permiso."),
            (PermisosAccesoCodigos.PermisoVer, "Ver permisos", "Permiso", "Permite ver permisos."),
            (PermisosAccesoCodigos.PermisoEditar, "Editar permisos", "Permiso", "Permite editar permisos."),
            (PermisosAccesoCodigos.PermisoEliminar, "Eliminar permisos", "Permiso", "Permite eliminar permisos."),

            (PermisosAccesoCodigos.CambioTurnoCrear, "Crear cambios turno", "CambioTurno", "Permite crear solicitudes de cambio de turno."),
            (PermisosAccesoCodigos.CambioTurnoVer, "Ver cambios turno", "CambioTurno", "Permite ver cambios de turno."),
            (PermisosAccesoCodigos.CambioTurnoEditar, "Editar cambios turno", "CambioTurno", "Permite editar cambios de turno."),
            (PermisosAccesoCodigos.CambioTurnoEliminar, "Eliminar cambios turno", "CambioTurno", "Permite eliminar cambios de turno."),

            (PermisosAccesoCodigos.SolicitudVer, "Ver solicitudes", "Solicitud", "Permite ver solicitudes."),
            (PermisosAccesoCodigos.SolicitudAprobarVacacion, "Aprobar solicitud vacacion", "Solicitud", "Permite aprobar solicitudes de vacaciones."),
            (PermisosAccesoCodigos.SolicitudAprobarPermiso, "Aprobar solicitud permiso", "Solicitud", "Permite aprobar solicitudes de permisos."),
            (PermisosAccesoCodigos.SolicitudAprobarCambioTurno, "Aprobar solicitud cambio turno", "Solicitud", "Permite aprobar solicitudes de cambios de turno."),
            (PermisosAccesoCodigos.SolicitudRechazarVacacion, "Rechazar solicitud vacacion", "Solicitud", "Permite rechazar solicitudes de vacaciones."),
            (PermisosAccesoCodigos.SolicitudRechazarPermiso, "Rechazar solicitud permiso", "Solicitud", "Permite rechazar solicitudes de permisos."),
            (PermisosAccesoCodigos.SolicitudRechazarCambioTurno, "Rechazar solicitud cambio turno", "Solicitud", "Permite rechazar solicitudes de cambios de turno."),

            (PermisosAccesoCodigos.RegistroTurnoCrear, "Crear registro turno", "RegistroTurno", "Permite crear turnos."),
            (PermisosAccesoCodigos.RegistroTurnoVer, "Ver registro turno", "RegistroTurno", "Permite ver turnos."),
            (PermisosAccesoCodigos.RegistroTurnoEditar, "Editar registro turno", "RegistroTurno", "Permite editar turnos."),
            (PermisosAccesoCodigos.RegistroTurnoEliminar, "Eliminar registro turno", "RegistroTurno", "Permite eliminar turnos."),

            (PermisosAccesoCodigos.PlanificacionVer, "Ver planificacion", "Planificacion", "Permite ver planificacion."),
            (PermisosAccesoCodigos.PlanificacionEditar, "Editar planificacion", "Planificacion", "Permite editar planificacion."),

            (PermisosAccesoCodigos.FeriadoCrear, "Crear feriado", "Feriado", "Permite crear feriados."),
            (PermisosAccesoCodigos.FeriadoVer, "Ver feriados", "Feriado", "Permite ver feriados."),
            (PermisosAccesoCodigos.FeriadoEditar, "Editar feriado", "Feriado", "Permite editar feriados."),
            (PermisosAccesoCodigos.FeriadoEliminar, "Eliminar feriado", "Feriado", "Permite eliminar feriados."),

            (PermisosAccesoCodigos.PermisoAccesoCrear, "Crear permiso acceso", "Seguridad", "Permite crear permisos de acceso."),
            (PermisosAccesoCodigos.PermisoAccesoVer, "Ver permiso acceso", "Seguridad", "Permite ver permisos de acceso."),
            (PermisosAccesoCodigos.PermisoAccesoEditar, "Editar permiso acceso", "Seguridad", "Permite editar permisos de acceso."),
            (PermisosAccesoCodigos.PermisoAccesoEliminar, "Eliminar permiso acceso", "Seguridad", "Permite eliminar permisos de acceso."),
            (PermisosAccesoCodigos.PermisoAccesoAsignarRol, "Asignar permiso a rol", "Seguridad", "Permite asignar permisos a roles."),
            (PermisosAccesoCodigos.PermisoAccesoAsignarUsuario, "Asignar permiso a usuario", "Seguridad", "Permite asignar permisos a usuarios.")
        };
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildRoleMap()
    {
        return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["SuperAdmin"] = PermisosAccesoCodigos.Todos,
            ["Admin"] = new[]
            {
                PermisosAccesoCodigos.PersonaCrear, PermisosAccesoCodigos.PersonaVer, PermisosAccesoCodigos.PersonaEditar, PermisosAccesoCodigos.PersonaEliminar,
                PermisosAccesoCodigos.EquipoCrear, PermisosAccesoCodigos.EquipoVer, PermisosAccesoCodigos.EquipoEditar, PermisosAccesoCodigos.EquipoEliminar,
                PermisosAccesoCodigos.TipoTurnoCrear, PermisosAccesoCodigos.TipoTurnoVer, PermisosAccesoCodigos.TipoTurnoEditar, PermisosAccesoCodigos.TipoTurnoEliminar,
                PermisosAccesoCodigos.GrupoCrear, PermisosAccesoCodigos.GrupoVer, PermisosAccesoCodigos.GrupoEditar, PermisosAccesoCodigos.GrupoEliminar,
                PermisosAccesoCodigos.VacacionCrear, PermisosAccesoCodigos.VacacionVer, PermisosAccesoCodigos.VacacionEditar, PermisosAccesoCodigos.VacacionEliminar,
                PermisosAccesoCodigos.PermisoCrear, PermisosAccesoCodigos.PermisoVer, PermisosAccesoCodigos.PermisoEditar, PermisosAccesoCodigos.PermisoEliminar,
                PermisosAccesoCodigos.CambioTurnoCrear, PermisosAccesoCodigos.CambioTurnoVer, PermisosAccesoCodigos.CambioTurnoEditar, PermisosAccesoCodigos.CambioTurnoEliminar,
                PermisosAccesoCodigos.SolicitudVer,
                PermisosAccesoCodigos.SolicitudAprobarVacacion, PermisosAccesoCodigos.SolicitudAprobarPermiso, PermisosAccesoCodigos.SolicitudAprobarCambioTurno,
                PermisosAccesoCodigos.SolicitudRechazarVacacion, PermisosAccesoCodigos.SolicitudRechazarPermiso, PermisosAccesoCodigos.SolicitudRechazarCambioTurno,
                PermisosAccesoCodigos.RegistroTurnoCrear, PermisosAccesoCodigos.RegistroTurnoVer, PermisosAccesoCodigos.RegistroTurnoEditar, PermisosAccesoCodigos.RegistroTurnoEliminar,
                PermisosAccesoCodigos.PlanificacionVer, PermisosAccesoCodigos.PlanificacionEditar,
                PermisosAccesoCodigos.FeriadoCrear, PermisosAccesoCodigos.FeriadoVer, PermisosAccesoCodigos.FeriadoEditar, PermisosAccesoCodigos.FeriadoEliminar,
                PermisosAccesoCodigos.PermisoAccesoVer, PermisosAccesoCodigos.PermisoAccesoAsignarUsuario
            },
            ["Lider"] = new[]
            {
                PermisosAccesoCodigos.PersonaVer, PermisosAccesoCodigos.PersonaCrear, PermisosAccesoCodigos.PersonaEditar,
                PermisosAccesoCodigos.EquipoVer,
                PermisosAccesoCodigos.TipoTurnoVer,
                PermisosAccesoCodigos.GrupoVer,
                PermisosAccesoCodigos.VacacionVer, PermisosAccesoCodigos.PermisoVer, PermisosAccesoCodigos.CambioTurnoVer,
                PermisosAccesoCodigos.SolicitudVer,
                PermisosAccesoCodigos.SolicitudAprobarVacacion, PermisosAccesoCodigos.SolicitudAprobarPermiso, PermisosAccesoCodigos.SolicitudAprobarCambioTurno,
                PermisosAccesoCodigos.SolicitudRechazarVacacion, PermisosAccesoCodigos.SolicitudRechazarPermiso, PermisosAccesoCodigos.SolicitudRechazarCambioTurno,
                PermisosAccesoCodigos.RegistroTurnoVer, PermisosAccesoCodigos.RegistroTurnoEditar,
                PermisosAccesoCodigos.PlanificacionVer, PermisosAccesoCodigos.PlanificacionEditar,
                PermisosAccesoCodigos.FeriadoVer
            },
            ["Usuario"] = new[]
            {
                PermisosAccesoCodigos.SolicitudVer,
                PermisosAccesoCodigos.VacacionCrear, PermisosAccesoCodigos.VacacionVer,
                PermisosAccesoCodigos.PermisoCrear, PermisosAccesoCodigos.PermisoVer,
                PermisosAccesoCodigos.CambioTurnoCrear, PermisosAccesoCodigos.CambioTurnoVer,
                PermisosAccesoCodigos.RegistroTurnoVer
            }
        };
    }
}

using System.Collections.Generic;

namespace TurneroTcs.Security;

public static class PermisosAccesoCodigos
{
    public const string PersonaCrear = "persona.create";
    public const string PersonaVer = "persona.read";
    public const string PersonaEditar = "persona.update";
    public const string PersonaEliminar = "persona.delete";

    public const string EquipoCrear = "equipo.create";
    public const string EquipoVer = "equipo.read";
    public const string EquipoEditar = "equipo.update";
    public const string EquipoEliminar = "equipo.delete";

    public const string TipoTurnoCrear = "tipoturno.create";
    public const string TipoTurnoVer = "tipoturno.read";
    public const string TipoTurnoEditar = "tipoturno.update";
    public const string TipoTurnoEliminar = "tipoturno.delete";

    public const string GrupoCrear = "grupo.create";
    public const string GrupoVer = "grupo.read";
    public const string GrupoEditar = "grupo.update";
    public const string GrupoEliminar = "grupo.delete";

    public const string VacacionCrear = "vacacion.create";
    public const string VacacionVer = "vacacion.read";
    public const string VacacionEditar = "vacacion.update";
    public const string VacacionEliminar = "vacacion.delete";

    public const string PermisoCrear = "permiso.create";
    public const string PermisoVer = "permiso.read";
    public const string PermisoEditar = "permiso.update";
    public const string PermisoEliminar = "permiso.delete";

    public const string CambioTurnoCrear = "cambioturno.create";
    public const string CambioTurnoVer = "cambioturno.read";
    public const string CambioTurnoEditar = "cambioturno.update";
    public const string CambioTurnoEliminar = "cambioturno.delete";

    public const string SolicitudVer = "solicitud.read";
    public const string SolicitudAprobarVacacion = "solicitud.approve.vacacion";
    public const string SolicitudAprobarPermiso = "solicitud.approve.permiso";
    public const string SolicitudAprobarCambioTurno = "solicitud.approve.cambioturno";
    public const string SolicitudRechazarVacacion = "solicitud.reject.vacacion";
    public const string SolicitudRechazarPermiso = "solicitud.reject.permiso";
    public const string SolicitudRechazarCambioTurno = "solicitud.reject.cambioturno";

    public const string RegistroTurnoCrear = "registroturno.create";
    public const string RegistroTurnoVer = "registroturno.read";
    public const string RegistroTurnoEditar = "registroturno.update";
    public const string RegistroTurnoEliminar = "registroturno.delete";

    public const string PlanificacionVer = "planificacion.read";
    public const string PlanificacionEditar = "planificacion.update";

    public const string FeriadoCrear = "feriado.create";
    public const string FeriadoVer = "feriado.read";
    public const string FeriadoEditar = "feriado.update";
    public const string FeriadoEliminar = "feriado.delete";

    public const string PermisoAccesoCrear = "security.permission.create";
    public const string PermisoAccesoVer = "security.permission.read";
    public const string PermisoAccesoEditar = "security.permission.update";
    public const string PermisoAccesoEliminar = "security.permission.delete";
    public const string PermisoAccesoAsignarRol = "security.permission.assign.role";
    public const string PermisoAccesoAsignarUsuario = "security.permission.assign.user";

    public static IReadOnlyList<string> Todos => new[]
    {
        PersonaCrear, PersonaVer, PersonaEditar, PersonaEliminar,
        EquipoCrear, EquipoVer, EquipoEditar, EquipoEliminar,
        TipoTurnoCrear, TipoTurnoVer, TipoTurnoEditar, TipoTurnoEliminar,
        GrupoCrear, GrupoVer, GrupoEditar, GrupoEliminar,
        VacacionCrear, VacacionVer, VacacionEditar, VacacionEliminar,
        PermisoCrear, PermisoVer, PermisoEditar, PermisoEliminar,
        CambioTurnoCrear, CambioTurnoVer, CambioTurnoEditar, CambioTurnoEliminar,
        SolicitudVer, SolicitudAprobarVacacion, SolicitudAprobarPermiso, SolicitudAprobarCambioTurno,
        SolicitudRechazarVacacion, SolicitudRechazarPermiso, SolicitudRechazarCambioTurno,
        RegistroTurnoCrear, RegistroTurnoVer, RegistroTurnoEditar, RegistroTurnoEliminar,
        PlanificacionVer, PlanificacionEditar,
        FeriadoCrear, FeriadoVer, FeriadoEditar, FeriadoEliminar,
        PermisoAccesoCrear, PermisoAccesoVer, PermisoAccesoEditar, PermisoAccesoEliminar,
        PermisoAccesoAsignarRol, PermisoAccesoAsignarUsuario
    };
}

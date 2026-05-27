using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TurneroTcs.Models;

namespace TurneroTcs.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Persona> Personas => Set<Persona>();
    public DbSet<Equipo> Equipos => Set<Equipo>();
    public DbSet<TipoTurno> TipoTurnos => Set<TipoTurno>();
    public DbSet<RegistroTurno> RegistroTurnos => Set<RegistroTurno>();
    public DbSet<TipoSolicitud> TipoSolicitudes => Set<TipoSolicitud>();
    public DbSet<Solicitud> Solicitudes => Set<Solicitud>();
    public DbSet<Vacacion> Vacaciones => Set<Vacacion>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<CambioTurno> CambiosTurno => Set<CambioTurno>();
    public DbSet<Calamidad> Calamidades => Set<Calamidad>();
    public DbSet<CalamidadReemplazo> CalamidadReemplazos => Set<CalamidadReemplazo>();
    public DbSet<Grupo> Grupos => Set<Grupo>();
    public DbSet<GrupoTurnoConfig> GrupoTurnoConfigs => Set<GrupoTurnoConfig>();
    public DbSet<PersonaGrupo> PersonaGrupos => Set<PersonaGrupo>();
    public DbSet<Planificacion> Planificaciones => Set<Planificacion>();
    public DbSet<PlanificacionApoyoGrupo> PlanificacionesApoyoGrupo => Set<PlanificacionApoyoGrupo>();
    public DbSet<PlanificacionTurnoOpcionalVacacionGrupo> PlanificacionesTurnosOpcionalesVacacionGrupo => Set<PlanificacionTurnoOpcionalVacacionGrupo>();
    public DbSet<PlanificacionAuxiliarEquipo> PlanificacionesAuxiliaresEquipo => Set<PlanificacionAuxiliarEquipo>();
    public DbSet<PlanificacionAuxiliarEquipoGrupo> PlanificacionesAuxiliaresEquipoGrupos => Set<PlanificacionAuxiliarEquipoGrupo>();
    public DbSet<PlanificacionBlueprint> PlanificacionBlueprints => Set<PlanificacionBlueprint>();
    public DbSet<EquipoTipoTurno> EquipoTipoTurnos => Set<EquipoTipoTurno>();
    public DbSet<Feriado> Feriados => Set<Feriado>();
    public DbSet<FeriadoCoberturaConfig> FeriadoCoberturaConfigs => Set<FeriadoCoberturaConfig>();
    public DbSet<EquipoPlanificacionConfig> EquipoPlanificacionConfigs => Set<EquipoPlanificacionConfig>();
    public DbSet<PermisoAcceso> PermisosAcceso => Set<PermisoAcceso>();
    public DbSet<RolPermisoAcceso> RolesPermisosAcceso => Set<RolPermisoAcceso>();
    public DbSet<UsuarioPermisoAcceso> UsuariosPermisosAcceso => Set<UsuarioPermisoAcceso>();
    public DbSet<ExcepcionTurnoPersona> ExcepcionesTurnoPersonas => Set<ExcepcionTurnoPersona>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Persona>()
            .HasOne(p => p.User)
            .WithOne()
            .HasForeignKey<Persona>(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.Entity<Persona>()
            .HasIndex(p => p.UserId)
            .IsUnique();

        builder.Entity<Persona>()
            .Property(p => p.PersonaId)
            .HasDefaultValueSql("generate_short_id()")
            .ValueGeneratedOnAdd();
        
        builder.Entity<Persona>()
            .HasOne(p => p.Equipo)
            .WithMany(e => e.Personas)
            .HasForeignKey( p => p.EquipoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Equipo>()
            .Property(p => p.EquipoId)
            .HasDefaultValueSql("generate_short_id()")
            .ValueGeneratedOnAdd();

        builder.Entity<Equipo>()
            .Property(e => e.TipoGeneracion)
            .HasDefaultValue("Rotacion")
            .HasMaxLength(20);

        builder.Entity<Grupo>()
            .HasOne(g => g.Equipo)
            .WithMany(e => e.Grupos)
            .HasForeignKey(g => g.EquipoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RegistroTurno>()
            .HasOne(rt => rt.Persona)
            .WithMany()
            .HasForeignKey(rt => rt.PersonaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RegistroTurno>()
            .HasOne(rt => rt.TipoTurno)
            .WithMany()
            .HasForeignKey(rt => rt.TipoTurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RegistroTurno>()
            .HasOne(rt => rt.Grupo)
            .WithMany()
            .HasForeignKey(rt => rt.GrupoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RegistroTurno>()
            .Property(rt => rt.EsFeriado)
            .HasDefaultValue(false);

        builder.Entity<RegistroTurno>()
            .Property(rt => rt.NoLaboradoPorFeriado)
            .HasDefaultValue(false);

        builder.Entity<RegistroTurno>()
            .Property(rt => rt.EsTurnoExtra)
            .HasDefaultValue(false);

        builder.Entity<RegistroTurno>()
            .HasIndex(rt => rt.PersonaId);

        builder.Entity<RegistroTurno>()
            .HasIndex(rt => rt.TipoTurnoId);

        builder.Entity<RegistroTurno>()
            .HasIndex(rt => new { rt.PersonaId, rt.TipoTurnoId });

        builder.Entity<RegistroTurno>()
            .HasIndex(rt => new { rt.FechaTurno, rt.EsFeriado, rt.NoLaboradoPorFeriado });

        builder.Entity<RegistroTurno>()
            .HasIndex(rt => new { rt.PersonaId, rt.FechaTurno, rt.EsTurnoExtra });

        builder.Entity<RegistroTurno>()
            .HasIndex(rt => rt.GrupoId);

        builder.Entity<Grupo>()
            .Property(g => g.GrupoId)
            .HasDefaultValueSql("generate_short_id()")
            .ValueGeneratedOnAdd();

        builder.Entity<Grupo>()
            .HasIndex(g => g.EquipoId);

        builder.Entity<Grupo>()
            .HasIndex(g => new { g.EquipoId, g.NombreGrupo })
            .IsUnique();

        builder.Entity<PlanificacionAuxiliarEquipo>()
            .Property(p => p.PlanificacionAuxiliarEquipoId)
            .HasDefaultValueSql("generate_short_id()")
            .ValueGeneratedOnAdd();

        builder.Entity<PlanificacionAuxiliarEquipo>()
            .HasOne(p => p.Equipo)
            .WithMany(e => e.PlanificacionesAuxiliaresEquipo)
            .HasForeignKey(p => p.EquipoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PlanificacionAuxiliarEquipo>()
            .HasOne(p => p.TipoTurno)
            .WithMany()
            .HasForeignKey(p => p.TipoTurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PlanificacionAuxiliarEquipo>()
            .HasIndex(p => new { p.EquipoId, p.TipoTurnoId })
            .IsUnique();

        builder.Entity<PlanificacionAuxiliarEquipoGrupo>()
            .Property(p => p.PlanificacionAuxiliarEquipoGrupoId)
            .HasDefaultValueSql("generate_short_id()")
            .ValueGeneratedOnAdd();

        builder.Entity<PlanificacionAuxiliarEquipoGrupo>()
            .HasOne(p => p.PlanificacionAuxiliarEquipo)
            .WithMany(p => p.GruposPermitidos)
            .HasForeignKey(p => p.PlanificacionAuxiliarEquipoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PlanificacionAuxiliarEquipoGrupo>()
            .HasOne(p => p.Grupo)
            .WithMany(g => g.PlanificacionesAuxiliaresEquipo)
            .HasForeignKey(p => p.GrupoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PlanificacionAuxiliarEquipoGrupo>()
            .HasIndex(p => new { p.PlanificacionAuxiliarEquipoId, p.GrupoId })
            .IsUnique();

        builder.Entity<PlanificacionApoyoGrupo>()
            .Property(p => p.PlanificacionApoyoGrupoId)
            .HasDefaultValueSql("generate_short_id()")
            .ValueGeneratedOnAdd();

        builder.Entity<PlanificacionApoyoGrupo>()
            .HasOne(p => p.Grupo)
            .WithMany(g => g.PlanificacionesApoyo)
            .HasForeignKey(p => p.GrupoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PlanificacionApoyoGrupo>()
            .HasOne(p => p.TipoTurno)
            .WithMany()
            .HasForeignKey(p => p.TipoTurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PlanificacionApoyoGrupo>()
            .HasIndex(p => new { p.GrupoId, p.Dia, p.TipoTurnoId })
            .IsUnique();

        builder.Entity<PlanificacionTurnoOpcionalVacacionGrupo>()
            .Property(p => p.PlanificacionTurnoOpcionalVacacionGrupoId)
            .HasDefaultValueSql("generate_short_id()")
            .ValueGeneratedOnAdd();

        builder.Entity<PlanificacionTurnoOpcionalVacacionGrupo>()
            .HasOne(p => p.Grupo)
            .WithMany(g => g.PlanificacionesTurnosOpcionalesVacacion)
            .HasForeignKey(p => p.GrupoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PlanificacionTurnoOpcionalVacacionGrupo>()
            .HasOne(p => p.TipoTurno)
            .WithMany()
            .HasForeignKey(p => p.TipoTurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PlanificacionTurnoOpcionalVacacionGrupo>()
            .HasIndex(p => new { p.GrupoId, p.Dia, p.TipoTurnoId })
            .IsUnique();

        builder.Entity<GrupoTurnoConfig>()
            .Property(g => g.GrupoTurnoConfigId)
            .HasDefaultValueSql("generate_short_id()")
            .ValueGeneratedOnAdd();

        builder.Entity<GrupoTurnoConfig>()
            .HasOne(g => g.Grupo)
            .WithMany()
            .HasForeignKey(g => g.GrupoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<GrupoTurnoConfig>()
            .HasOne(g => g.TipoTurno)
            .WithMany()
            .HasForeignKey(g => g.TipoTurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<GrupoTurnoConfig>()
            .HasIndex(g => g.GrupoId);

        builder.Entity<GrupoTurnoConfig>()
            .HasIndex(g => g.TipoTurnoId);

        builder.Entity<GrupoTurnoConfig>()
            .HasIndex(g => new { g.GrupoId, g.Dia, g.TipoTurnoId })
            .IsUnique();

        builder.Entity<PersonaGrupo>()
            .Property(pg => pg.PersonaGrupoId)
            .HasDefaultValueSql("generate_short_id()")
            .ValueGeneratedOnAdd();

        builder.Entity<PersonaGrupo>()
            .Property(pg => pg.EsPrincipal)
            .HasDefaultValue(true);

        builder.Entity<PersonaGrupo>()
            .HasOne(pg => pg.Persona)
            .WithMany()
            .HasForeignKey(pg => pg.PersonaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PersonaGrupo>()
            .HasOne(pg => pg.Grupo)
            .WithMany()
            .HasForeignKey(pg => pg.GrupoId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.Entity<PersonaGrupo>()
            .HasIndex(pg => new { pg.PersonaId, pg.GrupoId })
            .IsUnique();

        builder.Entity<Solicitud>()
            .HasOne(s => s.PersonaSolicitante)
            .WithMany()
            .HasForeignKey(s => s.PersonaSolicitanteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Solicitud>()
            .HasOne(s => s.TipoSolicitud)
            .WithMany()
            .HasForeignKey(s => s.TipoSolicitudId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Solicitud>()
            .HasOne(s => s.PersonaAprobador1)
            .WithMany()
            .HasForeignKey(s => s.PersonaAprobador1Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Solicitud>()
            .HasOne(s => s.PersonaAprobador2)
            .WithMany()
            .HasForeignKey(s => s.PersonaAprobador2Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Solicitud>()
            .HasIndex(s => s.PersonaSolicitanteId);

        builder.Entity<Solicitud>()
            .HasIndex(s => s.TipoSolicitudId);

        builder.Entity<Solicitud>()
            .HasIndex(s => s.EstadoSolicitud);

        builder.Entity<Solicitud>()
            .HasIndex(s => s.FechaSolicitud);

        builder.Entity<Solicitud>()
            .Property(s => s.EstadoSolicitud)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Entity<Vacacion>()
            .HasOne(v => v.Solicitud)
            .WithMany()
            .HasForeignKey(v => v.SolicitudId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Vacacion>()
            .HasIndex(v => v.SolicitudId);

        builder.Entity<Vacacion>()
            .HasIndex(v => v.FechaInicio);

        builder.Entity<Vacacion>()
            .HasIndex(v => v.FechaFin);

        builder.Entity<Permiso>()
            .HasOne(p => p.Solicitud)
            .WithMany()
            .HasForeignKey(p => p.SolicitudId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Permiso>()
            .HasOne(p => p.RegistroTurno)
            .WithMany()
            .HasForeignKey(p => p.RegistroTurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Permiso>()
            .HasIndex(p => p.SolicitudId);

        builder.Entity<Permiso>()
            .HasIndex(p => p.RegistroTurnoId);

        builder.Entity<Permiso>()
            .HasIndex(p => p.HoraInicio);

        builder.Entity<Permiso>()
            .HasIndex(p => p.HoraFin);

        builder.Entity<CambioTurno>()
            .HasOne(c => c.Solicitud)
            .WithMany()
            .HasForeignKey(c => c.SolicitudId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CambioTurno>()
            .HasOne(c => c.TurnoOrigen)
            .WithMany()
            .HasForeignKey(c => c.TurnoOrigenId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CambioTurno>()
            .HasOne(c => c.TurnoDestino)
            .WithMany()
            .HasForeignKey(c => c.TurnoDestinoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CambioTurno>()
            .HasIndex(c => c.SolicitudId);

        builder.Entity<CambioTurno>()
            .HasIndex(c => c.TurnoOrigenId);

        builder.Entity<CambioTurno>()
            .HasIndex(c => c.TurnoDestinoId);

        builder.Entity<Calamidad>()
            .HasOne(c => c.Solicitud)
            .WithMany()
            .HasForeignKey(c => c.SolicitudId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Calamidad>()
            .HasIndex(c => c.SolicitudId)
            .IsUnique();

        builder.Entity<Calamidad>()
            .HasIndex(c => c.FechaInicio);

        builder.Entity<Calamidad>()
            .HasIndex(c => c.FechaFin);

        builder.Entity<CalamidadReemplazo>()
            .HasOne(c => c.Solicitud)
            .WithMany()
            .HasForeignKey(c => c.SolicitudId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CalamidadReemplazo>()
            .HasOne(c => c.TurnoAusente)
            .WithMany()
            .HasForeignKey(c => c.TurnoAusenteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CalamidadReemplazo>()
            .HasOne(c => c.TurnoReemplazo)
            .WithMany()
            .HasForeignKey(c => c.TurnoReemplazoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CalamidadReemplazo>()
            .Property(c => c.ModoReemplazo)
            .HasMaxLength(24)
            .HasDefaultValue("SWAP");

        builder.Entity<CalamidadReemplazo>()
            .HasIndex(c => c.SolicitudId);

        builder.Entity<CalamidadReemplazo>()
            .HasIndex(c => c.TurnoAusenteId);

        builder.Entity<CalamidadReemplazo>()
            .HasIndex(c => c.TurnoReemplazoId);

        builder.Entity<CalamidadReemplazo>()
            .HasIndex(c => new { c.SolicitudId, c.TurnoAusenteId })
            .IsUnique();

        builder.Entity<CalamidadReemplazo>()
            .HasIndex(c => new { c.SolicitudId, c.TurnoReemplazoId })
            .IsUnique();

        builder.Entity<Planificacion>()
            .Property(p => p.PlanificacionId)
            .HasDefaultValueSql("generate_short_id()")
            .ValueGeneratedOnAdd();

        builder.Entity<Planificacion>()
            .HasOne(p => p.Grupo)
            .WithMany()
            .HasForeignKey(p => p.GrupoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Planificacion>()
            .HasOne(p => p.TipoTurno)
            .WithMany()
            .HasForeignKey(p => p.TipoTurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Planificacion>()
            .HasOne(p => p.GrupoFuenteSecundarios)
            .WithMany()
            .HasForeignKey(p => p.GrupoFuenteSecundariosId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Planificacion>()
            .HasIndex(p => p.GrupoId);

        builder.Entity<Planificacion>()
            .HasIndex(p => p.TipoTurnoId);

        builder.Entity<Planificacion>()
            .HasIndex(p => p.GrupoFuenteSecundariosId);

        builder.Entity<Planificacion>()
            .HasIndex(p => new { p.GrupoId, p.Dia, p.TipoTurnoId, p.IsAuxiliar })
            .IsUnique();

        builder.Entity<PlanificacionBlueprint>()
            .Property(p => p.PlanificacionBlueprintId)
            .HasDefaultValueSql("generate_short_id()")
            .ValueGeneratedOnAdd();

        builder.Entity<PlanificacionBlueprint>()
            .HasOne(p => p.Grupo)
            .WithMany()
            .HasForeignKey(p => p.GrupoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PlanificacionBlueprint>()
            .HasOne(p => p.TipoTurno)
            .WithMany()
            .HasForeignKey(p => p.TipoTurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PlanificacionBlueprint>()
            .HasIndex(p => p.GrupoId);

        builder.Entity<PlanificacionBlueprint>()
            .HasIndex(p => p.TipoTurnoId);

        builder.Entity<PlanificacionBlueprint>()
            .HasIndex(p => new { p.GrupoId, p.Dia, p.TipoTurnoId })
            .IsUnique();

        builder.Entity<EquipoTipoTurno>()
            .HasKey(et => new { et.EquipoId, et.TipoTurnoId });

        builder.Entity<EquipoTipoTurno>()
            .HasOne(et => et.Equipo)
            .WithMany(e => e.EquipoTipoTurnos)
            .HasForeignKey(et => et.EquipoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<EquipoTipoTurno>()
            .HasOne(et => et.TipoTurno)
            .WithMany(t => t.EquipoTipoTurnos)
            .HasForeignKey(et => et.TipoTurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<EquipoTipoTurno>()
            .HasIndex(et => et.EquipoId);

        builder.Entity<EquipoTipoTurno>()
            .HasIndex(et => et.TipoTurnoId);

        builder.Entity<Feriado>()
            .Property(f => f.FeriadoId)
            .HasDefaultValueSql("generate_short_id()")
            .ValueGeneratedOnAdd();

        builder.Entity<Feriado>()
            .HasIndex(f => f.InicioFeriado);

        builder.Entity<Feriado>()
            .HasIndex(f => f.FinFeriado);

        builder.Entity<FeriadoCoberturaConfig>()
            .Property(f => f.FeriadoCoberturaConfigId)
            .HasDefaultValueSql("generate_short_id()")
            .ValueGeneratedOnAdd();

        builder.Entity<FeriadoCoberturaConfig>()
            .HasOne(f => f.Equipo)
            .WithMany(e => e.FeriadoCoberturaConfigs)
            .HasForeignKey(f => f.EquipoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<FeriadoCoberturaConfig>()
            .HasOne(f => f.Grupo)
            .WithMany()
            .HasForeignKey(f => f.GrupoId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.Entity<FeriadoCoberturaConfig>()
            .HasOne(f => f.TipoTurno)
            .WithMany()
            .HasForeignKey(f => f.TipoTurnoId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.Entity<FeriadoCoberturaConfig>()
            .HasIndex(f => f.EquipoId);

        builder.Entity<FeriadoCoberturaConfig>()
            .HasIndex(f => new { f.EquipoId, f.GrupoId });

        builder.Entity<FeriadoCoberturaConfig>()
            .HasIndex(f => new { f.EquipoId, f.GrupoId, f.TipoTurnoId })
            .IsUnique();

        builder.Entity<EquipoPlanificacionConfig>()
            .Property(c => c.EquipoPlanificacionConfigId)
            .HasDefaultValueSql("generate_short_id()")
            .ValueGeneratedOnAdd();

        builder.Entity<EquipoPlanificacionConfig>()
            .HasOne(c => c.Equipo)
            .WithMany(e => e.PlanificacionConfigs)
            .HasForeignKey(c => c.EquipoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<EquipoPlanificacionConfig>()
            .HasIndex(c => c.EquipoId)
            .IsUnique();

        builder.Entity<PermisoAcceso>()
            .Property(p => p.PermisoAccesoId)
            .HasDefaultValueSql("generate_short_id()")
            .ValueGeneratedOnAdd();

        builder.Entity<PermisoAcceso>()
            .HasIndex(p => p.CodigoPermiso)
            .IsUnique();

        builder.Entity<PermisoAcceso>()
            .HasIndex(p => p.Modulo);

        builder.Entity<RolPermisoAcceso>()
            .HasKey(rp => new { rp.RoleId, rp.PermisoAccesoId });

        builder.Entity<RolPermisoAcceso>()
            .HasOne(rp => rp.Role)
            .WithMany()
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RolPermisoAcceso>()
            .HasOne(rp => rp.PermisoAcceso)
            .WithMany(p => p.RolesAsignados)
            .HasForeignKey(rp => rp.PermisoAccesoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RolPermisoAcceso>()
            .HasIndex(rp => rp.PermisoAccesoId);

        builder.Entity<UsuarioPermisoAcceso>()
            .HasKey(up => new { up.UserId, up.PermisoAccesoId });

        builder.Entity<UsuarioPermisoAcceso>()
            .HasOne(up => up.User)
            .WithMany()
            .HasForeignKey(up => up.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<UsuarioPermisoAcceso>()
            .HasOne(up => up.PermisoAcceso)
            .WithMany(p => p.UsuariosAsignados)
            .HasForeignKey(up => up.PermisoAccesoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<UsuarioPermisoAcceso>()
            .Property(up => up.EsDenegado)
            .HasDefaultValue(false);

        builder.Entity<UsuarioPermisoAcceso>()
            .HasIndex(up => new { up.UserId, up.EsDenegado });

        builder.Entity<UsuarioPermisoAcceso>()
            .HasIndex(up => up.PermisoAccesoId);

        builder.Entity<ExcepcionTurnoPersona>()
            .HasOne(e => e.Persona)
            .WithMany()
            .HasForeignKey(e => e.PersonaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ExcepcionTurnoPersona>()
            .HasOne(e => e.TipoTurno)
            .WithMany()
            .HasForeignKey(e => e.TipoTurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ExcepcionTurnoPersona>()
            .Property(e => e.ExcepcionTurnoPersonaId)
            .HasDefaultValueSql("generate_short_id()")
            .ValueGeneratedOnAdd();

        builder.Entity<ExcepcionTurnoPersona>()
            .HasIndex(e => new { e.PersonaId, e.FechaInicio, e.FechaFin });

        builder.Entity<ExcepcionTurnoPersona>()
            .HasIndex(e => e.TipoTurnoId);

    }

    public override int SaveChanges()
    {
        UpdatePersonaTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdatePersonaTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdatePersonaTimestamps()
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Persona>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreadoEn = utcNow;
                entry.Entity.ActualizadoEn = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(e => e.CreadoEn).IsModified = false;
                entry.Entity.ActualizadoEn = utcNow;
            }
        }

        foreach (var entry in ChangeTracker.Entries<PermisoAcceso>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreadoEn = utcNow;
                entry.Entity.ActualizadoEn = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(e => e.CreadoEn).IsModified = false;
                entry.Entity.ActualizadoEn = utcNow;
            }
        }
    }
}

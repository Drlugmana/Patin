namespace TurneroTcs.Services.RotacionV2.Domain;


///////////////////////////////////// MODELOS PARA GENERACION DE ROTACIÓN ////////////////////////////////////

/// <summary>
/// Plantilla de rotación semanal para un grupo de trabajo.
/// Define qué personas participan y qué turnos (regulares y auxiliares) deben cubrirse cada semana.
/// </summary>
public class Plantilla
{
    /// <summary>Nombre descriptivo de la plantilla.</summary>
    public required string Nombre { get; set; }

    /// <summary>Identificador del grupo al que pertenece la plantilla.</summary>
    public required string GrupoId { get; set; }

    /// <summary>Lista de personas con sus datos de grupo y número de orden.</summary>
    public required List<PersonaTurno> PersonaTurno { get; set; }

    /// <summary>Turnos regulares definidos en la plantilla semanal.</summary>
    public required List<Turno> Turnos { get; set; }

    /// <summary>Turnos auxiliares definidos en la plantilla semanal.</summary>
    public required List<Turno> TurnosAuxiliares { get; set; }

    public Plantilla()
    {
    }

    public Plantilla(string Nombre, string GrupoId, List<PersonaTurno> PersonaTurno, List<Turno> Turnos, List<Turno> TurnosAuxiliares) : this()
    {
        this.Nombre = Nombre;
        this.GrupoId = GrupoId;
        this.PersonaTurno = PersonaTurno;
        this.Turnos = Turnos;
        this.TurnosAuxiliares = TurnosAuxiliares;
    }

    public override string ToString()
    {
        var resultado = $"Plantilla para el grupo {GrupoId} - {Nombre}:\n";
        resultado += "Personas:\n";
        foreach (var persona in PersonaTurno)
        {
            resultado += $"  - {persona}\n";
        }
        resultado += "Turnos:\n";
        foreach (var turno in Turnos)
        {
            resultado += $"  - {turno}\n";
        }
        return resultado;
    }
}

/// <summary>
/// Representa a una persona dentro de una plantilla de rotación, con su grupo de pertenencia
/// y los grupos secundarios en los que puede colaborar como apoyo.
/// </summary>
public class PersonaTurno
{
    /// <summary>Identificador de la persona en el sistema.</summary>
    public required string PersonaId { get; set; }

    /// <summary>Nombre completo de la persona.</summary>
    public required string Nombre { get; set; }

    /// <summary>Número de orden dentro del grupo, usado para priorización y diagnóstico.</summary>
    public int Numero { get; set; }

    /// <summary>Nombre del grupo primario de la persona.</summary>
    public required string Grupo { get; set; }

    /// <summary>Identificador del grupo primario de la persona.</summary>
    public string GrupoId { get; set; } = string.Empty;

    /// <summary>Grupos secundarios en los que la persona puede trabajar como apoyo.</summary>
    public HashSet<string> GruposSecundarios { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public PersonaTurno()
    {
    }

    public PersonaTurno(string PersonaId, string Nombre, int Numero, string Grupo) : this()
    {
        this.PersonaId = PersonaId;
        this.Nombre = Nombre;
        this.Numero = Numero;
        this.Grupo = Grupo;
    }

    public override string ToString()
    {
        return $"Persona {Numero}: {Nombre}";
    }
}

/// <summary>
/// Plantilla de un equipo completo, que agrupa varios <see cref="GrupoEquipo"/> y
/// define la planificación auxiliar compartida entre ellos.
/// </summary>
public class EquipoPlantilla
{
    /// <summary>Nombre del equipo.</summary>
    public required string Nombre { get; set; }

    /// <summary>Lista de grupos que componen el equipo.</summary>
    public required List<GrupoEquipo> Grupos { get; set; }

    /// <summary>Configuración de turnos auxiliares compartidos entre grupos del equipo.</summary>
    public List<PlanificacionAuxiliarCompartida> PlanificacionAuxiliarCompartidaSemanal { get; set; } = new();

    public EquipoPlantilla()
    {
    }

    public EquipoPlantilla(string Nombre, List<GrupoEquipo> Grupos) : this()
    {
        this.Nombre = Nombre;
        this.Grupos = Grupos;
    }
    public override string ToString()
    {
        var resultado = $"Equipo: {Nombre}\n";
        foreach (var grupo in Grupos)
        {
            resultado += $"Grupo ID: {grupo.GrupoId}\n";
            resultado += "Personas:\n";
            foreach (var persona in grupo.Personas)
            {
                resultado += $"  - {persona}\n";
            }
            resultado += "Planificación Semanal:\n";
            foreach (var planificacion in grupo.PlanificacionSemanal)
            {
                resultado += $"  - Día: {planificacion.Dia}, TipoHorario: {planificacion.TipoHorario}, Cantidad: {planificacion.Cantidad}\n";
            }
        }
        return resultado;
    }
}

/// <summary>
/// Representa a un grupo dentro de un equipo, con sus personas, planificación semanal
/// y configuración de turnos auxiliares.
/// </summary>
public class GrupoEquipo
{
    /// <summary>Identificador del grupo.</summary>
    public required string GrupoId { get; set; }

    /// <summary>Personas que pertenecen al grupo.</summary>
    public required List<PersonaTurno> Personas { get; set; }

    /// <summary>Planificación de turnos regulares por día de la semana.</summary>
    public required List<PlanificacionTurno> PlanificacionSemanal { get; set; }

    /// <summary>Planificación de turnos auxiliares por día de la semana.</summary>
    public List<PlanificacionTurno> PlanificacionAuxiliarSemanal { get; set; } = new();

    /// <summary>Patrones de turno predefinidos para el algoritmo de blueprinting.</summary>
    public List<PatronTurnos>? PatronDeTurnos { get; set; }

    /// <summary>Historial de patrones de semana previos por persona, utilizado por el algoritmo de blueprinting.</summary>
    public Dictionary<string, List<HistorialPatronSemana>>? HistorialPatronesPorPersona { get; set; }

    /// <summary>Indica que este grupo rota personas de otros grupos como cobertura secundaria.</summary>
    public bool UsaGruposSecundarios { get; set; } = false;
}

/// <summary>
/// Registro histórico del patrón de turno aplicado a una persona en una semana específica.
/// Se usa para mejorar la continuidad en soluciones sucesivas.
/// </summary>
public class HistorialPatronSemana
{
    /// <summary>Fecha de inicio de la semana (lunes).</summary>
    public DateOnly SemanaInicio { get; set; }

    /// <summary>Número de semana ISO de la semana registrada.</summary>
    public int NumeroSemanaIso { get; set; }

    /// <summary>Año ISO al que pertenece el número de semana.</summary>
    public int AnioSemanaIso { get; set; }

    /// <summary>Nombre del patrón de turno aplicado esa semana; <see langword="null"/> si no se aplicó patrón.</summary>
    public string? PatronNombre { get; set; }

    /// <summary>Similitud medida entre el patrón y la solución real de esa semana (0.0–1.0).</summary>
    public double Similitud { get; set; }

    /// Inicio real del último turno de esa semana según RegistroTurnos (puede diferir del patrón por cambios manuales).
    public DateTime? UltimoTurnoInicioReal { get; set; }

    /// Fin real del último turno de esa semana según RegistroTurnos (puede diferir del patrón por cambios manuales).
    public DateTime? UltimoTurnoFinReal { get; set; }
}

/// <summary>
/// Define la demanda de personal para un turno en un día específico de la semana,
/// incluyendo cantidades de apoyo y flags de comportamiento especial.
/// </summary>
public class PlanificacionTurno
{
    /// <summary>Día de la semana al que aplica esta entrada (por ejemplo, <c>"Lunes"</c>).</summary>
    public required string Dia { get; set; }

    /// <summary>Código del tipo de horario (por ejemplo, <c>"M"</c>, <c>"T"</c>, <c>"N"</c>).</summary>
    public required string TipoHorario { get; set; }

    /// <summary>Número de empleados regulares requeridos en este turno y día.</summary>
    public int Cantidad { get; set; }

    /// <summary>Número mínimo requerido del turno cuando la cobertura es flexible.</summary>
    public int CantidadMinima { get; set; }

    /// <summary>Número de empleados de apoyo adicionales permitidos para ceder a otros grupos.</summary>
    public int CantidadApoyo { get; set; }

    /// <summary>Indica si este turno puede omitirse cuando hay vacaciones activas en el grupo.</summary>
    public bool PuedeOmitirsePorVacacion { get; set; }

    /// <summary>
    /// Número mínimo de personas requerido cuando el turno nocturno flexible opera en modo
    /// de nocturnos consecutivos. Corresponde a <c>min_personas</c> de la planificación.
    /// </summary>
    public int MinimoFlexible { get; set; }

    /// <summary>Indica si este turno es opcional para fines de vacación (no es un turno de demanda obligatoria).</summary>
    public bool EsOpcional { get; set; } = false;

    /// <summary>Fecha y hora de inicio del turno (incluye la hora específica del día).</summary>
    public DateTime Inicio { get; set; }

    /// <summary>Fecha y hora de fin del turno.</summary>
    public DateTime Fin { get; set; }
}

/// <summary>
/// Configura un turno auxiliar compartido entre grupos del mismo equipo,
/// con un límite global de capacidad aplicado a todos los grupos que lo comparten.
/// </summary>
public class PlanificacionAuxiliarCompartida
{
    /// <summary>Clave que identifica el pool de capacidad compartida entre grupos.</summary>
    public required string SharedKey { get; set; }

    /// <summary>Día de la semana al que aplica la planificación auxiliar compartida.</summary>
    public required string Dia { get; set; }

    /// <summary>Código del tipo de horario del turno auxiliar compartido.</summary>
    public required string TipoHorario { get; set; }

    /// <summary>Capacidad total máxima del pool de auxiliares compartidos para ese día.</summary>
    public int Cantidad { get; set; }

    /// <summary>Identificadores de grupos que pueden aportar empleados a este pool de auxiliares.</summary>
    public HashSet<string> GruposPermitidos { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Patrón de turnos predefinido para el algoritmo de blueprinting,
/// que representa una secuencia típica de días de trabajo.
/// </summary>
public class PatronTurnos
{
    /// <summary>Nombre descriptivo del patrón.</summary>
    public required string Nombre { get; set; }

    /// <summary>Lista de días de trabajo incluidos en el patrón.</summary>
    public required List<PlanificacionTurno> DiasTrabajo { get; set; }
}

/// <summary>
/// Representa una ocurrencia concreta de un turno dentro de una semana expandida,
/// con las personas asignadas y los detalles de horario y configuración.
/// </summary>
public class Turno
{
    /// <summary>Número de turno en la plantilla, usado como identificador secuencial.</summary>
    public int NumeroTurno { get; set; }

    /// <summary>Identificador del grupo al que pertenece el turno.</summary>
    public string GrupoId { get; set; } = string.Empty;

    /// <summary>Día de la semana del turno.</summary>
    public required string Dia { get; set; }

    /// <summary>Código del tipo de horario del turno.</summary>
    public required string TipoHorario { get; set; }

    /// <summary>Identificador del tipo de turno en el sistema.</summary>
    public required string TipoTurnoId { get; set; }

    /// <summary>Fecha y hora de inicio del turno.</summary>
    public DateTime Inicio { get; set; }

    /// <summary>Fecha y hora de fin del turno.</summary>
    public DateTime Fin { get; set; }

    /// <summary>Personas asignadas a este turno en la solución.</summary>
    public required List<PersonaTurno> PersonaTurnoTurno { get; set; }

    /// <summary>Número mínimo de personas requeridas para cubrir el turno.</summary>
    public int MinimoPersTurno { get; set; }

    /// <summary>Capacidad total planificada, incluyendo posibles personas de apoyo.</summary>
    public int CapacidadPlanificada { get; set; }

    /// <summary>Número máximo de personas que pueden cederse como apoyo a otro grupo en la misma fecha.</summary>
    public int MaximoApoyoCedible { get; set; }

    /// <summary>Indica si el turno es auxiliar (no computa para el objetivo de horas del grupo principal).</summary>
    public bool IsAuxiliar { get; set; }

    /// <summary>Indica si el turno fue creado para reemplazar a alguien en vacaciones.</summary>
    public bool EsReemplazoVacacion { get; set; }

    /// <summary>Indica si el turno puede suprimirse cuando hay vacaciones activas en el grupo y la fecha.</summary>
    public bool PuedeOmitirsePorVacacion { get; set; }

    /// <summary>
    /// Número mínimo de personas requerido cuando el turno nocturno flexible opera en modo
    /// de nocturnos consecutivos. Corresponde a <c>min_personas</c> de la planificación.
    /// </summary>
    public int MinimoFlexible { get; set; }

    /// <summary>Clave del pool de capacidad compartida para turnos auxiliares entre grupos.</summary>
    public string AuxiliarSharedKey { get; set; } = string.Empty;

    /// <summary>Capacidad máxima del pool auxiliar compartido identificado por <see cref="AuxiliarSharedKey"/>.</summary>
    public int AuxiliarMaxCompartido { get; set; }

    /// <summary>Indica si el turno es opcional para fines de cobertura de vacaciones.</summary>
    public bool EsOpcional { get; set; } = false;

    /// <summary>Minutos de trabajo efectivo que computan para el objetivo semanal de horas.</summary>
    public int MinutosTrabajoComputables { get; set; }

    public Turno()
    {
    }

    public override string ToString()
    {
        var tipo = IsAuxiliar ? "Aux" : "Normal";
        var reemplazo = EsReemplazoVacacion ? ", ReemplazoVacacion" : string.Empty;
        var apoyo = MaximoApoyoCedible > 0 ? $", Apoyo={MaximoApoyoCedible}" : string.Empty;
        var opcionalVacacion = PuedeOmitirsePorVacacion ? ", OpcionalVacacion" : string.Empty;
        return $"Turno {NumeroTurno}: {Dia} {TipoHorario} ({Inicio:HH:mm}-{Fin:HH:mm}), Tipo={tipo}{reemplazo}{apoyo}{opcionalVacacion}, MinPers: {MinimoPersTurno}, Asignados: [{string.Join(", ", PersonaTurnoTurno.Select(p => p.Nombre))}]";
    }
}

/// <summary>
/// Resultado final de la rotación en formato de lista de asignaciones por número de turno.
/// Cada entrada del diccionario mapea el número de turno a los turnos con sus empleados asignados.
/// </summary>
public class SolucionRotacion
{
    /// <summary>
    /// Mapa de número de turno a la lista de turnos con los empleados asignados en la solución.
    /// </summary>
    public required Dictionary<int, List<Turno>> ListaAsignaciones { get; set; }

    public SolucionRotacion()
    {
    }

    public override string ToString()
    {
        var resultado = "Solución de Rotación:\n";
        foreach (var asignacion in ListaAsignaciones)
        {
            resultado += $"ID Asignación: {asignacion.Key}\n";
            foreach (var turno in asignacion.Value)
            {
                resultado += turno.ToString() + "\n";
            }
            resultado += "\n";
        }
        return resultado;
    }
}



///////////////////////////////////// MODELOS PARA PLANTILLA DE ROTACIÓN ////////////////////////////////////

/// <summary>
/// Registra la asignación de un patrón de turno a una persona para una semana específica,
/// incluyendo los días de trabajo resultantes.
/// </summary>
public class AsignacionPersonaPatron
{
    /// <summary>Identificador de la persona.</summary>
    public required string PersonaId { get; set; }

    /// <summary>Nombre de la persona.</summary>
    public required string PersonaNombre { get; set; }

    /// <summary>Número de orden de la persona dentro del grupo.</summary>
    public required int PersonaNumero { get; set; }

    /// <summary>Nombre del patrón de turno asignado a la persona para esa semana.</summary>
    public required string PatronNombre { get; set; }

    /// <summary>Número de semana dentro del horizonte al que pertenece la asignación.</summary>
    public required int NumeroSemana { get; set; }

    /// <summary>Lista de días de trabajo resultantes de aplicar el patrón a la persona.</summary>
    public required List<PlanificacionTurno> DiasTrabajo { get; set; }
}

/// <summary>
/// Resultado de la rotación mediante patrones para un grupo, organizado por semana.
/// </summary>
public class SolucionRotacionPatron
{
    /// <summary>Identificador del grupo al que corresponde la solución.</summary>
    public required string GrupoId { get; set; }

    /// <summary>Asignaciones de patrones por semana: clave = número de semana, valor = lista de asignaciones de personas.</summary>
    public required Dictionary<int, List<AsignacionPersonaPatron>> AsignacionesPorSemana { get; set; }
}

/// <summary>
/// Persona de un grupo reemplazante, pre-autorizada para cubrir un slot (Dia, TipoTurnoId)
/// específico del grupo principal cuando hay déficit por vacaciones.
/// </summary>
public class PersonaReemplazante
{
    /// <summary>Identificador de la persona reemplazante.</summary>
    public required string PersonaId { get; set; }

    /// <summary>Nombre de la persona reemplazante.</summary>
    public required string Nombre { get; set; }

    /// <summary>Identificador del grupo de origen del reemplazante.</summary>
    public required string GrupoOrigenId { get; set; }

    /// <summary>Nombre del grupo de origen del reemplazante.</summary>
    public required string GrupoOrigenNombre { get; set; }

    /// <summary>TipoTurnoId del slot que esta persona puede cubrir.</summary>
    public required string TipoTurnoId { get; set; }

    /// <summary>Día normalizado (ej. "Lunes") del slot que esta persona puede cubrir.</summary>
    public required string Dia { get; set; }
}

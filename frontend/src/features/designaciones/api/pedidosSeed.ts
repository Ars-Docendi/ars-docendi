// ============================================================
// Datos iniciales (seed) del mock de pedidos de designación.
// Hidratan el store la primera vez que no hay nada en localStorage.
// Representan el período abierto del Jefe de Cátedra Gustavo Ruiz
// (cátedra "Ingeniería de Software", carrera "Ingeniería en Informática"):
// docentes del período anterior precargados como "Sin novedad" + algunos
// ejemplos en estados del lado del JC para que "Mis pedidos" se vea real.
// ============================================================
import type {
  Adjunto,
  AsignacionMateria,
  EstadoPedido,
  EventoHistorial,
  Novedad,
  PedidoDesignacion,
  Rol,
} from "../types";

/** Período abierto sobre el que se cargan los pedidos (FK a periodosMock id "1"). */
export const PERIODO_ABIERTO_ID = "1";

const CATEDRA = "Ingeniería de Software";
const CARRERA = "Ingeniería en Informática";
const JC_NOMBRE = "G. Ruiz";

let contadorId = 0;
function siguienteId(prefijo: string): string {
  contadorId += 1;
  return `${prefijo}-${contadorId}`;
}

/** Número de trámite legible ("N°-AAAA-NNNN"). En el real lo asigna el backend. */
const ANIO_NUMERO = 2026;
let contadorNumero = 0;
function siguienteNumero(): string {
  contadorNumero += 1;
  return `N°-${ANIO_NUMERO}-${String(123 + contadorNumero).padStart(4, "0")}`;
}

interface SemillaPedido {
  dni: string;
  nombre: string;
  /** Legajo institucional. Ausente en una Alta: el docente todavía no existe en el sistema. */
  legajo?: string;
  antiguedad: number;
  asignaciones: AsignacionMateria[];
  cargoActual: PedidoDesignacion["cargoActual"];
  dedicacionActual: PedidoDesignacion["dedicacionActual"];
  novedad: Novedad;
  cargoSolicitado?: PedidoDesignacion["cargoSolicitado"];
  dedicacionSolicitada?: PedidoDesignacion["dedicacionSolicitada"];
  justificacion?: string;
  tipoBaja?: PedidoDesignacion["tipoBaja"];
  tipoBajaDetalle?: string;
  adjuntos?: Adjunto[];
  estado: PedidoDesignacion["estado"];
  prioritario?: boolean;
  horasExternas?: number;
  horasInvestigacion?: number;
  etapaRetorno?: PedidoDesignacion["etapaRetorno"];
  propietarioActual?: PedidoDesignacion["propietarioActual"];
  // Overrides de ámbito: por defecto la cátedra/carrera del JC Gustavo Ruiz.
  carrera?: string;
  catedra?: string;
}

function eventoCreacion(): EventoHistorial {
  return {
    id: siguienteId("ev"),
    accion: "crear",
    porRol: "Jefe de Cátedra",
    porNombre: JC_NOMBRE,
    etapa: "borrador",
    fecha: "2026-06-08T10:00:00.000Z",
  };
}

function eventoEnvio(): EventoHistorial {
  return {
    id: siguienteId("ev"),
    accion: "enviar",
    porRol: "Jefe de Cátedra",
    porNombre: JC_NOMBRE,
    etapa: "en_revision_coordinador",
    fecha: "2026-06-12T09:30:00.000Z",
  };
}

function eventoDevolucion(): EventoHistorial {
  return {
    id: siguienteId("ev"),
    accion: "devolver",
    porRol: "Coordinador",
    porNombre: "M. Díaz",
    etapa: "en_revision_coordinador",
    comentario: "Falta adjuntar la justificación del cambio de dedicación.",
    fecha: "2026-06-19T15:10:00.000Z",
  };
}

function eventoAceptacion(
  porRol: Rol,
  porNombre: string,
  etapaResultante: EstadoPedido,
  fecha: string,
): EventoHistorial {
  return {
    id: siguienteId("ev"),
    accion: "aceptar",
    porRol,
    porNombre,
    etapa: etapaResultante,
    fecha,
  };
}

function eventoRechazo(): EventoHistorial {
  return {
    id: siguienteId("ev"),
    accion: "rechazar",
    porRol: "Coordinador",
    porNombre: "M. Díaz",
    etapa: "rechazado",
    comentario: "El cargo solicitado excede el cupo de la cátedra para el período.",
    fecha: "2026-06-16T11:00:00.000Z",
  };
}

const ACEPTA_COORD = (): EventoHistorial =>
  eventoAceptacion("Coordinador", "M. Díaz", "en_revision_secretaria", "2026-06-15T10:00:00.000Z");
const ACEPTA_SECRE = (): EventoHistorial =>
  eventoAceptacion(
    "Secretaría",
    "L. Fernández",
    "en_revision_decanato",
    "2026-06-17T09:00:00.000Z",
  );
const ACEPTA_DECANO = (): EventoHistorial =>
  eventoAceptacion("Decanato", "R. Sosa", "en_lote", "2026-06-18T16:00:00.000Z");

/** Reconstruye un historial coherente para el estado objetivo del seed. */
function historialPara(estado: EstadoPedido): EventoHistorial[] {
  const historial: EventoHistorial[] = [eventoCreacion()];
  switch (estado) {
    case "en_revision_coordinador":
      historial.push(eventoEnvio());
      break;
    case "en_revision_secretaria":
      historial.push(eventoEnvio(), ACEPTA_COORD());
      break;
    case "en_revision_decanato":
      historial.push(eventoEnvio(), ACEPTA_COORD(), ACEPTA_SECRE());
      break;
    case "en_lote":
      historial.push(eventoEnvio(), ACEPTA_COORD(), ACEPTA_SECRE(), ACEPTA_DECANO());
      break;
    case "rechazado":
      historial.push(eventoEnvio(), eventoRechazo());
      break;
    case "devuelto":
      historial.push(eventoEnvio(), eventoDevolucion());
      break;
    default:
      break; // borrador / cancelado: solo la creación
  }
  return historial;
}

function desdeSemilla(semilla: SemillaPedido): PedidoDesignacion {
  const historial = historialPara(semilla.estado);
  return {
    id: siguienteId("ped"),
    numero: siguienteNumero(),
    periodoId: PERIODO_ABIERTO_ID,
    catedra: semilla.catedra ?? CATEDRA,
    carrera: semilla.carrera ?? CARRERA,
    docente: {
      dni: semilla.dni,
      nombre: semilla.nombre,
      antiguedad: semilla.antiguedad,
      legajo: semilla.legajo,
    },
    asignaciones: semilla.asignaciones,
    cargoActual: semilla.cargoActual,
    dedicacionActual: semilla.dedicacionActual,
    novedad: semilla.novedad,
    cargoSolicitado: semilla.cargoSolicitado,
    dedicacionSolicitada: semilla.dedicacionSolicitada,
    justificacion: semilla.justificacion,
    tipoBaja: semilla.tipoBaja,
    tipoBajaDetalle: semilla.tipoBajaDetalle,
    horasExternas: semilla.horasExternas ?? 0,
    horasInvestigacion: semilla.horasInvestigacion ?? 0,
    adjuntos: semilla.adjuntos ?? [],
    estado: semilla.estado,
    prioritario: semilla.prioritario ?? false,
    etapaRetorno: semilla.etapaRetorno,
    propietarioActual: semilla.propietarioActual,
    historial,
  };
}

const SEMILLAS: SemillaPedido[] = [
  // Cátedra "Ingeniería de Software" del JC de prueba: 11 ejemplos cubriendo los 7 estados
  // posibles, para tener variedad al probar (filtros, columnas, acciones por estado).
  // Un borrador editable, que se puede enviar.
  {
    dni: "27345678",
    nombre: "Laura Giménez",
    legajo: "1002",
    antiguedad: 12,
    asignaciones: [{ materia: "Ingeniería de Software", horas: 8 }],
    cargoActual: "Titular",
    dedicacionActual: "Categoría 2",
    novedad: "Sin novedad",
    estado: "borrador",
  },
  // Un Cambio de dedicación ya enviado a revisión (read-only para el JC).
  {
    dni: "28776655",
    nombre: "Valeria Suárez",
    legajo: "1005",
    antiguedad: 9,
    asignaciones: [{ materia: "Ingeniería de Software", horas: 6 }],
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 4",
    novedad: "Cambio de cargo o dedicación",
    cargoSolicitado: "Adjunto",
    dedicacionSolicitada: "Categoría 3",
    justificacion: "Mayor carga de investigación asignada para el ciclo 2026.",
    horasInvestigacion: 3,
    estado: "en_revision_coordinador",
  },
  // Un Cambio devuelto por el Coordinador (vuelve editable al JC, se puede reenviar).
  {
    dni: "31445566",
    nombre: "Pablo Herrera",
    legajo: "1006",
    antiguedad: 6,
    asignaciones: [{ materia: "Algoritmos y Estructuras de Datos", horas: 6 }],
    cargoActual: "JTP",
    dedicacionActual: "Categoría 4",
    novedad: "Cambio de cargo o dedicación",
    cargoSolicitado: "Adjunto",
    dedicacionSolicitada: "Categoría 3",
    estado: "devuelto",
    etapaRetorno: "en_revision_coordinador",
    propietarioActual: "Jefe de Cátedra",
  },
  // Rechazado (terminal, de solo lectura).
  {
    dni: "32110099",
    nombre: "Brenda Ortiz",
    antiguedad: 2,
    asignaciones: [{ materia: "Algoritmos y Estructuras de Datos", horas: 4 }],
    cargoActual: null,
    dedicacionActual: null,
    novedad: "Alta",
    cargoSolicitado: "Ayudante",
    dedicacionSolicitada: "Categoría 5",
    adjuntos: [
      { id: "adj-cv-brenda", nombre: "cv-brenda-ortiz.pdf", tipo: "cv" },
      { id: "adj-dnif-brenda", nombre: "dni-frente.jpg", tipo: "dni_frente" },
      { id: "adj-dnid-brenda", nombre: "dni-dorso.jpg", tipo: "dni_dorso" },
    ],
    estado: "rechazado",
  },
  // Otro borrador "Sin novedad" (mismos datos que su entrada en el catálogo de docentes).
  {
    dni: "30987654",
    nombre: "Diego Morales",
    legajo: "1003",
    antiguedad: 7,
    asignaciones: [{ materia: "Ingeniería de Software", horas: 6 }],
    cargoActual: "JTP",
    dedicacionActual: "Categoría 4",
    novedad: "Sin novedad",
    horasExternas: 2,
    estado: "borrador",
  },
  // Una Baja en borrador (único ejemplo de esta novedad en el seed).
  {
    dni: "33112233",
    nombre: "Sofía Romano",
    legajo: "1004",
    antiguedad: 4,
    asignaciones: [{ materia: "Algoritmos y Estructuras de Datos", horas: 4 }],
    cargoActual: "Ayudante",
    dedicacionActual: "Categoría 5",
    novedad: "Baja",
    tipoBaja: "Renuncia",
    adjuntos: [
      { id: "adj-just-sofia", nombre: "renuncia-sofia-romano.pdf", tipo: "justificativo" },
    ],
    estado: "borrador",
  },
  // Un Cambio de cargo en borrador (no está en el catálogo de docentes existentes).
  {
    dni: "29112233",
    nombre: "Martín Acosta",
    legajo: "1008",
    antiguedad: 10,
    asignaciones: [{ materia: "Bases de Datos", horas: 6 }],
    cargoActual: "Titular",
    dedicacionActual: "Categoría 1",
    novedad: "Cambio de cargo o dedicación",
    cargoSolicitado: "Titular",
    dedicacionSolicitada: "Categoría 0",
    justificacion: "Mayor dedicación por dirección de proyectos de investigación.",
    estado: "borrador",
  },
  // Otro Cambio en en_revision_coordinador (mismos datos que su entrada en el catálogo).
  {
    dni: "28341567",
    nombre: "Lucía Fernández",
    legajo: "1001",
    antiguedad: 8,
    asignaciones: [{ materia: "Programación I", horas: 6 }],
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Cambio de cargo o dedicación",
    cargoSolicitado: "Adjunto",
    dedicacionSolicitada: "Categoría 1",
    justificacion: "Aumento de horas de investigación asignadas para el ciclo 2026.",
    horasInvestigacion: 2,
    estado: "en_revision_coordinador",
  },
  // En revisión, etapa Secretaría (no está en el catálogo de docentes existentes).
  {
    dni: "31556644",
    nombre: "Florencia Cabrera",
    legajo: "1009",
    antiguedad: 5,
    asignaciones: [{ materia: "Sistemas Operativos", horas: 6 }],
    cargoActual: "JTP",
    dedicacionActual: "Categoría 4",
    novedad: "Cambio de cargo o dedicación",
    cargoSolicitado: "Adjunto",
    dedicacionSolicitada: "Categoría 2",
    justificacion: "Ampliación de responsabilidades en la cátedra.",
    estado: "en_revision_secretaria",
  },
  // En revisión, etapa Decanato (no está en el catálogo de docentes existentes).
  {
    dni: "30667788",
    nombre: "Hernán Vidal",
    legajo: "1010",
    antiguedad: 9,
    asignaciones: [{ materia: "Matemática Discreta", horas: 6 }],
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Cambio de cargo o dedicación",
    cargoSolicitado: "Titular",
    dedicacionSolicitada: "Categoría 1",
    justificacion: "Vacante de titular por jubilación del cargo actual.",
    estado: "en_revision_decanato",
  },
  // Aceptado, en_lote (terminal-prototipo) — mismos datos que su entrada en el catálogo.
  {
    dni: "27660011",
    nombre: "Gabriel Núñez",
    legajo: "1007",
    antiguedad: 11,
    asignaciones: [{ materia: "Ingeniería de Software", horas: 6 }],
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Cambio de cargo o dedicación",
    cargoSolicitado: "Adjunto",
    dedicacionSolicitada: "Categoría 1",
    justificacion: "Aumento de carga externa aprobado por el Departamento.",
    horasExternas: 4,
    estado: "en_lote",
  },
  // Otra carrera (Ingeniería Industrial): NO debe verlo el Coordinador de Informática [BR-009].
  {
    dni: "30445511",
    nombre: "Mariano Tévez",
    legajo: "2001",
    antiguedad: 6,
    asignaciones: [{ materia: "Física I", horas: 6 }],
    cargoActual: "JTP",
    dedicacionActual: "Categoría 4",
    novedad: "Cambio de cargo o dedicación",
    cargoSolicitado: "Adjunto",
    dedicacionSolicitada: "Categoría 3",
    justificacion: "Reasignación de carga horaria en la carrera de Industrial.",
    estado: "en_revision_coordinador",
    carrera: "Ingeniería Industrial",
    catedra: "Física I",
  },
];

/** Devuelve una copia fresca del seed (ids estables dentro de la sesión). */
export function crearSeedPedidos(): PedidoDesignacion[] {
  contadorId = 0;
  contadorNumero = 0;
  return SEMILLAS.map(desdeSemilla);
}

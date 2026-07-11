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
    docente: { dni: semilla.dni, nombre: semilla.nombre, antiguedad: semilla.antiguedad },
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
  // Precarga del período anterior como "Sin novedad" (borradores).
  {
    dni: "27345678",
    nombre: "Laura Giménez",
    antiguedad: 12,
    asignaciones: [{ materia: "Ingeniería de Software", horas: 8 }],
    cargoActual: "Titular",
    dedicacionActual: "Categoría 2",
    novedad: "Sin novedad",
    estado: "borrador",
  },
  {
    dni: "30987654",
    nombre: "Diego Morales",
    antiguedad: 7,
    asignaciones: [{ materia: "Ingeniería de Software", horas: 6 }],
    cargoActual: "JTP",
    dedicacionActual: "Categoría 4",
    novedad: "Sin novedad",
    estado: "borrador",
  },
  {
    dni: "33112233",
    nombre: "Sofía Romano",
    antiguedad: 4,
    asignaciones: [{ materia: "Algoritmos y Estructuras de Datos", horas: 4 }],
    cargoActual: "Ayudante",
    dedicacionActual: "Categoría 5",
    novedad: "Sin novedad",
    estado: "borrador",
  },
  // Un Alta en borrador con adjuntos cargados y múltiples materias (D3).
  {
    dni: "35998877",
    nombre: "Martín Acosta",
    antiguedad: 1,
    asignaciones: [
      { materia: "Ingeniería de Software", horas: 4 },
      { materia: "Programación II", horas: 4 },
    ],
    cargoActual: null,
    dedicacionActual: null,
    novedad: "Alta",
    cargoSolicitado: "Ayudante",
    dedicacionSolicitada: "Categoría 5",
    horasInvestigacion: 2,
    adjuntos: [
      { id: "adj-cv-seed", nombre: "cv-martin-acosta.pdf", tipo: "cv" },
      { id: "adj-dnif-seed", nombre: "dni-frente.jpg", tipo: "dni_frente" },
      { id: "adj-dnid-seed", nombre: "dni-dorso.jpg", tipo: "dni_dorso" },
    ],
    estado: "borrador",
    prioritario: true,
  },
  // Un Cambio de dedicación ya enviado a revisión (read-only para el JC).
  {
    dni: "28776655",
    nombre: "Valeria Suárez",
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
  // Un Cambio devuelto por el Coordinador (vuelve editable al JC).
  {
    dni: "31445566",
    nombre: "Pablo Herrera",
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
  // Una Baja con múltiples materias asignadas (mismo docente del catálogo, 2 materias).
  {
    dni: "28341567",
    nombre: "Lucía Fernández",
    antiguedad: 8,
    asignaciones: [
      { materia: "Programación I", horas: 6 },
      { materia: "Ingeniería de Software", horas: 4 },
    ],
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Baja",
    tipoBaja: "Renuncia",
    justificacion:
      "Renuncia del docente por traslado a otra institución. Cese efectivo al fin del cuatrimestre.",
    adjuntos: [
      { id: "adj-just-lucia", nombre: "renuncia-lucia-fernandez.pdf", tipo: "justificativo" },
    ],
    estado: "en_revision_coordinador",
  },
  // --- SCRUM-8: pedidos en etapas de revisión para que cada tablero se vea real ---
  // En revisión de Secretaría (ya aceptado por el Coordinador).
  {
    dni: "29554433",
    nombre: "Florencia Cabrera",
    antiguedad: 8,
    asignaciones: [{ materia: "Ingeniería de Software", horas: 6 }],
    cargoActual: "JTP",
    dedicacionActual: "Categoría 3",
    novedad: "Cambio de cargo o dedicación",
    cargoSolicitado: "Adjunto",
    dedicacionSolicitada: "Categoría 2",
    justificacion: "Promoción por concurso docente cerrado en 2025.",
    estado: "en_revision_secretaria",
  },
  // En revisión de Decanato (aceptado por Coordinador y Secretaría), prioritario.
  {
    dni: "26889900",
    nombre: "Hernán Vidal",
    antiguedad: 15,
    asignaciones: [{ materia: "Ingeniería de Software", horas: 8 }],
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 2",
    novedad: "Cambio de cargo o dedicación",
    cargoSolicitado: "Titular",
    dedicacionSolicitada: "Categoría 1",
    justificacion: "Cobertura del cargo de Titular vacante por jubilación.",
    estado: "en_revision_decanato",
    prioritario: true,
  },
  // Rechazado (terminal) — alimenta la columna "Rechazado".
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
  // En lote (terminal-prototipo, aprobado por toda la cadena) — columna "Aprobado".
  {
    dni: "27660011",
    nombre: "Gabriel Núñez",
    antiguedad: 11,
    asignaciones: [{ materia: "Ingeniería de Software", horas: 6 }],
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Sin novedad",
    estado: "en_lote",
  },
  // Otra carrera (Ingeniería Industrial): NO debe verlo el Coordinador de Informática [BR-009].
  {
    dni: "30445511",
    nombre: "Mariano Tévez",
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

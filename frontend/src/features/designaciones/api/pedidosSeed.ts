// ============================================================
// Datos iniciales (seed) del mock de pedidos de designación.
// Hidratan el store la primera vez que no hay nada en localStorage.
// Representan el período abierto del Jefe de Cátedra Gustavo Ruiz
// (cátedra "Ingeniería de Software", carrera "Ingeniería en Informática"):
// ejemplos de Alta/Baja/Cambio en varios estados para que "Mis pedidos" se vea
// real (la novedad "Sin novedad" ya no existe en el sistema — ver
// `ajustes-pedido-y-revision` — así que no hay precarga automática).
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
  esAgenteExterno?: boolean;
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
    esAgenteExterno: semilla.esAgenteExterno ?? false,
    adjuntos: semilla.adjuntos ?? [],
    estado: semilla.estado,
    prioritario: semilla.prioritario ?? false,
    etapaRetorno: semilla.etapaRetorno,
    propietarioActual: semilla.propietarioActual,
    historial,
  };
}

const SEMILLAS: SemillaPedido[] = [
  // Cátedra "Ingeniería de Software" del JC de prueba: cubre los 7 estados posibles, para tener
  // variedad al probar (filtros, columnas, acciones por estado) — incluye varios casos de Alta y
  // Baja en distintas etapas del circuito, que originalmente eran muy pocos (solo 1 cada uno).
  // Un borrador editable, que se puede enviar.
  {
    dni: "27345678",
    nombre: "Laura Giménez",
    legajo: "1002",
    antiguedad: 12,
    asignaciones: [{ materia: "Ingeniería de Software", horas: 8 }],
    cargoActual: "Titular",
    dedicacionActual: "Categoría 2",
    novedad: "Cambio de cargo o dedicación",
    cargoSolicitado: "Titular",
    dedicacionSolicitada: "Categoría 1",
    justificacion: "Mayor dedicación por continuidad en proyectos de la cátedra.",
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
  // Otro Cambio en borrador (mismos datos que su entrada en el catálogo de docentes).
  {
    dni: "30987654",
    nombre: "Diego Morales",
    legajo: "1003",
    antiguedad: 7,
    asignaciones: [{ materia: "Ingeniería de Software", horas: 6 }],
    cargoActual: "JTP",
    dedicacionActual: "Categoría 4",
    novedad: "Cambio de cargo o dedicación",
    cargoSolicitado: "Adjunto",
    dedicacionSolicitada: "Categoría 3",
    justificacion: "Ascenso de cargo por antigüedad en la cátedra.",
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
  // --- Altas y Bajas adicionales, en varias etapas del circuito (eran muy pocos casos) ---
  // Alta en revisión, etapa Coordinador.
  {
    dni: "34221100",
    nombre: "Ignacio Paz",
    antiguedad: 0,
    asignaciones: [{ materia: "Redes de Computadoras", horas: 4 }],
    cargoActual: null,
    dedicacionActual: null,
    novedad: "Alta",
    cargoSolicitado: "Ayudante",
    dedicacionSolicitada: "Categoría 5",
    justificacion: "Cobertura de la comisión nueva del turno noche.",
    adjuntos: [
      { id: "adj-cv-ignacio", nombre: "cv-ignacio-paz.pdf", tipo: "cv" },
      { id: "adj-dnif-ignacio", nombre: "dni-frente.jpg", tipo: "dni_frente" },
      { id: "adj-dnid-ignacio", nombre: "dni-dorso.jpg", tipo: "dni_dorso" },
    ],
    estado: "en_revision_coordinador",
  },
  // Alta en revisión, etapa Secretaría.
  {
    dni: "34556677",
    nombre: "Carla Beltrán",
    antiguedad: 0,
    asignaciones: [{ materia: "Bases de Datos", horas: 4 }],
    cargoActual: null,
    dedicacionActual: null,
    novedad: "Alta",
    cargoSolicitado: "JTP",
    dedicacionSolicitada: "Categoría 4",
    justificacion: "Refuerzo de la cátedra por licencia de un docente titular.",
    adjuntos: [
      { id: "adj-cv-carla", nombre: "cv-carla-beltran.pdf", tipo: "cv" },
      { id: "adj-dnif-carla", nombre: "dni-frente.jpg", tipo: "dni_frente" },
      { id: "adj-dnid-carla", nombre: "dni-dorso.jpg", tipo: "dni_dorso" },
    ],
    estado: "en_revision_secretaria",
  },
  // Alta devuelta por Secretaría, esperando corrección del JC (queda en la sección "En Decanato").
  {
    dni: "34881122",
    nombre: "Rodrigo Funes",
    antiguedad: 0,
    asignaciones: [{ materia: "Sistemas Operativos", horas: 4 }],
    cargoActual: null,
    dedicacionActual: null,
    novedad: "Alta",
    cargoSolicitado: "Ayudante",
    dedicacionSolicitada: "Categoría 6",
    justificacion: "Cobertura de comisión de laboratorio.",
    adjuntos: [
      { id: "adj-cv-rodrigo", nombre: "cv-rodrigo-funes.pdf", tipo: "cv" },
      { id: "adj-dnif-rodrigo", nombre: "dni-frente.jpg", tipo: "dni_frente" },
    ],
    estado: "devuelto",
    etapaRetorno: "en_revision_decanato",
    propietarioActual: "Secretaría",
  },
  // Alta aceptada, en_lote (queda en "Finalizados").
  {
    dni: "34009988",
    nombre: "Melina Suárez",
    antiguedad: 0,
    asignaciones: [{ materia: "Matemática Discreta", horas: 4 }],
    cargoActual: null,
    dedicacionActual: null,
    novedad: "Alta",
    cargoSolicitado: "Ayudante",
    dedicacionSolicitada: "Categoría 5",
    justificacion: "Nueva comisión aprobada para el segundo cuatrimestre.",
    adjuntos: [
      { id: "adj-cv-melina", nombre: "cv-melina-suarez.pdf", tipo: "cv" },
      { id: "adj-dnif-melina", nombre: "dni-frente.jpg", tipo: "dni_frente" },
      { id: "adj-dnid-melina", nombre: "dni-dorso.jpg", tipo: "dni_dorso" },
    ],
    estado: "en_lote",
  },
  // Baja en revisión, etapa Coordinador.
  {
    dni: "35112200",
    nombre: "Esteban Roldán",
    legajo: "1011",
    antiguedad: 15,
    asignaciones: [{ materia: "Programación I", horas: 6 }],
    cargoActual: "Titular",
    dedicacionActual: "Categoría 1",
    novedad: "Baja",
    tipoBaja: "Jubilación",
    adjuntos: [
      { id: "adj-just-esteban", nombre: "jubilacion-esteban-roldan.pdf", tipo: "justificativo" },
    ],
    estado: "en_revision_coordinador",
  },
  // Baja en revisión, etapa Decanato.
  {
    dni: "35667788",
    nombre: "Patricia Núñez",
    legajo: "1012",
    antiguedad: 20,
    asignaciones: [{ materia: "Bases de Datos", horas: 6 }],
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Baja",
    tipoBaja: "Renuncia",
    adjuntos: [
      { id: "adj-just-patricia", nombre: "renuncia-patricia-nunez.pdf", tipo: "justificativo" },
    ],
    estado: "en_revision_decanato",
  },
  // Baja devuelta por el Coordinador, esperando corrección del JC (queda en "En Coordinación").
  {
    dni: "35998877",
    nombre: "Osvaldo Cabral",
    legajo: "1013",
    antiguedad: 18,
    asignaciones: [{ materia: "Sistemas Operativos", horas: 6 }],
    cargoActual: "JTP",
    dedicacionActual: "Categoría 4",
    novedad: "Baja",
    tipoBaja: "Otro",
    tipoBajaDetalle: "Cambio a otra unidad académica.",
    adjuntos: [
      { id: "adj-just-osvaldo", nombre: "detalle-osvaldo-cabral.pdf", tipo: "justificativo" },
    ],
    estado: "devuelto",
    etapaRetorno: "en_revision_coordinador",
    propietarioActual: "Jefe de Cátedra",
  },
  // Cambio devuelto Y prioritario a la vez (queda en "En Coordinación") — para ver la fila en rojo
  // (prioritario gana el fondo sobre devuelto) con los dos íconos juntos en la columna Prioritario.
  {
    dni: "36223311",
    nombre: "Verónica Salas",
    legajo: "1015",
    antiguedad: 13,
    asignaciones: [{ materia: "Bases de Datos", horas: 6 }],
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 4",
    novedad: "Cambio de cargo o dedicación",
    cargoSolicitado: "Adjunto",
    dedicacionSolicitada: "Categoría 2",
    justificacion: "Reasignación urgente por licencia de otro docente de la cátedra.",
    estado: "devuelto",
    etapaRetorno: "en_revision_coordinador",
    propietarioActual: "Jefe de Cátedra",
    prioritario: true,
  },
  // Baja rechazada (queda en "Finalizados").
  {
    dni: "35334455",
    nombre: "Nora Aguirre",
    legajo: "1014",
    antiguedad: 25,
    asignaciones: [{ materia: "Matemática Discreta", horas: 6 }],
    cargoActual: "Titular",
    dedicacionActual: "Categoría 0",
    novedad: "Baja",
    tipoBaja: "Renuncia",
    adjuntos: [{ id: "adj-just-nora", nombre: "renuncia-nora-aguirre.pdf", tipo: "justificativo" }],
    estado: "rechazado",
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

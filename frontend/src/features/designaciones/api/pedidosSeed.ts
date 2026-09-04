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
  /**
   * Solo para `estado: "devuelto"`: la etapa desde la que se devolvió, que es
   * también a la que vuelve al reenviarse. Quién firma la devolución y quién
   * tiene que corregir se DERIVAN de acá según BR-014 — la semilla no los
   * declara, para que no pueda escribir una combinación que la máquina de
   * estados nunca produciría (p. ej. devuelto desde Secretaría y a la vez a
   * cargo de Secretaría).
   */
  etapaRetorno?: PedidoDesignacion["etapaRetorno"];
  /**
   * Reubica el historial en el tiempo, en días hacia atrás desde hoy: el `enviar`
   * a `diasDesdeEnvio` días y el último evento a `diasDesdeUltimoEvento`, con los
   * eventos del medio repartidos entre ambos. Sirve para que la Tabla de revisión
   * muestre contadores variados (un pedido recién enviado vs. uno trabado hace
   * meses) en vez de que todas las filas digan lo mismo. Sin estos campos, la
   * semilla usa las fechas fijas de junio 2026.
   */
  diasDesdeEnvio?: number;
  diasDesdeUltimoEvento?: number;
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

/**
 * Quién revisa cada etapa (firma la devolución) y quién tiene que corregir lo
 * devuelto desde ella [BR-014]: la devolución retrocede UN nivel, nunca salta
 * directo a la Cátedra — Decanato devuelve a Secretaría, Secretaría a
 * Coordinación, Coordinación a la Cátedra. Espejo de `ROL_DE_ETAPA` y
 * `PROPIETARIO_DEVOLUCION` en `maquinaEstados.ts`.
 */
const REVISOR_DE_ETAPA: Record<string, { rol: Rol; nombre: string }> = {
  en_revision_coordinador: { rol: "Coordinador", nombre: "M. Díaz" },
  en_revision_secretaria: { rol: "Secretaría", nombre: "L. Fernández" },
  en_revision_decanato: { rol: "Decanato", nombre: "R. Sosa" },
};

const CORRIGE_LO_DEVUELTO_DESDE: Record<string, Rol> = {
  en_revision_coordinador: "Jefe de Cátedra",
  en_revision_secretaria: "Coordinador",
  en_revision_decanato: "Secretaría",
};

function eventoDevolucion(etapaRetorno: EstadoPedido | undefined): EventoHistorial {
  const revisor = REVISOR_DE_ETAPA[etapaRetorno ?? "en_revision_coordinador"];
  return {
    id: siguienteId("ev"),
    accion: "devolver",
    porRol: revisor.rol,
    porNombre: revisor.nombre,
    etapa: etapaRetorno ?? "en_revision_coordinador",
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
function historialPara(estado: EstadoPedido, etapaRetorno?: EstadoPedido): EventoHistorial[] {
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
      historial.push(eventoEnvio(), eventoDevolucion(etapaRetorno));
      break;
    default:
      break; // borrador / cancelado: solo la creación
  }
  return historial;
}

const MS_POR_DIA = 24 * 60 * 60 * 1000;

/**
 * Reubica el historial en el tiempo dejando el `enviar` y el último evento a la
 * distancia pedida de hoy, y repartiendo los eventos del medio proporcionalmente
 * entre ambos. El `crear` queda 4 días antes del envío (el borrador siempre
 * existe antes de enviarse). Muta las fechas de `historial` en el lugar.
 */
function reubicarEnElTiempo(
  historial: EventoHistorial[],
  diasDesdeEnvio: number,
  diasDesdeUltimoEvento: number,
): void {
  const indiceEnvio = historial.findIndex((evento) => evento.accion === "enviar");
  if (indiceEnvio === -1) return;

  const hoy = Date.now();
  const tEnvio = hoy - diasDesdeEnvio * MS_POR_DIA;
  const tUltimo = hoy - diasDesdeUltimoEvento * MS_POR_DIA;
  const tramos = historial.length - 1 - indiceEnvio;

  historial[indiceEnvio].fecha = new Date(tEnvio).toISOString();
  for (let i = indiceEnvio + 1; i < historial.length; i += 1) {
    const avance = (i - indiceEnvio) / tramos;
    historial[i].fecha = new Date(tEnvio + (tUltimo - tEnvio) * avance).toISOString();
  }
  for (let i = 0; i < indiceEnvio; i += 1) {
    historial[i].fecha = new Date(tEnvio - (indiceEnvio - i) * 4 * MS_POR_DIA).toISOString();
  }
}

function desdeSemilla(semilla: SemillaPedido): PedidoDesignacion {
  const historial = historialPara(semilla.estado, semilla.etapaRetorno);
  if (semilla.diasDesdeEnvio !== undefined) {
    reubicarEnElTiempo(
      historial,
      semilla.diasDesdeEnvio,
      semilla.diasDesdeUltimoEvento ?? semilla.diasDesdeEnvio,
    );
  }
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
    // Derivado, no declarado: ver BR-014 en `CORRIGE_LO_DEVUELTO_DESDE`.
    propietarioActual: semilla.etapaRetorno
      ? CORRIGE_LO_DEVUELTO_DESDE[semilla.etapaRetorno]
      : undefined,
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
  // ============================================================
  // Muestrario para la Tabla de revisión vista desde Secretaría (cuenta Demo,
  // rol Secretaría): un ejemplo de cada caso que la grilla sabe representar, con
  // contadores de días variados para que el par Inicio / Últ. actualización se
  // lea (uno recién enviado, uno trabado hace meses, uno que circuló rápido).
  // ============================================================
  // (a) Prioritario puro, en la sección propia de Secretaría: le toca revisarlo Y es urgente.
  {
    dni: "36778899",
    nombre: "Ariel Bustos",
    legajo: "1016",
    antiguedad: 14,
    asignaciones: [{ materia: "Bases de Datos", horas: 6 }],
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Cambio de cargo o dedicación",
    cargoSolicitado: "Titular",
    dedicacionSolicitada: "Categoría 2",
    justificacion: "Cobertura urgente de la titularidad vacante de la cátedra.",
    estado: "en_revision_secretaria",
    prioritario: true,
    diasDesdeEnvio: 41,
    diasDesdeUltimoEvento: 2,
  },
  // (b) Devuelto A Secretaría: el chip dice "Devuelto — corregís vos" para este actor.
  {
    dni: "36889900",
    nombre: "Camila Ferreyra",
    legajo: "1017",
    antiguedad: 7,
    asignaciones: [{ materia: "Programación I", horas: 6 }],
    cargoActual: "JTP",
    dedicacionActual: "Categoría 4",
    novedad: "Cambio de cargo o dedicación",
    cargoSolicitado: "Adjunto",
    dedicacionSolicitada: "Categoría 3",
    justificacion: "Reasignación por apertura de comisión nueva.",
    estado: "devuelto",
    // Devuelto por Decanato: BR-014 hace que lo corrija Secretaría.
    etapaRetorno: "en_revision_decanato",
    diasDesdeEnvio: 68,
    diasDesdeUltimoEvento: 9,
  },
  // (c) Devuelto pero a OTRO (el Coordinador corrige): Secretaría lo ve, no lo toca.
  {
    dni: "36990011",
    nombre: "Emilia Pardo",
    legajo: "1018",
    antiguedad: 11,
    asignaciones: [{ materia: "Sistemas Operativos", horas: 6 }],
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Cambio de cargo o dedicación",
    cargoSolicitado: "Adjunto",
    dedicacionSolicitada: "Categoría 2",
    justificacion: "Ampliación de dedicación por dirección de tesinas.",
    estado: "devuelto",
    etapaRetorno: "en_revision_secretaria",
    diasDesdeEnvio: 52,
    diasDesdeUltimoEvento: 14,
  },
  // (d) Prioritario puro en Coordinación, recién enviado: el chip de Prioridad sin devolución,
  //     y el contador chico que contrasta con los de meses.
  {
    dni: "37001122",
    nombre: "Nicolás Ferrari",
    legajo: "1019",
    antiguedad: 3,
    asignaciones: [{ materia: "Redes de Computadoras", horas: 4 }],
    cargoActual: "Ayudante",
    dedicacionActual: "Categoría 5",
    novedad: "Cambio de cargo o dedicación",
    cargoSolicitado: "JTP",
    dedicacionSolicitada: "Categoría 4",
    justificacion: "Reemplazo urgente por licencia médica del JTP de la comisión.",
    estado: "en_revision_coordinador",
    prioritario: true,
    diasDesdeEnvio: 5,
    diasDesdeUltimoEvento: 5,
  },
  // (e) Trabado hace meses en Decanato: Inicio y Últ. actualización los dos grandes —
  //     el par que grita "nadie lo movió".
  {
    dni: "37112233",
    nombre: "Silvina Ocampo",
    legajo: "1020",
    antiguedad: 22,
    asignaciones: [{ materia: "Matemática Discreta", horas: 6 }],
    cargoActual: "Titular",
    dedicacionActual: "Categoría 1",
    novedad: "Baja",
    tipoBaja: "Jubilación",
    adjuntos: [
      { id: "adj-just-silvina", nombre: "jubilacion-silvina-ocampo.pdf", tipo: "justificativo" },
    ],
    estado: "en_revision_decanato",
    diasDesdeEnvio: 156,
    diasDesdeUltimoEvento: 121,
  },
  // (f) Aceptado Y prioritario: el chip de Prioridad también en Finalizados, y el contador
  //     congelado — Inicio dice "tardó 24 d" aunque el cierre haya sido hace una semana.
  {
    dni: "37223344",
    nombre: "Tomás Vera",
    legajo: "1021",
    antiguedad: 16,
    asignaciones: [{ materia: "Ingeniería de Software", horas: 8 }],
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 2",
    novedad: "Cambio de cargo o dedicación",
    cargoSolicitado: "Titular",
    dedicacionSolicitada: "Categoría 1",
    justificacion: "Promoción aprobada por concurso.",
    estado: "en_lote",
    prioritario: true,
    diasDesdeEnvio: 30,
    diasDesdeUltimoEvento: 6,
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

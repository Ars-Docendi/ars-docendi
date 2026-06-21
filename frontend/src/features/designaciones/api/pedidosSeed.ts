// ============================================================
// Datos iniciales (seed) del mock de pedidos de designación.
// Hidratan el store la primera vez que no hay nada en localStorage.
// Representan el período abierto del Jefe de Cátedra Gustavo Ruiz
// (cátedra "Ingeniería de Software", carrera "Ingeniería en Informática"):
// docentes del período anterior precargados como "Sin novedad" + algunos
// ejemplos en estados del lado del JC para que "Mis pedidos" se vea real.
// ============================================================
import type { Adjunto, EventoHistorial, Novedad, PedidoDesignacion } from "../types";

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

interface SemillaPedido {
  dni: string;
  nombre: string;
  antiguedad: number;
  materia: string;
  cargoActual: PedidoDesignacion["cargoActual"];
  dedicacionActual: PedidoDesignacion["dedicacionActual"];
  novedad: Novedad;
  cargoSolicitado?: PedidoDesignacion["cargoSolicitado"];
  dedicacionSolicitada?: PedidoDesignacion["dedicacionSolicitada"];
  justificacion?: string;
  adjuntos?: Adjunto[];
  estado: PedidoDesignacion["estado"];
  prioritario?: boolean;
  haceHorasOtroDepto?: boolean;
  etapaRetorno?: PedidoDesignacion["etapaRetorno"];
  propietarioActual?: PedidoDesignacion["propietarioActual"];
}

function eventoCreacion(): EventoHistorial {
  return {
    id: siguienteId("ev"),
    accion: "crear",
    porRol: "Jefe de Cátedra",
    porNombre: JC_NOMBRE,
    etapa: "borrador",
    fecha: "2026-03-02T10:00:00.000Z",
  };
}

function eventoEnvio(): EventoHistorial {
  return {
    id: siguienteId("ev"),
    accion: "enviar",
    porRol: "Jefe de Cátedra",
    porNombre: JC_NOMBRE,
    etapa: "en_revision_coordinador",
    fecha: "2026-03-05T09:30:00.000Z",
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
    fecha: "2026-03-07T15:10:00.000Z",
  };
}

function desdeSemilla(semilla: SemillaPedido): PedidoDesignacion {
  const historial: EventoHistorial[] = [eventoCreacion()];
  if (semilla.estado === "en_revision_coordinador") {
    historial.push(eventoEnvio());
  }
  if (semilla.estado === "devuelto") {
    historial.push(eventoEnvio(), eventoDevolucion());
  }
  return {
    id: siguienteId("ped"),
    periodoId: PERIODO_ABIERTO_ID,
    catedra: CATEDRA,
    carrera: CARRERA,
    docente: { dni: semilla.dni, nombre: semilla.nombre, antiguedad: semilla.antiguedad },
    materiaAsociada: semilla.materia,
    cargoActual: semilla.cargoActual,
    dedicacionActual: semilla.dedicacionActual,
    novedad: semilla.novedad,
    cargoSolicitado: semilla.cargoSolicitado,
    dedicacionSolicitada: semilla.dedicacionSolicitada,
    justificacion: semilla.justificacion,
    haceHorasOtroDepto: semilla.haceHorasOtroDepto ?? false,
    horasInvestigacion: 0,
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
    materia: "Ingeniería de Software",
    cargoActual: "Titular",
    dedicacionActual: "Categoría 2",
    novedad: "Sin novedad",
    estado: "borrador",
  },
  {
    dni: "30987654",
    nombre: "Diego Morales",
    antiguedad: 7,
    materia: "Ingeniería de Software",
    cargoActual: "JTP",
    dedicacionActual: "Categoría 4",
    novedad: "Sin novedad",
    estado: "borrador",
  },
  {
    dni: "33112233",
    nombre: "Sofía Romano",
    antiguedad: 4,
    materia: "Algoritmos y Estructuras de Datos",
    cargoActual: "Ayudante",
    dedicacionActual: "Categoría 5",
    novedad: "Sin novedad",
    estado: "borrador",
  },
  // Un Alta en borrador con adjuntos cargados.
  {
    dni: "35998877",
    nombre: "Martín Acosta",
    antiguedad: 1,
    materia: "Ingeniería de Software",
    cargoActual: null,
    dedicacionActual: null,
    novedad: "Alta",
    cargoSolicitado: "Ayudante",
    dedicacionSolicitada: "Categoría 5",
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
    materia: "Ingeniería de Software",
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 4",
    novedad: "Cambio de cargo o dedicación",
    cargoSolicitado: "Adjunto",
    dedicacionSolicitada: "Categoría 3",
    justificacion: "Mayor carga de investigación asignada para el ciclo 2026.",
    estado: "en_revision_coordinador",
  },
  // Un Cambio devuelto por el Coordinador (vuelve editable al JC).
  {
    dni: "31445566",
    nombre: "Pablo Herrera",
    antiguedad: 6,
    materia: "Algoritmos y Estructuras de Datos",
    cargoActual: "JTP",
    dedicacionActual: "Categoría 4",
    novedad: "Cambio de cargo o dedicación",
    cargoSolicitado: "Adjunto",
    dedicacionSolicitada: "Categoría 3",
    estado: "devuelto",
    etapaRetorno: "en_revision_coordinador",
    propietarioActual: "Jefe de Cátedra",
  },
];

/** Devuelve una copia fresca del seed (ids estables dentro de la sesión). */
export function crearSeedPedidos(): PedidoDesignacion[] {
  contadorId = 0;
  return SEMILLAS.map(desdeSemilla);
}

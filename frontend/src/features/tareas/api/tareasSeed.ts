// ============================================================
// Datos iniciales (seed) del mock de tareas. Hidratan el store la
// primera vez que no hay nada en localStorage. Las fechas se calculan
// relativas a "hoy" para que el semáforo de vencimiento (verde/amarillo/
// rojo por % de plazo transcurrido) se vea representativo en cualquier
// momento en que se corra la app, no solo el día que se escribió el seed.
// ============================================================
import type { ComentarioTarea, EventoHistorialTarea, Rol, Tarea } from "../types";
import { PROYECTO_TESTING_ID } from "./proyectosSeed";

const SECRETARIA = { nombre: "L. Fernández", rol: "Secretaría Académica" as Rol };
const DECANATO = { nombre: "R. Sosa", rol: "Decanato" as Rol };
const ADMINISTRACION = { nombre: "P. Gómez", rol: "Administrativo" as Rol };

const JEFE_CATEDRA = { nombre: "G. Ruiz", rol: "Jefe de Cátedra" as Rol };
const COORDINADOR = { nombre: "M. Díaz", rol: "Coordinador de Carrera" as Rol };
const DOCENTE = { nombre: "C. López", rol: "Docente" as Rol };

let contadorId = 0;
function siguienteId(): string {
  contadorId += 1;
  return `t-${contadorId}`;
}

let contadorNumero = 0;
function siguienteNumero(): number {
  contadorNumero += 1;
  return contadorNumero;
}

/** Fecha ISO (yyyy-mm-dd) a `dias` de hoy (negativo = pasado, positivo = futuro). */
function fechaRelativa(dias: number): string {
  const fecha = new Date();
  fecha.setDate(fecha.getDate() + dias);
  return fecha.toISOString().slice(0, 10);
}

function evento(
  accion: EventoHistorialTarea["accion"],
  actor: { nombre: string; rol: Rol },
  estado: Tarea["estado"],
  detalle?: string,
  hace = 0,
): EventoHistorialTarea {
  const fecha = new Date();
  fecha.setDate(fecha.getDate() - hace);
  return {
    id: crypto.randomUUID(),
    accion,
    porRol: actor.rol,
    porNombre: actor.nombre,
    estado,
    detalle,
    fecha: fecha.toISOString(),
  };
}

function comentario(actor: { nombre: string; rol: Rol }, texto: string, hace = 0): ComentarioTarea {
  const fecha = new Date();
  fecha.setDate(fecha.getDate() - hace);
  return {
    id: crypto.randomUUID(),
    autor: actor.nombre,
    rolAutor: actor.rol,
    texto,
    fecha: fecha.toISOString(),
  };
}

/** Construye el seed inicial de tareas — variedad de estados, prioridades y semáforo. */
export function crearSeedTareas(): Tarea[] {
  // IDs fijos (no autoincrementales) para las tres tareas que arman el
  // ejemplo de jerarquía padre/hijas multinivel — se referencian entre sí
  // por `tareaPadreId` antes de que las demás terminen de asignar sus ids.
  const idPadreMigracion = siguienteId();
  const idHijaCalificaciones = siguienteId();
  const idNietaValidacionNotas = siguienteId();

  const idPadron = siguienteId();
  const idNovedades = siguienteId();

  return [
    // Pendiente, recién creada — plazo largo, semáforo verde. Relacionada
    // con "Cargar novedades…" (acceso rápido entre ambas, sin jerarquía).
    {
      id: idPadron,
      numero: siguienteNumero(),
      titulo: "Actualizar el padrón de aulas disponibles",
      descripcion: "Relevar qué aulas quedaron libres tras el cierre de inscripciones.",
      fechaInicio: fechaRelativa(-1),
      fechaFin: fechaRelativa(19),
      prioridad: "media",
      tipo: "administrativa",
      estado: "pendiente",
      porcentajeAvance: 0,
      responsable: JEFE_CATEDRA,
      creadoPor: SECRETARIA,
      comentarios: [],
      historial: [evento("crear", SECRETARIA, "pendiente", undefined, 1)],
      tareasRelacionadasIds: [idNovedades],
    },
    // En curso, plazo a mitad de camino — semáforo amarillo.
    {
      id: idNovedades,
      numero: siguienteNumero(),
      titulo: "Cargar novedades de docentes en el sistema",
      descripcion: "Ingresar las altas/bajas informadas por las cátedras esta semana.",
      fechaInicio: fechaRelativa(-6),
      fechaFin: fechaRelativa(4),
      prioridad: "alta",
      tipo: "administrativa",
      estado: "en_curso",
      porcentajeAvance: 45,
      responsable: COORDINADOR,
      creadoPor: SECRETARIA,
      comentarios: [],
      historial: [
        evento("crear", SECRETARIA, "pendiente", undefined, 6),
        evento("cambiar_estado", COORDINADOR, "en_curso", undefined, 5),
        evento("editar_avance", COORDINADOR, "en_curso", "45%", 1),
      ],
      tareasRelacionadasIds: [idPadron],
    },
    // En curso, plazo casi vencido — semáforo rojo. Asociada al Proyecto
    // "Nuevo sistema de Ingeniería para Testing".
    {
      id: siguienteId(),
      numero: siguienteNumero(),
      titulo: "Confirmar disponibilidad del laboratorio de Redes",
      descripcion: "Coordinar con mantenimiento el estado de las PCs antes del examen.",
      fechaInicio: fechaRelativa(-9),
      fechaFin: fechaRelativa(1),
      prioridad: "alta",
      tipo: "academica",
      estado: "en_curso",
      porcentajeAvance: 70,
      responsable: DOCENTE,
      creadoPor: ADMINISTRACION,
      comentarios: [],
      historial: [
        evento("crear", ADMINISTRACION, "pendiente", undefined, 9),
        evento("cambiar_estado", DOCENTE, "en_curso", undefined, 7),
      ],
      proyectoId: PROYECTO_TESTING_ID,
      tareasRelacionadasIds: [],
    },
    // Pausa con comentario — se distingue en el listado del creador.
    {
      id: siguienteId(),
      numero: siguienteNumero(),
      titulo: "Revisar el cupo de la comisión de Algoritmos",
      descripcion: "Confirmar si hace falta abrir una comisión extra para el próximo cuatrimestre.",
      fechaInicio: fechaRelativa(-3),
      fechaFin: fechaRelativa(7),
      prioridad: "media",
      tipo: "academica",
      estado: "pausa",
      porcentajeAvance: 20,
      responsable: JEFE_CATEDRA,
      creadoPor: DECANATO,
      comentarios: [
        comentario(JEFE_CATEDRA, "Necesito confirmar el cupo real con Bedelía antes de seguir.", 1),
      ],
      historial: [
        evento("crear", DECANATO, "pendiente", undefined, 3),
        evento("cambiar_estado", JEFE_CATEDRA, "en_curso", undefined, 2),
        evento("cambiar_estado", JEFE_CATEDRA, "pausa", undefined, 1),
      ],
      tareasRelacionadasIds: [],
    },
    // Resuelta, con Solución completa. Asociada al mismo Proyecto de Testing.
    {
      id: siguienteId(),
      numero: siguienteNumero(),
      titulo: "Actualizar el cartel de horarios de la secretaría",
      descripcion: "Reflejar el nuevo horario de atención al público.",
      fechaInicio: fechaRelativa(-12),
      fechaFin: fechaRelativa(-2),
      prioridad: "baja",
      tipo: "administrativa",
      estado: "resuelta",
      porcentajeAvance: 100,
      solucion: "Se imprimió y colocó el nuevo cartel el lunes.",
      responsable: ADMINISTRACION,
      creadoPor: SECRETARIA,
      comentarios: [],
      historial: [
        evento("crear", SECRETARIA, "pendiente", undefined, 12),
        evento("cambiar_estado", ADMINISTRACION, "en_curso", undefined, 10),
        evento(
          "cambiar_estado",
          ADMINISTRACION,
          "resuelta",
          "Se imprimió y colocó el nuevo cartel el lunes.",
          2,
        ),
      ],
      proyectoId: PROYECTO_TESTING_ID,
      tareasRelacionadasIds: [],
    },
    // Cancelada por la autoridad creadora.
    {
      id: siguienteId(),
      numero: siguienteNumero(),
      titulo: "Coordinar mesa examinadora extraordinaria",
      descripcion: "Se evaluó una mesa extra para rezagados de la cursada anterior.",
      fechaInicio: fechaRelativa(-8),
      fechaFin: fechaRelativa(-1),
      prioridad: "media",
      tipo: "academica",
      estado: "cancelada",
      porcentajeAvance: 10,
      responsable: COORDINADOR,
      creadoPor: DECANATO,
      comentarios: [],
      historial: [
        evento("crear", DECANATO, "pendiente", undefined, 8),
        evento("cambiar_estado", DECANATO, "cancelada", undefined, 4),
      ],
      tareasRelacionadasIds: [],
    },
    // Jerarquía padre/hijas multinivel, asociada al Proyecto de Testing —
    // la hija y la nieta heredan el proyectoId del padre.
    {
      id: idPadreMigracion,
      numero: siguienteNumero(),
      titulo: "Migrar el sistema de inscripciones al nuevo campus",
      descripcion: "Coordinar la migración completa antes del inicio del próximo cuatrimestre.",
      fechaInicio: fechaRelativa(-5),
      fechaFin: fechaRelativa(25),
      prioridad: "alta",
      tipo: "investigacion",
      estado: "en_curso",
      porcentajeAvance: 15,
      responsable: DECANATO,
      creadoPor: DECANATO,
      comentarios: [],
      historial: [
        evento("crear", DECANATO, "pendiente", undefined, 5),
        evento("cambiar_estado", DECANATO, "en_curso", undefined, 4),
      ],
      proyectoId: PROYECTO_TESTING_ID,
      tareasRelacionadasIds: [],
    },
    {
      id: idHijaCalificaciones,
      numero: siguienteNumero(),
      titulo: "Migrar el módulo de calificaciones",
      descripcion: "Portar las notas históricas sin perder el detalle por comisión.",
      fechaInicio: fechaRelativa(-4),
      fechaFin: fechaRelativa(16),
      prioridad: "alta",
      tipo: "investigacion",
      estado: "en_curso",
      porcentajeAvance: 30,
      responsable: COORDINADOR,
      creadoPor: DECANATO,
      comentarios: [],
      historial: [
        evento("crear", DECANATO, "pendiente", undefined, 4),
        evento("cambiar_estado", COORDINADOR, "en_curso", undefined, 3),
      ],
      proyectoId: PROYECTO_TESTING_ID,
      tareaPadreId: idPadreMigracion,
      tareasRelacionadasIds: [],
    },
    {
      id: idNietaValidacionNotas,
      numero: siguienteNumero(),
      titulo: "Validar las notas migradas contra el sistema anterior",
      descripcion: "Muestreo comisión por comisión para confirmar que no se perdió información.",
      fechaInicio: fechaRelativa(-1),
      fechaFin: fechaRelativa(9),
      prioridad: "media",
      tipo: "investigacion",
      estado: "pendiente",
      porcentajeAvance: 0,
      responsable: JEFE_CATEDRA,
      creadoPor: DECANATO,
      comentarios: [],
      historial: [evento("crear", DECANATO, "pendiente", undefined, 1)],
      proyectoId: PROYECTO_TESTING_ID,
      tareaPadreId: idHijaCalificaciones,
      tareasRelacionadasIds: [],
    },
  ];
}

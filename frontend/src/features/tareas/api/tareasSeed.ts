// ============================================================
// Datos iniciales (seed) del mock de tareas. Hidratan el store la
// primera vez que no hay nada en localStorage. Las fechas se calculan
// relativas a "hoy" para que el semáforo de vencimiento (verde/amarillo/
// rojo por % de plazo transcurrido) se vea representativo en cualquier
// momento en que se corra la app, no solo el día que se escribió el seed.
// ============================================================
import type { ComentarioTarea, EventoHistorialTarea, Rol, Tarea } from "../types";

const SECRETARIA = { nombre: "L. Fernández", rol: "Secretaría" as Rol };
const DECANATO = { nombre: "R. Sosa", rol: "Decanato" as Rol };
const ADMINISTRACION = { nombre: "P. Gómez", rol: "Administración" as Rol };

const JEFE_CATEDRA = { nombre: "G. Ruiz", rol: "Jefe de Cátedra" as Rol };
const COORDINADOR = { nombre: "M. Díaz", rol: "Coordinador" as Rol };
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
  return [
    // Pendiente, recién creada — plazo largo, semáforo verde.
    {
      id: siguienteId(),
      numero: siguienteNumero(),
      titulo: "Actualizar el padrón de aulas disponibles",
      descripcion: "Relevar qué aulas quedaron libres tras el cierre de inscripciones.",
      fechaInicio: fechaRelativa(-1),
      fechaFin: fechaRelativa(19),
      prioridad: "media",
      estado: "pendiente",
      porcentajeAvance: 0,
      responsable: JEFE_CATEDRA,
      creadoPor: SECRETARIA,
      comentarios: [],
      historial: [evento("crear", SECRETARIA, "pendiente", undefined, 1)],
    },
    // En curso, plazo a mitad de camino — semáforo amarillo.
    {
      id: siguienteId(),
      numero: siguienteNumero(),
      titulo: "Cargar novedades de docentes en el sistema",
      descripcion: "Ingresar las altas/bajas informadas por las cátedras esta semana.",
      fechaInicio: fechaRelativa(-6),
      fechaFin: fechaRelativa(4),
      prioridad: "alta",
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
    },
    // En curso, plazo casi vencido — semáforo rojo.
    {
      id: siguienteId(),
      numero: siguienteNumero(),
      titulo: "Confirmar disponibilidad del laboratorio de Redes",
      descripcion: "Coordinar con mantenimiento el estado de las PCs antes del examen.",
      fechaInicio: fechaRelativa(-9),
      fechaFin: fechaRelativa(1),
      prioridad: "alta",
      estado: "en_curso",
      porcentajeAvance: 70,
      responsable: DOCENTE,
      creadoPor: ADMINISTRACION,
      comentarios: [],
      historial: [
        evento("crear", ADMINISTRACION, "pendiente", undefined, 9),
        evento("cambiar_estado", DOCENTE, "en_curso", undefined, 7),
      ],
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
    },
    // Resuelta, con Solución completa.
    {
      id: siguienteId(),
      numero: siguienteNumero(),
      titulo: "Actualizar el cartel de horarios de la secretaría",
      descripcion: "Reflejar el nuevo horario de atención al público.",
      fechaInicio: fechaRelativa(-12),
      fechaFin: fechaRelativa(-2),
      prioridad: "baja",
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
      estado: "cancelada",
      porcentajeAvance: 10,
      responsable: COORDINADOR,
      creadoPor: DECANATO,
      comentarios: [],
      historial: [
        evento("crear", DECANATO, "pendiente", undefined, 8),
        evento("cambiar_estado", DECANATO, "cancelada", undefined, 4),
      ],
    },
  ];
}

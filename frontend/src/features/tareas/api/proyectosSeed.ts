// ============================================================
// Datos iniciales (seed) del mock de proyectos. Hidratan el store la
// primera vez que no hay nada en localStorage. Mismo patrón que
// `tareasSeed.ts` — fechas relativas a "hoy" para que el orden por
// Fecha de Fin se vea representativo en cualquier momento.
// ============================================================
import type { Rol, Proyecto } from "../types";

const SECRETARIA = { nombre: "L. Fernández", rol: "Secretaría Académica" as Rol };
const DECANATO = { nombre: "R. Sosa", rol: "Decanato" as Rol };

/** Id fijo (no autoincremental): referenciado por `tareasSeed.ts` para asociar tareas de ejemplo. */
export const PROYECTO_TESTING_ID = "p-1";

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

/** Construye el seed inicial de proyectos — variedad de estados. */
export function crearSeedProyectos(): Proyecto[] {
  return [
    // Abierto, con tareas asociadas (incluida la jerarquía padre/hijas de ejemplo).
    {
      id: PROYECTO_TESTING_ID,
      numero: siguienteNumero(),
      nombre: "Nuevo sistema de Ingeniería para Testing",
      descripcion: "Modernización del entorno de pruebas de la carrera de Ingeniería.",
      fechaInicio: fechaRelativa(-20),
      fechaFin: fechaRelativa(40),
      estado: "abierto",
      responsable: DECANATO,
    },
    // Abierto, sin tareas todavía — su cuadro no aparece en la pantalla
    // inicial hasta que tenga al menos una (ver `agrupacionProyectos.ts`).
    {
      id: "p-2",
      numero: siguienteNumero(),
      nombre: "Digitalización del archivo histórico",
      descripcion: "Escaneo y catalogación de expedientes de alumnos anteriores a 2010.",
      fechaInicio: fechaRelativa(-5),
      fechaFin: fechaRelativa(90),
      estado: "abierto",
      responsable: SECRETARIA,
    },
    // Finalizado — no genera cuadro en la pantalla inicial, solo accesible
    // desde el listado completo de proyectos.
    {
      id: "p-3",
      numero: siguienteNumero(),
      nombre: "Migración a la nueva red edilicia",
      descripcion: "Cableado y wifi del edificio anexo.",
      fechaInicio: fechaRelativa(-120),
      fechaFin: fechaRelativa(-10),
      estado: "finalizado",
      responsable: DECANATO,
    },
    // Cancelado — misma condición que Finalizado: solo acceso manual.
    {
      id: "p-4",
      numero: siguienteNumero(),
      nombre: "Plataforma propia de videoconferencia",
      descripcion: "Se evaluó reemplazar la herramienta externa actual.",
      fechaInicio: fechaRelativa(-60),
      fechaFin: fechaRelativa(-30),
      estado: "cancelado",
      responsable: SECRETARIA,
    },
  ];
}

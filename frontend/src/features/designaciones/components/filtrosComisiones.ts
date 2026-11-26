import type { Comision, HorarioComision } from "../api/comisionesApi";

export interface FiltrosComisiones {
  materia: string;
  carrera: string;
  docente: string;
  modalidad: string;
}

export const FILTROS_COMISIONES_INICIALES: FiltrosComisiones = {
  materia: "",
  carrera: "",
  docente: "",
  modalidad: "",
};

const formatoFechaHora = new Intl.DateTimeFormat("es-AR", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
  hour12: false,
});

export function formatearFechaHora(iso: string): string {
  return formatoFechaHora.format(new Date(iso)).replace(",", "");
}

const DIAS = ["", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo"];

export function nombreDia(dia: number): string {
  return DIAS[dia] ?? "—";
}

/** "Lunes 08:00–12:00 · Aula 101"; sin aula no se agrega el separador. */
export function formatearBanda(horario: HorarioComision): string {
  const base = `${nombreDia(horario.dia)} ${horario.desde}–${horario.hasta}`;
  return horario.aula ? `${base} · ${horario.aula}` : base;
}

/** Aulas distintas de las bandas de la comisión, en el orden en que aparecen. */
export function aulasDeComision(comision: Comision): string[] {
  return [...new Set(comision.horarios.map((h) => h.aula).filter((a): a is string => Boolean(a)))];
}

function normalizar(texto: string): string {
  return texto.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase().trim();
}

function contiene(valores: (string | null)[], buscado: string): boolean {
  const filtro = normalizar(buscado);
  return (
    filtro === "" || valores.some((valor) => valor !== null && normalizar(valor).includes(filtro))
  );
}

/** Filtros locales: la grilla recibe el período completo, sin paginación. */
export function aplicarFiltrosComisiones(
  comisiones: Comision[],
  filtros: FiltrosComisiones,
): Comision[] {
  return comisiones.filter(
    (c) =>
      contiene([c.materia.nombre, c.materia.codigo], filtros.materia) &&
      contiene(
        c.carreras.flatMap((k) => [k.nombre, k.codigo]),
        filtros.carrera,
      ) &&
      contiene(
        c.docentes.map((d) => d.nombre),
        filtros.docente,
      ) &&
      contiene([c.modalidad, c.modalidadCodigo], filtros.modalidad),
  );
}

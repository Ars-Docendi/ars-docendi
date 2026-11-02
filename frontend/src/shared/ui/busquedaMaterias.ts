/** Par materia–carrera del catálogo informativo: `id` es el de la materia canónica. */
export interface OpcionMateriaCarrera {
  id: string;
  codigo: string;
  nombre: string;
  carreraId?: string | null;
  carreraNombre?: string | null;
  carreraCodigo?: string | null;
}

/**
 * `par`: cada resultado es una materia en una carrera (asignaciones y membresía Docente).
 * `materia`: una fila por materia canónica con sus carreras (membresía Jefe de Cátedra).
 */
export type ModoBuscadorMaterias = "par" | "materia";

export interface FiltrosMateria {
  materiaCodigo: string;
  materiaNombre: string;
  carreraCodigo: string;
  carreraNombre: string;
}

export const FILTROS_MATERIA_VACIOS: FiltrosMateria = {
  materiaCodigo: "",
  materiaNombre: "",
  carreraCodigo: "",
  carreraNombre: "",
};

export interface CarreraDeMateria {
  id: string;
  codigo: string;
  nombre: string;
}

export interface ResultadoMateria {
  /** Identifica la fila: la materia en modo `materia`, el par en modo `par`. */
  clave: string;
  materiaId: string;
  materiaCodigo: string;
  materiaNombre: string;
  /** Sólo en modo `par`. */
  carrera?: CarreraDeMateria;
  /** Carreras en que se dicta la materia; en modo `par`, sólo la del par. */
  carreras: CarreraDeMateria[];
}

export interface SeleccionMateria {
  materiaId: string;
  carreraId?: string | null;
}

function normalizar(texto: string | null | undefined): string {
  return (texto ?? "").normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase().trim();
}

function coincide(opcion: OpcionMateriaCarrera, filtros: FiltrosMateria): boolean {
  return (
    normalizar(opcion.codigo).includes(normalizar(filtros.materiaCodigo)) &&
    normalizar(opcion.nombre).includes(normalizar(filtros.materiaNombre)) &&
    normalizar(opcion.carreraCodigo).includes(normalizar(filtros.carreraCodigo)) &&
    normalizar(opcion.carreraNombre).includes(normalizar(filtros.carreraNombre))
  );
}

function carreraDe(opcion: OpcionMateriaCarrera): CarreraDeMateria {
  return {
    id: opcion.carreraId!,
    codigo: opcion.carreraCodigo ?? "",
    nombre: opcion.carreraNombre ?? "",
  };
}

export function hayFiltros(filtros: FiltrosMateria): boolean {
  return Object.values(filtros).some((valor) => valor.trim() !== "");
}

/**
 * Filtra el catálogo combinando los cuatro filtros (todos deben coincidir), por contenido y sin
 * distinguir mayúsculas ni tildes. En modo `materia` una materia coincide si alguna de sus
 * carreras coincide.
 */
export function buscarMaterias(
  opciones: OpcionMateriaCarrera[],
  filtros: FiltrosMateria,
  modo: ModoBuscadorMaterias,
): ResultadoMateria[] {
  const pares = opciones.filter((opcion) => opcion.carreraId);
  const ordenar = (a: ResultadoMateria, b: ResultadoMateria) =>
    a.materiaNombre.localeCompare(b.materiaNombre) ||
    a.materiaCodigo.localeCompare(b.materiaCodigo) ||
    (a.carrera?.nombre ?? "").localeCompare(b.carrera?.nombre ?? "");

  if (modo === "par") {
    const vistos = new Set<string>();
    return pares
      .filter((opcion) => coincide(opcion, filtros))
      .flatMap((opcion) => {
        const clave = `${opcion.id}|${opcion.carreraId}`;
        if (vistos.has(clave)) return [];
        vistos.add(clave);
        const carrera = carreraDe(opcion);
        return [
          {
            clave,
            materiaId: opcion.id,
            materiaCodigo: opcion.codigo,
            materiaNombre: opcion.nombre,
            carrera,
            carreras: [carrera],
          },
        ];
      })
      .sort(ordenar);
  }

  // En modo materia también entran materias canónicas sin carrera (opciones sin `carreraId`):
  // sólo coinciden mientras no se filtre por carrera.
  const porMateria = new Map<string, { opciones: OpcionMateriaCarrera[]; coincide: boolean }>();
  for (const opcion of opciones) {
    const grupo = porMateria.get(opcion.id) ?? { opciones: [], coincide: false };
    if (!grupo.opciones.some((otra) => (otra.carreraId ?? null) === (opcion.carreraId ?? null))) {
      grupo.opciones.push(opcion);
    }
    grupo.coincide ||= coincide(opcion, filtros);
    porMateria.set(opcion.id, grupo);
  }
  return [...porMateria.entries()]
    .filter(([, grupo]) => grupo.coincide)
    .map(([materiaId, grupo]) => ({
      clave: materiaId,
      materiaId,
      materiaCodigo: grupo.opciones[0].codigo,
      materiaNombre: grupo.opciones[0].nombre,
      carreras: grupo.opciones
        .filter((opcion) => opcion.carreraId)
        .map(carreraDe)
        .sort((a, b) => a.codigo.localeCompare(b.codigo) || a.nombre.localeCompare(b.nombre)),
    }))
    .sort(ordenar);
}

/** Cantidad de filas del buscador sin filtros: pares o materias distintas según el modo. */
export function totalOpciones(opciones: OpcionMateriaCarrera[], modo: ModoBuscadorMaterias) {
  return buscarMaterias(opciones, FILTROS_MATERIA_VACIOS, modo).length;
}

export function esSeleccionActual(
  resultado: ResultadoMateria,
  seleccion: SeleccionMateria | null | undefined,
): boolean {
  if (!seleccion?.materiaId || seleccion.materiaId !== resultado.materiaId) return false;
  return !resultado.carrera || resultado.carrera.id === seleccion.carreraId;
}

/** Texto del campo de solo lectura: materia (código) — carrera (código). */
export function describirSeleccion(
  opciones: OpcionMateriaCarrera[],
  seleccion: SeleccionMateria | null | undefined,
  modo: ModoBuscadorMaterias,
): string {
  if (!seleccion?.materiaId) return "";
  const materia = opciones.find((opcion) => opcion.id === seleccion.materiaId);
  if (!materia) return "";
  const textoMateria = `${materia.nombre} (${materia.codigo})`;
  if (modo === "materia") return `${textoMateria} — todas sus carreras`;
  const par = opciones.find(
    (opcion) => opcion.id === seleccion.materiaId && opcion.carreraId === seleccion.carreraId,
  );
  if (!par) return textoMateria;
  return par.carreraCodigo
    ? `${textoMateria} — ${par.carreraNombre} (${par.carreraCodigo})`
    : `${textoMateria} — ${par.carreraNombre}`;
}

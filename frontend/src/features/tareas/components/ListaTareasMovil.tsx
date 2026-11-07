import { HerramientasLista, type OpcionOrden } from "../../../shared/ui/HerramientasLista";
import type { GrupoFiltro } from "../../../shared/ui/HojaFiltros";
import { ListaTarjetas, type Tarjeta } from "../../../shared/ui/ListaTarjetas";
import type { Prioridad, Proyecto, Tarea } from "../types";
import { formatearFecha } from "./detalleAdapters";
import { EstadoTareaBadge } from "./EstadoTareaBadge";
import {
  aplicarFiltrosColumnas,
  ETIQUETA_ESTADO,
  ETIQUETA_PRIORIDAD,
  opcionesColumnasTareas,
  type FiltrosColumnasTareas,
} from "./filtrosTareas";
import type { ColumnaOrdenableTarea, OrdenTareas } from "./ordenTareas";
import { nivelAlertaSemaforo } from "./semaforoTarea";

const ORDENES: OpcionOrden<ColumnaOrdenableTarea>[] = [
  { columna: "fechaInicio", direccion: "asc", etiqueta: "Inicio (más antiguo)" },
  { columna: "fechaInicio", direccion: "desc", etiqueta: "Inicio (más reciente)" },
  { columna: "fechaFin", direccion: "asc", etiqueta: "Fin (más próximo)" },
  { columna: "fechaFin", direccion: "desc", etiqueta: "Fin (más lejano)" },
  { columna: "prioridad", direccion: "desc", etiqueta: "Prioridad" },
  { columna: "avance", direccion: "desc", etiqueta: "% Avance" },
  { columna: "numero", direccion: "asc", etiqueta: "N°" },
  { columna: "responsable", direccion: "asc", etiqueta: "Responsable" },
  { columna: "estado", direccion: "asc", etiqueta: "Estado" },
];

interface ListaTareasMovilProps {
  /** Every task of the table, unfiltered: the sheet counts results and offers options from it. */
  todas: Tarea[];
  /** Filtered and sorted, as the table shows them. */
  tareas: Tarea[];
  proyectos?: Proyecto[];
  filtros: FiltrosColumnasTareas;
  /** Filters the sheet's "limpiar" goes back to (the Estado preset's own selection). */
  vacios: FiltrosColumnasTareas;
  /** Offer the Estado filter; with a preset the Pendientes/Terminadas/Todas tabs own it. */
  conEstado: boolean;
  orden: OrdenTareas | null;
  onFiltrosChange: (filtros: FiltrosColumnasTareas) => void;
  onOrdenChange: (orden: OrdenTareas) => void;
}

/** Phone rendering of the task table: same rows, filters and sort; the semáforo tints the card. */
export function ListaTareasMovil({
  todas,
  tareas,
  proyectos,
  filtros,
  vacios,
  conEstado,
  orden,
  onFiltrosChange,
  onOrdenChange,
}: ListaTareasMovilProps) {
  const opciones = opcionesColumnasTareas(todas);
  const grupos: GrupoFiltro<FiltrosColumnasTareas>[] = [
    {
      clave: "prioridad",
      titulo: "Prioridad",
      opciones: (Object.keys(ETIQUETA_PRIORIDAD) as Prioridad[]).map((p) => ({
        valor: p,
        etiqueta: ETIQUETA_PRIORIDAD[p],
      })),
    },
    ...(conEstado
      ? [
          {
            clave: "estado",
            titulo: "Estado",
            opciones: Object.entries(ETIQUETA_ESTADO).map(([valor, etiqueta]) => ({
              valor,
              etiqueta,
            })),
          } as const,
        ]
      : []),
    {
      clave: "responsable",
      titulo: "Responsable",
      opciones: opciones.responsables.map((r) => ({ valor: r, etiqueta: r })),
    },
    {
      clave: "autor",
      titulo: "Autor",
      opciones: opciones.autores.map((a) => ({ valor: a, etiqueta: a })),
    },
    { clave: "numero", titulo: "N°", tipo: "texto", placeholder: "Buscar número…" },
    { clave: "fechaInicio", titulo: "Inicio", tipo: "texto", placeholder: "Buscar inicio…" },
    { clave: "fechaFin", titulo: "Fin", tipo: "texto", placeholder: "Buscar fin…" },
    { clave: "avance", titulo: "% Avance", tipo: "texto", placeholder: "% exacto…" },
  ];

  const tarjetas: Tarjeta[] = tareas.map((tarea) => {
    const ruta = `/tareas/${tarea.id}`;
    const proyecto = proyectos?.find((p) => p.id === tarea.proyectoId)?.nombre;
    return {
      id: tarea.id,
      nombre: tarea.titulo,
      titulo: tarea.titulo,
      ruta,
      etiquetaEnlace: `Ver la tarea ${tarea.numero}: ${tarea.titulo}`,
      meta: `N° ${tarea.numero} · ${formatearFecha(tarea.fechaInicio)} – ${formatearFecha(tarea.fechaFin)}`,
      linea: `${tarea.responsable.nombre} · Prioridad ${ETIQUETA_PRIORIDAD[tarea.prioridad]} · ${tarea.porcentajeAvance}%${proyecto ? ` · ${proyecto}` : ""}`,
      insignias: <EstadoTareaBadge estado={tarea.estado} />,
      tinte: nivelAlertaSemaforo(tarea.estado, tarea.fechaInicio, tarea.fechaFin) ?? undefined,
    };
  });

  return (
    <>
      <HerramientasLista
        grupos={grupos}
        filtros={filtros}
        vacios={vacios}
        onFiltrosChange={onFiltrosChange}
        contar={(f) => aplicarFiltrosColumnas(todas, f).length}
        busqueda={{ clave: "titulo", placeholder: "Buscar tarea…" }}
        ordenes={ORDENES}
        orden={orden}
        onOrdenChange={onOrdenChange}
      />
      <ListaTarjetas
        etiqueta="Tareas"
        tarjetas={tarjetas}
        vacio="Sin tareas que cumplan los filtros."
        reinicio={JSON.stringify([filtros, orden])}
      />
    </>
  );
}

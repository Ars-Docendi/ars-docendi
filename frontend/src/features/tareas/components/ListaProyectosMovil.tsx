import { HerramientasLista, type OpcionOrden } from "../../../shared/ui/HerramientasLista";
import type { GrupoFiltro } from "../../../shared/ui/HojaFiltros";
import { ListaTarjetas, type Tarjeta } from "../../../shared/ui/ListaTarjetas";
import type { EstadoProyectoCatalogo, Proyecto } from "../types";
import { formatearFecha } from "./detalleAdapters";
import { EstadoProyectoBadge } from "./EstadoProyectoBadge";
import {
  aplicarFiltrosProyectos,
  FILTROS_PROYECTOS_INICIALES,
  responsablesDeProyectos,
  type FiltrosColumnasProyectos,
} from "./filtrosProyectos";
import type { ColumnaOrdenableProyecto, OrdenProyectos } from "./ordenProyectos";

const ORDENES: OpcionOrden<ColumnaOrdenableProyecto>[] = [
  { columna: "estado", direccion: "asc", etiqueta: "Estado" },
  { columna: "fechaInicio", direccion: "asc", etiqueta: "Inicio (más antiguo)" },
  { columna: "fechaInicio", direccion: "desc", etiqueta: "Inicio (más reciente)" },
  { columna: "fechaFin", direccion: "asc", etiqueta: "Fin (más próximo)" },
  { columna: "fechaFin", direccion: "desc", etiqueta: "Fin (más lejano)" },
  { columna: "nombre", direccion: "asc", etiqueta: "Nombre (A–Z)" },
  { columna: "responsable", direccion: "asc", etiqueta: "Responsable" },
  { columna: "numero", direccion: "asc", etiqueta: "N°" },
];

interface ListaProyectosMovilProps {
  /** Every project, unfiltered: the sheet counts results and offers options from it. */
  todos: Proyecto[];
  /** Filtered and sorted, as the table shows them. */
  proyectos: Proyecto[];
  estados: EstadoProyectoCatalogo[];
  filtros: FiltrosColumnasProyectos;
  orden: OrdenProyectos | null;
  onFiltrosChange: (filtros: FiltrosColumnasProyectos) => void;
  onOrdenChange: (orden: OrdenProyectos) => void;
}

/** Phone rendering of the project table: same rows, filters and sort. */
export function ListaProyectosMovil({
  todos,
  proyectos,
  estados,
  filtros,
  orden,
  onFiltrosChange,
  onOrdenChange,
}: ListaProyectosMovilProps) {
  const grupos: GrupoFiltro<FiltrosColumnasProyectos>[] = [
    {
      clave: "estado",
      titulo: "Estado",
      opciones: estados.map((e) => ({ valor: e.codigo, etiqueta: e.nombre })),
    },
    {
      clave: "responsable",
      titulo: "Responsable",
      opciones: responsablesDeProyectos(todos).map((r) => ({ valor: r, etiqueta: r })),
    },
    { clave: "numero", titulo: "N°", tipo: "texto", placeholder: "Buscar número…" },
    { clave: "fechaInicio", titulo: "Inicio", tipo: "texto", placeholder: "Buscar inicio…" },
    { clave: "fechaFin", titulo: "Fin", tipo: "texto", placeholder: "Buscar fin…" },
  ];

  const tarjetas: Tarjeta[] = proyectos.map((proyecto) => ({
    id: proyecto.id,
    nombre: proyecto.nombre,
    titulo: proyecto.nombre,
    ruta: `/tareas/proyectos/${proyecto.id}`,
    etiquetaEnlace: `Ver el proyecto ${proyecto.numero}: ${proyecto.nombre}`,
    meta: `N° ${proyecto.numero} · ${formatearFecha(proyecto.fechaInicio)} – ${formatearFecha(proyecto.fechaFin)}`,
    linea: proyecto.responsable.nombre,
    insignias: <EstadoProyectoBadge estado={proyecto.estado} nombre={proyecto.estadoNombre} />,
  }));

  return (
    <>
      <HerramientasLista
        grupos={grupos}
        filtros={filtros}
        vacios={FILTROS_PROYECTOS_INICIALES}
        onFiltrosChange={onFiltrosChange}
        contar={(f) => aplicarFiltrosProyectos(todos, f).length}
        busqueda={{ clave: "nombre", placeholder: "Buscar proyecto…" }}
        ordenes={ORDENES}
        orden={orden}
        onOrdenChange={onOrdenChange}
      />
      <ListaTarjetas
        etiqueta="Proyectos"
        tarjetas={tarjetas}
        vacio={
          todos.length === 0
            ? "Todavía no hay proyectos cargados."
            : "Sin proyectos que cumplan los filtros."
        }
        reinicio={JSON.stringify([filtros, orden])}
      />
    </>
  );
}

import { StatusBadge } from "@ars-docendi/ui";

import { useFiltrosURL, useOrdenURL } from "../../../shared/navegacion/estadoURL";
import { HerramientasLista, type OpcionOrden } from "../../../shared/ui/HerramientasLista";
import type { GrupoFiltro } from "../../../shared/ui/HojaFiltros";
import { ListaTarjetas, type Tarjeta } from "../../../shared/ui/ListaTarjetas";
import type { PeriodoDesignacion } from "../types";
import {
  COLUMNAS_ORDEN_PERIODOS,
  FILTROS_PERIODOS_INICIALES,
  aplicarFiltrosPeriodos,
  aplicarFiltrosYOrdenPeriodos,
  formatearFecha,
  formatearMesAnio,
  type ColumnaOrdenPeriodos,
  type FiltrosPeriodos,
} from "./filtrosPeriodos";

const ORDENES: OpcionOrden<ColumnaOrdenPeriodos>[] = [
  { columna: "impactoDesde", direccion: "desc", etiqueta: "Impacto (más reciente)" },
  { columna: "impactoDesde", direccion: "asc", etiqueta: "Impacto (más antiguo)" },
  { columna: "nombre", direccion: "asc", etiqueta: "Nombre (A–Z)" },
  { columna: "nombre", direccion: "desc", etiqueta: "Nombre (Z–A)" },
  { columna: "cargaDesde", direccion: "desc", etiqueta: "Carga desde" },
  { columna: "cargaHasta", direccion: "desc", etiqueta: "Carga hasta" },
  { columna: "impactoHasta", direccion: "desc", etiqueta: "Impacto hasta" },
  { columna: "activo", direccion: "asc", etiqueta: "Estado" },
];

const GRUPOS: GrupoFiltro<FiltrosPeriodos>[] = [
  {
    clave: "activo",
    titulo: "Estado",
    opciones: [
      { valor: "activo", etiqueta: "Activo" },
      { valor: "inactivo", etiqueta: "Inactivo" },
    ],
  },
  { clave: "cargaDesde", titulo: "Carga desde", tipo: "texto", placeholder: "Buscar fecha…" },
  { clave: "cargaHasta", titulo: "Carga hasta", tipo: "texto", placeholder: "Buscar fecha…" },
  {
    clave: "impactoDesde",
    titulo: "Impacto desde",
    tipo: "texto",
    placeholder: "Buscar mes o fecha…",
  },
  {
    clave: "impactoHasta",
    titulo: "Impacto hasta",
    tipo: "texto",
    placeholder: "Buscar mes o fecha…",
  },
];

interface ListaPeriodosMovilProps {
  periodos: PeriodoDesignacion[];
  onEditar: (periodo: PeriodoDesignacion) => void;
  onEliminar: (periodo: PeriodoDesignacion) => void;
}

/** Phone rendering of the Períodos table: same URL filters and sort, same row actions. */
export function ListaPeriodosMovil({ periodos, onEditar, onEliminar }: ListaPeriodosMovilProps) {
  const [filtros, setFiltros] = useFiltrosURL(FILTROS_PERIODOS_INICIALES);
  const [orden, setOrden] = useOrdenURL<ColumnaOrdenPeriodos>(COLUMNAS_ORDEN_PERIODOS);
  const visibles = aplicarFiltrosYOrdenPeriodos(periodos, filtros, orden);

  const tarjetas: Tarjeta[] = visibles.map((periodo) => ({
    id: periodo.id,
    nombre: periodo.nombre,
    titulo: periodo.nombre,
    onAbrir: () => onEditar(periodo),
    etiquetaEnlace: `Editar período ${periodo.nombre}`,
    meta: `Carga ${formatearFecha(periodo.cargaDesde)} – ${formatearFecha(periodo.cargaHasta)}`,
    linea: `Impacto ${formatearMesAnio(periodo.impactoDesde)} – ${formatearMesAnio(periodo.impactoHasta)}`,
    insignias: (
      <StatusBadge
        kind={periodo.activo ? "aprobado" : "cancelado"}
        label={periodo.activo ? "Activo" : "Inactivo"}
      />
    ),
    acciones: [
      { etiqueta: "Editar", onSelect: () => onEditar(periodo) },
      { etiqueta: "Eliminar", peligro: true, onSelect: () => onEliminar(periodo) },
    ],
  }));

  return (
    <>
      <HerramientasLista
        grupos={GRUPOS}
        filtros={filtros}
        vacios={FILTROS_PERIODOS_INICIALES}
        onFiltrosChange={setFiltros}
        contar={(f) => aplicarFiltrosPeriodos(periodos, f).length}
        busqueda={{ clave: "nombre", placeholder: "Buscar nombre…" }}
        ordenes={ORDENES}
        orden={orden}
        onOrdenChange={setOrden}
      />
      <ListaTarjetas
        etiqueta="Períodos"
        tarjetas={tarjetas}
        vacio="Sin resultados para los filtros aplicados."
        reinicio={JSON.stringify([filtros, orden])}
      />
    </>
  );
}

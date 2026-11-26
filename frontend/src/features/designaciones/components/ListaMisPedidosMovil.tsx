import { HerramientasLista, type OpcionOrden } from "../../../shared/ui/HerramientasLista";
import type { GrupoFiltro } from "../../../shared/ui/HojaFiltros";
import { ListaTarjetas, type Tarjeta } from "../../../shared/ui/ListaTarjetas";
import type { AccionMenu } from "../../../shared/ui/MenuAcciones";
import type { OrdenURL } from "../../../shared/navegacion/estadoURL";
import type { PedidoDesignacion } from "../types";
import { EstadoPedidoPill } from "./EstadoPedidoPill";
import {
  FILTROS_INICIALES,
  aplicarFiltrosMisPedidos,
  etiquetaEstadoFiltro,
  etiquetaNovedadCorta,
  fechaEnviado,
  opcionesEstadosMisPedidos,
  opcionesTiposMisPedidos,
  type ColumnaOrdenMisPedidos,
  type FiltrosMisPedidosState,
} from "./filtrosMisPedidos";

const ORDENES: OpcionOrden<ColumnaOrdenMisPedidos>[] = [
  { columna: "docente", direccion: "asc", etiqueta: "Docente (A–Z)" },
  { columna: "docente", direccion: "desc", etiqueta: "Docente (Z–A)" },
  { columna: "enviado", direccion: "desc", etiqueta: "Enviado (más reciente)" },
  { columna: "enviado", direccion: "asc", etiqueta: "Enviado (más antiguo)" },
  { columna: "catedra", direccion: "asc", etiqueta: "Cátedra" },
  { columna: "legajo", direccion: "asc", etiqueta: "Legajo" },
  { columna: "tipo", direccion: "asc", etiqueta: "Tipo" },
  { columna: "estado", direccion: "asc", etiqueta: "Estado" },
];

interface ListaMisPedidosMovilProps {
  /** Every request, unfiltered: option lists and sheet counts come from here. */
  todos: PedidoDesignacion[];
  /** Filtered and sorted, as the table shows them. */
  pedidos: PedidoDesignacion[];
  filtros: FiltrosMisPedidosState;
  orden: OrdenURL<ColumnaOrdenMisPedidos> | null;
  onFiltrosChange: (filtros: FiltrosMisPedidosState) => void;
  onOrdenChange: (orden: OrdenURL<ColumnaOrdenMisPedidos>) => void;
  onEditar: (pedido: PedidoDesignacion) => void;
  onEliminar: (pedido: PedidoDesignacion) => void;
}

/** Phone rendering of the Mis pedidos table: same rows, filters, sort and row actions. */
export function ListaMisPedidosMovil({
  todos,
  pedidos,
  filtros,
  orden,
  onFiltrosChange,
  onOrdenChange,
  onEditar,
  onEliminar,
}: ListaMisPedidosMovilProps) {
  const grupos: GrupoFiltro<FiltrosMisPedidosState>[] = [
    {
      clave: "estado",
      titulo: "Estado",
      opciones: opcionesEstadosMisPedidos(todos).map((e) => ({
        valor: e,
        etiqueta: etiquetaEstadoFiltro(e),
      })),
    },
    {
      clave: "tipo",
      titulo: "Tipo",
      opciones: opcionesTiposMisPedidos(todos).map((t) => ({ valor: t, etiqueta: t })),
    },
    { clave: "legajo", titulo: "Legajo", tipo: "texto", placeholder: "Buscar legajo…" },
    { clave: "catedra", titulo: "Cátedra", tipo: "texto", placeholder: "Buscar cátedra…" },
    { clave: "enviado", titulo: "Enviado", tipo: "texto", placeholder: "Buscar fecha…" },
  ];

  const tarjetas: Tarjeta[] = pedidos.map((pedido) => {
    const acciones: AccionMenu[] = [];
    if (pedido.accionesPermitidas?.includes("editar"))
      acciones.push({ etiqueta: "Editar", onSelect: () => onEditar(pedido) });
    if (pedido.accionesPermitidas?.includes("eliminar"))
      acciones.push({ etiqueta: "Eliminar", peligro: true, onSelect: () => onEliminar(pedido) });
    return {
      id: pedido.id,
      nombre: pedido.docente.nombre,
      titulo: pedido.docente.nombre,
      ruta: `/designaciones/pedidos/${pedido.id}`,
      etiquetaEnlace: `Ver el pedido de ${pedido.docente.nombre}`,
      meta: `Legajo ${pedido.docente.legajo ?? "—"} · ${fechaEnviado(pedido)}`,
      linea: `${pedido.catedra} · ${etiquetaNovedadCorta(pedido)}`,
      insignias: <EstadoPedidoPill estado={pedido.estado} />,
      acciones,
    };
  });

  return (
    <>
      <HerramientasLista
        grupos={grupos}
        filtros={filtros}
        vacios={FILTROS_INICIALES}
        onFiltrosChange={onFiltrosChange}
        contar={(f) => aplicarFiltrosMisPedidos(todos, f).length}
        busqueda={{ clave: "docente", placeholder: "Buscar docente…" }}
        ordenes={ORDENES}
        orden={orden}
        onOrdenChange={onOrdenChange}
      />
      <ListaTarjetas
        etiqueta="Mis pedidos de designación"
        tarjetas={tarjetas}
        vacio="Sin resultados para los filtros aplicados."
        reinicio={JSON.stringify([filtros, orden])}
      />
    </>
  );
}

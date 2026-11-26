import { HerramientasLista, type OpcionOrden } from "../../../shared/ui/HerramientasLista";
import type { GrupoFiltro } from "../../../shared/ui/HojaFiltros";
import { ListaTarjetas, type Tarjeta } from "../../../shared/ui/ListaTarjetas";
import type { PedidoDesignacion } from "../types";
import { EstadoPedidoBadge } from "./EstadoPedidoBadge";
import {
  FILTROS_COLUMNAS_INICIALES,
  OPCIONES_ESTADO,
  type FiltrosColumnasTablero,
  type opcionesColumnasTablero,
} from "./filtrosTablero";
import { NovedadChip } from "./NovedadChip";
import {
  areaActual,
  etiquetaEstado,
  inicioEnCircuito,
  ultimaActualizacion,
  type ColumnaOrdenable,
  type IdPestania,
  type OrdenTabla,
} from "./tableroRevisionModelo";

const ORDENES: OpcionOrden<ColumnaOrdenable>[] = [
  { columna: "docente", direccion: "asc", etiqueta: "Docente (A–Z)" },
  { columna: "docente", direccion: "desc", etiqueta: "Docente (Z–A)" },
  { columna: "ultima", direccion: "desc", etiqueta: "Últ. actualización (reciente)" },
  { columna: "ultima", direccion: "asc", etiqueta: "Últ. actualización (antigua)" },
  { columna: "inicio", direccion: "desc", etiqueta: "Inicio (reciente)" },
  { columna: "inicio", direccion: "asc", etiqueta: "Inicio (antiguo)" },
  { columna: "legajo", direccion: "asc", etiqueta: "Legajo" },
  { columna: "tipo", direccion: "asc", etiqueta: "Tipo" },
  { columna: "estado", direccion: "asc", etiqueta: "Estado" },
];

interface ListaRevisionMovilProps {
  /** Rows of the selected area tab, filtered and sorted as the table shows them. */
  pedidos: PedidoDesignacion[];
  /** Selected area tab; it only resets the pagination here. */
  pestania: IdPestania;
  mostrarArea: boolean;
  opciones: ReturnType<typeof opcionesColumnasTablero>;
  filtros: FiltrosColumnasTablero;
  orden: OrdenTabla | null;
  onFiltrosChange: (filtros: FiltrosColumnasTablero) => void;
  onOrdenChange: (orden: OrdenTabla) => void;
  /** Rows of the selected tab that pass the given column filters. */
  contar: (filtros: FiltrosColumnasTablero) => number;
}

/** Phone rendering of the Revisión table body: the area tabs stay in `TablaRevision`. */
export function ListaRevisionMovil({
  pedidos,
  pestania,
  mostrarArea,
  opciones,
  filtros,
  orden,
  onFiltrosChange,
  onOrdenChange,
  contar,
}: ListaRevisionMovilProps) {
  const etiquetaDe = (valor: string) =>
    OPCIONES_ESTADO.find((o) => o.value === valor)?.label ?? valor;
  const grupos: GrupoFiltro<FiltrosColumnasTablero>[] = [
    {
      clave: "estado",
      titulo: "Estado",
      opciones: opciones.estados.map((e) => ({ valor: e, etiqueta: etiquetaDe(e) })),
    },
    {
      clave: "tipo",
      titulo: "Tipo",
      opciones: opciones.tipos.map((t) => ({ valor: t, etiqueta: t })),
    },
    ...(mostrarArea
      ? [
          {
            clave: "area" as const,
            titulo: "Área",
            opciones: opciones.areas.map((a) => ({ valor: a, etiqueta: a })),
          },
        ]
      : []),
    { clave: "legajo", titulo: "Legajo", tipo: "texto", placeholder: "Buscar legajo…" },
    { clave: "inicio", titulo: "Inicio", tipo: "texto", placeholder: "Buscar inicio…" },
    {
      clave: "ultima",
      titulo: "Últ. actualización",
      tipo: "texto",
      placeholder: "Buscar últ. actualización…",
    },
  ];

  const tarjetas: Tarjeta[] = pedidos.map((pedido) => ({
    id: pedido.id,
    nombre: pedido.docente.nombre,
    titulo: pedido.docente.nombre,
    ruta: `/designaciones/pedidos/${pedido.id}`,
    etiquetaEnlace: `Ver el pedido de ${pedido.docente.nombre}`,
    meta: `Legajo ${pedido.docente.legajo ?? "—"}`,
    linea: [
      `Inicio ${inicioEnCircuito(pedido) ?? "—"}`,
      `Últ. actualización ${ultimaActualizacion(pedido) ?? "—"}`,
      ...(mostrarArea ? [areaActual(pedido) ?? "—"] : []),
    ].join(" · "),
    insignias: (
      <>
        <NovedadChip novedad={pedido.novedad} />
        <EstadoPedidoBadge
          estado={pedido.estado}
          etiqueta={etiquetaEstado(pedido)}
          prioritario={pedido.prioritario}
        />
      </>
    ),
  }));

  return (
    <>
      <HerramientasLista
        grupos={grupos}
        filtros={filtros}
        vacios={FILTROS_COLUMNAS_INICIALES}
        onFiltrosChange={onFiltrosChange}
        contar={contar}
        busqueda={{ clave: "docente", placeholder: "Buscar docente…" }}
        ordenes={ORDENES}
        orden={orden}
        onOrdenChange={onOrdenChange}
      />
      <ListaTarjetas
        etiqueta="Pedidos en revisión"
        tarjetas={tarjetas}
        vacio="Sin pedidos que cumplan los filtros."
        reinicio={JSON.stringify([pestania, filtros, orden])}
      />
    </>
  );
}

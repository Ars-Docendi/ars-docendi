import { HerramientasLista, type OpcionOrden } from "../../../shared/ui/HerramientasLista";
import type { GrupoFiltro } from "../../../shared/ui/HojaFiltros";
import { ListaTarjetas, type Tarjeta } from "../../../shared/ui/ListaTarjetas";
import { rutaDetalleSolicitud } from "../rutas";
import type { EstadoSolicitudAula, SolicitudReservaAula } from "../types";
import {
  aplicarFiltrosSolicitudes,
  etiquetaEstado,
  fechaSolicitud,
  FILTROS_INICIALES,
  horarioSolicitud,
  opcionesEstados,
  type ColumnaOrdenSolicitudes,
  type FiltrosSolicitudesState,
  type OrdenSolicitudes,
} from "./filtrosSolicitudes";
import { EstadoSolicitudPill } from "./EstadoSolicitudPill";

const ORDENES: OpcionOrden<ColumnaOrdenSolicitudes>[] = [
  { columna: "dia", direccion: "asc", etiqueta: "Día (más próximo)" },
  { columna: "dia", direccion: "desc", etiqueta: "Día (más lejano)" },
  { columna: "horario", direccion: "asc", etiqueta: "Horario" },
  { columna: "alumnos", direccion: "desc", etiqueta: "Capacidad" },
  { columna: "materia", direccion: "asc", etiqueta: "Materia" },
  { columna: "comision", direccion: "asc", etiqueta: "Comisión" },
  { columna: "estado", direccion: "asc", etiqueta: "Estado" },
];

const ORDEN_DOCENTE: OpcionOrden<ColumnaOrdenSolicitudes> = {
  columna: "docente",
  direccion: "asc",
  etiqueta: "Docente",
};

interface ListaSolicitudesMovilProps {
  /** "mias" is the Docente's list (with Cancelar); "todas" is the Administrativo's. */
  modo: "mias" | "todas";
  /** Filtered and sorted, as the table shows them. */
  solicitudes: SolicitudReservaAula[];
  /** Every request, unfiltered: the sheet counts results and offers states from this list. */
  todas: SolicitudReservaAula[];
  filtros: FiltrosSolicitudesState;
  orden: OrdenSolicitudes | null;
  onFiltrosChange: (filtros: FiltrosSolicitudesState) => void;
  onOrdenChange: (orden: OrdenSolicitudes) => void;
  onCancelar?: (solicitud: SolicitudReservaAula) => void;
}

/** Phone rendering of the request tables: same rows, filters, sort and Cancelar action. */
export function ListaSolicitudesMovil({
  modo,
  solicitudes,
  todas,
  filtros,
  orden,
  onFiltrosChange,
  onOrdenChange,
  onCancelar,
}: ListaSolicitudesMovilProps) {
  const grupos: GrupoFiltro<FiltrosSolicitudesState>[] = [
    {
      clave: "estado",
      titulo: "Estado",
      opciones: opcionesEstados(todas).map((e: EstadoSolicitudAula) => ({
        valor: e,
        etiqueta: etiquetaEstado(e),
      })),
    },
    { clave: "dia", titulo: "Día", tipo: "texto", placeholder: "Buscar día…" },
    { clave: "comision", titulo: "Comisión", tipo: "texto", placeholder: "Buscar comisión…" },
    ...(modo === "todas"
      ? [
          {
            clave: "docente",
            titulo: "Docente",
            tipo: "texto",
            placeholder: "Buscar docente…",
          } as const,
        ]
      : []),
  ];

  const tarjetas: Tarjeta[] = solicitudes.map((solicitud) => {
    const nombreDocente = solicitud.docente
      ? `${solicitud.docente.apellido}, ${solicitud.docente.nombre}`
      : "docente sin asignar";
    const fecha = fechaSolicitud(solicitud);
    return {
      id: solicitud.id,
      nombre: solicitud.materia.nombre,
      titulo: solicitud.materia.nombre,
      ruta: rutaDetalleSolicitud(solicitud.id),
      etiquetaEnlace:
        modo === "mias"
          ? `Ver la solicitud del ${fecha}`
          : `Ver la solicitud de ${nombreDocente} del ${fecha}`,
      meta: `${solicitud.materia.codigo} · Com. ${solicitud.comision} · ${solicitud.aulaAsignada ?? "Sin aula"}`,
      linea: `${modo === "todas" ? `${solicitud.docente ? nombreDocente : "—"} · ` : ""}${fecha} · ${horarioSolicitud(solicitud)} · ${solicitud.cantidadAlumnosAprox} alumnos`,
      insignias: <EstadoSolicitudPill estado={solicitud.estado} />,
      acciones:
        modo === "mias" && onCancelar && solicitud.estado === "pendiente"
          ? [{ etiqueta: "Cancelar", onSelect: () => onCancelar(solicitud) }]
          : [],
    };
  });

  return (
    <>
      <HerramientasLista
        grupos={grupos}
        filtros={filtros}
        vacios={FILTROS_INICIALES}
        onFiltrosChange={onFiltrosChange}
        contar={(f) => aplicarFiltrosSolicitudes(todas, f).length}
        busqueda={{ clave: "materia", placeholder: "Buscar materia…" }}
        ordenes={modo === "todas" ? [...ORDENES, ORDEN_DOCENTE] : ORDENES}
        orden={orden}
        onOrdenChange={onOrdenChange}
      />
      <ListaTarjetas
        etiqueta={modo === "mias" ? "Mis solicitudes" : "Todas las solicitudes"}
        tarjetas={tarjetas}
        vacio="Sin resultados para los filtros aplicados."
        reinicio={JSON.stringify([filtros, orden])}
      />
    </>
  );
}

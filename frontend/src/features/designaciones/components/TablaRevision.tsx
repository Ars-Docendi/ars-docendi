import { useState } from "react";
import type { ActorContexto, PedidoDesignacion } from "../types";
import {
  construirColumnas,
  esTuTurno,
  fechaUltimaActualizacion,
  inicialesDocente,
  seccionInicialDelActor,
  type ColumnaTablero,
} from "./tableroRevisionModelo";
import type { FiltrosTablero } from "./filtrosTablero";
import { aplicarFiltros } from "./filtrosTablero";
import { NovedadChip, PrioridadFlagIcono } from "./NovedadChip";
import { EstadoAvance } from "./EstadoAvance";
import { IconoChevronDown } from "./lucide";
import "./revision.css";

interface TablaRevisionProps {
  pedidos: PedidoDesignacion[];
  actor: ActorContexto;
  filtros: FiltrosTablero;
  onSeleccionar: (pedido: PedidoDesignacion) => void;
}

/**
 * Vista Tabla del tablero de revisión: los pedidos agrupados en 4 secciones
 * desplegables por etapa del circuito — En Coordinación / En Secretaría /
 * En Decanato / Finalizados (Aceptados + Rechazados) — cada una con su
 * propio head de columnas, separadas entre sí. Reusa `construirColumnas`
 * (agrupación por sección) y `EstadoAvance` (celda Estado: avance del
 * circuito, "Aceptado", o "Devuelto por {revisor}"). Arranca expandida solo
 * la sección del rol del actor (Coordinador/Secretaría/Decanato); las demás
 * arrancan colapsadas — Administración no tiene sección "propia", así que
 * las 4 arrancan colapsadas. Cada fila es un botón que navega al detalle.
 */
export function TablaRevision({ pedidos, actor, filtros, onSeleccionar }: TablaRevisionProps) {
  const filtrados = aplicarFiltros(pedidos, filtros);
  const visibles =
    filtros.vista === "mis-pendientes"
      ? filtrados.filter((pedido) => esTuTurno(pedido, actor))
      : filtrados;
  const secciones = construirColumnas(visibles);
  const seccionDelActor = seccionInicialDelActor(actor);

  return (
    <div className="adoc-tabla">
      {secciones.map((seccion) => (
        <SeccionEstadoTabla
          key={seccion.id}
          seccion={seccion}
          expandidoInicial={seccion.id === seccionDelActor}
          onSeleccionar={onSeleccionar}
        />
      ))}
    </div>
  );
}

/**
 * Sección desplegable de la Tabla de revisión: header con título + contador
 * (`aria-expanded`, chevron que rota) y, cuando está expandida, su propio
 * head de columnas + filas (o el estado vacío) — cada sección es una
 * mini-tabla independiente, separada de las demás. Mismo patrón de
 * expandir/colapsar que `GrupoColapsable` de `Sidebar.tsx` — sin componente
 * Accordion nuevo en `@ars-docendi/ui`.
 */
function SeccionEstadoTabla({
  seccion,
  expandidoInicial,
  onSeleccionar,
}: {
  seccion: ColumnaTablero;
  expandidoInicial: boolean;
  onSeleccionar: (pedido: PedidoDesignacion) => void;
}) {
  const [expandido, setExpandido] = useState(expandidoInicial);
  const idBody = `adoc-seccion-${seccion.id}`;

  return (
    <div className={`adoc-seccion-tabla adoc-seccion-tabla--${seccion.tono}`}>
      <button
        type="button"
        className="adoc-seccion-header"
        aria-expanded={expandido}
        aria-controls={idBody}
        aria-label={`${expandido ? "Colapsar" : "Expandir"} sección ${seccion.titulo}`}
        onClick={() => setExpandido((previo) => !previo)}
      >
        <span
          className={`adoc-seccion-chevron${expandido ? "" : " adoc-seccion-chevron--colapsado"}`}
          aria-hidden="true"
        >
          <IconoChevronDown />
        </span>
        <span className="adoc-seccion-titulo">{seccion.titulo}</span>
        <span className="adoc-seccion-subtitulo">{seccion.subtitulo}</span>
        <span className="adoc-seccion-contador">Total: {seccion.pedidos.length}</span>
      </button>

      {expandido && (
        <div id={idBody}>
          <div className="adoc-tabla-head" aria-hidden="true">
            <span className="adoc-tabla-h">Docente</span>
            <span className="adoc-tabla-h">Legajo</span>
            <span className="adoc-tabla-h">Asignatura</span>
            <span className="adoc-tabla-h">Tipo</span>
            <span className="adoc-tabla-h">Fecha última actualización</span>
            <span className="adoc-tabla-h">Estado</span>
            <span className="adoc-tabla-h" />
          </div>

          {seccion.pedidos.length === 0 ? (
            <p className="adoc-kanban-col-empty">Sin pedidos</p>
          ) : (
            seccion.pedidos.map((pedido) => (
              <FilaTablaRevision key={pedido.id} pedido={pedido} onSeleccionar={onSeleccionar} />
            ))
          )}
        </div>
      )}
    </div>
  );
}

function FilaTablaRevision({
  pedido,
  onSeleccionar,
}: {
  pedido: PedidoDesignacion;
  onSeleccionar: (pedido: PedidoDesignacion) => void;
}) {
  return (
    <button
      type="button"
      className="adoc-tabla-row"
      onClick={() => onSeleccionar(pedido)}
      aria-label={`Ver el pedido de ${pedido.docente.nombre}`}
    >
      <span className="adoc-tabla-c adoc-tabla-docente">
        <span className="adoc-pedido-avatar" aria-hidden="true">
          {inicialesDocente(pedido.docente.nombre)}
        </span>
        <span className="adoc-tabla-nombre">{pedido.docente.nombre}</span>
      </span>
      <span className="adoc-tabla-c adoc-tabla-legajo">{pedido.docente.legajo ?? "—"}</span>
      <span className="adoc-tabla-c adoc-tabla-asig">{pedido.catedra}</span>
      <span className="adoc-tabla-c">
        <NovedadChip novedad={pedido.novedad} />
      </span>
      <span className="adoc-tabla-c adoc-tabla-fecha">{fechaUltimaActualizacion(pedido)}</span>
      <span className="adoc-tabla-c">
        <EstadoAvance pedido={pedido} />
      </span>
      <span className="adoc-tabla-c adoc-tabla-prio">
        {pedido.prioritario && <PrioridadFlagIcono />}
      </span>
    </button>
  );
}

import type { Tarea } from "../types";
import { EstadoTareaBadge } from "./EstadoTareaBadge";
import { estadoSemaforo, muestraSemaforo } from "./semaforoTarea";
import type { ClaveOrdenTarea, OrdenTareasState } from "./ordenTareas";
import "./tablaTareas.css";

interface TablaTareasProps {
  tareas: Tarea[];
  orden: OrdenTareasState;
  onOrdenar: (clave: ClaveOrdenTarea) => void;
  onSeleccionar: (tarea: Tarea) => void;
}

const ETIQUETA_PRIORIDAD: Record<Tarea["prioridad"], string> = {
  alta: "Alta",
  media: "Media",
  baja: "Baja",
};

const COLUMNAS: { clave: ClaveOrdenTarea; etiqueta: string }[] = [
  { clave: "numero", etiqueta: "N°" },
  { clave: "titulo", etiqueta: "TÍTULO" },
  { clave: "autor", etiqueta: "AUTOR" },
  { clave: "responsable", etiqueta: "RESPONSABLE" },
  { clave: "fechaInicio", etiqueta: "INICIO" },
  { clave: "fechaFin", etiqueta: "FIN" },
  { clave: "prioridad", etiqueta: "PRIORIDAD" },
  { clave: "avance", etiqueta: "% AVANCE" },
  { clave: "estado", etiqueta: "ESTADO" },
];

/** Formatea un ISO (yyyy-mm-dd) a dd/mm/aaaa, sin depender del locale. */
function formatearFecha(iso: string): string {
  const fecha = new Date(iso);
  const dia = String(fecha.getUTCDate()).padStart(2, "0");
  const mes = String(fecha.getUTCMonth() + 1).padStart(2, "0");
  return `${dia}/${mes}/${fecha.getUTCFullYear()}`;
}

/**
 * Listado único de tareas: Nro, Título, Autor, Responsable, Fecha Inicio,
 * Fecha Fin (con semáforo de vencimiento en el fondo de la fila), Prioridad,
 * % Avance y Estado. Cada columna del header es clickeable para ordenar por
 * ella (alterna asc/desc); por defecto el listado llega ordenado por Fecha
 * Inicio ascendente (ver `ordenTareas.ts`, aplicado en `IndexPage`). Cada
 * fila navega al detalle al hacer click — mismo patrón que
 * `designaciones/components/TablaMisPedidos.tsx`.
 */
export function TablaTareas({ tareas, orden, onOrdenar, onSeleccionar }: TablaTareasProps) {
  return (
    <div className="adoc-tt-table" role="table" aria-label="Listado de tareas">
      <div className="adoc-tt-head" role="row">
        {COLUMNAS.map((col) => {
          const activa = orden.clave === col.clave;
          return (
            <span
              key={col.clave}
              role="columnheader"
              aria-sort={activa ? (orden.direccion === "asc" ? "ascending" : "descending") : "none"}
            >
              <button
                type="button"
                className={`adoc-tt-th${activa ? " adoc-tt-th--activa" : ""}`}
                onClick={() => onOrdenar(col.clave)}
                aria-label={`Ordenar por ${col.etiqueta.toLowerCase()}`}
              >
                {col.etiqueta}
                <span className="adoc-tt-th-flecha" aria-hidden="true">
                  {activa ? (orden.direccion === "asc" ? "↑" : "↓") : ""}
                </span>
              </button>
            </span>
          );
        })}
      </div>
      {tareas.map((tarea) => {
        // Solo amarillo/rojo resaltan la fila (verde es el caso normal, sin
        // urgencia — no necesita destacarse). Resuelta/Cancelada no muestran
        // semáforo en absoluto (`muestraSemaforo`).
        const semaforo = muestraSemaforo(tarea.estado)
          ? estadoSemaforo(tarea.fechaInicio, tarea.fechaFin)
          : null;
        const claseSemaforo =
          semaforo === "red"
            ? " adoc-tt-row--vencida"
            : semaforo === "yellow"
              ? " adoc-tt-row--por-vencer"
              : "";

        return (
          <div
            className={`adoc-tt-row${claseSemaforo}`}
            role="row"
            key={tarea.id}
            tabIndex={0}
            aria-label={`Ver la tarea "${tarea.titulo}"`}
            onClick={() => onSeleccionar(tarea)}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                onSeleccionar(tarea);
              }
            }}
          >
            <span className="adoc-tt-num" role="cell">
              {tarea.numero}
            </span>
            <span className="adoc-tt-titulo" role="cell">
              {tarea.titulo}
            </span>
            <span className="adoc-tt-persona" role="cell">
              {tarea.creadoPor.nombre}
            </span>
            <span className="adoc-tt-persona" role="cell">
              {tarea.responsable.nombre}
            </span>
            <span className="adoc-tt-fecha" role="cell">
              {formatearFecha(tarea.fechaInicio)}
            </span>
            <span className="adoc-tt-fecha" role="cell">
              {formatearFecha(tarea.fechaFin)}
            </span>
            <span className="adoc-tt-prioridad" role="cell">
              {ETIQUETA_PRIORIDAD[tarea.prioridad]}
            </span>
            <span className="adoc-tt-avance" role="cell">
              {tarea.porcentajeAvance}%
            </span>
            <span className="adoc-tt-estado" role="cell">
              <EstadoTareaBadge estado={tarea.estado} />
            </span>
          </div>
        );
      })}
    </div>
  );
}

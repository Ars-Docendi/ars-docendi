import { Button, InlineAlert, Pagination } from "@ars-docendi/ui";

import type { EventoAuditoria, PaginaAuditoria } from "../api/auditoriaApi";
import { agruparPorDia } from "../utils/agruparPorDia";
import { horaHmsEnZona } from "../utils/zonaHoraria";

interface ListaAuditoriaProps {
  pagina?: PaginaAuditoria;
  cargando: boolean;
  esError: boolean;
  eventoSeleccionado: string | null;
  onSeleccionarFila: (evento: EventoAuditoria) => void;
  onCambiarPagina: (pagina: number) => void;
  onReintentar: () => void;
}

function iniciales(nombre: string): string {
  return nombre
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((parte) => parte[0])
    .join("")
    .toUpperCase();
}

const CHEVRON = (
  <svg width="14" height="14" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
    <path
      d="M9 6l6 6-6 6"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    />
  </svg>
);

/** La lista de auditoría agrupada por día (requisito «Auditoría tab filters and day-grouped list»). */
export function ListaAuditoria({
  pagina,
  cargando,
  esError,
  eventoSeleccionado,
  onSeleccionarFila,
  onCambiarPagina,
  onReintentar,
}: ListaAuditoriaProps) {
  if (esError) {
    return (
      <div className="auditoria-lista auditoria-lista--estado" role="alert">
        <p>No se pudieron cargar los registros.</p>
        <Button variant="secondary" type="button" onClick={onReintentar}>
          Reintentar
        </Button>
      </div>
    );
  }

  if (cargando || !pagina) {
    return (
      <div className="auditoria-lista auditoria-lista--estado" role="status">
        Cargando registros de auditoría…
      </div>
    );
  }

  const {
    elementos,
    total,
    tamanoPagina,
    pagina: paginaActual,
    parcial,
    fuentesNoDisponibles,
  } = pagina;
  const totalPaginas = Math.max(1, Math.ceil(total / tamanoPagina));
  const primero = elementos.length === 0 ? 0 : (paginaActual - 1) * tamanoPagina + 1;
  const ultimo = elementos.length === 0 ? 0 : primero + elementos.length - 1;
  const ahora = new Date();
  const grupos = agruparPorDia(elementos, (e) => e.cambiadoEn, ahora);

  return (
    <div className="auditoria-lista">
      {parcial && (
        <InlineAlert severity="warning">
          {fuentesNoDisponibles.includes("asistente")
            ? "No se pudieron cargar los registros del asistente. Se muestran los demás."
            : "Algunos registros no pudieron cargarse. Se muestran los demás."}
        </InlineAlert>
      )}

      {elementos.length === 0 ? (
        <p className="auditoria-lista-vacio">No hay registros para estos filtros.</p>
      ) : (
        <>
          <div className="auditoria-lista-encabezado" aria-hidden="true">
            <span>Hora</span>
            <span>Cambio · usuario · módulo</span>
            <span>Acción</span>
            <span />
          </div>
          {grupos.map((grupo) => (
            <div key={grupo.clave}>
              <div className="auditoria-dia-titulo">{grupo.titulo}</div>
              {grupo.elementos.map((evento) => {
                const seleccionada = evento.id === eventoSeleccionado;
                return (
                  <button
                    key={evento.id}
                    id={`auditoria-fila-${evento.id}`}
                    type="button"
                    className="auditoria-fila"
                    aria-pressed={seleccionada}
                    aria-current={seleccionada ? "true" : undefined}
                    onClick={() => onSeleccionarFila(evento)}
                  >
                    <span className="auditoria-fila-hora">
                      {horaHmsEnZona(new Date(evento.cambiadoEn))}
                    </span>
                    <span className="auditoria-fila-cambio">
                      <span
                        className={`auditoria-avatar ${
                          evento.tipoActor === "persona"
                            ? "auditoria-avatar--persona"
                            : "auditoria-avatar--neutral"
                        }`}
                        aria-hidden="true"
                      >
                        {iniciales(evento.actor)}
                      </span>
                      <span className="auditoria-fila-texto">
                        <span className="auditoria-fila-resumen">{evento.resumen}</span>
                        <span className="auditoria-fila-contexto">
                          {evento.actor} · {evento.modulo} · {evento.objeto}
                        </span>
                      </span>
                    </span>
                    <span className={`sistema-chip-accion sistema-chip-accion--${evento.accion}`}>
                      {evento.accionEtiqueta}
                    </span>
                    {CHEVRON}
                  </button>
                );
              })}
            </div>
          ))}
          <Pagination
            page={paginaActual}
            pageCount={totalPaginas}
            onChange={onCambiarPagina}
            meta={
              <span>
                {primero}–{ultimo} de {total}
              </span>
            }
          />
        </>
      )}
    </div>
  );
}

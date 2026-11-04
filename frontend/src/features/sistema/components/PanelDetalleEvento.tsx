import { useEffect, useRef } from "react";
import type { KeyboardEvent } from "react";

import type { CambioAuditoria, EventoAuditoria } from "../api/auditoriaApi";
import { fechaHoraCompletaEnZona } from "../utils/zonaHoraria";

interface PanelDetalleEventoProps {
  evento: EventoAuditoria;
  onCerrar: () => void;
}

const CANDADO = (
  <svg width="13" height="13" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
    <rect
      x="4"
      y="10"
      width="16"
      height="10"
      rx="2"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
    />
    <path d="M8 10V7a4 4 0 018 0v3" fill="none" stroke="currentColor" strokeWidth="2" />
  </svg>
);

function BloqueDeCambio({ cambio }: { cambio: CambioAuditoria }) {
  if (cambio.oculto) {
    return (
      <div className="auditoria-detalle-campo">
        <span className="auditoria-detalle-nombre-campo">{cambio.etiquetaCampo}</span>
        <span className="auditoria-detalle-enmascarado">{CANDADO} Enmascarado por política</span>
      </div>
    );
  }
  const antes = cambio.valorAnterior ?? "—";
  const despues = cambio.valorNuevo ?? "—";
  return (
    <div className="auditoria-detalle-campo">
      <span className="auditoria-detalle-nombre-campo">{cambio.etiquetaCampo}</span>
      <div className="auditoria-detalle-valores">
        <span
          className={
            cambio.valorAnterior === null ? "auditoria-valor-vacio" : "auditoria-valor-antes"
          }
        >
          {antes}
        </span>
        <span aria-hidden="true">→</span>
        <span
          className={
            cambio.valorNuevo === null ? "auditoria-valor-vacio" : "auditoria-valor-despues"
          }
        >
          {despues}
        </span>
      </div>
    </div>
  );
}

/** El panel de detalle inline (design D10, requisito «Inline audit event detail panel»). */
export function PanelDetalleEvento({ evento, onCerrar }: PanelDetalleEventoProps) {
  const tituloRef = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    tituloRef.current?.focus();
  }, [evento.id]);

  // Escape cierra el panel igual que «×» (ARS-160): reusa el mismo onCerrar,
  // así que el foco vuelve a la fila que lo abrió por el mismo camino.
  function alPresionarTecla(evt: KeyboardEvent<HTMLElement>) {
    if (evt.key === "Escape") onCerrar();
  }

  return (
    <aside
      className="auditoria-detalle-panel"
      aria-label="Detalle del registro"
      onKeyDown={alPresionarTecla}
    >
      <header className="auditoria-detalle-encabezado">
        <div>
          <span className={`sistema-chip-accion sistema-chip-accion--${evento.accion}`}>
            {evento.accionEtiqueta}
          </span>
          <h2 ref={tituloRef} tabIndex={-1} className="auditoria-detalle-resumen">
            {evento.resumen}
          </h2>
          <p className="auditoria-detalle-meta">
            {evento.actor} · {fechaHoraCompletaEnZona(new Date(evento.cambiadoEn))}
          </p>
        </div>
        <button
          type="button"
          aria-label="Cerrar detalle"
          className="auditoria-detalle-cerrar"
          onClick={onCerrar}
        >
          ×
        </button>
      </header>

      <section className="auditoria-detalle-seccion">
        <h3 className="sistema-grupo-titulo">Qué cambió</h3>
        {evento.cambios.length === 0 ? (
          <p className="auditoria-detalle-sin-cambios">Sin campos registrados para este evento.</p>
        ) : (
          evento.cambios.map((cambio) => <BloqueDeCambio key={cambio.campo} cambio={cambio} />)
        )}
      </section>

      <section className="auditoria-detalle-seccion auditoria-detalle-tecnicos">
        <h3 className="sistema-grupo-titulo">Datos técnicos</h3>
        <dl className="auditoria-detalle-datos-tecnicos">
          <dt>Tabla</dt>
          <dd>
            {evento.schema}.{evento.tabla}
          </dd>
          <dt>Clave de fila</dt>
          <dd>{evento.rowPk}</dd>
          <dt>Solicitud</dt>
          <dd>{evento.requestId ?? "—"}</dd>
        </dl>
      </section>
    </aside>
  );
}

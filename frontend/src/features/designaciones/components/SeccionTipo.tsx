import type { ReactNode } from "react";
import { TIPOS_PEDIDO, exigeDocumentacion, type TipoPedido } from "../mock/mockPedido";

const ICONOS: Record<TipoPedido, ReactNode> = {
  "alta-nueva": (
    <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth="1.5">
      <circle cx="9" cy="6" r="3" />
      <path d="M3 16c0-3 3-5 6-5s6 2 6 5" />
      <path d="M14 4v4M16 6h-4" />
    </svg>
  ),
  renovacion: (
    <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth="1.5">
      <path d="M15 8a6 6 0 11-3-5" />
      <path d="M15 3v4h-4" />
    </svg>
  ),
  cambio: (
    <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth="1.5">
      <path d="M3 6h10M11 4l2 2-2 2M15 12H5M7 14l-2-2 2-2" />
    </svg>
  ),
  baja: (
    <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth="1.5">
      <circle cx="9" cy="6" r="3" />
      <path d="M3 16c0-3 3-5 6-5s6 2 6 5" />
      <path d="M16 5l-4 4M16 9l-4-4" />
    </svg>
  ),
};

interface SeccionTipoProps {
  tipo: TipoPedido;
  onCambiarTipo: (tipo: TipoPedido) => void;
}

/**
 * Sección 1 — selección del tipo de pedido como grilla de tarjetas (radiogroup
 * accesible). La librería no expone una "card-radio", así que se compone acá
 * sobre CSS local manteniendo la semántica de selección única.
 */
export function SeccionTipo({ tipo, onCambiarTipo }: SeccionTipoProps) {
  return (
    <section className="adoc-form-section" id="tipo">
      <header>
        <h3>1 · Tipo de pedido</h3>
        <div className="hint">
          Definí qué clase de movimiento estás solicitando para el docente.
        </div>
      </header>
      <div className="body">
        <div className="col-12">
          <div className="pedido-tipo-grid" role="radiogroup" aria-label="Tipo de pedido">
            {TIPOS_PEDIDO.map((t) => {
              const seleccionado = t.id === tipo;
              return (
                <button
                  type="button"
                  key={t.id}
                  role="radio"
                  aria-checked={seleccionado}
                  data-tono={t.tono}
                  className={`pedido-tipo-card${seleccionado ? " selected" : ""}`}
                  onClick={() => onCambiarTipo(t.id)}
                >
                  <span className="ico">{ICONOS[t.id]}</span>
                  <b>{t.nombre}</b>
                  <span className="desc">{t.descripcion}</span>
                  {exigeDocumentacion(t.id) && <span className="req-flag">requiere CV + DNI</span>}
                </button>
              );
            })}
          </div>
        </div>
      </div>
    </section>
  );
}

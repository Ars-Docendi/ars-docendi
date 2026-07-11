import type { ReactNode } from "react";
import type { Adjunto, Cargo, Dedicacion, Novedad, PedidoDesignacion, TipoAdjunto } from "../types";
import { iniciales } from "./detalleAdapters";

/** Tono del chip de novedad (clases del design system). */
const TONO_NOVEDAD: Record<Novedad, string> = {
  Alta: "success",
  Baja: "danger",
  "Cambio de cargo o dedicación": "warning",
  "Sin novedad": "neutral",
};

const ETIQUETA_NOVEDAD: Record<Novedad, string> = {
  Alta: "Alta",
  Baja: "Baja",
  "Cambio de cargo o dedicación": "Cambio",
  "Sin novedad": "Sin novedad",
};

const ETIQUETA_ADJUNTO: Record<TipoAdjunto, string> = {
  cv: "CV",
  dni_frente: "DNI (frente)",
  dni_dorso: "DNI (dorso)",
  justificativo: "Justificativo",
};

/** Celda etiqueta + valor del grid de datos. */
function Dato({ etiqueta, children }: { etiqueta: string; children: ReactNode }) {
  return (
    <div className="adoc-dato">
      <span className="adoc-eyebrow">{etiqueta}</span>
      <span className="adoc-dato-val">{children}</span>
    </div>
  );
}

/** Valor con transición actual → solicitado (cuando hay cambio). */
function Transicion({
  desde,
  hacia,
}: {
  desde: Cargo | Dedicacion | null;
  hacia?: Cargo | Dedicacion;
}) {
  if (hacia && desde && hacia !== desde) {
    return (
      <span className="adoc-dato-trans">
        <span className="adoc-dato-from">{desde}</span>
        <svg
          className="adoc-dato-arrow"
          viewBox="0 0 16 16"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.4"
          aria-hidden="true"
        >
          <path d="M3 8h9M9 5l3 3-3 3" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
        <span className="adoc-dato-to">{hacia}</span>
      </span>
    );
  }
  return <>{hacia ?? desde ?? "—"}</>;
}

/**
 * Tarjeta "Datos del pedido": cabecera del docente (avatar + identidad + chip de
 * novedad), grilla de datos (cátedra/carrera/cargo/dedicación/horas), el
 * justificativo del Jefe de Cátedra en cita y la documentación adjunta. Sólo
 * datos reales del `PedidoDesignacion` (sin campos inventados — invariante #7).
 */
interface ResumenPedidoProps {
  pedido: PedidoDesignacion;
  /** Nombre legible del período, resuelto desde `periodoId` en la página. */
  periodoNombre?: string;
}

export function ResumenPedido({ pedido, periodoNombre }: ResumenPedidoProps) {
  const { docente } = pedido;
  const tieneAdjuntos = pedido.adjuntos.length > 0;

  return (
    <section className="adoc-card adoc-det-card">
      <header className="adoc-det-head">
        <div className="adoc-det-id">
          <span className="adoc-det-avatar" aria-hidden="true">
            {iniciales(docente.nombre)}
          </span>
          <div className="adoc-det-namecol">
            <div className="adoc-det-namerow">
              <span className="adoc-det-name">{docente.nombre}</span>
              {pedido.cargoActual && (
                <span className="adoc-det-rolechip">{pedido.cargoActual}</span>
              )}
            </div>
            <span className="adoc-det-meta">
              DNI {docente.dni} · {docente.antiguedad} años de antigüedad
            </span>
          </div>
        </div>
        <span className={`adoc-det-tipo ${TONO_NOVEDAD[pedido.novedad]}`}>
          {ETIQUETA_NOVEDAD[pedido.novedad]}
        </span>
      </header>

      <div className="adoc-divider" />

      <p className="adoc-eyebrow">Datos del pedido</p>
      <div className="adoc-datos-grid">
        <Dato etiqueta="Cátedra">{pedido.catedra}</Dato>
        <Dato etiqueta="Carrera">{pedido.carrera}</Dato>
        <Dato etiqueta="Período">{periodoNombre ?? "—"}</Dato>
        <Dato etiqueta="Cargo">
          <Transicion desde={pedido.cargoActual} hacia={pedido.cargoSolicitado} />
        </Dato>
        <Dato etiqueta="Dedicación">
          <Transicion desde={pedido.dedicacionActual} hacia={pedido.dedicacionSolicitada} />
        </Dato>
        <Dato etiqueta="Materias">
          {pedido.asignaciones.map((a) => `${a.materia} (${a.horas}h)`).join(" · ") || "—"}
        </Dato>
        <Dato etiqueta="Horas de investigación">
          <span className="adoc-dato-horas">
            {pedido.horasInvestigacion} h semanales
            <span className="adoc-portal-tag">Portal</span>
          </span>
        </Dato>
        <Dato etiqueta="Horas externas">{pedido.horasExternas} h semanales</Dato>
      </div>

      {pedido.justificacion && (
        <>
          <div className="adoc-divider" />
          <div className="adoc-justif">
            <p className="adoc-eyebrow">Justificativo del Jefe de Cátedra</p>
            <blockquote className="adoc-justif-quote">{pedido.justificacion}</blockquote>
          </div>
        </>
      )}

      {tieneAdjuntos && (
        <>
          <div className="adoc-divider" />
          <div className="adoc-adjuntos">
            <p className="adoc-eyebrow">Documentación adjunta</p>
            <ul className="adoc-adjuntos-list">
              {pedido.adjuntos.map((adjunto: Adjunto) => (
                <li key={adjunto.id} className="adoc-adjunto">
                  <span className="adoc-adjunto-tipo">{ETIQUETA_ADJUNTO[adjunto.tipo]}</span>
                  <span className="adoc-adjunto-nombre">{adjunto.nombre}</span>
                </li>
              ))}
            </ul>
          </div>
        </>
      )}
    </section>
  );
}

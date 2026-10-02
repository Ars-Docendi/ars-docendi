import { Fragment } from "react";
import type { EtapaCadena } from "./detalleAdapters";

const ICONO_CHECK = (
  <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
    <path d="m3.5 8.5 3 3 6-7" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
);

const ICONO_X = (
  <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
    <path d="m4.5 4.5 7 7m0-7-7 7" strokeLinecap="round" />
  </svg>
);

const ICONO_DEVOLVER = (
  <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6" aria-hidden="true">
    <path d="M9.5 4.5 6 8l3.5 3.5M6 8h5" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
);

/** Marca del segmento: check (done) / dot (current) / número (pending) / x (rejected). */
function Marca({ etapa, indice }: { etapa: EtapaCadena; indice: number }) {
  if (etapa.estado === "cumplida") return ICONO_CHECK;
  if (etapa.estado === "rechazada") return ICONO_X;
  if (etapa.estado === "devuelta") return ICONO_DEVOLVER;
  if (etapa.estado === "actual") return <span className="adoc-cadena-dot" aria-hidden="true" />;
  return <span className="adoc-cadena-num">{indice + 1}</span>;
}

/**
 * Stepper horizontal de la cadena de aprobación (5 etapas: Jefe de Cátedra →
 * Coordinador → Secretaría → Decanato → En lote). Presentacional: recibe las
 * etapas ya derivadas (`derivarCadena`). Los conectores se pintan en accent
 * cuando la etapa previa está cumplida.
 */
export function CadenaRevision({ etapas }: { etapas: EtapaCadena[] }) {
  return (
    <div className="adoc-card adoc-cadena" role="list" aria-label="Cadena de aprobación">
      {etapas.map((etapa, indice) => (
        <Fragment key={etapa.rol}>
          {indice > 0 && (
            <span
              className={`adoc-cadena-conn${etapas[indice - 1].estado === "cumplida" ? " cumplida" : ""}`}
              aria-hidden="true"
            />
          )}
          <div
            className={`adoc-cadena-seg ${etapa.estado}`}
            role="listitem"
            aria-current={etapa.estado === "actual" ? "step" : undefined}
          >
            <span className="adoc-cadena-mark">
              <Marca etapa={etapa} indice={indice} />
            </span>
            <span className="adoc-cadena-text">
              <span className="adoc-cadena-rol">{etapa.rol}</span>
              <span className="adoc-cadena-detalle">{etapa.detalle}</span>
            </span>
          </div>
        </Fragment>
      ))}
    </div>
  );
}

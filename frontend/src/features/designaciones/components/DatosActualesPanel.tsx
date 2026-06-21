import { Fragment } from "react";
import type { ReactNode } from "react";
import type { Cargo, Dedicacion } from "../types";

interface DatosActualesPanelProps {
  antiguedad: number;
  cargoActual: Cargo;
  dedicacionActual: Dedicacion;
  materia: string;
  /** Cambio: dedicación solicitada. Si difiere de la actual se muestra como transición. */
  dedicacionSolicitada?: Dedicacion;
}

/** "Categoría 3" → "Cat. 3" para la transición compacta del panel. */
function abreviar(dedicacion: Dedicacion): string {
  return dedicacion.replace("Categoría", "Cat.");
}

/**
 * Panel de solo lectura con la designación vigente del docente
 * (Antigüedad · Cargo actual · Dedicación actual · Materia).
 * En un Cambio, la dedicación se muestra como transición `actual → solicitada`.
 * Replica el bloque `datosActuales` de los frames Baja/Cambio.
 */
export function DatosActualesPanel({
  antiguedad,
  cargoActual,
  dedicacionActual,
  materia,
  dedicacionSolicitada,
}: DatosActualesPanelProps) {
  const hayCambioDedicacion = Boolean(
    dedicacionSolicitada && dedicacionSolicitada !== dedicacionActual,
  );

  const dedicacionValor: ReactNode =
    hayCambioDedicacion && dedicacionSolicitada ? (
      <span className="adoc-pf-datos-trans">
        <span className="adoc-pf-datos-from">{abreviar(dedicacionActual)}</span>
        <svg
          className="adoc-pf-datos-arrow"
          viewBox="0 0 16 16"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.4"
          aria-hidden="true"
        >
          <path d="M3 8h9M9 5l3 3-3 3" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
        <span className="adoc-pf-datos-to">{abreviar(dedicacionSolicitada)}</span>
      </span>
    ) : (
      dedicacionActual
    );

  const columnas: { clave: string; valor: ReactNode }[] = [
    { clave: "Antigüedad", valor: `${antiguedad} ${antiguedad === 1 ? "año" : "años"}` },
    { clave: "Cargo actual", valor: cargoActual },
    { clave: hayCambioDedicacion ? "Dedicación" : "Dedicación actual", valor: dedicacionValor },
    { clave: "Materia", valor: materia },
  ];

  return (
    <div className="adoc-pf-datos">
      {columnas.map((columna, indice) => (
        <Fragment key={indice}>
          {indice > 0 && <span className="adoc-pf-datos-div" aria-hidden="true" />}
          <div className="adoc-pf-datos-col">
            <span className="adoc-pf-datos-k">{columna.clave}</span>
            <span className="adoc-pf-datos-v">{columna.valor}</span>
          </div>
        </Fragment>
      ))}
    </div>
  );
}

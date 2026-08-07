import { Fragment } from "react";
import type { ReactNode } from "react";
import type { Cargo, Dedicacion } from "../types";

interface DatosActualesPanelProps {
  antiguedad: number;
  cargoActual: Cargo;
  /** Cambio: cargo solicitado. Si difiere del actual se muestra como transición. */
  cargoSolicitado?: Cargo;
  dedicacionActual: Dedicacion;
  /** Cambio: dedicación solicitada. Si difiere de la actual se muestra como transición. */
  dedicacionSolicitada?: Dedicacion;
  /** La cátedra del pedido. Es su materia: un pedido cubre exactamente una. */
  materia: string;
  /** Horas vigentes del docente en esa cátedra. `undefined` en un Alta: todavía no tiene designación. */
  horasActuales?: number;
  /** Cambio: horas tal como quedan editadas en el form. Su presencia dispara la sub-sección "Materia". */
  horasSolicitadas?: number;
  /** Sin novedad: muestra la materia en la franja superior. */
  mostrarMateria?: boolean;
  horasInvestigacionActuales?: number;
  horasInvestigacionSolicitadas?: number;
  horasExternasActuales?: number;
  horasExternasSolicitadas?: number;
}

/** "Categoría 3" → "Cat. 3" para las transiciones compactas del panel. */
function abreviar(valor: string): string {
  return valor.replace("Categoría", "Cat.");
}

/** Valor `desde → hacia` con flecha — mismo patrón visual para cualquier campo. */
function Transicion({ desde, hacia }: { desde: ReactNode; hacia: ReactNode }) {
  return (
    <span className="adoc-pf-datos-trans">
      <span className="adoc-pf-datos-from">{desde}</span>
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
      <span className="adoc-pf-datos-to">{hacia}</span>
    </span>
  );
}

/** Fila compacta "etiqueta · valor" del resumen de cambios — mismo estilo tenga o no cambios. */
function FilaResumen({ etiqueta, children }: { etiqueta: string; children: ReactNode }) {
  return (
    <div className="adoc-pf-datos-sub-fila">
      <span className="adoc-pf-datos-sub-etiqueta">{etiqueta}</span>
      <span className="adoc-pf-datos-sub-valor">{children}</span>
    </div>
  );
}

/**
 * Panel de solo lectura con la designación vigente del docente. En Cambio
 * (cuando llegan los props "solicitados") se convierte en un resumen de
 * cambios: transición `actual → solicitado` de cargo, dedicación, la carga
 * horaria de la materia y las horas de investigación/externas — para que la
 * modificación se entienda de un vistazo. Replica el bloque `datosActuales`
 * de los frames Baja/Cambio.
 */
export function DatosActualesPanel({
  antiguedad,
  cargoActual,
  cargoSolicitado,
  dedicacionActual,
  dedicacionSolicitada,
  materia,
  horasActuales,
  horasSolicitadas,
  mostrarMateria = true,
  horasInvestigacionActuales,
  horasInvestigacionSolicitadas,
  horasExternasActuales,
  horasExternasSolicitadas,
}: DatosActualesPanelProps) {
  const hayCambioCargo = Boolean(cargoSolicitado && cargoSolicitado !== cargoActual);
  const hayCambioDedicacion = Boolean(
    dedicacionSolicitada && dedicacionSolicitada !== dedicacionActual,
  );

  const columnas: { clave: string; valor: ReactNode }[] = [
    { clave: "Antigüedad", valor: `${antiguedad} ${antiguedad === 1 ? "año" : "años"}` },
    {
      clave: hayCambioCargo ? "Cargo" : "Cargo actual",
      valor:
        hayCambioCargo && cargoSolicitado ? (
          <Transicion desde={cargoActual} hacia={cargoSolicitado} />
        ) : (
          cargoActual
        ),
    },
    {
      clave: hayCambioDedicacion ? "Dedicación" : "Dedicación actual",
      valor:
        hayCambioDedicacion && dedicacionSolicitada ? (
          <Transicion desde={abreviar(dedicacionActual)} hacia={abreviar(dedicacionSolicitada)} />
        ) : (
          dedicacionActual
        ),
    },
    ...(mostrarMateria && horasSolicitadas === undefined
      ? [{ clave: "Materia", valor: materia || "—" }]
      : []),
  ];

  // En Cambio la materia no puede variar (es la cátedra), así que el resumen sólo
  // compara su carga horaria.
  const muestraMateriaConHoras = horasSolicitadas !== undefined;
  const cambioHoras = muestraMateriaConHoras && horasSolicitadas !== (horasActuales ?? 0);

  const cambioInvestigacion =
    horasInvestigacionSolicitadas !== undefined &&
    horasInvestigacionSolicitadas !== (horasInvestigacionActuales ?? 0);
  const cambioExternas =
    horasExternasSolicitadas !== undefined &&
    horasExternasSolicitadas !== (horasExternasActuales ?? 0);
  const muestraHoras =
    horasInvestigacionSolicitadas !== undefined || horasExternasSolicitadas !== undefined;

  return (
    <div className="adoc-pf-datos">
      <div className="adoc-pf-datos-strip">
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

      {muestraMateriaConHoras && (
        <div className="adoc-pf-datos-sub">
          <span className="adoc-pf-datos-sub-h">Materia</span>
          <div className="adoc-pf-datos-sub-lista">
            <div className="adoc-pf-datos-sub-fila">
              <span className="adoc-pf-datos-sub-etiqueta">{materia || "—"}</span>
              <span className="adoc-pf-datos-sub-valor">
                {cambioHoras ? (
                  <Transicion desde={`${horasActuales ?? 0}h`} hacia={`${horasSolicitadas}h`} />
                ) : (
                  `${horasActuales ?? horasSolicitadas ?? 0}h`
                )}
              </span>
            </div>
          </div>
        </div>
      )}

      {muestraHoras && (
        <div className="adoc-pf-datos-sub">
          <span className="adoc-pf-datos-sub-h">Horas</span>
          <div className="adoc-pf-datos-sub-filas">
            <FilaResumen etiqueta="Investigación">
              {cambioInvestigacion ? (
                <Transicion
                  desde={`${horasInvestigacionActuales ?? 0}h`}
                  hacia={`${horasInvestigacionSolicitadas}h`}
                />
              ) : (
                `${horasInvestigacionActuales ?? horasInvestigacionSolicitadas ?? 0}h`
              )}
            </FilaResumen>
            <FilaResumen etiqueta="Externas">
              {cambioExternas ? (
                <Transicion
                  desde={`${horasExternasActuales ?? 0}h`}
                  hacia={`${horasExternasSolicitadas}h`}
                />
              ) : (
                `${horasExternasActuales ?? horasExternasSolicitadas ?? 0}h`
              )}
            </FilaResumen>
          </div>
        </div>
      )}
    </div>
  );
}

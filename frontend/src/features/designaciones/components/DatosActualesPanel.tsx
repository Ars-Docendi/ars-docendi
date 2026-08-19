import { Fragment } from "react";
import type { ReactNode } from "react";
import type { AsignacionMateria, Cargo, Dedicacion } from "../types";

interface DatosActualesPanelProps {
  antiguedad: number;
  cargoActual: Cargo;
  /** Cambio: cargo solicitado. Si difiere del actual se muestra como transición. */
  cargoSolicitado?: Cargo;
  dedicacionActual: Dedicacion;
  /** Cambio: dedicación solicitada. Si difiere de la actual se muestra como transición. */
  dedicacionSolicitada?: Dedicacion;
  /** Materias vigentes del docente (catálogo), no lo que el usuario esté editando. */
  materiasActuales: AsignacionMateria[];
  /** Cambio: materias tal como quedan editadas en el form. Su presencia dispara la sub-sección "Materias". */
  materiasSolicitadas?: AsignacionMateria[];
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

/** Fila de materia: nombre + horas, con la misma transición (viejo tenue → nuevo en negrita) que Cargo/Dedicación — sin fondo ni ícono de aviso. */
function FilaMateriaResumen({ fila }: { fila: FilaMateria }) {
  const claseEstado =
    fila.estado === "agregada" || fila.estado === "quitada" ? ` ${fila.estado}` : "";
  return (
    <div className={`adoc-pf-datos-sub-fila${claseEstado}`}>
      <span className="adoc-pf-datos-sub-etiqueta">{fila.materia}</span>
      <span className="adoc-pf-datos-sub-valor">
        {fila.estado === "horas-cambiadas" ? (
          <Transicion desde={`${fila.horasActual}h`} hacia={`${fila.horasNueva}h`} />
        ) : (
          `${fila.horasActual ?? fila.horasNueva}h`
        )}
      </span>
    </div>
  );
}

type EstadoFilaMateria = "sin-cambios" | "horas-cambiadas" | "agregada" | "quitada";

interface FilaMateria {
  materia: string;
  horasActual?: number;
  horasNueva?: number;
  estado: EstadoFilaMateria;
}

/** Compara el listado de materias actual vs. solicitado, por nombre de materia. */
function compararMaterias(
  actuales: AsignacionMateria[],
  solicitadas: AsignacionMateria[],
): FilaMateria[] {
  const mapaActual = new Map(actuales.map((a) => [a.materia, a.horas]));
  const mapaSolicitada = new Map(solicitadas.map((a) => [a.materia, a.horas]));
  const nombres = [...new Set([...mapaActual.keys(), ...mapaSolicitada.keys()])];
  return nombres.map((materia): FilaMateria => {
    const horasActual = mapaActual.get(materia);
    const horasNueva = mapaSolicitada.get(materia);
    if (horasActual !== undefined && horasNueva !== undefined) {
      return {
        materia,
        horasActual,
        horasNueva,
        estado: horasActual === horasNueva ? "sin-cambios" : "horas-cambiadas",
      };
    }
    if (horasActual !== undefined) return { materia, horasActual, estado: "quitada" };
    return { materia, horasNueva, estado: "agregada" };
  });
}

/**
 * Panel de solo lectura con la designación vigente del docente. En Cambio
 * (cuando llegan los props "solicitados") se convierte en un resumen de
 * cambios: transición `actual → solicitado` de cargo, dedicación, cada
 * materia (con sus horas) y horas de investigación/externas — para que la
 * modificación se entienda de un vistazo. Replica el bloque `datosActuales`
 * de los frames Baja/Cambio.
 */
export function DatosActualesPanel({
  antiguedad,
  cargoActual,
  cargoSolicitado,
  dedicacionActual,
  dedicacionSolicitada,
  materiasActuales,
  materiasSolicitadas,
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
  ];

  const filasMaterias = materiasSolicitadas
    ? compararMaterias(materiasActuales, materiasSolicitadas)
    : [];

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

      {filasMaterias.length > 0 && (
        <div className="adoc-pf-datos-sub">
          <span className="adoc-pf-datos-sub-h">Materias</span>
          <div className="adoc-pf-datos-sub-lista">
            {filasMaterias.map((fila) => (
              <FilaMateriaResumen key={fila.materia} fila={fila} />
            ))}
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

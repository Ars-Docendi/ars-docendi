import { Table } from "@ars-docendi/ui";

import { MarcaSensible } from "./MarcaSensible";
import { formatearCelda } from "../utils/celdas";
import type { ColumnaDelResultado } from "../types";

interface TablaDeResultadoProps {
  columnas: ColumnaDelResultado[];
  filas: unknown[][];
  truncado: boolean;
}

/**
 * Las filas del resultado.
 *
 * Con columnas sensibles, la narración deja de ser el vehículo del dato: el modelo
 * redacta el marco («encontré 4 docentes») y el valor real llega por acá, porque
 * nunca viajó al proveedor. Sin esta tabla, una respuesta con datos personales sería
 * un párrafo con marcadores.
 *
 * LA TABLA SCROLLEA DENTRO DE SU PROPIO MARCO. El envoltorio de la librería trae
 * `overflow: hidden` y la tabla `width: 100%`: con más columnas de las que entran,
 * las de la derecha se recortaban sin aviso. La clase propia que recibe el `Table`
 * es lo que `asistente.css` usa para sobreescribirlo, sin `!important` ni fork.
 */
export function TablaDeResultado({ columnas, filas, truncado }: TablaDeResultadoProps) {
  if (columnas.length === 0 || filas.length === 0) return null;

  const haySensibles = columnas.some((columna) => columna.sensible);

  return (
    <div className="adoc-asistente-tabla">
      <Table className="adoc-asistente-tabla-wrap">
        <Table.Root>
          <Table.Head>
            <Table.Row>
              {columnas.map((columna) => (
                <Table.HeaderCell key={columna.nombre}>
                  {columna.nombre}
                  {columna.sensible && <MarcaSensible />}
                </Table.HeaderCell>
              ))}
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {filas.map((fila, indiceDeFila) => (
              // El índice como key es correcto acá: las filas no se reordenan ni se
              // editan, se renderizan una vez y se reemplazan enteras con el turno.
              <Table.Row key={indiceDeFila}>
                {fila.map((valor, indiceDeColumna) => (
                  <Table.Cell key={indiceDeColumna} numeric={typeof valor === "number"}>
                    {formatearCelda(valor)}
                  </Table.Cell>
                ))}
              </Table.Row>
            ))}
          </Table.Body>
        </Table.Root>
      </Table>

      {haySensibles && (
        // Dice qué es personal, no por dónde viajó: el enmascaramiento y el
        // proveedor son mecánica interna (RNF-18).
        <p className="adoc-asistente-leyenda-sensible">
          Las columnas con candado contienen datos personales.
        </p>
      )}

      {truncado && (
        // SIN NÚMEROS. «Ves 3 de 124» es un canal de inferencia sobre datos que el
        // usuario no puede ver: por eso el backend devuelve un booleano y nunca un
        // conteo, y la interfaz respeta la misma regla.
        <p className="adoc-asistente-truncado">
          Hay más resultados de los que se muestran. Acotá la pregunta para verlos.
        </p>
      )}
    </div>
  );
}

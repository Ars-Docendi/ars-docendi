import { Table } from "@ars-docendi/ui";

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
 */
export function TablaDeResultado({ columnas, filas, truncado }: TablaDeResultadoProps) {
  if (columnas.length === 0 || filas.length === 0) return null;

  return (
    <div className="adoc-asistente-tabla">
      <Table>
        <Table.Root>
          <Table.Head>
            <Table.Row>
              {columnas.map((columna) => (
                <Table.HeaderCell key={columna.nombre}>{columna.nombre}</Table.HeaderCell>
              ))}
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {filas.map((fila, indiceDeFila) => (
              // El índice como key es correcto acá: las filas no se reordenan ni se
              // editan, se renderizan una vez y se reemplazan enteras con el turno.
              <Table.Row key={indiceDeFila}>
                {fila.map((valor, indiceDeColumna) => (
                  <Table.Cell key={indiceDeColumna}>{formatear(valor)}</Table.Cell>
                ))}
              </Table.Row>
            ))}
          </Table.Body>
        </Table.Root>
      </Table>

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

function formatear(valor: unknown): string {
  if (valor === null || valor === undefined) return "—";
  if (typeof valor === "boolean") return valor ? "Sí" : "No";
  return String(valor);
}

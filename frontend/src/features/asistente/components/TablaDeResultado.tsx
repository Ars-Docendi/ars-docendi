import { useState } from "react";
import { Button, Table } from "@ars-docendi/ui";
import { Link } from "react-router-dom";

import { MarcaSensible } from "./MarcaSensible";
import { downloadIcon } from "../../../app/shell/icons";
import { formatearCelda } from "../utils/celdas";
import { descargarArchivo } from "../utils/descargas";
import { destinoDe } from "../utils/destinos";
import { nombreDelArchivoCsv, tablaComoCsv } from "../utils/portapapeles";
import type { ColumnaDelResultado, VinculoDelResultado } from "../types";

interface TablaDeResultadoProps {
  columnas: ColumnaDelResultado[];
  filas: unknown[][];
  truncado: boolean;
  vinculos?: VinculoDelResultado[];
  /** The turn's thread id, only to name the exported file. */
  hilo?: string;
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
 *
 * LA CELDA QUE IDENTIFICA ALGO ABRIBLE SE VUELVE ENLACE, y no hay una columna
 * «Ver» aparte: en el modal el ancho ya está comprometido y una columna más le
 * roba lugar al dato. El identificador es además lo que el usuario ya iba a copiar,
 * así que la acción queda donde la mano ya estaba.
 *
 * QUÉ CELDAS SON ENLACE NO LO DECIDE ESTE COMPONENTE. Lo decide el backend, contra
 * el módulo dueño del recurso: que una fila esté acá no significa que su pantalla
 * esté abierta para quien pregunta. Sin vínculo, la celda es texto — y el dato se
 * ve igual.
 */
export function TablaDeResultado({
  columnas,
  filas,
  truncado,
  vinculos = [],
  hilo = "",
}: TablaDeResultadoProps) {
  if (columnas.length === 0 || filas.length === 0) return null;

  const haySensibles = columnas.some((columna) => columna.sensible);
  const porCelda = new Map(vinculos.map((v) => [`${v.fila}:${v.columna}`, v]));

  return (
    <TablaConExportacion
      columnas={columnas}
      filas={filas}
      truncado={truncado}
      hilo={hilo}
      haySensibles={haySensibles}
      porCelda={porCelda}
    />
  );
}

interface TablaConExportacionProps {
  columnas: ColumnaDelResultado[];
  filas: unknown[][];
  truncado: boolean;
  hilo: string;
  haySensibles: boolean;
  porCelda: Map<string, VinculoDelResultado>;
}

/**
 * The rendering the early return above guards, plus the export action.
 *
 * Split out only so the component can call a hook (the confirmation text
 * needs state) after the early return above stays a plain, no-hook guard —
 * React does not allow a hook after a conditional return in the same
 * component.
 */
function TablaConExportacion({
  columnas,
  filas,
  truncado,
  hilo,
  haySensibles,
  porCelda,
}: TablaConExportacionProps) {
  const [confirmacion, setConfirmacion] = useState<string | null>(null);

  function exportar() {
    // Reads only the already-rendered `columnas`/`filas` — the same arrays
    // painted above, already masked by the time they got here. Nothing else
    // is read, so there is nothing to un-mask even by accident.
    const csv = tablaComoCsv(columnas, filas, truncado);
    const nombre = nombreDelArchivoCsv(hilo, new Date(), truncado);

    descargarArchivo(nombre, csv, "text/csv;charset=utf-8");
    setConfirmacion("El archivo está listo para descargar.");
  }

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
                    <Celda
                      valor={valor}
                      vinculo={porCelda.get(`${indiceDeFila}:${indiceDeColumna}`)}
                    />
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

      <div className="adoc-asistente-exportar">
        <Button variant="ghost" size="sm" leadingIcon={downloadIcon} onClick={exportar}>
          Exportar a CSV
        </Button>

        {/* Anunciado por la región viva ancestral (Conversacion.tsx, role="log"
            aria-live="polite"): no hay una región propia acá, y el foco no se
            mueve — sigue en el botón que se activó. */}
        {confirmacion && <p className="adoc-asistente-exportar-confirmacion">{confirmacion}</p>}
      </div>
    </div>
  );
}

/**
 * El contenido de una celda: texto, o enlace si lleva a algún lado.
 *
 * EL NOMBRE ACCESIBLE DICE A DÓNDE VA. «Enlace, 2026-9005» no informa nada; «Ver el
 * trámite 2026-9005» sí, y es lo único que oye quien no ve la tabla alrededor.
 */
function Celda({ valor, vinculo }: { valor: unknown; vinculo?: VinculoDelResultado }) {
  const texto = formatearCelda(valor);
  const destino = vinculo && destinoDe(vinculo.tipo);

  // Sin destino conocido queda texto. Pasa con un tipo que este cliente todavía no
  // traduce, y es la degradación correcta: el dato se lee igual.
  if (!vinculo || !destino) return <>{texto}</>;

  return (
    <Link
      className="adoc-asistente-vinculo"
      to={destino.ruta(vinculo.id)}
      aria-label={`Ver el ${destino.sustantivo} ${texto}`}
    >
      {texto}
    </Link>
  );
}

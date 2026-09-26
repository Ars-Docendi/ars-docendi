import { Table } from "@ars-docendi/ui";

import type { UsoAgregado, UsoDelAsistente } from "../types";

interface PanelDeUsoProps {
  uso: UsoDelAsistente | undefined;
  cargando: boolean;
}

/**
 * Las tres tablas del panel de uso —por usuario, por rol y organizacional
 * (asistente-panel-de-uso, tasks.md 11.3)—, sourced de
 * `GET /api/asistente/administracion/uso`.
 *
 * EL COSTO SIEMPRE LLEVA «(estimado)» AL LADO, nunca sólo en un título de
 * sección: es la etiqueta que exige la spec en el LUGAR donde se muestra el
 * valor, no en un texto que alguien puede no leer. Un turno sin precio
 * vigente NUNCA se cuenta como costo cero: se ve aparte, con su propio texto.
 */
export function PanelDeUso({ uso, cargando }: PanelDeUsoProps) {
  if (cargando) {
    return <p aria-live="polite">Cargando el panel de uso…</p>;
  }

  if (!uso) {
    return <p>No se pudo cargar el panel de uso todavía.</p>;
  }

  return (
    <div className="adoc-asistente-admin-panel-uso">
      <TablaDeUso titulo="Por usuario" etiquetaClave="Usuario" filas={uso.porUsuario} />
      <TablaDeUso titulo="Por rol" etiquetaClave="Rol" filas={uso.porRol} />
      <TablaDeUso titulo="Organización" etiquetaClave="Organización" filas={[uso.organizacion]} />
    </div>
  );
}

function TablaDeUso({
  titulo,
  etiquetaClave,
  filas,
}: {
  titulo: string;
  etiquetaClave: string;
  filas: UsoAgregado[];
}) {
  return (
    <section aria-label={titulo} className="adoc-asistente-admin-tabla-uso">
      <h3>{titulo}</h3>
      {filas.length === 0 ? (
        <p>No hay uso registrado en este período.</p>
      ) : (
        <Table className="adoc-asistente-tabla-wrap">
          <Table.Root>
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell>{etiquetaClave}</Table.HeaderCell>
                <Table.HeaderCell>Turnos</Table.HeaderCell>
                <Table.HeaderCell>Llamadas al modelo</Table.HeaderCell>
                <Table.HeaderCell>Tokens entrada/salida/caché</Table.HeaderCell>
                <Table.HeaderCell>Latencia prom. / p95</Table.HeaderCell>
                <Table.HeaderCell>Costo estimado</Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {filas.map((fila) => (
                <Table.Row key={fila.clave}>
                  <Table.Cell>{fila.nombreParaMostrar ?? fila.clave}</Table.Cell>
                  <Table.Cell numeric>{fila.turnos}</Table.Cell>
                  <Table.Cell numeric>{fila.llamadasAlModelo}</Table.Cell>
                  <Table.Cell numeric>
                    {fila.tokensDeEntrada} / {fila.tokensDeSalida} / {fila.tokensDeCache}
                  </Table.Cell>
                  <Table.Cell numeric>
                    {Math.round(fila.latenciaPromedioMs)} ms / {Math.round(fila.latenciaP95Ms)} ms
                  </Table.Cell>
                  <Table.Cell numeric>
                    <span>{formatearUsd(fila.costoEstimado)} (estimado)</span>
                    {fila.turnosSinPrecio > 0 && (
                      <span className="adoc-asistente-admin-sin-precio">
                        {fila.turnosSinPrecio} {fila.turnosSinPrecio === 1 ? "turno" : "turnos"} sin
                        precio
                      </span>
                    )}
                  </Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table.Root>
        </Table>
      )}
    </section>
  );
}

/** «US$ 12,34»: la factura del proveedor es la fuente de verdad, esto es sólo una guía. */
function formatearUsd(valor: number): string {
  return new Intl.NumberFormat("es-AR", {
    style: "currency",
    currency: "USD",
    minimumFractionDigits: 2,
  }).format(valor);
}

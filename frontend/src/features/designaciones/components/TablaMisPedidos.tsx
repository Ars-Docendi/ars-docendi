import { Table, Button } from "@ars-docendi/ui";
import type { PedidoDesignacion } from "../types";
import { EstadoPedidoBadge } from "./EstadoPedidoBadge";

interface TablaMisPedidosProps {
  pedidos: PedidoDesignacion[];
  onEditar: (pedido: PedidoDesignacion) => void;
  onEnviar: (pedido: PedidoDesignacion) => void;
  onCancelar: (pedido: PedidoDesignacion) => void;
}

/** El JC edita en borrador o cuando el pedido fue devuelto a él. */
function puedeEditar(pedido: PedidoDesignacion): boolean {
  return (
    pedido.estado === "borrador" ||
    (pedido.estado === "devuelto" && pedido.propietarioActual === "Jefe de Cátedra")
  );
}

function puedeEnviar(pedido: PedidoDesignacion): boolean {
  return pedido.estado === "borrador";
}

function puedeCancelar(pedido: PedidoDesignacion): boolean {
  return pedido.estado === "borrador";
}

export function TablaMisPedidos({ pedidos, onEditar, onEnviar, onCancelar }: TablaMisPedidosProps) {
  return (
    <Table>
      <Table.Root>
        <Table.Head>
          <Table.Row>
            <Table.HeaderCell>Docente</Table.HeaderCell>
            <Table.HeaderCell>Materia asociada</Table.HeaderCell>
            <Table.HeaderCell>Novedad</Table.HeaderCell>
            <Table.HeaderCell>Estado</Table.HeaderCell>
            <Table.HeaderCell>Acciones</Table.HeaderCell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {pedidos.map((pedido) => (
            <Table.Row key={pedido.id}>
              <Table.Cell>
                <strong>{pedido.docente.nombre}</strong>
                <div
                  style={{
                    color: "var(--color-text-tertiary)",
                    fontSize: "var(--text-body-sm-size)",
                  }}
                >
                  DNI {pedido.docente.dni}
                </div>
              </Table.Cell>
              <Table.Cell>{pedido.materiaAsociada}</Table.Cell>
              <Table.Cell>{pedido.novedad}</Table.Cell>
              <Table.Cell>
                <EstadoPedidoBadge estado={pedido.estado} prioritario={pedido.prioritario} />
              </Table.Cell>
              <Table.Cell>
                <div style={{ display: "flex", gap: "var(--space-1)" }}>
                  {puedeEditar(pedido) && (
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => onEditar(pedido)}
                      aria-label={`Editar pedido de ${pedido.docente.nombre}`}
                    >
                      Editar
                    </Button>
                  )}
                  {puedeEnviar(pedido) && (
                    <Button
                      variant="primary"
                      size="sm"
                      onClick={() => onEnviar(pedido)}
                      aria-label={`Enviar a revisión el pedido de ${pedido.docente.nombre}`}
                    >
                      Enviar
                    </Button>
                  )}
                  {puedeCancelar(pedido) && (
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => onCancelar(pedido)}
                      aria-label={`Cancelar pedido de ${pedido.docente.nombre}`}
                      style={{ color: "var(--color-text-danger)" }}
                    >
                      Cancelar
                    </Button>
                  )}
                  {!puedeEditar(pedido) && !puedeEnviar(pedido) && !puedeCancelar(pedido) && (
                    <span
                      style={{
                        color: "var(--color-text-tertiary)",
                        fontSize: "var(--text-body-sm-size)",
                      }}
                    >
                      Sin acciones
                    </span>
                  )}
                </div>
              </Table.Cell>
            </Table.Row>
          ))}
        </Table.Body>
      </Table.Root>
    </Table>
  );
}

import { useState, type FormEvent } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button, InlineAlert, Input, Table } from "@ars-docendi/ui";
import { Modal } from "../../../shared/ui/Modal";
import { propsFilaClickeable } from "../../../shared/ui/filaClickeable";
import { buscarPersonasAlta, formatearDni, type PersonaBusqueda } from "../api/catalogos";

interface PopupBuscarPersonaProps {
  open: boolean;
  onSeleccionar: (persona: PersonaBusqueda) => void;
  onCerrar: () => void;
}

/**
 * Buscador emergente de una persona ya registrada para un Alta (por ejemplo, alguien creado en
 * la administración que todavía no tiene designación). El servidor busca por apellido, nombre,
 * DNI o legajo y omite a quien ya tiene un pedido en curso en el período.
 */
export function PopupBuscarPersona({ open, onSeleccionar, onCerrar }: PopupBuscarPersonaProps) {
  const [texto, setTexto] = useState("");
  const [busqueda, setBusqueda] = useState("");
  const {
    data: personas = [],
    isFetching,
    isError,
  } = useQuery({
    queryKey: ["designaciones", "personas-alta", busqueda],
    queryFn: () => buscarPersonasAlta(busqueda),
    enabled: open && busqueda.length >= 2,
  });

  function cerrar() {
    setTexto("");
    setBusqueda("");
    onCerrar();
  }

  function buscar(evento: FormEvent) {
    evento.preventDefault();
    setBusqueda(texto.trim());
  }

  return (
    <Modal
      open={open}
      tamano="formulario"
      onOpenChange={(siguiente) => {
        if (!siguiente) cerrar();
      }}
      title="Buscar persona del sistema"
      footer={
        <Button variant="secondary" onClick={cerrar}>
          Cancelar
        </Button>
      }
    >
      <form
        onSubmit={buscar}
        style={{ display: "flex", gap: "var(--space-2)", marginBottom: "var(--space-3)" }}
      >
        <div style={{ flex: 1 }}>
          <Input
            value={texto}
            onChange={(e) => setTexto(e.target.value)}
            placeholder="Apellido, nombre, DNI o legajo"
            aria-label="Buscar persona"
            autoFocus
          />
        </div>
        <Button type="submit" variant="primary">
          Buscar
        </Button>
      </form>

      {isError && (
        <InlineAlert severity="danger" title="No se pudo buscar">
          Probá de nuevo en unos segundos.
        </InlineAlert>
      )}

      <Table>
        <Table.Root>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>Apellido y nombre</Table.HeaderCell>
              <Table.HeaderCell>DNI</Table.HeaderCell>
              <Table.HeaderCell>Legajo</Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {busqueda.length < 2 ? (
              <Table.Row>
                <Table.Cell colSpan={3} className="empty">
                  Escribí al menos dos caracteres y buscá.
                </Table.Cell>
              </Table.Row>
            ) : isFetching ? (
              <Table.Row>
                <Table.Cell colSpan={3} className="empty">
                  Buscando…
                </Table.Cell>
              </Table.Row>
            ) : personas.length === 0 ? (
              <Table.Row>
                <Table.Cell colSpan={3} className="empty">
                  No hay personas disponibles que coincidan con la búsqueda.
                </Table.Cell>
              </Table.Row>
            ) : (
              personas.map((persona) => (
                <Table.Row
                  key={persona.id}
                  {...propsFilaClickeable(() => {
                    onSeleccionar(persona);
                    cerrar();
                  })}
                  className="adoc-fila-clickeable"
                  aria-label={`Elegir a ${persona.apellido}, ${persona.nombre}`}
                >
                  <Table.Cell>
                    {persona.apellido}, {persona.nombre}
                  </Table.Cell>
                  <Table.Cell className="adoc-mono">{formatearDni(persona.documento)}</Table.Cell>
                  <Table.Cell className="adoc-mono">{persona.legajo ?? "—"}</Table.Cell>
                </Table.Row>
              ))
            )}
          </Table.Body>
        </Table.Root>
      </Table>
    </Modal>
  );
}

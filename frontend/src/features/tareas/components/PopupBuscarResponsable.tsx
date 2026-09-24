import { useState, type FormEvent } from "react";
import { Button, InlineAlert, Input, Modal, Table } from "@ars-docendi/ui";
import { useCandidatosResponsable } from "../hooks/useTareas";
import type { PersonaCandidata } from "../types";
import "./tablaTareas.css";
import "./selectorResponsable.css";

interface PopupBuscarResponsableProps {
  open: boolean;
  /** Acota los candidatos a quienes pueden ser Responsable de un Proyecto. */
  paraProyecto?: boolean;
  onSeleccionar: (persona: PersonaCandidata) => void;
  onCerrar: () => void;
}

/**
 * Buscador emergente de Responsable. Con muchos usuarios un desplegable deja de servir: se
 * busca por nombre, apellido, usuario, legajo o DNI y se elige de la lista de resultados. La
 * búsqueda la hace el servidor, que además aplica la jerarquía de asignación.
 */
export function PopupBuscarResponsable({
  open,
  paraProyecto = false,
  onSeleccionar,
  onCerrar,
}: PopupBuscarResponsableProps) {
  const [texto, setTexto] = useState("");
  const [busqueda, setBusqueda] = useState("");
  const {
    data: candidatos = [],
    isLoading,
    isError,
  } = useCandidatosResponsable(paraProyecto, open, busqueda);

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
      onOpenChange={(siguiente) => {
        if (!siguiente) cerrar();
      }}
      title="Buscar Responsable"
      footer={
        <Button variant="secondary" onClick={cerrar}>
          Cancelar
        </Button>
      }
    >
      <form className="adoc-buscador-responsable-form" onSubmit={buscar}>
        <Input
          value={texto}
          onChange={(e) => setTexto(e.target.value)}
          placeholder="Nombre, apellido, usuario, legajo o DNI"
          aria-label="Buscar responsable"
          autoFocus
        />
        <Button type="submit" variant="primary">
          Buscar
        </Button>
      </form>

      {isError && (
        <InlineAlert severity="danger" title="No se pudieron cargar los candidatos">
          Probá de nuevo en unos segundos.
        </InlineAlert>
      )}

      <div className="adoc-buscador-responsable-resultados">
        <Table>
          <Table.Root>
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell>Nombre</Table.HeaderCell>
                <Table.HeaderCell>Rol</Table.HeaderCell>
                <Table.HeaderCell>Usuario</Table.HeaderCell>
                <Table.HeaderCell>Legajo</Table.HeaderCell>
                <Table.HeaderCell>DNI</Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {isLoading ? (
                <Table.Row>
                  <Table.Cell colSpan={5} className="empty">
                    Buscando…
                  </Table.Cell>
                </Table.Row>
              ) : candidatos.length === 0 ? (
                <Table.Row>
                  <Table.Cell colSpan={5} className="empty">
                    No hay usuarios que coincidan con la búsqueda.
                  </Table.Cell>
                </Table.Row>
              ) : (
                candidatos.map((persona) => (
                  <Table.Row
                    key={persona.id}
                    className="adoc-tt-row--clicable"
                    onClick={() => {
                      onSeleccionar(persona);
                      cerrar();
                    }}
                  >
                    <Table.Cell>{persona.nombre}</Table.Cell>
                    <Table.Cell>{persona.rol}</Table.Cell>
                    <Table.Cell>{persona.usuario}</Table.Cell>
                    <Table.Cell className="adoc-mono">{persona.legajo ?? "—"}</Table.Cell>
                    <Table.Cell className="adoc-mono">{persona.documento ?? "—"}</Table.Cell>
                  </Table.Row>
                ))
              )}
            </Table.Body>
          </Table.Root>
        </Table>
      </div>
    </Modal>
  );
}

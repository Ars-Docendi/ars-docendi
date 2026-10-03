import { useState, type FormEvent } from "react";
import { Button, InlineAlert, Input, Modal, Select, Table } from "@ars-docendi/ui";
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
 * filtra por tipo de usuario y por texto (nombre, apellido, legajo o DNI) y se elige de la lista
 * de resultados. El texto lo busca el servidor, que además aplica la jerarquía de asignación.
 */
export function PopupBuscarResponsable({
  open,
  paraProyecto = false,
  onSeleccionar,
  onCerrar,
}: PopupBuscarResponsableProps) {
  const [texto, setTexto] = useState("");
  const [busqueda, setBusqueda] = useState("");
  const [tipo, setTipo] = useState("");
  const {
    data: candidatos = [],
    isLoading,
    isError,
  } = useCandidatosResponsable(paraProyecto, open, busqueda);

  const tipos = [...new Set([...candidatos.map((c) => c.rol), ...(tipo ? [tipo] : [])])].sort(
    (a, b) => a.localeCompare(b),
  );
  const visibles = tipo ? candidatos.filter((c) => c.rol === tipo) : candidatos;

  function cerrar() {
    setTexto("");
    setBusqueda("");
    setTipo("");
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
        <Select value={tipo} onChange={(e) => setTipo(e.target.value)} aria-label="Tipo de usuario">
          <option value="">Todos los tipos de usuario</option>
          {tipos.map((rol) => (
            <option key={rol} value={rol}>
              {rol}
            </option>
          ))}
        </Select>
        <div className="adoc-buscador-responsable-texto">
          <Input
            value={texto}
            onChange={(e) => setTexto(e.target.value)}
            placeholder="Nombre, apellido, legajo o DNI"
            aria-label="Buscar responsable"
            autoFocus
          />
        </div>
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
                <Table.HeaderCell>Nombre y apellido</Table.HeaderCell>
                <Table.HeaderCell>Tipo de usuario</Table.HeaderCell>
                <Table.HeaderCell>Legajo</Table.HeaderCell>
                <Table.HeaderCell>DNI</Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {isLoading ? (
                <Table.Row>
                  <Table.Cell colSpan={4} className="empty">
                    Buscando…
                  </Table.Cell>
                </Table.Row>
              ) : visibles.length === 0 ? (
                <Table.Row>
                  <Table.Cell colSpan={4} className="empty">
                    No hay usuarios que coincidan con la búsqueda.
                  </Table.Cell>
                </Table.Row>
              ) : (
                visibles.map((persona) => (
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

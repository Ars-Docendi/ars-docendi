import { useState } from "react";
import { Button, InlineAlert, Table } from "@ars-docendi/ui";
import { mensajeProblema } from "../../../shared/api/problemDetails";
import { Migas } from "../../../shared/navegacion/Migas";
import { BotonEliminarFila } from "../../../shared/ui/BotonEliminarFila";
import { ModalConfirmarEliminar } from "../../../shared/ui/ModalConfirmarEliminar";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { ModalAula } from "../components/ModalAula";
import { useCatalogoAulas } from "../hooks/useCatalogoAulas";
import type { Aula } from "../types";

type Edicion = { aula?: Aula } | null;

/** Catálogo de aulas y laboratorios que se asignan a las reservas (`aulas.gestionar`). */
export function CatalogoAulasPage() {
  const remoto = useCatalogoAulas();
  const aulas = remoto.consulta.data ?? [];
  const [edicion, setEdicion] = useState<Edicion>(null);
  const [eliminando, setEliminando] = useState<Aula | undefined>();

  function abrirEdicion(aula?: Aula) {
    remoto.guardar.reset();
    setEdicion({ aula });
  }

  function cerrarEliminar() {
    remoto.eliminar.reset();
    setEliminando(undefined);
  }

  return (
    <>
      <Migas items={[{ label: "Inicio", href: "/" }, { label: "Catálogo de aulas" }]} />
      <PageHeader
        title="Catálogo de aulas"
        meta={`${aulas.length} aula${aulas.length !== 1 ? "s" : ""}`}
        actions={
          <Button variant="primary" onClick={() => abrirEdicion()}>
            Nueva aula
          </Button>
        }
      />
      {remoto.consulta.isLoading && <p role="status">Cargando aulas…</p>}
      {remoto.consulta.isError && (
        <InlineAlert severity="danger" title="No se pudo cargar el catálogo">
          <Button variant="secondary" size="sm" onClick={() => remoto.consulta.refetch()}>
            Reintentar
          </Button>
        </InlineAlert>
      )}
      {remoto.cambiarEstado.isError && (
        <InlineAlert severity="danger" title="No se pudo cambiar el estado del aula">
          {mensajeProblema(remoto.cambiarEstado.error, "Intentá de nuevo.")}
        </InlineAlert>
      )}

      {remoto.consulta.isSuccess && (
        <Table>
          <Table.Root>
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell>Nombre</Table.HeaderCell>
                <Table.HeaderCell>Tipo</Table.HeaderCell>
                <Table.HeaderCell>Capacidad</Table.HeaderCell>
                <Table.HeaderCell>Equipamiento</Table.HeaderCell>
                <Table.HeaderCell>Estado</Table.HeaderCell>
                <Table.HeaderCell>Acciones</Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {aulas.length === 0 ? (
                <Table.Row>
                  <Table.Cell
                    colSpan={6}
                    style={{ textAlign: "center", color: "var(--color-text-tertiary)" }}
                  >
                    Todavía no hay aulas. Creá la primera para poder asignarla a las reservas.
                  </Table.Cell>
                </Table.Row>
              ) : (
                aulas.map((aula) => (
                  <Table.Row key={aula.id}>
                    <Table.Cell>{aula.nombre}</Table.Cell>
                    <Table.Cell>{aula.tipo === "laboratorio" ? "Laboratorio" : "Aula"}</Table.Cell>
                    <Table.Cell>{aula.capacidad}</Table.Cell>
                    <Table.Cell>{aula.equipamiento ?? "—"}</Table.Cell>
                    <Table.Cell>{aula.activa ? "Activa" : "Inactiva"}</Table.Cell>
                    <Table.Cell>
                      <div className="adoc-acciones-fila">
                        <Button variant="ghost" size="sm" onClick={() => abrirEdicion(aula)}>
                          Editar
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          disabled={remoto.cambiarEstado.isPending}
                          onClick={() =>
                            remoto.cambiarEstado.mutate({ aula, activa: !aula.activa })
                          }
                        >
                          {aula.activa ? "Desactivar" : "Activar"}
                        </Button>
                        <BotonEliminarFila
                          aria-label={`Eliminar ${aula.nombre}`}
                          onClick={() => setEliminando(aula)}
                        />
                      </div>
                    </Table.Cell>
                  </Table.Row>
                ))
              )}
            </Table.Body>
          </Table.Root>
        </Table>
      )}

      {edicion && (
        <ModalAula
          aula={edicion.aula}
          guardando={remoto.guardar.isPending}
          error={
            remoto.guardar.isError
              ? mensajeProblema(remoto.guardar.error, "Intentá de nuevo.")
              : undefined
          }
          onCerrar={() => setEdicion(null)}
          onGuardar={(datos) =>
            remoto.guardar.mutate(
              { id: edicion.aula?.id, datos },
              { onSuccess: () => setEdicion(null) },
            )
          }
        />
      )}

      <ModalConfirmarEliminar
        open={Boolean(eliminando)}
        onOpenChange={(abierto) => !abierto && cerrarEliminar()}
        titulo="Eliminar aula"
        objeto={
          <>
            el aula <strong>"{eliminando?.nombre}"</strong>
          </>
        }
        error={
          remoto.eliminar.isError
            ? mensajeProblema(remoto.eliminar.error, "No se pudo eliminar el aula.")
            : undefined
        }
        eliminando={remoto.eliminar.isPending}
        onConfirmar={() =>
          eliminando && remoto.eliminar.mutate(eliminando.id, { onSuccess: cerrarEliminar })
        }
      />
    </>
  );
}

import { useNavigate } from "react-router-dom";
import { Breadcrumbs, InlineAlert, Table } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { EstadoProyectoBadge } from "../components/EstadoProyectoBadge";
import { formatearFecha } from "../components/detalleAdapters";
import { useListadoProyectos } from "../hooks/useProyectos";
import type { Proyecto } from "../types";
import "../components/tablaTareas.css";

const RUTA_TAREAS = "/tareas";

/**
 * Listado con TODOS los proyectos, sin importar su Estado — es la vía de
 * acceso manual a los Finalizados/Cancelados, que no tienen cuadro en la
 * pantalla inicial (esa solo cubre los Abiertos). Tabla simple, sin el
 * modelo de filtros/orden de `TablaTareas` — no son tareas, y el volumen
 * esperado de proyectos es mucho menor.
 */
export function ListadoProyectosPage() {
  const navegar = useNavigate();
  const { data: proyectos, isLoading, isError } = useListadoProyectos();

  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[
          { label: "Inicio", href: "/" },
          { label: "Tareas", href: RUTA_TAREAS },
          { label: "Proyectos" },
        ]}
      />

      <PageHeader
        pretitle="Tareas"
        title="Proyectos"
        meta={isLoading ? "Cargando…" : `${proyectos?.length ?? 0} proyecto(s)`}
      />

      {isLoading && (
        <p role="status" aria-live="polite" style={{ color: "var(--color-text-secondary)" }}>
          Cargando los proyectos…
        </p>
      )}

      {isError && (
        <InlineAlert severity="danger" title="No se pudieron cargar los proyectos">
          Hubo un problema al obtener el listado de proyectos. Recargá la página para reintentar.
        </InlineAlert>
      )}

      {!isLoading && !isError && proyectos && (
        <div className="adoc-tabla-scroll">
          <Table>
            <Table.Root>
              <Table.Head>
                <Table.Row>
                  <Table.HeaderCell>Nombre</Table.HeaderCell>
                  <Table.HeaderCell>Responsable</Table.HeaderCell>
                  <Table.HeaderCell>Fecha de fin</Table.HeaderCell>
                  <Table.HeaderCell>Estado</Table.HeaderCell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {proyectos.length === 0 ? (
                  <Table.Row>
                    <Table.Cell colSpan={4} className="empty">
                      Todavía no hay proyectos cargados.
                    </Table.Cell>
                  </Table.Row>
                ) : (
                  proyectos.map((proyecto: Proyecto) => (
                    <Table.Row
                      key={proyecto.id}
                      className="adoc-tt-row--clicable"
                      onClick={() => navegar(`/tareas/proyectos/${proyecto.id}`)}
                    >
                      <Table.Cell>{proyecto.nombre}</Table.Cell>
                      <Table.Cell>{proyecto.responsable.nombre}</Table.Cell>
                      <Table.Cell>{formatearFecha(proyecto.fechaFin)}</Table.Cell>
                      <Table.Cell>
                        <EstadoProyectoBadge estado={proyecto.estado} />
                      </Table.Cell>
                    </Table.Row>
                  ))
                )}
              </Table.Body>
            </Table.Root>
          </Table>
        </div>
      )}
    </>
  );
}

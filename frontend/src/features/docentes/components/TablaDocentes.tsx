import { Button, StatusBadge, Table } from "@ars-docendi/ui";
import { ABREV_CARGOS, nombreCompleto, type DocenteMock } from "../mock/mockStore";

interface TablaDocentesProps {
  docentes: DocenteMock[];
  onDesactivar: (docente: DocenteMock) => void;
  onActivar: (docente: DocenteMock) => void;
  onEditar: (docente: DocenteMock) => void;
}

export function TablaDocentes({ docentes, onDesactivar, onActivar, onEditar }: TablaDocentesProps) {
  return (
    <div style={{ overflowX: "auto" }}>
      <Table>
        <Table.Root>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>Apellido y Nombre</Table.HeaderCell>
              <Table.HeaderCell>Documento</Table.HeaderCell>
              <Table.HeaderCell>Legajo</Table.HeaderCell>
              <Table.HeaderCell>Rol</Table.HeaderCell>
              <Table.HeaderCell>Asignaciones</Table.HeaderCell>
              <Table.HeaderCell style={{ whiteSpace: "nowrap", width: "1%" }}>
                Estado
              </Table.HeaderCell>
              <Table.HeaderCell style={{ whiteSpace: "nowrap", width: "1%" }}>
                Acciones
              </Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {docentes.map((docente) => (
              <Table.Row key={docente.id} data-inactivo={!docente.is_active || undefined}>
                <Table.Cell>{nombreCompleto(docente)}</Table.Cell>
                <Table.Cell className="adoc-mono">{docente.documento}</Table.Cell>
                <Table.Cell className="adoc-mono">{docente.legajo}</Table.Cell>
                <Table.Cell>
                  <div style={{ display: "flex", flexWrap: "wrap", gap: "4px" }}>
                    {docente.roles.map((r) => (
                      <span
                        key={r}
                        className={
                          r === "Jefe de Cátedra"
                            ? "adoc-badge s-aprobado"
                            : "adoc-badge s-pendiente"
                        }
                        style={{ fontSize: "11px", height: "20px", padding: "0 8px" }}
                      >
                        {r}
                      </span>
                    ))}
                  </div>
                </Table.Cell>
                <Table.Cell>
                  <div style={{ display: "flex", flexWrap: "wrap", gap: "4px" }}>
                    {docente.asignaciones.map((a) => (
                      <span
                        key={a.materia.codigo}
                        className="adoc-badge s-pendiente"
                        style={{ fontSize: "11px", height: "20px", padding: "0 8px" }}
                      >
                        {a.materia.codigo} – {ABREV_CARGOS[a.cargo]}
                      </span>
                    ))}
                  </div>
                </Table.Cell>
                <Table.Cell style={{ whiteSpace: "nowrap", width: "1%" }}>
                  <StatusBadge
                    kind={docente.is_active ? "aprobado" : "rechazado"}
                    label={docente.is_active ? "Activo" : "Inactivo"}
                  />
                </Table.Cell>
                <Table.Cell
                  className="adoc-table-actions"
                  style={{ whiteSpace: "nowrap", width: "1%" }}
                >
                  <Button variant="ghost" size="sm" onClick={() => onEditar(docente)}>
                    Editar
                  </Button>
                  {docente.is_active ? (
                    <Button variant="ghost" size="sm" onClick={() => onDesactivar(docente)}>
                      Desactivar
                    </Button>
                  ) : (
                    <Button variant="ghost" size="sm" onClick={() => onActivar(docente)}>
                      Activar
                    </Button>
                  )}
                </Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table.Root>
      </Table>
    </div>
  );
}

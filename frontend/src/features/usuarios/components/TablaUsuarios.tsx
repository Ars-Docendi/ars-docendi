import { Button, StatusBadge, Table } from "@ars-docendi/ui";
import { nombreCompleto, type UsuarioMock } from "../mock/mockStore";

interface TablaUsuariosProps {
  usuarios: UsuarioMock[];
  onDesactivar: (usuario: UsuarioMock) => void;
  onActivar: (usuario: UsuarioMock) => void;
  onEditarUsuario: (usuario: UsuarioMock) => void;
}

export function TablaUsuarios({
  usuarios,
  onDesactivar,
  onActivar,
  onEditarUsuario,
}: TablaUsuariosProps) {
  return (
    <div style={{ overflowX: "auto" }}>
      <Table>
        <Table.Root>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>Apellido y Nombre</Table.HeaderCell>
              <Table.HeaderCell>Documento</Table.HeaderCell>
              <Table.HeaderCell>Legajo</Table.HeaderCell>
              <Table.HeaderCell>UPN / Email</Table.HeaderCell>
              <Table.HeaderCell>Roles</Table.HeaderCell>
              <Table.HeaderCell>Estado</Table.HeaderCell>
              <Table.HeaderCell>Acciones</Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {usuarios.map((usuario) => (
              <Table.Row key={usuario.id} data-inactivo={!usuario.is_active || undefined}>
                <Table.Cell>{nombreCompleto(usuario)}</Table.Cell>
                <Table.Cell className="adoc-mono">{usuario.documento}</Table.Cell>
                <Table.Cell className="adoc-mono">{usuario.legajo}</Table.Cell>
                <Table.Cell>{usuario.upn}</Table.Cell>
                <Table.Cell>
                  <div style={{ display: "flex", flexWrap: "wrap", gap: "4px" }}>
                    {usuario.roles.map((r) => (
                      <span
                        key={r}
                        className="adoc-badge s-pendiente"
                        style={{ fontSize: "11px", height: "20px", padding: "0 8px" }}
                      >
                        {r}
                      </span>
                    ))}
                  </div>
                </Table.Cell>
                <Table.Cell>
                  <StatusBadge
                    kind={usuario.is_active ? "aprobado" : "rechazado"}
                    label={usuario.is_active ? "Activo" : "Inactivo"}
                  />
                </Table.Cell>
                <Table.Cell className="adoc-table-actions">
                  <Button variant="ghost" size="sm" onClick={() => onEditarUsuario(usuario)}>
                    Editar
                  </Button>
                  {usuario.is_active ? (
                    <Button variant="ghost" size="sm" onClick={() => onDesactivar(usuario)}>
                      Desactivar
                    </Button>
                  ) : (
                    <Button variant="ghost" size="sm" onClick={() => onActivar(usuario)}>
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

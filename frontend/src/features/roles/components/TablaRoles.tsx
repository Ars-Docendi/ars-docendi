import { Button, Table } from "@ars-docendi/ui";
import { ETIQUETAS_SCOPE, type RolMock } from "../mock/mockStore";

interface TablaRolesProps {
  roles: RolMock[];
  onEditar: (rol: RolMock) => void;
  onEliminar: (rol: RolMock) => void;
}

export function TablaRoles({ roles, onEditar, onEliminar }: TablaRolesProps) {
  if (roles.length === 0) {
    return (
      <div
        style={{
          padding: "2rem",
          textAlign: "center",
          color: "var(--color-text-secondary)",
          border: "1px solid var(--color-border)",
          borderRadius: "var(--radius-md)",
          marginTop: "1rem",
        }}
      >
        No se encontraron roles con el criterio de búsqueda.
      </div>
    );
  }

  return (
    <div style={{ overflowX: "auto" }}>
      <Table>
        <Table.Root>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>Nombre</Table.HeaderCell>
              <Table.HeaderCell>Descripción</Table.HeaderCell>
              <Table.HeaderCell>Ámbito</Table.HeaderCell>
              <Table.HeaderCell>Tipo</Table.HeaderCell>
              <Table.HeaderCell>Acciones</Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {roles.map((rol) => (
              <Table.Row key={rol.id}>
                <Table.Cell>
                  <strong>{rol.nombre}</strong>
                </Table.Cell>
                <Table.Cell>{rol.descripcion}</Table.Cell>
                <Table.Cell>{ETIQUETAS_SCOPE[rol.scope]}</Table.Cell>
                <Table.Cell>{rol.es_sistema ? "Sistema" : "Personalizado"}</Table.Cell>
                <Table.Cell className="adoc-table-actions">
                  <Button variant="ghost" size="sm" onClick={() => onEditar(rol)}>
                    Editar
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => onEliminar(rol)}
                    disabled={rol.es_sistema}
                    title={
                      rol.es_sistema ? "Los roles de sistema no se pueden eliminar" : undefined
                    }
                  >
                    Eliminar
                  </Button>
                </Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table.Root>
      </Table>
    </div>
  );
}

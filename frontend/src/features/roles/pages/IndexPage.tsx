import { useState, useMemo } from "react";
import { Breadcrumbs, Button } from "@ars-docendi/ui";

import { PageHeader } from "../../../shared/ui/PageHeader";
import { useConfiguracion } from "../../../shared/configuracion/useConfiguracion";
import { TablaRoles } from "../components/TablaRoles";
import { ModalNuevoRol } from "../components/ModalNuevoRol";
import { ModalEditarRol } from "../components/ModalEditarRol";
import { ModalConfirmarEliminarRol } from "../components/ModalConfirmarEliminarRol";
import { normalizarTexto, type RolMock } from "../mock/mockStore";

export function IndexPage() {
  const { roles, agregarRol, editarRol, eliminarRol } = useConfiguracion();
  const [busqueda, setBusqueda] = useState("");
  const [modalNuevo, setModalNuevo] = useState(false);
  const [rolAEditar, setRolAEditar] = useState<RolMock | null>(null);
  const [rolAEliminar, setRolAEliminar] = useState<RolMock | null>(null);

  const rolesFiltrados = useMemo(() => {
    const q = normalizarTexto(busqueda);
    if (!q) return roles;
    return roles.filter(
      (r) => normalizarTexto(r.nombre).includes(q) || normalizarTexto(r.descripcion).includes(q),
    );
  }, [roles, busqueda]);

  function handleCrear(datos: Omit<RolMock, "id">, rolBaseId: string | null) {
    agregarRol(datos, rolBaseId);
    setModalNuevo(false);
  }

  function handleEditar(datos: Omit<RolMock, "id">) {
    if (!rolAEditar) return;
    editarRol(rolAEditar.id, datos);
    setRolAEditar(null);
  }

  return (
    <>
      <Breadcrumbs separator="›" items={[{ label: "Inicio", href: "/" }, { label: "Roles" }]} />
      <PageHeader
        title="Administración de Roles"
        meta={`${roles.length} roles`}
        actions={
          <Button variant="primary" onClick={() => setModalNuevo(true)}>
            Nuevo rol
          </Button>
        }
      />

      <div style={{ marginBottom: "1rem" }}>
        <input
          type="search"
          className="adoc-input"
          placeholder="Buscar por nombre o descripción..."
          value={busqueda}
          onChange={(e) => setBusqueda(e.target.value)}
          style={{ maxWidth: "360px", width: "100%" }}
        />
      </div>

      <TablaRoles roles={rolesFiltrados} onEditar={setRolAEditar} onEliminar={setRolAEliminar} />

      <ModalNuevoRol
        open={modalNuevo}
        rolesExistentes={roles}
        nombresExistentes={roles.map((r) => r.nombre)}
        onCrear={handleCrear}
        onCerrar={() => setModalNuevo(false)}
      />

      <ModalEditarRol
        rol={rolAEditar}
        nombresExistentes={roles.filter((r) => r.id !== rolAEditar?.id).map((r) => r.nombre)}
        onGuardar={handleEditar}
        onCerrar={() => setRolAEditar(null)}
      />

      <ModalConfirmarEliminarRol
        rol={rolAEliminar}
        onConfirmar={() => {
          if (rolAEliminar) eliminarRol(rolAEliminar.id);
          setRolAEliminar(null);
        }}
        onCerrar={() => setRolAEliminar(null)}
      />
    </>
  );
}

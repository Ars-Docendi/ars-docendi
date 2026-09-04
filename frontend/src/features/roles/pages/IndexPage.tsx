import { useState, useMemo } from "react";
import { Breadcrumbs, Button } from "@ars-docendi/ui";

import { PageHeader } from "../../../shared/ui/PageHeader";
import { useRoles } from "../hooks/useRoles";
import { TablaRoles } from "../components/TablaRoles";
import { ModalNuevoRol } from "../components/ModalNuevoRol";
import { ModalEditarRol } from "../components/ModalEditarRol";
import {
  normalizarTexto,
  type DatosRolEditables,
  type DatosRolNuevo,
  type RolMock,
} from "../models";
const SIN_ROLES: RolMock[] = [];

export function IndexPage() {
  const remoto = useRoles();
  const roles = remoto.consulta.data ?? SIN_ROLES;
  const [busqueda, setBusqueda] = useState("");
  const [modalNuevo, setModalNuevo] = useState(false);
  const [rolAEditar, setRolAEditar] = useState<RolMock | null>(null);

  const rolesFiltrados = useMemo(() => {
    const q = normalizarTexto(busqueda);
    if (!q) return roles;
    return roles.filter(
      (r) => normalizarTexto(r.nombre).includes(q) || normalizarTexto(r.descripcion).includes(q),
    );
  }, [roles, busqueda]);

  function handleCrear(datos: DatosRolNuevo, rolBaseId: string | null) {
    remoto.crear.mutate({ datos, rolBaseId }, { onSuccess: () => setModalNuevo(false) });
  }

  function handleEditar(datos: DatosRolEditables) {
    if (!rolAEditar) return;
    remoto.editar.mutate({ rol: rolAEditar, datos }, { onSuccess: () => setRolAEditar(null) });
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
      {remoto.consulta.isLoading && <p role="status">Cargando roles…</p>}
      {remoto.consulta.isError && (
        <p role="alert">
          No se pudieron cargar los roles.{" "}
          <button onClick={() => remoto.consulta.refetch()}>Reintentar</button>
        </p>
      )}
      {(remoto.crear.isError || remoto.editar.isError) && (
        <p role="alert">No se pudo guardar el rol. Revisá los datos e intentá nuevamente.</p>
      )}
      {(remoto.crear.isPending || remoto.editar.isPending) && <p role="status">Guardando rol…</p>}

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

      <TablaRoles roles={rolesFiltrados} onEditar={setRolAEditar} />

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
    </>
  );
}

import { useMemo, useState } from "react";
import { Breadcrumbs, Button, Modal } from "@ars-docendi/ui";

import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { ListaRoles } from "../components/ListaRoles";
import { ModalEditarRol } from "../components/ModalEditarRol";
import { ModalNuevoRol } from "../components/ModalNuevoRol";
import { PanelPermisos } from "../components/PanelPermisos";
import { useMembresiaRol, useRoles } from "../hooks/useRoles";
import {
  normalizarTexto,
  type DatosRolEditables,
  type DatosRolNuevo,
  type RolMock,
} from "../models";
import "../roles.css";

const ROLES_VACIOS: RolMock[] = [];

export function IndexPage() {
  const usuario = useCurrentUser().user;
  const remoto = useRoles();
  const roles = remoto.consulta.data ?? ROLES_VACIOS;
  const [busqueda, setBusqueda] = useState("");
  const [rolSeleccionadoId, setRolSeleccionadoId] = useState<string | null>(null);
  const [rolAEditar, setRolAEditar] = useState<RolMock | null>(null);
  const [rolAEliminar, setRolAEliminar] = useState<RolMock | null>(null);
  const [modalNuevo, setModalNuevo] = useState(false);
  const [permisosPendientes, setPermisosPendientes] = useState<string[] | null>(null);

  const rolSeleccionado = roles.find((rol) => rol.id === rolSeleccionadoId) ?? roles[0] ?? null;
  const membresia = useMembresiaRol(rolSeleccionado);
  const permisosAsignados = permisosPendientes ?? membresia.asignados.data?.map((p) => p.id) ?? [];
  const puedeAdministrar = usuario?.permissions.includes("roles.administrar") ?? false;
  const puedeGestionarMembresia =
    usuario?.permissions.includes("roles.gestionar_membresia") ?? false;

  const rolesFiltrados = useMemo(() => {
    const q = normalizarTexto(busqueda);
    return q
      ? roles.filter(
          (rol) =>
            normalizarTexto(rol.nombre).includes(q) || normalizarTexto(rol.descripcion).includes(q),
        )
      : roles;
  }, [busqueda, roles]);

  function handleCrear(datos: DatosRolNuevo, rolBaseId: string | null) {
    remoto.crear.mutate(
      { datos, rolBaseId },
      { onSuccess: (creado) => setRolSeleccionadoId(creado.id) },
    );
  }

  function handleEditar(datos: DatosRolEditables) {
    if (!rolAEditar) return;
    remoto.editar.mutate({ rol: rolAEditar, datos }, { onSuccess: () => setRolAEditar(null) });
  }

  function handleEliminar() {
    if (!rolAEliminar) return;
    remoto.eliminar.mutate(rolAEliminar, {
      onSuccess: () => {
        setRolAEliminar(null);
        setRolSeleccionadoId(null);
      },
    });
  }

  return (
    <>
      <Breadcrumbs separator="›" items={[{ label: "Inicio", href: "/" }, { label: "Roles" }]} />
      <PageHeader
        title="Administración de Roles"
        meta={`${roles.length} roles`}
        actions={
          puedeAdministrar ? (
            <Button variant="primary" onClick={() => setModalNuevo(true)}>
              Nuevo rol
            </Button>
          ) : undefined
        }
      />
      {remoto.consulta.isLoading && <p role="status">Cargando roles…</p>}
      {remoto.consulta.isError && (
        <p role="alert">
          No se pudieron cargar los roles.{" "}
          <button onClick={() => remoto.consulta.refetch()}>Reintentar</button>
        </p>
      )}
      {(remoto.crear.isError || remoto.editar.isError || remoto.eliminar.isError) && (
        <p role="alert">
          No se pudo guardar el cambio del rol. Revisá los datos e intentá nuevamente.
        </p>
      )}
      {membresia.catalogo.isError || membresia.asignados.isError || membresia.guardar.isError ? (
        <p role="alert">No se pudo cargar o guardar la membresía. Reintentá la operación.</p>
      ) : null}

      <div className="roles-layout">
        <ListaRoles
          roles={rolesFiltrados}
          busqueda={busqueda}
          onBusquedaChange={setBusqueda}
          rolSeleccionadoId={rolSeleccionado?.id ?? null}
          onSeleccionar={(rol) => {
            setPermisosPendientes(null);
            setRolSeleccionadoId(rol.id);
          }}
          puedeAdministrar={puedeAdministrar}
          onEditar={setRolAEditar}
          onEliminar={setRolAEliminar}
        />
        {rolSeleccionado && (membresia.catalogo.isLoading || membresia.asignados.isLoading) ? (
          <p role="status">Cargando permisos…</p>
        ) : rolSeleccionado ? (
          <PanelPermisos
            rol={rolSeleccionado}
            permisos={membresia.catalogo.data ?? []}
            asignados={permisosAsignados}
            puedeEditar={puedeGestionarMembresia}
            guardando={membresia.guardar.isPending}
            onToggle={(permisoId) =>
              setPermisosPendientes((actuales) => {
                const ids = actuales ?? permisosAsignados;
                return ids.includes(permisoId)
                  ? ids.filter((id) => id !== permisoId)
                  : [...ids, permisoId];
              })
            }
            onGuardar={() => {
              void membresia.guardar.mutateAsync(permisosAsignados).then(() => {
                setPermisosPendientes(null);
              });
            }}
          />
        ) : (
          <p className="roles-empty-detail">Seleccioná un rol para ver sus permisos.</p>
        )}
      </div>

      <ModalNuevoRol
        open={modalNuevo}
        rolesExistentes={roles}
        nombresExistentes={roles.map((rol) => rol.nombre)}
        onCrear={handleCrear}
        onCerrar={() => setModalNuevo(false)}
      />
      <ModalEditarRol
        rol={rolAEditar}
        nombresExistentes={roles
          .filter((rol) => rol.id !== rolAEditar?.id)
          .map((rol) => rol.nombre)}
        onGuardar={handleEditar}
        onCerrar={() => setRolAEditar(null)}
      />
      <Modal
        open={rolAEliminar !== null}
        onOpenChange={(abierto) => !abierto && setRolAEliminar(null)}
        title="Eliminar rol"
        footer={
          <>
            <Button variant="secondary" onClick={() => setRolAEliminar(null)}>
              Cancelar
            </Button>
            <Button
              variant="destructive"
              disabled={remoto.eliminar.isPending}
              onClick={handleEliminar}
            >
              {remoto.eliminar.isPending ? "Eliminando…" : "Eliminar rol"}
            </Button>
          </>
        }
      >
        <p>
          El rol <strong>{rolAEliminar?.nombre}</strong> quedará inactivo. Sus asignaciones
          históricas y permisos se conservarán.
        </p>
      </Modal>
    </>
  );
}

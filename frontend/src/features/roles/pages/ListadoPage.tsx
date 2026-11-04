import { useMemo, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { Button, InlineAlert } from "@ars-docendi/ui";
import { useFiltrosURL, useOrdenURL } from "../../../shared/navegacion/estadoURL";
import { Migas } from "../../../shared/navegacion/Migas";
import { useAbrirDetalle } from "../../../shared/navegacion/useEstadoVolver";

import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { ListaRolesMovil } from "../components/ListaRolesMovil";
import { TablaRoles } from "../components/TablaRoles";
import { mensajeConflictoRol } from "../erroresRol";
import {
  COLUMNAS_ORDEN_ROLES,
  FILTROS_VACIOS,
  filtrarRoles,
  ordenarRoles,
  siguienteOrden,
} from "../filtrosRoles";
import { useCatalogoPermisos, useRoles } from "../hooks/useRoles";
import type { RolMock } from "../models";
import "../roles.css";
import { Modal } from "../../../shared/ui/Modal";
import { usePantallaTelefono } from "../../../shared/ui/usePantallaTelefono";

export function ListadoPage() {
  const navegar = useNavigate();
  const abrir = useAbrirDetalle();
  const aviso = (useLocation().state as { aviso?: string } | null)?.aviso;
  const permisos = useCurrentUser().user?.permissions ?? [];
  const administrar = permisos.includes("roles.administrar");
  const editarPermisos = permisos.includes("roles.gestionar_membresia");
  const { consulta, cambiarEstado } = useRoles();
  const catalogo = useCatalogoPermisos();
  const [filtros, setFiltros] = useFiltrosURL(FILTROS_VACIOS);
  const [orden, setOrden] = useOrdenURL(COLUMNAS_ORDEN_ROLES);
  const telefono = usePantallaTelefono();
  const [aConfirmar, setAConfirmar] = useState<RolMock | null>(null);

  const roles = useMemo(() => consulta.data ?? [], [consulta.data]);
  const visibles = useMemo(
    () => ordenarRoles(filtrarRoles(roles, filtros), orden),
    [roles, filtros, orden],
  );
  const activos = roles.filter((rol) => rol.activo).length;
  // Tras un conflicto la lista se recarga: se confirma sobre la versión vigente del rol.
  const vigente = aConfirmar && (roles.find((rol) => rol.id === aConfirmar.id) ?? aConfirmar);
  const activar = vigente ? !vigente.activo : false;
  const conteo =
    visibles.length === roles.length
      ? `${roles.length} roles`
      : `${visibles.length} de ${roles.length} roles`;

  const totalPermisos = catalogo.data?.length ?? 0;
  const abrirRol = (rol: RolMock) => abrir(`/roles/${rol.id}`);
  const pedirCambioEstado = administrar
    ? (rol: RolMock) => {
        cambiarEstado.reset();
        setAConfirmar(rol);
      }
    : undefined;
  const textoAbrir = administrar || editarPermisos ? "Editar" : "Ver";

  return (
    <>
      <Migas items={[{ label: "Inicio", href: "/" }, { label: "Roles" }]} />
      <PageHeader
        title="Administración de roles"
        meta={`${conteo} · ${activos} activos`}
        actions={
          administrar && (
            <Button variant="primary" onClick={() => navegar("/roles/nuevo")}>
              Nuevo rol
            </Button>
          )
        }
      />
      {aviso && <InlineAlert severity="success" title={aviso} />}
      {(consulta.isError || catalogo.isError) && (
        <InlineAlert severity="danger" title="No se pudieron cargar los roles">
          Reintentá en unos segundos.
        </InlineAlert>
      )}
      {consulta.isLoading || catalogo.isLoading ? (
        <p role="status">Cargando roles…</p>
      ) : telefono ? (
        <ListaRolesMovil
          todos={roles}
          roles={visibles}
          totalPermisos={totalPermisos}
          filtros={filtros}
          orden={orden}
          onFiltrosChange={setFiltros}
          onOrdenChange={setOrden}
          onAbrir={abrirRol}
          onCambiarEstado={pedirCambioEstado}
          textoAbrir={textoAbrir}
        />
      ) : (
        <TablaRoles
          roles={visibles}
          totalPermisos={totalPermisos}
          filtros={filtros}
          orden={orden}
          onFiltrosChange={setFiltros}
          onOrdenar={(columna) => setOrden(siguienteOrden(orden, columna))}
          onAbrir={abrirRol}
          onCambiarEstado={pedirCambioEstado}
          textoAbrir={textoAbrir}
        />
      )}

      <Modal
        open={aConfirmar !== null}
        tamano="confirmacion"
        pendiente={cambiarEstado.isPending}
        onOpenChange={(abierto) => !abierto && setAConfirmar(null)}
        title={activar ? "Activar rol" : "Desactivar rol"}
        footer={
          <>
            <Button variant="secondary" onClick={() => setAConfirmar(null)}>
              Cancelar
            </Button>
            <Button
              variant={activar ? "primary" : "destructive"}
              loading={cambiarEstado.isPending}
              onClick={() =>
                vigente &&
                cambiarEstado.mutate(
                  { rol: vigente, activar },
                  { onSuccess: () => setAConfirmar(null) },
                )
              }
            >
              {activar ? "Activar" : "Desactivar"}
            </Button>
          </>
        }
      >
        {cambiarEstado.isError && (
          <InlineAlert severity="danger" title={mensajeConflictoRol(cambiarEstado.error)} />
        )}
        {aConfirmar && (
          <p>
            ¿Seguro que querés {activar ? "activar" : "desactivar"} el rol{" "}
            <strong>{aConfirmar.nombre}</strong>?{" "}
            {activar
              ? "Las personas que lo tienen asignado vuelven a tener sus permisos."
              : "Las personas que lo tienen asignado pierden sus permisos; las asignaciones se conservan."}
          </p>
        )}
      </Modal>
    </>
  );
}

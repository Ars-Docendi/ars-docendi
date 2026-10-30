import { useMemo, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { Breadcrumbs, Button, InlineAlert, Modal } from "@ars-docendi/ui";

import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { TablaRoles } from "../components/TablaRoles";
import { mensajeConflictoRol } from "../erroresRol";
import {
  FILTROS_VACIOS,
  filtrarRoles,
  ordenarRoles,
  siguienteOrden,
  type FiltrosRoles,
  type OrdenRoles,
} from "../filtrosRoles";
import { useCatalogoPermisos, useRoles } from "../hooks/useRoles";
import type { RolMock } from "../models";
import "../roles.css";

export function ListadoPage() {
  const navegar = useNavigate();
  const aviso = (useLocation().state as { aviso?: string } | null)?.aviso;
  const permisos = useCurrentUser().user?.permissions ?? [];
  const administrar = permisos.includes("roles.administrar");
  const editarPermisos = permisos.includes("roles.gestionar_membresia");
  const { consulta, cambiarEstado } = useRoles();
  const catalogo = useCatalogoPermisos();
  const [filtros, setFiltros] = useState<FiltrosRoles>(FILTROS_VACIOS);
  const [orden, setOrden] = useState<OrdenRoles | null>(null);
  const [aConfirmar, setAConfirmar] = useState<RolMock | null>(null);

  const roles = useMemo(() => consulta.data ?? [], [consulta.data]);
  const visibles = useMemo(
    () => ordenarRoles(filtrarRoles(roles, filtros), orden),
    [roles, filtros, orden],
  );
  const activos = roles.filter((rol) => rol.activo).length;
  const activar = aConfirmar ? !aConfirmar.activo : false;
  const conteo =
    visibles.length === roles.length
      ? `${roles.length} roles`
      : `${visibles.length} de ${roles.length} roles`;

  return (
    <>
      <Breadcrumbs separator="›" items={[{ label: "Inicio", href: "/" }, { label: "Roles" }]} />
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
      ) : (
        <TablaRoles
          roles={visibles}
          totalPermisos={catalogo.data?.length ?? 0}
          filtros={filtros}
          orden={orden}
          onFiltrosChange={setFiltros}
          onOrdenar={(columna) => setOrden(siguienteOrden(orden, columna))}
          onAbrir={(rol) => navegar(`/roles/${rol.id}`)}
          onCambiarEstado={
            administrar
              ? (rol) => {
                  cambiarEstado.reset();
                  setAConfirmar(rol);
                }
              : undefined
          }
          textoAbrir={administrar || editarPermisos ? "Editar" : "Ver"}
        />
      )}

      <Modal
        open={aConfirmar !== null}
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
                aConfirmar &&
                cambiarEstado.mutate(
                  { rol: aConfirmar, activar },
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

import { useState, useMemo } from "react";
import { Breadcrumbs, Button } from "@ars-docendi/ui";
import { useSearchParams } from "react-router-dom";

import { PageHeader } from "../../../shared/ui/PageHeader";
import { TablaUsuarios } from "../components/TablaUsuarios";
import { ModalNuevoUsuario } from "../components/ModalNuevoUsuario";
import { ModalConfirmarDesactivacion } from "../components/ModalConfirmarDesactivacion";
import { ModalConfirmarActivacion } from "../components/ModalConfirmarActivacion";
import { ModalEditarUsuario } from "../components/ModalEditarRol";
import { useUsuarios } from "../hooks/useUsuarios";
import { mensajeProblema } from "../../../shared/api/problemDetails";
import {
  aplicarFiltrosYOrdenUsuarios,
  FILTROS_USUARIOS_VACIOS,
  type OrdenUsuarios,
} from "../filtrosUsuarios";
import type { UsuarioFormulario, UsuarioMock } from "../models";
import type { CatalogosUsuarios } from "../api/usuariosApi";

const SIN_USUARIOS: UsuarioMock[] = [];
const CATALOGOS_VACIOS: CatalogosUsuarios = { roles: [], carreras: [], materias: [] };

export function IndexPage() {
  const remoto = useUsuarios();
  const usuarios = remoto.usuarios.data ?? SIN_USUARIOS;
  const catalogos = remoto.catalogos.data ?? CATALOGOS_VACIOS;
  const [filtros, setFiltros] = useState(FILTROS_USUARIOS_VACIOS);
  const [orden, setOrden] = useState<OrdenUsuarios | null>(null);
  const [modalNuevo, setModalNuevo] = useState(false);
  const [usuarioADesactivar, setUsuarioADesactivar] = useState<UsuarioMock | null>(null);
  const [usuarioAActivar, setUsuarioAActivar] = useState<UsuarioMock | null>(null);
  const [usuarioAEditar, setUsuarioAEditar] = useState<UsuarioMock | null>(null);
  const [parametros, setParametros] = useSearchParams();
  const usuarioDesdeUrl = usuarios.find(
    (candidato) => candidato.persona_id === parametros.get("personaId"),
  );
  const usuarioEnEdicion = usuarioAEditar ?? usuarioDesdeUrl ?? null;

  function cerrarEdicion() {
    setUsuarioAEditar(null);
    if (parametros.has("personaId")) setParametros({}, { replace: true });
  }

  const usuariosFiltrados = useMemo(
    () => aplicarFiltrosYOrdenUsuarios(usuarios, filtros, orden),
    [usuarios, filtros, orden],
  );

  function handleCrear(datos: UsuarioFormulario) {
    remoto.crear.mutate(datos, { onSuccess: () => setModalNuevo(false) });
  }

  function handleEditar(datos: UsuarioFormulario) {
    if (!usuarioEnEdicion) return;
    remoto.editar.mutate(
      { id: usuarioEnEdicion.id, datos: { ...datos, version: usuarioEnEdicion.version } },
      { onSuccess: cerrarEdicion },
    );
  }

  function handleDesactivar() {
    if (!usuarioADesactivar) return;
    remoto.cambiarEstado.mutate(
      { usuario: usuarioADesactivar, activo: false },
      { onSuccess: () => setUsuarioADesactivar(null) },
    );
  }

  function handleActivar() {
    if (!usuarioAActivar) return;
    remoto.cambiarEstado.mutate(
      { usuario: usuarioAActivar, activo: true },
      { onSuccess: () => setUsuarioAActivar(null) },
    );
  }

  return (
    <>
      <Breadcrumbs separator="›" items={[{ label: "Inicio", href: "/" }, { label: "Usuarios" }]} />
      <PageHeader
        title="Administración de Usuarios"
        meta={`${usuariosFiltrados.length} usuarios · ${usuariosFiltrados.filter((u) => u.is_active).length} activos`}
        actions={
          <Button variant="primary" onClick={() => setModalNuevo(true)}>
            Nuevo usuario
          </Button>
        }
      />

      {remoto.usuarios.isLoading && <p role="status">Cargando usuarios…</p>}
      {remoto.usuarios.isError && (
        <p role="alert">
          No se pudieron cargar los usuarios.{" "}
          <button onClick={() => remoto.usuarios.refetch()}>Reintentar</button>
        </p>
      )}
      {!remoto.usuarios.isLoading && !remoto.usuarios.isError && usuarios.length === 0 && (
        <p>No hay usuarios para mostrar.</p>
      )}
      {(remoto.crear.error ?? remoto.editar.error ?? remoto.cambiarEstado.error) && (
        <p role="alert">
          {mensajeProblema(
            remoto.crear.error ?? remoto.editar.error ?? remoto.cambiarEstado.error,
            "No se pudo guardar el cambio. Revisá los datos e intentá nuevamente.",
          )}
        </p>
      )}
      {(remoto.crear.isPending || remoto.editar.isPending || remoto.cambiarEstado.isPending) && (
        <p role="status">Guardando usuario…</p>
      )}

      <TablaUsuarios
        usuarios={usuariosFiltrados}
        usuariosParaOpciones={usuarios}
        filtros={filtros}
        orden={orden}
        onFiltrosChange={setFiltros}
        onOrdenChange={setOrden}
        onDesactivar={setUsuarioADesactivar}
        onActivar={setUsuarioAActivar}
        onEditarUsuario={setUsuarioAEditar}
      />

      <ModalNuevoUsuario
        open={modalNuevo}
        upnsExistentes={usuarios.map((u) => u.upn)}
        onCrear={handleCrear}
        onCerrar={() => setModalNuevo(false)}
        error={
          remoto.crear.error
            ? mensajeProblema(remoto.crear.error, "No se pudo crear el usuario.")
            : undefined
        }
        catalogos={catalogos}
      />

      <ModalConfirmarDesactivacion
        usuario={usuarioADesactivar}
        onConfirmar={handleDesactivar}
        onCerrar={() => setUsuarioADesactivar(null)}
      />

      <ModalConfirmarActivacion
        usuario={usuarioAActivar}
        onConfirmar={handleActivar}
        onCerrar={() => setUsuarioAActivar(null)}
      />

      <ModalEditarUsuario
        usuario={usuarioEnEdicion}
        upnsExistentes={usuarios.filter((u) => u.id !== usuarioEnEdicion?.id).map((u) => u.upn)}
        onGuardar={handleEditar}
        onCerrar={cerrarEdicion}
        error={
          remoto.editar.error
            ? mensajeProblema(remoto.editar.error, "No se pudo editar el usuario.")
            : undefined
        }
        catalogos={catalogos}
      />
    </>
  );
}

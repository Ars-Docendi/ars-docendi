import { useState, useMemo } from "react";
import { Breadcrumbs, Button } from "@ars-docendi/ui";

import { PageHeader } from "../../../shared/ui/PageHeader";
import { TablaUsuarios } from "../components/TablaUsuarios";
import { FiltrosUsuarios, type FiltrosState } from "../components/FiltrosUsuarios";
import { ModalNuevoUsuario } from "../components/ModalNuevoUsuario";
import { ModalConfirmarDesactivacion } from "../components/ModalConfirmarDesactivacion";
import { ModalConfirmarActivacion } from "../components/ModalConfirmarActivacion";
import { ModalEditarUsuario } from "../components/ModalEditarRol";
import { useUsuarios } from "../hooks/useUsuarios";
import { mensajeProblema } from "../../../shared/api/problemDetails";
import { normalizarTexto, type UsuarioMock, type RolSistema } from "../models";

const FILTROS_VACIOS: FiltrosState = {
  apellido: "",
  nombre: "",
  documento: "",
  legajo: "",
  mail: "",
  rol: "",
  estado: "",
};
const SIN_USUARIOS: UsuarioMock[] = [];

export function IndexPage() {
  const remoto = useUsuarios();
  const usuarios = remoto.usuarios.data ?? SIN_USUARIOS;
  const rolesDisponibles = remoto.catalogos.data?.roles.map((rol) => rol.nombre) ?? [];
  const [filtros, setFiltros] = useState<FiltrosState>(FILTROS_VACIOS);
  const [modalNuevo, setModalNuevo] = useState(false);
  const [usuarioADesactivar, setUsuarioADesactivar] = useState<UsuarioMock | null>(null);
  const [usuarioAActivar, setUsuarioAActivar] = useState<UsuarioMock | null>(null);
  const [usuarioAEditar, setUsuarioAEditar] = useState<UsuarioMock | null>(null);

  const usuariosFiltrados = useMemo(() => {
    const apellido = normalizarTexto(filtros.apellido);
    const nombre = normalizarTexto(filtros.nombre);
    const doc = normalizarTexto(filtros.documento);
    const leg = normalizarTexto(filtros.legajo);
    const mail = filtros.mail.toLowerCase();
    return usuarios.filter((u) => {
      if (apellido && !normalizarTexto(u.apellido).includes(apellido)) return false;
      if (nombre && !normalizarTexto(u.nombre).includes(nombre)) return false;
      if (doc && !normalizarTexto(u.documento).includes(doc)) return false;
      if (leg && !normalizarTexto(u.legajo).includes(leg)) return false;
      if (mail && !u.upn.toLowerCase().includes(mail)) return false;
      if (filtros.rol && !u.roles.includes(filtros.rol as RolSistema)) return false;
      if (filtros.estado === "activo" && !u.is_active) return false;
      if (filtros.estado === "inactivo" && u.is_active) return false;
      return true;
    });
  }, [usuarios, filtros]);

  function handleCrear(datos: Omit<UsuarioMock, "id" | "is_active">) {
    remoto.crear.mutate(datos, { onSuccess: () => setModalNuevo(false) });
  }

  function handleEditar(datos: Omit<UsuarioMock, "id" | "is_active">) {
    if (!usuarioAEditar) return;
    remoto.editar.mutate(
      { id: usuarioAEditar.id, datos: { ...datos, version: usuarioAEditar.version } },
      { onSuccess: () => setUsuarioAEditar(null) },
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
        meta={`${usuarios.length} usuarios · ${usuarios.filter((u) => u.is_active).length} activos`}
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

      <FiltrosUsuarios filtros={filtros} onChange={setFiltros} roles={rolesDisponibles} />

      <TablaUsuarios
        usuarios={usuariosFiltrados}
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
        rolesDisponibles={rolesDisponibles}
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
        usuario={usuarioAEditar}
        upnsExistentes={usuarios.filter((u) => u.id !== usuarioAEditar?.id).map((u) => u.upn)}
        onGuardar={handleEditar}
        onCerrar={() => setUsuarioAEditar(null)}
        error={
          remoto.editar.error
            ? mensajeProblema(remoto.editar.error, "No se pudo editar el usuario.")
            : undefined
        }
        rolesDisponibles={rolesDisponibles}
      />
    </>
  );
}

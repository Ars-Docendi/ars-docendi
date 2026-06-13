import { useState, useMemo } from "react";
import { Breadcrumbs, Button } from "@ars-docendi/ui";

import { PageHeader } from "../../../shared/ui/PageHeader";
import { TablaUsuarios } from "../components/TablaUsuarios";
import { FiltrosUsuarios, type FiltrosState } from "../components/FiltrosUsuarios";
import { ModalNuevoUsuario } from "../components/ModalNuevoUsuario";
import { ModalConfirmarDesactivacion } from "../components/ModalConfirmarDesactivacion";
import { ModalConfirmarActivacion } from "../components/ModalConfirmarActivacion";
import { ModalEditarUsuario } from "../components/ModalEditarRol";
import {
  USUARIOS_INICIALES,
  agregarUsuario,
  editarUsuario,
  desactivarUsuario,
  activarUsuario,
  normalizarTexto,
  type UsuarioMock,
  type RolSistema,
} from "../mock/mockStore";

const FILTROS_VACIOS: FiltrosState = {
  apellido: "",
  nombre: "",
  documento: "",
  legajo: "",
  mail: "",
  rol: "",
  estado: "",
};

export function IndexPage() {
  const [usuarios, setUsuarios] = useState<UsuarioMock[]>(USUARIOS_INICIALES);
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
    setUsuarios((prev) => agregarUsuario(prev, datos));
    setModalNuevo(false);
  }

  function handleEditar(datos: Omit<UsuarioMock, "id" | "is_active">) {
    if (!usuarioAEditar) return;
    setUsuarios((prev) => editarUsuario(prev, usuarioAEditar.id, datos));
    setUsuarioAEditar(null);
  }

  function handleDesactivar() {
    if (!usuarioADesactivar) return;
    setUsuarios((prev) => desactivarUsuario(prev, usuarioADesactivar.id));
    setUsuarioADesactivar(null);
  }

  function handleActivar() {
    if (!usuarioAActivar) return;
    setUsuarios((prev) => activarUsuario(prev, usuarioAActivar.id));
    setUsuarioAActivar(null);
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

      <FiltrosUsuarios filtros={filtros} onChange={setFiltros} />

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
      />
    </>
  );
}

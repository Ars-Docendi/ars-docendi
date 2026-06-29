import { useState, useMemo } from "react";
import { Breadcrumbs, Button } from "@ars-docendi/ui";
import { useCurrentUser } from "../../../shared/auth/useCurrentUser";

import { PageHeader } from "../../../shared/ui/PageHeader";
import { TablaDocentes } from "../components/TablaDocentes";
import { FiltrosDocentes, type FiltrosState } from "../components/FiltrosDocentes";
import { ModalNuevoDocente } from "../components/ModalNuevoDocente";
import { ModalConfirmarDesactivacion } from "../components/ModalConfirmarDesactivacion";
import { ModalConfirmarActivacion } from "../components/ModalConfirmarActivacion";
import { ModalEditarDocente } from "../components/ModalEditarDocente";
import {
  DOCENTES_INICIALES,
  agregarDocente,
  editarDocente,
  desactivarDocente,
  activarDocente,
  normalizarTexto,
  type DocenteMock,
} from "../mock/mockStore";

const FILTROS_VACIOS: FiltrosState = {
  apellido: "",
  nombre: "",
  documento: "",
  codigoMateria: "",
  materia: "",
  cargo: "",
  rol: "",
  estado: "",
};

export function IndexPage() {
  const [docentes, setDocentes] = useState<DocenteMock[]>(DOCENTES_INICIALES);
  const [filtros, setFiltros] = useState<FiltrosState>(FILTROS_VACIOS);
  const [modalNuevo, setModalNuevo] = useState(false);
  const [docenteADesactivar, setDocenteADesactivar] = useState<DocenteMock | null>(null);
  const [docenteAActivar, setDocenteAActivar] = useState<DocenteMock | null>(null);
  const [docenteAEditar, setDocenteAEditar] = useState<DocenteMock | null>(null);

  const usuario = useCurrentUser();
  const esJdC = usuario.role === "Jefe de Cátedra";

  const materiasJdC = useMemo(() => {
    if (!esJdC) return null;
    const propio = docentes.find((d) => d.upn === usuario.upn);
    return propio ? propio.asignaciones.map((a) => a.materia.codigo) : [];
  }, [esJdC, usuario.upn, docentes]);

  const docentesFiltrados = useMemo(() => {
    const apellido = normalizarTexto(filtros.apellido);
    const nombre = normalizarTexto(filtros.nombre);
    const doc = normalizarTexto(filtros.documento);
    const cargo = normalizarTexto(filtros.cargo);
    const codigoBuscado = filtros.codigoMateria.trim();
    const materiaBuscada = filtros.materia;

    return docentes.filter((d) => {
      if (
        materiasJdC !== null &&
        !d.asignaciones.some((a) => materiasJdC.includes(a.materia.codigo))
      )
        return false;
      if (apellido && !normalizarTexto(d.apellido).includes(apellido)) return false;
      if (nombre && !normalizarTexto(d.nombre).includes(nombre)) return false;
      if (doc && !normalizarTexto(d.documento).includes(doc)) return false;
      if (cargo && !d.asignaciones.some((a) => normalizarTexto(a.cargo).includes(cargo)))
        return false;
      if (codigoBuscado && !d.asignaciones.some((a) => a.materia.codigo.includes(codigoBuscado)))
        return false;
      if (materiaBuscada && !d.asignaciones.some((a) => a.materia.codigo === materiaBuscada))
        return false;
      if (filtros.rol && !d.roles.some((r) => r === filtros.rol)) return false;
      if (filtros.estado === "activo" && !d.is_active) return false;
      if (filtros.estado === "inactivo" && d.is_active) return false;
      return true;
    });
  }, [docentes, filtros, materiasJdC]);

  function handleCrear(datos: Omit<DocenteMock, "id" | "is_active">) {
    setDocentes((prev) => agregarDocente(prev, datos));
    setModalNuevo(false);
  }

  function handleEditar(datos: Omit<DocenteMock, "id" | "is_active">) {
    if (!docenteAEditar) return;
    setDocentes((prev) => editarDocente(prev, docenteAEditar.id, datos));
    setDocenteAEditar(null);
  }

  function handleDesactivar() {
    if (!docenteADesactivar) return;
    setDocentes((prev) => desactivarDocente(prev, docenteADesactivar.id));
    setDocenteADesactivar(null);
  }

  function handleActivar() {
    if (!docenteAActivar) return;
    setDocentes((prev) => activarDocente(prev, docenteAActivar.id));
    setDocenteAActivar(null);
  }

  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[{ label: "Inicio", href: "/" }, { label: esJdC ? "Mis Docentes" : "Docentes" }]}
      />
      <PageHeader
        title={esJdC ? "Mis Docentes" : "Administración de Docentes"}
        meta={`${docentesFiltrados.length} docentes · ${docentesFiltrados.filter((d) => d.is_active).length} activos`}
        actions={
          !esJdC && (
            <Button variant="primary" onClick={() => setModalNuevo(true)}>
              Nuevo docente
            </Button>
          )
        }
      />

      <FiltrosDocentes filtros={filtros} onChange={setFiltros} />

      <TablaDocentes
        docentes={docentesFiltrados}
        onDesactivar={setDocenteADesactivar}
        onActivar={setDocenteAActivar}
        onEditar={setDocenteAEditar}
      />

      <ModalNuevoDocente
        open={modalNuevo}
        upnsExistentes={docentes.map((d) => d.upn)}
        onCrear={handleCrear}
        onCerrar={() => setModalNuevo(false)}
      />

      <ModalConfirmarDesactivacion
        docente={docenteADesactivar}
        onConfirmar={handleDesactivar}
        onCerrar={() => setDocenteADesactivar(null)}
      />

      <ModalConfirmarActivacion
        docente={docenteAActivar}
        onConfirmar={handleActivar}
        onCerrar={() => setDocenteAActivar(null)}
      />

      <ModalEditarDocente
        docente={docenteAEditar}
        upnsExistentes={docentes.filter((d) => d.id !== docenteAEditar?.id).map((d) => d.upn)}
        onGuardar={handleEditar}
        onCerrar={() => setDocenteAEditar(null)}
      />
    </>
  );
}

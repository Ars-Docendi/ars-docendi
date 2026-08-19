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
import { useDocentes } from "../hooks/useDocentes";
import { mensajeProblema } from "../../../shared/api/problemDetails";
import { normalizarTexto, type DocenteMock } from "../models";

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
const SIN_DOCENTES: DocenteMock[] = [];

export function IndexPage() {
  const remoto = useDocentes();
  const docentes = remoto.consulta.data ?? SIN_DOCENTES;
  const materias = remoto.catalogos.data?.materias ?? [];
  const cargos = remoto.catalogos.data?.cargos.map((c) => c.nombre) ?? [];
  const rolesDisponibles = remoto.catalogos.data?.roles.map((rol) => rol.nombre) ?? [];
  const personas =
    remoto.catalogos.data?.personasElegibles.map((p) => ({
      id: p.id,
      nombre: p.nombre,
      apellido: p.apellido,
      documento: p.documento,
      legajo: p.legajo ?? "",
      cuil: p.cuil ?? "",
      fecha_nacimiento: p.fechaNacimiento ?? "",
      telefono: p.telefono ?? "",
      upn: p.upn ?? "",
      version: p.version ?? undefined,
    })) ?? [];
  const [filtros, setFiltros] = useState<FiltrosState>(FILTROS_VACIOS);
  const [modalNuevo, setModalNuevo] = useState(false);
  const [docenteADesactivar, setDocenteADesactivar] = useState<DocenteMock | null>(null);
  const [docenteAActivar, setDocenteAActivar] = useState<DocenteMock | null>(null);
  const [docenteAEditar, setDocenteAEditar] = useState<DocenteMock | null>(null);

  const { user: usuario } = useCurrentUser();
  const esJdC = usuario?.role === "Jefe de Cátedra";

  const materiasJdC = useMemo(() => {
    if (!esJdC) return null;
    const propio = docentes.find((d) => d.upn === usuario?.upn);
    return propio ? propio.asignaciones.map((a) => a.materia.codigo) : [];
  }, [esJdC, usuario?.upn, docentes]);

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
    remoto.crear.mutate(datos, { onSuccess: () => setModalNuevo(false) });
  }

  function handleEditar(datos: Omit<DocenteMock, "id" | "is_active">) {
    if (!docenteAEditar) return;
    remoto.editar.mutate(
      { docente: docenteAEditar, datos },
      { onSuccess: () => setDocenteAEditar(null) },
    );
  }

  function handleDesactivar() {
    if (!docenteADesactivar) return;
    remoto.cambiarEstado.mutate(
      { docente: docenteADesactivar, activo: false },
      { onSuccess: () => setDocenteADesactivar(null) },
    );
  }

  function handleActivar() {
    if (!docenteAActivar) return;
    remoto.cambiarEstado.mutate(
      { docente: docenteAActivar, activo: true },
      { onSuccess: () => setDocenteAActivar(null) },
    );
  }

  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[{ label: "Inicio", href: "/" }, { label: esJdC ? "Mis Docentes" : "Docentes" }]}
      />
      {(remoto.consulta.isLoading || remoto.catalogos.isLoading) && (
        <p role="status">Cargando docentes…</p>
      )}
      {(remoto.consulta.isError || remoto.catalogos.isError) && (
        <p role="alert">
          No se pudieron cargar los docentes.{" "}
          <button onClick={() => remoto.consulta.refetch()}>Reintentar</button>
        </p>
      )}
      {!remoto.consulta.isLoading && !remoto.consulta.isError && docentes.length === 0 && (
        <p>No hay docentes para mostrar.</p>
      )}
      {(remoto.crear.isError || remoto.editar.isError || remoto.cambiarEstado.isError) && (
        <p role="alert">
          {mensajeProblema(
            remoto.crear.error ?? remoto.editar.error ?? remoto.cambiarEstado.error,
            "No se pudo guardar el cambio docente.",
          )}
        </p>
      )}
      {(remoto.crear.isPending || remoto.editar.isPending || remoto.cambiarEstado.isPending) && (
        <p role="status">Guardando docente…</p>
      )}
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

      <FiltrosDocentes
        filtros={filtros}
        onChange={setFiltros}
        materias={materias}
        roles={rolesDisponibles}
      />

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
        materias={materias}
        cargos={cargos}
        personas={personas}
        error={
          remoto.crear.error
            ? mensajeProblema(remoto.crear.error, "No se pudo crear el docente.")
            : undefined
        }
        rolesDisponibles={rolesDisponibles}
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
        materias={materias}
        cargos={cargos}
        error={
          remoto.editar.error
            ? mensajeProblema(remoto.editar.error, "No se pudo editar el docente.")
            : undefined
        }
        rolesDisponibles={rolesDisponibles}
      />
    </>
  );
}

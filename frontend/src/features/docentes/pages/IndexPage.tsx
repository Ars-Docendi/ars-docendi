import { useState, useMemo } from "react";
import { Breadcrumbs, Button } from "@ars-docendi/ui";
import { useSearchParams } from "react-router-dom";
import { useCurrentUser } from "../../../shared/auth/useCurrentUser";

import { PageHeader } from "../../../shared/ui/PageHeader";
import { TablaDocentes } from "../components/TablaDocentes";
import { ModalNuevoDocente } from "../components/ModalNuevoDocente";
import { ModalConfirmarEstado } from "../components/ModalConfirmarEstado";
import { ModalEditarDocente } from "../components/ModalEditarDocente";
import { useDocentes } from "../hooks/useDocentes";
import { mensajeProblema } from "../../../shared/api/problemDetails";
import {
  aplicarFiltrosYOrdenDocentes,
  FILTROS_DOCENTES_VACIOS,
  type OrdenDocentes,
} from "../filtrosDocentes";
import type { DocenteMock } from "../models";

const SIN_DOCENTES: DocenteMock[] = [];

export function IndexPage() {
  const remoto = useDocentes();
  const docentes = remoto.consulta.data ?? SIN_DOCENTES;
  const materias = remoto.catalogos.data?.materias ?? [];
  const cargos = remoto.catalogos.data?.cargos.map((c) => c.nombre) ?? [];
  const rolesDisponibles = remoto.catalogos.data?.roles ?? [];
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
  const [filtros, setFiltros] = useState(FILTROS_DOCENTES_VACIOS);
  const [orden, setOrden] = useState<OrdenDocentes | null>(null);
  const [modalNuevo, setModalNuevo] = useState(false);
  const [docenteADesactivar, setDocenteADesactivar] = useState<DocenteMock | null>(null);
  const [docenteAActivar, setDocenteAActivar] = useState<DocenteMock | null>(null);
  const [docenteAEditar, setDocenteAEditar] = useState<DocenteMock | null>(null);
  const [parametros, setParametros] = useSearchParams();
  const docenteDesdeUrl = docentes.find(
    (candidato) => candidato.persona_id === parametros.get("personaId"),
  );
  const docenteEnEdicion = docenteAEditar ?? docenteDesdeUrl ?? null;

  function cerrarEdicion() {
    setDocenteAEditar(null);
    if (parametros.has("personaId")) setParametros({}, { replace: true });
  }

  const { user: usuario } = useCurrentUser();
  const esJdC = usuario?.role === "Jefe de Cátedra";

  const docentesFiltrados = useMemo(
    () => aplicarFiltrosYOrdenDocentes(docentes, filtros, orden),
    [docentes, filtros, orden],
  );

  function handleCrear(datos: Omit<DocenteMock, "id" | "is_active">) {
    remoto.crear.mutate(datos, { onSuccess: () => setModalNuevo(false) });
  }

  function handleEditar(datos: Omit<DocenteMock, "id" | "is_active">) {
    if (!docenteEnEdicion) return;
    remoto.editar.mutate({ docente: docenteEnEdicion, datos }, { onSuccess: cerrarEdicion });
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

      <TablaDocentes
        docentes={docentesFiltrados}
        docentesParaOpciones={docentes}
        filtros={filtros}
        orden={orden}
        onFiltrosChange={setFiltros}
        onOrdenChange={setOrden}
        onDesactivar={setDocenteADesactivar}
        onActivar={setDocenteAActivar}
        onEditar={setDocenteAEditar}
        soloLectura={esJdC}
      />

      <ModalNuevoDocente
        open={modalNuevo}
        upnsExistentes={docentes.map((d) => d.upn)}
        onCrear={handleCrear}
        onCerrar={() => setModalNuevo(false)}
        materias={materias}
        cargos={cargos}
        dedicaciones={remoto.catalogos.data?.dedicaciones.filter((d) => d.activo) ?? []}
        personas={personas}
        error={
          remoto.crear.error
            ? mensajeProblema(remoto.crear.error, "No se pudo crear el docente.")
            : undefined
        }
        rolesDisponibles={rolesDisponibles}
      />

      <ModalConfirmarEstado
        docente={docenteADesactivar}
        onConfirmar={handleDesactivar}
        onCerrar={() => setDocenteADesactivar(null)}
      />

      <ModalConfirmarEstado
        docente={docenteAActivar}
        activar
        onConfirmar={handleActivar}
        onCerrar={() => setDocenteAActivar(null)}
      />

      <ModalEditarDocente
        docente={docenteEnEdicion}
        upnsExistentes={docentes.filter((d) => d.id !== docenteEnEdicion?.id).map((d) => d.upn)}
        onGuardar={handleEditar}
        onCerrar={cerrarEdicion}
        materias={materias}
        cargos={cargos}
        dedicaciones={remoto.catalogos.data?.dedicaciones.filter((d) => d.activo) ?? []}
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

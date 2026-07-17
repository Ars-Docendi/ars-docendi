import { useState } from "react";
import { Breadcrumbs, Button } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { TablaPeriodos } from "../components/TablaPeriodos";
import { ModalPeriodo } from "../components/ModalPeriodo";
import { ModalEliminarPeriodo } from "../components/ModalEliminarPeriodo";
import { ModalDesactivarPeriodo } from "../components/ModalDesactivarPeriodo";
import { PERIODOS_MOCK } from "../api/periodosMock";
import type { PeriodoDesignacion } from "../types";

export function PeriodosPage() {
  const [periodos, setPeriodos] = useState<PeriodoDesignacion[]>(PERIODOS_MOCK);

  const [modalPeriodoAbierto, setModalPeriodoAbierto] = useState(false);
  const [periodoEditando, setPeriodoEditando] = useState<PeriodoDesignacion | undefined>();
  const [clavModal, setClavModal] = useState(0);

  const [modalEliminarAbierto, setModalEliminarAbierto] = useState(false);
  const [periodoEliminando, setPeriodoEliminando] = useState<PeriodoDesignacion | undefined>();

  const [modalDesactivarAbierto, setModalDesactivarAbierto] = useState(false);
  const [periodoDesactivando, setPeriodoDesactivando] = useState<PeriodoDesignacion | undefined>();
  const [datosPendientesDesactivar, setDatosPendientesDesactivar] = useState<
    Omit<PeriodoDesignacion, "id"> | undefined
  >();

  function handleNuevoPeriodo() {
    setPeriodoEditando(undefined);
    setClavModal((c) => c + 1);
    setModalPeriodoAbierto(true);
  }

  function handleEditar(periodo: PeriodoDesignacion) {
    setPeriodoEditando(periodo);
    setClavModal((c) => c + 1);
    setModalPeriodoAbierto(true);
  }

  function handleEliminar(periodo: PeriodoDesignacion) {
    setPeriodoEliminando(periodo);
    setModalEliminarAbierto(true);
  }

  function handleGuardar(datos: Omit<PeriodoDesignacion, "id">) {
    if (periodoEditando) {
      setPeriodos((prev) =>
        prev.map((p) => (p.id === periodoEditando.id ? { ...datos, id: periodoEditando.id } : p)),
      );
    } else {
      const nuevoPeriodo: PeriodoDesignacion = {
        ...datos,
        id: crypto.randomUUID(),
      };
      setPeriodos((prev) => [nuevoPeriodo, ...prev]);
    }
    setModalPeriodoAbierto(false);
  }

  function handleConfirmarEliminar() {
    if (periodoEliminando) {
      setPeriodos((prev) => prev.filter((p) => p.id !== periodoEliminando.id));
    }
    setModalEliminarAbierto(false);
    setPeriodoEliminando(undefined);
  }

  function handleNecesitaConfirmarDesactivacion(datos: Omit<PeriodoDesignacion, "id">) {
    if (!periodoEditando) return;
    setModalPeriodoAbierto(false);
    setPeriodoDesactivando(periodoEditando);
    setDatosPendientesDesactivar(datos);
    setModalDesactivarAbierto(true);
  }

  function handleConfirmarDesactivar() {
    if (datosPendientesDesactivar) {
      handleGuardar(datosPendientesDesactivar);
    }
    setModalDesactivarAbierto(false);
    setPeriodoDesactivando(undefined);
    setDatosPendientesDesactivar(undefined);
  }

  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[{ label: "Inicio", href: "/" }, { label: "Períodos de designación" }]}
      />
      <PageHeader
        pretitle="Configuración"
        title="Períodos de designación"
        meta={`${periodos.length} período${periodos.length !== 1 ? "s" : ""}`}
        actions={
          <Button variant="primary" onClick={handleNuevoPeriodo}>
            Nuevo período
          </Button>
        }
      />

      <TablaPeriodos periodos={periodos} onEditar={handleEditar} onEliminar={handleEliminar} />

      <ModalPeriodo
        key={clavModal}
        open={modalPeriodoAbierto}
        onOpenChange={setModalPeriodoAbierto}
        periodo={periodoEditando}
        periodos={periodos}
        onGuardar={handleGuardar}
        onNecesitaConfirmarDesactivacion={handleNecesitaConfirmarDesactivacion}
      />

      <ModalEliminarPeriodo
        open={modalEliminarAbierto}
        onOpenChange={setModalEliminarAbierto}
        periodo={periodoEliminando}
        onConfirmar={handleConfirmarEliminar}
      />

      <ModalDesactivarPeriodo
        open={modalDesactivarAbierto}
        onOpenChange={setModalDesactivarAbierto}
        periodo={periodoDesactivando}
        onConfirmar={handleConfirmarDesactivar}
      />
    </>
  );
}

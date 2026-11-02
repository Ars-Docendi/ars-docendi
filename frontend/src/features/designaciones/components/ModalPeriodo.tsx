import { useState } from "react";
import { Field, Input, Button, InlineAlert, Toggle } from "@ars-docendi/ui";
import { CampoFechaISO } from "../../../shared/ui/CampoFechaISO";
import type { PeriodoDesignacion } from "../types";
import { validarPeriodo, esPeriodoValido } from "../periodoValidacion";
import type { DatosEditablesPeriodo } from "../periodoValidacion";
import { Modal } from "../../../shared/ui/Modal";

type DatosPeriodo = Omit<PeriodoDesignacion, "id">;

/** Fecha correspondiente a un mes antes de `fechaIso` (mismo día del mes anterior). */
function unMesAntes(fechaIso: string): string {
  const fecha = new Date(`${fechaIso}T00:00:00`);
  fecha.setMonth(fecha.getMonth() - 1);
  return fecha.toISOString().slice(0, 10);
}

interface ModalPeriodoProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  periodo?: PeriodoDesignacion;
  /** Resto de los períodos, para validar la regla de único período activo. */
  periodos: PeriodoDesignacion[];
  error?: string;
  onGuardar: (datos: DatosPeriodo) => void;
  /** Se dispara en vez de `onGuardar` cuando el período estaba activo y se lo desactiva: pide confirmación antes de aplicar. */
  onNecesitaConfirmarDesactivacion: (datos: DatosPeriodo) => void;
}

const FORMULARIO_VACIO: DatosEditablesPeriodo = {
  nombre: "",
  cargaDesde: "",
  cargaHasta: "",
  impactoDesde: "",
  impactoHasta: "",
  activo: false,
};

function periodoAFormulario(periodo: PeriodoDesignacion): DatosEditablesPeriodo {
  return {
    nombre: periodo.nombre,
    cargaDesde: periodo.cargaDesde,
    cargaHasta: periodo.cargaHasta,
    impactoDesde: periodo.impactoDesde,
    impactoHasta: periodo.impactoHasta,
    activo: periodo.activo,
  };
}

export function ModalPeriodo({
  open,
  onOpenChange,
  periodo,
  periodos,
  error,
  onGuardar,
  onNecesitaConfirmarDesactivacion,
}: ModalPeriodoProps) {
  const esEdicion = Boolean(periodo);
  const [formulario, setFormulario] = useState<DatosEditablesPeriodo>(() =>
    periodo ? periodoAFormulario(periodo) : FORMULARIO_VACIO,
  );
  const [errores, setErrores] = useState<ReturnType<typeof validarPeriodo>>({});
  const [mostrarErrores, setMostrarErrores] = useState(false);

  function actualizar<K extends keyof DatosEditablesPeriodo>(
    campo: K,
    valor: DatosEditablesPeriodo[K],
  ) {
    setFormulario((prev) => {
      const siguiente = { ...prev, [campo]: valor };
      if (campo === "impactoDesde" && valor && !prev.cargaHasta) {
        siguiente.cargaHasta = unMesAntes(valor as string);
      }
      return siguiente;
    });
  }

  function handleGuardar() {
    const erroresActuales = validarPeriodo(formulario, {
      periodosExistentes: periodos,
      periodoActualId: periodo?.id,
    });
    setErrores(erroresActuales);
    setMostrarErrores(true);
    if (!esPeriodoValido(erroresActuales)) {
      return;
    }

    const datos: DatosPeriodo = {
      nombre: formulario.nombre,
      cargaDesde: formulario.cargaDesde,
      cargaHasta: formulario.cargaHasta,
      impactoDesde: formulario.impactoDesde,
      impactoHasta: formulario.impactoHasta,
      activo: formulario.activo,
    };

    const seDesactiva = (periodo?.activo ?? false) && !formulario.activo;
    if (seDesactiva) {
      onNecesitaConfirmarDesactivacion(datos);
      return;
    }
    onGuardar(datos);
  }

  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title={esEdicion ? "Editar período" : "Nuevo período"}
      sucio={
        JSON.stringify(formulario) !==
        JSON.stringify(periodo ? periodoAFormulario(periodo) : FORMULARIO_VACIO)
      }
      footer={
        <>
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            Cancelar
          </Button>
          <Button variant="primary" onClick={handleGuardar}>
            Guardar
          </Button>
        </>
      }
    >
      <div className="adoc-form">
        {error && (
          <InlineAlert severity="warning" title="Atención">
            {error}
          </InlineAlert>
        )}

        <Field label="Nombre" required error={mostrarErrores ? errores.nombre : undefined}>
          <Input
            value={formulario.nombre}
            onChange={(e) => actualizar("nombre", e.target.value)}
            placeholder="Nombre del período"
          />
        </Field>

        <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-2)" }}>
          <p
            style={{
              margin: 0,
              fontWeight: 600,
              fontSize: "var(--text-body-sm-size)",
              color: "var(--color-text-primary)",
            }}
          >
            Ventana para cargar pedidos
          </p>

          <div className="adoc-form-fila cols-2">
            <Field label="Desde" required>
              <CampoFechaISO
                value={formulario.cargaDesde}
                onChange={(valor) => actualizar("cargaDesde", valor)}
              />
            </Field>

            <Field label="Hasta" required error={mostrarErrores ? errores.cargaHasta : undefined}>
              <CampoFechaISO
                value={formulario.cargaHasta}
                onChange={(valor) => actualizar("cargaHasta", valor)}
              />
            </Field>
          </div>
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-2)" }}>
          <p
            style={{
              margin: 0,
              fontWeight: 600,
              fontSize: "var(--text-body-sm-size)",
              color: "var(--color-text-primary)",
            }}
          >
            Período de impacto de la designación
          </p>

          <div className="adoc-form-fila cols-2">
            <Field label="Desde" required error={mostrarErrores ? errores.impactoDesde : undefined}>
              <CampoFechaISO
                value={formulario.impactoDesde}
                onChange={(valor) => actualizar("impactoDesde", valor)}
              />
            </Field>

            <Field label="Hasta" required error={mostrarErrores ? errores.impactoHasta : undefined}>
              <CampoFechaISO
                value={formulario.impactoHasta}
                onChange={(valor) => actualizar("impactoHasta", valor)}
              />
            </Field>
          </div>
        </div>

        <Field error={mostrarErrores ? errores.activo : undefined}>
          <Toggle
            label="Período activo"
            checked={formulario.activo}
            onChange={(e) => actualizar("activo", e.target.checked)}
          />
        </Field>
      </div>
    </Modal>
  );
}

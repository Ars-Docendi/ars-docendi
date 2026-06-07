import { useState } from "react";
import { Modal, Field, Input, Select, DatePicker, Button, InlineAlert } from "@ars-docendi/ui";
import type { EstadoPeriodo, PeriodoDesignacion } from "../types";

type DatosPeriodo = Omit<PeriodoDesignacion, "id">;

function calcularDuracionDias(apertura: string, cierre: string): number | null {
  if (!apertura || !cierre) return null;
  const diff = new Date(cierre).getTime() - new Date(apertura).getTime();
  if (diff < 0) return null;
  return Math.round(diff / (1000 * 60 * 60 * 24)) + 1;
}

interface ModalPeriodoProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  periodo?: PeriodoDesignacion;
  error?: string;
  onGuardar: (datos: DatosPeriodo) => void;
}

interface EstadoFormulario {
  nombre: string;
  cuatrimestre: "1C" | "2C" | "Verano";
  anio: string;
  fechaApertura: string;
  fechaCierre: string;
  estado: EstadoPeriodo;
}

const FORMULARIO_VACIO: EstadoFormulario = {
  nombre: "",
  cuatrimestre: "1C",
  anio: String(new Date().getFullYear()),
  fechaApertura: "",
  fechaCierre: "",
  estado: "abierto",
};

function periodoAFormulario(periodo: PeriodoDesignacion): EstadoFormulario {
  return {
    nombre: periodo.nombre,
    cuatrimestre: periodo.cuatrimestre,
    anio: String(periodo.anio),
    fechaApertura: periodo.fechaApertura,
    fechaCierre: periodo.fechaCierre,
    estado: periodo.estado,
  };
}

export function ModalPeriodo({ open, onOpenChange, periodo, error, onGuardar }: ModalPeriodoProps) {
  const esEdicion = Boolean(periodo);
  const [formulario, setFormulario] = useState<EstadoFormulario>(() =>
    periodo ? periodoAFormulario(periodo) : FORMULARIO_VACIO,
  );

  function actualizar<K extends keyof EstadoFormulario>(campo: K, valor: EstadoFormulario[K]) {
    setFormulario((prev) => ({ ...prev, [campo]: valor }));
  }

  function handleGuardar() {
    onGuardar({
      nombre: formulario.nombre,
      cuatrimestre: formulario.cuatrimestre,
      anio: Number(formulario.anio),
      fechaApertura: formulario.fechaApertura,
      fechaCierre: formulario.fechaCierre,
      estado: formulario.estado,
    });
  }

  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title={esEdicion ? "Editar período" : "Nuevo período"}
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
      <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
        {error && (
          <InlineAlert severity="warning" title="No se pudo guardar">
            {error}
          </InlineAlert>
        )}

        <Field label="Nombre" required>
          <Input
            value={formulario.nombre}
            onChange={(e) => actualizar("nombre", e.target.value)}
            placeholder="Ej. 1er cuatrimestre 2026"
          />
        </Field>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "var(--space-4)" }}>
          <Field label="Cuatrimestre" required>
            <Select
              value={formulario.cuatrimestre}
              onChange={(e) => actualizar("cuatrimestre", e.target.value as "1C" | "2C" | "Verano")}
            >
              <option value="1C">1C</option>
              <option value="2C">2C</option>
              <option value="Verano">Verano</option>
            </Select>
          </Field>

          <Field label="Año" required>
            <Input
              type="number"
              value={formulario.anio}
              onChange={(e) => actualizar("anio", e.target.value)}
              min={2020}
              max={2099}
            />
          </Field>
        </div>

        <Field label="Fecha de apertura" required>
          <DatePicker
            value={formulario.fechaApertura}
            onChange={(e) => actualizar("fechaApertura", e.target.value)}
          />
        </Field>

        <Field label="Fecha de cierre" required>
          <DatePicker
            value={formulario.fechaCierre}
            onChange={(e) => actualizar("fechaCierre", e.target.value)}
          />
        </Field>

        {calcularDuracionDias(formulario.fechaApertura, formulario.fechaCierre) !== null && (
          <p
            style={{
              margin: 0,
              fontSize: "var(--text-body-sm-size)",
              color: "var(--color-text-secondary)",
            }}
          >
            Duración:{" "}
            <strong style={{ color: "var(--color-text-primary)" }}>
              {calcularDuracionDias(formulario.fechaApertura, formulario.fechaCierre)} días
            </strong>
          </p>
        )}

        <Field label="Estado" required>
          <Select
            value={formulario.estado}
            onChange={(e) => actualizar("estado", e.target.value as EstadoPeriodo)}
          >
            <option value="abierto">Abierto</option>
            <option value="proximo">Próximo</option>
            <option value="cerrado">Cerrado</option>
          </Select>
        </Field>
      </div>
    </Modal>
  );
}

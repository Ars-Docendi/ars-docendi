import { useState } from "react";
import { Button, DatePicker, Field, InlineAlert, Input, Modal, Textarea } from "@ars-docendi/ui";
import { SelectorResponsable } from "./SelectorResponsable";
import { PERSONAS_CANDIDATAS } from "../api/personasSeed";
import { puedeAsignarComoResponsableProyecto } from "../api/maquinaEstadosProyecto";
import type { ActorTarea, DatosEditablesProyecto } from "../types";

interface ModalNuevoProyectoProps {
  open: boolean;
  actor: ActorTarea;
  onGuardar: (datos: DatosEditablesProyecto) => void;
  onCerrar: () => void;
  guardando?: boolean;
  error?: string;
}

const VACIO = {
  nombre: "",
  descripcion: "",
  fechaInicio: "",
  fechaFin: "",
  responsable: "",
};

/**
 * Formulario de alta de Proyecto: Nombre, Descripción, Fecha Inicio/Fin y
 * Responsable — restringido a Secretaría Académica o Decanato, respetando
 * además la jerarquía de asignación (ver `maquinaEstadosProyecto.ts`).
 */
export function ModalNuevoProyecto({
  open,
  actor,
  onGuardar,
  onCerrar,
  guardando = false,
  error,
}: ModalNuevoProyectoProps) {
  const [campos, setCampos] = useState(VACIO);
  const [enviado, setEnviado] = useState(false);

  const candidatosResponsable = PERSONAS_CANDIDATAS.filter((p) =>
    puedeAsignarComoResponsableProyecto(actor, p),
  );

  function set<K extends keyof typeof VACIO>(campo: K, valor: (typeof VACIO)[K]) {
    setCampos((p) => ({ ...p, [campo]: valor }));
  }

  function handleCerrar() {
    setCampos(VACIO);
    setEnviado(false);
    onCerrar();
  }

  function handleConfirmar() {
    setEnviado(true);
    const { nombre, fechaInicio, fechaFin, responsable } = campos;
    if (!nombre || !fechaInicio || !fechaFin || !responsable) return;
    if (fechaFin < fechaInicio) return;

    const persona = candidatosResponsable.find((p) => p.nombre === responsable);
    if (!persona) return;

    onGuardar({
      nombre,
      descripcion: campos.descripcion,
      fechaInicio,
      fechaFin,
      responsable: persona,
    });
  }

  const fechaFinInvalida = enviado && !!campos.fechaFin && campos.fechaFin < campos.fechaInicio;
  const grilla: React.CSSProperties = {
    display: "grid",
    gridTemplateColumns: "1fr 1fr",
    gap: "1.25rem",
  };

  return (
    <Modal
      open={open}
      onOpenChange={(next) => {
        if (!next) handleCerrar();
      }}
      title="Nuevo Proyecto"
      footer={
        <>
          <Button variant="secondary" onClick={handleCerrar}>
            Cancelar
          </Button>
          <Button variant="primary" onClick={handleConfirmar} loading={guardando}>
            Crear proyecto
          </Button>
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        {error && (
          <InlineAlert severity="danger" title="No se pudo guardar el proyecto">
            {error}
          </InlineAlert>
        )}

        <Field
          label="Nombre"
          required
          error={enviado && !campos.nombre ? "Campo obligatorio" : undefined}
        >
          <Input
            value={campos.nombre}
            onChange={(e) => set("nombre", e.target.value)}
            placeholder="Ej: Nuevo sistema de Ingeniería para Testing"
          />
        </Field>

        <Field label="Descripción">
          <Textarea
            value={campos.descripcion}
            onChange={(e) => set("descripcion", e.target.value)}
            placeholder="Detalle del alcance del proyecto…"
            rows={3}
          />
        </Field>

        <div style={grilla}>
          <Field
            label="Fecha de inicio"
            required
            error={enviado && !campos.fechaInicio ? "Campo obligatorio" : undefined}
          >
            <DatePicker
              value={campos.fechaInicio}
              onChange={(e) => set("fechaInicio", e.target.value)}
            />
          </Field>
          <Field
            label="Fecha de fin"
            required
            error={
              enviado && !campos.fechaFin
                ? "Campo obligatorio"
                : fechaFinInvalida
                  ? "Debe ser posterior o igual a la Fecha de inicio"
                  : undefined
            }
          >
            <DatePicker value={campos.fechaFin} onChange={(e) => set("fechaFin", e.target.value)} />
          </Field>
        </div>

        <Field
          label="Responsable"
          required
          error={enviado && !campos.responsable ? "Campo obligatorio" : undefined}
        >
          <SelectorResponsable
            valor={campos.responsable}
            onChange={(nombre) => set("responsable", nombre)}
            personas={candidatosResponsable}
            ariaLabel="Responsable del proyecto"
            invalid={enviado && !campos.responsable}
          />
        </Field>
      </div>
    </Modal>
  );
}

import { useState } from "react";
import {
  Button,
  DatePicker,
  Field,
  InlineAlert,
  Input,
  Modal,
  Select,
  Textarea,
} from "@ars-docendi/ui";
import { SelectorResponsable } from "./SelectorResponsable";
import { PERSONAS_CANDIDATAS } from "../api/personasSeed";
import type { DatosEditablesTarea, Prioridad, Tarea } from "../types";

interface ModalNuevaTareaProps {
  open: boolean;
  /** Presente en modo edición: precarga el formulario con sus datos. */
  tarea?: Tarea;
  onGuardar: (datos: DatosEditablesTarea) => void;
  onCerrar: () => void;
  guardando?: boolean;
  error?: string;
}

const VACIO = {
  titulo: "",
  descripcion: "",
  fechaInicio: "",
  fechaFin: "",
  prioridad: "" as Prioridad | "",
  responsable: "",
};

function datosIniciales(tarea: Tarea | undefined): typeof VACIO {
  if (!tarea) return VACIO;
  return {
    titulo: tarea.titulo,
    descripcion: tarea.descripcion,
    fechaInicio: tarea.fechaInicio,
    fechaFin: tarea.fechaFin,
    prioridad: tarea.prioridad,
    responsable: tarea.responsable.nombre,
  };
}

/**
 * Formulario de alta/edición de tarea: Título, Descripción, Fecha Inicio/Fin,
 * Prioridad, Responsable. Con `tarea` presente arranca precargado en modo
 * edición (exclusivo de la autoridad creadora); sin ella, es "Nueva Tarea".
 */
export function ModalNuevaTarea({
  open,
  tarea,
  onGuardar,
  onCerrar,
  guardando = false,
  error,
}: ModalNuevaTareaProps) {
  const [campos, setCampos] = useState(() => datosIniciales(tarea));
  const [enviado, setEnviado] = useState(false);

  // Repone el formulario cuando el modal se vuelve a abrir (alta o edición
  // de otra tarea): patrón "ajustar estado durante el render" de React, no
  // un efecto — evita el render en cascada de `useEffect`.
  const [abiertoPrevio, setAbiertoPrevio] = useState(open);
  if (open !== abiertoPrevio) {
    setAbiertoPrevio(open);
    if (open) setCampos(datosIniciales(tarea));
  }

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
    const { titulo, fechaInicio, fechaFin, prioridad, responsable } = campos;
    if (!titulo || !fechaInicio || !fechaFin || !prioridad || !responsable) return;
    if (fechaFin < fechaInicio) return;

    const persona = PERSONAS_CANDIDATAS.find((p) => p.nombre === responsable);
    if (!persona) return;

    onGuardar({
      titulo,
      descripcion: campos.descripcion,
      fechaInicio,
      fechaFin,
      prioridad,
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
      title={tarea ? "Editar tarea" : "Nueva Tarea"}
      footer={
        <>
          <Button variant="secondary" onClick={handleCerrar}>
            Cancelar
          </Button>
          <Button variant="primary" onClick={handleConfirmar} loading={guardando}>
            {tarea ? "Guardar cambios" : "Crear tarea"}
          </Button>
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        {error && (
          <InlineAlert severity="danger" title="No se pudo guardar la tarea">
            {error}
          </InlineAlert>
        )}

        <Field
          label="Título"
          required
          error={enviado && !campos.titulo ? "Campo obligatorio" : undefined}
        >
          <Input
            value={campos.titulo}
            onChange={(e) => set("titulo", e.target.value)}
            placeholder="Ej: Revisar disponibilidad de aulas"
          />
        </Field>

        <Field label="Descripción">
          <Textarea
            value={campos.descripcion}
            onChange={(e) => set("descripcion", e.target.value)}
            placeholder="Detalle de qué hay que hacer…"
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

        <div style={grilla}>
          <Field
            label="Prioridad"
            required
            error={enviado && !campos.prioridad ? "Campo obligatorio" : undefined}
          >
            <Select
              value={campos.prioridad}
              onChange={(e) => set("prioridad", e.target.value as Prioridad)}
            >
              <option value="">Seleccioná una prioridad…</option>
              <option value="alta">Alta</option>
              <option value="media">Media</option>
              <option value="baja">Baja</option>
            </Select>
          </Field>
          <Field
            label="Responsable"
            required
            error={enviado && !campos.responsable ? "Campo obligatorio" : undefined}
          >
            <SelectorResponsable
              valor={campos.responsable}
              onChange={(nombre) => set("responsable", nombre)}
              ariaLabel="Responsable de la tarea"
              invalid={enviado && !campos.responsable}
            />
          </Field>
        </div>
      </div>
    </Modal>
  );
}

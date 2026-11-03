import { useState } from "react";
import { Button, Field, InlineAlert, Input, Textarea } from "@ars-docendi/ui";
import { SelectorResponsable } from "./SelectorResponsable";
import type { DatosEditablesProyecto, PersonaTarea, Proyecto } from "../types";
import "../../../shared/ui/modalEstirable.css";
import "./modalesEstirables.css";
import { CampoFechaISO } from "../../../shared/ui/CampoFechaISO";
import { Modal } from "../../../shared/ui/Modal";

interface ModalProyectoProps {
  open: boolean;
  /** Presente en modo edición: precarga el formulario con sus datos. */
  proyecto?: Proyecto;
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
  responsable: null as PersonaTarea | null,
};

function datosIniciales(proyecto: Proyecto | undefined): typeof VACIO {
  if (!proyecto) return VACIO;
  return {
    nombre: proyecto.nombre,
    descripcion: proyecto.descripcion,
    fechaInicio: proyecto.fechaInicio,
    fechaFin: proyecto.fechaFin,
    responsable: proyecto.responsable,
  };
}

/**
 * Formulario de alta/edición de Proyecto: Nombre, Descripción, Fecha Inicio/Fin y
 * Responsable — restringido a Secretaría Académica o Decanato, respetando además la
 * jerarquía de asignación (la valida el servidor). Con `proyecto` arranca precargado
 * en modo edición; sin él, es "Nuevo Proyecto". El Estado no se edita acá.
 */
export function ModalProyecto({
  open,
  proyecto,
  onGuardar,
  onCerrar,
  guardando = false,
  error,
}: ModalProyectoProps) {
  const [campos, setCampos] = useState(() => datosIniciales(proyecto));
  const [enviado, setEnviado] = useState(false);

  // Repone el formulario cuando el modal se vuelve a abrir (alta o edición): patrón
  // "ajustar estado durante el render", no un efecto.
  const [abiertoPrevio, setAbiertoPrevio] = useState(open);
  if (open !== abiertoPrevio) {
    setAbiertoPrevio(open);
    if (open) setCampos(datosIniciales(proyecto));
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
    const { nombre, fechaInicio, fechaFin, responsable } = campos;
    if (!nombre || !fechaInicio || !fechaFin || !responsable) return;
    if (fechaFin < fechaInicio) return;

    onGuardar({
      nombre,
      descripcion: campos.descripcion,
      fechaInicio,
      fechaFin,
      responsableId: responsable.id,
    });
  }

  const fechaFinInvalida = enviado && !!campos.fechaFin && campos.fechaFin < campos.fechaInicio;

  return (
    <Modal
      open={open}
      className="adoc-modal--estirable adoc-modal-tareas"
      tamano="libre"
      pendiente={guardando}
      sucio={JSON.stringify(campos) !== JSON.stringify(datosIniciales(proyecto))}
      onOpenChange={(next) => {
        if (!next) handleCerrar();
      }}
      title={proyecto ? "Editar Proyecto" : "Nuevo Proyecto"}
      footer={
        <>
          <Button variant="secondary" onClick={handleCerrar}>
            Cancelar
          </Button>
          <Button variant="primary" onClick={handleConfirmar} loading={guardando}>
            {proyecto ? "Guardar cambios" : "Crear proyecto"}
          </Button>
        </>
      }
    >
      <div className="adoc-form">
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

        <div className="adoc-form-fila cols-2">
          <Field
            label="Fecha de inicio"
            required
            error={enviado && !campos.fechaInicio ? "Campo obligatorio" : undefined}
          >
            <CampoFechaISO
              value={campos.fechaInicio}
              onChange={(valor) => set("fechaInicio", valor)}
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
            <CampoFechaISO value={campos.fechaFin} onChange={(valor) => set("fechaFin", valor)} />
          </Field>
        </div>

        <Field
          label="Responsable"
          required
          error={enviado && !campos.responsable ? "Campo obligatorio" : undefined}
        >
          <SelectorResponsable
            valor={campos.responsable}
            onChange={(persona) => set("responsable", persona)}
            paraProyecto
            ariaLabel="Responsable del proyecto"
            invalid={enviado && !campos.responsable}
          />
        </Field>
      </div>
    </Modal>
  );
}

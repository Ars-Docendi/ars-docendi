import { useState } from "react";
import { Button, Field, InlineAlert, Input, Modal } from "@ars-docendi/ui";
import type { SolicitudReservaAula } from "../types";
import { fechaSolicitud } from "./filtrosSolicitudes";

export type ModoAsignarAula = "asignar" | "actualizar";

interface ModalAsignarAulaProps {
  open: boolean;
  modo: ModoAsignarAula;
  onOpenChange: (open: boolean) => void;
  solicitud: SolicitudReservaAula | undefined;
  error?: string;
  asignando?: boolean;
  onConfirmar: (aulaAsignada: string) => void;
  /** Sólo en modo "asignar": abre el flujo de rechazo en vez de asignar un aula. */
  onRechazar?: (solicitud: SolicitudReservaAula) => void;
}

const TEXTOS: Record<ModoAsignarAula, { titulo: string; boton: string; errorTitulo: string }> = {
  asignar: {
    titulo: "Asignar aula",
    boton: "Asignar y aprobar",
    errorTitulo: "No se pudo asignar el aula",
  },
  actualizar: {
    titulo: "Actualizar aula asignada",
    boton: "Actualizar aula",
    errorTitulo: "No se pudo actualizar el aula",
  },
};

/**
 * Asignación/actualización del aula de una solicitud (Administrativo). Un solo modal para las dos
 * acciones: "asignar" aprueba una Pendiente, "actualizar" corrige el aula de una ya Aprobada sin
 * cambiar su estado — ver openspec/changes/reserva-aulas/design.md (decisión 8).
 * <p>
 * El padre lo remonta con un `key` distinto por solicitud/modo (patrón "reset state on prop change"
 * de React): así el valor inicial del input se recalcula solo, sin useEffect ni setState en efecto.
 */
export function ModalAsignarAula({
  open,
  modo,
  onOpenChange,
  solicitud,
  error,
  asignando = false,
  onConfirmar,
  onRechazar,
}: ModalAsignarAulaProps) {
  const [aula, setAula] = useState(() =>
    modo === "actualizar" ? (solicitud?.aulaAsignada ?? "") : "",
  );
  const [mostrarError, setMostrarError] = useState(false);

  function handleConfirmar() {
    if (!aula.trim()) {
      setMostrarError(true);
      return;
    }
    onConfirmar(aula.trim());
  }

  const textos = TEXTOS[modo];

  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title={textos.titulo}
      footer={
        <>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={asignando}>
            Cancelar
          </Button>
          {modo === "asignar" && onRechazar && (
            <Button
              variant="destructive"
              disabled={asignando}
              onClick={() => solicitud && onRechazar(solicitud)}
            >
              Rechazar solicitud
            </Button>
          )}
          <Button variant="primary" onClick={handleConfirmar} loading={asignando}>
            {textos.boton}
          </Button>
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
        {error && (
          <InlineAlert severity="danger" title={textos.errorTitulo}>
            {error}
          </InlineAlert>
        )}
        <p style={{ margin: 0, color: "var(--color-text-primary)" }}>
          Solicitud del <strong>{solicitud ? fechaSolicitud(solicitud) : ""}</strong> para{" "}
          <strong>{solicitud?.materia.nombre}</strong> ({solicitud?.comision}) —{" "}
          {solicitud?.cantidadAlumnosAprox} alumnos aprox.
        </p>
        <Field
          label="Aula o laboratorio"
          required
          error={mostrarError ? "Indicá el aula a asignar." : undefined}
        >
          <Input
            value={aula}
            placeholder="Ej: Aula 204"
            onChange={(e) => setAula(e.target.value)}
          />
        </Field>
      </div>
    </Modal>
  );
}

import { useState } from "react";
import {
  Button,
  Field,
  FileUpload,
  InlineAlert,
  Input,
  Radio,
  Select,
  Textarea,
} from "@ars-docendi/ui";
import type { UploadedFile } from "@ars-docendi/ui";
import type {
  Cargo,
  Dedicacion,
  DatosEditablesPedido,
  Novedad,
  PedidoDesignacion,
  TipoAdjunto,
} from "../types";
import { validarPedido, type ErroresValidacion } from "../pedidoValidacion";

const NOVEDADES: Novedad[] = ["Sin novedad", "Alta", "Baja", "Cambio de cargo o dedicación"];
const CARGOS: Cargo[] = ["Titular", "Adjunto", "JTP", "Ayudante"];
const DEDICACIONES: Dedicacion[] = [
  "Categoría 1",
  "Categoría 2",
  "Categoría 3",
  "Categoría 4",
  "Categoría 5",
  "Categoría 6",
];

interface PedidoFormProps {
  pedidoInicial?: PedidoDesignacion;
  pedidosExistentes: PedidoDesignacion[];
  guardando?: boolean;
  onGuardar: (datos: DatosEditablesPedido) => void;
  onCancelar: () => void;
}

function datosIniciales(pedido?: PedidoDesignacion): DatosEditablesPedido {
  return {
    docente: pedido?.docente ?? { dni: "", nombre: "", antiguedad: 0 },
    materiaAsociada: pedido?.materiaAsociada ?? "",
    cargoActual: pedido?.cargoActual ?? null,
    dedicacionActual: pedido?.dedicacionActual ?? null,
    novedad: pedido?.novedad ?? "Sin novedad",
    cargoSolicitado: pedido?.cargoSolicitado,
    dedicacionSolicitada: pedido?.dedicacionSolicitada,
    justificacion: pedido?.justificacion,
    haceHorasOtroDepto: pedido?.haceHorasOtroDepto ?? false,
    adjuntos: pedido?.adjuntos ?? [],
  };
}

const ADJUNTOS_POR_NOVEDAD: Partial<Record<Novedad, { tipo: TipoAdjunto; etiqueta: string }[]>> = {
  Alta: [
    { tipo: "cv", etiqueta: "CV" },
    { tipo: "dni_frente", etiqueta: "Foto DNI (frente)" },
    { tipo: "dni_dorso", etiqueta: "Foto DNI (dorso)" },
  ],
  Baja: [{ tipo: "justificativo", etiqueta: "Justificativo" }],
};

export function PedidoForm({
  pedidoInicial,
  pedidosExistentes,
  guardando = false,
  onGuardar,
  onCancelar,
}: PedidoFormProps) {
  const [datos, setDatos] = useState<DatosEditablesPedido>(() => datosIniciales(pedidoInicial));
  const [errores, setErrores] = useState<ErroresValidacion>({});

  function actualizar<K extends keyof DatosEditablesPedido>(
    campo: K,
    valor: DatosEditablesPedido[K],
  ) {
    setDatos((prev) => ({ ...prev, [campo]: valor }));
  }

  function agregarAdjunto(tipo: TipoAdjunto, archivos: FileList) {
    const archivo = archivos.item(0);
    if (!archivo) return;
    setDatos((prev) => ({
      ...prev,
      adjuntos: [
        ...prev.adjuntos.filter((adjunto) => adjunto.tipo !== tipo),
        { id: crypto.randomUUID(), nombre: archivo.name, tipo },
      ],
    }));
  }

  function quitarAdjunto(tipo: TipoAdjunto) {
    setDatos((prev) => ({
      ...prev,
      adjuntos: prev.adjuntos.filter((adjunto) => adjunto.tipo !== tipo),
    }));
  }

  function adjuntoComoUploaded(tipo: TipoAdjunto): UploadedFile[] {
    const adjunto = datos.adjuntos.find((item) => item.tipo === tipo);
    return adjunto ? [{ id: adjunto.id, name: adjunto.nombre, status: "uploaded" }] : [];
  }

  function handleGuardar() {
    const resultado = validarPedido(datos, {
      pedidosExistentes,
      pedidoActualId: pedidoInicial?.id,
    });
    setErrores(resultado);
    if (Object.keys(resultado).length === 0) {
      onGuardar(datos);
    }
  }

  const muestraSolicitud =
    datos.novedad === "Alta" || datos.novedad === "Cambio de cargo o dedicación";
  const adjuntosRequeridos = ADJUNTOS_POR_NOVEDAD[datos.novedad];

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        handleGuardar();
      }}
      style={{ display: "flex", flexDirection: "column", gap: "var(--space-5)", maxWidth: 720 }}
    >
      {/* Datos del docente */}
      <fieldset
        style={{ border: "none", margin: 0, padding: 0, display: "grid", gap: "var(--space-4)" }}
      >
        <legend style={{ fontWeight: "var(--weight-semibold)", marginBottom: "var(--space-2)" }}>
          Datos del docente
        </legend>
        <div style={{ display: "grid", gridTemplateColumns: "1fr 2fr", gap: "var(--space-4)" }}>
          <Field label="DNI" required error={errores.docente}>
            <Input
              value={datos.docente.dni}
              onChange={(e) => actualizar("docente", { ...datos.docente, dni: e.target.value })}
              placeholder="Ej. 30111222"
            />
          </Field>
          <Field label="Nombre y apellido" required>
            <Input
              value={datos.docente.nombre}
              onChange={(e) => actualizar("docente", { ...datos.docente, nombre: e.target.value })}
              placeholder="Ej. Ana Pérez"
            />
          </Field>
        </div>
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: "var(--space-4)" }}>
          <Field label="Antigüedad (años)">
            <Input
              type="number"
              min={0}
              value={String(datos.docente.antiguedad)}
              onChange={(e) =>
                actualizar("docente", { ...datos.docente, antiguedad: Number(e.target.value) })
              }
            />
          </Field>
          <Field label="Cargo actual" hint="Dato actual (solo lectura)">
            <Input value={datos.cargoActual ?? "Sin designación actual"} readOnly />
          </Field>
          <Field label="Dedicación actual" hint="Dato actual (solo lectura)">
            <Input value={datos.dedicacionActual ?? "—"} readOnly />
          </Field>
        </div>
        <Field label="Materia asociada" required error={errores.materiaAsociada}>
          <Input
            value={datos.materiaAsociada}
            onChange={(e) => actualizar("materiaAsociada", e.target.value)}
            placeholder="Ej. Ingeniería de Software"
          />
        </Field>
      </fieldset>

      {/* Novedad */}
      <fieldset style={{ border: "none", margin: 0, padding: 0 }}>
        <legend style={{ fontWeight: "var(--weight-semibold)", marginBottom: "var(--space-2)" }}>
          Novedad
        </legend>
        <div
          role="radiogroup"
          aria-label="Novedad"
          style={{ display: "grid", gap: "var(--space-2)" }}
        >
          {NOVEDADES.map((opcion) => (
            <Radio
              key={opcion}
              name="novedad"
              label={opcion}
              value={opcion}
              checked={datos.novedad === opcion}
              onChange={() => actualizar("novedad", opcion)}
            />
          ))}
        </div>
        <label style={{ display: "flex", gap: "var(--space-2)", marginTop: "var(--space-3)" }}>
          <input
            type="checkbox"
            checked={datos.haceHorasOtroDepto}
            onChange={(e) => actualizar("haceHorasOtroDepto", e.target.checked)}
          />
          Hace más horas en otro Departamento
        </label>
      </fieldset>

      {/* Solicitud (Alta / Cambio) */}
      {muestraSolicitud && (
        <fieldset
          style={{ border: "none", margin: 0, padding: 0, display: "grid", gap: "var(--space-4)" }}
        >
          <legend style={{ fontWeight: "var(--weight-semibold)", marginBottom: "var(--space-2)" }}>
            Solicitud
          </legend>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "var(--space-4)" }}>
            <Field label="Cargo solicitado" required error={errores.cargoSolicitado}>
              <Select
                value={datos.cargoSolicitado ?? ""}
                onChange={(e) =>
                  actualizar("cargoSolicitado", (e.target.value || undefined) as Cargo)
                }
              >
                <option value="">Seleccionar…</option>
                {CARGOS.map((cargo) => (
                  <option key={cargo} value={cargo}>
                    {cargo}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Dedicación solicitada" required error={errores.dedicacionSolicitada}>
              <Select
                value={datos.dedicacionSolicitada ?? ""}
                onChange={(e) =>
                  actualizar("dedicacionSolicitada", (e.target.value || undefined) as Dedicacion)
                }
              >
                <option value="">Seleccionar…</option>
                {DEDICACIONES.map((dedicacion) => (
                  <option key={dedicacion} value={dedicacion}>
                    {dedicacion}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
        </fieldset>
      )}

      {/* Justificación (Cambio) */}
      {datos.novedad === "Cambio de cargo o dedicación" && (
        <Field label="Justificación" required error={errores.justificacion}>
          <Textarea
            rows={3}
            value={datos.justificacion ?? ""}
            onChange={(e) => actualizar("justificacion", e.target.value)}
            placeholder="Motivo del cambio de cargo o dedicación"
          />
        </Field>
      )}

      {/* Adjuntos (Alta / Baja) */}
      {adjuntosRequeridos && (
        <fieldset
          style={{ border: "none", margin: 0, padding: 0, display: "grid", gap: "var(--space-3)" }}
        >
          <legend style={{ fontWeight: "var(--weight-semibold)", marginBottom: "var(--space-2)" }}>
            Documentación obligatoria
          </legend>
          {errores.adjuntos && (
            <InlineAlert severity="warning" title="Faltan adjuntos">
              {errores.adjuntos}
            </InlineAlert>
          )}
          {adjuntosRequeridos.map(({ tipo, etiqueta }) => (
            <FileUpload
              key={tipo}
              title={etiqueta}
              hint="PDF o imagen (mock: solo se registra el nombre)"
              files={adjuntoComoUploaded(tipo)}
              onFilesAdded={(archivos) => agregarAdjunto(tipo, archivos)}
              onRemove={() => quitarAdjunto(tipo)}
            />
          ))}
        </fieldset>
      )}

      <div style={{ display: "flex", gap: "var(--space-2)" }}>
        <Button type="button" variant="secondary" onClick={onCancelar}>
          Cancelar
        </Button>
        <Button type="submit" variant="primary" loading={guardando}>
          Guardar borrador
        </Button>
      </div>
    </form>
  );
}

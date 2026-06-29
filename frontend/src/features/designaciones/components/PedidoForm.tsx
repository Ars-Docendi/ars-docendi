import { useState } from "react";
import { Button, Field, InlineAlert, Radio, Textarea, Toggle } from "@ars-docendi/ui";
import type { UploadedFile } from "@ars-docendi/ui";
import type {
  DatosEditablesPedido,
  DocenteExistente,
  DocentePedido,
  EstadoPedido,
  Novedad,
  PedidoDesignacion,
  TipoAdjunto,
} from "../types";
import { DOCENTES_EXISTENTES } from "../api/catalogos";
import { validarPedido, type ErroresValidacion } from "../pedidoValidacion";
import { SeccionDocentePedido } from "./SeccionDocentePedido";
import { SeccionDesignacionSolicitada } from "./SeccionDesignacionSolicitada";
import { SeccionAdjuntosPedido } from "./SeccionAdjuntosPedido";
import "./pedidoForm.css";

const NOVEDADES: Novedad[] = ["Sin novedad", "Alta", "Baja", "Cambio de cargo o dedicación"];

/** Etiqueta legible de la etapa a la que retorna un pedido devuelto. */
const ETIQUETA_ETAPA: Partial<Record<EstadoPedido, string>> = {
  en_revision_coordinador: "En revisión Coordinador",
  en_revision_secretaria: "En revisión Secretaría",
  en_revision_decanato: "En revisión Decanato",
};

interface PedidoFormProps {
  pedidoInicial?: PedidoDesignacion;
  pedidosExistentes: PedidoDesignacion[];
  esEdicion?: boolean;
  /** Etiqueta del período abierto para el subtítulo ("2026 · 1C"). */
  periodoLabel?: string;
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

export function PedidoForm({
  pedidoInicial,
  pedidosExistentes,
  esEdicion = false,
  periodoLabel = "2026 · 1C",
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

  // Catálogo de docentes para el selector. Si se edita un pedido cuyo docente
  // no está en el catálogo, se antepone para que quede seleccionable.
  const opcionesDocente: DocenteExistente[] = [...DOCENTES_EXISTENTES];
  const dniInicial = (pedidoInicial?.docente.dni ?? "").replace(/\D/g, "");
  if (
    pedidoInicial &&
    dniInicial &&
    pedidoInicial.cargoActual &&
    pedidoInicial.dedicacionActual &&
    !opcionesDocente.some((docente) => docente.dni === dniInicial)
  ) {
    opcionesDocente.unshift({
      dni: dniInicial,
      nombre: pedidoInicial.docente.nombre,
      antiguedad: pedidoInicial.docente.antiguedad,
      cargoActual: pedidoInicial.cargoActual,
      dedicacionActual: pedidoInicial.dedicacionActual,
      materiaActual: pedidoInicial.materiaAsociada,
    });
  }

  function seleccionarDocente(dni: string) {
    const docente = opcionesDocente.find((item) => item.dni === dni.replace(/\D/g, ""));
    if (!docente) {
      setDatos((prev) => ({
        ...prev,
        docente: { dni: "", nombre: "", antiguedad: 0 },
        cargoActual: null,
        dedicacionActual: null,
        materiaAsociada: "",
      }));
      return;
    }
    setDatos((prev) => ({
      ...prev,
      docente: { dni: docente.dni, nombre: docente.nombre, antiguedad: docente.antiguedad },
      cargoActual: docente.cargoActual,
      dedicacionActual: docente.dedicacionActual,
      materiaAsociada: docente.materiaActual,
    }));
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

  const { novedad } = datos;
  const esAlta = novedad === "Alta";
  const esBaja = novedad === "Baja";
  const esCambio = novedad === "Cambio de cargo o dedicación";
  const esSinNovedad = novedad === "Sin novedad";
  const muestraSolicitud = esAlta || esCambio;
  const muestraToggle = esAlta || esCambio;

  const numero = pedidoInicial?.numero ?? "";
  const titulo = esEdicion ? "Editar pedido de designación" : "Nuevo pedido de designación";
  const subtitulo = construirSubtitulo(novedad, esEdicion, pedidoInicial, numero, periodoLabel);
  const devolucion = pedidoInicial?.estado === "devuelto" ? ultimaDevolucion(pedidoInicial) : null;

  return (
    <form
      className="adoc-pf"
      onSubmit={(e) => {
        e.preventDefault();
        handleGuardar();
      }}
    >
      <header className="adoc-pf-head">
        <p className="adoc-pf-eyebrow">DESIGNACIONES · {novedad.toUpperCase()}</p>
        <h1 className="adoc-pf-title">{titulo}</h1>
        <p className="adoc-pf-subtitle">{subtitulo}</p>
      </header>

      {devolucion && (
        <InlineAlert severity="warning" title={`Devuelto por el ${devolucion.porRol}`}>
          {devolucion.comentario ? `«${devolucion.comentario}» ` : ""}
          Al reenviar, el pedido retoma la etapa{" "}
          {ETIQUETA_ETAPA[pedidoInicial?.etapaRetorno ?? "en_revision_coordinador"] ??
            "de revisión"}
          .
        </InlineAlert>
      )}

      <div className="adoc-pf-card">
        <section className="adoc-pf-sec">
          <h2 className="adoc-pf-sec-h">Tipo de novedad</h2>
          <div className="adoc-pf-radios" role="radiogroup" aria-label="Tipo de novedad">
            {NOVEDADES.map((opcion) => (
              <Radio
                key={opcion}
                name="novedad"
                label={opcion}
                value={opcion}
                checked={novedad === opcion}
                onChange={() => actualizar("novedad", opcion)}
              />
            ))}
          </div>
        </section>

        <SeccionDocentePedido
          novedad={novedad}
          docente={datos.docente}
          errorDocente={errores.docente}
          opcionesDocente={opcionesDocente}
          cargoActual={datos.cargoActual}
          dedicacionActual={datos.dedicacionActual}
          materia={datos.materiaAsociada}
          dedicacionSolicitada={esCambio ? datos.dedicacionSolicitada : undefined}
          onCambiarDocente={(docente: DocentePedido) => actualizar("docente", docente)}
          onSeleccionarDocente={seleccionarDocente}
        />

        {muestraSolicitud && (
          <SeccionDesignacionSolicitada
            esAlta={esAlta}
            materia={datos.materiaAsociada}
            cargoSolicitado={datos.cargoSolicitado}
            dedicacionSolicitada={datos.dedicacionSolicitada}
            errores={errores}
            onMateria={(valor) => actualizar("materiaAsociada", valor)}
            onCargo={(valor) => actualizar("cargoSolicitado", valor)}
            onDedicacion={(valor) => actualizar("dedicacionSolicitada", valor)}
          />
        )}

        {!esSinNovedad && (
          <section className="adoc-pf-sec">
            <h2 className="adoc-pf-sec-h">Justificación</h2>
            <Field
              label={esBaja ? "Motivo de la baja" : "Motivo del pedido"}
              error={errores.justificacion}
            >
              <Textarea
                rows={3}
                value={datos.justificacion ?? ""}
                onChange={(e) => actualizar("justificacion", e.target.value)}
                placeholder={
                  esBaja
                    ? "Motivo de la baja del docente"
                    : esCambio
                      ? "Motivo del cambio de cargo o dedicación"
                      : "Motivo del pedido de designación"
                }
              />
            </Field>
            {muestraToggle && (
              <Toggle
                label="El docente hace más horas en otro Departamento (se gestiona como externo)"
                checked={datos.haceHorasOtroDepto}
                onChange={(e) => actualizar("haceHorasOtroDepto", e.target.checked)}
              />
            )}
          </section>
        )}

        {!esSinNovedad && (
          <SeccionAdjuntosPedido
            novedad={novedad}
            errorAdjuntos={errores.adjuntos}
            adjuntoComoUploaded={adjuntoComoUploaded}
            onAgregar={agregarAdjunto}
            onQuitar={quitarAdjunto}
          />
        )}

        <div className="adoc-pf-actions">
          <Button type="button" variant="secondary" onClick={onCancelar}>
            Cancelar
          </Button>
          <Button type="submit" variant="primary" loading={guardando}>
            Guardar pedido
          </Button>
        </div>
      </div>
    </form>
  );
}

/** Subtítulo del encabezado, según novedad / si es edición. */
function construirSubtitulo(
  novedad: Novedad,
  esEdicion: boolean,
  pedidoInicial: PedidoDesignacion | undefined,
  numero: string,
  periodoLabel: string,
): string {
  if (esEdicion) {
    const ref = numero || "borrador";
    return pedidoInicial?.estado === "devuelto"
      ? `Editás un pedido devuelto · ${ref}`
      : `Editás un borrador · ${ref}`;
  }
  switch (novedad) {
    case "Alta":
      return `Cargá la novedad de un docente · período ${periodoLabel}`;
    case "Baja":
      return `Registrá la baja de un docente · período ${periodoLabel}`;
    case "Cambio de cargo o dedicación":
      return `Cargá un cambio de cargo o dedicación · período ${periodoLabel}`;
    default:
      return `Reconfirmá la designación de un docente · período ${periodoLabel}`;
  }
}

/** Último evento de devolución del historial (para el banner de pedido devuelto). */
function ultimaDevolucion(pedido: PedidoDesignacion) {
  return [...pedido.historial].reverse().find((evento) => evento.accion === "devolver") ?? null;
}

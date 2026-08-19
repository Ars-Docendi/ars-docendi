import { useState } from "react";
import { Button, Field, InlineAlert, Radio, Select, Textarea } from "@ars-docendi/ui";
import type { UploadedFile } from "@ars-docendi/ui";
import type {
  DatosEditablesPedido,
  DocenteExistente,
  DocentePedido,
  EstadoPedido,
  Novedad,
  PedidoDesignacion,
  TipoAdjunto,
  TipoBaja,
} from "../types";
import { DOCENTES_EXISTENTES, TIPOS_BAJA } from "../api/catalogos";
import { validarPedido, type ErroresValidacion } from "../pedidoValidacion";
import { SeccionDocentePedido } from "./SeccionDocentePedido";
import { SeccionDesignacionSolicitada } from "./SeccionDesignacionSolicitada";
import { SeccionAdjuntosPedido } from "./SeccionAdjuntosPedido";
import "./pedidoForm.css";

const NOVEDADES: Novedad[] = ["Alta", "Baja", "Cambio de cargo o dedicación"];

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
  /** `opciones.enviar` pide guardar y, en el mismo paso, enviar/reenviar a revisión. */
  onGuardar: (datos: DatosEditablesPedido, opciones?: { enviar?: boolean }) => void;
  onCancelar: () => void;
}

function datosIniciales(pedido?: PedidoDesignacion): DatosEditablesPedido {
  return {
    docente: pedido?.docente ?? { dni: "", nombre: "", antiguedad: 0 },
    asignaciones: pedido?.asignaciones ?? [{ materia: "", horas: 0 }],
    cargoActual: pedido?.cargoActual ?? null,
    dedicacionActual: pedido?.dedicacionActual ?? null,
    novedad: pedido?.novedad ?? "Alta",
    cargoSolicitado: pedido?.cargoSolicitado,
    dedicacionSolicitada: pedido?.dedicacionSolicitada,
    justificacion: pedido?.justificacion,
    tipoBaja: pedido?.tipoBaja,
    tipoBajaDetalle: pedido?.tipoBajaDetalle,
    horasExternas: pedido?.horasExternas ?? 0,
    horasInvestigacion: pedido?.horasInvestigacion ?? 0,
    esAgenteExterno: pedido?.esAgenteExterno ?? false,
    departamentoAgenteExterno: pedido?.departamentoAgenteExterno,
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
      legajo: pedidoInicial.docente.legajo ?? "",
      antiguedad: pedidoInicial.docente.antiguedad,
      cargoActual: pedidoInicial.cargoActual,
      dedicacionActual: pedidoInicial.dedicacionActual,
      materiasActuales: pedidoInicial.asignaciones,
      horasInvestigacionActuales: pedidoInicial.horasInvestigacion,
      horasExternasActuales: pedidoInicial.horasExternas,
    });
  }

  // Docente seleccionado (catálogo): fuente de verdad de los valores "actuales"
  // para el resumen de cambios de Cambio (D-8) — no lo que el usuario edita.
  const docenteSeleccionado = opcionesDocente.find(
    (item) => item.dni === datos.docente.dni.replace(/\D/g, ""),
  );

  function seleccionarDocente(dni: string) {
    const docente = opcionesDocente.find((item) => item.dni === dni.replace(/\D/g, ""));
    if (!docente) {
      setDatos((prev) => ({
        ...prev,
        docente: { dni: "", nombre: "", antiguedad: 0 },
        cargoActual: null,
        dedicacionActual: null,
        asignaciones: [{ materia: "", horas: 0 }],
        horasInvestigacion: 0,
        horasExternas: 0,
        esAgenteExterno: false,
        departamentoAgenteExterno: undefined,
      }));
      return;
    }
    setDatos((prev) => ({
      ...prev,
      docente: {
        dni: docente.dni,
        nombre: docente.nombre,
        antiguedad: docente.antiguedad,
        legajo: docente.legajo,
      },
      cargoActual: docente.cargoActual,
      dedicacionActual: docente.dedicacionActual,
      asignaciones: docente.materiasActuales.map((asignacion) => ({ ...asignacion })),
      horasInvestigacion: docente.horasInvestigacionActuales,
      horasExternas: docente.horasExternasActuales,
      // Sin "valor actual" de agente externo (D-2): arranca sin marcar al elegir un docente existente.
      esAgenteExterno: false,
      departamentoAgenteExterno: undefined,
    }));
  }

  function agregarMateria() {
    setDatos((prev) => ({
      ...prev,
      asignaciones: [...prev.asignaciones, { materia: "", horas: 0 }],
    }));
  }

  function quitarMateria(indice: number) {
    // Ni Alta ni Cambio exigen un mínimo de materias (regla de negocio) — Baja nunca
    // llega acá (su listado es de solo lectura, sin acción de quitar).
    setDatos((prev) => ({
      ...prev,
      asignaciones: prev.asignaciones.filter((_, i) => i !== indice),
    }));
  }

  function cambiarMateria(indice: number, materia: string) {
    setDatos((prev) => ({
      ...prev,
      asignaciones: prev.asignaciones.map((asignacion, i) =>
        i === indice ? { ...asignacion, materia } : asignacion,
      ),
    }));
  }

  function cambiarHoras(indice: number, horas: number) {
    setDatos((prev) => ({
      ...prev,
      asignaciones: prev.asignaciones.map((asignacion, i) =>
        i === indice ? { ...asignacion, horas } : asignacion,
      ),
    }));
  }

  function cambiarEsAgenteExterno(valor: boolean) {
    setDatos((prev) => ({
      ...prev,
      esAgenteExterno: valor,
      // Al desmarcar, se limpia el departamento — no queda un valor huérfano sin el checkbox.
      departamentoAgenteExterno: valor ? prev.departamentoAgenteExterno : undefined,
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

  function handleGuardar(opciones?: { enviar?: boolean }) {
    // "Guardar pedido" siempre guarda el estado actual del form, aunque falten
    // campos obligatorios — la validación completa solo bloquea el envío a
    // revisión ("Guardar y enviar"/"Guardar y reenviar", opciones.enviar).
    if (!opciones?.enviar) {
      setErrores({});
      onGuardar(datos);
      return;
    }
    const resultado = validarPedido(datos, {
      pedidosExistentes,
      pedidoActualId: pedidoInicial?.id,
    });
    setErrores(resultado);
    if (Object.keys(resultado).length === 0) {
      onGuardar(datos, opciones);
    }
  }

  const { novedad } = datos;
  const esAlta = novedad === "Alta";
  const esBaja = novedad === "Baja";
  const esCambio = novedad === "Cambio de cargo o dedicación";
  const muestraSolicitud = esAlta || esCambio;

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
          cargoSolicitado={esCambio ? datos.cargoSolicitado : undefined}
          dedicacionActual={datos.dedicacionActual}
          dedicacionSolicitada={esCambio ? datos.dedicacionSolicitada : undefined}
          materiasActuales={docenteSeleccionado?.materiasActuales ?? []}
          materiasSolicitadas={esCambio ? datos.asignaciones : undefined}
          horasInvestigacionActuales={docenteSeleccionado?.horasInvestigacionActuales}
          horasInvestigacionSolicitadas={esCambio ? datos.horasInvestigacion : undefined}
          horasExternasActuales={docenteSeleccionado?.horasExternasActuales}
          horasExternasSolicitadas={esCambio ? datos.horasExternas : undefined}
          onCambiarDocente={(docente: DocentePedido) => actualizar("docente", docente)}
          onSeleccionarDocente={seleccionarDocente}
        />

        {muestraSolicitud && (
          <SeccionDesignacionSolicitada
            asignaciones={datos.asignaciones}
            cargoSolicitado={datos.cargoSolicitado}
            dedicacionSolicitada={datos.dedicacionSolicitada}
            dedicacionActual={datos.dedicacionActual}
            horasInvestigacion={datos.horasInvestigacion}
            horasExternas={datos.horasExternas}
            errores={errores}
            onAgregarMateria={agregarMateria}
            onQuitarMateria={quitarMateria}
            onCambiarMateria={cambiarMateria}
            onCambiarHoras={cambiarHoras}
            onCargo={(valor) => actualizar("cargoSolicitado", valor)}
            onDedicacion={(valor) => actualizar("dedicacionSolicitada", valor)}
            onHorasInvestigacion={(valor) => actualizar("horasInvestigacion", valor)}
            onHorasExternas={(valor) => actualizar("horasExternas", valor)}
            esAgenteExterno={datos.esAgenteExterno}
            onEsAgenteExterno={cambiarEsAgenteExterno}
            departamentoAgenteExterno={datos.departamentoAgenteExterno}
            onDepartamentoAgenteExterno={(valor) => actualizar("departamentoAgenteExterno", valor)}
          />
        )}

        <section className="adoc-pf-sec">
          <h2 className="adoc-pf-sec-h">Justificación</h2>
          {esBaja && (
            <>
              <Field label="Tipo de baja" error={errores.tipoBaja}>
                <Select
                  value={datos.tipoBaja ?? ""}
                  onChange={(e) =>
                    actualizar("tipoBaja", (e.target.value || undefined) as TipoBaja)
                  }
                >
                  <option value="">Seleccioná el tipo de baja…</option>
                  {TIPOS_BAJA.map((tipo) => (
                    <option key={tipo} value={tipo}>
                      {tipo}
                    </option>
                  ))}
                </Select>
              </Field>
              {datos.tipoBaja === "Otro" && (
                <Field label="Detalle" error={errores.tipoBajaDetalle}>
                  <Textarea
                    rows={2}
                    value={datos.tipoBajaDetalle ?? ""}
                    onChange={(e) => actualizar("tipoBajaDetalle", e.target.value)}
                    placeholder="Describí el motivo de la baja"
                  />
                </Field>
              )}
            </>
          )}
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
        </section>

        <SeccionAdjuntosPedido
          novedad={novedad}
          errorAdjuntos={errores.adjuntos}
          adjuntoComoUploaded={adjuntoComoUploaded}
          onAgregar={agregarAdjunto}
          onQuitar={quitarAdjunto}
        />

        <div className="adoc-pf-actions">
          <Button type="button" variant="secondary" onClick={onCancelar}>
            Cancelar
          </Button>
          <Button type="submit" variant="secondary" loading={guardando}>
            Guardar pedido
          </Button>
          <Button
            type="button"
            variant="primary"
            loading={guardando}
            onClick={() => handleGuardar({ enviar: true })}
          >
            {pedidoInicial?.estado === "devuelto" ? "Guardar y reenviar" : "Guardar y enviar"}
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

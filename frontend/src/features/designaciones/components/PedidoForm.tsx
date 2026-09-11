import { useMemo, useState } from "react";
import { Button, Field, InlineAlert, Radio, Select, Textarea } from "@ars-docendi/ui";
import type { UploadedFile } from "@ars-docendi/ui";
import type {
  DatosEditablesPedido,
  DocenteExistente,
  EstadoPedido,
  Novedad,
  NovedadAdmitida,
  PedidoDesignacion,
  MateriaPedido,
  TipoAdjunto,
  TipoBaja,
} from "../types";
import { asignacionVigenteEnMateria } from "../api/catalogos";
import { validarPedido, type ErroresValidacion } from "../pedidoValidacion";
import { SeccionDocentePedido } from "./SeccionDocentePedido";
import { SeccionDesignacionSolicitada } from "./SeccionDesignacionSolicitada";
import { SeccionAdjuntosPedido } from "./SeccionAdjuntosPedido";
import "./pedidoForm.css";

const NOVEDADES: NovedadAdmitida[] = ["Alta", "Baja", "Cambio de cargo o dedicación"];

/** Etiqueta legible de la etapa a la que retorna un pedido devuelto. */
const ETIQUETA_ETAPA: Partial<Record<EstadoPedido, string>> = {
  en_revision_coordinador: "En revisión Coordinador",
  en_revision_secretaria: "En revisión Secretaría",
  en_revision_decanato: "En revisión Decanato",
};

interface PedidoFormProps {
  pedidoInicial?: PedidoDesignacion;
  pedidosExistentes: PedidoDesignacion[];
  /** Nombre presentacional de respaldo; la selección autoritativa es `materiaId`. */
  catedra?: string;
  esEdicion?: boolean;
  /** Etiqueta del período abierto para el subtítulo ("2026 · 1C"). */
  periodoLabel?: string;
  guardando?: boolean;
  /** `opciones.enviar` pide guardar y, en el mismo paso, enviar/reenviar a revisión. */
  onGuardar: (datos: DatosEditablesPedido, opciones?: { enviar?: boolean }) => void;
  onCancelar: () => void;
  docentes: DocenteExistente[];
  materias?: MateriaPedido[];
  cargos: string[];
  dedicaciones: string[];
  tiposBaja: string[];
}

function datosIniciales(catedra: string, pedido?: PedidoDesignacion): DatosEditablesPedido {
  return {
    docente: pedido?.docente ?? { dni: "", nombre: "", antiguedad: 0 },
    catedra: pedido?.catedra ?? catedra,
    horas: pedido?.horas ?? 0,
    cargoActual: pedido?.cargoActual ?? null,
    dedicacionActual: pedido?.dedicacionActual ?? null,
    novedad: pedido?.novedad ?? "",
    cargoSolicitado: pedido?.cargoSolicitado,
    dedicacionSolicitada: pedido?.dedicacionSolicitada,
    justificacion: pedido?.justificacion,
    tipoBaja: pedido?.tipoBaja,
    tipoBajaDetalle: pedido?.tipoBajaDetalle,
    horasExternas: pedido?.horasExternas ?? 0,
    horasInvestigacion: pedido?.horasInvestigacion ?? 0,
    adjuntos: pedido?.adjuntos ?? [],
    personaId: pedido?.personaId,
    materiaId: pedido?.materiaId,
    periodoId: pedido?.periodoId,
    version: pedido?.version,
  };
}

export function PedidoForm({
  pedidoInicial,
  pedidosExistentes,
  catedra = "",
  esEdicion = false,
  periodoLabel = "2026 · 1C",
  guardando = false,
  onGuardar,
  onCancelar,
  docentes,
  materias = [],
  cargos,
  dedicaciones,
  tiposBaja,
}: PedidoFormProps) {
  const [datos, setDatos] = useState<DatosEditablesPedido>(() =>
    datosIniciales(catedra, pedidoInicial),
  );
  const [errores, setErrores] = useState<ErroresValidacion>({});

  function actualizar<K extends keyof DatosEditablesPedido>(
    campo: K,
    valor: DatosEditablesPedido[K],
  ) {
    setDatos((prev) => ({ ...prev, [campo]: valor }));
  }

  // Catálogo de docentes para el selector. Si se edita un pedido cuyo docente
  // no está en el catálogo, se antepone para que quede seleccionable.
  const opcionesDocente: DocenteExistente[] = [...docentes];
  const dniInicial = (pedidoInicial?.docente.dni ?? "").replace(/\D/g, "");
  if (
    pedidoInicial &&
    dniInicial &&
    pedidoInicial.cargoActual &&
    pedidoInicial.dedicacionActual &&
    !opcionesDocente.some((docente) => docente.dni === dniInicial)
  ) {
    opcionesDocente.unshift({
      personaId: pedidoInicial.personaId ?? "persona-legada",
      dni: dniInicial,
      nombre: pedidoInicial.docente.nombre,
      legajo: pedidoInicial.docente.legajo ?? "",
      antiguedad: pedidoInicial.docente.antiguedad,
      cargoActual: pedidoInicial.cargoActual,
      dedicacionActual: pedidoInicial.dedicacionActual,
      materiasActuales: [
        {
          materiaId: pedidoInicial.materiaId ?? "materia-legada",
          materia: pedidoInicial.catedra,
          horas: pedidoInicial.horasActuales ?? pedidoInicial.horas,
          cargoActual: pedidoInicial.cargoActual,
          dedicacionActual: pedidoInicial.dedicacionActual,
          horasInvestigacion: pedidoInicial.horasInvestigacionActuales ?? null,
          horasExternas: pedidoInicial.horasExternasActuales ?? null,
        },
      ],
      horasInvestigacionActuales: pedidoInicial.horasInvestigacionActuales ?? null,
      horasExternasActuales: pedidoInicial.horasExternasActuales ?? null,
    });
  }

  // Docente seleccionado (catálogo): fuente de verdad de los valores "actuales"
  // para el resumen de cambios de Cambio (D-8) — no lo que el usuario edita.
  const docenteSeleccionado = opcionesDocente.find(
    (item) => item.dni === datos.docente.dni.replace(/\D/g, ""),
  );
  const materiasBase = useMemo(
    () =>
      materias.length > 0
        ? materias
        : catedra
          ? [{ id: pedidoInicial?.materiaId ?? "materia-legada", nombre: catedra }]
          : [],
    [catedra, materias, pedidoInicial?.materiaId],
  );
  const esAlta = datos.novedad === "Alta";
  const materiasDisponibles = useMemo(() => {
    if (esAlta) return materiasBase;
    const materiasDelActor = new Set(materiasBase.map((materia) => materia.id));
    const ids = new Set<string>();
    return (docenteSeleccionado?.materiasActuales ?? []).flatMap((asignacion) => {
      if (!materiasDelActor.has(asignacion.materiaId)) return [];
      if (ids.has(asignacion.materiaId)) return [];
      ids.add(asignacion.materiaId);
      return [{ id: asignacion.materiaId, nombre: asignacion.materia }];
    });
  }, [docenteSeleccionado, esAlta, materiasBase]);
  const materiaIdSeleccionada = materiasDisponibles.some(
    (materia) => materia.id === datos.materiaId,
  )
    ? datos.materiaId
    : materiasDisponibles.length === 1
      ? materiasDisponibles[0].id
      : undefined;
  const asignacionSeleccionada = asignacionVigenteEnMateria(
    docenteSeleccionado,
    materiaIdSeleccionada,
  );
  const materiaSeleccionada = materiasDisponibles.find(
    (materia) => materia.id === materiaIdSeleccionada,
  );
  const errorMateriaSinOpciones =
    !esAlta && docenteSeleccionado && materiasDisponibles.length === 0
      ? "El docente seleccionado no tiene una designación vigente en una materia a cargo del actor."
      : undefined;
  const usaSnapshot =
    pedidoInicial?.snapshot != null && materiaIdSeleccionada === pedidoInicial.materiaId;
  const horasActuales = usaSnapshot ? pedidoInicial?.horasActuales : asignacionSeleccionada?.horas;
  const horasInvestigacionActuales = usaSnapshot
    ? pedidoInicial?.horasInvestigacionActuales
    : asignacionSeleccionada?.horasInvestigacion;
  const horasExternasActuales = usaSnapshot
    ? pedidoInicial?.horasExternasActuales
    : asignacionSeleccionada?.horasExternas;

  function seleccionarDocente(dni: string) {
    const docente = opcionesDocente.find((item) => item.dni === dni.replace(/\D/g, ""));
    if (!docente) {
      setDatos((prev) => ({
        ...prev,
        docente: { dni: "", nombre: "", antiguedad: 0 },
        cargoActual: null,
        dedicacionActual: null,
        horas: 0,
        horasInvestigacion: 0,
        horasExternas: 0,
        personaId: undefined,
        materiaId: undefined,
      }));
      return;
    }
    const materiasDelActor = new Set(materiasBase.map((materia) => materia.id));
    const materiasCompatibles = docente.materiasActuales.filter((asignacion) =>
      materiasDelActor.has(asignacion.materiaId),
    );
    const asignacion = materiasCompatibles.length === 1 ? materiasCompatibles[0] : undefined;
    setDatos((prev) => ({
      ...prev,
      docente: {
        dni: docente.dni,
        nombre: docente.nombre,
        antiguedad: docente.antiguedad,
        legajo: docente.legajo,
      },
      cargoActual: asignacion?.cargoActual ?? docente.cargoActual,
      dedicacionActual: asignacion?.dedicacionActual ?? docente.dedicacionActual,
      horas: asignacion?.horas ?? 0,
      horasInvestigacion: asignacion?.horasInvestigacion ?? 0,
      horasExternas: asignacion?.horasExternas ?? 0,
      personaId: docente.personaId,
      materiaId: asignacion?.materiaId,
      catedra: asignacion?.materia ?? "",
    }));
  }

  function cambiarPersona(campo: "dni" | "nombrePersona" | "apellido", valor: string) {
    setDatos((prev) => ({
      ...prev,
      personaId: undefined,
      docente: {
        ...prev.docente,
        [campo]: valor,
        ...(campo === "nombrePersona" ? { nombre: valor } : {}),
      },
    }));
  }

  function seleccionarMateria(materiaId: string) {
    const materia = materiasDisponibles.find((item) => item.id === materiaId);
    const asignacion = asignacionVigenteEnMateria(docenteSeleccionado, materiaId);
    setDatos((prev) => ({
      ...prev,
      materiaId: materia?.id,
      catedra: materia?.nombre ?? "",
      cargoActual: asignacion?.cargoActual ?? (esAlta ? null : prev.cargoActual),
      dedicacionActual: asignacion?.dedicacionActual ?? (esAlta ? null : prev.dedicacionActual),
      horas: asignacion?.horas ?? (esAlta ? prev.horas : 0),
      horasInvestigacion: asignacion?.horasInvestigacion ?? (esAlta ? prev.horasInvestigacion : 0),
      horasExternas: asignacion?.horasExternas ?? (esAlta ? prev.horasExternas : 0),
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
    const datosParaGuardar = {
      ...datos,
      materiaId: materiaIdSeleccionada,
      catedra: materiaSeleccionada?.nombre ?? "",
    };
    const resultado = validarPedido(datosParaGuardar, {
      pedidosExistentes,
      pedidoActualId: pedidoInicial?.id,
    });
    if (errorMateriaSinOpciones) resultado.materia = errorMateriaSinOpciones;
    setErrores(resultado);
    if (Object.keys(resultado).length === 0) {
      onGuardar(datosParaGuardar, opciones);
    }
  }

  const { novedad } = datos;
  const esBaja = novedad === "Baja";
  const esCambio = novedad === "Cambio de cargo o dedicación";
  const esSinNovedad = novedad === "Sin novedad";
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
        <p className="adoc-pf-eyebrow">
          DESIGNACIONES · {novedad ? novedad.toUpperCase() : "NOVEDAD"}
        </p>
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
          {!novedad && <p className="adoc-pf-note">Seleccioná una novedad para continuar.</p>}
          {errores.novedad && (
            <p className="adoc-pf-materias-error" role="alert">
              {errores.novedad}
            </p>
          )}
          {esSinNovedad && (
            <p className="adoc-pf-note">
              Este pedido legado conserva «Sin novedad». Elegí una novedad admitida para editarlo.
            </p>
          )}
        </section>

        {novedad && (
          <SeccionDocentePedido
            novedad={novedad}
            docente={datos.docente}
            errorDocente={errores.docente}
            opcionesDocente={opcionesDocente}
            materias={materiasDisponibles}
            materiaId={materiaIdSeleccionada}
            errorMateria={errores.materia ?? errorMateriaSinOpciones}
            cargoActual={datos.cargoActual}
            cargoSolicitado={esCambio ? datos.cargoSolicitado : undefined}
            dedicacionActual={datos.dedicacionActual}
            dedicacionSolicitada={esCambio ? datos.dedicacionSolicitada : undefined}
            materia={materiaSeleccionada?.nombre ?? catedra}
            horasActuales={horasActuales}
            horasSolicitadas={esCambio ? datos.horas : undefined}
            horasInvestigacionActuales={horasInvestigacionActuales}
            horasInvestigacionSolicitadas={esCambio ? datos.horasInvestigacion : undefined}
            horasExternasActuales={horasExternasActuales}
            horasExternasSolicitadas={esCambio ? datos.horasExternas : undefined}
            onSeleccionarDocente={seleccionarDocente}
            onCambiarPersona={cambiarPersona}
            onSeleccionarMateria={seleccionarMateria}
          />
        )}

        {muestraSolicitud && (
          <SeccionDesignacionSolicitada
            materia={materiaSeleccionada?.nombre ?? catedra}
            horas={datos.horas}
            cargoSolicitado={datos.cargoSolicitado}
            dedicacionSolicitada={datos.dedicacionSolicitada}
            horasInvestigacion={datos.horasInvestigacion}
            horasExternas={datos.horasExternas}
            errores={errores}
            onCambiarHoras={(valor) => actualizar("horas", valor)}
            onCargo={(valor) => actualizar("cargoSolicitado", valor)}
            onDedicacion={(valor) => actualizar("dedicacionSolicitada", valor)}
            onHorasInvestigacion={(valor) => actualizar("horasInvestigacion", valor)}
            onHorasExternas={(valor) => actualizar("horasExternas", valor)}
            cargos={cargos}
            dedicaciones={dedicaciones}
          />
        )}

        {novedad && !esSinNovedad && (
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
                    {tiposBaja.map((tipo) => (
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
        )}

        {novedad && !esSinNovedad && (
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
  novedad: Novedad | "",
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
    case "":
      return `Seleccioná una novedad · período ${periodoLabel}`;
    default:
      return `Reconfirmá la designación de un docente · período ${periodoLabel}`;
  }
}

/** Último evento de devolución del historial (para el banner de pedido devuelto). */
function ultimaDevolucion(pedido: PedidoDesignacion) {
  return [...pedido.historial].reverse().find((evento) => evento.accion === "devolver") ?? null;
}

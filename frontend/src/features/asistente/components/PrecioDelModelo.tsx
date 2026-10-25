import { useEffect, useRef, useState } from "react";
import { Button, Field, InlineAlert, Input } from "@ars-docendi/ui";

import type { PedidoDePrecio, PrecioDelModelo as Precio } from "../types";
import {
  codigoDeError,
  fechaLegible,
  hoyEnIso,
  montoLegible,
  nombreDeLaEmpresa,
  pedidoDePrecio,
  textoDelCodigo,
  validarPrecio,
  type ErroresDelPrecio,
  type FormularioDePrecio,
} from "../utils/modeloDelAsistente";

interface PrecioDelModeloProps {
  empresa: string;
  modelo: string;
  /** El nombre con que se muestra el modelo, «empresa/modelo». */
  nombre: string;
  /** El precio vigente, o `undefined` si el modelo está sin precio cargado. */
  vigente: Precio | undefined;
  /** Guarda una versión nueva; rechaza si el servidor la rechaza. */
  onGuardar: (pedido: PedidoDePrecio) => Promise<void>;
}

const FORMULARIO_VACIO: FormularioDePrecio = { entrada: "", salida: "", cache: "", desde: "" };

/**
 * El precio del modelo elegido (asistente-modelo-en-caliente, tareas 5.3 y 5.4):
 * «Sin precio cargado» con «Cargar precio», o el precio vigente con «Cargar
 * nueva versión». Los dos abren el mismo formulario, en línea.
 *
 * LOS PRECIOS NO SE EDITAN: guardar escribe una versión nueva, y la anterior
 * queda para costear el historial con el precio de su momento. El formulario se
 * descarta al cambiar de modelo (el padre lo monta con `key`).
 *
 * El texto de ayuda sólo SUGIERE dónde buscar los valores: la página de precios
 * del proveedor es la fuente; la lista pública de LiteLLM es un atajo no oficial.
 */
export function PrecioDelModelo({
  empresa,
  modelo,
  nombre,
  vigente,
  onGuardar,
}: PrecioDelModeloProps) {
  const [formulario, setFormulario] = useState<FormularioDePrecio | null>(null);
  const [errores, setErrores] = useState<ErroresDelPrecio>({});
  const [errorDelServidor, setErrorDelServidor] = useState<string | null>(null);
  const [guardando, setGuardando] = useState(false);
  const botonAbrirRef = useRef<HTMLButtonElement>(null);
  const devolverElFoco = useRef(false);

  // El botón que abrió el formulario se vuelve a montar al cerrarlo: el foco
  // vuelve a él una vez que ese render ya ocurrió.
  useEffect(() => {
    if (devolverElFoco.current && botonAbrirRef.current) {
      botonAbrirRef.current.focus();
      devolverElFoco.current = false;
    }
  });

  const empresaLegible = nombreDeLaEmpresa(empresa);

  function abrir() {
    setFormulario({ ...FORMULARIO_VACIO, desde: hoyEnIso() });
    setErrores({});
    setErrorDelServidor(null);
  }

  function cerrar() {
    setFormulario(null);
    setErrores({});
    setErrorDelServidor(null);
    devolverElFoco.current = true;
  }

  function cambiar(campo: keyof FormularioDePrecio, valor: string) {
    if (!formulario) return;
    setFormulario({ ...formulario, [campo]: valor });
    if (errores[campo]) setErrores({ ...errores, [campo]: undefined });
  }

  async function guardar() {
    if (!formulario) return;
    const encontrados = validarPrecio(formulario);
    if (Object.keys(encontrados).length > 0) {
      setErrores(encontrados);
      return;
    }
    setGuardando(true);
    setErrorDelServidor(null);
    try {
      await onGuardar(pedidoDePrecio({ empresa, modelo }, formulario));
      setFormulario(null);
      devolverElFoco.current = true;
    } catch (error) {
      setErrorDelServidor(textoDelCodigo(codigoDeError(error), "nube"));
    } finally {
      setGuardando(false);
    }
  }

  const botonAbrir = (
    <Button ref={botonAbrirRef} variant="secondary" size="sm" onClick={abrir}>
      {vigente ? "Cargar nueva versión" : "Cargar precio"}
    </Button>
  );

  return (
    <div className="adoc-asistente-modelo-precio">
      {!formulario &&
        (vigente ? (
          <div className="adoc-asistente-modelo-precio-vigente">
            <span className="adoc-hint">
              Precio vigente desde {fechaLegible(vigente.vigenteDesde)}: US${" "}
              {montoLegible(vigente.entradaPorMillon)} entrada · US${" "}
              {montoLegible(vigente.salidaPorMillon)} salida · US${" "}
              {montoLegible(vigente.cachePorMillon)} caché, por millón de tokens.
            </span>
            {botonAbrir}
          </div>
        ) : (
          <InlineAlert severity="warning" role="note" title="Sin precio cargado.">
            No hay precio para <span className="adoc-asistente-modelo-mono">{nombre}</span>: los
            turnos se registran «sin precio» y no suman al tope mensual.
            <div className="adoc-asistente-modelo-precio-accion">{botonAbrir}</div>
          </InlineAlert>
        ))}

      {formulario && (
        <div
          className="sistema-tarjeta adoc-asistente-modelo-subpanel"
          role="group"
          aria-labelledby="modelo-precio-titulo"
        >
          <span id="modelo-precio-titulo" className="sistema-tarjeta-nombre">
            Precio de <span className="adoc-asistente-modelo-mono">{nombre}</span>
          </span>
          <p className="adoc-hint">
            Tomalo de la página de precios de {empresaLegible}, en US$ por millón de tokens. La
            lista pública de LiteLLM (model_prices_and_context_window.json) sirve como atajo no
            oficial: sus valores son por token, así que multiplicalos por 1.000.000 y revisalos.
          </p>
          <div className="adoc-asistente-modelo-precio-campos">
            <Field label="Entrada (US$ por millón)" required error={errores.entrada}>
              <Input
                type="number"
                min="0"
                step="any"
                inputMode="decimal"
                value={formulario.entrada}
                onChange={(e) => cambiar("entrada", e.target.value)}
              />
            </Field>
            <Field label="Salida (US$ por millón)" required error={errores.salida}>
              <Input
                type="number"
                min="0"
                step="any"
                inputMode="decimal"
                value={formulario.salida}
                onChange={(e) => cambiar("salida", e.target.value)}
              />
            </Field>
            <Field
              label="Caché (US$ por millón)"
              error={errores.cache}
              hint="Opcional. Vacío, el caché se cobra como la entrada."
            >
              <Input
                type="number"
                min="0"
                step="any"
                inputMode="decimal"
                value={formulario.cache}
                onChange={(e) => cambiar("cache", e.target.value)}
              />
            </Field>
            <Field label="Vigente desde" required error={errores.desde}>
              <Input
                type="date"
                value={formulario.desde}
                min={vigente?.vigenteDesde}
                onChange={(e) => cambiar("desde", e.target.value)}
              />
            </Field>
          </div>
          {errorDelServidor && (
            <InlineAlert severity="danger" title="No se guardó el precio.">
              {errorDelServidor}
            </InlineAlert>
          )}
          <p className="adoc-hint">
            Se guarda como una versión nueva. La anterior queda para costear el historial con el
            precio de su momento.
          </p>
          <div className="adoc-asistente-admin-mantenimiento-acciones">
            <Button variant="ghost" size="sm" disabled={guardando} onClick={cerrar}>
              Cancelar
            </Button>
            <Button
              size="sm"
              loading={guardando}
              disabled={guardando}
              onClick={() => void guardar()}
            >
              Guardar precio
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

import { useEffect, useRef, useState } from "react";
import { Button, Field, Textarea } from "@ars-docendi/ui";

import { IconoWrench } from "../../../shared/ui/iconos";
import type { MantenimientoDelAsistente } from "../types";

interface BannerDeMantenimientoProps {
  /** El estado vigente, de `capacidades`. `undefined` mientras no se conoce. */
  mantenimiento: MantenimientoDelAsistente | undefined;
  onGuardar: (activo: boolean, razon?: string) => Promise<void>;
}

/**
 * El kill switch (asistente-modo-mantenimiento, tasks.md 11.6), rehecho 1:1
 * con el canvas de Claude Design: un banner compacto de una fila —punto de
 * estado, título y copete— en vez de la tarjeta grande con toggle + «Guardar
 * cambios» del primer rediseño. Activar pide la razón EN UN PANEL QUE
 * APARECE debajo del banner, con «Cancelar»/«Activar mantenimiento»;
 * desactivar es un solo click, sin razón —mismo contrato que ya exige el
 * backend (6.2): activar sin razón está deshabilitado hasta que haya texto.
 */
export function BannerDeMantenimiento({ mantenimiento, onGuardar }: BannerDeMantenimientoProps) {
  const [editando, setEditando] = useState(false);
  const [razon, setRazon] = useState("");
  const [enviando, setEnviando] = useState(false);
  // El botón «Activar mantenimiento» reaparece con el foco cuando el panel de
  // razón se cierra —guardado o cancelado— para no soltarlo al `<body>`
  // (tasks.md 13.2: los guardados se anuncian sin mover el foco).
  const botonActivarRef = useRef<HTMLButtonElement>(null);
  const volverAEnfocarRef = useRef(false);
  // Activar mantenimiento CAMBIA DE RAMA: el banner entero pasa de la vista
  // «disponible» a la vista «en mantenimiento», así que no hay ningún botón
  // en común al que volver (a diferencia de cancelar, que se queda en la
  // misma rama). El foco pasa al botón nuevo que corresponde a esa rama
  // —«Desactivar mantenimiento»— en vez de perderse en el `<body>`.
  const botonDesactivarRef = useRef<HTMLButtonElement>(null);
  const activadoAhoraRef = useRef(false);

  useEffect(() => {
    if (volverAEnfocarRef.current && !editando) {
      botonActivarRef.current?.focus();
      volverAEnfocarRef.current = false;
    }
  }, [editando]);

  const activo = mantenimiento?.activo ?? false;
  const faltaRazon = razon.trim().length === 0;

  useEffect(() => {
    if (activadoAhoraRef.current && activo) {
      botonDesactivarRef.current?.focus();
      activadoAhoraRef.current = false;
    }
  }, [activo]);

  async function activar() {
    if (faltaRazon) return;
    setEnviando(true);
    try {
      activadoAhoraRef.current = true;
      await onGuardar(true, razon.trim());
      setEditando(false);
      setRazon("");
    } finally {
      setEnviando(false);
    }
  }

  async function desactivar() {
    setEnviando(true);
    try {
      await onGuardar(false, undefined);
    } finally {
      setEnviando(false);
    }
  }

  function cancelar() {
    volverAEnfocarRef.current = true;
    setEditando(false);
    setRazon("");
  }

  if (activo) {
    return (
      <div
        className="adoc-asistente-admin-mantenimiento adoc-asistente-admin-mantenimiento--activo"
        role="group"
        aria-label="Modo mantenimiento"
      >
        <div className="adoc-asistente-admin-mantenimiento-fila">
          <span className="adoc-asistente-admin-mantenimiento-icono" aria-hidden="true">
            <IconoWrench />
          </span>
          <div className="adoc-asistente-admin-mantenimiento-texto">
            <p className="adoc-asistente-admin-mantenimiento-titulo">Asistente en mantenimiento</p>
            <p className="adoc-asistente-admin-mantenimiento-copete">
              Sólo quien administra puede consultar, para verificar. Razón visible: «
              {mantenimiento?.razon}»
            </p>
          </div>
          <Button
            ref={botonDesactivarRef}
            variant="secondary"
            loading={enviando}
            disabled={enviando}
            onClick={() => void desactivar()}
          >
            Desactivar mantenimiento
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div
      className="adoc-asistente-admin-mantenimiento"
      role="group"
      aria-label="Modo mantenimiento"
    >
      <div className="adoc-asistente-admin-mantenimiento-fila">
        <span className="adoc-asistente-admin-mantenimiento-punto" aria-hidden="true" />
        <div className="adoc-asistente-admin-mantenimiento-texto">
          <p className="adoc-asistente-admin-mantenimiento-titulo">Asistente disponible</p>
          <p className="adoc-asistente-admin-mantenimiento-copete">
            Todos los usuarios con permiso pueden consultar.
          </p>
        </div>
        {!editando && (
          <Button
            ref={botonActivarRef}
            variant="secondary"
            className="adoc-asistente-admin-mantenimiento-activar"
            onClick={() => setEditando(true)}
          >
            <IconoWrench />
            Activar mantenimiento
          </Button>
        )}
      </div>

      {editando && (
        <div className="adoc-asistente-admin-mantenimiento-panel">
          <Field label="Razón" required hint="Se muestra en el banner de todos los usuarios.">
            <Textarea
              value={razon}
              onChange={(e) => setRazon(e.target.value)}
              placeholder="p. ej. Actualización del modelo, volvemos a las 14 h."
              rows={2}
              autoFocus
            />
          </Field>
          <div className="adoc-asistente-admin-mantenimiento-acciones">
            <Button variant="ghost" size="sm" disabled={enviando} onClick={cancelar}>
              Cancelar
            </Button>
            <Button
              size="sm"
              loading={enviando}
              disabled={enviando || faltaRazon}
              onClick={() => void activar()}
            >
              Activar mantenimiento
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

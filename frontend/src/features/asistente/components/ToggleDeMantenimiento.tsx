import { useState } from "react";
import { Button, Field, Textarea, Toggle } from "@ars-docendi/ui";

import type { MantenimientoDelAsistente } from "../types";

interface ToggleDeMantenimientoProps {
  /** El estado vigente, de `capacidades`. `undefined` mientras no se conoce. */
  mantenimiento: MantenimientoDelAsistente | undefined;
  onGuardar: (activo: boolean, razon?: string) => Promise<void>;
}

/**
 * El kill switch (asistente-modo-mantenimiento, tasks.md 11.6).
 *
 * LA RAZÓN ES OBLIGATORIA PARA ACTIVAR, del mismo modo que ya lo exige el
 * backend (6.2): «Guardar cambios» queda deshabilitado hasta que haya texto,
 * en vez de dejar que el pedido llegue y vuelva con un 400 — el mismo criterio
 * que `SoporteHistorialPage` ya aplica con su propia razón obligatoria.
 */
export function ToggleDeMantenimiento({ mantenimiento, onGuardar }: ToggleDeMantenimientoProps) {
  const [activo, setActivo] = useState(mantenimiento?.activo ?? false);
  const [razon, setRazon] = useState(mantenimiento?.razon ?? "");
  const [enviando, setEnviando] = useState(false);

  // Se sincroniza con lo que trae `capacidades` cuando cambia desde afuera —otra
  // pestaña, u otro admin—, comparando el VALOR y no la identidad del objeto:
  // react-query entrega un objeto nuevo en cada refetch aunque no haya cambiado
  // nada, y comparar por identidad pisaría lo que este admin esté escribiendo
  // ahora mismo. Ajuste de estado en render (mismo patrón que `Sidebar.tsx`
  // `GrupoColapsable`), no un efecto.
  const [ultimoActivoVisto, setUltimoActivoVisto] = useState(mantenimiento?.activo);
  const [ultimaRazonVista, setUltimaRazonVista] = useState(mantenimiento?.razon ?? null);
  if (
    mantenimiento &&
    (mantenimiento.activo !== ultimoActivoVisto ||
      (mantenimiento.razon ?? null) !== ultimaRazonVista)
  ) {
    setUltimoActivoVisto(mantenimiento.activo);
    setUltimaRazonVista(mantenimiento.razon ?? null);
    setActivo(mantenimiento.activo);
    setRazon(mantenimiento.razon ?? "");
  }

  const faltaRazon = activo && razon.trim().length === 0;

  async function guardar() {
    if (faltaRazon) return;
    setEnviando(true);
    try {
      await onGuardar(activo, activo ? razon.trim() : undefined);
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div
      className="adoc-asistente-admin-mantenimiento"
      role="group"
      aria-label="Modo mantenimiento"
    >
      <h3>Modo mantenimiento</h3>

      <Toggle
        label="Asistente en mantenimiento"
        checked={activo}
        onChange={(e) => setActivo(e.target.checked)}
      />

      {activo && (
        <Field
          label="Razón"
          required
          hint="Obligatoria para activar el mantenimiento. Se ve en el banner de todos los usuarios."
        >
          <Textarea
            value={razon}
            onChange={(e) => setRazon(e.target.value)}
            placeholder="¿Por qué se activa el mantenimiento?"
            rows={2}
          />
        </Field>
      )}

      <Button onClick={() => void guardar()} loading={enviando} disabled={enviando || faltaRazon}>
        Guardar cambios
      </Button>
    </div>
  );
}

import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Field, Select } from "@ars-docendi/ui";

import { EditorDeLimite } from "../components/EditorDeLimite";
import { PanelDeUso } from "../components/PanelDeUso";
import { ToggleDeMantenimiento } from "../components/ToggleDeMantenimiento";
import { PageHeader } from "../../../shared/ui/PageHeader";
import {
  editarCupoDeRol,
  editarCupoDeUsuario,
  editarMantenimiento,
  editarTopeOrganizacional,
  obtenerUso,
} from "../api/administracionAsistenteApi";
import { obtenerCapacidades } from "../api/asistenteApi";
import type { PeriodoDeUso } from "../types";
import "../asistente.css";

const PERIODOS: { valor: PeriodoDeUso; etiqueta: string }[] = [
  { valor: "dia", etiqueta: "Hoy" },
  { valor: "semana", etiqueta: "Última semana" },
  { valor: "mes", etiqueta: "Último mes" },
];

/**
 * El panel administrativo del asistente (asistente-administracion-de-uso).
 *
 * SÓLO LLEGA ACÁ QUIEN TIENE `asistente.administrar`: `routes.tsx` la
 * envuelve con `RequirePermission`, el mismo criterio que ya usa
 * `soporte-historial` — esta página no vuelve a chequear el permiso.
 *
 * UNA SOLA REGIÓN VIVA PROPIA DE LA PÁGINA (tasks.md 13.2), separada de la del
 * asistente conversacional (`Conversacion.tsx`): esta pantalla no monta el
 * panel de chat, así que no hay una región viva existente que reusar acá.
 */
export function AdministracionAsistentePage() {
  const [periodo, setPeriodo] = useState<PeriodoDeUso>("dia");
  const [anuncio, setAnuncio] = useState("");
  const queryClient = useQueryClient();

  const uso = useQuery({
    queryKey: ["asistente", "administracion", "uso", periodo],
    queryFn: () => obtenerUso(periodo),
  });

  const capacidades = useQuery({
    queryKey: ["asistente", "capacidades"],
    queryFn: obtenerCapacidades,
  });

  async function refrescarCapacidades() {
    await queryClient.invalidateQueries({ queryKey: ["asistente", "capacidades"] });
  }

  async function guardarMantenimiento(activo: boolean, razon?: string) {
    await editarMantenimiento(activo, razon);
    await refrescarCapacidades();
    // El foco se queda en el botón que se apretó: el anuncio es la ÚNICA cosa
    // que se mueve (tasks.md 13.2), igual que ya hace el resto de la feature
    // con sus propias confirmaciones.
    setAnuncio(activo ? "Modo mantenimiento activado." : "Modo mantenimiento desactivado.");
  }

  return (
    <div className="adoc-asistente-administracion">
      <PageHeader
        title="Uso del asistente"
        meta="Panel administrativo: presupuestos, tope organizacional y mantenimiento."
      />

      {/* Única región viva de esta página: anuncia guardados sin mover el foco
          (tasks.md 13.2), con el mismo mecanismo simple que ya usa
          `IndicadorDeProceso` —un solo texto, reemplazado entero—. */}
      <p role="status" aria-live="polite" className="adoc-asistente-admin-anuncio">
        {anuncio}
      </p>

      <ToggleDeMantenimiento
        mantenimiento={capacidades.data?.mantenimiento}
        onGuardar={guardarMantenimiento}
      />

      <section aria-label="Panel de uso">
        <Field label="Período">
          <Select value={periodo} onChange={(e) => setPeriodo(e.target.value as PeriodoDeUso)}>
            {PERIODOS.map((p) => (
              <option key={p.valor} value={p.valor}>
                {p.etiqueta}
              </option>
            ))}
          </Select>
        </Field>

        <PanelDeUso uso={uso.data} cargando={uso.isLoading} />
      </section>

      <section aria-label="Presupuestos" className="adoc-asistente-admin-presupuestos">
        <EditorDeLimite
          titulo="Cupo diario por rol"
          descripcion="Turnos por día para todos los usuarios de un rol. 0 desactiva el tope."
          etiquetaClave="Código de rol"
          placeholderClave="p. ej. docente"
          etiquetaValor="Cupo diario (turnos)"
          onGuardar={(rol, cupo) => editarCupoDeRol(rol, cupo)}
          onGuardado={setAnuncio}
        />

        <EditorDeLimite
          titulo="Override de cupo por usuario"
          descripcion="Gana siempre sobre el default del rol, más chico o más grande. 0 desactiva el tope."
          etiquetaClave="Id del usuario"
          placeholderClave="UUID del actor"
          etiquetaValor="Cupo diario (turnos)"
          onGuardar={(actorId, cupo) => editarCupoDeUsuario(actorId, cupo)}
          onGuardado={setAnuncio}
        />

        <EditorDeLimite
          titulo="Tope organizacional mensual"
          descripcion="Gasto estimado en USD, todos los usuarios juntos. 0 desactiva el tope."
          etiquetaValor="Tope mensual (USD)"
          admiteDecimales
          onGuardar={(_clave, tope) => editarTopeOrganizacional(tope)}
          onGuardado={setAnuncio}
        />
      </section>
    </div>
  );
}

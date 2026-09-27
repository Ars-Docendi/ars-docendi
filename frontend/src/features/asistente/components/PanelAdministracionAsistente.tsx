import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";

import { KpisDeUso } from "./KpisDeUso";
import { PanelDeUso } from "./PanelDeUso";
import { ToggleDeMantenimiento } from "./ToggleDeMantenimiento";
import { TopeOrganizacionalCard } from "./TopeOrganizacionalCard";
import {
  editarCupoDeRol,
  editarCupoDeUsuario,
  editarMantenimiento,
  editarTopeOrganizacional,
  obtenerPresupuestos,
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

interface PanelAdministracionAsistenteProps {
  /**
   * Contador que sube cada vez que la pestaña Asistente de «Sistema» pide
   * «Actualizar» (design D8, sistema-seccion-unificada). El panel reacciona
   * invalidando sus propias queries (`["asistente","administracion"]` y
   * `["asistente","capacidades"]`) SIN perder el período elegido — nada más
   * se resetea.
   */
  actualizacion?: number;
}

/**
 * El panel administrativo del asistente (asistente-administracion-de-uso),
 * rediseñado 1:1 con la referencia «Uso del asistente» (sistema-seccion-
 * unificada): un período en pastillas en vez de un `<select>` de ancho
 * completo, cuatro KPIs organizacionales + el tope mensual arriba, y el
 * detalle por usuario/rol con el cupo diario editado directo en la fila
 * (`PanelDeUso`) en vez de formularios sueltos que pedían escribir un código
 * de rol o un UUID a mano.
 *
 * SÓLO LLEGA ACÁ QUIEN TIENE `asistente.administrar`: el gate lo aplica la
 * pestaña de `SistemaPage` (mismo criterio que antes aplicaba `routes.tsx`
 * con `RequirePermission`) — este componente no vuelve a chequear el permiso.
 */
export function PanelAdministracionAsistente({ actualizacion }: PanelAdministracionAsistenteProps) {
  const [periodo, setPeriodo] = useState<PeriodoDeUso>("dia");
  const [anuncio, setAnuncio] = useState("");
  const queryClient = useQueryClient();

  const uso = useQuery({
    queryKey: ["asistente", "administracion", "uso", periodo],
    queryFn: () => obtenerUso(periodo),
  });

  // El tope, el gasto del mes y los cupos persistidos (tarea 12.8 de
  // sistema-seccion-unificada) — reemplaza el "guardado en esta sesión" que
  // `TopeOrganizacionalCard`/`EditorDeCupoEnFila` usaban porque no había
  // ningún `GET` todavía.
  const presupuestos = useQuery({
    queryKey: ["asistente", "administracion", "presupuestos"],
    queryFn: obtenerPresupuestos,
  });

  const capacidades = useQuery({
    queryKey: ["asistente", "capacidades"],
    queryFn: obtenerCapacidades,
  });

  async function refrescarCapacidades() {
    await queryClient.invalidateQueries({ queryKey: ["asistente", "capacidades"] });
  }

  async function refrescarPresupuestos() {
    await queryClient.invalidateQueries({
      queryKey: ["asistente", "administracion", "presupuestos"],
    });
  }

  // «Actualizar» de la sección Sistema: invalida las queries propias del panel
  // sin tocar el período elegido (design D8). No corre en el montaje inicial.
  useEffect(() => {
    if (actualizacion === undefined) return;
    void queryClient.invalidateQueries({ queryKey: ["asistente", "administracion"] });
    void refrescarCapacidades();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [actualizacion]);

  async function guardarMantenimiento(activo: boolean, razon?: string) {
    await editarMantenimiento(activo, razon);
    await refrescarCapacidades();
    // El foco se queda en el botón que se apretó: el anuncio es la ÚNICA cosa
    // que se mueve (tasks.md 13.2), igual que ya hace el resto de la feature
    // con sus propias confirmaciones.
    setAnuncio(activo ? "Modo mantenimiento activado." : "Modo mantenimiento desactivado.");
  }

  async function guardarTope(topeMensualUsd: number) {
    await editarTopeOrganizacional(topeMensualUsd);
    await refrescarPresupuestos();
  }

  async function guardarCupoDeRolYRefrescar(rol: string, cupo: number) {
    await editarCupoDeRol(rol, cupo);
    await refrescarPresupuestos();
  }

  async function guardarCupoDeUsuarioYRefrescar(actorId: string, cupo: number) {
    await editarCupoDeUsuario(actorId, cupo);
    await refrescarPresupuestos();
  }

  return (
    <div className="adoc-asistente-administracion">
      {/* Única región viva de esta pantalla: anuncia guardados sin mover el foco
          (tasks.md 13.2), con el mismo mecanismo simple que ya usa
          `IndicadorDeProceso` —un solo texto, reemplazado entero—. */}
      <p role="status" aria-live="polite" className="adoc-asistente-admin-anuncio">
        {anuncio}
      </p>

      <div className="adoc-asistente-admin-encabezado">
        <p className="adoc-asistente-admin-copete">
          Consumo, presupuestos, acceso y mantenimiento.
        </p>
        <div className="adoc-asistente-admin-periodo" role="group" aria-label="Período">
          {PERIODOS.map((p) => (
            <button
              key={p.valor}
              type="button"
              aria-pressed={periodo === p.valor}
              className="adoc-asistente-admin-periodo-boton"
              onClick={() => setPeriodo(p.valor)}
            >
              {p.etiqueta}
            </button>
          ))}
        </div>
      </div>

      <ToggleDeMantenimiento
        mantenimiento={capacidades.data?.mantenimiento}
        onGuardar={guardarMantenimiento}
      />

      <section aria-label="Organización" className="adoc-asistente-admin-kpis-fila">
        <KpisDeUso organizacion={uso.data?.organizacion} />
        <TopeOrganizacionalCard
          topeConocido={presupuestos.data?.topeMensualUsd}
          gastoEstimadoDelMes={presupuestos.data?.gastoEstimadoDelMes}
          error={presupuestos.isError}
          onGuardar={guardarTope}
          onGuardado={setAnuncio}
        />
      </section>

      <section aria-label="Panel de uso">
        <PanelDeUso
          uso={uso.data}
          cargando={uso.isLoading}
          cuposPorRol={presupuestos.data?.cuposPorRol ?? []}
          overridesPorUsuario={presupuestos.data?.overridesPorUsuario ?? []}
          onGuardarCupoDeRol={guardarCupoDeRolYRefrescar}
          onGuardarCupoDeUsuario={guardarCupoDeUsuarioYRefrescar}
          onGuardado={setAnuncio}
        />
      </section>
    </div>
  );
}

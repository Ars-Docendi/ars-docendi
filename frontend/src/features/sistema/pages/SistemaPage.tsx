import { useState, type ComponentType } from "react";
import { useIsFetching, useQueryClient } from "@tanstack/react-query";
import { Button, Tabs } from "@ars-docendi/ui";
import type { TabItem } from "@ars-docendi/ui";
import { useNavigate } from "react-router-dom";

import { PageHeader } from "../../../shared/ui/PageHeader";
import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import { IconoRefreshCw } from "../../../shared/ui/iconos";
import { useSeccionSistema, type IdPestanaSistema } from "../hooks/useSeccionSistema";
import { PREFIJO_SALUD, useSaludSistema } from "../hooks/useSaludSistema";
import { COMPONENTES_PING } from "../api/sistemaApi";
import {
  resumirSalud,
  type EntradaComponenteSalud,
  type EstadoDePunto,
} from "../utils/resumirSalud";
import { PuntoDeEstado } from "../components/PuntoDeEstado";
import { PestanaEstado } from "../components/PestanaEstado";
import { PestanaAuditoria } from "../components/PestanaAuditoria";
import "../sistema.css";

interface SistemaPageProps {
  /** El panel de uso del asistente, compuesto desde `app/router.tsx` (design D8). */
  PanelAsistente: ComponentType<{ actualizacion?: number }>;
}

const NOMBRES_PESTANAS: Record<IdPestanaSistema, string> = {
  estado: "Estado",
  asistente: "Asistente",
  auditoria: "Auditoría",
};

const PREFIJO_ASISTENTE = ["asistente"] as const;
const PREFIJO_AUDITORIA = ["administracion", "auditoria"] as const;

function etiquetaEstado(tono: EstadoDePunto): string {
  if (tono === "negativo") return "componentes no disponibles";
  if (tono === "neutral") return "estado pendiente";
  return "sin problemas";
}

function etiquetaAsistente(tono: EstadoDePunto): string {
  if (tono === "advertencia") return "en mantenimiento";
  if (tono === "neutral") return "estado pendiente";
  return "sin mantenimiento";
}

/**
 * La sección «Sistema» (design D9/D10, sistema-seccion-unificada): un route,
 * tres pestañas gateadas por permiso, deep-linkables por hash. Las consultas
 * de salud se piden acá arriba —no sólo dentro de la pestaña Estado— porque
 * el punto de la pestaña Estado las necesita aunque esté en otra pestaña
 * (design D10); react-query comparte la misma clave de query, así que no hay
 * pedidos duplicados cuando la pestaña Estado también las usa.
 */
export function SistemaPage({ PanelAsistente }: SistemaPageProps) {
  const { user } = useCurrentUser();
  const permisos = user?.permissions ?? [];
  const puedeEstado = permisos.includes("sistema.estado.ver");
  const puedeAsistente = permisos.includes("asistente.administrar");
  const puedeAuditoria = permisos.includes("auditoria.ver");

  const { pestanaActiva, pestanasPermitidas, cambiarPestana } = useSeccionSistema(permisos);
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  // `undefined` hasta el primer «Actualizar»: el panel sólo reacciona a partir
  // de ahí, nunca en su propio montaje inicial.
  const [contadorAsistente, setContadorAsistente] = useState<number | undefined>(undefined);

  const { pings, postgresql } = useSaludSistema(puedeEstado);
  const entradas: EntradaComponenteSalud[] = [
    ...COMPONENTES_PING.map((c) => ({
      id: c.id,
      nombre: c.nombre,
      disponible: pings[c.id].data?.disponible,
      comprobadoEn: pings[c.id].data?.comprobadoEn,
    })),
    {
      id: "postgresql",
      nombre: "PostgreSQL",
      disponible: postgresql.data ? postgresql.data.estado === "disponible" : undefined,
      comprobadoEn: postgresql.data?.comprobadoEn,
    },
  ];
  const resumen = resumirSalud({
    componentes: entradas,
    mantenimientoAsistente: postgresql.data?.mantenimientoAsistente ?? "desconocido",
  });

  const prefijoActivo =
    pestanaActiva === "estado"
      ? PREFIJO_SALUD
      : pestanaActiva === "auditoria"
        ? PREFIJO_AUDITORIA
        : PREFIJO_ASISTENTE;
  const cantidadEnCurso = useIsFetching({ queryKey: prefijoActivo });
  const actualizando = cantidadEnCurso > 0;

  function alActualizar() {
    if (pestanaActiva === "asistente") {
      // El panel reacciona a este contador invalidando sus propias queries
      // (design D8): no hace falta `refetchQueries` acá.
      setContadorAsistente((c) => (c ?? 0) + 1);
      return;
    }
    void queryClient.refetchQueries({ queryKey: prefijoActivo });
  }

  function irAAuditoriaConEvento(idEvento: string) {
    navigate({
      pathname: "/sistema",
      search: `?periodo=todo&evento=${encodeURIComponent(idEvento)}`,
      hash: "#auditoria",
    });
  }

  function irATodaLaAuditoria() {
    navigate({ pathname: "/sistema", hash: "#auditoria" });
  }

  const items: TabItem[] = pestanasPermitidas.map((id) => {
    const tono: EstadoDePunto =
      id === "estado"
        ? resumen.puntoEstado
        : id === "asistente"
          ? resumen.puntoAsistente
          : "neutral";
    const etiqueta =
      id === "estado"
        ? etiquetaEstado(tono)
        : id === "asistente"
          ? etiquetaAsistente(tono)
          : "sin novedades";
    return {
      id,
      label: (
        <>
          <PuntoDeEstado tono={tono} etiqueta={etiqueta} />
          {NOMBRES_PESTANAS[id]}
        </>
      ),
    };
  });

  return (
    <main className="sistema-seccion">
      <PageHeader
        title="Sistema"
        meta="Estado de los servicios, uso del asistente y registros de auditoría."
        actions={
          <Button
            variant="secondary"
            type="button"
            onClick={alActualizar}
            disabled={actualizando}
            loading={actualizando}
          >
            <span className="sistema-icono-actualizar">
              <IconoRefreshCw />
            </span>
            {actualizando ? "Actualizando…" : "Actualizar"}
          </Button>
        }
      />

      <Tabs
        items={items}
        value={pestanaActiva}
        onChange={(id) => cambiarPestana(id as IdPestanaSistema)}
        aria-label="Secciones de Sistema"
      />

      <div role="tabpanel" id={`panel-${pestanaActiva}`} aria-labelledby={`tab-${pestanaActiva}`}>
        {pestanaActiva === "estado" && (
          <PestanaEstado
            puedeVerUso={puedeAsistente}
            puedeVerAuditoria={puedeAuditoria}
            onVerUso={() => cambiarPestana("asistente")}
            onAbrirCambio={irAAuditoriaConEvento}
            onVerTodaLaAuditoria={irATodaLaAuditoria}
          />
        )}
        {pestanaActiva === "asistente" && <PanelAsistente actualizacion={contadorAsistente} />}
        {pestanaActiva === "auditoria" && <PestanaAuditoria />}
      </div>
    </main>
  );
}

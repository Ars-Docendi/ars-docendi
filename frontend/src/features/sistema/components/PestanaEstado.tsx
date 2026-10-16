import { COMPONENTES_PING, type IdComponentePing } from "../api/sistemaApi";
import { useSaludSistema } from "../hooks/useSaludSistema";
import { resumirSalud, type EntradaComponenteSalud } from "../utils/resumirSalud";
import { BannerDeSalud } from "./BannerDeSalud";
import { TarjetaDeComponente } from "./TarjetaDeComponente";
import { CambiosRecientes } from "./CambiosRecientes";

interface PestanaEstadoProps {
  puedeVerUso: boolean;
  puedeVerAuditoria: boolean;
  onVerUso: () => void;
  onAbrirCambio: (idEvento: string) => void;
  onVerTodaLaAuditoria: () => void;
}

const NOMBRES: Record<IdComponentePing, string> = Object.fromEntries(
  COMPONENTES_PING.map((c) => [c.id, c.nombre]),
) as Record<IdComponentePing, string>;

/** La pestaña Estado (design D12, requisitos «Dashboard de salud operativa» y «Estado summary banner...»). */
export function PestanaEstado({
  puedeVerUso,
  puedeVerAuditoria,
  onVerUso,
  onAbrirCambio,
  onVerTodaLaAuditoria,
}: PestanaEstadoProps) {
  const { pings, postgresql, reintentar } = useSaludSistema(true);

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

  const modulos = COMPONENTES_PING.filter((c) => c.id !== "asistente");
  const asistente = COMPONENTES_PING.find((c) => c.id === "asistente")!;

  return (
    <div className="sistema-pestana-estado">
      <BannerDeSalud resumen={resumen} />

      <section aria-labelledby="sistema-grupo-modulos">
        <h2 id="sistema-grupo-modulos" className="sistema-grupo-titulo">
          Módulos
        </h2>
        <div className="sistema-tarjetas-grid">
          {modulos.map((c) => {
            const consulta = pings[c.id];
            return (
              <TarjetaDeComponente
                key={c.id}
                nombre={NOMBRES[c.id]}
                tipo="modulo"
                cargando={consulta.isLoading || consulta.isPending}
                disponible={consulta.data?.disponible}
                motivoFalla={consulta.data?.motivoFalla}
                duracionMs={consulta.data?.duracionMs}
                onReintentar={() => reintentar(c.id)}
              />
            );
          })}
          <TarjetaDeComponente
            nombre={NOMBRES.asistente}
            tipo="asistente"
            cargando={pings.asistente.isLoading || pings.asistente.isPending}
            disponible={pings.asistente.data?.disponible}
            motivoFalla={pings.asistente.data?.motivoFalla}
            duracionMs={pings.asistente.data?.duracionMs}
            mantenimientoAsistente={postgresql.data?.mantenimientoAsistente}
            puedeVerUso={puedeVerUso}
            onVerUso={onVerUso}
            onReintentar={() => reintentar(asistente.id)}
          />
        </div>
      </section>

      <section aria-labelledby="sistema-grupo-infra">
        <h2 id="sistema-grupo-infra" className="sistema-grupo-titulo">
          Infraestructura
        </h2>
        <div className="sistema-tarjetas-grid">
          <TarjetaDeComponente
            nombre="PostgreSQL"
            tipo="postgresql"
            cargando={postgresql.isLoading || postgresql.isPending}
            disponible={postgresql.data ? postgresql.data.estado === "disponible" : undefined}
            duracionMs={postgresql.data?.duracionMs}
            onReintentar={() => reintentar("postgresql")}
          />
        </div>
      </section>

      {puedeVerAuditoria && (
        <CambiosRecientes onAbrirCambio={onAbrirCambio} onVerTodo={onVerTodaLaAuditoria} />
      )}
    </div>
  );
}

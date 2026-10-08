import type { ResumenSalud } from "../utils/resumirSalud";
import { fechaHoraCompletaEnZona } from "../utils/zonaHoraria";

interface BannerDeSaludProps {
  resumen: ResumenSalud;
}

// `toLocaleString("es-AR", { dateStyle: "short", timeStyle: "medium" })` da
// «26/9/26, 10:45:23 p. m.»: año de 2 dígitos, coma y AM/PM en vez del
// `26/9/2026 22:16:48` de 24 horas del canvas (task 11.4, defecto #2).
// `fechaHoraCompletaEnZona` es el único formateador compartido para esto
// (design D11): mismo resultado acá y en `PanelDetalleEvento`.
function fechaLocal(valor: string | null): string {
  if (!valor) return "—";
  const fecha = new Date(valor);
  if (!Number.isFinite(fecha.getTime())) return "—";
  return fechaHoraCompletaEnZona(fecha);
}

/** El banner resumen de la pestaña Estado (design D12, requisito «Estado summary banner...»). */
export function BannerDeSalud({ resumen }: BannerDeSaludProps) {
  return (
    <div
      className={`sistema-banner sistema-banner--${resumen.variante}`}
      role="status"
      aria-live="polite"
    >
      <span className="sistema-banner-punto" aria-hidden="true" />
      <div className="sistema-banner-texto">
        <div className="sistema-banner-titulo">{resumen.titulo}</div>
        {resumen.subtitulo && <div className="sistema-banner-subtitulo">{resumen.subtitulo}</div>}
      </div>
      <div className="sistema-banner-comprobacion">
        Última comprobación
        <br />
        <span className="sistema-banner-hora">{fechaLocal(resumen.ultimaComprobacion)}</span>
      </div>
    </div>
  );
}

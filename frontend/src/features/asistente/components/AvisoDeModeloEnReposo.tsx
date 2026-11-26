import { Button, InlineAlert } from "@ars-docendi/ui";

import type { AvisoDelModelo } from "../hooks/useDisponibilidadDelModelo";

interface AvisoDeModeloEnReposoProps {
  aviso: AvisoDelModelo;
  onActivar: () => void;
}

const TEXTOS: Record<Exclude<AvisoDelModelo, "oculto">, string> = {
  en_reposo: "El modelo está en reposo. La primera respuesta puede demorar unos segundos.",
  activando: "Activando el modelo…",
  listo: "El modelo está listo.",
  fallo: "No se pudo activar el modelo. Probá de nuevo.",
};

const SEVERIDAD = {
  en_reposo: "info",
  activando: "info",
  listo: "success",
  fallo: "danger",
} as const;

/**
 * El modelo propio descargado de la GPU compartida y cómo cargarlo antes de preguntar
 * (asistente-modelo-en-reposo, D4). La región viva existe mientras haya aviso y se
 * reutiliza entre sus estados (en reposo → activando → listo), así que los cambios se anuncian; el
 * panel ya tiene otras regiones vivas y ésta no las reemplaza. Sin notificaciones del sistema.
 */
export function AvisoDeModeloEnReposo({ aviso, onActivar }: AvisoDeModeloEnReposoProps) {
  if (aviso === "oculto") return null;

  return (
    <div role="status" aria-live="polite" className="adoc-asistente-modelo-aviso">
      {/* `presentation`: la región viva ya es el contenedor; dos anunciarían el texto dos veces. */}
      <InlineAlert severity={SEVERIDAD[aviso]} role="presentation">
        <div className="adoc-asistente-modelo-aviso-fila">
          <p className="adoc-asistente-modelo-aviso-texto">{TEXTOS[aviso]}</p>
          {aviso !== "listo" && (
            <Button
              variant="secondary"
              size="sm"
              disabled={aviso === "activando"}
              onClick={onActivar}
            >
              Activar modelo
            </Button>
          )}
        </div>
        {aviso === "activando" && (
          // Ollama no informa el avance de la carga: indeterminada, nunca un porcentaje inventado.
          <div className="adoc-asistente-modelo-progreso" aria-hidden="true" />
        )}
      </InlineAlert>
    </div>
  );
}

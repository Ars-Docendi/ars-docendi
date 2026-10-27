import { useRef, useState } from "react";
import { Button } from "@ars-docendi/ui";

import type { ModeloEnUso } from "../types";
import { nombreDelModelo } from "../utils/modeloDelAsistente";

interface BannerDelModeloEnUsoProps {
  enUso: ModeloEnUso;
  /** El modelo elegido en pantalla es de una API en la nube: la redacción enmascara siempre. */
  enmascara: boolean;
  onVolverAlServidor: () => Promise<void>;
}

/**
 * «En uso: …» (asistente-modelo-en-caliente, tarea 4.3): el mismo banner compacto del modo
 * mantenimiento, con el modelo vigente, de dónde sale y, sólo si fue elegido desde esta pantalla,
 * el camino de vuelta a la configuración del servidor.
 */
export function BannerDelModeloEnUso({
  enUso,
  enmascara,
  onVolverAlServidor,
}: BannerDelModeloEnUsoProps) {
  const [enviando, setEnviando] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // «Volver…» desaparece al tener éxito (el origen pasa a `servidor`): el foco
  // se lleva al banner para no soltarlo al `<body>`.
  const grupoRef = useRef<HTMLDivElement>(null);

  async function volver() {
    setEnviando(true);
    setError(null);
    try {
      await onVolverAlServidor();
      grupoRef.current?.focus();
    } catch {
      setError("No se pudo volver a la configuración del servidor. Probá de nuevo.");
    } finally {
      setEnviando(false);
    }
  }

  const enPantalla = enUso.origen === "pantalla";

  return (
    <div
      ref={grupoRef}
      tabIndex={-1}
      className="adoc-asistente-admin-mantenimiento"
      role="group"
      aria-label="Modelo en uso"
    >
      <div className="adoc-asistente-admin-mantenimiento-fila">
        <span className="adoc-asistente-admin-mantenimiento-punto" aria-hidden="true" />
        <div className="adoc-asistente-admin-mantenimiento-texto">
          <p className="adoc-asistente-admin-mantenimiento-titulo">
            En uso:{" "}
            <span className="adoc-asistente-modelo-mono">
              {nombreDelModelo(enUso.empresa, enUso.modelo)}
            </span>
          </p>
          <p className="adoc-asistente-admin-mantenimiento-copete">
            {enPantalla
              ? "Elegido desde esta pantalla. Rige desde el próximo turno."
              : "Configuración del servidor: tomada de las variables de entorno al iniciar."}
          </p>
          {enmascara && (
            <p className="adoc-asistente-admin-mantenimiento-copete">
              Con una API en la nube, los datos personales viajan siempre enmascarados.
            </p>
          )}
        </div>
        {enPantalla && (
          <Button variant="secondary" size="sm" loading={enviando} onClick={() => void volver()}>
            Volver a la configuración del servidor
          </Button>
        )}
      </div>
      {error && (
        <p className="adoc-asistente-admin-mantenimiento-error" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}

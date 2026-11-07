import { useEffect, type RefObject } from "react";
import { Button, InlineAlert } from "@ars-docendi/ui";

/** Lo que la pantalla muestra de la prueba del destino que está elegido ahora. */
export type VistaDePrueba =
  | { estado: "inactiva" }
  | { estado: "probando" }
  | { estado: "ok"; latenciaMs: number }
  /** Una prueba que el proveedor no aceptó; `texto` ya viene traducido. */
  | { estado: "fallida"; texto: string }
  /** El servicio estaba ocupado: no es un fallo de la configuración. */
  | { estado: "saturada"; texto: string };

interface AccionesDelModeloProps {
  prueba: VistaDePrueba;
  destino: string;
  vigente: string;
  /** La empresa cuya clave nueva se guardará al aplicar, si corresponde. */
  claveAGuardarDe: string | null;
  puedeProbar: boolean;
  puedeAplicar: boolean;
  confirmando: boolean;
  aplicando: boolean;
  onProbar: () => void;
  onAplicar: () => void;
  onCancelar: () => void;
  onConfirmar: () => void;
  botonProbarRef: RefObject<HTMLButtonElement | null>;
  botonAplicarRef: RefObject<HTMLButtonElement | null>;
}

/** «Probar conexión» y «Aplicar cambio» (tarea 4.8). */
export function AccionesDelModelo({
  prueba,
  destino,
  vigente,
  claveAGuardarDe,
  puedeProbar,
  puedeAplicar,
  confirmando,
  aplicando,
  onProbar,
  onAplicar,
  onCancelar,
  onConfirmar,
  botonProbarRef,
  botonAplicarRef,
}: AccionesDelModeloProps) {
  const probando = prueba.estado === "probando";

  return (
    <>
      {prueba.estado === "ok" && (
        <InlineAlert severity="success" title="Conexión correcta.">
          <span className="adoc-asistente-modelo-mono">{destino}</span> respondió en{" "}
          {prueba.latenciaMs} ms.
        </InlineAlert>
      )}
      {prueba.estado === "fallida" && (
        <InlineAlert severity="danger" title="La prueba falló.">
          {prueba.texto} No se guardó nada.
        </InlineAlert>
      )}
      {prueba.estado === "saturada" && <InlineAlert severity="info" title={prueba.texto} />}

      {confirmando ? (
        <ConfirmacionDelCambio
          destino={destino}
          vigente={vigente}
          claveAGuardarDe={claveAGuardarDe}
          aplicando={aplicando}
          onCancelar={onCancelar}
          onConfirmar={onConfirmar}
        />
      ) : (
        <div className="adoc-asistente-modelo-acciones">
          <span className="adoc-hint adoc-asistente-modelo-acciones-pista">
            Para aplicar, primero la prueba tiene que pasar.
          </span>
          <Button
            ref={botonProbarRef}
            variant="secondary"
            loading={probando}
            disabled={!puedeProbar}
            onClick={onProbar}
          >
            {probando ? "Probando…" : "Probar conexión"}
          </Button>
          <Button ref={botonAplicarRef} disabled={!puedeAplicar} onClick={onAplicar}>
            Aplicar cambio
          </Button>
        </div>
      )}
    </>
  );
}

interface ConfirmacionDelCambioProps {
  destino: string;
  vigente: string;
  claveAGuardarDe: string | null;
  aplicando: boolean;
  onCancelar: () => void;
  onConfirmar: () => void;
}

function ConfirmacionDelCambio({
  destino,
  vigente,
  claveAGuardarDe,
  aplicando,
  onCancelar,
  onConfirmar,
}: ConfirmacionDelCambioProps) {
  // La confirmación aparece en el lugar de los botones que se acaban de tocar:
  // el foco va a la acción, no se pierde en el `<body>`.
  useEffect(() => {
    document.getElementById("modelo-confirmar-cambio")?.focus();
  }, []);

  return (
    <>
      <InlineAlert severity="info" title="¿Cambiar el modelo?">
        Desde el próximo turno el asistente responde con{" "}
        <span className="adoc-asistente-modelo-mono">{destino}</span>. Los turnos en curso terminan
        con <span className="adoc-asistente-modelo-mono">{vigente}</span>.
        {claveAGuardarDe ? ` Se guardará la clave de ${claveAGuardarDe}, cifrada.` : ""}
      </InlineAlert>
      <div className="adoc-asistente-admin-mantenimiento-acciones">
        <Button variant="ghost" size="sm" disabled={aplicando} onClick={onCancelar}>
          Cancelar
        </Button>
        <Button
          id="modelo-confirmar-cambio"
          size="sm"
          loading={aplicando}
          disabled={aplicando}
          onClick={onConfirmar}
        >
          Confirmar cambio
        </Button>
      </div>
    </>
  );
}

import { useState } from "react";
import { Button, Field, Input, InlineAlert } from "@ars-docendi/ui";

interface PanelDeReemplazoDeClaveProps {
  nombreDeLaEmpresa: string;
  /** Los últimos cuatro caracteres de la clave que sigue en uso. */
  pista: string;
  validando: boolean;
  /** El motivo del último intento fallido, o `null`. */
  error: string | null;
  onConfirmar: (clave: string) => void;
  onCancelar: () => void;
}

/**
 * Reemplazo de la clave de una empresa (tarea 4.9), en un panel aparte para que
 * quede claro que la clave vigente sigue en uso hasta que la nueva pase la
 * prueba contra el proveedor. El texto de la clave vive sólo acá: al cerrar el
 * panel se descarta.
 */
export function PanelDeReemplazoDeClave({
  nombreDeLaEmpresa,
  pista,
  validando,
  error,
  onConfirmar,
  onCancelar,
}: PanelDeReemplazoDeClaveProps) {
  const [clave, setClave] = useState("");
  const vacia = clave.trim().length === 0;

  return (
    <div
      className="sistema-tarjeta adoc-asistente-modelo-subpanel"
      role="group"
      aria-labelledby="modelo-reemplazo-titulo"
    >
      <span id="modelo-reemplazo-titulo" className="sistema-tarjeta-nombre">
        Reemplazar la clave de {nombreDeLaEmpresa}
      </span>
      <Field
        label="Nueva clave de API"
        required
        hint={`Al confirmar, se prueba contra el proveedor. Sólo si responde, reemplaza a ••••${pista}; si no, la actual queda como está.`}
      >
        <Input
          type="password"
          autoComplete="off"
          autoFocus
          placeholder="Pegá la nueva clave"
          value={clave}
          onChange={(e) => setClave(e.target.value)}
        />
      </Field>
      {error && (
        <InlineAlert severity="danger" title="No se reemplazó.">
          {error} La clave ••••{pista} sigue en uso.
        </InlineAlert>
      )}
      <div className="adoc-asistente-admin-mantenimiento-acciones">
        <Button variant="ghost" size="sm" disabled={validando} onClick={onCancelar}>
          Cancelar
        </Button>
        <Button
          size="sm"
          loading={validando}
          disabled={validando || vacia}
          onClick={() => onConfirmar(clave.trim())}
        >
          {validando ? "Probando la nueva clave…" : "Confirmar reemplazo"}
        </Button>
      </div>
    </div>
  );
}

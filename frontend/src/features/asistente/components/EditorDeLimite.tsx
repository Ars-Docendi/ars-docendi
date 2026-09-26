import { useState, type FormEvent } from "react";
import { Button, Field, Input } from "@ars-docendi/ui";

interface EditorDeLimiteProps {
  titulo: string;
  descripcion?: string;
  /** Si viene, el editor pide además una clave (código de rol, id de usuario). */
  etiquetaClave?: string;
  placeholderClave?: string;
  etiquetaValor: string;
  /** `true` para el tope organizacional (USD); los cupos diarios son turnos enteros. */
  admiteDecimales?: boolean;
  onGuardar: (clave: string, valor: number) => Promise<void>;
  /** Texto para la región viva compartida de la página (tasks.md 13.2). */
  onGuardado: (mensaje: string) => void;
}

/**
 * Un editor de límite numérico (cupo de rol, override de usuario, tope
 * organizacional — tasks.md 11.5), con confirmación INLINE antes de BAJAR un
 * valor que ya se guardó en esta misma sesión — mismo patrón que «Eliminar»
 * en el rail de conversaciones (`RailDeConversaciones.tsx`: la fila
 * reemplaza su contenido por «¿Borrar…?» + Confirmar/Cancelar), no un `Modal`.
 *
 * NO HAY ENDPOINT PARA LEER EL VALOR VIGENTE (docs/architecture/api-contracts.md
 * §presupuestos/tope-organizacional: sólo `PUT`, nunca `GET`), así que «vigente»
 * acá es «lo último que este editor guardó con éxito», no lo que haya en la base.
 * El primer guardado de una clave nunca pide confirmación —no hay nada previo
 * con qué compararlo—.
 */
export function EditorDeLimite({
  titulo,
  descripcion,
  etiquetaClave,
  placeholderClave,
  etiquetaValor,
  admiteDecimales = false,
  onGuardar,
  onGuardado,
}: EditorDeLimiteProps) {
  const [clave, setClave] = useState("");
  const [valor, setValor] = useState("");
  const [ultimoGuardado, setUltimoGuardado] = useState<Record<string, number>>({});
  const [confirmando, setConfirmando] = useState<{ clave: string; valor: number } | null>(null);
  const [enviando, setEnviando] = useState(false);

  const claveEfectiva = etiquetaClave ? clave.trim() : "__unico__";

  async function aplicar(claveAGuardar: string, valorAGuardar: number) {
    setEnviando(true);
    try {
      await onGuardar(etiquetaClave ? claveAGuardar : "", valorAGuardar);
      setUltimoGuardado((previo) => ({ ...previo, [claveAGuardar]: valorAGuardar }));
      onGuardado(
        `${titulo} actualizado${etiquetaClave ? ` para ${claveAGuardar}` : ""}: ${
          valorAGuardar === 0 ? "sin límite" : valorAGuardar
        }.`,
      );
      setConfirmando(null);
    } finally {
      setEnviando(false);
    }
  }

  function alEnviar(evento: FormEvent) {
    evento.preventDefault();
    const numero = admiteDecimales ? Number.parseFloat(valor) : Number.parseInt(valor, 10);
    if (Number.isNaN(numero) || numero < 0) return;
    if (etiquetaClave && claveEfectiva.length === 0) return;

    const previo = ultimoGuardado[claveEfectiva];
    if (previo !== undefined && numero < previo) {
      setConfirmando({ clave: claveEfectiva, valor: numero });
      return;
    }
    void aplicar(claveEfectiva, numero);
  }

  // Reemplaza el formulario ENTERO por la confirmación, en el mismo lugar —el
  // patrón exacto de «Borrar» arriba, no un diálogo aparte.
  if (confirmando) {
    const anterior = ultimoGuardado[confirmando.clave];
    return (
      <div className="adoc-asistente-admin-editor" role="group" aria-label={titulo}>
        <h3>{titulo}</h3>
        <p className="adoc-asistente-admin-confirmar">
          ¿Bajar {etiquetaClave ? `${etiquetaClave.toLowerCase()} «${confirmando.clave}»` : titulo}{" "}
          de {anterior} a {confirmando.valor}? Alguien que hoy tiene cupo disponible puede quedar
          bloqueado de inmediato.
        </p>
        <Button
          variant="secondary"
          size="sm"
          loading={enviando}
          disabled={enviando}
          onClick={() => void aplicar(confirmando.clave, confirmando.valor)}
        >
          Confirmar
        </Button>
        <Button variant="ghost" size="sm" disabled={enviando} onClick={() => setConfirmando(null)}>
          Cancelar
        </Button>
      </div>
    );
  }

  return (
    <div className="adoc-asistente-admin-editor" role="group" aria-label={titulo}>
      <h3>{titulo}</h3>
      {descripcion && <p className="adoc-asistente-admin-editor-desc">{descripcion}</p>}

      <form onSubmit={alEnviar} className="adoc-asistente-admin-editor-form">
        {etiquetaClave && (
          <Field label={etiquetaClave}>
            <Input
              value={clave}
              onChange={(e) => setClave(e.target.value)}
              placeholder={placeholderClave}
            />
          </Field>
        )}

        <Field label={etiquetaValor}>
          <Input
            type="number"
            min={0}
            step={admiteDecimales ? "0.01" : "1"}
            value={valor}
            onChange={(e) => setValor(e.target.value)}
          />
        </Field>

        <Button type="submit" loading={enviando} disabled={enviando}>
          Guardar
        </Button>
      </form>
    </div>
  );
}

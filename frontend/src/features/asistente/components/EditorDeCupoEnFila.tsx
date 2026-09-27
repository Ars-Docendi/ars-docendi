import { useEffect, useRef, useState } from "react";
import { Button, Input } from "@ars-docendi/ui";

interface EditorDeCupoEnFilaProps {
  /** El nombre para mostrar de la fila (usuario o rol): nunca se escribe a mano. */
  nombre: string;
  /**
   * `0` desactiva el cupo; `undefined` si esta fila no tiene un cupo
   * persistido todavía (una fila de usuario sin override propio).
   */
  cupoConocido: number | undefined;
  /**
   * De dónde sale `cupoConocido`, para distinguir el default del rol de un
   * override puntual de usuario (tarea 12.8 de sistema-seccion-unificada).
   * `undefined` cuando `cupoConocido` también lo es —nada que etiquetar—.
   */
  origen?: "rol" | "override";
  onGuardar: (cupo: number) => Promise<void>;
  onGuardado: (cupo: number) => void;
}

/**
 * El cupo diario, editado DIRECTO EN LA FILA de la tabla de uso (rediseño
 * «Uso del asistente», sistema-seccion-unificada) — reemplaza los dos
 * formularios sueltos de `EditorDeLimite` («Cupo diario por rol» / «Override
 * de cupo por usuario») que pedían escribir un código de rol o un UUID a
 * mano. Acá la fila YA ES la clave: no hay nada que tipear para identificarla.
 *
 * Mismo patrón de confirmación inline al BAJAR un valor ya conocido que
 * `EditorDeLimite` usaba, y la misma limitación documentada ahí: no hay
 * `GET` para leer el cupo vigente (docs/architecture/api-contracts.md
 * §presupuestos), así que «conocido» es sólo lo que este admin guardó con
 * éxito EN ESTA SESIÓN.
 */
export function EditorDeCupoEnFila({
  nombre,
  cupoConocido,
  origen,
  onGuardar,
  onGuardado,
}: EditorDeCupoEnFilaProps) {
  const [editando, setEditando] = useState(false);
  const [valor, setValor] = useState("");
  const [confirmando, setConfirmando] = useState<number | null>(null);
  const [enviando, setEnviando] = useState(false);
  // El botón «Editar» reaparece en el lugar exacto donde estaba el foco
  // cuando la edición termina —guardada o cancelada—, para no soltar el foco
  // al `<body>` cuando el campo/los botones de edición desaparecen del DOM
  // (tasks.md 13.2: los guardados se anuncian sin mover el foco).
  const botonEditarRef = useRef<HTMLButtonElement>(null);
  // Ref, no estado: sólo decide qué hace el próximo efecto, nunca dispara un
  // render por sí sola — `setState` dentro de un efecto encadenaría otro render.
  const volverAEnfocarRef = useRef(false);

  useEffect(() => {
    if (volverAEnfocarRef.current && !editando && confirmando === null) {
      botonEditarRef.current?.focus();
      volverAEnfocarRef.current = false;
    }
  }, [editando, confirmando]);

  async function aplicar(cupo: number) {
    setEnviando(true);
    try {
      await onGuardar(cupo);
      onGuardado(cupo);
      volverAEnfocarRef.current = true;
      setEditando(false);
      setConfirmando(null);
    } finally {
      setEnviando(false);
    }
  }

  function alGuardar() {
    const numero = Number.parseInt(valor, 10);
    if (Number.isNaN(numero) || numero < 0) return;
    if (cupoConocido !== undefined && cupoConocido > 0 && numero < cupoConocido) {
      setConfirmando(numero);
      return;
    }
    void aplicar(numero);
  }

  function cancelar() {
    volverAEnfocarRef.current = true;
    setEditando(false);
    setConfirmando(null);
  }

  if (confirmando !== null) {
    return (
      <div className="adoc-asistente-admin-cupo-fila">
        <span className="adoc-asistente-admin-cupo-confirmar">
          ¿Bajar de {cupoConocido} a {confirmando}? Alguien con cupo disponible hoy puede quedar
          bloqueado de inmediato.
        </span>
        <Button
          variant="secondary"
          size="sm"
          loading={enviando}
          disabled={enviando}
          onClick={() => void aplicar(confirmando)}
        >
          Confirmar
        </Button>
        <Button variant="ghost" size="sm" disabled={enviando} onClick={cancelar}>
          Cancelar
        </Button>
      </div>
    );
  }

  if (editando) {
    return (
      <div className="adoc-asistente-admin-cupo-fila">
        <Input
          type="number"
          min={0}
          autoFocus
          aria-label={`Cupo diario de ${nombre} (turnos)`}
          value={valor}
          onChange={(e) => setValor(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") alGuardar();
            if (e.key === "Escape") cancelar();
          }}
          className="adoc-asistente-admin-cupo-input"
        />
        <Button size="sm" loading={enviando} disabled={enviando} onClick={alGuardar}>
          Guardar
        </Button>
        <Button variant="ghost" size="sm" disabled={enviando} onClick={cancelar}>
          Cancelar
        </Button>
      </div>
    );
  }

  return (
    <div className="adoc-asistente-admin-cupo-fila">
      <span className="adoc-asistente-admin-cupo-valor">
        {cupoConocido === undefined
          ? "sin override propio"
          : cupoConocido === 0
            ? "sin tope"
            : cupoConocido}
        {cupoConocido !== undefined && origen && (
          <span className="adoc-asistente-admin-cupo-origen">
            {origen === "rol" ? " (del rol)" : " (override)"}
          </span>
        )}
      </span>
      <Button
        ref={botonEditarRef}
        variant="ghost"
        size="sm"
        aria-label={`Editar cupo diario de ${nombre}`}
        onClick={() => {
          setValor(cupoConocido !== undefined ? String(cupoConocido) : "");
          setEditando(true);
        }}
      >
        Editar
      </Button>
    </div>
  );
}

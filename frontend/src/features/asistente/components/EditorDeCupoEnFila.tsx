import { useEffect, useRef, useState } from "react";
import { Button, Input } from "@ars-docendi/ui";

import { IconoRotateCcw, IconoSquarePen } from "../../../shared/ui/iconos";

/**
 * «40 por día · del rol» / «60 por día · propio» / «sin tope · propio» —
 * mismo patrón del canvas (`{{ r.cupo }} por día · {{ r.badge }}`), con
 * «sin tope» en vez de «∞ por día» (criterio ya usado en el resto de la
 * feature: `formatoDeUso`, los mensajes de `onGuardado`).
 *
 * `origen` puede faltar por dos motivos bien distintos: una fila de ROL sin
 * cupo seedeado (no debería pasar: los siete roles siempre lo tienen) o una
 * fila de USUARIO sin override Y sin ningún rol de sistema vigente — ahí
 * «sin dato» es la verdad, porque no hay ningún rol del que heredar un
 * default (`ReglaDeCupoEfectivo` en el backend, tarea «rol y cupo efectivo
 * por usuario» de sistema-seccion-unificada). Un usuario CON al menos un rol
 * y sin override sí trae un cupo resuelto: «N por día · del rol» — ya no
 * «sin dato», que es lo que este componente mostraba de la 12.8 hasta acá.
 */
function textoDeCupo(valor: number | undefined, origen: "rol" | "override" | undefined): string {
  if (valor === undefined) return "sin dato";
  const sufijo = origen ? (origen === "rol" ? "del rol" : "propio") : undefined;
  const base = valor === 0 ? "sin tope" : `${valor} por día`;
  return sufijo ? `${base} · ${sufijo}` : base;
}

interface EditorDeCupoEnFilaProps {
  /** El nombre para mostrar de la fila (usuario o rol): nunca se escribe a mano. */
  nombre: string;
  /**
   * `0` desactiva el cupo; `undefined` cuando esta fila no tiene ningún
   * valor efectivo resoluble —hoy, sólo una fila de usuario sin override
   * propio y sin ningún rol de sistema vigente (tarea «rol y cupo efectivo
   * por usuario» de sistema-seccion-unificada)—.
   */
  cupoConocido: number | undefined;
  /**
   * De dónde sale `cupoConocido`: el default del rol, o un override puntual
   * de usuario (tarea 12.8 de sistema-seccion-unificada). `undefined` cuando
   * `cupoConocido` también lo es —nada que etiquetar—.
   */
  origen?: "rol" | "override";
  onGuardar: (cupo: number) => Promise<void>;
  onGuardado: (cupo: number) => void;
  /**
   * Restablece el cupo al del rol (asistente-acceso-granular). Sólo se
   * ofrece cuando la fila tiene un cupo propio (`origen === "override"`).
   */
  onRestablecer?: () => Promise<void>;
  /**
   * Sin acceso al asistente el cupo no aplica: se muestra atenuado y no se
   * edita (canvas «Uso del asistente», columna «Acceso»).
   */
  deshabilitado?: boolean;
}

/**
 * El cupo diario, editado DIRECTO EN LA FILA de la tabla de uso (rediseño
 * «Uso del asistente», sistema-seccion-unificada) — reemplaza los dos
 * formularios sueltos de `EditorDeLimite` («Cupo diario por rol» / «Override
 * de cupo por usuario») que pedían escribir un código de rol o un UUID a
 * mano. Acá la fila YA ES la clave: no hay nada que tipear para identificarla.
 *
 * Mismo patrón de confirmación inline al BAJAR un valor ya conocido que
 * `EditorDeLimite` usaba. El botón lleva un ícono de lápiz —no el texto
 * «Editar»— con el nombre accesible en `aria-label` (fidelidad con el
 * canvas, punto 3).
 */
export function EditorDeCupoEnFila({
  nombre,
  cupoConocido,
  origen,
  onGuardar,
  onGuardado,
  onRestablecer,
  deshabilitado = false,
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
        <span className="adoc-asistente-admin-cupo-hint">0 = sin tope.</span>
      </div>
    );
  }

  async function restablecer() {
    if (!onRestablecer) return;
    setEnviando(true);
    try {
      await onRestablecer();
      // El botón de restablecer desaparece con el override: el foco vuelve
      // al lápiz, que siempre está (tasks.md 13.2).
      botonEditarRef.current?.focus();
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div
      className={`adoc-asistente-admin-cupo-fila${
        deshabilitado ? " adoc-asistente-admin-cupo-fila--deshabilitada" : ""
      }`}
    >
      <span className="adoc-asistente-admin-cupo-valor">{textoDeCupo(cupoConocido, origen)}</span>
      {origen === "override" && onRestablecer && !deshabilitado && (
        <Button
          variant="ghost"
          size="sm"
          className="adoc-asistente-admin-cupo-editar"
          aria-label={`Restablecer el cupo de ${nombre} al del rol`}
          title="Volver al cupo del rol"
          disabled={enviando}
          onClick={() => void restablecer()}
        >
          <IconoRotateCcw />
        </Button>
      )}
      <Button
        ref={botonEditarRef}
        variant="ghost"
        size="sm"
        className="adoc-asistente-admin-cupo-editar"
        aria-label={`Editar cupo diario de ${nombre}`}
        disabled={deshabilitado || enviando}
        onClick={() => {
          setValor(cupoConocido !== undefined ? String(cupoConocido) : "");
          setEditando(true);
        }}
      >
        <IconoSquarePen />
      </Button>
    </div>
  );
}

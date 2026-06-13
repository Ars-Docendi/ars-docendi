import { Button } from "@ars-docendi/ui";

interface FooterPedidoProps {
  /** Mensaje de estado de validación que se muestra a la izquierda. */
  mensaje: string;
  /** Tono del mensaje: ok (listo) o warn (falta algo). */
  tono: "ok" | "warn";
  /** Habilita la acción primaria "Enviar a revisión". */
  puedeEnviar: boolean;
  /** Deshabilita todas las acciones (estado error). */
  acciones?: boolean;
  onCancelar: () => void;
  onGuardarBorrador: () => void;
  onEnviar: () => void;
}

/**
 * Footer sticky con el estado de validación a la izquierda y las acciones a la
 * derecha. "Enviar a revisión" es la única acción primaria de la pantalla.
 */
export function FooterPedido({
  mensaje,
  tono,
  puedeEnviar,
  acciones = true,
  onCancelar,
  onGuardarBorrador,
  onEnviar,
}: FooterPedidoProps) {
  return (
    <div className="adoc-form-footer">
      <div className={`left ${tono}`}>{mensaje}</div>
      <div className="right">
        <Button variant="ghost" disabled={!acciones} onClick={onCancelar}>
          Cancelar
        </Button>
        <Button variant="secondary" disabled={!acciones} onClick={onGuardarBorrador}>
          Guardar borrador
        </Button>
        <Button variant="primary" disabled={!acciones || !puedeEnviar} onClick={onEnviar}>
          Enviar a revisión
        </Button>
      </div>
    </div>
  );
}

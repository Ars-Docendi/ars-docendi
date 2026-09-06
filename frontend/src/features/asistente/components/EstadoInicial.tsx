import { Button } from "@ars-docendi/ui";

import { AyudaDelAsistente } from "./AyudaDelAsistente";

import type { CapacidadesDelAsistente } from "../types";

interface EstadoInicialProps {
  capacidades: CapacidadesDelAsistente;
  onElegir: (pregunta: string) => void;
  deshabilitado: boolean;
}

/**
 * La pantalla vacía, armada SÓLO con el catálogo de `GET /capacidades`.
 *
 * Nada de acá se inventa en el cliente: la presentación, el alcance, los ejemplos,
 * las áreas y los límites los manda el backend, que es quien sabe qué puede
 * responder para este usuario. Los ejemplos son preguntas verificadas, así que un
 * chip es una pregunta que se sabe que funciona, y se manda tal cual.
 *
 * LA PRESENTACIÓN LA ESCRIBE EL BACKEND, y no el cliente con una tabla de roles.
 * `identity.roles` no es un catálogo cerrado —Secretaría crea roles desde la
 * aplicación— así que una lista embebida acá se desactualizaría sola, con el mismo
 * argumento por el que `useAccesoAlAsistente` le pregunta al backend en vez de
 * decidir por rol.
 *
 * LA PRESENTACIÓN, EL ALCANCE Y LOS LÍMITES VIVEN DETRÁS DEL «?». Ocupaban la mitad
 * de la pantalla y competían con lo único accionable que hay acá: los ejemplos.
 * Nadie los relee, y quien entra por primera vez tampoco los lee antes de probar.
 * Siguen a un clic porque cuando hacen falta —«¿por qué no me contestó esto?»— son
 * exactamente lo que responde. Ver `AyudaDelAsistente`.
 *
 * EL CONTEO DE ÁREAS VA CON EL ALCANCE, y por eso se calcula acá y se le pasa: es
 * la única señal de amplitud que hay. Sin él, una presentación acotada —«los pedidos
 * de tu carrera»— se leería como el techo de lo que el asistente sabe.
 *
 * DE LAS ÁREAS SÓLO SE DICE CUÁNTAS HAY. `cubre[].nombre` es `schema.tabla`
 * —«designaciones.pedidos»—, una etiqueta interna que RNF-18 prohíbe mostrar. Y
 * `cubre[].descripcion` tampoco se pinta: es el `COMMENT ON TABLE` que el backend
 * le manda al modelo en el prefijo del prompt —nombres de tablas, sinónimos del
 * dominio, «NO confundir con…»—, escrito para el modelo y no para el usuario, y
 * el cliente no tiene cómo sanearlo. Una descripción para el usuario es trabajo
 * del backend; hasta entonces, el conteo en la línea del alcance es lo único de
 * las áreas que llega a la pantalla.
 *
 * Desaparece con el primer turno: el panel deja de montarlo.
 */
export function EstadoInicial({ capacidades, onElegir, deshabilitado }: EstadoInicialProps) {
  return (
    <div className="adoc-asistente-inicio">
      <div className="adoc-asistente-inicio-entrada">
        <h2 className="adoc-asistente-inicio-titulo">¿Qué querés saber del sistema?</h2>
        <AyudaDelAsistente capacidades={capacidades} areas={areasDeDatos(capacidades.tablas)} />
      </div>

      {capacidades.ejemplos.length > 0 && (
        <ul className="adoc-asistente-chips" aria-label="Preguntas de ejemplo">
          {capacidades.ejemplos.map((ejemplo) => (
            <li key={ejemplo}>
              <Button
                variant="ghost"
                size="sm"
                disabled={deshabilitado}
                onClick={() => onElegir(ejemplo)}
              >
                {ejemplo}
              </Button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/** «1 área de datos», «2 áreas de datos». */
function areasDeDatos(cantidad: number): string {
  return cantidad === 1 ? "1 área de datos" : `${cantidad} áreas de datos`;
}

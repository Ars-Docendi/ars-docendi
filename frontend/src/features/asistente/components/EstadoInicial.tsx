import { Button } from "@ars-docendi/ui";

import type { CapacidadesDelAsistente } from "../types";

interface EstadoInicialProps {
  capacidades: CapacidadesDelAsistente;
  onElegir: (pregunta: string) => void;
  deshabilitado: boolean;
}

/**
 * La pantalla vacía, armada SÓLO con el catálogo de `GET /capacidades`.
 *
 * Nada de acá se inventa en el cliente: el alcance, los ejemplos, las áreas y los
 * límites los manda el backend, que es quien sabe qué puede responder para este
 * usuario. Los ejemplos son preguntas verificadas, así que un chip es una pregunta
 * que se sabe que funciona, y se manda tal cual.
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
      <h2 className="adoc-asistente-inicio-titulo">¿Qué querés saber del sistema?</h2>

      <p className="adoc-asistente-inicio-alcance">
        Conozco {areasDeDatos(capacidades.tablas)} del sistema. {capacidades.alcance}
      </p>

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

      {capacidades.noPuede.length > 0 && (
        // Sin límites no queda ni el rótulo.
        <div className="adoc-asistente-inicio-detalle">
          <p className="adoc-asistente-inicio-rotulo">No puedo:</p>
          <ul className="adoc-asistente-inicio-lista">
            {capacidades.noPuede.map((limite) => (
              <li key={limite}>{limite}</li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

/** «1 área de datos», «2 áreas de datos». */
function areasDeDatos(cantidad: number): string {
  return cantidad === 1 ? "1 área de datos" : `${cantidad} áreas de datos`;
}

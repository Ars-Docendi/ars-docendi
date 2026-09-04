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
 * LAS ÁREAS SE PRESENTAN POR SU DESCRIPCIÓN Y NUNCA POR SU NOMBRE. `cubre[].nombre`
 * es `schema.tabla` —«designaciones.pedidos»—, una etiqueta interna que RNF-18
 * prohíbe mostrar. La descripción es opcional: el área que no la trae no se lista,
 * y si ninguna la trae la sección desaparece entera. Nunca se cae en el nombre.
 *
 * El conteo de áreas se conserva en la línea del alcance aunque haya descripciones:
 * es lo único que queda cuando no vienen.
 *
 * Desaparece con el primer turno: el panel deja de montarlo.
 */
export function EstadoInicial({ capacidades, onElegir, deshabilitado }: EstadoInicialProps) {
  const descripciones = capacidades.cubre
    .map((area) => area.descripcion)
    .filter((descripcion): descripcion is string => Boolean(descripcion));

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

      {(descripciones.length > 0 || capacidades.noPuede.length > 0) && (
        <div className="adoc-asistente-inicio-detalle">
          <Detalle rotulo="Puedo consultar:" items={descripciones} />
          <Detalle rotulo="No puedo:" items={capacidades.noPuede} />
        </div>
      )}
    </div>
  );
}

/** Una lista compacta con su rótulo. Sin ítems no deja ni el rótulo. */
function Detalle({ rotulo, items }: { rotulo: string; items: string[] }) {
  if (items.length === 0) return null;

  return (
    <div>
      <p className="adoc-asistente-inicio-rotulo">{rotulo}</p>
      <ul className="adoc-asistente-inicio-lista">
        {items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
    </div>
  );
}

/** «1 área de datos», «2 áreas de datos». */
function areasDeDatos(cantidad: number): string {
  return cantidad === 1 ? "1 área de datos" : `${cantidad} áreas de datos`;
}

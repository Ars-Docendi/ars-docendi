import { useId } from "react";
import { InlineAlert } from "@ars-docendi/ui";

import type { PaginaDeProblemas, TipoDeProblema } from "../types";
import {
  ETIQUETAS_DE_TIPO,
  detalleDeProblema,
  formatearCantidad,
  formatearDia,
} from "../utils/calidad";

const FILTROS: { valor: TipoDeProblema | undefined; etiqueta: string }[] = [
  { valor: undefined, etiqueta: "Todas" },
  { valor: "negativa", etiqueta: "Pulgar abajo" },
  { valor: "fallo", etiqueta: "Fallos" },
  { valor: "reemplazo", etiqueta: "Reemplazadas" },
];

interface ProblemasDeCalidadProps {
  /** Whether the viewer holds `asistente.revisar_calidad`. Without it nothing is requested. */
  puedeRevisar: boolean;
  tipo: TipoDeProblema | undefined;
  onCambiarTipo: (tipo: TipoDeProblema | undefined) => void;
  pagina: PaginaDeProblemas | undefined;
  cargando: boolean;
  error: boolean;
  numeroDePagina: number;
  onCambiarPagina: (pagina: number) => void;
}

/** «Respuestas con problemas»: the anonymous list, one row per turn. */
export function ProblemasDeCalidad({
  puedeRevisar,
  tipo,
  onCambiarTipo,
  pagina,
  cargando,
  error,
  numeroDePagina,
  onCambiarPagina,
}: ProblemasDeCalidadProps) {
  const idTitulo = useId();

  if (!puedeRevisar) {
    return (
      <section className="adoc-asistente-admin-panel-uso-tarjeta" aria-labelledby={idTitulo}>
        <span id={idTitulo} className="sistema-cambios-titulo">
          Respuestas con problemas
        </span>
        <InlineAlert severity="info" title="Necesitás el permiso «Revisar calidad del asistente».">
          Los indicadores de arriba son agregados y los ve cualquier administrador del asistente; el
          detalle de cada pregunta, no.
        </InlineAlert>
      </section>
    );
  }

  const tamano = pagina?.tamanoDePagina ?? 25;
  const total = pagina?.total ?? 0;
  const ultima = Math.max(1, Math.ceil(total / tamano));
  const primeraFila = total === 0 ? 0 : (numeroDePagina - 1) * tamano + 1;
  const ultimaFila = Math.min(total, numeroDePagina * tamano);

  return (
    <section className="adoc-asistente-admin-panel-uso-tarjeta" aria-labelledby={idTitulo}>
      <div className="adoc-asistente-admin-panel-uso-encabezado">
        <span id={idTitulo} className="sistema-cambios-titulo">
          Respuestas con problemas
        </span>
        <div className="adoc-asistente-admin-periodo" role="group" aria-label="Tipo de problema">
          {FILTROS.map((f) => (
            <button
              key={f.etiqueta}
              type="button"
              aria-pressed={tipo === f.valor}
              className="adoc-asistente-admin-periodo-boton"
              onClick={() => onCambiarTipo(f.valor)}
            >
              {f.etiqueta}
            </button>
          ))}
        </div>
      </div>

      {error ? (
        <InlineAlert severity="danger" title="No se pudo cargar la lista.">
          Probá actualizar la pantalla en unos segundos.
        </InlineAlert>
      ) : cargando && !pagina ? (
        <p aria-live="polite" className="adoc-asistente-admin-panel-vacio-texto">
          Cargando las respuestas…
        </p>
      ) : pagina && pagina.filas.length === 0 ? (
        <p className="adoc-asistente-admin-panel-vacio-texto">
          No hay respuestas con este problema en el período.
        </p>
      ) : (
        pagina && (
          <>
            <div className="adoc-table-wrap">
              <table className="adoc-table">
                <thead>
                  <tr>
                    <th scope="col">Día</th>
                    <th scope="col">Pregunta</th>
                    <th scope="col">Problema</th>
                    <th scope="col">Detalle</th>
                    <th scope="col">Modelo</th>
                  </tr>
                </thead>
                <tbody>
                  {pagina.filas.map((f, i) => (
                    <tr key={`${numeroDePagina}-${i}`}>
                      <td className="calidad-nowrap">{formatearDia(f.dia)}</td>
                      <td>{f.pregunta}</td>
                      <td>
                        <span className={`calidad-tipo calidad-tipo--${f.tipo}`}>
                          {ETIQUETAS_DE_TIPO[f.tipo]}
                        </span>
                      </td>
                      <td>{detalleDeProblema(f.tipo, f.motivo)}</td>
                      <td className="calidad-mono calidad-nowrap">{f.modelo ?? "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <nav className="calidad-paginacion" aria-label="Paginación de respuestas con problemas">
              <span aria-live="polite">
                {formatearCantidad(primeraFila)}–{formatearCantidad(ultimaFila)} de{" "}
                {formatearCantidad(total)}
              </span>
              <span className="calidad-paginacion-botones">
                <button
                  type="button"
                  className="calidad-enlace"
                  disabled={numeroDePagina <= 1}
                  onClick={() => onCambiarPagina(numeroDePagina - 1)}
                >
                  Anterior
                </button>
                <span>
                  Página {numeroDePagina} de {ultima}
                </span>
                <button
                  type="button"
                  className="calidad-enlace"
                  disabled={numeroDePagina >= ultima}
                  onClick={() => onCambiarPagina(numeroDePagina + 1)}
                >
                  Siguiente
                </button>
              </span>
            </nav>
          </>
        )
      )}
      <p className="adoc-asistente-admin-panel-uso-pie">
        Sin nombre de quien preguntó y sin el comentario libre. La pregunta puede incluir nombres
        que escribió el usuario, por eso esta lista pide el permiso «Revisar calidad del asistente»
        y cada consulta queda registrada en Auditoría.
      </p>
    </section>
  );
}

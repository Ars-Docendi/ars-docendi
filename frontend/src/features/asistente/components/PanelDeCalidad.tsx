import { useState } from "react";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { InlineAlert, Select } from "@ars-docendi/ui";

import { obtenerCalidad, obtenerProblemasDeCalidad } from "../api/administracionAsistenteApi";
import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import type { PeriodoDeCalidad, TipoDeProblema } from "../types";
import { CLAVE_CALIDAD } from "../utils/claveDeCalidad";
import {
  PERIODOS_DE_CALIDAD,
  PERMISO_REVISAR_CALIDAD,
  etiquetaDeCausa,
  etiquetaDeMotivo,
} from "../utils/calidad";
import { BarrasDeConteo } from "./BarrasDeConteo";
import { GraficoDeNegativas } from "./GraficoDeNegativas";
import { KpisDeCalidad } from "./KpisDeCalidad";
import { ProblemasDeCalidad } from "./ProblemasDeCalidad";
import { TablaPorModelo } from "./TablaPorModelo";
import "../asistente.css";

/**
 * The «Calidad» tab of Sistema (asistente-metricas-de-calidad): how users rate
 * the answers and where the system visibly fails, per period and per model.
 *
 * Only holders of `asistente.administrar` reach this component (the tab is
 * gated by `SistemaPage`). The anonymous problem list needs a second
 * permission, `asistente.revisar_calidad`; without it the list is never
 * requested and a notice is shown instead.
 */
export function PanelDeCalidad() {
  const { user } = useCurrentUser();
  const puedeRevisar = user?.permissions.includes(PERMISO_REVISAR_CALIDAD) ?? false;

  const [periodo, setPeriodo] = useState<PeriodoDeCalidad>(30);
  const [modelo, setModelo] = useState("");
  const [tipo, setTipo] = useState<TipoDeProblema | undefined>(undefined);
  const [numeroDePagina, setNumeroDePagina] = useState(1);

  const calidad = useQuery({
    queryKey: [...CLAVE_CALIDAD, "agregados", periodo, modelo],
    queryFn: () => obtenerCalidad(periodo, modelo || undefined),
    placeholderData: keepPreviousData,
  });

  const problemas = useQuery({
    queryKey: [...CLAVE_CALIDAD, "problemas", periodo, modelo, tipo ?? "", numeroDePagina],
    queryFn: () => obtenerProblemasDeCalidad(periodo, numeroDePagina, modelo || undefined, tipo),
    enabled: puedeRevisar,
    placeholderData: keepPreviousData,
  });

  // Any filter change goes back to the first page.
  function cambiarPeriodo(siguiente: PeriodoDeCalidad) {
    setPeriodo(siguiente);
    setNumeroDePagina(1);
  }
  function cambiarModelo(siguiente: string) {
    setModelo(siguiente);
    setNumeroDePagina(1);
  }
  function cambiarTipo(siguiente: TipoDeProblema | undefined) {
    setTipo(siguiente);
    setNumeroDePagina(1);
  }

  const datos = calidad.data;
  const modelos = datos?.modelosDisponibles ?? [];
  // A model chosen earlier can be missing from the new period: keep it selectable.
  const opciones = modelo && !modelos.includes(modelo) ? [modelo, ...modelos] : modelos;

  return (
    <div className="adoc-asistente-administracion">
      <div className="adoc-asistente-admin-encabezado">
        <div>
          <h2 className="adoc-asistente-admin-titulo">Calidad de las respuestas</h2>
          <p className="adoc-asistente-admin-copete">
            Valoraciones de los usuarios y fallos que el sistema detecta solo. Datos anónimos: nunca
            se cruzan con quién preguntó.
          </p>
        </div>
        <div className="adoc-asistente-admin-encabezado-acciones">
          <span className="calidad-selector-modelo">
            <Select
              aria-label="Modelo"
              value={modelo}
              onChange={(e) => cambiarModelo(e.target.value)}
              className="calidad-mono"
            >
              <option value="">Todos los modelos</option>
              {opciones.map((m) => (
                <option key={m} value={m}>
                  {m}
                </option>
              ))}
            </Select>
          </span>
          <div className="adoc-asistente-admin-periodo" role="group" aria-label="Período">
            {PERIODOS_DE_CALIDAD.map((p) => (
              <button
                key={p.valor}
                type="button"
                aria-pressed={periodo === p.valor}
                className="adoc-asistente-admin-periodo-boton"
                onClick={() => cambiarPeriodo(p.valor)}
              >
                {p.etiqueta}
              </button>
            ))}
          </div>
        </div>
      </div>

      {calidad.isError && !datos ? (
        <InlineAlert severity="danger" title="No se pudieron cargar las métricas de calidad.">
          Probá actualizar la pantalla en unos segundos.
        </InlineAlert>
      ) : !datos ? (
        <p aria-live="polite" className="adoc-asistente-admin-panel-vacio-texto">
          Cargando la calidad de las respuestas…
        </p>
      ) : (
        <>
          <KpisDeCalidad calidad={datos} />

          <div className="calidad-fila calidad-fila--grafico">
            <GraficoDeNegativas calidad={datos} />
            <BarrasDeConteo
              titulo="Por qué no sirvió"
              conteos={datos.negativosPorMotivo}
              etiquetaDe={etiquetaDeMotivo}
              vacio="Nadie marcó «No sirvió» en este período."
              nota="Un voto puede tener varios motivos: los porcentajes se calculan sobre los motivos marcados."
            />
          </div>

          <div className="calidad-fila calidad-fila--causas">
            <BarrasDeConteo
              titulo="Fallos por causa"
              conteos={datos.fallosPorCausa}
              etiquetaDe={etiquetaDeCausa}
              vacio="No se detectaron fallos en este período."
              marcarSenales
            />
            <TablaPorModelo filas={datos.porModelo} />
          </div>

          <BarrasDeConteo
            titulo="Denegaciones de política"
            conteos={datos.denegacionesDePolitica}
            etiquetaDe={etiquetaDeCausa}
            vacio="No hubo denegaciones en este período."
            nota="Cupo, tope, mantenimiento o acceso: dicen algo de la política, no del modelo, así que no entran en ninguna tasa de arriba."
          />
        </>
      )}

      <ProblemasDeCalidad
        puedeRevisar={puedeRevisar}
        tipo={tipo}
        onCambiarTipo={cambiarTipo}
        pagina={problemas.data}
        cargando={problemas.isLoading}
        error={problemas.isError}
        numeroDePagina={numeroDePagina}
        onCambiarPagina={setNumeroDePagina}
      />
    </div>
  );
}

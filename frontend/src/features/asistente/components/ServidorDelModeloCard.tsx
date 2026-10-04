import type { ServidorLocal } from "../types";

interface ServidorDelModeloCardProps {
  servidor: ServidorLocal | undefined;
}

/** «42 %», o «—» si el servidor no publica la métrica. */
function porcentaje(valor: number | null): string {
  return valor === null ? "—" : `${Math.round(valor * 100)} %`;
}

/** «3», o «—» si no se sabe: cero es un dato, «no sé» es otro. */
function cantidad(valor: number | null): string {
  return valor === null ? "—" : String(valor);
}

/**
 * La carga del servidor del modelo propio (asistente-optimizaciones-modelo-local,
 * D8): turnos en curso y en espera, KV cache y caché de prefijo según su
 * `/metrics`, y la compuerta del backend que le reparte la GPU.
 *
 * Sólo aparece con proveedor `local`: con un proveedor en la nube no hay
 * servidor propio que mirar. Una métrica que el servidor no publica se muestra
 * «—» y no «0», para no decir que la GPU está libre cuando nadie lo midió.
 */
export function ServidorDelModeloCard({ servidor }: ServidorDelModeloCardProps) {
  if (!servidor?.configurado) return null;

  return (
    <section
      aria-label="Servidor del modelo"
      className="adoc-asistente-admin-kpi adoc-asistente-admin-servidor"
    >
      <span className="adoc-asistente-admin-kpi-etiqueta">
        Servidor del modelo{servidor.motor ? ` · ${servidor.motor}` : ""}
      </span>

      {!servidor.alcanzable ? (
        <p className="adoc-asistente-admin-servidor-aviso">
          No responde sus métricas. Revisá que el servidor esté levantado.
        </p>
      ) : (
        <dl className="adoc-asistente-admin-servidor-metricas">
          <div>
            <dt>En curso</dt>
            <dd>{cantidad(servidor.enCurso)}</dd>
          </div>
          <div>
            <dt>En espera</dt>
            <dd>{cantidad(servidor.enEspera)}</dd>
          </div>
          <div>
            <dt>KV cache</dt>
            <dd>{porcentaje(servidor.usoDeKvCache)}</dd>
          </div>
          <div>
            <dt>Caché de prefijo</dt>
            <dd>{porcentaje(servidor.aciertosDeCacheDePrefijo)}</dd>
          </div>
        </dl>
      )}

      {servidor.compuerta && (
        <p className="adoc-asistente-admin-servidor-compuerta">
          Compuerta del backend: {servidor.compuerta.enCurso} de {servidor.compuerta.capacidad} en
          curso, {servidor.compuerta.enEspera} en cola.
        </p>
      )}
    </section>
  );
}

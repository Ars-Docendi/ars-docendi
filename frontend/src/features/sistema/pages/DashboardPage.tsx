import { useQuery } from "@tanstack/react-query";

import { Button } from "@ars-docendi/ui";

import { PageHeader } from "../../../shared/ui/PageHeader";
import { consultarEstadoSistema } from "../api/sistemaApi";
import type { ComprobacionComponente } from "../api/sistemaApi";
import "../sistema.css";

function fechaLocal(valor: string): string {
  const fecha = new Date(valor);
  if (!Number.isFinite(fecha.getTime())) return "Hora no disponible";
  return fecha.toLocaleString("es-AR", { dateStyle: "short", timeStyle: "medium" });
}

function duracion(valor: number): string {
  return Number.isFinite(valor) && valor >= 0 ? `${Math.round(valor)} ms` : "—";
}

function EtiquetaEstado({ estado }: { estado: ComprobacionComponente["estado"] }) {
  const etiqueta =
    estado === "disponible"
      ? "Disponible"
      : estado === "no_disponible"
        ? "No disponible"
        : "Desconocido";
  const clase =
    estado === "disponible"
      ? "sistema-estado--ok"
      : estado === "no_disponible"
        ? "sistema-estado--error"
        : "sistema-estado--desconocido";
  return <span className={`sistema-estado ${clase}`}>{etiqueta}</span>;
}

export function DashboardPage() {
  const consulta = useQuery({
    queryKey: ["administracion", "sistema", "estado"],
    queryFn: consultarEstadoSistema,
    refetchOnWindowFocus: false,
  });
  const componentes = consulta.data ? [...consulta.data.modulos, consulta.data.baseDatos] : [];

  return (
    <main className="administracion-sistema">
      <PageHeader
        title="Dashboard del sistema"
        meta="Estado actual de los servicios y la base de datos"
        actions={
          <Button
            variant="secondary"
            type="button"
            onClick={() => void consulta.refetch()}
            disabled={consulta.isFetching}
            loading={consulta.isFetching}
          >
            {consulta.isFetching ? "Actualizando…" : "Actualizar"}
          </Button>
        }
      />

      {consulta.isPending && <p role="status">Consultando el estado actual…</p>}
      {consulta.isError && (
        <p role="alert">
          No se pudo cargar el estado del sistema.{" "}
          <Button variant="secondary" onClick={() => void consulta.refetch()}>
            Reintentar
          </Button>
        </p>
      )}
      {consulta.data && (
        <section className="sistema-panel" aria-labelledby="sistema-componentes-titulo">
          <header className="sistema-panel-encabezado">
            <h2 id="sistema-componentes-titulo">Estado por componente</h2>
            <p className="sistema-nota">
              Las sondas de módulos verifican su respuesta HTTP; PostgreSQL se comprueba por
              separado. Cada resultado se informa de forma independiente.
            </p>
          </header>
          <div className="sistema-tabla-contenedor">
            <table className="sistema-tabla" aria-label="Estado de módulos y base de datos">
              <thead>
                <tr>
                  <th scope="col">Componente</th>
                  <th scope="col">Estado</th>
                  <th scope="col">Duración</th>
                  <th scope="col">Última comprobación</th>
                </tr>
              </thead>
              <tbody>
                {componentes.map((componente) => (
                  <tr key={componente.id}>
                    <th scope="row">{componente.nombre}</th>
                    <td>
                      <EtiquetaEstado estado={componente.estado} />
                    </td>
                    <td>{duracion(componente.duracionMs)}</td>
                    <td>
                      <time dateTime={componente.comprobadoEn}>
                        {fechaLocal(componente.comprobadoEn)}
                      </time>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </main>
  );
}

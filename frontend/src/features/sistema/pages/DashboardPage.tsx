import { useQuery } from "@tanstack/react-query";

import { Button } from "@ars-docendi/ui";

import { ContenedorTabla } from "../../../shared/ui/ContenedorTabla";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { consultarEstadoSistema } from "../api/sistemaApi";
import type { ComprobacionComponente } from "../api/sistemaApi";
import { usePantallaTelefono } from "../../../shared/ui/usePantallaTelefono";
import "../../../shared/ui/ListaTarjetas.css";
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
        : estado === "deshabilitado"
          ? "Deshabilitado"
          : "Desconocido";
  const clase =
    estado === "disponible"
      ? "sistema-estado--ok"
      : estado === "no_disponible"
        ? "sistema-estado--error"
        : estado === "deshabilitado"
          ? "sistema-estado--deshabilitado"
          : "sistema-estado--desconocido";
  return <span className={`sistema-estado ${clase}`}>{etiqueta}</span>;
}

export function DashboardPage() {
  const telefono = usePantallaTelefono();
  const consulta = useQuery({
    queryKey: ["administracion", "sistema", "estado"],
    queryFn: consultarEstadoSistema,
    refetchOnWindowFocus: false,
  });
  const componentes = consulta.data
    ? [...consulta.data.modulos, consulta.data.baseDatos, consulta.data.inferencia]
    : [];

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
              Las sondas de módulos verifican su respuesta HTTP; PostgreSQL y la inferencia se
              comprueban desde el backend. Cada resultado se informa de forma independiente; la
              inferencia figura como deshabilitada cuando el ambiente no la tiene configurada.
            </p>
          </header>
          {telefono ? (
            <ul
              className="adoc-lista-tarjetas"
              aria-label="Estado de módulos, base de datos e inferencia"
            >
              {componentes.map((componente) => (
                <li key={componente.id} className="adoc-tarjeta">
                  <strong>{componente.nombre}</strong>
                  <div className="adoc-tarjeta-meta">
                    {duracion(componente.duracionMs)} ·{" "}
                    <time dateTime={componente.comprobadoEn}>
                      {fechaLocal(componente.comprobadoEn)}
                    </time>
                  </div>
                  <div className="adoc-tarjeta-insignias">
                    <EtiquetaEstado estado={componente.estado} />
                  </div>
                </li>
              ))}
            </ul>
          ) : (
            <ContenedorTabla etiqueta="Estado de módulos, base de datos e inferencia">
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
            </ContenedorTabla>
          )}
        </section>
      )}
    </main>
  );
}

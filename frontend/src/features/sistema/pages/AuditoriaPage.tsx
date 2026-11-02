import { useMemo, useState, type FormEvent } from "react";
import { useQuery } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { Button } from "@ars-docendi/ui";

import { CampoFechaISO } from "../../../shared/ui/CampoFechaISO";
import { CampoHora } from "../../../shared/ui/CampoHora";
import { horaTextoValida } from "../../../shared/ui/camposFechaHora";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { listarAuditoria } from "../api/sistemaApi";
import type { CambioAuditoria, FiltrosAuditoria } from "../api/sistemaApi";
import "../sistema.css";

interface FormularioFiltros {
  /** Fecha ISO `aaaa-mm-dd`. */
  desde: string;
  /** `hh:mm`; vacío = 00:00. */
  desdeHora: string;
  hasta: string;
  /** `hh:mm`; vacío = 23:59. */
  hastaHora: string;
  accion: string;
  schema: string;
  tabla: string;
  actor: string;
  rowPk: string;
}

const formularioVacio: FormularioFiltros = {
  desde: "",
  desdeHora: "",
  hasta: "",
  hastaHora: "",
  accion: "",
  schema: "",
  tabla: "",
  actor: "",
  rowPk: "",
};
const tamanoPagina = 50;
const schemasPorEtiqueta: Record<string, string> = {
  identidad: "identity",
  designaciones: "designaciones",
  portal: "portal",
};

/** Fecha ISO + hora `hh:mm` (hora local) + segundos a instante ISO; sin fecha no hay filtro. */
function aIso(
  fecha: string,
  hora: string,
  horaPorDefecto: string,
  segundos = "00",
): string | undefined {
  if (!fecha) return undefined;
  const instante = new Date(`${fecha}T${horaTextoValida(hora) ?? horaPorDefecto}:${segundos}`);
  return Number.isNaN(instante.getTime()) ? undefined : instante.toISOString();
}

const CAMPOS_URL = Object.keys(formularioVacio) as (keyof FormularioFiltros)[];

/** Filtros aplicados, leídos del query: recargar o volver restaura la consulta. */
function filtrosDeLaURL(params: URLSearchParams) {
  const aplicados = { ...formularioVacio };
  for (const campo of CAMPOS_URL) aplicados[campo] = params.get(campo) ?? "";
  const pagina = Math.max(1, Number.parseInt(params.get("pagina") ?? "1", 10) || 1);
  return { aplicados, pagina };
}

function normalizarSchemaFiltro(valor: string): string {
  const texto = valor.trim();
  return schemasPorEtiqueta[texto.toLocaleLowerCase("es-AR")] ?? texto.toLowerCase();
}

/** Parámetros de la API a partir de los filtros aplicados. */
function aFiltros(aplicados: FormularioFiltros, pagina: number): FiltrosAuditoria {
  const filtros: FiltrosAuditoria = { pagina, tamanoPagina };
  const desde = aIso(aplicados.desde, aplicados.desdeHora, "00:00");
  const hasta = aIso(aplicados.hasta, aplicados.hastaHora, "23:59", "59.999");
  if (desde) filtros.desde = desde;
  if (hasta) filtros.hasta = hasta;
  if (aplicados.accion) filtros.accion = aplicados.accion;
  if (aplicados.schema.trim()) filtros.schema = normalizarSchemaFiltro(aplicados.schema);
  if (aplicados.tabla.trim()) filtros.tabla = aplicados.tabla.trim();
  if (aplicados.actor.trim()) filtros.actor = aplicados.actor.trim();
  if (aplicados.rowPk.trim()) filtros.rowPk = aplicados.rowPk.trim();
  return filtros;
}

function mostrarValor(cambio: CambioAuditoria, lado: "anterior" | "nuevo"): string {
  if (cambio.oculto) return "Enmascarado por política";
  const valor = lado === "anterior" ? cambio.valorAnterior : cambio.valorNuevo;
  return valor ?? "—";
}

function fechaLocal(valor: string): string {
  return new Date(valor).toLocaleString("es-AR", { dateStyle: "short", timeStyle: "medium" });
}

export function AuditoriaPage() {
  // Los filtros aplicados y la página viven en el query; el formulario es un borrador.
  const [params, setParams] = useSearchParams();
  const lectura = params.toString();
  const { aplicados, pagina: paginaActual } = useMemo(
    () => filtrosDeLaURL(new URLSearchParams(lectura)),
    [lectura],
  );
  const [formulario, setFormulario] = useState(aplicados);
  const [lecturaPrevia, setLecturaPrevia] = useState(lectura);
  if (lectura !== lecturaPrevia) {
    // Atrás/Adelante: el borrador vuelve a lo que muestra la URL.
    setLecturaPrevia(lectura);
    setFormulario(aplicados);
  }
  const filtros = useMemo(() => aFiltros(aplicados, paginaActual), [aplicados, paginaActual]);
  const [detallesAbiertos, setDetallesAbiertos] = useState<Set<number>>(() => new Set());
  const consulta = useQuery({
    queryKey: ["administracion", "auditoria", filtros],
    queryFn: () => listarAuditoria(filtros),
    refetchOnWindowFocus: false,
  });
  const pagina = consulta.data;
  const totalPaginas = pagina ? Math.max(1, Math.ceil(pagina.total / pagina.tamanoPagina)) : 1;

  function buscar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    const siguientes = new URLSearchParams();
    for (const campo of CAMPOS_URL) {
      if (formulario[campo].trim()) siguientes.set(campo, formulario[campo].trim());
    }
    setDetallesAbiertos(new Set());
    setParams(siguientes, { replace: true });
  }

  function limpiar() {
    setFormulario(formularioVacio);
    setDetallesAbiertos(new Set());
    setParams(new URLSearchParams(), { replace: true });
  }

  function cambiarPagina(paginaNueva: number) {
    setDetallesAbiertos(new Set());
    setParams(
      (previos) => {
        const siguientes = new URLSearchParams(previos);
        if (paginaNueva <= 1) siguientes.delete("pagina");
        else siguientes.set("pagina", String(paginaNueva));
        return siguientes;
      },
      { replace: true },
    );
  }

  function alternarDetalle(id: number) {
    setDetallesAbiertos((actuales) => {
      const nuevos = new Set(actuales);
      if (nuevos.has(id)) nuevos.delete(id);
      else nuevos.add(id);
      return nuevos;
    });
  }

  return (
    <main className="administracion-sistema">
      <PageHeader
        title="Registros de auditoría"
        meta={pagina ? `${pagina.total} registros` : "Consulta de solo lectura"}
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

      <form className="auditoria-filtros" onSubmit={buscar}>
        <label>
          Desde
          <CampoFechaISO
            aria-label="Desde"
            value={formulario.desde}
            onChange={(desde) => setFormulario({ ...formulario, desde })}
          />
        </label>
        <label>
          Hora desde
          <CampoHora
            value={formulario.desdeHora}
            onChange={(desdeHora) => setFormulario({ ...formulario, desdeHora })}
          />
        </label>
        <label>
          Hasta
          <CampoFechaISO
            aria-label="Hasta"
            value={formulario.hasta}
            onChange={(hasta) => setFormulario({ ...formulario, hasta })}
          />
        </label>
        <label>
          Hora hasta
          <CampoHora
            value={formulario.hastaHora}
            onChange={(hastaHora) => setFormulario({ ...formulario, hastaHora })}
          />
        </label>
        <label>
          Acción
          <select
            aria-label="Acción"
            value={formulario.accion}
            onChange={(e) => setFormulario({ ...formulario, accion: e.target.value })}
          >
            <option value="">Todas</option>
            <option value="INSERT">Alta</option>
            <option value="UPDATE">Actualización</option>
            <option value="DELETE">Eliminación física</option>
          </select>
        </label>
        <label>
          Módulo o schema
          <input
            aria-label="Módulo o schema"
            placeholder="Identidad o schema exacto"
            value={formulario.schema}
            onChange={(e) => setFormulario({ ...formulario, schema: e.target.value })}
          />
        </label>
        <label>
          Tabla
          <input
            aria-label="Tabla"
            value={formulario.tabla}
            onChange={(e) => setFormulario({ ...formulario, tabla: e.target.value })}
          />
        </label>
        <label>
          Actor
          <input
            aria-label="Actor"
            placeholder="Buscar por nombre"
            value={formulario.actor}
            onChange={(e) => setFormulario({ ...formulario, actor: e.target.value })}
          />
        </label>
        <label>
          Clave de fila
          <input
            aria-label="Clave de fila"
            aria-describedby="auditoria-clave-ayuda"
            value={formulario.rowPk}
            onChange={(e) => setFormulario({ ...formulario, rowPk: e.target.value })}
          />
          <small id="auditoria-clave-ayuda">
            Usá el JSON completo para una relación precisa. Una clave parcial legada consulta una
            agrupación histórica, no una relación individual.
          </small>
        </label>
        <div className="auditoria-filtros-acciones">
          <Button variant="primary" type="submit">
            Buscar
          </Button>
          <Button variant="secondary" type="button" onClick={limpiar}>
            Limpiar
          </Button>
        </div>
      </form>

      {consulta.isPending && <p role="status">Cargando registros de auditoría…</p>}
      {consulta.isError && (
        <p role="alert">
          No se pudieron cargar los registros.{" "}
          <Button variant="secondary" onClick={() => void consulta.refetch()}>
            Reintentar
          </Button>
        </p>
      )}
      {pagina && (
        <>
          <div className="sistema-tabla-contenedor">
            <table className="sistema-tabla" aria-label="Registros de auditoría">
              <thead>
                <tr>
                  <th scope="col">Fecha</th>
                  <th scope="col">Usuario</th>
                  <th scope="col">Acción</th>
                  <th scope="col">Módulo</th>
                  <th scope="col">Cambio</th>
                </tr>
              </thead>
              <tbody>
                {pagina.elementos.map((registro) => {
                  const abierto = detallesAbiertos.has(registro.id);
                  return (
                    <tr key={registro.id}>
                      <td>
                        <time dateTime={registro.cambiadoEn}>
                          {fechaLocal(registro.cambiadoEn)}
                        </time>
                      </td>
                      <td>{registro.actor}</td>
                      <td>
                        <span className="auditoria-accion">{registro.accionEtiqueta}</span>
                      </td>
                      <td>{registro.modulo}</td>
                      <td>
                        <strong>{registro.resumen}</strong>
                        <div className="auditoria-contexto">
                          <span>{registro.objeto}</span>
                          <span aria-hidden="true">·</span>
                          <span>{registro.tabla}</span>
                          <Button
                            variant="ghost"
                            size="sm"
                            type="button"
                            aria-expanded={abierto}
                            onClick={() => alternarDetalle(registro.id)}
                          >
                            {abierto ? "Ocultar detalle" : "Ver detalle"}
                          </Button>
                        </div>
                        {abierto && (
                          <section
                            className="auditoria-detalle"
                            aria-label={`Detalle del evento ${registro.id}`}
                          >
                            <dl className="auditoria-metadatos">
                              <div>
                                <dt>Tabla</dt>
                                <dd>
                                  {registro.schema}.{registro.tabla}
                                </dd>
                              </div>
                              <div>
                                <dt>Clave de fila</dt>
                                <dd>{registro.rowPk}</dd>
                              </div>
                              <div>
                                <dt>Solicitud</dt>
                                <dd>{registro.requestId ?? "—"}</dd>
                              </div>
                            </dl>
                            {registro.cambios.length > 0 && (
                              <div className="auditoria-detalle-tabla">
                                <table aria-label={`Cambios del registro ${registro.id}`}>
                                  <thead>
                                    <tr>
                                      <th scope="col">Campo</th>
                                      <th scope="col">Anterior</th>
                                      <th scope="col">Nuevo</th>
                                    </tr>
                                  </thead>
                                  <tbody>
                                    {registro.cambios.map((cambio) => (
                                      <tr key={cambio.campo}>
                                        <th scope="row">{cambio.etiquetaCampo}</th>
                                        <td>{mostrarValor(cambio, "anterior")}</td>
                                        <td>{mostrarValor(cambio, "nuevo")}</td>
                                      </tr>
                                    ))}
                                  </tbody>
                                </table>
                              </div>
                            )}
                          </section>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          {pagina.elementos.length === 0 && <p>No hay registros para los filtros seleccionados.</p>}
          <nav className="auditoria-paginacion" aria-label="Paginación de auditoría">
            <Button
              variant="secondary"
              type="button"
              onClick={() => cambiarPagina(filtros.pagina - 1)}
              disabled={filtros.pagina <= 1}
            >
              Anterior
            </Button>
            <span>
              Página {filtros.pagina} de {totalPaginas}
            </span>
            <Button
              variant="secondary"
              type="button"
              onClick={() => cambiarPagina(filtros.pagina + 1)}
              disabled={filtros.pagina >= totalPaginas}
            >
              Siguiente
            </Button>
          </nav>
        </>
      )}
    </main>
  );
}

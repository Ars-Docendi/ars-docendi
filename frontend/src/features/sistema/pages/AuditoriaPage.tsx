import { useState, type FormEvent } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button } from "@ars-docendi/ui";

import { PageHeader } from "../../../shared/ui/PageHeader";
import { listarAuditoria } from "../api/sistemaApi";
import type { CambioAuditoria, FiltrosAuditoria } from "../api/sistemaApi";
import "../sistema.css";

interface FormularioFiltros {
  desde: string;
  hasta: string;
  accion: string;
  schema: string;
  tabla: string;
  actor: string;
  rowPk: string;
}

const formularioVacio: FormularioFiltros = {
  desde: "",
  hasta: "",
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

function aIso(fecha: string): string | undefined {
  return fecha ? new Date(fecha).toISOString() : undefined;
}

function normalizarSchemaFiltro(valor: string): string {
  const texto = valor.trim();
  return schemasPorEtiqueta[texto.toLocaleLowerCase("es-AR")] ?? texto.toLowerCase();
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
  const [formulario, setFormulario] = useState(formularioVacio);
  const [filtros, setFiltros] = useState<FiltrosAuditoria>({ pagina: 1, tamanoPagina });
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
    const nuevos: FiltrosAuditoria = { pagina: 1, tamanoPagina };
    const desde = aIso(formulario.desde);
    const hasta = aIso(formulario.hasta);
    if (desde) nuevos.desde = desde;
    if (hasta) nuevos.hasta = hasta;
    if (formulario.accion) nuevos.accion = formulario.accion;
    if (formulario.schema.trim()) nuevos.schema = normalizarSchemaFiltro(formulario.schema);
    if (formulario.tabla.trim()) nuevos.tabla = formulario.tabla.trim();
    if (formulario.actor.trim()) nuevos.actor = formulario.actor.trim();
    if (formulario.rowPk.trim()) nuevos.rowPk = formulario.rowPk.trim();
    setDetallesAbiertos(new Set());
    setFiltros(nuevos);
  }

  function limpiar() {
    setFormulario(formularioVacio);
    setDetallesAbiertos(new Set());
    setFiltros({ pagina: 1, tamanoPagina });
  }

  function cambiarPagina(paginaNueva: number) {
    setDetallesAbiertos(new Set());
    setFiltros((actuales) => ({ ...actuales, pagina: paginaNueva }));
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
          <input
            aria-label="Desde"
            type="datetime-local"
            value={formulario.desde}
            onChange={(e) => setFormulario({ ...formulario, desde: e.target.value })}
          />
        </label>
        <label>
          Hasta
          <input
            aria-label="Hasta"
            type="datetime-local"
            value={formulario.hasta}
            onChange={(e) => setFormulario({ ...formulario, hasta: e.target.value })}
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
            value={formulario.rowPk}
            onChange={(e) => setFormulario({ ...formulario, rowPk: e.target.value })}
          />
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

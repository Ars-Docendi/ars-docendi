import { useState, type FormEvent } from "react";
import { useQuery } from "@tanstack/react-query";

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
  cambiadoPor: string;
  rowPk: string;
}

const formularioVacio: FormularioFiltros = {
  desde: "",
  hasta: "",
  accion: "",
  schema: "",
  tabla: "",
  cambiadoPor: "",
  rowPk: "",
};
const tamanoPagina = 50;

function aIso(fecha: string): string | undefined {
  return fecha ? new Date(fecha).toISOString() : undefined;
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
    if (formulario.schema.trim()) nuevos.schema = formulario.schema.trim();
    if (formulario.tabla.trim()) nuevos.tabla = formulario.tabla.trim();
    if (formulario.cambiadoPor.trim()) nuevos.cambiadoPor = formulario.cambiadoPor.trim();
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
          <button
            className="sistema-accion"
            type="button"
            onClick={() => void consulta.refetch()}
            disabled={consulta.isFetching}
          >
            Actualizar
          </button>
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
            <option value="INSERT">INSERT</option>
            <option value="UPDATE">UPDATE</option>
            <option value="DELETE">DELETE</option>
          </select>
        </label>
        <label>
          Schema
          <input
            aria-label="Schema"
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
          Actor (UUID)
          <input
            aria-label="Actor (UUID)"
            value={formulario.cambiadoPor}
            onChange={(e) => setFormulario({ ...formulario, cambiadoPor: e.target.value })}
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
          <button className="sistema-accion" type="submit">
            Buscar
          </button>
          <button
            className="sistema-accion sistema-accion--secundaria"
            type="button"
            onClick={limpiar}
          >
            Limpiar
          </button>
        </div>
      </form>

      {consulta.isPending && <p role="status">Cargando registros de auditoría…</p>}
      {consulta.isError && (
        <p role="alert">
          No se pudieron cargar los registros.{" "}
          <button type="button" onClick={() => void consulta.refetch()}>
            Reintentar
          </button>
        </p>
      )}
      {pagina && (
        <>
          <div className="sistema-tabla-contenedor">
            <table className="sistema-tabla" aria-label="Registros de auditoría">
              <thead>
                <tr>
                  <th scope="col">Fecha</th>
                  <th scope="col">Acción</th>
                  <th scope="col">Objeto</th>
                  <th scope="col">Clave de fila</th>
                  <th scope="col">Actor</th>
                  <th scope="col">Campos modificados</th>
                  <th scope="col">Detalle</th>
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
                      <td>{registro.accion}</td>
                      <td>
                        {registro.schema}.{registro.tabla}
                      </td>
                      <td>{registro.rowPk}</td>
                      <td>{registro.cambiadoPor ?? "Sin actor identificado"}</td>
                      <td>
                        {registro.columnasCambiadas.length
                          ? registro.columnasCambiadas.join(", ")
                          : "—"}
                      </td>
                      <td>
                        {registro.cambios.length > 0 ? (
                          <button
                            className="sistema-enlace"
                            type="button"
                            aria-expanded={abierto}
                            onClick={() => alternarDetalle(registro.id)}
                          >
                            {abierto ? "Ocultar detalle" : "Ver detalle"}
                          </button>
                        ) : (
                          "—"
                        )}
                        {abierto && (
                          <div className="auditoria-detalle">
                            <table aria-label={`Cambios del registro ${registro.id}`}>
                              <thead>
                                <tr>
                                  <th>Campo</th>
                                  <th>Anterior</th>
                                  <th>Nuevo</th>
                                </tr>
                              </thead>
                              <tbody>
                                {registro.cambios.map((cambio) => (
                                  <tr key={cambio.campo}>
                                    <th scope="row">{cambio.campo}</th>
                                    <td>{mostrarValor(cambio, "anterior")}</td>
                                    <td>{mostrarValor(cambio, "nuevo")}</td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </div>
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
            <button
              className="sistema-accion sistema-accion--secundaria"
              type="button"
              onClick={() => cambiarPagina(filtros.pagina - 1)}
              disabled={filtros.pagina <= 1}
            >
              Anterior
            </button>
            <span>
              Página {filtros.pagina} de {totalPaginas}
            </span>
            <button
              className="sistema-accion sistema-accion--secundaria"
              type="button"
              onClick={() => cambiarPagina(filtros.pagina + 1)}
              disabled={filtros.pagina >= totalPaginas}
            >
              Siguiente
            </button>
          </nav>
        </>
      )}
    </main>
  );
}

import { useState } from "react";
import { Button, InlineAlert, Select } from "@ars-docendi/ui";
import { useFiltrosURL } from "../../../shared/navegacion/estadoURL";
import { Migas } from "../../../shared/navegacion/Migas";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import type { PeriodoImportado, ResultadoImportacion } from "../api/comisionesApi";
import { ModalImportarComisiones } from "../components/ModalImportarComisiones";
import { TablaComisiones } from "../components/TablaComisiones";
import {
  aplicarFiltrosComisiones,
  FILTROS_COMISIONES_INICIALES,
  formatearFechaHora,
} from "../components/filtrosComisiones";
import {
  useComisionesDelPeriodo,
  useExportarComisiones,
  usePeriodosImportados,
} from "../hooks/useComisiones";

const SELECCION_POR_DEFECTO = { anio: "", periodo: "" };

/** Comisiones importadas desde Guaraní; consultarlas nunca contacta a Guaraní. */
export function ComisionesPage() {
  const { user } = useCurrentUser();
  const puedeImportar = user?.permissions.includes("comisiones.importar") ?? false;
  const periodos = usePeriodosImportados();
  const [seleccion, setSeleccion] = useFiltrosURL(SELECCION_POR_DEFECTO);
  const [modalAbierto, setModalAbierto] = useState(false);
  const [claveModal, setClaveModal] = useState(0);
  const [ultimoResultado, setUltimoResultado] = useState<ResultadoImportacion | undefined>();

  const lista = periodos.data ?? [];
  // La lista llega del más reciente al más antiguo; una selección de la URL que ya no está
  // importada cae en el período más reciente del año o, sin ese año, en el más reciente de todos.
  const delAnioElegido = lista.filter((p) => String(p.anioAcademico) === seleccion.anio);
  const periodo =
    delAnioElegido.find((p) => p.id === seleccion.periodo) ??
    delAnioElegido[0] ??
    lista.find((p) => p.id === seleccion.periodo) ??
    lista[0];
  const anios = [...new Set(lista.map((p) => p.anioAcademico))].sort((a, b) => b - a);
  const delAnio = lista.filter((p) => p.anioAcademico === periodo?.anioAcademico);
  const comisiones = useComisionesDelPeriodo(periodo?.id);
  const filas = comisiones.data ?? [];
  const [filtros, setFiltros] = useFiltrosURL(FILTROS_COMISIONES_INICIALES);
  const visibles = aplicarFiltrosComisiones(filas, filtros);
  const exportar = useExportarComisiones();

  function exportarVisibles() {
    if (!periodo) return;
    exportar.mutate(
      { periodoId: periodo.id, comisionIds: visibles.map((c) => c.id) },
      { onSuccess: ({ archivo, nombre }) => descargar(archivo, nombre) },
    );
  }

  function abrirModal() {
    setClaveModal((c) => c + 1);
    setModalAbierto(true);
  }

  function alImportar(resultado: ResultadoImportacion) {
    setUltimoResultado(resultado);
    setSeleccion({
      anio: String(resultado.anioAcademico),
      periodo: resultado.periodoLectivoId,
    });
  }

  return (
    <>
      <Migas items={[{ label: "Inicio", href: "/" }, { label: "Comisiones" }]} />
      <PageHeader
        title="Comisiones"
        meta={periodo ? `${filas.length} comisi${filas.length !== 1 ? "ones" : "ón"}` : undefined}
        actions={
          <>
            {lista.length > 0 && (
              <>
                <Select
                  aria-label="Año"
                  value={String(periodo?.anioAcademico ?? "")}
                  onChange={(e) => setSeleccion({ anio: e.target.value, periodo: "" })}
                >
                  {anios.map((anio) => (
                    <option key={anio} value={anio}>
                      {anio}
                    </option>
                  ))}
                </Select>
                <Select
                  aria-label="Período lectivo"
                  value={periodo?.id ?? ""}
                  onChange={(e) =>
                    setSeleccion({
                      anio: String(periodo?.anioAcademico ?? ""),
                      periodo: e.target.value,
                    })
                  }
                >
                  {delAnio.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.nombre}
                    </option>
                  ))}
                </Select>
              </>
            )}
            {periodo && (
              <Button
                variant="secondary"
                onClick={exportarVisibles}
                disabled={visibles.length === 0 || exportar.isPending}
              >
                {exportar.isPending ? "Exportando…" : "Exportar a Excel"}
              </Button>
            )}
            {puedeImportar && (
              <Button variant="primary" onClick={abrirModal}>
                Actualizar desde Guaraní
              </Button>
            )}
          </>
        }
      />

      {periodos.isLoading && <p role="status">Cargando períodos importados…</p>}
      {periodos.isError && (
        <p role="alert">
          No se pudieron cargar los períodos importados.{" "}
          <button onClick={() => periodos.refetch()}>Reintentar</button>
        </p>
      )}
      {exportar.isError && (
        <InlineAlert severity="danger" title="No se pudo exportar">
          No se pudo generar el archivo. Actualizá la pantalla y reintentá.
        </InlineAlert>
      )}
      {ultimoResultado && (
        <InlineAlert severity="success" title="Importación completa">
          {resumenImportacion(ultimoResultado)}
        </InlineAlert>
      )}
      {periodos.isSuccess && lista.length === 0 && (
        <p>
          Todavía no hay comisiones importadas.
          {puedeImportar && " Usá “Actualizar desde Guaraní” para traer un período lectivo."}
        </p>
      )}

      {periodo && <EstadoImportacion periodo={periodo} />}
      {comisiones.isLoading && <p role="status">Cargando comisiones…</p>}
      {comisiones.isError && <p role="alert">No se pudieron cargar las comisiones.</p>}
      {periodo && comisiones.isSuccess && (
        <TablaComisiones visibles={visibles} filtros={filtros} onFiltros={setFiltros} />
      )}

      {puedeImportar && (
        <ModalImportarComisiones
          key={claveModal}
          open={modalAbierto}
          onOpenChange={setModalAbierto}
          onImportado={alImportar}
        />
      )}
    </>
  );
}

function descargar(archivo: Blob, nombre: string) {
  const url = URL.createObjectURL(archivo);
  const enlace = document.createElement("a");
  enlace.href = url;
  enlace.download = nombre;
  enlace.click();
  URL.revokeObjectURL(url);
}

function resumenImportacion(r: ResultadoImportacion): string {
  const partes = [`Se importaron ${r.cantidad} comisiones de ${r.periodoLectivoNombre}.`];
  if (r.docentesSinVincular > 0)
    partes.push(`${r.docentesSinVincular} docente(s) no figuran en el sistema.`);
  if (r.carrerasSinVincular > 0)
    partes.push(`${r.carrerasSinVincular} carrera(s) no figuran en el catálogo.`);
  return partes.join(" ");
}

function EstadoImportacion({ periodo }: { periodo: PeriodoImportado }) {
  const exitosa = periodo.ultimaExitosa;
  const intento = periodo.ultimoIntento;
  const ultimoFallo = intento?.estado === "fallida" ? intento : null;
  return (
    <>
      {exitosa && (
        <p className="adoc-comisiones-actualizacion">
          Actualizado el {formatearFechaHora(exitosa.fecha)}
          {exitosa.usuario ? ` por ${exitosa.usuario}` : ""}
        </p>
      )}
      {ultimoFallo && (
        <InlineAlert severity="warning" title="El último intento de actualización falló">
          El intento del {formatearFechaHora(ultimoFallo.fecha)} no pudo completarse; se muestran
          las comisiones de la última importación exitosa.
        </InlineAlert>
      )}
    </>
  );
}

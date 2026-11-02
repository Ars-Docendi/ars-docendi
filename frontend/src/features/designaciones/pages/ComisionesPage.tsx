import { useState } from "react";
import { Button, InlineAlert, Select } from "@ars-docendi/ui";
import { useFiltrosURL } from "../../../shared/navegacion/estadoURL";
import { Migas } from "../../../shared/navegacion/Migas";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import type { PeriodoImportado, ResultadoImportacion } from "../api/comisionesApi";
import { ModalImportarComisiones } from "../components/ModalImportarComisiones";
import { TablaComisiones } from "../components/TablaComisiones";
import { formatearFechaHora } from "../components/filtrosComisiones";
import { useComisionesDelPeriodo, usePeriodosImportados } from "../hooks/useComisiones";

const PERIODO_POR_DEFECTO = { periodo: "" };

/** Comisiones importadas desde Guaraní; consultarlas nunca contacta a Guaraní. */
export function ComisionesPage() {
  const { user } = useCurrentUser();
  const puedeImportar = user?.permissions.includes("comisiones.importar") ?? false;
  const periodos = usePeriodosImportados();
  const [{ periodo: periodoElegido }, setPeriodoElegido] = useFiltrosURL(PERIODO_POR_DEFECTO);
  const [modalAbierto, setModalAbierto] = useState(false);
  const [claveModal, setClaveModal] = useState(0);
  const [ultimoResultado, setUltimoResultado] = useState<ResultadoImportacion | undefined>();

  const lista = periodos.data ?? [];
  // Un período de la URL que ya no está importado cae en el más reciente.
  const periodo = lista.find((p) => p.id === periodoElegido) ?? lista[0];
  const comisiones = useComisionesDelPeriodo(periodo?.id);
  const filas = comisiones.data ?? [];

  function abrirModal() {
    setClaveModal((c) => c + 1);
    setModalAbierto(true);
  }

  function alImportar(resultado: ResultadoImportacion) {
    setUltimoResultado(resultado);
    setPeriodoElegido({ periodo: resultado.periodoLectivoId });
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
              <Select
                aria-label="Período lectivo"
                value={periodo?.id ?? ""}
                onChange={(e) => setPeriodoElegido({ periodo: e.target.value })}
              >
                {lista.map((p) => (
                  <option key={p.id} value={p.id}>
                    {etiquetaPeriodo(p)}
                  </option>
                ))}
              </Select>
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
      {periodo && comisiones.isSuccess && <TablaComisiones comisiones={filas} />}

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

function etiquetaPeriodo(periodo: PeriodoImportado): string {
  return periodo.nombre.includes(String(periodo.anioAcademico))
    ? periodo.nombre
    : `${periodo.nombre} ${periodo.anioAcademico}`;
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

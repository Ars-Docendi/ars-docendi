import { useState } from "react";
import { Breadcrumbs, Button, InlineAlert, Select } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import type { PeriodoImportado, ResultadoImportacion } from "../api/comisionesApi";
import { ModalImportarComisiones } from "../components/ModalImportarComisiones";
import { TablaComisiones } from "../components/TablaComisiones";
import { formatearFechaHora } from "../components/filtrosComisiones";
import { useComisionesDelPeriodo, usePeriodosImportados } from "../hooks/useComisiones";

/** Comisiones importadas desde Guaraní; consultarlas nunca contacta a Guaraní. */
export function ComisionesPage() {
  const { user } = useCurrentUser();
  const puedeImportar = user?.permissions.includes("comisiones.importar") ?? false;
  const periodos = usePeriodosImportados();
  const [periodoElegido, setPeriodoElegido] = useState<string | undefined>();
  const [modalAbierto, setModalAbierto] = useState(false);
  const [claveModal, setClaveModal] = useState(0);
  const [ultimoResultado, setUltimoResultado] = useState<ResultadoImportacion | undefined>();

  const lista = periodos.data ?? [];
  const periodoId = periodoElegido ?? lista[0]?.id;
  const periodo = lista.find((p) => p.id === periodoId);
  const comisiones = useComisionesDelPeriodo(periodo?.id);
  const filas = comisiones.data ?? [];

  function abrirModal() {
    setClaveModal((c) => c + 1);
    setModalAbierto(true);
  }

  function alImportar(resultado: ResultadoImportacion) {
    setUltimoResultado(resultado);
    setPeriodoElegido(resultado.periodoLectivoId);
  }

  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[{ label: "Inicio", href: "/" }, { label: "Comisiones" }]}
      />
      <PageHeader
        title="Comisiones"
        meta={periodo ? `${filas.length} comisi${filas.length !== 1 ? "ones" : "ón"}` : undefined}
        actions={
          <>
            {lista.length > 0 && (
              <Select
                aria-label="Período lectivo"
                value={periodoId ?? ""}
                onChange={(e) => setPeriodoElegido(e.target.value)}
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
      {periodo && comisiones.isSuccess && <TablaComisiones key={periodo.id} comisiones={filas} />}

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

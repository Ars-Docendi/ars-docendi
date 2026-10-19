import { useState } from "react";
import { Button, Field, InlineAlert, Modal, Select } from "@ars-docendi/ui";
import { mensajeProblema } from "../../../shared/api/problemDetails";
import type { ResultadoImportacion } from "../api/comisionesApi";
import { useImportarComisiones, usePeriodosGuarani } from "../hooks/useComisiones";

interface ModalImportarComisionesProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onImportado: (resultado: ResultadoImportacion) => void;
}

/**
 * Elige año y período lectivo de Guaraní y reemplaza las comisiones de ese período.
 * Los años salen de los períodos que informa Guaraní, así que nunca se elige uno vacío.
 */
export function ModalImportarComisiones({
  open,
  onOpenChange,
  onImportado,
}: ModalImportarComisionesProps) {
  const periodos = usePeriodosGuarani(open);
  const importar = useImportarComisiones();
  const [anioElegido, setAnioElegido] = useState<number | undefined>();
  const [periodoId, setPeriodoId] = useState("");

  const lista = periodos.data ?? [];
  const anios = [...new Set(lista.map((p) => p.anio))].sort((a, b) => b - a);
  const anio = anioElegido ?? anios[0];
  const delAnio = lista.filter((p) => p.anio === anio);
  const sinPeriodos = periodos.isSuccess && lista.length === 0;

  function confirmar() {
    if (!periodoId) return;
    importar.mutate(periodoId, {
      onSuccess: (resultado) => {
        onImportado(resultado);
        onOpenChange(false);
      },
    });
  }

  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title="Actualizar desde Guaraní"
      footer={
        <>
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            Cancelar
          </Button>
          <Button variant="primary" onClick={confirmar} disabled={!periodoId || importar.isPending}>
            {importar.isPending ? "Importando…" : "Importar"}
          </Button>
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
        <p style={{ margin: 0 }}>
          Se reemplazan las comisiones del período elegido con las que informa Guaraní. Los demás
          períodos no cambian.
        </p>
        {periodos.isLoading && <p role="status">Consultando períodos en Guaraní…</p>}
        {periodos.isError && (
          <InlineAlert severity="danger" title="No se pudieron obtener los períodos">
            {mensajeProblema(periodos.error, "No se pudo consultar a Guaraní.")}
          </InlineAlert>
        )}
        {sinPeriodos && (
          <InlineAlert severity="warning" title="Sin períodos">
            Guaraní no informa períodos lectivos disponibles.
          </InlineAlert>
        )}
        {importar.isError && (
          <InlineAlert severity="danger" title="No se pudo importar">
            {mensajeProblema(importar.error, "No se pudo importar el período.")}
          </InlineAlert>
        )}
        {anios.length > 0 && (
          <div style={{ display: "grid", gridTemplateColumns: "1fr 2fr", gap: "var(--space-3)" }}>
            <Field label="Año" required>
              <Select
                aria-label="Año"
                value={anio === undefined ? "" : String(anio)}
                onChange={(e) => {
                  setAnioElegido(Number(e.target.value));
                  setPeriodoId("");
                }}
              >
                {anios.map((a) => (
                  <option key={a} value={a}>
                    {a}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Período lectivo" required>
              <Select
                aria-label="Período lectivo"
                value={periodoId}
                onChange={(e) => setPeriodoId(e.target.value)}
              >
                <option value="">Elegí un período…</option>
                {delAnio.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.nombre}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
        )}
      </div>
    </Modal>
  );
}

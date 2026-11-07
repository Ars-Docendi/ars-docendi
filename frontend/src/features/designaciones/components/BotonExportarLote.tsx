import { Button } from "@ars-docendi/ui";
import { IconoDownload } from "./lucide";

interface BotonExportarLoteProps {
  /** Sin período activo no hay lote que descargar: el botón queda deshabilitado. */
  hayPeriodoActivo: boolean;
  exportando: boolean;
  onExportar: () => void;
  /** Id del texto que explica por qué está deshabilitado. */
  idAyuda?: string;
}

/** Acción principal de Revisión: descarga la planilla del lote del período activo. */
export function BotonExportarLote({
  hayPeriodoActivo,
  exportando,
  onExportar,
  idAyuda,
}: BotonExportarLoteProps) {
  return (
    <Button
      variant="primary"
      leadingIcon={<IconoDownload />}
      disabled={!hayPeriodoActivo || exportando}
      loading={exportando}
      onClick={onExportar}
      aria-describedby={!hayPeriodoActivo ? idAyuda : undefined}
    >
      {exportando ? "Exportando…" : "Exportar lote"}
    </Button>
  );
}

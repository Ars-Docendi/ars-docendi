export interface FilaAsignacion {
  materiaId: string;
  carreraId: string;
  cargoId: string;
  dedicacionId: string;
  horas: string;
  /** Dedicación histórica fuera del catálogo vigente (sólo se muestra). */
  dedicacionLegada?: string | null;
}

export const FILA_ASIGNACION_VACIA: FilaAsignacion = {
  materiaId: "",
  carreraId: "",
  cargoId: "",
  dedicacionId: "",
  horas: "",
};

export const filaCompleta = (f: FilaAsignacion) =>
  Boolean(
    f.materiaId &&
    f.carreraId &&
    f.cargoId &&
    (f.dedicacionId || f.dedicacionLegada) &&
    Number(f.horas) > 0,
  );

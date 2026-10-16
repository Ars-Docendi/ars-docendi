/** Error de un campo de período con mes elegido y sin año. */
export const MENSAJE_FALTA_ANIO = "Completá el año.";

/** Qué campos del período tienen un mes elegido pero todavía no tienen año. */
export interface MesSinAnio {
  desde: boolean;
  hasta: boolean;
}

export const SIN_MES_SIN_ANIO: MesSinAnio = { desde: false, hasta: false };

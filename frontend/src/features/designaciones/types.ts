export type EstadoPeriodo = "abierto" | "cerrado" | "proximo";

export interface PeriodoDesignacion {
  id: string;
  nombre: string;
  cuatrimestre: "1C" | "2C" | "Verano";
  anio: number;
  fechaApertura: string;
  fechaCierre: string;
  estado: EstadoPeriodo;
}

/**
 * Una designación vigente de la persona, para mostrarla en la administración (sólo lectura).
 * Es interna (cargo y dedicación) o externa: un docente externo lo paga otro departamento o
 * área (`sectorExternoId`) y no tiene cargo ni dedicación.
 */
export interface FilaAsignacion {
  materiaId: string;
  carreraId: string;
  cargoId: string;
  dedicacionId: string;
  horas: string;
  externo: boolean;
  sectorExternoId: string;
}

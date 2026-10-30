/** Identidad sembrada para ingresar en desarrollo; el rol se elige después. */
export interface IdentidadDesarrollo {
  usuarioId: string;
  nombreParaMostrar: string;
  upn: string;
  /** Nombres de sus roles vigentes, sólo para reconocerla en el selector. */
  roles: string[];
}

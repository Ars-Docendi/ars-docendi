/**
 * De un tipo de recurso a dónde se lo ve.
 *
 * EL BACKEND NO MANDA RUTAS. Manda qué clase de cosa identifica cada celda y con
 * qué identificador; a dónde va el usuario es una decisión de esta aplicación, y
 * cambiar una ruta no puede obligar a desplegar el backend.
 *
 * UN TIPO QUE NO ESTÁ ACÁ NO SE PINTA. Es lo que hace el contrato compatible en la
 * dirección que importa: el día que el servidor empiece a ofrecer vínculos a
 * perfiles de portal, un cliente viejo no muestra un enlace roto — sencillamente no
 * lo aprovecha.
 */
export interface DestinoDelVinculo {
  /** A dónde navega. */
  ruta: (id: string) => string;
  /**
   * Cómo se nombra el recurso en el nombre accesible del enlace.
   *
   * Un lector de pantalla que anuncia «enlace, 2026-9005» no dice a dónde lleva;
   * «Ver el trámite 2026-9005» sí.
   */
  sustantivo: string;
}

const DESTINOS: Record<string, DestinoDelVinculo> = {
  // El tipo lo elige el adaptador del backend; acá sólo se lo traduce. Tiene que
  // coincidir con `VinculosDeDesignaciones.TipoDelPedido`.
  "pedido-designacion": {
    ruta: (id) => `/designaciones/pedidos/${id}`,
    sustantivo: "trámite",
  },
};

/** El destino de un tipo, o `undefined` si este cliente no lo conoce. */
export function destinoDe(tipo: string): DestinoDelVinculo | undefined {
  return Object.hasOwn(DESTINOS, tipo) ? DESTINOS[tipo] : undefined;
}

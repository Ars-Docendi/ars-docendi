/** De un tipo de recurso a dónde se lo ve. EL BACKEND NO MANDA RUTAS. */
export interface DestinoDelVinculo {
  /** A dónde navega. */
  ruta: (id: string) => string;
  /** Cómo se nombra el recurso en el nombre accesible del enlace. */
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

/** Los tipos de principal que este cliente sabe dibujar como ficha. */
const TIPOS_CON_FICHA = new Set(["pedido-designacion"]);

export function tipoConFicha(tipo: string): boolean {
  return TIPOS_CON_FICHA.has(tipo) && destinoDe(tipo) !== undefined;
}

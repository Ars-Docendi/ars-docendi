/** Contexto que `LayoutAyuda` entrega a las páginas por `<Outlet context>`. */
export interface ContextoLayoutAyuda {
  /** La página de artículo informa qué paso se está leyendo (`paso-N`). */
  fijarPasoActivo: (id: string | undefined) => void;
}

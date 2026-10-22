import { LayoutAyuda } from "./components/LayoutAyuda";

/**
 * Centro de ayuda. No exige permiso: hereda `RequireAuth` y cada página filtra su
 * contenido por los permisos de la sesión.
 */
export const routes = {
  path: "/ayuda",
  element: <LayoutAyuda />,
  children: [
    {
      index: true,
      lazy: () =>
        import("./pages/InicioAyudaPage").then(({ InicioAyudaPage }) => ({
          Component: InicioAyudaPage,
        })),
    },
    {
      // Literal reservado: el router lo prioriza sobre `:modulo`.
      path: "buscar",
      lazy: () =>
        import("./pages/ResultadosAyudaPage").then(({ ResultadosAyudaPage }) => ({
          Component: ResultadosAyudaPage,
        })),
    },
    {
      path: ":modulo",
      lazy: () =>
        import("./pages/ModuloAyudaPage").then(({ ModuloAyudaPage }) => ({
          Component: ModuloAyudaPage,
        })),
    },
    {
      path: ":modulo/:articulo",
      lazy: () =>
        import("./pages/ArticuloAyudaPage").then(({ ArticuloAyudaPage }) => ({
          Component: ArticuloAyudaPage,
        })),
    },
  ],
};

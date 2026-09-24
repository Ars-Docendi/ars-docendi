import type { RouteObject } from "react-router-dom";

export const routes: RouteObject = {
  path: "tareas",
  children: [
    {
      index: true,
      lazy: () => import("./pages/IndexPage").then(({ IndexPage }) => ({ Component: IndexPage })),
    },
    {
      path: "proyectos",
      lazy: () =>
        import("./pages/ListadoProyectosPage").then(({ ListadoProyectosPage }) => ({
          Component: ListadoProyectosPage,
        })),
    },
    {
      path: "proyectos/:id",
      lazy: () =>
        import("./pages/DetalleProyectoPage").then(({ DetalleProyectoPage }) => ({
          Component: DetalleProyectoPage,
        })),
    },
    {
      path: ":id",
      lazy: () =>
        import("./pages/DetalleTareaPage").then(({ DetalleTareaPage }) => ({
          Component: DetalleTareaPage,
        })),
    },
  ],
};

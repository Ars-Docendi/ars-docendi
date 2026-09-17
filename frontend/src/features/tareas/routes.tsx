import type { RouteObject } from "react-router-dom";

export const routes: RouteObject = {
  path: "tareas",
  children: [
    {
      index: true,
      lazy: () => import("./pages/IndexPage").then(({ IndexPage }) => ({ Component: IndexPage })),
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

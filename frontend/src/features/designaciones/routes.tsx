import type { RouteObject } from "react-router-dom";
import { IndexPage } from "./pages/IndexPage";
import { PeriodosPage } from "./pages/PeriodosPage";

export const routes: RouteObject = {
  path: "designaciones",
  children: [
    { index: true, element: <IndexPage /> },
    { path: "periodos", element: <PeriodosPage /> },
  ],
};

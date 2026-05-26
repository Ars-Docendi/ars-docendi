import type { RouteObject } from "react-router-dom";
import { IndexPage } from "./pages/IndexPage";

export const routes: RouteObject = {
  path: "tareas",
  children: [{ index: true, element: <IndexPage /> }],
};

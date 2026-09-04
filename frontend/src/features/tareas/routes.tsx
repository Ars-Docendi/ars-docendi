import type { RouteObject } from "react-router-dom";
import { IndexPage } from "./pages/IndexPage";
import { DetalleTareaPage } from "./pages/DetalleTareaPage";

export const routes: RouteObject = {
  path: "tareas",
  children: [
    { index: true, element: <IndexPage /> },
    { path: ":id", element: <DetalleTareaPage /> },
  ],
};

import { createBrowserRouter, Navigate } from 'react-router-dom'

import App from '../App'
import { routes as designacionesRoutes } from '../features/designaciones/routes'
import { routes as aulasRoutes } from '../features/aulas/routes'
import { routes as portalRoutes } from '../features/portal/routes'
import { routes as tareasRoutes } from '../features/tareas/routes'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <App />,
    children: [
      { index: true, element: <Navigate to="/designaciones" replace /> },
      designacionesRoutes,
      aulasRoutes,
      portalRoutes,
      tareasRoutes,
    ],
  },
])

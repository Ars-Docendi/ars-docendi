import { Navigate, useSearchParams } from "react-router-dom";

/** Las pantallas de Usuarios y Docentes se unificaron en Personas; los enlaces viejos redirigen. */
export function RedireccionPersonas() {
  const [parametros] = useSearchParams();
  const personaId = parametros.get("personaId");
  return <Navigate to={personaId ? `/personas/${personaId}` : "/personas"} replace />;
}

import { isAxiosError } from "axios";
import { mensajeProblema, type ProblemDetailsApi } from "../../shared/api/problemDetails";

export function mensajeConflictoRol(error: unknown): string {
  const tipo = isAxiosError<ProblemDetailsApi>(error) ? error.response?.data?.type : undefined;
  if (tipo?.endsWith("identity-role-name-conflict"))
    return "Ya existe otro rol activo con ese nombre. Elegí un nombre diferente.";
  if (tipo?.endsWith("identity-role-scope-conflict"))
    return "No se puede cambiar el ámbito con asignaciones vigentes. Conservá el ámbito o revisá las membresías antes de editarlo.";
  return mensajeProblema(
    error,
    "No se pudo guardar el cambio del rol. Revisá los datos e intentá nuevamente.",
  );
}

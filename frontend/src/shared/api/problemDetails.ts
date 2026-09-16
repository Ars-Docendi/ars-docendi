import axios from "axios";

export interface ProblemDetailsApi {
  type?: string;
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export function mensajeProblema(error: unknown, fallback: string): string {
  if (!axios.isAxiosError<ProblemDetailsApi>(error)) return fallback;
  const problema = error.response?.data;
  if (problema?.type?.endsWith("concurrency-conflict")) {
    return "Los datos cambiaron mientras editabas. Cerrá el formulario, actualizá la lista y volvé a intentar.";
  }
  if (problema?.type?.endsWith("identity-role-scope-conflict")) {
    return "Revisá las membresías: cada rol debe tener un ámbito válido y no repetirse.";
  }
  if (problema?.type?.endsWith("identity-upn-conflict")) {
    return "Ya existe otra cuenta con esa UPN.";
  }
  if (problema?.type?.endsWith("identity-document-conflict")) {
    return "Ya existe otra persona con ese documento.";
  }
  if (problema?.type?.endsWith("identity-file-number-conflict")) {
    return "Ya existe otra persona con ese legajo.";
  }
  const campos = problema?.errors ? Object.values(problema.errors).flat() : [];
  return campos[0] ?? problema?.detail ?? problema?.title ?? fallback;
}

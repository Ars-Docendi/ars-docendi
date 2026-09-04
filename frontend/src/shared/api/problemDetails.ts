import axios from "axios";

export interface ProblemDetailsApi {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export function mensajeProblema(error: unknown, fallback: string): string {
  if (!axios.isAxiosError<ProblemDetailsApi>(error)) return fallback;
  const problema = error.response?.data;
  const campos = problema?.errors ? Object.values(problema.errors).flat() : [];
  return campos[0] ?? problema?.detail ?? problema?.title ?? fallback;
}

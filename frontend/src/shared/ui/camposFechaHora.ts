/** Mensajes de validación compartidos por los campos de fecha y hora. */
export const MENSAJE_FECHA_INVALIDA = "Ingresá una fecha válida (dd/mm/aaaa).";
export const MENSAJE_HORA_INVALIDA = "Ingresá un horario válido (hh:mm).";

/**
 * Aplica la máscara dd/mm/aaaa a lo que escribió la persona. Una fecha ISO pegada (aaaa-mm-dd)
 * se convierte. Con `borrando` no reinserta la barra que la persona acaba de borrar.
 */
export function formatearFecha(texto: string, borrando = false): string {
  const iso = /^\s*(\d{4})-(\d{2})-(\d{2})/.exec(texto);
  const base = iso ? `${iso[3]}${iso[2]}${iso[1]}` : texto;
  const digitos = base.replace(/\D/g, "").slice(0, 8);
  let salida = digitos.slice(0, 2);
  if (digitos.length > 2) salida += `/${digitos.slice(2, 4)}`;
  if (digitos.length > 4) salida += `/${digitos.slice(4, 8)}`;
  if (!borrando && (digitos.length === 2 || digitos.length === 4)) salida += "/";
  return salida;
}

/** Aplica la máscara hh:mm (24 h). */
export function formatearHora(texto: string, borrando = false): string {
  const digitos = texto.replace(/\D/g, "").slice(0, 4);
  let salida = digitos.slice(0, 2);
  if (digitos.length > 2) salida += `:${digitos.slice(2, 4)}`;
  if (!borrando && digitos.length === 2) salida += ":";
  return salida;
}

/** "dd/mm/aaaa" a "aaaa-mm-dd", o `null` si no es una fecha real entre 1900 y 2100. */
export function fechaTextoAISO(texto: string): string | null {
  const m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(texto);
  if (!m) return null;
  const dia = Number(m[1]);
  const mes = Number(m[2]);
  const anio = Number(m[3]);
  if (anio < 1900 || anio > 2100 || mes < 1 || mes > 12 || dia < 1) return null;
  if (dia > new Date(anio, mes, 0).getDate()) return null;
  return `${m[3]}-${m[2]}-${m[1]}`;
}

/** "aaaa-mm-dd" a "dd/mm/aaaa"; cadena vacía si no tiene ese formato. */
export function fechaISOATexto(iso: string): string {
  const partes = iso.split("-");
  return partes.length === 3 ? `${partes[2]}/${partes[1]}/${partes[0]}` : "";
}

/** El mismo "hh:mm" si es una hora de 24 h válida; `null` si no. */
export function horaTextoValida(texto: string): string | null {
  const m = /^(\d{2}):(\d{2})$/.exec(texto);
  return m && Number(m[1]) <= 23 && Number(m[2]) <= 59 ? texto : null;
}

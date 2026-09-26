/**
 * Triggers a browser download of `contenido` as a file named `nombre`.
 *
 * A thin wrapper around the Blob/object-URL dance, kept in its own function so
 * a test can mock the trigger without needing a real DOM download to happen.
 */
export function descargarArchivo(nombre: string, contenido: string, tipoMime: string): void {
  const blob = new Blob([contenido], { type: tipoMime });
  const url = URL.createObjectURL(blob);

  const enlace = document.createElement("a");
  enlace.href = url;
  enlace.download = nombre;
  enlace.click();

  URL.revokeObjectURL(url);
}

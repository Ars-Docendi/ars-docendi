/** Triggers a browser download of `contenido` as a file named `nombre`. */
export function descargarArchivo(nombre: string, contenido: string, tipoMime: string): void {
  const blob = new Blob([contenido], { type: tipoMime });
  const url = URL.createObjectURL(blob);

  // Colgado del documento: un <a> suelto no dispara la descarga en todos los
  // motores.
  const enlace = document.createElement("a");
  enlace.href = url;
  enlace.download = nombre;
  enlace.style.display = "none";
  document.body.appendChild(enlace);
  enlace.click();
  enlace.remove();

  // La descarga arranca asíncrona: revocar en el mismo tick la puede cortar (Safari, algunas
  // versiones de Firefox).
  window.setTimeout(() => URL.revokeObjectURL(url), 0);
}

/** Ids que enlazan cada pestaña con su panel (`aria-controls` / `aria-labelledby`). */
export function idPestana(prefijo: string, id: string) {
  return `${prefijo}-tab-${id}`;
}
export function idPanelPestana(prefijo: string, id: string) {
  return `${prefijo}-panel-${id}`;
}

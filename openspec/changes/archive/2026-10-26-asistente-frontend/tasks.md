## 1. La feature y su acceso (ARS-50)

- [x] 1.1 `features/asistente/types.ts` — el contrato de respuesta del backend
- [x] 1.2 `features/asistente/api/asistenteApi.ts` — `consultar` y `capacidades`
- [x] 1.3 `features/asistente/hooks/useAccesoAlAsistente.ts` — gate por `403`, no por rol
- [x] 1.4 `features/asistente/hooks/useAsistente.ts` — el turno, el hilo y la clave por intento
- [x] 1.5 `features/asistente/components/PanelAsistente.tsx` — la vista, montada dos veces
- [x] 1.6 `features/asistente/pages/AsistentePage.tsx` — la ruta a página completa
- [x] 1.7 `features/asistente/components/LanzadorAsistente.tsx` — el cajón desde la barra
- [x] 1.8 `TopBar.tsx` — el botón «Ayuda» deja de estar `disabled`
- [x] 1.9 `router.tsx` — la ruta `/asistente`
- [x] 1.10 Test: sin acceso no hay lanzador; con acceso sí
- [x] 1.11 Test: no queda ningún botón de ayuda deshabilitado
- [x] 1.12 Test: el lanzador y la ruta montan la misma vista

## 2. Los cuatro estados (ARS-50)

- [x] 2.1 `components/Mensaje.tsx` — el turno del usuario y el del asistente
- [x] 2.2 `components/TablaDeResultado.tsx` — filas, con aviso de truncado **sin conteo**
- [x] 2.3 `components/Opciones.tsx` — la aclaración, que continúa el turno
- [x] 2.4 `components/Sugerencias.tsx` — preguntas nuevas, visualmente distintas
- [x] 2.5 El degradado se muestra como estado, con el texto del backend
- [x] 2.6 Errores de transporte en español, sin códigos ni nombres técnicos
- [x] 2.7 Test: los cuatro estados se distinguen
- [x] 2.8 Test: el truncado avisa y no dice cuántas filas faltan
- [x] 2.9 Test: opciones y sugerencias se presentan distinto
- [x] 2.10 Test: un fallo de red no muestra el código de estado

## 3. Accesibilidad y feedback (ARS-51)

- [x] 3.1 `components/Conversacion.tsx` — `role="log"` + `aria-live="polite"` **solo sobre los mensajes**
- [x] 3.2 `components/LineaDeMetricas.tsx` — hermana de la región viva, fuera
- [x] 3.3 `components/IndicadorDeProceso.tsx` — umbral de aparición y `role="status"`
- [x] 3.4 Foco al campo de entrada cuando llega la respuesta
- [x] 3.5 Ninguna etapa simulada por temporizador
- [x] 3.6 Test: las métricas no están dentro de la región viva
- [x] 3.7 Test: la lista de mensajes tiene rol de registro
- [x] 3.8 Test: una respuesta rápida no muestra indicador
- [x] 3.9 Test: una respuesta lenta sí, y se anuncia como estado
- [x] 3.10 Test: el foco vuelve al campo tras responder y tras elegir una opción
- [x] 3.11 Verificar en rojo: envolver el contenedor entero hace fallar 3.6

## 4. Documentación

- [x] 4.1 `docs/architecture/domains/asistente.md` — la superficie de usuario
- [x] 4.2 `docs/quality/tech-debt.md` — el fake UI eliminado y lo que queda

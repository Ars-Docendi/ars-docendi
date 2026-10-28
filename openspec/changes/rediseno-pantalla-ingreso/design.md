# Design

## Context

`LoginPage.tsx` arma la pantalla con un panel decorativo (`Marquee`) y una tarjeta de ingreso. El panel usa `--neutral-900` con líneas diagonales sutiles, y el "logo" es un `div` con borde y el texto "UNLaM". El repo no tiene un archivo con el logo de Ars Docendi. El equipo lo compartió como imagen: un isotipo con la "A" y la "D" entrelazadas y un birrete, en verde, más el wordmark "Ars Docendi" y la bajada "Gestión docente universitaria". La paleta de `@ars-docendi/ui` (v1.0.4) define una escala `--accent-*` (verde, matiz 165) y una `--neutral-*`, y ambas se invierten en modo oscuro.

Este rediseño se resuelve sólo en código, sin pasar por los `.pen`, por pedido explícito del equipo.

## Goals / Non-Goals

**Goals:**

- Mostrar la marca de Ars Docendi y aplicar al panel un color de la guía, manteniendo la estructura de dos columnas.
- Que el copy describa el ingreso real (ver `specs/pantalla-ingreso/spec.md`).

**Non-Goals:**

- Cambiar estados, redirecciones, el selector de desarrollo o el backend.
- Rediseñar la tarjeta de ingreso más allá de sus textos.
- Mostrar la versión del sistema: la `v0.1.1` escrita a mano en el pie del panel ya estaba desactualizada y se elimina.

## Decisions

### Layout y color: panel de bienvenida en verde de marca

La pantalla sigue la estructura de una referencia que eligió el equipo. A la izquierda va un panel de bienvenida: degradé liso de `--accent-500` a `--accent-800`, sin motivos decorativos, el isotipo grande en blanco, el título "Te damos la bienvenida a Ars Docendi" (la marca en `--accent-200`, en su propio renglón), qué se puede hacer en el sistema y el departamento al pie. A la derecha, fondo `--color-bg-raised`, la marca (isotipo y "Ars Docendi") arriba, el formulario "Ingresá a tu cuenta" con el botón primario y el pie legal.

El botón de Microsoft usa el primario verde de la librería, igual que las acciones principales del resto del sistema. La escala accent se invierte en tema oscuro, así que el degradé usa `--accent-400` a `--accent-100`, que son los mismos verdes.

- Alternativa descartada: un botón oscuro como el de Microsoft. Se aparta del primario que usa el resto del sistema.

- Alternativas descartadas: verdes planos oscuros (`--accent-600` a `--accent-800`), que se confundían con el botón o se leían casi negros; un verde claro con tinta oscura, que no le dio identidad suficiente al panel; gris oscuro y blanco, que pierden el verde.

### Responsive

Hay tres rangos. En escritorio, dos columnas. Hasta 960 px, una columna: el panel pasa a ser una banda superior con el isotipo a la izquierda y, a su derecha, la bienvenida y la descripción. El pie del panel y la marca del formulario se ocultan, porque la banda ya identifica el sistema. Hasta 480 px, la banda muestra sólo el isotipo y la bienvenida, para que el botón de ingreso se vea sin scroll en un celular de 375 × 812. No hay scroll horizontal en ningún rango.

### Marca: isotipo vectorizado en un componente y en el favicon

El isotipo se vectoriza a mano a partir de la imagen y queda en `IsotipoArsDocendi.tsx` como SVG inline monocromo (`currentColor`): blanco en el panel y verde en la marca del formulario. El panel sigue siendo decorativo (`aria-hidden`). El favicon (`frontend/public/favicon.svg`), que hasta ahora era el logo por defecto de Vite, pasa a ser el mismo isotipo en el verde de marca (`#009674`, el valor de `--accent-500`). Como es un archivo estático, no puede usar tokens.

- Alternativa descartada: usar la imagen raster. Tiene fondo blanco y colores fijos, así que no se integra al panel ni escala bien.

### Copy

| Lugar                  | Actual                                                                                                          | Nuevo                                                                                                                                                                  |
| ---------------------- | --------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Crest                  | Departamento de Ingeniería / Universidad Nacional de La Matanza                                                 | Pie del panel: Departamento de Ingeniería e Investigaciones Tecnológicas / Universidad Nacional de La Matanza                                                          |
| Título del panel       | Ars Docendi                                                                                                     | Te damos la bienvenida a Ars Docendi                                                                                                                                   |
| Bajada del panel       | Designaciones, reservas de aula, tareas y perfil del docente — un solo lugar, una sola identidad institucional. | Acá vas a poder gestionar designaciones, reservar aulas, seguir tus tareas, mantener tu perfil docente y consultar al asistente conversacional, todo en un solo lugar. |
| Título del formulario  | Iniciá sesión                                                                                                   | Ingresá a tu cuenta                                                                                                                                                    |
| Bajada                 | Usá tu cuenta institucional para acceder.                                                                       | Usá tu cuenta institucional @docentes.unlam.edu.ar.                                                                                                                    |
| Botón                  | Iniciar sesión con cuenta institucional                                                                         | Ingresar con Microsoft                                                                                                                                                 |
| Ayuda                  | ¿Problemas para ingresar? Escribí a soporte… indicando tu legajo o DNI.                                         | Se elimina el bloque de ayuda y su separador                                                                                                                           |
| Error de autenticación | …si persiste, contactá a Soporte.                                                                               | …si persiste, comunicate con Secretaría Académica.                                                                                                                     |
| Condiciones de uso     | Al ingresar aceptás el uso institucional… log de auditoría.                                                     | Pie: "Al ingresar aceptás los Términos de uso y la Política de privacidad", con enlace a `/terminos`                                                                   |

### Página de términos de uso y privacidad

Es una ruta pública `/terminos`, fuera de `RequireAuth` y del shell de la aplicación, igual que `/login`. Vive en `frontend/src/shared/legal/` porque no pertenece a ningún módulo. Es contenido estático en JSX, sin backend, y nginx ya resuelve la ruta con su fallback SPA. Un solo documento reúne términos y privacidad, para que haya un único enlace y una única fecha de vigencia.

El contenido se apoya en lo que el sistema efectivamente guarda (`docs/architecture/data-model.md`) y en la Ley 25.326: deber de información (art. 6), derechos de acceso, rectificación y supresión (arts. 14 a 16), y la leyenda del órgano de control, hoy la AAIP. Sólo incluye datos institucionales confirmados. Ni el domicilio, ni la inscripción en el Registro Nacional de Bases de Datos, ni los plazos concretos de conservación figuran hasta que la Universidad los confirme. Los derechos se ejercen ante Secretaría Académica, el mismo canal que usa el resto de la pantalla.

- Alternativa descartada: un modal en el login. No se puede enlazar ni leer fuera del login, y el texto es largo.

## Risks / Trade-offs

- [El vector es una reproducción manual de la imagen] → Si el equipo genera el SVG original, se reemplazan los trazados del componente sin tocar la pantalla.
- [El texto legal no está revisado por la Universidad] → Antes de llevar el change a producción, el área legal de la UNLaM revisa la página y se agregan domicilio, inscripción registral y plazos de conservación. La leyenda de la AAIP se verifica contra el texto vigente.
- [Un panel verde con mucho texto puede bajar la legibilidad] → Verificar contraste AA del texto secundario (blanco al 75 %) sobre `--accent-600`.

## Migration Plan

Deploy normal del frontend. Rollback: revertir el commit.

# Modules.Asistente.Contracts

**Este proyecto está vacío a propósito, y su existencia es una decisión abierta.**

## El problema

La convención del repo es que cada `Modules.<X>` tenga su `Modules.<X>.Contracts`
como única superficie pública cross-module. Con el asistente esa simetría no se
sostiene sola: el módulo **consume** Contracts ajenos —para enrutar preguntas por
el carril determinista de API— pero **nadie lo consume a él**. Un asistente es una
hoja del grafo de dependencias.

Un `.Contracts` sin tipos, que ningún proyecto referencia, no es una frontera: es
estructura decorativa que hay que compilar, versionar y explicar.

## Las dos salidas

| Opción                         | A favor                                                                                        | En contra                                                             |
| ------------------------------ | ---------------------------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| **Conservarlo** por convención | Si mañana el asistente publica algo (un evento de consulta respondida, para métricas), ya está | Hoy es un assembly vacío en la solución                               |
| **Borrarlo** y documentar      | La solución solo tiene proyectos con contenido                                                 | Rompe la simetría; recrearlo después toca `.slnx`, CI y el Dockerfile |

Mientras no se resuelva, el proyecto queda en la solución **sin que nadie lo
referencie**. Es deliberado: un proyecto huérfano es visible en cada build y en
cada review, y obliga a cerrar la pregunta. Una referencia de cortesía desde
`Modules.Asistente` la escondería.

## Si se conserva

Valen las mismas reglas que para el resto: solo DTOs, interfaces y tokens. **Sin
lógica.**

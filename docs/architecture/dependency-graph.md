# Dependency graph

**Reglas**: grafo dirigido acíclico (DAG). Los módulos solo dependen de `ArsDocendi.Shared` y de los `Modules.*.Contracts` que necesiten. Módulo → módulo **solo** vía `.Contracts`.

## Diagrama

```mermaid
flowchart TD
  subgraph host [Host]
    Host["ArsDocendi.Host"]
  end
  subgraph shared [Shared]
    Shared["ArsDocendi.Shared"]
  end
  subgraph contracts [Contracts públicos]
    DesignacionesContracts["Modules.Designaciones.Contracts"]
    AulasContracts["Modules.Aulas.Contracts"]
    PortalContracts["Modules.Portal.Contracts"]
    TareasContracts["Modules.Tareas.Contracts"]
  end
  subgraph modules [Modules internos]
    Designaciones["Modules.Designaciones"]
    Aulas["Modules.Aulas"]
    Portal["Modules.Portal"]
    Tareas["Modules.Tareas"]
  end

  Host --> Designaciones
  Host --> Aulas
  Host --> Portal
  Host --> Tareas
  Host --> DesignacionesContracts
  Host --> AulasContracts
  Host --> PortalContracts
  Host --> TareasContracts

  Designaciones --> Shared
  Aulas --> Shared
  Portal --> Shared
  Tareas --> Shared

  Designaciones --> DesignacionesContracts
  Aulas --> AulasContracts
  Portal --> PortalContracts
  Tareas --> TareasContracts

  Designaciones -.->|"vía PortalContracts (TBD)"| PortalContracts
  Aulas -.->|"vía PortalContracts (TBD)"| PortalContracts
```

Líneas punteadas: dependencias cross-module proyectadas (no confirmadas todavía). Cuando se confirmen, pasan a sólidas y se agregan al edge registry.

## Edge registry

| From                    | To                                | Vía               | Notas                      |
| ----------------------- | --------------------------------- | ----------------- | -------------------------- |
| `ArsDocendi.Host`       | `Modules.Designaciones`           | project reference | Hosting + composition root |
| `ArsDocendi.Host`       | `Modules.Aulas`                   | project reference | Hosting                    |
| `ArsDocendi.Host`       | `Modules.Portal`                  | project reference | Hosting                    |
| `ArsDocendi.Host`       | `Modules.Tareas`                  | project reference | Hosting                    |
| `ArsDocendi.Host`       | `Modules.*.Contracts`             | project reference | DI / interfaces            |
| `Modules.Designaciones` | `ArsDocendi.Shared`               | project reference | Utilidades                 |
| `Modules.Designaciones` | `Modules.Designaciones.Contracts` | project reference | Propio contract público    |
| `Modules.Aulas`         | `ArsDocendi.Shared`               | project reference | Utilidades                 |
| `Modules.Aulas`         | `Modules.Aulas.Contracts`         | project reference | Propio contract público    |
| `Modules.Portal`        | `ArsDocendi.Shared`               | project reference | Utilidades                 |
| `Modules.Portal`        | `Modules.Portal.Contracts`        | project reference | Propio contract público    |
| `Modules.Tareas`        | `ArsDocendi.Shared`               | project reference | Utilidades                 |
| `Modules.Tareas`        | `Modules.Tareas.Contracts`        | project reference | Propio contract público    |

**Edges cross-module proyectados (a confirmar en spec respectiva)**:

| From                    | To                         | Vía             | Razón                                                |
| ----------------------- | -------------------------- | --------------- | ---------------------------------------------------- |
| `Modules.Designaciones` | `Modules.Portal.Contracts` | DI via interfaz | Validar que el docente designado existe en el portal |
| `Modules.Aulas`         | `Modules.Portal.Contracts` | DI via interfaz | Conocer el docente solicitante de la reserva         |

## Agregar un edge nuevo

1. Confirmar que **no genera ciclo** (rastrear deps transitivas).
2. Agregar fila al edge registry de arriba.
3. Implementar usando **import de Contracts + DI**, nunca proyecto interno.
4. Actualizar el diagrama Mermaid.
5. En el PR, documentar el motivo del edge.

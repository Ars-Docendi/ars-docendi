# Ars Docendi — Gestión Docente

Sistema web para el Departamento de Ingeniería de la UNLaM. Digitaliza el flujo de designaciones docentes, reserva de aulas, autogestión del docente y seguimiento de tareas internas.

> Para setup y comandos de desarrollo ver [README.md](README.md).

## Stack Tecnológico

| Capa          | Tecnología                                            |
| ------------- | ----------------------------------------------------- |
| Backend       | C# — .NET 10 (ASP.NET Core Web API)                   |
| Frontend      | React 19 + TypeScript (Vite)                          |
| Base de datos | PostgreSQL (un schema por módulo)                     |
| Autenticación | SSO Microsoft Azure AD (credenciales institucionales) |

## Arquitectura

Monolito modular en monorepo. Un Host compone 4 módulos aislados; cada módulo expone su API pública vía un proyecto `*.Contracts`. Comunicación entre módulos sólo a través de contratos — los proyectos `Modules.X` internos no se referencian entre sí.

## Módulos del sistema

1. **Gestión de Proyecto Docente** (`Designaciones`) — workflow de designaciones + visualización de asignaciones (API Guaraní)
2. **Reserva de Aulas / Laboratorios** (`Aulas`) — pedidos y asignación para exámenes
3. **Portal del Docente** (`Portal`) — datos personales, horas, áreas de experticia
4. **Seguimiento de Tareas** (`Tareas`) — tablero tipo Trello con semáforo de vencimiento

El chatbot se integra dentro de alguno de los módulos anteriores (pendiente definir dónde).

## Roles

| Rol                             | Alcance                                         |
| ------------------------------- | ----------------------------------------------- |
| Jefe de Cátedra                 | Genera el proyecto docente de su cátedra        |
| Coordinador de Carrera          | Aprueba/rechaza novedades de su carrera         |
| Secretaría Académica del Depto. | Ve todo el departamento, parametriza el sistema |
| Decanato                        | Aprobación final                                |
| Administrativos                 | Gestionan reservas de aulas y configurables     |
| Docente                         | Portal propio: datos, horas, áreas              |

## Estructura del repositorio

```
/
├── backend/                # .NET 10, monolito modular
│   ├── ArsDocendi.slnx
│   ├── global.json
│   └── src/
│       ├── ArsDocendi.Host/
│       ├── ArsDocendi.Shared/
│       ├── Modules.Designaciones/   + .Contracts/
│       ├── Modules.Aulas/           + .Contracts/
│       ├── Modules.Portal/          + .Contracts/
│       └── Modules.Tareas/          + .Contracts/
├── frontend/               # Vite + React 19 + TS
│   └── src/
│       ├── app/            # router
│       ├── shared/         # api client, auth, ui primitivos
│       └── features/       # uno por módulo del backend
├── .github/workflows/ci.yml
├── docker-compose.yml
└── .env.example
```

## Convenciones

- `Modules.X` solo referencia `Shared` y `Modules.*.Contracts`. **Prohibido** referenciar `Modules.X` internos entre sí.
- Cada módulo expone `GET /api/{x}/ping` como smoke test.
- En el frontend, las features no se importan entre sí; lo común sube a `src/shared/`.
- Naming de branches, estrategia de testing y demás convenciones: _pendiente de definir_.

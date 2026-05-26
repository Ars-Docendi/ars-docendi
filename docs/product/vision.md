# Product vision

## Summary

Ars Docendi reemplaza el flujo manual y fragmentado (planillas Excel, mails, papeles físicos) con el que hoy el Departamento de Ingeniería de UNLaM gestiona designaciones docentes, reservas de aulas, datos del docente y tareas internas. La promesa es **una sola herramienta institucional** que respete los roles existentes (Jefe de Cátedra, Coordinador de Carrera, Secretaría Académica, Decanato, Administrativos, Docente), trace cada decisión a su origen reglamentario, y deje evidencia auditable de cada cambio.

## Goals

- **Eliminar la fragmentación documental**: una única fuente de verdad por dominio (designaciones, aulas, portal, tareas).
- **Hacer cumplir el flujo institucional**: las aprobaciones/rechazos respetan la cadena de roles real del departamento.
- **Reducir tiempo administrativo** en los procesos cíclicos (período de designaciones, mesas de examen).
- **Integrar el ecosistema institucional**: SSO con Microsoft Azure AD (credenciales UNLaM), consumo de la API Guaraní para visualización de asignaciones existentes.
- **Trazabilidad reglamentaria**: toda regla de negocio rastreable a su origen normativo (estatutos, regímenes, normativas departamentales).

## Non-goals

- **Gestión académica de alumnos** (notas, regularidades, mesas) — eso vive en Guaraní, Ars Docendi solo consume.
- **Sistema general de RRHH universitario** — solo cubre el alcance del Departamento de Ingeniería.
- **Reemplazo del SSO institucional** — Ars Docendi consume Azure AD, no maneja credenciales propias.
- **Reservas de aulas para uso académico regular** (clases) — solo exámenes y casos puntuales coordinados por Administrativos.

## Success metrics

- **Adopción institucional**: que los 6 roles del departamento usen el sistema como herramienta primaria del flujo correspondiente.
- **Tiempo de ciclo de designaciones**: reducción medible vs. el flujo actual (a medir con baseline al iniciar producción).
- **Trazabilidad reglamentaria**: 100% de las reglas de negocio implementadas tienen registro en `docs/business-rules/<modulo>.md` con cita de origen.
- **Disponibilidad**: uptime adecuado para herramienta interna universitaria (a definir SLA con cliente).
- **Cobertura de tests**: cobertura significativa de reglas de negocio críticas (designaciones, autorización por rol).

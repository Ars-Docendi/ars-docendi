erDiagram
IDENTITY_PERSONAS {
uuid id PK
text documento UK
text cuil
text legajo UK
text nombre
text apellido
date fecha_nacimiento
text telefono
}

    IDENTITY_USERS {
        uuid id PK
        uuid azure_oid UK
        text upn UK
        uuid persona_id FK
        boolean is_active
        timestamptz last_login_at
    }

    IDENTITY_CARRERAS {
        uuid id PK
        text code UK
        text name
        boolean is_active
    }

    IDENTITY_MATERIAS {
        uuid id PK
        uuid carrera_id FK
        text code
        text name
        boolean is_active
    }

    IDENTITY_ROLES {
        uuid id PK
        text code UK
        text name
        text scope
        boolean es_sistema
        boolean is_active
    }

    IDENTITY_PERMISOS {
        uuid id PK
        text code UK
        text nombre
        text descripcion
    }

    IDENTITY_USER_ROLES {
        uuid id PK
        uuid user_id FK
        uuid role_id FK
        uuid materia_id FK
        uuid carrera_id FK
        uuid granted_by FK
        timestamptz deleted_at
    }

    IDENTITY_ROL_PERMISOS {
        uuid rol_id PK,FK
        uuid permiso_id PK,FK
    }

    DESIGNACIONES_CARGOS {
        uuid id PK
        text codigo UK
        text nombre
        text abreviatura
        smallint orden UK
        boolean activo
    }

    DESIGNACIONES_PERIODOS {
        uuid id PK
        text nombre
        date carga_desde
        date carga_hasta
        date impacto_desde
        date impacto_hasta
        boolean activo
    }

    DESIGNACIONES_PEDIDOS {
        uuid id PK
        text numero UK
        uuid periodo_id FK
        uuid persona_id FK
        uuid materia_id FK
        uuid cargo_solicitado_id FK
        text novedad
        text estado
        boolean prioritario
        jsonb snapshot
    }

    DESIGNACIONES_ADJUNTOS {
        uuid id PK
        uuid pedido_id FK
        text tipo
        text nombre
        text uri
    }

    DESIGNACIONES_HISTORIAL {
        uuid id PK
        uuid pedido_id FK
        uuid rol_id FK
        uuid actor_id FK
        text accion
        text etapa
        text comentario
    }

    DESIGNACIONES_VIGENTES {
        uuid id PK
        uuid persona_id FK
        uuid materia_id FK
        uuid cargo_id FK
        uuid origen_pedido_id FK
        text dedicacion
        int horas
        date vigente_desde
        date vigente_hasta
    }

    DESIGNACIONES_IDEMPOTENCIA {
        uuid id PK
        uuid actor_id FK
        uuid pedido_id FK
        uuid clave
        text ruta
        text request_hash
        jsonb response_body
    }

    AUDIT_CHANGE_LOG {
        bigint id PK
        text schema_name
        text table_name
        text row_pk
        text action
        uuid changed_by FK
        jsonb old_row
        jsonb new_row
        timestamptz changed_at
    }

    IDENTITY_PERSONAS o|--o| IDENTITY_USERS : "cuenta opcional"
    IDENTITY_CARRERAS ||--o{ IDENTITY_MATERIAS : contiene
    IDENTITY_USERS ||--o{ IDENTITY_USER_ROLES : recibe
    IDENTITY_ROLES ||--o{ IDENTITY_USER_ROLES : asignado
    IDENTITY_MATERIAS o|--o{ IDENTITY_USER_ROLES : "ámbito materia"
    IDENTITY_CARRERAS o|--o{ IDENTITY_USER_ROLES : "ámbito carrera"
    IDENTITY_ROLES ||--o{ IDENTITY_ROL_PERMISOS : agrupa
    IDENTITY_PERMISOS ||--o{ IDENTITY_ROL_PERMISOS : concede

    DESIGNACIONES_PERIODOS ||--o{ DESIGNACIONES_PEDIDOS : agrupa
    IDENTITY_PERSONAS ||--o{ DESIGNACIONES_PEDIDOS : docente
    IDENTITY_MATERIAS ||--o{ DESIGNACIONES_PEDIDOS : catedra
    DESIGNACIONES_CARGOS o|--o{ DESIGNACIONES_PEDIDOS : solicita
    DESIGNACIONES_PEDIDOS ||--o{ DESIGNACIONES_ADJUNTOS : adjunta
    DESIGNACIONES_PEDIDOS ||--o{ DESIGNACIONES_HISTORIAL : registra
    IDENTITY_ROLES ||--o{ DESIGNACIONES_HISTORIAL : rol_actor
    IDENTITY_USERS o|--o{ DESIGNACIONES_HISTORIAL : actor

    IDENTITY_PERSONAS ||--o{ DESIGNACIONES_VIGENTES : posee
    IDENTITY_MATERIAS ||--o{ DESIGNACIONES_VIGENTES : corresponde
    DESIGNACIONES_CARGOS ||--o{ DESIGNACIONES_VIGENTES : cargo
    DESIGNACIONES_PEDIDOS o|--o{ DESIGNACIONES_VIGENTES : origina

    IDENTITY_USERS ||--o{ DESIGNACIONES_IDEMPOTENCIA : ejecuta
    DESIGNACIONES_PEDIDOS ||--o{ DESIGNACIONES_IDEMPOTENCIA : protege
    IDENTITY_USERS o|--o{ AUDIT_CHANGE_LOG : autor

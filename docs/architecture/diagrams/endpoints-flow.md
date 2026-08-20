flowchart LR
Browser["React routes"]

    subgraph Frontend["Frontend API adapters"]
        UsersUI["/usuarios"]
        TeachersUI["/docentes"]
        RolesUI["/roles<br/>/membresia-roles"]
        RequestsUI["/designaciones/*"]
        EmptyUI["/aulas<br/>/portal<br/>/tareas"]
    end

    Auth["RequireAuth / role guards<br/>Axios apiClient"]
    Policies["ASP.NET authorization policies"]

    subgraph Controllers["HTTP surface"]
        UsersAPI["/api/administracion/usuarios*<br/>CatalogosAdministracionController"]
        TeachersAPI["/api/administracion/docentes*"]
        RolesAPI["/api/administracion/roles*<br/>/permisos"]
        RequestsAPI["/api/designaciones/pedidos*"]
        PeriodsAPI["/api/designaciones/periodos*"]
        DesignationCatalogs["/api/designaciones/catalogos"]
        Pings["/{aulas|portal|tareas|designaciones}/ping"]
        DevAPI["/api/desarrollo/identidades<br/>development only"]
    end

    subgraph Services["Application/domain layer"]
        UserServices["ServicioUsuarios / ServicioRoles"]
        TeacherService["ServicioDocentes"]
        RequestService["ServicioPedidosApi<br/>ServicioPedidos<br/>state machine"]
        PeriodService["ServicioPeriodos"]
        CatalogService["ServicioCatalogosDesignaciones"]
        DesignationContract["IAdministracionDesignaciones<br/>public module contract"]
    end

    subgraph Persistence["Persistence"]
        IdentityRepos["Identity repositories<br/>IConsultasIdentity"]
        DesignationRepos["Designaciones repositories"]
        IdentityDB[("identity schema")]
        DesignationDB[("designaciones schema")]
        AuditDB[("audit.change_log")]
    end

    Browser --> Frontend
    UsersUI --> Auth --> UsersAPI
    TeachersUI --> Auth --> TeachersAPI
    RolesUI --> Auth --> RolesAPI
    RequestsUI --> Auth
    Auth --> RequestsAPI
    Auth --> PeriodsAPI
    Auth --> DesignationCatalogs

    UsersAPI --> Policies --> UserServices
    RolesAPI --> Policies --> UserServices
    TeachersAPI --> Policies --> TeacherService
    RequestsAPI --> Policies --> RequestService
    PeriodsAPI --> Policies --> PeriodService
    DesignationCatalogs --> Policies --> CatalogService

    UserServices --> IdentityRepos --> IdentityDB

    TeacherService --> IdentityRepos
    TeacherService --> DesignationContract
    DesignationContract --> DesignationRepos

    RequestService --> IdentityRepos
    RequestService --> DesignationRepos
    PeriodService --> DesignationRepos
    CatalogService --> IdentityRepos
    CatalogService --> DesignationRepos
    DesignationRepos --> DesignationDB

    IdentityDB -. "audit triggers" .-> AuditDB
    DesignationDB -. "audit triggers" .-> AuditDB

    EmptyUI -. "no application API adapter yet" .-> Pings
    DevAPI --> IdentityRepos

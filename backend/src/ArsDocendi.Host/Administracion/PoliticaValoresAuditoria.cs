namespace ArsDocendi.Host.Administracion;

// Clasificación de exposición de valores; la captura SQL es más amplia para IDs
// de referencias. No se autorizan nombres arbitrarios por su sufijo o prefijo.
internal static class PoliticaValoresAuditoria
{
    private static readonly Dictionary<string, HashSet<string>> Campos = new(StringComparer.Ordinal)
    {
        ["identity.users"] = ["is_active", "created_at"],
        ["identity.personas"] = ["activo", "estado", "created_at"],
        ["identity.roles"] = ["code", "scope", "es_sistema", "is_active", "created_at"],
        ["identity.permisos"] = ["code", "created_at"],
        ["identity.rol_permisos"] = ["rol_id", "permiso_id", "created_at"],
        ["identity.user_roles"] = ["deleted_at", "created_at"],
        ["identity.materias"] = ["codigo", "activo", "created_at"],
        ["identity.carreras"] = ["codigo", "activo", "created_at"],
        ["identity.planes"] = ["activo", "created_at"],
        ["identity.materias_plan"] = ["created_at"],
        ["designaciones.designaciones"] = ["cargo_id", "dedicacion_id", "horas", "horas_investigacion", "horas_externas", "vigente_desde", "vigente_hasta", "origen_pedido_id", "created_at"],
        ["designaciones.pedidos"] = ["numero", "estado", "novedad", "prioritario", "cargo_solicitado_id", "dedicacion_solicitada_id", "horas", "horas_investigacion", "horas_externas", "tipo_baja", "etapa_retorno", "propietario_actual", "created_at"],
        ["designaciones.periodos"] = ["carga_desde", "carga_hasta", "impacto_desde", "impacto_hasta", "activo", "created_at"],
        ["designaciones.cargos"] = ["codigo", "orden", "activo", "created_at"],
        ["designaciones.dedicaciones"] = ["codigo", "orden", "activo", "created_at"],
        ["designaciones.pedido_adjuntos"] = ["tipo", "created_at"],
        ["designaciones.pedido_historial"] = ["accion", "etapa", "created_at"],
        ["portal.perfiles"] = ["created_at"],
        ["portal.contactos"] = ["created_at"],
        ["portal.cvs"] = ["fecha_carga", "created_at"],
        ["portal.experiencias"] = ["desde", "hasta", "created_at"],
        ["portal.educaciones"] = ["desde", "hasta", "created_at"],
        ["portal.certificaciones"] = ["fecha", "vencimiento", "created_at"],
        ["portal.proyectos"] = ["desde", "hasta", "created_at"],
        ["portal.proyecto_documentos"] = ["fecha_carga", "created_at"],
        ["portal.habilidades"] = ["sugerido", "usos", "created_at"],
        ["portal.docente_habilidades"] = ["perfil_id", "habilidad_id", "tipo", "created_at"],
        ["storage.archivos"] = ["proposito", "estado", "tamano_bytes", "expira_en", "confirmado_at", "revisado_at", "eliminado_at", "created_at"],
        ["tareas.proyectos"] = ["numero", "fecha_inicio", "fecha_fin", "estado_id", "created_at"],
        ["tareas.tareas"] = ["numero", "fecha_inicio", "fecha_fin", "prioridad_id", "tipo_id", "estado_id", "porcentaje_avance", "created_at"],
        ["tareas.tarea_relaciones"] = ["tarea_id", "relacionada_id", "created_at"],
        ["tareas.tarea_comentarios"] = ["created_at"],
        ["tareas.tarea_historial"] = ["accion", "estado", "estado_id", "created_at"],
    };

    public static bool Permite(string schema, string tabla, string campo) =>
        Campos.TryGetValue($"{schema}.{tabla}", out var campos) && campos.Contains(campo);
}

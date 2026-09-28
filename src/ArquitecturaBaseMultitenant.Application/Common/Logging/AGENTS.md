OperationLog envuelve cada método público de servicio y registra inicio, fin y código de error con LoggerMessage. No pases requests, secretos ni datos personales al logger; para medir duración usá TimeProvider inyectado.
Antes de escribir, leé: [logs](../../../../docs/rules/logs.md) · [backend §17](../../../../docs/architecture/backend.md#17-logging-openapi-health-rate-limiting-caché).
Copiá de: no hay implementación en ArquitecturaBase; la receta de su plan 2026-09-26 describe la firma.

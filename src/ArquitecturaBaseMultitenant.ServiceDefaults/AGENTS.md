Configuración común de observabilidad, resiliencia y salud: la resiliencia no reintenta los métodos que no son seguros (un POST va una vez), y fuera de Development y Testing la consola escribe JSON con los scopes, donde `RequestLogScopeMiddleware` de la Api pone `UserId` y `TenantId` ([logs](../../docs/rules/logs.md)).
Leé [docs/rules/capas-y-flujo.md](../../docs/rules/capas-y-flujo.md).
Copiá de [Extensions.cs](Extensions.cs).

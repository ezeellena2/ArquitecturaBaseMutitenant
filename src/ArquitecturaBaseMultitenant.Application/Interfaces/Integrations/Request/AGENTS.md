Contratos de contexto de la petición disponibles para Application. El tenant activo sale solo del acceso autenticado; el host de la página pública no autoriza datos privados.

Antes de escribir, leé: [multitenancy](../../../../../docs/rules/multitenancy.md) · [capas-y-flujo](../../../../../docs/rules/capas-y-flujo.md).

Copiá de: `ITenantContext.cs` y `ICurrentUser.cs`; Infrastructure conserva el contexto scoped y Api resuelve claims en E3. En E2, `ICurrentUser` usa un actor System por defecto para auditoría.

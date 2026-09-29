Servidor OIDC y revocación de tokens del acceso activo. Leé [identidad](../../../../docs/features/identidad.md), [multitenancy](../../../../docs/architecture/multitenancy.md) y [datos-personales](../../../../docs/rules/datos-personales.md).
El issuer es único; `access`, `tenant_id` y `tenant_kind` salen de la autorización, nunca del host. No registres tokens ni secretos.

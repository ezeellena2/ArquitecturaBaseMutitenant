# El arnés (fixture)

## 3. Mapa de carpetas → punteros

La primera parte de cada ruta indica la capa: `Domain/X` significa `src/Demo.Domain/X` y `Application/X` significa `src/Demo.Application/X`.

| Carpeta | Qué va / qué no | Fichas | Copiá de |
|---|---|---|---|
| `Domain/` | entidades y errores. Sin paquetes | guardado | `Domain/Authorization/Permissions.cs` |
| `Application/Services/` | servicio y helpers. No va EF | guardado, nueva | `Services/Roles/RoleService.cs` (E4) |
| `Application/Interfaces/Persistence/` | repositorios y lectores | guardado | `IRoleRepository.cs` (E5) |
| `Domain/Futuro/` | carpeta que todavía no existe | guardado | — |

# E8b · M2 · historial propio y enlaces vigentes

Estado: M2 implementado y verificado con RED→GREEN, 58/58 tests focales y guardas sin omisiones, y builds del alcance sin advertencias. No cierra E8b ni reemplaza su puerta final. Integración mantiene detenido el retiro de WhatsApp; este pase sólo verifica M2 en main.

## Problema y cambio

Antes, perder `users.read` después de ejecutar Usuarios activos, o dejar una organización de Mis organizaciones, hacía que `GET /conversations/{id}` devolviera `400 Assistant.QuickQuery.NotAvailable`. La conversación era del solicitante, pero la autorización estricta de sus enlaces impedía leerla.

El GET conserva la conversación propia y filtra los enlaces que el actor ya no puede navegar. El lookup sigue exigiendo propietario, acceso y organización; cuenta, membresía del acceso actual, feature, permiso de uso, legales y aviso IA siguen verificándose, incluida una reautorización final. No cambia qué persona puede leer una conversación.

`AssistantConversationPolicy` comparte un solo mapa de ruta/acceso/permiso entre historial y replay. El historial aplana los enlaces de la página y consulta una vez las organizaciones seleccionables del perfil, propagando sus errores operativos. Los CompanyId sólo se aceptan en Business para empresas activas del alcance actual, obtenidas por `ICompanyReader` con filtro/RLS. Rutas desconocidas, enlaces a otro acceso, empresas ajenas/inactivas y tenant impuesto se filtran. Se conserva la regla vigente: sólo PersonalOrganizations admite TenantId.

El replay y las consultas rápidas conservan la vía estricta: un enlace no disponible rechaza la respuesta original. No se adapta ni reescribe el snapshot confirmado para hacerlo pasar después de una revocación.

La proyección del GET reemplaza sólo Links en los records de respuesta. Conserva título, texto, IDs, versiones, fechas, estado, adjuntos, rating y cursor; sin UoW de lectura, escritura, renovación de actividad ni llamada al proveedor. Policy tiene seis dependencias de negocio y Service cinco. Sin DTO, ruta, permiso, migración o delta de contrato nuevos; sin cambios de Front.

## Tests y verificación

- Organizaciones: al abandonar una de dos organizaciones, GET conserva 200 y el único enlace vigente; el replay queda en 400 y no aparecen organizaciones ajenas.
- Usuarios: revocar `users.read` conservando `assistant.use` mantiene historial 200 sin enlaces y el contenido original; LinksJson/SnapshotJson permanecen intactos. Reasignar el permiso recupera el enlace. Revocar la membresía sigue denegando el acceso.
- Enlaces mixtos: dos mensajes personales filtran destinos ajenos/incompatibles/desconocidos; una sola consulta del perfil por página, errores propagados y cero llamadas al proveedor. El caso Business separa empresas activas, inactivas y de otra organización. Se releen filas y versiones originales sin cambios.

Antes de editar se preservaron las fuentes originales para el build RED mediante un override de Compile de Application guardado en `.local/reviews/e8b-history-links-20261011/red-source.targets`. El build RED confirmó el selector original y pasó sin advertencias: 3/3 casos nuevos fallaron y 2/7 de autorización fallaron por el historial rechazado; los otros cinco pasaron. GREEN se compiló sin override y con `--no-incremental`.

| Grupo final | Resultado |
|---|---|
| Historial de enlaces / autorización durable | 3/3 y 7/7 |
| Aislamiento de conversaciones | 3/3 |
| Actor / seguridad / Business | 2/2, 5/5 y 7/7 |
| Conversaciones durables | 4/4 |
| Consultas rápidas | 5/5 |
| Frontera IA / dependencias / módulos / DI | 12/12, 2/2, 2/2 y 6/6 |

Los 31 tests de integración usan PostgreSQL con Testcontainers/Docker y registro/autorización HTTP reales; las 27 guardas restantes son unitarias y de arquitectura. Todas las ejecuciones finales terminaron con exit 0 y cero omitidos. Builds de Api.IntegrationTests, Application.UnitTests y ArchitectureTests: cero advertencias y errores. `git diff --check`: 0. OpenAPI generado y versionado idénticos, SHA-256 `96e0f10b7aefdf085ea8ceca0cd2bf57e83261e9eb452ba4f5ad379522575a45`; sin delta HTTP ni tipos Front.

Se descartaron como evidencia RED el primer selector Compile duplicado y la selección vacía con filtro compuesto. El runner necesitó permisos para named pipes y Docker. En GREEN se corrigieron sólo las referencias iniciales de la fixture: PostgreSQL normaliza JSONb y conserva fechas en microsegundos. Ahora se releen las filas antes del GET y se exige igualdad exacta después, sin tolerancias ni cambios del producto para satisfacer los asserts. Las fuentes del producto conservaron la implementación preparada.

Los logs y las fuentes originales quedan en `.local/reviews/e8b-history-links-20261011/`. No se ejecutaron suite global, arnés, retiro, AppHost, Front ni E2E. `Role.cs` ajeno conserva SHA-256 `3260a9378a8bb68317e1d92ce81e290435d29b7d030624d72dfed470a114039a`. Runtime, retención M3 y las demás tareas del plan conservan sus pendientes.

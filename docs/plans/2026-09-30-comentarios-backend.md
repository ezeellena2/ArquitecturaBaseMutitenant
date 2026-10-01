# Comentarios de propósito del código backend — plan de implementación

> **Para agentes:** trabajar por capas en `main`, con rutas de `git add` explícitas. Este trabajo no cambia lógica; no se crean tests que solo comprueben la presencia de comentarios.

**Objetivo:** que una persona pueda abrir cada archivo C# escrito a mano del backend y entender la responsabilidad del tipo principal, su motivo y su lugar en el flujo.

**Alcance:** todos los `.cs` escritos a mano de `src/`, `tests/` y `tools/` de Api, Application, Domain, Infrastructure, AppHost, ServiceDefaults y herramientas E2E, incluidos DTO, enums, catálogos de errores, fixtures y pruebas. Se excluyen las migraciones, los archivos generados y los artefactos `bin/obj`. El usuario confirmó este alcance al pedir también una explicación de `AccessErrors`; no quedan exclusiones por considerar un tipo autoexplicativo.

**Criterio:** un `/// <summary>` de una a tres frases sobre el tipo principal, después del `namespace`. Debe explicar qué hace y para qué existe; cuando importa, señalar el límite con la siguiente pieza. En archivos de nivel superior sin tipo principal (`Program.cs`, `AppHost.cs`), el comentario de propósito va antes del bloque de arranque/composición. Se conservan los resúmenes existentes que ya responden eso. Un método público o bloque complejo recibe comentario solo si su nombre no aclara una decisión importante del flujo; nunca se traduce línea por línea. No copiar el nombre del tipo como única descripción, no describir `using`, no incluir secretos ni prometer comportamientos que la implementación no tenga.

## Tareas

1. **Inventario y muestra.** Revisar la convención existente y el flujo `ExternalLoginController → AccountGoogleService → GoogleMethodLinker`. Agregar comentarios útiles en esa muestra; comprobar con `git diff --check` y build de Application y Api. Commit `docs(code): explicar vinculacion de Google`.
2. **Domain.** Completar o mejorar resúmenes de entidades, reglas, errores y tipos de valor escritos a mano. Revisar el código y las fichas del área antes de describirlo. Commit por áreas coherentes `docs(code): explicar dominio de <area>`.
3. **Application.** Cubrir interfaces, servicios, modelos con semántica y helpers. Distinguir orquestación de reglas que viven en otro tipo. Commit por áreas coherentes `docs(code): explicar aplicacion de <area>`.
4. **Infrastructure.** Cubrir adaptadores, repositorios, lectores, configuración EF, migración/seed operativo y workers; no comentar migraciones generadas. Commit por áreas coherentes `docs(code): explicar infraestructura de <area>`.
5. **Api y hosts.** Cubrir controllers, middleware, filtros, composición y hosts, con énfasis en el origen de identidad/tenant y las responsabilidades de cada ruta. Commit por áreas coherentes `docs(code): explicar API de <area>`.
6. **Guía de lectura.** Crear `docs/guides/leer-backend.md` con un recorrido de un controller a un servicio, helper, puerto e infraestructura; explicar `Task<Result>`, guardas, UoW y navegación en Visual Studio usando Google como ejemplo. Commit `docs: explicar como leer un flujo del backend`.
7. **Auditoría y puerta.** Inventariar los archivos elegibles sin resumen, revisar que cada exclusión sea trivial o generada, confirmar mediante revisión de diffs que solo cambiaron comentarios/documentación, `git diff --check`, `dotnet build ArquitecturaBaseMultitenant.slnx` sin advertencias y `dotnet test` completo con Docker. Commit de ajustes finales `docs(code): cerrar guia de lectura del backend` si hace falta.

## Verificación de calidad

- Abrir una muestra por área y leer únicamente su `summary`: debe permitir decir qué hace el tipo y dónde seguir el flujo.
- En DTOs, enums y marcadores, explicar su función en el flujo; en las pruebas, el comportamiento que protegen. Evitar limitarse a traducir el nombre.
- En un archivo con varios tipos relevantes, documentar cada tipo que tenga responsabilidad propia.
- La documentación debe permanecer junto al tipo para aparecer en las ayudas de Visual Studio.

## Resultado de la ampliación

- Los 757 archivos elegibles tienen explicación inicial: 532 de `src/`, 222 de `tests/` y 3 de `tools/`. Esta ampliación documentó 258 archivos adicionales y conservó las explicaciones existentes.
- `AccessErrors` explica además los códigos estables y el motivo de cada rechazo. Los métodos reciben aclaraciones cuando aportan información que no resulta evidente del nombre.
- La auditoría de los commits C# confirma que solo cambiaron comentarios. Los cambios de seguridad que ya estaban en curso permanecen fuera de esos commits.
- `dotnet build ArquitecturaBaseMultitenant.slnx --artifacts-path .artifacts/comments-validation`: 0 advertencias y 0 errores. Se usó una carpeta separada porque la API abierta bloqueaba las DLL del destino habitual.
- `dotnet test --artifacts-path .artifacts/comments-validation --no-build --no-ansi --no-progress`: 1117 correctos, 0 errores, 0 omitidos; duración de 4 minutos y 10 segundos. `git diff --check` sin problemas.

# Comentarios de propósito del código backend — plan de implementación

> **Para agentes:** trabajar por capas en `main`, con rutas de `git add` explícitas. Este trabajo no cambia lógica; no se crean tests que solo comprueben la presencia de comentarios.

**Objetivo:** que una persona pueda abrir cada archivo C# escrito a mano del backend y entender la responsabilidad del tipo principal, su motivo y su lugar en el flujo.

**Alcance:** inventariar todos los `.cs` de `src/` de Api, Application, Domain, Infrastructure, AppHost y ServiceDefaults. Se excluyen por categoría las migraciones, archivos generados y artefactos `bin/obj`; entre los escritos a mano solo pueden quedar sin comentario los DTO, enums y marcadores cuyo nombre y miembros expliquen íntegramente su propósito. Al cierre se informa cuántos archivos se cubrieron y cuántos se excluyeron por cada categoría. En tests solo se aclaran fixtures o helpers cuyo propósito no resulte evidente del nombre y de las pruebas.

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
- No agregar comentarios a DTOs, enums o marcadores si el nombre y sus miembros ya cuentan toda la historia.
- En un archivo con varios tipos relevantes, documentar cada tipo que tenga responsabilidad propia.
- La documentación debe permanecer junto al tipo para aparecer en las ayudas de Visual Studio.

# Cómo leer un flujo del backend

No hace falta empezar por los `using` ni entender toda la solución. Elegí una acción que realiza una persona y seguí sus llamadas en este orden:

`Controller → interfaz del servicio → servicio → helper o regla de Domain → puerto → implementación de Infrastructure`

El controller traduce HTTP a una operación. El servicio coordina validaciones, reglas y guardado. Domain expresa conceptos y errores del negocio. Un puerto (`I…Repository`, `I…Reader`, `I…Service` de integración) dice qué necesita el servicio; Infrastructure implementa el acceso a base o proveedor externo.

## Ejemplo: vincular Google a una cuenta

1. [`ExternalLoginController`](../../src/ArquitecturaBaseMultitenant.Api/Controllers/Auth/ExternalLoginController.cs) recibe el callback de Google. Si el `state` protegido indica que la persona quiso vincular su cuenta, obtiene el usuario esperado y el usuario de la sesión y llama a `IAccountGoogleService.LinkAsync`.
2. [`IAccountGoogleService`](../../src/ArquitecturaBaseMultitenant.Application/Interfaces/Services/IAccountGoogleService.cs) muestra el contrato: recibe `LinkGoogleRequest` y devuelve `Task<Result>`. Todavía no dice cómo se implementa.
3. [`AccountGoogleService`](../../src/ArquitecturaBaseMultitenant.Application/Services/Identity/AccountGoogleService.cs) lee el ingreso externo temporal y lo cierra. Comprueba que vino de Google, que corresponde al usuario en sesión y que Google verificó el correo. Si falla una comprobación, devuelve un error de Domain.
4. Si todo es válido, el servicio abre una transacción con `IUnitOfWork` y delega en [`GoogleMethodLinker`](../../src/ArquitecturaBaseMultitenant.Application/Services/Identity/GoogleMethodLinker.cs). Ese helper bloquea la cuenta y los identificadores, evita asociar un ingreso que pertenece a otra persona, crea el método y prepara el aviso. Los repositorios y puertos que usa terminan en Infrastructure.

Este recorrido vincula Google a una **cuenta existente**. El registro o ingreso con Google siguen otro camino del mismo controller.

## Cómo leer las líneas

| En el código | Qué significa |
|---|---|
| `using …` | Nombres de otras piezas que usa el archivo. Leelos después, si necesitás seguir una llamada. |
| `namespace …` | Área y capa a la que pertenece el tipo. |
| `internal sealed class X(a, b) : IX` | Clase visible en su proyecto, que no se hereda; recibe `a` y `b` por inyección y cumple el contrato `IX`. |
| `Task<Result>` y `await` | Trabajo asincrónico que termina con éxito o con un error esperado. `Result<T>` también lleva un valor si hubo éxito. |
| `CancellationToken ct` | Permite cancelar la operación si termina la petición. |
| `if (…) return XErrors.Y` | Una condición que corta el flujo con un error conocido. Leé primero estas salidas y luego el camino que llega al último `return`. |
| `ExecuteInTransactionAsync(…, CommitPolicy.OnSuccess, ct)` | Agrupa la escritura en una transacción y confirma cuando el `Result` indica éxito. |
| `OperationLog.RunAsync(…)` | Envuelve la operación con el registro técnico previsto para servicios; la regla de negocio está dentro. |

## Una ruta rápida en Visual Studio

- Empezá por el método del controller y seguí solo las llamadas del recorrido que te interesa.
- **F12** abre la definición de un servicio, helper, interfaz o tipo de error. **Alt+F12** permite verla sin salir del archivo; **Mayús+F12** muestra quién usa esa pieza.
- Preguntate: ¿qué entra?, ¿qué puede fallar?, ¿qué cambia?, ¿dónde empieza la transacción? Cuando un método solo delega, seguí la pieza delegada.
- Si una clase tiene `/// <summary>`, leelo al pasar el cursor sobre el nombre. Resume la responsabilidad y orienta el siguiente salto; el código y los tests muestran el detalle.

Las reglas generales de carpetas y capas están en [`backend.md`](../architecture/backend.md); los recorridos del producto están en `docs/features/`.

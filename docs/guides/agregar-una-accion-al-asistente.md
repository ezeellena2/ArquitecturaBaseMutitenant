# Agregar una acción al Asistente

Sirve para que la IA pueda preparar crear, editar o eliminar algo de un área, y que la persona lo confirme en la pantalla de siempre. La regla y su porqué están en la [ADR 0068](../decisions/0068-acciones-del-asistente.md); leé también [Asistente](../features/asistente.md), [permisos](../rules/permisos.md), [capas y flujo](../rules/capas-y-flujo.md) y [tests](../rules/tests.md).

La mecánica (el puerto, el registro, las tres piezas de IA, la tarjeta y la batería genérica) se programa en la etapa «Acciones del asistente», antes de la E12. Esta receta vale desde entonces. Para una lectura sin escribir, seguí [agregar una herramienta](agregar-una-herramienta-al-asistente.md).

## Antes de empezar

- **¿Es una acción?** Lo es si crea, edita o elimina **un registro** en una pantalla que ya existe y que guarda por una ruta que ya existe.
  - Armar una definición entera (un formulario o un catálogo) sigue con su propuesta ([0057](../decisions/0057-la-ia-propone-y-una-persona-crea.md)).
  - Lo armado como dato, como las opciones de un catálogo, ya está declarado por tipo y no pide nada.
  - Sin pantalla donde confirmar, no hay acción: el asistente contesta «todavía no».
- **La IA nunca guarda.** La acción no tiene código que escriba: guarda la ruta de siempre cuando la persona confirma.

## Pasos

1. **Declarala en la fuente del área.**
   - **Dónde:** `Application/Services/<Área>/<Área>ActionSource.cs`, o la carpeta del área en su módulo. Implementa `IAssistantActionSource`, y sus `Definitions` son estáticas.
   - **Por cada acción:**
     - el id con versión (`suppliers.create`, versión 1) y la clase: crear, editar, eliminar u otra;
     - el requisito, **copiado del atributo de la ruta** que guarda;
     - el módulo, si es de un módulo, y la `SaveRoute` (`"POST api/suppliers"`);
     - cómo se confirma: `Dialog`, `Page`, `DraftCard` o `RemoveConfirmation`;
     - la pantalla (`ScreenKey`), de la lista cerrada de enlaces, y el efecto fuera del sistema;
     - los campos;
     - para editar o eliminar, qué lectura de la pantalla encuentra el registro.
   - **Registrala** en `<Área>Registration.cs` como `IAssistantActionSource`.
2. **Los campos son los de la pantalla,** no los del pedido HTTP: lo que la persona ve, y nada más.
   - **El tipo sale de la lista cerrada:** `Text`, `LongText`, `Integer`, `Decimal`, `Money`, `Date`, `Boolean`, `Email`, `Phone`, `TaxId`, `Choice`, `Choices`, `Record` o `Consent`, y cada campo lleva su límite.
   - **Las listas, al editar,** se cambian con «sumar» y «quitar».
   - **Un consentimiento** es `Consent`: llega sin tildar.
   - **No declares** claves, conexiones, URL ni campos de tenant o de usuario. No existen como tipo, y el test los rechaza por nombre.
3. **Resolvé y validá sin guardar.**
   - **`ListChoicesAsync`:** solo para un `Choice` (un rol, un formulario, un catálogo), con su reader, 20 por página y solo lo que quien pide puede dar.
   - **`ResolveAsync`:** convierte cada texto o referencia en un id. Usá los readers del área, con RLS, y la `Policy` que usa el `Guard` de la ruta. Si la regla todavía vive en el `Guard`, separala en una `Policy` pura y que la usen los dos. Sin coincidencia es un error con la ruta del campo, y con varias, una duda («Revisá»).
   - **`ValidateAsync`:** arma el mismo `*Request` que la pantalla y corre su validador.
   - **Las dependencias:** solo readers, validadores, `Policy`, `Resolver`, `ICurrentUser` y lecturas de permisos. Nunca `IUnitOfWork`, repositorios, `Writer`, `Guard` ni servicios: el test de arquitectura lo frena, y además la fuente corre en una transacción de solo lectura.
4. **Los textos:**
   - `Action.<id>@<versión>.Name`, `.Description` y `.Field.<campo>`, en es y en, en los resources del área;
   - la descripción le dice al modelo para qué sirve la acción, sin ejemplos de datos reales;
   - nada de prompts escritos a mano.
5. **La pantalla acepta una propuesta** (front). Sumale el adaptador `useAssistantProposal('<id>@<versión>', toFormValues)`. **Todo valor propuesto se dibuja:** si uno no entra en la pantalla, el adaptador rechaza la propuesta. Lo demás lo hace el hook:
   - lee la propuesta por id;
   - marca «Propuesta del asistente» y «Revisá»;
   - en editar, muestra «Lo que cambia», también lo que se quita;
   - saca de la propuesta la primera clave de idempotencia.

   Los selectores buscan por id lo elegido que no cargaron.
6. **Sus casos y su escenario:**
   - **Los casos de la acción:** uno válido, uno inválido, uno ambiguo, uno con instrucciones escondidas (en un adjunto o en un nombre) y uno con algo de otra organización.
   - **El escenario de la batería genérica,** en `TestFeatures`: prepara la organización, quién pide, el registro de destino y otra organización.
   - **Con eso, la batería** prueba sola los permisos, el aislamiento, la validación, que no escriba, la revocación en el medio, la versión y el guardado único. No escribas tests que la repitan.
7. **Calificala y ofrecela.**
   - La acción entra registrada y sin ofrecer.
   - Sus casos se califican con el modelo real, con el visto bueno del usuario, y se anota su costo.
   - Se ofrece en el despliegue siguiente ([0060](../decisions/0060-dos-versiones-conviven.md)).
8. **Para cambiarla, una versión nueva.** Sumar, quitar o cambiar un campo es `@2`:
   - el `@1` se sigue leyendo un despliegue más;
   - una propuesta vieja que ya no se puede abrir se ve «ya no disponible».

## Ejemplo: «Proveedores»

**Si es un catálogo a mano** de la E12, no hay que programar nada: lo cubren las acciones de las opciones de un catálogo.

**Si es un área programada** ([agregar un área](agregar-un-area.md)), con su diálogo «Nuevo proveedor», `POST api/suppliers` con `[HasPermission(Permissions.Suppliers.Manage)]` e `[Idempotent]`, y su `CreateSupplierRequest`:
- **`SupplierActionSource`** declara tres acciones:
  - `suppliers.create@1`: `Dialog`, con nombre (`Text`, 80), CUIT (`TaxId`), correo (`Email`), teléfono (`Phone`) y rubro (`Choice` de un catálogo);
  - `suppliers.edit@1`: los mismos campos, con su `Target`;
  - `suppliers.remove@1`: `RemoveConfirmation`.

  Las resuelve con `ISupplierReader` y valida con el validador de `CreateSupplierRequest`.
- **Los textos** `Action.suppliers.*`, en es y en.
- **El adaptador** del diálogo «Nuevo proveedor» y el de la confirmación de borrar.
- **Los casos y el escenario.**

No hace falta una herramienta, un prompt, una tarjeta ni tests a mano para la IA.

## Lo verifica

- **`AssistantActionDeclarationTests`:**
  - el id con versión es único;
  - el requisito es el de la ruta, y la ruta existe, escribe y, si crea, es idempotente;
  - los campos son de la lista cerrada y sin nombres prohibidos;
  - están los textos y los casos;
  - las dependencias no escriben.
- **`AssistantActionContractTests`:** la batería genérica, una teoría por acción.
- **En el front:** el adaptador dibuja todo lo propuesto y manda lo que se ve.

Al terminar la tarea van el build y los tests rápidos enteros, nunca filtrados ([AGENTS.md](../../AGENTS.md)), y la batería de integración y los tests del front de esa pantalla. La suite completa va en la puerta.

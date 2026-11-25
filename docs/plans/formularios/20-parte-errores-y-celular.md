# E20 · Cargar una respuesta desde la web: errores y celular (spec, parte)

**Estado:** pendiente · parte de la [spec de la E20](20-cargar-desde-la-web.md), que dice qué leer y cómo se cierra.

## Errores y sus textos

Cuándo sale cada uno está en la [parte de contrato](20-parte-contrato-datos-y-pasos.md#contrato). Los textos van en `FormsTexts.resx` y `.en.resx`, y los de `WhatsApp.Flow`, en `WhatsAppTexts.resx`.

| Código | Tipo | es | en |
| --- | --- | --- | --- |
| `Forms.Entry.VersionChanged` | Conflict | {0} tiene una versión nueva. Pasá a ella para enviar: lo que cargaste se conserva. | {0} has a new version. Switch to it to send: what you entered is kept. |
| `Forms.Entry.Deactivated` | Conflict | {0} se dio de baja y ya no se puede cargar. | {0} was deactivated and can no longer be filled in. |
| `Forms.Entry.NotForEmployees` | Conflict | {0} es solo para clientes: se responde por WhatsApp. | {0} is for customers only: it's answered on WhatsApp. |
| `Forms.Entry.QuestionNotFound` | NotFound | Esa pregunta no es de este formulario. | That question isn't part of this form. |
| `Forms.Entry.AnswersInvalid` | Validation (`questions.<id>`; en un ítem, `questions.<bloque>.<n>.<pregunta>`) | Revisá las respuestas marcadas. | Check the highlighted answers. |
| `Forms.Entry.TooLarge` | PayloadTooLarge | Lo que mandás pasa de 100 MiB. Sacá algún archivo y volvé a enviar. | What you're sending is over 100 MiB. Remove a file and send it again. |
| `Forms.Draft.DependentListInvalid` | Validation (`questions[i].recipientFilters[j]`) | Tiene que ser una pregunta anterior que elija de «{0}». | It has to be an earlier question that picks from «{0}». |
| `Forms.Draft.DependentListForCustomers` | Validation (`questions[i]`) | Una lista dependiente no se puede usar en un formulario solo para clientes. | A dependent list can't be used in a form for customers only. |
| `Forms.Draft.AppOnlyForCustomers` | Validation (`appOnly`) | Los clientes responden solo por WhatsApp. | Customers answer only on WhatsApp. |
| `WhatsApp.Flow.DependentListNotSupported` | Validation (`questions[i]`) | Usa una lista dependiente, y WhatsApp no las muestra. | It uses a dependent list, and WhatsApp doesn't show them. |
| `WhatsApp.Flow.TooManyLocations` | Validation (`questions[i]`) | Por WhatsApp se pide una sola ubicación. | Only one location can be requested on WhatsApp. |
| `Forms.Record.LocationInvalid` | Validation (`questions.<id>`) | Revisá la ubicación y volvé a pedirla. | Check the location and ask for it again. |
| `Forms.Entry.UploadBusy` | TooManyRequests (`Retry-After`) | Ya estás enviando una respuesta. Esperá a que termine. | You're already sending a response. Wait for it to finish. |
| `Forms.Entry.UploadQueueFull` | TooManyRequests (`Retry-After`) | Se están enviando muchas respuestas. Probá de nuevo en un momento. | Many responses are being sent. Try again in a moment. |
| `Forms.Entry.UploadTimedOut` | Validation | La respuesta tardó demasiado en subir. Volvé a enviarla. | The response took too long to upload. Send it again. |

Cada error de `AnswersInvalid` lleva el código de la captura en su pregunta, también `Forms.Record.OptionWithdrawn`, `FileRejected` y `RowsInvalid`, y `Forms.Offer.RecipientDataMissing`, con el nombre del dato.

## Desde el celular

Como allá, un Android entra por USB con el reenvío de puertos de `chrome://inspect` (el 5174, a `localhost:5174`), sin exponer nada en internet. Acá se abre `https://localtest.me:5174`, que resuelve a 127.0.0.1 también en el teléfono, con un subdominio por organización y la carga en el dominio principal; el proxy de Vite recibe el `Host` tal cual. Si el DNS del teléfono filtra las direcciones privadas, se prueba con los datos móviles o sin el DNS privado de Android. El certificado de desarrollo no cubre `localtest.me`: Chrome avisa una vez por host, y se sigue con «Configuración avanzada». Si el ingreso no anda así, se anota en el plan, sin cambiar la configuración. Con iPhone no hay reenvío desde Windows.

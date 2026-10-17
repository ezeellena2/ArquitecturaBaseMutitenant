# E14 · Fotos y documentos (resumen)

**Estado:** resumen del 2026-10-17, armado desde el [traspaso](../2026-10-17-formularios-traspaso/README.md). Corresponde a la etapa 2B de ArquitecturaBase, que cerró el 2026-10-15 con Meta y Azure reales. Se planifica con `/plan-etapa` cuando le toque, después de la E13.

**Dónde está el detalle:**
- en el traspaso: [04 §2B](../2026-10-17-formularios-traspaso/04-etapas.md), anexo C §2 (etapa 2B), anexo B (E2B) y anexo D (F1–F5);
- en ArquitecturaBase: `docs/features/formularios.md` y `docs/features/whatsapp.md`, en `origin/main`.

## Qué se construye

- **Back:**
  - **Tipos «Foto» y «Documento»:**
    - Foto: «Cámara o galería» o «Solo cámara»;
    - Documento: PDF, Excel, Word, JPEG y PNG;
    - mínimo y máximo por pregunta; obligatoria quiere decir mínimo 1;
    - no se publica si la suma de los máximos pasa de 10.
  - **Tope por archivo:** uno de la instalación, de 1 a 25 MiB, y se sella al publicar.
  - **Flow:** `PhotoPicker` y `DocumentPicker`, una pregunta de archivo por pantalla. Los IDs de pantalla son solo letras y guiones bajos, con un perfil nuevo.
  - **Captura con archivos:**
    - baja cada archivo de la API de medios, solo por HTTPS desde hosts de Meta y sin redirecciones;
    - verifica hash, tamaño y tipo real, con las dimensiones antes de decodificar;
    - arma la miniatura y sube con clave determinística;
    - confirma todo junto con la respuesta: si algo falla, nada se guarda a medias;
    - tiene 90 s por captura, y reintenta a los 30, 90, 210, 450 y 930 s.
  - **Acceso:**
    - miniatura y original solo por la Api, con `formrecords.read` y pertenencia, `no-store` y `nosniff`;
    - los documentos siempre se descargan, con un nombre que lleva la extensión del tipo verificado;
    - tiene un cupo propio de lecturas.
  - **Limpieza** de archivos huérfanos cada hora.
- **Front:**
  - en el editor, los dos tipos y el contador «Archivos: hasta N de 10»;
  - en la tarjeta del chat, «bajando» o «reintentando», guardada (con miniaturas y «+N») o rechazada (con «Mandar de nuevo»);
  - el visor común: anterior, siguiente, tira de miniaturas y Descargar.
- **Tablero:** F1 a F5 de ArquitecturaBase, con la disposición copiada (decisión 18).

## Decisiones tomadas (no se vuelven a preguntar)

- **Formatos:** sin CSV (Meta no lo admite), video ni comprimidos.
- **Límites de Meta:** hasta 10 archivos y 100 MiB por respuesta.
- **Nada a medias:** si un archivo no baja tras los reintentos, o no coinciden hash, tamaño o tipo, la respuesta queda rechazada y visible, y se puede mandar de nuevo.
- **Reintentos simples:** bajar de Meta y subir con clave determinística son operaciones idempotentes, sin protocolo durable. Meta guarda los archivos hasta 20 días.
- **Los archivos siguen a la respuesta,** no a los 90 días del texto de WhatsApp.
- **Fotos:** JPEG y PNG se muestran en línea; la miniatura se arma al guardar y el original se baja solo al abrir el visor.
- **Privacidad:** nombres y contenidos nunca van a logs, a la lista de chats ni a los avisos.
- **Sin almacenamiento,** los tipos se ven pero no se eligen.

## Fuera de esta etapa

- Exportar los archivos y descargar todas las fotos juntas.
- El antivirus, que es requisito antes de producción y se decide con la operación.
- Video, comprimidos y CSV.

## Cómo se cierra

La puerta de la etapa, más una **prueba real con Meta y el almacenamiento de la E10**:
1. una respuesta con cámara, un PDF y una pregunta de archivo opcional vacía (mirar qué manda Meta: en ArquitecturaBase la omitía);
2. las descargas íntegras, con el tamaño y el SHA-256 iguales a los guardados;
3. un rechazo y el reenvío manual, sin saltos de numeración.

## Requisitos previos

- La E12 y la E13 cerradas.
- **De la E10:** el adaptador de nube de `IFileStore`. En desarrollo alcanza con `LocalFileStore` o el emulador que elija la E10.

## Ajustes ya decididos

- **Idioma:** solo el original es obligatorio, sin mezclar al mandar (decisión 17).
- **Pantallas:** se copian de ArquitecturaBase (decisión 18).
- **Cuánto se guardan los archivos:** sin plazo mientras exista la organización (decisión 23 de la hoja de ruta).

## En el multitenant

- **Datos de la organización:** `FormRecordFiles` (respuesta, pregunta, orden, clave, nombre, tipo, tamaño, SHA-256 y miniatura) es `ITenantOwned`, con RLS.
- **Almacenamiento (E10):** los archivos van en `IFileStore`, con un prefijo o un contenedor por organización (lo fija la E10). La limpieza usa su diario (`IFileStoreMaintenance`).
- **Workers:**
  - la captura y la limpieza entran a la organización con `ExecutionActor`, que usa `ITenantScope` por dentro (T5 de la E12);
  - la limpieza recorre las organizaciones con `TenantJobRunner`, o un registro técnico sin datos de negocio.
- **Feature `forms`:** las rutas nuevas llevan `[FeatureGate("forms")]`.
- **Permisos:** ninguno nuevo; ver y descargar pide `formrecords.read`.
- **WhatsApp (E11):** la API de medios se llama con el token de la WABA de la organización.

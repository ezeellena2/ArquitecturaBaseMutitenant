# E14 · Texto para el chat de Diseño

**Estado:** pendiente · Diseño lo toma en su turno.

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso los tableros de la E14 que cambian con el traspaso 10, para el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** Tres decisiones del 07/11 cambian cómo se publica y se manda un formulario: el estado de la publicación se dice una vez, en la etiqueta del encabezado, sin pasos (175); la vista oficial de WhatsApp se recorre en un diálogo (176), y el mensaje que acompaña al formulario lo nombra (165). Valen las fichas del front y las de toda la app del traspaso 10 (160 a 164, 173 a 175 y 180): lo que carga dibuja su encabezado y el aro, sin filas esqueleto; una acción que no se deshace pide confirmar, con el foco en «Cancelar»; cambiar algo no pierde lo escrito sin avisar; todo el texto a la vista, sin cortar con «…»; sin el cuadrado del logo, pastillas ni sombras fuera de los menús; la escala de ArquitecturaBase, con la letra y las filas más chicas y las esquinas de 3 px como mucho, y el estado dicho una vez, arriba.

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`: la spec `docs/plans/formularios/14-formularios.md`, §9 y §15, y en su parte de decisiones y flujos, la §6, puntos 4 a 6. Para mirar cómo quedó allá, en `ArquitecturaBaseFront`: `docs/design/formularios-whatsapp.md`, «El estado de la publicación y la vista oficial: EP1-A y VO1-A» y «El mensaje nombra el formulario», y `visual-baseline.md`, «El estado, una vez». Se copia el armado, con nuestras piezas (126).

**Qué se dibuja:**
1. **`FRM-Publicando` y `M-FRM-Editor`** (se redibujan): salen la zona con los pasos (Aceptada, Enviada a WhatsApp, WhatsApp la revisa y Lista para mandar), la ⓘ que la abría y el segmentado «Borrador | Versión N en WhatsApp».
   - La etiqueta del encabezado dice el estado: «Publicando la versión 4», «Publicado · versión 4», «Sin confirmar · versión 4», «Necesita revisión · versión 4» y «No se pudo publicar la versión 4».
   - En la banda, solo cuando hacen falta: «Consultar publicación», sin confirmar y en revisión, que dice «Consultando…» con su aro; «Repetir el mismo intento», cuando el pedido de publicar quedó sin respuesta, y, con «Necesita revisión», «Salió» y «No salió».
   - «Publicar en WhatsApp» apagado, con su motivo en el tooltip: una publicación en curso, sin confirmar o en revisión, o un pedido sin respuesta.
   - Lo rechazado queda marcado en su pregunta, sin una zona «Problemas»; la cuenta de WhatsApp sin medio de pago va en el aviso importante, con su ⓘ.
   - «Ver vista oficial» va en la banda cuando hay una versión publicada. Al costado queda «Así lo ve quien lo recibe», con el borrador.
   - En el teléfono no hay hojas: las acciones van en el pie fijo, y el motivo de lo apagado, en la franja oscura.
2. **`FRM-Vista-Oficial` y `M-FRM-Vista-Oficial`** (nuevos): el diálogo «Vista oficial · versión 4», con el marco de Meta de 480 × 840 achicado entero para entrar en la ventana, sin barras ni cortes. Arranca en la primera pantalla y se recorre como en el celular, con «Continuar»; «Motivo» trae sus primeras 20 opciones. Lleva «Abrir en otra pestaña» y «Cerrar». Cargando, vencida («La vista oficial venció.», con «Cargarla de nuevo») y con error conservan la medida del marco. En el teléfono va a pantalla completa, también a 320.
3. **`WA-Chats-Formulario` y `WA-Chats-Formulario-Cliente`, con sus teléfonos** (se redibujan): al elegir «Visita a cliente», el mensaje propone «Completá el formulario «Visita a cliente».». Elegir otro cambia el nombre, pero lo que escribió el operador no se pisa: dibujá los dos casos.
4. **`WA-Chats-Respuesta`, `M-WA-Conversacion-Respuesta`, `WA-Mensajes-14` y `M-WA-Mensaje-14`** (se redibujan): el mensaje enviado dice «Completá el formulario «Visita a cliente».», en lugar de «Completá este formulario, por favor.».

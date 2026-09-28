# Plantillas de WhatsApp: cuándo y cómo crearlas

## Qué son y por qué hacen falta
- **Plantilla:** WhatsApp (Meta) solo deja que una empresa **le escriba primero** a alguien con una plantilla **aprobada**: un texto fijo con variables (`{{1}}`, `{{2}}`…).
- **Mensaje libre:** solo se puede mandar dentro de las **24 horas** posteriores a que la persona nos escribió. Por eso la respuesta del bot de ingreso (el enlace para entrar) no necesita plantilla: la persona acaba de escribir.
- **Aprobación:** cada plantilla la aprueba Meta. Tarda de minutos a 48 horas, y puede rechazarla.
- **Consecuencia:** una plantilla se crea **antes de programar** el envío que la usa, no al final.

## Cuándo se crea cada una
**Regla:** una plantilla nace en la etapa que implementa su mensaje. Es el primer paso de esa tarea, antes de escribir el código, así la aprobación de Meta corre mientras se programa.

| Plantilla | Categoría | Variables | Para qué | Etapa |
|---|---|---|---|---|
| `codigo_ingreso` | Authentication | el código | ingresar, registrarse, verificar un método nuevo o confirmar un cambio | E8 (ya existe en la cuenta de ArquitecturaBase) |
| `invitacion_organizacion` | Utility | quien invita, la organización y el vencimiento; botón con el enlace | invitación a una organización. **Reemplaza a** `invitacion_acceso` de ArquitecturaBase, que no nombra la organización | E8 |
| `aviso_metodo_ingreso` | Utility | qué pasó (agregado, quitado o principal), el método enmascarado y la fecha | aviso en todos los métodos cuando cambia uno (multitenancy §3.1) | E8 |
| `revisa_metodos_ingreso` | Utility | la organización | terminó una membresía y sus correos administrados dejaron de servir | E8 |
| `baja_cuenta_pedida` | Utility | la fecha de eliminación | se pidió la baja (§3.2) | E8 |
| `baja_cuenta_cancelada` | Utility | — | se canceló la baja | E8 |
| `cuenta_eliminada` | Utility | — | se completó la baja | E8 |

- **Lo que va solo por correo:** la exportación de datos, porque el enlace de descarga no va por WhatsApp.
- **Un mensaje nuevo que un producto quiera mandar por WhatsApp:** se agrega a esta tabla con su etapa, y después se crea la plantilla.

## Cómo se crea
1. **Abrir el administrador:** en [Meta Business Suite](https://business.facebook.com/), andá a **WhatsApp Manager → Administrar plantillas → Crear plantilla**, con la cuenta de WhatsApp Business del número que corresponde. En desarrollo es el número de prueba que comparte ArquitecturaBase; en producción, el del multitenant ([configuracion.md](configuracion.md) §4).
2. **Categoría:**
   - **Authentication** solo para códigos: Meta arma el texto y agrega el botón "Copiar código";
   - **Utility** para avisos de la cuenta;
   - **nunca Marketing**.
3. **Nombre:** en minúsculas y `snake_case`, igual al de la tabla. Es el mismo nombre en los dos idiomas.
4. **Idiomas:** español (`es`) e inglés (`en`), los mismos que usa la plataforma. Cada idioma se aprueba por separado.
5. **Texto:**
   - **Contenido:** corto, en el tono de los correos (`Notifications.resx`), con voseo y sin datos sensibles: ni correos ni teléfonos completos. El único dato sensible permitido es el código, y solo en Authentication.
   - **Variables:** cada una con un **ejemplo**, porque Meta lo pide para aprobar.
   - **Enlace:** si lleva uno, va como **botón de URL con variable**: la parte fija es `https://plataforma.com/` y la variable es el resto.
6. **Aprobación:** enviar a revisión y esperar el estado **Activa**. Si Meta la rechaza, se corrige el texto; lo habitual es que falte un ejemplo o que el texto parezca promoción.
7. **Registro en la configuración:**
   - en `appsettings.json`, bajo `WhatsApp:Templates:<Nombre>`, como hace ArquitecturaBase (`LoginCode`, `Invitation`);
   - en el catálogo de plantillas del módulo, con el orden de sus variables;
   - un test verifica que cada plantilla del catálogo esté configurada y que el orden de las variables coincida.
8. **Prueba:** mandarla al número de prueba desde el flujo real y verificar en el webhook que llegue el estado `delivered`.

## Reglas
- **Una plantilla aprobada no se edita:** se crea otra con sufijo (`aviso_metodo_ingreso_v2`), se cambia la configuración y se borra la vieja cuando ya nadie la usa. Editarla la vuelve a revisión y puede cortar los envíos.
- **Mismas plantillas en los dos ambientes:** las de desarrollo y las de producción tienen el mismo nombre y el mismo texto. Al pasar a producción se vuelven a crear en la cuenta nueva.
- **Nunca un mensaje libre fuera de la ventana de 24 horas:** Meta lo rechaza.
- **El envío pasa por el outbox**, cifrado, como el correo ([backend.md §15](../architecture/backend.md#15-whatsapp-un-módulo-quitable)).

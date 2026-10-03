import { contextoDe } from '../../comun/hallazgos.mjs';
import { lineas, lineasPlaneadas } from '../../comun/markdown.mjs';
import { mapaDeCapas } from '../../comun/rutas.mjs';
import { parecidos } from '../../comun/similitud.mjs';

export const nombre = 'simbolos';

const sufijos = 'Service|Repository|Reader|Controller|Errors|Tests|Policy|Request|Response|Row|Guard|Issuer|Verifier|Linker|Validator|Configuration|Options|Interceptor|Factory|Provider|Extensions|Attribute|Query|Middleware|Formatter|Catalog|Handler|Scope|Filter|Registration|Settings|Page|Form|Dialog|Layout|Banner|Button|Panel|Table|Gate|Boundary|Context';
const conSufijoDelRepo = new RegExp(`^I?[A-Z][A-Za-z0-9]*(?:${sufijos})$`);
// En el front también son símbolos del repo los hooks (`useAccess`).
const conFormaDelRepo = { test: (n) => conSufijoDelRepo.test(n) || /^use[A-Z][A-Za-z0-9]+$/.test(n) };
const conMiembro = /^([A-Z][A-Za-z0-9]*)\.([A-Za-z_]\w*)(?:\(.*\))?$/;

// Tipos del framework o de paquetes que se nombran en los docs y no viven en el repo.
const delFramework = new Set([
  'DbContextOptions', 'IOptions', 'IOptionsMonitor', 'IOptionsSnapshot', 'IHostedService', 'IServiceProvider', 'IMemoryCache',
  'IHttpContextAccessor', 'IExceptionHandler', 'IAuthorizationHandler', 'IAuthorizationPolicyProvider', 'ILoggerProvider',
  'IClaimsTransformation', 'IMiddleware', 'IActionFilter', 'IAsyncActionFilter', 'IResultFilter', 'ActionFilterAttribute',
  'AuthorizeAttribute', 'IEntityTypeConfiguration', 'IModelCacheKeyFactory', 'IDbContextFactory', 'ProblemDetailsFactory',
  'WebApplicationFactory', 'IClassFixture', 'ValidationAttribute', 'IWebHostEnvironment', 'IHostEnvironment', 'IDistributedCache',
  'HybridCacheEntryOptions', 'JsonSerializerOptions', 'CultureInfo', 'IStringLocalizer', 'IStringLocalizerFactory',
  'TimeProvider', 'FakeTimeProvider', 'BackgroundService', 'TreatWarningsAsErrors', 'UseExceptionHandler',
  'RequestLocalizationOptions', 'AuthenticationOptions', 'IdentityOptions', 'CookieAuthenticationOptions',
]);

// Un nombre de archivo (`Email.cs`) no es un miembro.
const extensionesDeArchivo = new Set(['cs', 'ts', 'tsx', 'md', 'json', 'mjs', 'resx', 'yml', 'ps1', 'html', 'txt', 'csproj', 'props', 'slnx']);

const esInterfaz = (n) => /^I[A-Z]/.test(n);

export async function detectar(ctx) {
  // Nombres de proyecto (`ArchitectureTests`, `Api.IntegrationTests`): terminan en un sufijo del repo y no son tipos.
  const proyectos = new Set([...mapaDeCapas(ctx.archivos).keys()].flatMap((k) => k.split('.')));
  const propios = await ctx.indiceSimbolos();
  const ajenos = await ctx.indiceSimbolosHermano();
  const existe = (n) => propios.existe(n) || (ajenos?.existe(n) ?? false);
  const todos = () => [...new Set([...propios.tipos.keys(), ...(ajenos ? ajenos.tipos.keys() : [])])];
  const out = [];

  for (const archivo of ctx.archivos.archivos) {
    if (!archivo.endsWith('.md') || ctx.excluido(archivo)) continue;
    const texto = await ctx.leer(archivo);
    const planeadas = lineasPlaneadas(texto, ctx.etapasCerradas);
    for (const l of lineas(texto)) {
      if (l.codigo || planeadas.has(l.n)) continue;
      for (const m of l.contenido.matchAll(/`([^`\n]+)`/g)) {
        const valor = m[1].trim();
        const conM = valor.match(conMiembro);
        const tipo = conM ? conM[1] : valor;
        if (!conM && !/^[A-Za-z0-9]+$/.test(valor)) continue;
        if (delFramework.has(tipo) || (conM && extensionesDeArchivo.has(conM[2]))) continue;

        // Solo se verifican los miembros de tipos con forma del repo: `Legal.X` o `Companies.X` suelen ser códigos o nombres de tabla.
        if (conM && existe(tipo) && !conFormaDelRepo.test(tipo)) continue;
        if (conM && existe(tipo)) {
          const lugar = propios.existe(tipo) ? propios : ajenos;
          if (!(await lugar.tieneMiembro(tipo, conM[2]))) {
            out.push(ctx.crear({
              archivo, linea: l.n, tipo: 'miembro-inexistente', clave: `${tipo}.${conM[2]}`,
              real: `${tipo} no tiene un miembro ${conM[2]}`, esperado: lugar.archivosDe(tipo).join(', '),
              contexto: contextoDe(texto, l.n), accion: 'modelo',
            }));
          }
          continue;
        }
        if (existe(tipo)) continue;
        if (!conFormaDelRepo.test(tipo) || proyectos.has(tipo)) continue;

        const cercanos = parecidos(tipo, todos(), { tolerancia: 3 }).filter((c) => c.nombre !== tipo);
        const unico = cercanos.length === 1 || (cercanos.length > 1 && cercanos[0].distancia < cercanos[1].distancia);
        const base = { archivo, linea: l.n, tipo: 'simbolo-inexistente', clave: tipo, real: `${tipo} no existe en el código`, contexto: contextoDe(texto, l.n) };
        // Una interfaz y una clase con nombre parecido no son un typo: si no son del mismo tipo, decide el modelo.
        // Tampoco con otro sufijo (`...Tests` por `...Texts`): el rol del tipo no cambia por un typo.
        const sufijoDe = (n) => n.match(new RegExp(`(${sufijos})$`))?.[1];
        const mismoTipo = cercanos.length > 0 && esInterfaz(tipo) === esInterfaz(cercanos[0].nombre) && sufijoDe(tipo) === sufijoDe(cercanos[0].nombre);
        if (unico && mismoTipo && cercanos[0].distancia <= 2 && tipo.length >= 8) {
          const nuevo = valor.replace(tipo, cercanos[0].nombre);
          out.push(ctx.crear({ ...base, esperado: cercanos[0].nombre, accion: 'auto', parche: { tipo: 'reemplazar', archivo, linea: l.n, de: `\`${valor}\``, a: `\`${nuevo}\`` } }));
        } else {
          out.push(ctx.crear({ ...base, candidatos: cercanos.map((c) => c.nombre), accion: 'modelo' }));
        }
      }
    }
  }
  return out;
}

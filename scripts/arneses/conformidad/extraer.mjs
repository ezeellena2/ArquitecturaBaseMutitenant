/** Qué nombre nuevo trae cada cambio, por clase. Funciones puras sobre un archivo de `parsearDiff` o un texto. */

export const ERROR = /"([A-Z][A-Za-z0-9]*(?:\.[A-Z][A-Za-z0-9]*){2,})"/g;
export const PERMISO = /"([a-z][a-z0-9-]*(?:\.[a-z][a-z0-9-]*)+)"/g;
export const FUNCIONALIDAD_DEFINIDA = /\bnew(?:\s+FeatureDefinition)?\s*\(\s*"([a-z][a-z0-9-]*)"/g;
export const FUNCIONALIDAD_CLAVE = /\bconst\s+string\s+Key\s*=\s*"([a-z][a-z0-9-]*)"/g;
// `path: "/org/…"` en el router o en un manifiesto, o una constante `…Path = "/avisos"`.
export const RUTA_FRONT = /\b(?:path|[a-z]\w*Path)\s*[:=]\s*["'`](\/[^"'`$\s]*)["'`]/g;

export const esCodigoDeError = (ruta) => /^src\/.*Errors\.cs$/.test(ruta);
export const esCatalogoDePermisos = (ruta) => /^src\/.*Domain\/Authorization\/[^/]*Permissions\.cs$/.test(ruta);
export const esMigracion = (ruta) => /^src\/.*\/Migrations\/[^/]+\.cs$/.test(ruta) && !/(\.Designer|ModelSnapshot)\.cs$/.test(ruta);
export const esController = (ruta) => /^src\/.*Controller\.cs$/.test(ruta);
export const esDefinicionDeFuncionalidad = (ruta) => /^src\/.*FeatureDefinitionContributor\.cs$/.test(ruta);
export const esClaveDeFuncionalidad = (ruta) => /^src\/.*Feature\.cs$/.test(ruta);
// Las rutas del router: código del front, sin tests, galerías ni mocks, que también tienen `path:`.
export const esCodigoDelFront = (ruta) => /^src\/.*\.tsx?$/.test(ruta) && !/\.(test|spec)\.tsx?$/.test(ruta)
  && !/(^|\/)(test|mocks?|__mocks__|fixtures?)\//.test(ruta) && !/gallery/i.test(ruta);

/** Un componente compartido nuevo: un archivo que nace en `src/shared/ui/`, sin tests, historias ni índices. Devuelve su nombre. */
export function componenteNuevo(archivo) {
  if (!archivo.nuevo || !/^src\/shared\/ui\/.+\.[jt]sx?$/.test(archivo.ruta)) return null;
  if (/\.(test|spec|stories)\.[jt]sx?$/.test(archivo.ruta) || /(^|\/)index\.[jt]sx?$/.test(archivo.ruta) || /\.d\.ts$/.test(archivo.ruta)) return null;
  return archivo.ruta.split('/').at(-1).replace(/\.[jt]sx?$/, '');
}

/** El índice del cierre que corresponde a la apertura en `desde`, sin contar lo que va entre comillas. */
function cierre(texto, desde) {
  const abre = texto[desde];
  const cierra = { '(': ')', '{': '}', '[': ']' }[abre];
  let nivel = 0;
  for (let i = desde; i < texto.length; i += 1) {
    const c = texto[i];
    if (c === '"') {
      for (i += 1; i < texto.length && texto[i] !== '"'; i += 1) if (texto[i] === '\\') i += 1;
    } else if (c === abre) nivel += 1;
    else if (c === cierra && (nivel -= 1) === 0) return i;
  }
  return texto.length;
}

const argumento = (args, nombre) => args.match(new RegExp(`\\b${nombre}\\s*:\\s*"([^"]+)"`))?.[1] ?? null;

/**
 * Las tablas (`CreateTable`, con sus columnas) y las columnas sueltas (`AddColumn`) del `Up` de una migración nueva.
 * El `Down` no cuenta: el de una migración que borra vuelve a crear la tabla.
 */
export function cambiosDeMigracion(archivo) {
  const fin = archivo.agregadas.findIndex((l) => /\bvoid\s+Down\s*\(/.test(l.texto));
  const up = fin === -1 ? archivo.agregadas : archivo.agregadas.slice(0, fin);
  const texto = up.map((l) => l.texto).join('\n');
  const lineaDe = (indice) => up[texto.slice(0, indice).split('\n').length - 1]?.n ?? 0;
  const tablas = [];
  const columnas = [];
  for (const m of texto.matchAll(/\bCreateTable\s*\(/g)) {
    const abre = m.index + m[0].length - 1;
    const args = texto.slice(abre + 1, cierre(texto, abre));
    const enColumnas = args.search(/\bcolumns\s*:/);
    const cabeza = enColumnas === -1 ? args : args.slice(0, enColumnas);
    const tabla = argumento(cabeza, 'name');
    if (!tabla) continue;
    const propias = [];
    const llave = enColumnas === -1 ? -1 : args.indexOf('{', enColumnas);
    if (llave !== -1) {
      const bloque = args.slice(llave + 1, cierre(args, llave));
      const base = abre + 1 + llave + 1;
      for (const c of bloque.matchAll(/(\w+)\s*=\s*table\.Column\s*<[\s\S]*?>\s*\(/g)) {
        const ap = c.index + c[0].length - 1;
        propias.push({ nombre: argumento(bloque.slice(ap + 1, cierre(bloque, ap)), 'name') ?? c[1], linea: lineaDe(base + c.index) });
      }
    }
    tablas.push({ tabla, esquema: argumento(cabeza, 'schema'), linea: lineaDe(m.index), columnas: propias });
  }
  for (const m of texto.matchAll(/\bAddColumn\s*<[\s\S]*?>\s*\(/g)) {
    const abre = m.index + m[0].length - 1;
    const args = texto.slice(abre + 1, cierre(texto, abre));
    const nombre = argumento(args, 'name');
    const tabla = argumento(args, 'table');
    if (nombre && tabla) columnas.push({ tabla, esquema: argumento(args, 'schema'), nombre, linea: lineaDe(m.index) });
  }
  return { tablas, columnas };
}

const METODOS = new Set(['get', 'post', 'put', 'patch', 'delete']);

function operaciones(texto) {
  let doc;
  try {
    doc = texto ? JSON.parse(texto) : {};
  } catch {
    doc = {};
  }
  return new Set(Object.entries(doc?.paths ?? {}).flatMap(([ruta, ops]) => Object.keys(ops ?? {}).filter((m) => METODOS.has(m)).map((m) => `${m.toUpperCase()} ${ruta}`)));
}

/** Las operaciones que el contrato exportado tiene después y no antes, con la línea de su ruta. */
export function rutasNuevasDeOpenApi(antes, despues) {
  const viejas = operaciones(antes);
  const lineas = (despues ?? '').split(/\r?\n/);
  const out = [];
  for (const op of operaciones(despues)) {
    if (viejas.has(op)) continue;
    const [metodo, ruta] = op.split(' ');
    const linea = lineas.findIndex((l) => l.trimStart().startsWith(`${JSON.stringify(ruta)}:`)) + 1;
    out.push({ metodo, ruta, linea });
  }
  return out;
}

const ATRIBUTO_HTTP = /\[Http(Get|Post|Put|Patch|Delete)(?:\(\s*"([^"]*)"[^)]*\))?\]/g;

function componer(prefijo, plantilla) {
  if (/^~?\//.test(plantilla)) return plantilla.replace(/^~/, '');
  return `/${[prefijo, plantilla].map((p) => p.replace(/^\/+|\/+$/g, '')).filter(Boolean).join('/')}`;
}

/** Sin contrato exportado: los `[Http*]` agregados a un controller, con el `[Route]` de su clase (en el archivo entero del commit). */
export function rutasDeController(archivo, textoCompleto) {
  const clase = (textoCompleto ?? '').search(/\bclass\s+\w+/);
  const antesDeLaClase = clase === -1 ? '' : textoCompleto.slice(0, clase);
  const prefijo = [...antesDeLaClase.matchAll(/\[Route\(\s*"([^"]*)"\s*\)\]/g)].at(-1)?.[1] ?? '';
  const quitadas = new Set(archivo.quitadas.flatMap((t) => [...t.matchAll(ATRIBUTO_HTTP)].map((m) => `${m[1]} ${m[2] ?? ''}`)));
  const out = [];
  for (const { n, texto } of archivo.agregadas) {
    for (const m of texto.matchAll(ATRIBUTO_HTTP)) {
      if (quitadas.has(`${m[1]} ${m[2] ?? ''}`)) continue;
      out.push({ metodo: m[1].toUpperCase(), ruta: componer(prefijo, m[2] ?? ''), linea: n });
    }
  }
  return out;
}

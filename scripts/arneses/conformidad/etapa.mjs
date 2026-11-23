/**
 * Lo que comparten los detectores de conformidad: la clave del plan, los commits de la etapa con lo que agregaron y la
 * spec contra la que se comparan. Solo lee: el plan y la spec del working tree, y la historia de cada repo con git.
 */
import { existsSync } from 'node:fs';
import { readFile, readdir } from 'node:fs/promises';
import { basename, isAbsolute, join, relative } from 'node:path';
import { git } from '../comun/git.mjs';

export const CARPETA_SPECS = 'docs/plans/formularios';
export const CONTRATO = 'docs/contracts/openapi.json';

export const escapar = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
/** El nombre entero, sin distinguir mayúsculas: `FormCatalog` no aparece dentro de `FormCatalogItems`. */
export const palabra = (nombre) => new RegExp(`(?<![A-Za-z0-9_])${escapar(nombre)}(?![A-Za-z0-9_])`, 'i');

/** `docs/plans/2026-11-08-etapa-12-catalogos.md` → `12-catalogos`: el nombre sin la fecha ni `etapa-`. */
export function claveDelPlan(ruta) {
  return basename(ruta).replace(/\.md$/i, '').replace(/^\d{4}-\d\d-\d\d-/, '').replace(/^etapa-/, '');
}

/** Un `git show --unified=0`: por archivo, si es nuevo, las líneas agregadas con su número y las quitadas. */
export function parsearDiff(texto) {
  const archivos = [];
  let actual = null;
  let n = 0;
  let enHunk = false;
  for (const crudo of texto.split('\n')) {
    const l = crudo.replace(/\r$/, '');
    const cabecera = l.match(/^diff --git a\/.+ b\/(.+)$/);
    if (cabecera) {
      actual = { ruta: cabecera[1], nuevo: false, borrado: false, agregadas: [], quitadas: [] };
      archivos.push(actual);
      enHunk = false;
      continue;
    }
    if (!actual) continue;
    const hunk = l.match(/^@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@/);
    if (hunk) {
      n = Number(hunk[1]);
      enHunk = true;
    } else if (!enHunk) {
      if (l.startsWith('new file mode')) actual.nuevo = true;
      else if (l.startsWith('deleted file mode')) actual.borrado = true;
      else if (l.startsWith('+++ b/')) actual.ruta = l.slice(6);
    } else if (l.startsWith('+')) {
      actual.agregadas.push({ n, texto: l.slice(1) });
      n += 1;
    } else if (l.startsWith('-')) actual.quitadas.push(l.slice(1));
    else if (l.startsWith(' ')) n += 1;
  }
  return archivos.filter((a) => !a.borrado);
}

/** Los commits de `HEAD` con la línea `Tarea: <clave>/<id>`, del más viejo al más nuevo. Sin historia, ninguno. */
export async function commitsDeLaEtapa(raiz, clave) {
  let salida;
  try {
    salida = await git(raiz, ['log', '--no-merges', '--reverse', '-F', `--grep=Tarea: ${clave}/`, '--format=%H%x1f%h%x1f%B%x1e', 'HEAD']);
  } catch {
    return [];
  }
  const tarea = new RegExp(`Tarea: ${escapar(clave)}/(\\S+)`);
  return salida.split('\x1e').map((r) => r.trim()).filter(Boolean).map((r) => {
    const [sha, corto, mensaje = ''] = r.split('\x1f');
    return { sha, corto, tarea: mensaje.match(tarea)?.[1] ?? '?' };
  });
}

/** El archivo en una versión (`<sha>:<ruta>`), o null si no estaba. */
export async function leerEn(raiz, version, ruta) {
  try {
    return await git(raiz, ['show', `${version}:${ruta}`]);
  } catch {
    return null;
  }
}

async function cambiosDelCommit(raiz, sha) {
  return parsearDiff(await git(raiz, ['show', '--format=', '--unified=0', '--no-color', '--no-renames', '--no-ext-diff', '--no-textconv', sha]));
}

/** El juego de la spec: el plan, las specs que nombra su línea «Spec:» y las de su número, con sus partes (`NN-parte-*` o `<spec>-parte-*`). */
export async function leerSpec(raizBack, rutaPlan, clave) {
  const textoPlan = await readFile(rutaPlan, 'utf8');
  const carpeta = join(raizBack, CARPETA_SPECS);
  const disponibles = existsSync(carpeta) ? (await readdir(carpeta)).filter((a) => a.endsWith('.md')) : [];
  const cabecera = textoPlan.split(/\r?\n/).find((l) => /\bSpec:/.test(l)) ?? '';
  const elegidas = new Set([...cabecera.matchAll(/([A-Za-z0-9-]+\.md)/g)].map((m) => m[1]).filter((a) => disponibles.includes(a)));
  const numero = clave.match(/^(\d\d)-/)?.[1];
  if (numero) for (const a of disponibles) if (a.startsWith(`${numero}-`)) elegidas.add(a);
  for (const spec of [...elegidas]) {
    const tallo = spec.replace(/\.md$/, '');
    const prefijo = /^\d\d-/.test(tallo) ? `${tallo.slice(0, 2)}-parte-` : `${tallo}-parte-`;
    for (const a of disponibles) if (a.startsWith(prefijo)) elegidas.add(a);
  }
  const documentos = [{ archivo: relative(raizBack, rutaPlan).replaceAll('\\', '/'), texto: textoPlan }];
  for (const a of [...elegidas].sort()) documentos.push({ archivo: `${CARPETA_SPECS}/${a}`, texto: await readFile(join(carpeta, a), 'utf8') });
  return indexarSpec(documentos);
}

// Las columnas que pone cada tabla por su clase de dato, su auditoría o su versión: la spec no las repite.
const IMPLICITAS = new Set(['id', 'tenantid', 'businesstenantid', 'consumertenantid', 'ispublished', 'createdatutc', 'createdby', 'modifiedatutc',
  'modifiedby', 'isdeleted', 'deletedatutc', 'deletedby', 'version', 'xmin']);
const METODO = /\b(GET|POST|PUT|PATCH|DELETE)\b/g;
const normApi = (r) => r.trim().toLowerCase().replace(/\?.*$/, '').replace(/\{[^}]*\}/g, '{}').replace(/^\/+|\/+$/g, '');
const normFront = (r) => r.toLowerCase().replace(/\{[^}]*\}/g, ':').replace(/:[a-z0-9_]+/g, ':').replace(/\/+$/, '');

/** `FormCatalogs` también se nombra en singular. */
function variantes(tabla) {
  const v = new Set([tabla]);
  if (/ies$/i.test(tabla)) v.add(`${tabla.slice(0, -3)}y`);
  else if (/s$/i.test(tabla)) v.add(tabla.slice(0, -1));
  if (/es$/i.test(tabla)) v.add(tabla.slice(0, -2));
  return [...v];
}

/**
 * Lo que dice la spec de una tabla: la línea que la nombra y lo que la sigue. Un título, hasta el siguiente; una fila de
 * tabla, sola; un párrafo, entero y con la tabla de columnas que venga debajo.
 */
function bloquesDe(documentos, patrones) {
  const out = [];
  for (const ls of documentos) {
    for (let i = 0; i < ls.length; i += 1) {
      if (!patrones.some((p) => p.test(ls[i]))) continue;
      const bloque = [ls[i]];
      let j = i + 1;
      if (/^\s*#/.test(ls[i])) while (j < ls.length && !/^\s*#/.test(ls[j])) bloque.push(ls[j++]);
      else if (!/^\s*\|/.test(ls[i])) {
        while (j < ls.length && ls[j].trim() !== '') bloque.push(ls[j++]);
        let k = j;
        while (k < ls.length && ls[k].trim() === '') k += 1;
        if (k < ls.length && k - j <= 1 && /^\s*\|/.test(ls[k])) for (j = k; j < ls.length && /^\s*\|/.test(ls[j]); j += 1) bloque.push(ls[j]);
      }
      out.push(bloque.join('\n'));
    }
  }
  return out;
}

/** La spec como texto y con lo que hace falta para buscar cada clase de nombre en ella. */
export function indexarSpec(documentos) {
  const texto = documentos.map((d) => d.texto).join('\n');
  const lineas = documentos.map((d) => d.texto.split(/\r?\n/));
  const textoFront = normFront(texto);
  const porMetodo = new Map();
  const prefijos = new Set();
  const enBackticks = new Set([...texto.matchAll(/`([^`\n]+)`/g)].flatMap((m) => m[1].toLowerCase().match(/[a-z0-9_]+/g) ?? []));
  const sumar = (metodo, token) => {
    if (!porMetodo.has(metodo)) porMetodo.set(metodo, new Set());
    porMetodo.get(metodo).add(normApi(token));
  };
  for (const linea of lineas.flat()) {
    const metodos = [...linea.matchAll(METODO)].map((m) => m[1]);
    for (const [, crudo] of linea.matchAll(/`([^`]+)`/g)) {
      const conMetodo = crudo.match(/^(GET|POST|PUT|PATCH|DELETE)\s+(\S+)/);
      const token = conMetodo ? conMetodo[2] : crudo;
      if (conMetodo) sumar(conMetodo[1], token);
      for (const m of metodos) sumar(m, token);
      if (normApi(token).startsWith('api/')) prefijos.add(normApi(token));
    }
  }
  const nombraTabla = (tabla) => variantes(tabla).some((v) => palabra(v).test(texto));
  return {
    archivos: documentos.map((d) => d.archivo),
    nombra: (nombre) => palabra(nombre).test(texto),
    /** Una clave corta como `forms` solo cuenta entre comillas o backticks, no suelta en una ruta. */
    entreComillas: (nombre) => new RegExp(`[\`"']${escapar(nombre)}[\`"']`, 'i').test(texto),
    tabla: nombraTabla,
    /**
     * En el bloque de su tabla, o como `Tabla.Columna` en cualquier lado. Un nombre compuesto (`ConsentConfirmedAtUtc`)
     * vale también entre backticks en cualquier lado, porque la spec dice «la invitación suma…»; uno suelto (`Name`), no.
     * `Duenio_Campo` vale también por `Campo`.
     */
    columna: (tabla, columna) => {
      if (IMPLICITAS.has(columna.toLowerCase())) return true;
      const tablas = variantes(tabla);
      const columnas = [...new Set([columna, columna.split('_').filter(Boolean).at(-1)])];
      const bloques = bloquesDe(lineas, tablas.map(palabra));
      if (bloques.some((b) => columnas.some((c) => palabra(c).test(b)))) return true;
      if (columnas.some((c) => /[a-z0-9][A-Z]/.test(c) && enBackticks.has(c.toLowerCase()))) return true;
      return tablas.some((t) => columnas.some((c) => palabra(`${t}.${c}`).test(texto)));
    },
    /** Una fila con el método y la ruta: entera, o lo que sigue a un prefijo que la spec nombra (`{id}/items`, o `/`). */
    rutaApi: (metodo, ruta) => {
      const p = normApi(ruta);
      const candidatos = new Set([p]);
      for (const pre of prefijos) if (p === pre || p.startsWith(`${pre}/`)) candidatos.add(p.slice(pre.length).replace(/^\//, ''));
      const tokens = porMetodo.get(metodo) ?? new Set();
      return [...candidatos].some((c) => tokens.has(c));
    },
    /** La ruta entera, con cualquier nombre en sus parámetros (`:id`, `:catalogId` o `{id}`). */
    rutaFront: (ruta) => {
      const p = normFront(ruta);
      return p.length > 1 && new RegExp(`(?<![a-z0-9/_-])${escapar(p)}(?![a-z0-9/_:-])`).test(textoFront);
    },
  };
}

const memo = new WeakMap();

/** El plan, la spec y los commits de la etapa con sus cambios; una vez por repo y por corrida. */
export function etapaDe(ctx) {
  if (!memo.has(ctx)) memo.set(ctx, cargar(ctx));
  return memo.get(ctx);
}

async function cargar(ctx) {
  const ruta = ctx.opciones?.plan;
  if (!ruta || ruta === true) throw new Error('conformidad necesita --plan <ruta del plan>');
  const raizBack = ctx.raices?.back ?? ctx.raiz;
  const plan = isAbsolute(ruta) ? ruta : join(raizBack, ruta);
  if (!existsSync(plan)) throw new Error(`No existe el plan ${ruta}`);
  const clave = claveDelPlan(plan);
  const spec = await leerSpec(raizBack, plan, clave);
  const commits = await commitsDeLaEtapa(ctx.raiz, clave);
  for (const c of commits) c.archivos = await cambiosDelCommit(ctx.raiz, c.sha);
  const avisar = ctx.avisar ?? (() => {});
  if (ctx.repo === 'back') {
    avisar(`spec: ${spec.archivos.join(', ')}`);
    if (spec.archivos.length === 1) avisar('el plan no nombra ninguna spec de docs/plans/formularios/: se compara solo contra el plan');
    const front = ctx.raices?.front;
    if (!ctx.opciones.repo && front && !existsSync(front)) avisar(`no está el repo del front en ${front}: se omite`);
  }
  avisar(commits.length
    ? `${commits.length} ${commits.length === 1 ? 'commit' : 'commits'} con «Tarea: ${clave}/»`
    : `no hay commits con «Tarea: ${clave}/»: nada que comparar`);
  return { clave, spec, commits };
}

/**
 * Los nombres que agregan los commits en los archivos que cumplen `filtro`, con el primer commit que los trajo. Un nombre
 * que el mismo commit también quita no es nuevo: es un renglón que cambió o algo que se movió.
 */
export function nombresNuevos(commits, filtro, patron) {
  const vistos = new Map();
  for (const commit of commits) {
    const archivos = commit.archivos.filter((a) => filtro(a.ruta));
    const quitados = new Set(archivos.flatMap((a) => a.quitadas.flatMap((t) => [...t.matchAll(patron)].map((m) => m[1]))));
    for (const archivo of archivos) {
      for (const { n, texto } of archivo.agregadas) {
        for (const m of texto.matchAll(patron)) {
          if (quitados.has(m[1]) || vistos.has(m[1])) continue;
          vistos.set(m[1], { nombre: m[1], commit, archivo: archivo.ruta, linea: n });
        }
      }
    }
  }
  return [...vistos.values()];
}

const QUE = {
  tabla: 'la tabla',
  columna: 'la columna',
  error: 'el código de error',
  permiso: 'el permiso',
  funcionalidad: 'la funcionalidad',
  ruta: 'la ruta',
  'ruta-front': 'la ruta del front',
  componente: 'el componente compartido nuevo',
};

/** Un hallazgo para el dueño de la tarea: lo saca, o pasa a la spec por «Cambios al contrato»; si no entra, va a «Para después». */
export function hallazgo(ctx, etapa, { tipo, nombre, commit, archivo, linea }) {
  const real = `${QUE[tipo]} ${nombre} no está en la spec (${commit.corto}, Tarea: ${etapa.clave}/${commit.tarea})`;
  const pregunta = `${real}. Que su dueño lo saque, o que pase a la spec por «Cambios al contrato» si es una decisión de producto del usuario; si no entra ahora, va a «Para después».`;
  return ctx.crear({ archivo, linea, tipo, clave: `${tipo}:${nombre}`, real, pregunta, accion: 'humano' });
}

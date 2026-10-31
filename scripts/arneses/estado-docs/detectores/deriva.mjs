import { posix } from 'node:path';
import { git } from '../../comun/git.mjs';

export const nombre = 'deriva';

const NO_CAMBIA_LA_CONDUCTA = /^\S+ (?:docs|style|test|chore)[(:!]/;
const FICHA_FUNCIONAL = /^docs\/features\/[^/]+\.md$/;
const MOSTRAR = 3;

/** El último commit de `rutas` o, con `desde`, los posteriores a ese día: «fecha asunto». */
function logDeGit(ctx) {
  return async (rutas, desde) => {
    const alcance = desde ? [`--since=${desde}T23:59:59`, '--format=%cs %s'] : ['-1', '--format=%cs'];
    const salida = await git(ctx.raiz, ['log', '--no-merges', ...alcance, '--', ...rutas]);
    return salida.split(/\r?\n/).filter(Boolean);
  };
}

/** Qué carpetas de código describe cada ficha funcional: las que la nombran en su `AGENTS.md`. */
async function carpetasDeCadaFicha(ctx) {
  const fichas = new Map();
  for (const agents of ctx.archivos.archivos) {
    if (!/(?:^|\/)AGENTS\.md$/.test(agents) || /^(?:docs|scripts)\//.test(agents)) continue;
    const carpeta = posix.dirname(agents);
    if (carpeta === '.') continue;
    for (const e of ctx.markdown.enlaces(await ctx.leer(agents))) {
      if (!e.ruta) continue;
      const destino = posix.normalize(posix.join(carpeta, e.ruta));
      if (!FICHA_FUNCIONAL.test(destino)) continue;
      if (!fichas.has(destino)) fichas.set(destino, new Set());
      fichas.get(destino).add(carpeta);
    }
  }
  return fichas;
}

/** Una ficha funcional cuyo código cambió después de la última vez que alguien la miró. */
export async function detectar(ctx) {
  const log = ctx.log ?? logDeGit(ctx);
  const out = [];
  for (const [archivo, carpetas] of await carpetasDeCadaFicha(ctx)) {
    if (!ctx.archivos.existe(archivo)) continue;
    const [ultimoCommit = ''] = await log([archivo]);
    const revisado = (await ctx.leer(archivo)).match(/^\*\*Revisado:\*\*\s+(\d{4}-\d\d-\d\d)/m)?.[1] ?? '';
    const desde = ultimoCommit > revisado ? ultimoCommit : revisado;
    if (!desde) continue;
    const commits = (await log([...carpetas], desde)).filter((c) => !NO_CAMBIA_LA_CONDUCTA.test(c));
    if (commits.length === 0) continue;
    const cantidad = `${commits.length} ${commits.length === 1 ? 'commit' : 'commits'}`;
    const real = `${archivo}: ${cantidad} de su código después del ${desde}. Por ejemplo: ${commits.slice(0, MOSTRAR).join(' · ')}`;
    const pregunta = `${real}. ¿Sigue siendo cierta? Si cambió, actualizala; si está bien, agregale \`**Revisado:** AAAA-MM-DD\` debajo del título.`;
    out.push(ctx.crear({ archivo, linea: 1, tipo: 'codigo-cambio', clave: 'codigo', real, pregunta, accion: 'humano' }));
  }
  return out;
}

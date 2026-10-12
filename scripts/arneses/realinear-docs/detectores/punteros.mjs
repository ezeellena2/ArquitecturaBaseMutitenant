import { filasDeTabla, estaCerrada, marcasEtapa, secciones } from '../../comun/markdown.mjs';
import { mapaDeCapas } from '../../comun/rutas.mjs';

export const nombre = 'punteros';

const esPuntero = (ruta) => /(^|\/)(AGENTS|CLAUDE)\.md$/.test(ruta);
const escapar = (s) => s.replace(/[.+^${}()|[\]\\]/g, '\\$&');

/** Filas del mapa de carpetas → punteros (arnes.md): { patron, queVa, fichas, copia, linea, planeada }. */
export async function leerMapa(ctx) {
  const ruta = 'docs/architecture/arbol.md';
  if (!ctx.archivos.esArchivo(ruta)) return [];
  const seccion = secciones(await ctx.leer(ruta)).find((s) => /mapa de carpetas/i.test(s.titulo));
  if (!seccion) return [];
  const filas = filasDeTabla(seccion.lineas.map((l) => l.contenido).join('\n'));
  const base = seccion.linea;
  const out = [];
  for (const f of filas) {
    if (f.celdas.length < 2) continue;
    const patrones = [...f.celdas[0].matchAll(/`([^`]+)`/g)].map((m) => m[1]);
    if (!patrones.length || /^carpeta$/i.test(f.celdas[0])) continue;
    const linea = base + f.linea;
    const marcas = marcasEtapa(f.celdas[0]);
    out.push({ patrones, queVa: f.celdas[1], fichas: f.celdas[2] ?? '', copia: f.celdas[3] ?? '', linea, planeada: marcas.length > 0 && !estaCerrada(marcas, ctx.etapasCerradas) });
  }
  return out;
}

/** Carpetas reales que cumplen un patrón del mapa (`<X>` y `*` valen un segmento). */
function carpetasDelPatron(patron, ctx, capas) {
  let p = patron.replace(/\/$/, '');
  const [primero, ...resto] = p.split('/');
  if (capas.has(primero)) p = [capas.get(primero), ...resto].join('/');
  if (!/[<*]/.test(p)) return ctx.archivos.esDirectorio(p) ? [p] : [];
  const re = new RegExp(`^${escapar(p).replace(/<[^>]+>/g, '[^/]+').replace(/\*/g, '[^/]+')}$`);
  return [...ctx.archivos.directorios].filter((d) => re.test(d));
}

function tieneArchivosDeCodigo(ctx, dir) {
  return ctx.archivos.archivos.some((a) => a.startsWith(`${dir}/`) && !esPuntero(a));
}

function contenidoAgents(fila, ctx) {
  const frase = fila.queVa.replace(/`/g, '').trim();
  const l1 = `${frase.charAt(0).toUpperCase()}${frase.slice(1)}${/[.!?]$/.test(frase) ? '' : '.'}`;
  const lineas = [l1];
  const fichas = fila.fichas.split(',').map((f) => f.replace(/[`\s]|\[|\]|\(.*?\)/g, '')).filter((f) => ctx.archivos.esArchivo(`docs/rules/${f}.md`));
  if (fichas.length) lineas.push(`Antes de escribir, leé: ${fichas.map((f) => `docs/rules/${f}.md`).join(' · ')}`);
  const copia = fila.copia.replace(/`/g, '').trim();
  const marcas = marcasEtapa(copia);
  if (copia && !/^[—–-]$/.test(copia) && (!marcas.length || estaCerrada(marcas, ctx.etapasCerradas))) {
    lineas.push(`Copiá de: ${copia.replace(/\s*\(E\d+\)/g, '').trim()}`);
  }
  return `${lineas.join('\n')}\n`;
}

export async function detectar(ctx) {
  const out = [];
  const mapa = await leerMapa(ctx);
  if (!mapa.length) return out;
  const capas = mapaDeCapas(ctx.archivos);
  const mapeadas = new Set();

  for (const fila of mapa) {
    for (const patron of fila.patrones) {
      for (const dir of carpetasDelPatron(patron, ctx, capas)) {
        mapeadas.add(dir);
        if (fila.planeada || !tieneArchivosDeCodigo(ctx, dir)) continue;
        const agents = `${dir}/AGENTS.md`;
        const claude = `${dir}/CLAUDE.md`;
        if (!ctx.archivos.esArchivo(agents)) {
          out.push(ctx.crear({ archivo: agents, linea: 0, tipo: 'puntero-faltante', clave: agents, real: `la carpeta ${dir} está en el mapa y no tiene AGENTS.md`, accion: 'auto', parche: { tipo: 'crear-archivo', archivo: agents, contenido: contenidoAgents(fila, ctx) } }));
        }
        if (!ctx.archivos.esArchivo(claude)) {
          out.push(ctx.crear({ archivo: claude, linea: 0, tipo: 'puntero-faltante', clave: claude, real: `la carpeta ${dir} está en el mapa y no tiene CLAUDE.md`, accion: 'auto', parche: { tipo: 'crear-archivo', archivo: claude, contenido: '@AGENTS.md\n' } }));
        } else if ((await ctx.leer(claude)).trim() !== '@AGENTS.md') {
          out.push(ctx.crear({ archivo: claude, linea: 1, tipo: 'puntero-claude-invalido', clave: claude, real: 'CLAUDE.md no es @AGENTS.md', esperado: '@AGENTS.md', accion: 'auto', parche: { tipo: 'escribir-archivo', archivo: claude, contenido: '@AGENTS.md\n' } }));
        }
      }
    }
  }

  const carpetasConPuntero = new Set();
  for (const a of ctx.archivos.archivos) {
    if (esPuntero(a) && a.includes('/') && !ctx.excluido(a)) carpetasConPuntero.add(a.slice(0, a.lastIndexOf('/')));
  }
  for (const dir of [...carpetasConPuntero].sort()) {
    if (!tieneArchivosDeCodigo(ctx, dir)) {
      for (const nombreP of ['AGENTS.md', 'CLAUDE.md']) {
        const archivo = `${dir}/${nombreP}`;
        if (ctx.archivos.esArchivo(archivo)) {
          out.push(ctx.crear({ archivo, linea: 0, tipo: 'puntero-huerfano', clave: archivo, real: `${dir} ya no tiene archivos: el puntero quedó solo`, accion: 'auto', parche: { tipo: 'borrar-archivo', archivo } }));
        }
      }
      continue;
    }
    const agents = `${dir}/AGENTS.md`;
    if (mapeadas.has(dir)) continue;
    for (const nombreP of ['AGENTS.md', 'CLAUDE.md']) {
      const archivo = dir + '/' + nombreP;
      if (ctx.archivos.esArchivo(archivo)) out.push(ctx.crear({ archivo, linea: 0, tipo: 'puntero-fuera-del-mapa', clave: archivo,
        real: dir + ' no es una raíz declarada', accion: 'auto', parche: { tipo: 'borrar-archivo', archivo } }));
    }
  }
  return out;
}

import { posix } from 'node:path';
import { marcadorDescribir, parsearArbol } from '../../comun/arbolmd.mjs';

export const nombre = 'arbol';

const ARCHIVO = 'docs/architecture/arbol.md';
const esPuntero = (ruta) => /(^|\/)(AGENTS|CLAUDE)\.md$/.test(ruta);
const extensionesDelArbol = /\.(cs|ts|tsx|md|json|resx|mjs|ps1|yml|csproj|props|slnx)$/;
const generados = /(^|\/)Migrations\/|\.Designer\.cs$|\.g\.cs$|(^|\/)wwwroot\/|(^|\/)package-lock\.json$/;

function lineaNueva(hermano, nombreArchivo, descripcion, columna = 50) {
  const izquierda = `${hermano.prefijo}├── ${nombreArchivo}`;
  return `${izquierda.padEnd(Math.max(columna, izquierda.length + 1))}${descripcion}`;
}

/** Descripción corta sin llamar al modelo: el `<summary>` (C#) o el JSDoc (TS) del archivo o, para un archivo de tests, "tests de X". */
async function describirArchivo(ctx, archivo) {
  const nombreArchivo = posix.basename(archivo);
  const esTs = /\.(ts|tsx|mjs)$/.test(archivo);
  if (archivo.endsWith('.cs') || esTs) {
    const texto = await ctx.leer(archivo);
    const m = esTs ? texto.match(/\/\*\*([\s\S]*?)\*\//) : texto.match(/\/\/\/\s*<summary>([\s\S]*?)<\/summary>/);
    if (m) {
      const limpio = m[1].replace(/^\s*(\/\/\/|\*)\s?/gm, '').replace(/<see cref="([^"]+)"\s*\/>/g, '$1').replace(/<[^>]+>/g, '').replace(/\s+/g, ' ').trim();
      // Primera oración y, si es larga, hasta el primer signo de corte; tope de 10 palabras.
      const oracion = limpio.split(/\.\s|\.$/)[0].trim();
      const corta = oracion.split(/\s[—–-]\s|[:;]\s/)[0].trim();
      const palabras = corta.split(' ');
      if (!corta) return null;
      return palabras.length <= 10 ? corta : `${palabras.slice(0, 10).join(' ')}…`;
    }
    const tests = nombreArchivo.match(/^(\w+?)Tests\.cs$/) ?? nombreArchivo.match(/^([\w.-]+?)\.test\.(?:ts|tsx|mjs)$/);
    if (tests) return `tests de ${tests[1]}`;
  }
  return null;
}

export async function detectar(ctx) {
  if (!ctx.archivos.esArchivo(ARCHIVO)) return [];
  const texto = await ctx.leer(ARCHIVO);
  const secciones = parsearArbol(texto, ctx.etapasCerradas);
  if (!secciones.length) return [];
  const out = [];
  const nombresMencionados = new Set([...texto.matchAll(/[\w.-]+\.(?:cs|ts|tsx|md|json|resx|mjs|ps1|csproj)/g)].map((m) => m[0]));

  for (const s of secciones) {
    const listadas = new Set(s.entradas.map((e) => e.ruta));

    for (const e of s.entradas) {
      if (e.planeada) continue;
      if (e.descripcion.includes(marcadorDescribir)) {
        const cabeza = e.esDirectorio || !ctx.archivos.esArchivo(e.ruta) ? '' : (await ctx.leer(e.ruta)).split(/\r?\n/).slice(0, 25).join('\n');
        out.push(ctx.crear({ archivo: ARCHIVO, linea: e.linea, tipo: 'linea-sin-descripcion', clave: e.ruta, real: `${e.ruta} está en arbol.md sin descripción`, contexto: cabeza, accion: 'modelo', pregunta: 'Descripción de hasta 10 palabras' }));
      }
      if (ctx.archivos.existe(e.ruta)) continue;
      const tipo = e.esDirectorio ? 'carpeta-faltante' : 'archivo-faltante';
      const base = { archivo: ARCHIVO, linea: e.linea, tipo, clave: e.ruta, real: `${e.ruta} está en arbol.md y no existe` };
      if (await ctx.fueBorrado(e.ruta)) {
        out.push(ctx.crear({ ...base, accion: 'auto', parche: { tipo: 'borrar-linea', archivo: ARCHIVO, linea: e.linea, de: e.texto } }));
      } else {
        out.push(ctx.crear({ ...base, accion: 'humano', pregunta: `${e.ruta} figura en arbol.md (etapa cerrada) y nunca estuvo en la historia: ¿se creó con otro nombre o hay que sacarlo del árbol?` }));
      }
    }

    // Archivos nuevos dentro de carpetas que el árbol ya lista.
    const carpetas = [{ ruta: s.raiz, entrada: null }, ...s.entradas.filter((e) => e.esDirectorio && !e.planeada && ctx.archivos.esDirectorio(e.ruta)).map((e) => ({ ruta: e.ruta, entrada: e }))];
    for (const c of carpetas) {
      const hijos = s.entradas.filter((e) => posix.dirname(e.ruta) === (c.ruta || '.') && e.profundidad === (c.entrada ? c.entrada.profundidad + 1 : 0));
      if (!hijos.length) continue;
      // Una carpeta con un nombre de plantilla (`NNNN-<titulo>.md`) lista un patrón, no cada archivo.
      if (hijos.some((h) => /[<>]|NNNN|AAAA/.test(h.nombre))) continue;
      const hermano = hijos[hijos.length - 1];
      const columna = hijos.find((h) => h.columna > 0)?.columna;
      const nuevos = ctx.archivos.archivos.filter((a) => posix.dirname(a) === (c.ruta || '.') && !esPuntero(a) && !ctx.excluido(a) && extensionesDelArbol.test(a) && !generados.test(a) && !listadas.has(a) && !nombresMencionados.has(posix.basename(a)));
      for (const archivo of nuevos.sort().reverse()) {
        const descripcion = await describirArchivo(ctx, archivo);
        const base = { archivo: ARCHIVO, linea: hermano.linea, tipo: 'archivo-nuevo', clave: archivo, real: `${archivo} existe y no está en arbol.md` };
        if (descripcion) {
          out.push(ctx.crear({ ...base, accion: 'auto', parche: { tipo: 'insertar-linea', archivo: ARCHIVO, despues: hermano.linea - 1, texto: lineaNueva(hermano, posix.basename(archivo), descripcion, columna) } }));
        } else {
          out.push(ctx.crear({ ...base, accion: 'modelo', esperado: lineaNueva(hermano, posix.basename(archivo), marcadorDescribir, columna), contexto: (await ctx.leer(archivo)).split(/\r?\n/).slice(0, 25).join('\n'), pregunta: `Insertá la línea esperada antes de la línea ${hermano.linea} de arbol.md, con una descripción de hasta 10 palabras.` }));
        }
      }
    }
  }
  return out;
}

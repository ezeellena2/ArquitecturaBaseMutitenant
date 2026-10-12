import { posix } from 'node:path';
import { parsearArbol } from '../../comun/arbolmd.mjs';

export const nombre = 'arbol';
const ARCHIVO = 'docs/architecture/arbol.md';

export async function detectar(ctx) {
  if (!ctx.archivos.esArchivo(ARCHIVO)) return [];
  const secciones = parsearArbol(await ctx.leer(ARCHIVO), ctx.etapasCerradas);
  const listadas = new Set(secciones.flatMap(s => [s.raiz, ...s.entradas.filter(e => e.esDirectorio).map(e => e.ruta)]));
  const out = [];
  for (const s of secciones) {
    for (const e of s.entradas.filter(e => e.esDirectorio && !e.planeada && !ctx.excluido(e.ruta))) {
      if (ctx.archivos.esDirectorio(e.ruta)) continue;
      const borrada = await ctx.fueBorrado(e.ruta);
      out.push(ctx.crear({ archivo: ARCHIVO, linea: e.linea, tipo: 'carpeta-faltante', clave: e.ruta,
        real: e.ruta + ' está en arbol.md y no existe', accion: borrada ? 'auto' : 'humano',
        ...(borrada ? { parche: { tipo: 'borrar-linea', archivo: ARCHIVO, linea: e.linea, de: e.texto } }
          : { pregunta: 'Retirá o corregí la carpeta documentada.' }) }));
    }
    const carpetas = [{ ruta: s.raiz, entrada: null }, ...s.entradas.filter(e => e.esDirectorio && !e.planeada).map(e => ({ ruta: e.ruta, entrada: e }))];
    for (const c of carpetas) {
      if (c.ruta && !ctx.archivos.esDirectorio(c.ruta)) continue;
      const nuevas = [...ctx.archivos.directorios].filter(d => posix.dirname(d) === (c.ruta || '.') && !listadas.has(d) && !ctx.excluido(d));
      for (const dir of nuevas.sort()) {
        out.push(ctx.crear({ archivo: ARCHIVO, linea: c.entrada?.linea ?? s.linea, tipo: 'carpeta-nueva', clave: dir,
          real: dir + ' existe y no está en arbol.md', accion: 'modelo', pregunta: 'Agregá la carpeta y su responsabilidad al árbol.' }));
      }
    }
  }
  return out;
}

import { readFile } from 'node:fs/promises';
import { join } from 'node:path';

const declaraCsharp = /\b(?:class|record(?:\s+struct)?|struct|interface|enum)\s+([A-Za-z_]\w*)/g;
const declaraTs = /\bexport\s+(?:default\s+)?(?:declare\s+)?(?:abstract\s+)?(?:async\s+)?(?:function\*?|class|const|let|interface|type|enum)\s+([A-Za-z_$][\w$]*)/g;

/**
 * Índice de tipos de un repo: C# por regex (class, record, struct, interface, enum) y TS por `export`.
 * Los miembros se verifican leyendo el archivo del tipo cuando se piden.
 * @returns {Promise<{ raiz: string, tipos: Map<string, string[]>, existe(n: string): boolean, archivosDe(n: string): string[], tieneMiembro(tipo: string, miembro: string): Promise<boolean> }>}
 */
export async function indexarSimbolos(archivos) {
  const tipos = new Map();
  const agregar = (nombre, archivo) => {
    if (!tipos.has(nombre)) tipos.set(nombre, []);
    if (!tipos.get(nombre).includes(archivo)) tipos.get(nombre).push(archivo);
  };
  for (const archivo of archivos.archivos) {
    const esCs = archivo.endsWith('.cs');
    const esTs = /\.(ts|tsx)$/.test(archivo) && !archivo.endsWith('.d.ts');
    if (!esCs && !esTs) continue;
    let texto;
    try {
      texto = await readFile(join(archivos.raiz, archivo), 'utf8');
    } catch {
      continue;
    }
    for (const m of texto.matchAll(esCs ? declaraCsharp : declaraTs)) agregar(m[1], archivo);
  }
  return {
    raiz: archivos.raiz,
    tipos,
    existe: (n) => tipos.has(n),
    archivosDe: (n) => tipos.get(n) ?? [],
    async tieneMiembro(tipo, miembro) {
      const re = new RegExp(`(?<![\\w$])${miembro.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(?![\\w$])`);
      for (const archivo of tipos.get(tipo) ?? []) {
        try {
          if (re.test(await readFile(join(archivos.raiz, archivo), 'utf8'))) return true;
        } catch {
          /* el archivo ya no está: se ignora */
        }
      }
      return false;
    },
  };
}

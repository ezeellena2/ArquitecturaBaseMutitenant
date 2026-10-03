import { estaCerrada, marcasEtapa } from './markdown.mjs';

/**
 * Parser de `docs/architecture/arbol.md`: secciones `## <ruta>` con un bloque de código tipo `tree`.
 * Devuelve las entradas con la ruta reconstruida, la línea del archivo y si son de una etapa sin cerrar (D3).
 */

const rama = /^(?<prefijo>(?:│   |    )*)(?<marca>├── |└── )(?<nombre>\S+)(?<resto>.*)$/;

/** Marca que el arnés escribe en la línea de un archivo nuevo, hasta que el modelo la describe. */
export const marcadorDescribir = '<describir>';

/** ¿El título de la sección es una ruta? Devuelve la ruta sin barra final, '' para `Raíz`, o null. */
export function rutaDelTitulo(titulo) {
  const limpio = titulo.replace(/`[^`]*`/g, '').trim();
  if (/^Raíz$/i.test(limpio)) return '';
  const esRuta = /^[\w.-]+(?:\/[\w.-]+)*\/?$/.test(limpio) && /\/|^(src|tests|docs|scripts)$/.test(limpio);
  return esRuta ? limpio.replace(/\/$/, '') : null;
}

const esPlaceholder = (nombre) => /[<>{}*[\]]|NNNN|AAAA|\.\.\.|…/.test(nombre);

/** @returns {{ titulo: string, raiz: string, linea: number, entradas: object[] }[]} */
export function parsearArbol(texto, etapasCerradas) {
  const ls = texto.split(/\r?\n/);
  const secciones = [];
  for (let i = 0; i < ls.length; i += 1) {
    const h = ls[i].match(/^##\s+(.*)$/);
    if (!h) continue;
    const raiz = rutaDelTitulo(h[1]);
    if (raiz === null) continue;
    const marcasTitulo = marcasEtapa(h[1]);
    if (marcasTitulo.length && !estaCerrada(marcasTitulo, etapasCerradas)) continue;
    let j = i + 1;
    while (j < ls.length && !/^\s*```/.test(ls[j]) && !/^##\s/.test(ls[j])) j += 1;
    if (j >= ls.length || /^##\s/.test(ls[j])) continue;
    const inicio = j + 1;
    let fin = inicio;
    while (fin < ls.length && !/^\s*```/.test(ls[fin])) fin += 1;
    secciones.push({ titulo: h[1], raiz, linea: i + 1, entradas: parsearBloque(ls, inicio, fin, raiz, etapasCerradas) });
  }
  return secciones;
}

function parsearBloque(ls, inicio, fin, raiz, etapasCerradas) {
  const entradas = [];
  // pila[d] = directorio donde viven las entradas de profundidad d (pila[0] es la raíz del bloque)
  const pila = [{ ruta: raiz, planeada: false }];
  for (let n = inicio; n < fin; n += 1) {
    const m = ls[n].match(rama);
    if (!m) continue;
    const profundidad = m.groups.prefijo.length / 4;
    const nombre = m.groups.nombre;
    const marcas = marcasEtapa(m.groups.resto);
    const padre = pila[profundidad] ?? pila[pila.length - 1];
    const abierta = marcas.length > 0 && !estaCerrada(marcas, etapasCerradas);
    const esDirectorio = nombre.endsWith('/');
    const limpio = nombre.replace(/\/$/, '');
    const ruta = padre.ruta ? `${padre.ruta}/${limpio}` : limpio;
    const planeada = padre.planeada || abierta || esPlaceholder(nombre);
    pila.length = profundidad + 1;
    pila[profundidad + 1] = { ruta, planeada };
    if (nombre === 'bin/' || nombre === 'obj/') continue;
    const descripcion = m.groups.resto.trim();
    const columna = descripcion ? ls[n].length - m.groups.resto.trimStart().length : -1;
    entradas.push({
      linea: n + 1, profundidad, nombre, ruta, esDirectorio, planeada, descripcion, texto: ls[n],
      prefijo: m.groups.prefijo, marca: m.groups.marca, columna,
    });
  }
  return entradas;
}

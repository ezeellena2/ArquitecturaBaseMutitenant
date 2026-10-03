/** Utilidades de Markdown: enlaces, anclas, rutas entre backticks, tablas y marcas de etapa. */

/** Líneas con su número (1-based) y si están dentro de un bloque de código. */
export function lineas(texto) {
  let enCodigo = false;
  return texto.split(/\r?\n/).map((contenido, i) => {
    const cerca = /^\s*(```|~~~)/.test(contenido);
    const l = { n: i + 1, contenido, codigo: enCodigo || cerca };
    if (cerca) enCodigo = !enCodigo;
    return l;
  });
}

export function slug(titulo) {
  // Como GitHub (y como HarnessTests): cada espacio es un guion, sin colapsar.
  return titulo
    .trim()
    .toLowerCase()
    .replace(/<[^>]*>/g, '')
    .replace(/[^\p{L}\p{N}_\s-]/gu, '')
    .replace(/\s/g, '-');
}

export function anclas(texto) {
  const set = new Set();
  const vistos = new Map();
  for (const l of lineas(texto)) {
    const m = !l.codigo && l.contenido.match(/^#{1,6}\s+(.*?)\s*#*\s*$/);
    if (!m) continue;
    const base = slug(m[1]);
    const veces = vistos.get(base) ?? 0;
    vistos.set(base, veces + 1);
    set.add(veces === 0 ? base : `${base}-${veces}`);
  }
  return set;
}

/** Enlaces `[texto](destino)` fuera de bloques de código; ignora http, mailto y data. */
export function enlaces(texto) {
  const out = [];
  for (const l of lineas(texto)) {
    if (l.codigo) continue;
    const sinCodigoEnLinea = l.contenido.replace(/`[^`]*`/g, (c) => ' '.repeat(c.length));
    for (const m of sinCodigoEnLinea.matchAll(/\[([^\]]*)\]\(([^)\s]+)(?:\s+"[^"]*")?\)/g)) {
      const destino = m[2];
      if (/^(https?:|mailto:|data:)/i.test(destino)) continue;
      const i = destino.indexOf('#');
      out.push({ linea: l.n, texto: m[1], ruta: i < 0 ? destino : destino.slice(0, i), ancla: i < 0 ? '' : destino.slice(i + 1) });
    }
  }
  return out;
}

const extensiones = /\.(cs|ts|tsx|md|json|mjs|ps1|resx|yml|yaml|csproj|props|slnx|html|txt|sql)$/i;

/** Valores entre backticks que parecen una ruta: tienen `/` o terminan en una extensión conocida. Sin espacios ni placeholders. */
export function rutasEnBackticks(texto) {
  const out = [];
  for (const l of lineas(texto)) {
    if (l.codigo) continue;
    for (const m of l.contenido.matchAll(/`([^`\n]+)`/g)) {
      const valor = m[1].trim();
      if (/[\s<>{}*|$=(),;:"']/.test(valor) || /^(https?:|\/|\.\.?$|-)/.test(valor)) continue;
      if (!valor.includes('/') && !extensiones.test(valor)) continue;
      if (valor.includes('/') && !extensiones.test(valor) && !valor.endsWith('/')) continue;
      out.push({ linea: l.n, valor });
    }
  }
  return out;
}

/** Marcas `(E4)`, `[E4]`, `E3–E5`, `E3-E5`: lista de rangos [desde, hasta]. */
export function marcasEtapa(linea) {
  const out = [];
  for (const m of linea.matchAll(/E(\d+)\s*[–-]\s*E?(\d+)|[(\[]E(\d+)[)\]]/g)) {
    if (m[1]) out.push([Number(m[1]), Number(m[2])]);
    else out.push([Number(m[3]), Number(m[3])]);
  }
  return out;
}

/** D3: lo marcado con una etapa que no cerró se ignora. Sin marcas: cerrado. Un rango cuenta cuando cerraron todas. */
export function estaCerrada(rangos, cerradas) {
  return rangos.every(([a, b]) => {
    for (let e = a; e <= b; e += 1) if (!cerradas.includes(e)) return false;
    return true;
  });
}

/** Filas de tablas Markdown: [{ linea, celdas }]; salta el separador. */
export function filasDeTabla(texto) {
  const out = [];
  for (const l of lineas(texto)) {
    if (l.codigo || !/^\s*\|/.test(l.contenido)) continue;
    if (/^\s*\|[\s:|-]+\|\s*$/.test(l.contenido)) continue;
    const celdas = l.contenido.trim().replace(/^\|/, '').replace(/\|$/, '').split(/(?<!\\)\|/).map((c) => c.trim());
    out.push({ linea: l.n, celdas });
  }
  return out;
}

/** Título y líneas de cada sección `##`. */
export function secciones(texto) {
  const out = [];
  let actual = null;
  for (const l of lineas(texto)) {
    const m = !l.codigo && l.contenido.match(/^##\s+(.*)$/);
    if (m) {
      actual = { titulo: m[1].trim(), linea: l.n, lineas: [] };
      out.push(actual);
    } else if (actual) actual.lineas.push(l);
  }
  return out;
}

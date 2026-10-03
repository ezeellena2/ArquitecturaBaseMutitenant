/** @typedef {{ id: string, detector: string, repo: string, archivo: string, linea: number, tipo: string, real: string, esperado?: string, accion: 'auto'|'modelo'|'humano', parche?: object, contexto?: string, candidatos?: string[], pregunta?: string }} Hallazgo */

/** El id es estable entre corridas: no depende de la línea. */
export function crearHallazgo({ detector, repo, archivo, linea = 0, tipo, clave, real, esperado, accion, parche, contexto, candidatos, pregunta }) {
  const h = { id: `${detector}|${repo}|${archivo}|${tipo}|${clave}`, detector, repo, archivo, linea, tipo, real, accion };
  if (esperado !== undefined) h.esperado = esperado;
  if (parche) h.parche = parche;
  if (contexto !== undefined) h.contexto = contexto;
  if (candidatos?.length) h.candidatos = candidatos;
  if (pregunta) h.pregunta = pregunta;
  return h;
}

export function ordenar(hallazgos) {
  return [...hallazgos].sort((a, b) => a.repo.localeCompare(b.repo) || a.archivo.localeCompare(b.archivo) || a.linea - b.linea || a.id.localeCompare(b.id));
}

export function filtrarIgnorados(hallazgos, ignorados) {
  const ids = new Set(ignorados.map((i) => i.id));
  return hallazgos.filter((h) => !ids.has(h.id));
}

export function contar(hallazgos) {
  const c = { auto: 0, modelo: 0, humano: 0 };
  for (const h of hallazgos) c[h.accion] += 1;
  return c;
}

/** ±radio líneas alrededor de `linea`, con su número. */
export function contextoDe(texto, linea, radio = 5) {
  const ls = texto.split(/\r?\n/);
  const desde = Math.max(1, linea - radio);
  const hasta = Math.min(ls.length, linea + radio);
  const out = [];
  for (let n = desde; n <= hasta; n += 1) out.push(`${n}: ${ls[n - 1]}`);
  return out.join('\n');
}

/** Distancia de edición (Levenshtein) entre dos cadenas. */
export function distancia(a, b) {
  if (a === b) return 0;
  if (!a.length) return b.length;
  if (!b.length) return a.length;
  let anterior = Array.from({ length: b.length + 1 }, (_, i) => i);
  for (let i = 1; i <= a.length; i += 1) {
    const actual = [i];
    for (let j = 1; j <= b.length; j += 1) {
      const costo = a[i - 1] === b[j - 1] ? 0 : 1;
      actual[j] = Math.min(actual[j - 1] + 1, anterior[j] + 1, anterior[j - 1] + costo);
    }
    anterior = actual;
  }
  return anterior[b.length];
}

/** Hasta `max` candidatos parecidos a `nombre`, del más cercano al más lejano (distancia ≤ `tolerancia`). */
export function parecidos(nombre, candidatos, { tolerancia = 3, max = 5 } = {}) {
  const puntuados = [];
  for (const c of candidatos) {
    const d = distancia(nombre.toLowerCase(), c.toLowerCase());
    if (d <= tolerancia) puntuados.push([d, c]);
  }
  return puntuados.sort((x, y) => x[0] - y[0] || x[1].localeCompare(y[1])).slice(0, max).map(([d, c]) => ({ nombre: c, distancia: d }));
}

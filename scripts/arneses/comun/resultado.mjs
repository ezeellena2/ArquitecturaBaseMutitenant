/**
 * La respuesta final del modelo es un objeto `{ resultados: [{ id, resultado, nota }] }`
 * (Codex exige un objeto en la raíz con --output-schema; Claude recibe la misma forma en las instrucciones).
 */

/** Saca el JSON de un texto: el texto entero, un bloque ```json o el último objeto balanceado que tenga "resultados". */
export function extraerResultado(texto) {
  const candidatos = [texto.trim()];
  for (const m of texto.matchAll(/```(?:json)?\s*([\s\S]*?)```/g)) candidatos.push(m[1].trim());
  const idx = texto.lastIndexOf('{"resultados"');
  if (idx >= 0) candidatos.push(texto.slice(idx).trim());
  const ultimo = texto.lastIndexOf('{');
  if (ultimo >= 0) candidatos.push(texto.slice(texto.indexOf('{')).trim());
  for (const c of candidatos.reverse()) {
    for (const intento of [c, recortarObjeto(c)]) {
      if (!intento) continue;
      try {
        const obj = JSON.parse(intento);
        if (obj && typeof obj === 'object') return obj;
      } catch {
        /* se prueba con el siguiente */
      }
    }
  }
  return null;
}

function recortarObjeto(texto) {
  const inicio = texto.indexOf('{');
  if (inicio < 0) return null;
  let profundidad = 0;
  let enCadena = false;
  for (let i = inicio; i < texto.length; i += 1) {
    const c = texto[i];
    if (enCadena) {
      if (c === '\\') i += 1;
      else if (c === '"') enCadena = false;
    } else if (c === '"') enCadena = true;
    else if (c === '{') profundidad += 1;
    else if (c === '}' && --profundidad === 0) return texto.slice(inicio, i + 1);
  }
  return null;
}

/** Validador mínimo de JSON Schema (type, enum, required, properties, items, additionalProperties:false). Devuelve la lista de errores. */
export function validar(valor, esquema, ruta = '$') {
  const errores = [];
  const tipo = Array.isArray(valor) ? 'array' : valor === null ? 'null' : typeof valor;
  if (esquema.type && esquema.type !== tipo) return [`${ruta}: se esperaba ${esquema.type} y vino ${tipo}`];
  if (esquema.enum && !esquema.enum.includes(valor)) errores.push(`${ruta}: «${valor}» no es uno de ${esquema.enum.join(', ')}`);
  if (tipo === 'object') {
    for (const r of esquema.required ?? []) if (!(r in valor)) errores.push(`${ruta}.${r}: falta`);
    for (const [k, v] of Object.entries(valor)) {
      if (esquema.properties?.[k]) errores.push(...validar(v, esquema.properties[k], `${ruta}.${k}`));
      else if (esquema.additionalProperties === false) errores.push(`${ruta}.${k}: no está permitido`);
    }
  }
  if (tipo === 'array' && esquema.items) valor.forEach((v, i) => errores.push(...validar(v, esquema.items, `${ruta}[${i}]`)));
  return errores;
}

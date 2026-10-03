import { readFile } from 'node:fs/promises';
import { ejecutarProceso, resolverEjecutable } from '../proceso.mjs';
import { extraerResultado } from '../resultado.mjs';

export const nombre = 'codex';

/** Argumentos de `codex exec` (plan §7); el prompt va por stdin (`-`). `aislar` = `--ignore-user-config`. */
export function argumentos({ cwd, modelo, esfuerzo, esquema, salida, aislar = true }) {
  const args = ['exec', '-C', cwd, '-m', modelo, '-c', `model_reasoning_effort="${esfuerzo}"`, '-s', 'workspace-write'];
  if (aislar) args.push('--ignore-user-config');
  args.push('--ephemeral', '--output-schema', esquema, '-o', salida, '--json', '-');
  return args;
}

/**
 * Consumo de los eventos `--json`: se toma el último evento con un objeto de uso.
 * Codex no informa el costo: `costoUsd` queda en null.
 */
export function consumoDe(jsonl) {
  let ultimo = null;
  for (const linea of jsonl.split(/\r?\n/)) {
    if (!linea.trim().startsWith('{')) continue;
    let evento;
    try {
      evento = JSON.parse(linea);
    } catch {
      continue;
    }
    const uso = buscarUso(evento);
    if (uso) ultimo = uso;
  }
  if (!ultimo) return null;
  const entrada = ultimo.input_tokens ?? 0;
  return {
    tokensEntrada: entrada,
    cacheLectura: ultimo.cached_input_tokens ?? ultimo.cache_read_input_tokens ?? 0,
    tokensSalida: (ultimo.output_tokens ?? 0),
    tokensRazonamiento: ultimo.reasoning_output_tokens ?? 0,
    costoUsd: null,
  };
}

function buscarUso(nodo, profundidad = 0) {
  if (!nodo || typeof nodo !== 'object' || profundidad > 5) return null;
  if (typeof nodo.input_tokens === 'number' && typeof nodo.output_tokens === 'number') return nodo;
  for (const v of Object.values(nodo)) {
    const hallado = buscarUso(v, profundidad + 1);
    if (hallado) return hallado;
  }
  return null;
}

/**
 * Corre el motor. `instrucciones` es el texto de instrucciones.md: Codex no tiene un flag equivalente a
 * `--append-system-prompt-file`, así que va al principio del prompt.
 */
export async function ejecutar({ tarea, instruccionesTexto, modelo, esfuerzo, minutosMaximos = 10, cwd, esquema, salida, aislar = true, bin }) {
  const comando = [...(bin ?? resolverEjecutable('codex')), ...argumentos({ cwd, modelo, esfuerzo, esquema, salida, aislar })];
  const r = await ejecutarProceso(comando, { cwd, stdin: `${instruccionesTexto}\n\n---\n\n${tarea}\n`, timeoutMs: minutosMaximos * 60_000 });
  const consumo = consumoDe(r.stdout);
  if (r.cortado) return { ok: false, estado: 'cortado por tiempo', resultado: null, consumo, texto: '', stderr: r.stderr };
  let texto = '';
  try {
    texto = await readFile(salida, 'utf8');
  } catch {
    return { ok: false, estado: `sin mensaje final (código ${r.codigo})`, resultado: null, consumo, texto: r.stdout, stderr: r.stderr };
  }
  if (r.codigo !== 0) return { ok: false, estado: `error (código ${r.codigo})`, resultado: null, consumo, texto, stderr: r.stderr };
  const resultado = extraerResultado(texto);
  return { ok: resultado !== null, estado: resultado ? 'ok' : 'sin resultado JSON', resultado, consumo, texto, stderr: r.stderr };
}

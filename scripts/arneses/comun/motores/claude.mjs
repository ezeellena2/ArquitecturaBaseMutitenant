import { ejecutarProceso, resolverEjecutable } from '../proceso.mjs';
import { extraerResultado } from '../resultado.mjs';

export const nombre = 'claude';

const herramientasPermitidas = 'Read Edit Write Glob Grep';

/** Argumentos de `claude -p` (plan §7). `aislar` evita cargar la config del usuario: plugins, hooks y MCP. */
export function argumentos({ tarea, instrucciones, modelo, esfuerzo, presupuestoUsd, aislar = true }) {
  const args = ['-p', tarea, '--model', modelo, '--effort', esfuerzo];
  if (presupuestoUsd) args.push('--max-budget-usd', String(presupuestoUsd));
  args.push('--append-system-prompt-file', instrucciones, '--allowedTools', herramientasPermitidas, '--disallowedTools', 'Bash', '--permission-mode', 'acceptEdits');
  if (aislar) args.push('--setting-sources', 'project', '--strict-mcp-config');
  args.push('--no-session-persistence', '--output-format', 'json');
  return args;
}

/** Tokens de entrada = sin caché + lectura de caché + escritura de caché (así se factura lo que el modelo leyó). */
export function consumoDe(salida) {
  const u = salida?.usage ?? {};
  const sinCache = u.input_tokens ?? 0;
  const lectura = u.cache_read_input_tokens ?? 0;
  const escritura = u.cache_creation_input_tokens ?? 0;
  return {
    tokensEntrada: sinCache + lectura + escritura,
    entradaSinCache: sinCache,
    cacheLectura: lectura,
    cacheEscritura: escritura,
    tokensSalida: u.output_tokens ?? 0,
    costoUsd: salida?.total_cost_usd ?? null,
  };
}

/**
 * Corre el motor y devuelve `{ ok, estado, resultado, consumo, texto, stderr }`.
 * `bin` reemplaza al ejecutable (los tests usan un `claude` falso).
 */
export async function ejecutar({ tarea, instrucciones, modelo, esfuerzo, presupuestoUsd, minutosMaximos = 15, cwd, aislar = true, bin }) {
  const comando = [...(bin ?? resolverEjecutable('claude')), ...argumentos({ tarea, instrucciones, modelo, esfuerzo, presupuestoUsd, aislar })];
  const r = await ejecutarProceso(comando, { cwd, timeoutMs: minutosMaximos * 60_000 });
  if (r.cortado) return { ok: false, estado: 'cortado por tiempo', resultado: null, consumo: null, texto: r.stdout, stderr: r.stderr };
  let salida = null;
  try {
    salida = JSON.parse(r.stdout);
  } catch {
    return { ok: false, estado: `salida no válida (código ${r.codigo})`, resultado: null, consumo: null, texto: r.stdout, stderr: r.stderr };
  }
  const consumo = consumoDe(salida);
  const texto = typeof salida.result === 'string' ? salida.result : '';
  if (salida.is_error || r.codigo !== 0) {
    return { ok: false, estado: salida.subtype && salida.subtype !== 'success' ? salida.subtype : `error (código ${r.codigo})`, resultado: null, consumo, texto, stderr: r.stderr };
  }
  const resultado = extraerResultado(texto);
  return { ok: resultado !== null, estado: resultado ? 'ok' : 'sin resultado JSON', resultado, consumo, texto, stderr: r.stderr };
}

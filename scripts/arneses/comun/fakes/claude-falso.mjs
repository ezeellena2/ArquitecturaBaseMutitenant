#!/usr/bin/env node
// `claude` falso: imita la salida de `claude -p --output-format json` sin llamar a ningún modelo.
import { readFileSync } from 'node:fs';
import { aplicarEscenario, registrar } from './escenario.mjs';

const args = process.argv.slice(2);
const valor = (flag) => args[args.indexOf(flag) + 1];
const instrucciones = args.includes('--append-system-prompt-file') ? readFileSync(valor('--append-system-prompt-file'), 'utf8') : '';
registrar({ motor: 'claude', args, cwd: process.cwd(), instrucciones: instrucciones.slice(0, 40) });

const { escenario, resultado } = aplicarEscenario(process.cwd());
if (escenario.error) {
  process.stdout.write(JSON.stringify({ type: 'result', subtype: 'error_max_budget_usd', is_error: true, total_cost_usd: 1.5, usage: { input_tokens: 5, output_tokens: 1 } }));
  process.exit(1);
}
process.stdout.write(JSON.stringify({
  type: 'result',
  subtype: 'success',
  is_error: false,
  result: `Listo.\n\`\`\`json\n${JSON.stringify(resultado)}\n\`\`\``,
  total_cost_usd: 0.0123,
  usage: { input_tokens: 12, cache_read_input_tokens: 100, cache_creation_input_tokens: 50, output_tokens: 30 },
}));

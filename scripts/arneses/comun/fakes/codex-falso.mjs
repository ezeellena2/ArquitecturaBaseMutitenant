#!/usr/bin/env node
// `codex` falso: imita `codex exec --json -o <archivo>` sin llamar a ningún modelo.
import { writeFileSync } from 'node:fs';
import { aplicarEscenario, registrar } from './escenario.mjs';

const args = process.argv.slice(2);
const valor = (flag) => args[args.indexOf(flag) + 1];
let stdin = '';
process.stdin.setEncoding('utf8');
for await (const trozo of process.stdin) stdin += trozo;

const cwd = valor('-C') ?? process.cwd();
registrar({ motor: 'codex', args, cwd, stdin: stdin.slice(0, 60) });

const { escenario, resultado } = aplicarEscenario(cwd);
if (escenario.error) process.exit(1);
writeFileSync(valor('-o'), JSON.stringify(resultado));
const eventos = [
  { type: 'thread.started', thread_id: 'falso' },
  { type: 'item.completed', item: { type: 'agent_message', text: 'Listo.' } },
  { type: 'turn.completed', usage: { input_tokens: 150, cached_input_tokens: 100, output_tokens: 20, reasoning_output_tokens: 5 } },
];
process.stdout.write(`${eventos.map((e) => JSON.stringify(e)).join('\n')}\n`);

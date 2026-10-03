import { spawn, spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { delimiter, dirname, join } from 'node:path';

/**
 * Resuelve un ejecutable de línea de comandos a `[comando, ...argumentosFijos]` que `spawn` entienda sin shell.
 * En Windows los CLI de npm son `.cmd`: se lee el shim para llegar al `.exe` o al `.js` real.
 */
export function resolverEjecutable(nombre, { entorno = process.env, plataforma = process.platform } = {}) {
  if (plataforma !== 'win32') return [nombre];
  const carpetas = (entorno.PATH ?? entorno.Path ?? '').split(delimiter).filter(Boolean);
  for (const carpeta of carpetas) {
    const exe = join(carpeta, `${nombre}.exe`);
    if (existsSync(exe)) return [exe];
    const cmd = join(carpeta, `${nombre}.cmd`);
    if (!existsSync(cmd)) continue;
    const citas = [...readFileSync(cmd, 'utf8').matchAll(/"([^"]*%dp0%[^"]*)"/gi)].map((m) => m[1]);
    const destino = citas.at(-1);
    if (!destino) return [cmd];
    const real = destino.replace(/%dp0%\\?/gi, `${dirname(cmd)}\\`);
    return /\.js$/i.test(real) ? [process.execPath, real] : [real];
  }
  return [nombre];
}

function matarArbol(proceso) {
  if (process.platform === 'win32') spawnSync('taskkill', ['/PID', String(proceso.pid), '/T', '/F'], { windowsHide: true });
  else proceso.kill('SIGKILL');
}

/** Corre un proceso, con `stdin` opcional y tope de tiempo. Nunca lanza por el código de salida. */
export function ejecutarProceso(comando, { cwd, stdin, timeoutMs, env, shell = false } = {}) {
  return new Promise((resolver) => {
    // Con `shell`, `comando` es una sola cadena (un comando de `verificacion`).
    const [bin, ...args] = shell ? [comando, []] : comando;
    const proceso = spawn(bin, shell ? [] : args, { cwd, env: env ?? process.env, windowsHide: true, shell, stdio: ['pipe', 'pipe', 'pipe'] });
    let stdout = '';
    let stderr = '';
    let cortado = false;
    proceso.stdout.on('data', (d) => { stdout += d; });
    proceso.stderr.on('data', (d) => { stderr += d; });
    const reloj = timeoutMs ? setTimeout(() => { cortado = true; matarArbol(proceso); }, timeoutMs) : null;
    proceso.on('error', (e) => { clearTimeout(reloj); resolver({ codigo: -1, stdout, stderr: `${stderr}${e.message}`, cortado }); });
    proceso.on('close', (codigo) => { clearTimeout(reloj); resolver({ codigo: codigo ?? -1, stdout, stderr, cortado }); });
    proceso.stdin.on('error', () => {});
    proceso.stdin.end(stdin ?? '');
  });
}

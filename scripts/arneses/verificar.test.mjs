import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { ejecutar, erroresDeBuild, fallosDeTests, informe, parsear, planDe, RAPIDOS, resumenDeTests } from './verificar.mjs';

// Salidas reales de dotnet, recortadas; ningún test corre dotnet.
const TESTS_CON_ERROR = `Ejecutando pruebas desde C:\\repo\\tests\\X.Api.IntegrationTests\\bin\\Debug\\net10.0\\X.Api.IntegrationTests.dll (net10.0|x64)
con errores X.Api.IntegrationTests.Organization.DelegatedTests.Inviting_again_asks_to_hold_the_owner (7s 696ms)
  desde C:\\repo\\tests\\X.Api.IntegrationTests\\bin\\Debug\\net10.0\\X.Api.IntegrationTests.dll (net10.0|x64)
  201: {"invitationId":"01a20ec9","expiresAtUtc":"2026-12-01T18:19:44Z"}
    a las X.Api.IntegrationTests.Organization.DelegatedTests.AssertProblemAsync(HttpResponseMessage response) en C:\\repo\\Delegated.cs:línea 40
    --- End of stack trace from previous location ---
con errores X.Api.IntegrationTests.Organization.DelegatedTests.A_company_answers_not_found (2s 183ms)
  403: {"title":"Acceso denegado","status":403}
C:\\repo\\tests\\X.Api.IntegrationTests\\bin\\Debug\\net10.0\\X.Api.IntegrationTests.dll (net10.0|x64) error con 2 errores (25s 348ms)
Código de salida: 2
  Salida estándar: [testcontainers.org 00:00:00.17] Connected to Docker:

Resumen de la serie de pruebas: Error!
  total: 2
  error: 2
  correcto: 0
  omitido: 0
  duración: 25s 676ms`;

const TESTS_EN_VERDE = `Test run summary: Passed!
  total: 412
  failed: 0
  succeeded: 410
  skipped: 2
  duration: 12s 001ms`;

const BUILD_CON_ERROR = `C:\\repo\\src\\Api\\RoleService.cs(12,5): error CS0103: El nombre 'x' no existe en el contexto actual [C:\\repo\\src\\Api\\Api.csproj]
C:\\repo\\src\\Api\\RoleService.cs(12,5): error CS0103: El nombre 'x' no existe en el contexto actual [C:\\repo\\src\\Api\\Api.csproj]
C:\\repo\\src\\Api\\Roles.cs(3,1): warning CA1502: 'Save' tiene una complejidad ciclomática de 60 [C:\\repo\\src\\Api\\Api.csproj]`;

test('verificar: los fallos salen con su nombre y su mensaje, sin la pila ni el log de la app', () => {
  const fallos = fallosDeTests(TESTS_CON_ERROR);
  assert.deepEqual(fallos.map((f) => f.test), [
    'X.Api.IntegrationTests.Organization.DelegatedTests.Inviting_again_asks_to_hold_the_owner',
    'X.Api.IntegrationTests.Organization.DelegatedTests.A_company_answers_not_found',
  ]);
  assert.deepEqual(fallos[0].detalle, ['201: {"invitationId":"01a20ec9","expiresAtUtc":"2026-12-01T18:19:44Z"}']);
  assert.deepEqual(fallos[1].detalle, ['403: {"title":"Acceso denegado","status":403}']);
  assert.deepEqual(resumenDeTests(TESTS_CON_ERROR), { total: 2, fallaron: 2, pasaron: 0, omitidos: 0 });
  assert.deepEqual(resumenDeTests(TESTS_EN_VERDE), { total: 412, fallaron: 0, pasaron: 410, omitidos: 2 });
});

test('verificar: el build muestra cada error una vez, y un paso en verde ocupa una línea', () => {
  assert.equal(erroresDeBuild(BUILD_CON_ERROR).length, 2);
  assert.match(erroresDeBuild(BUILD_CON_ERROR)[0], /RoleService\.cs\(12,5\): error CS0103/);
  assert.doesNotMatch(erroresDeBuild(BUILD_CON_ERROR)[0], /Api\.csproj\]$/);
  assert.deepEqual(informe('Domain.UnitTests', 0, TESTS_EN_VERDE, 12), ['Domain.UnitTests: ok, 410 de 412, 2 omitidos (12 s)']);
  const mal = informe('Api.IntegrationTests', 2, TESTS_CON_ERROR, 25);
  assert.equal(mal[0], 'Api.IntegrationTests: 2 con error, 0 de 2 pasaron (25 s)');
  assert.ok(mal.length < 10, 'nada del log de la app');
  assert.match(informe('build', 1, 'algo raro', 3).join('\n'), /últimas líneas[\s\S]*algo raro/);
});

test('verificar: las opciones arman los comandos de siempre', () => {
  assert.deepEqual(planDe(parsear([])).map((p) => p.nombre), ['build', ...RAPIDOS]);
  assert.deepEqual(planDe(parsear(['--sin-build'])).map((p) => p.nombre), RAPIDOS);
  const una = planDe(parsear(['--sin-build', '--proyecto', 'ArquitecturaBaseMultitenant.Application.UnitTests', '--clase', 'RoleServiceTests']));
  assert.deepEqual(una[0].args, ['test', '--project', 'tests/ArquitecturaBaseMultitenant.Application.UnitTests/ArquitecturaBaseMultitenant.Application.UnitTests.csproj', '--no-build', '--', '--filter-class', 'RoleServiceTests']);
  const integra = planDe(parsear(['--integracion', '--espacio', 'X.Organization']));
  assert.deepEqual(integra.map((p) => p.nombre), ['build', 'Api.IntegrationTests']);
  assert.deepEqual(integra[1].args.slice(-2), ['--filter-namespace', 'X.Organization']);
  assert.throws(() => parsear(['--clase', 'X']), /van con --proyecto/);
  assert.throws(() => parsear(['--rapido']), /Opción desconocida/);
});

test('verificar: deja el log entero en .local/verificar y frena después de un build roto', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'verificar-'));
  try {
    spawnSync('git', ['init', '-q', '-b', 'main'], { cwd: dir });
    const corridos = [];
    const salida = [];
    const correr = (args) => {
      corridos.push(args[0]);
      return args[0] === 'build' ? { codigo: 1, texto: BUILD_CON_ERROR } : { codigo: 0, texto: TESTS_EN_VERDE };
    };
    assert.equal(await ejecutar([], { cwd: dir, escribir: (s) => salida.push(s), correr }), 1);
    assert.deepEqual(corridos, ['build']);
    assert.match(salida.join('\n'), /build: 2 error\(es\)[\s\S]*Log entero: .*build\.log/);
    assert.equal(readFileSync(join(dir, '.local', 'verificar', 'build.log'), 'utf8'), BUILD_CON_ERROR);
    salida.length = 0;
    assert.equal(await ejecutar(['--sin-build'], { cwd: dir, escribir: (s) => salida.push(s), correr }), 0);
    assert.equal(salida.length, RAPIDOS.length, 'una línea por proyecto en verde');
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

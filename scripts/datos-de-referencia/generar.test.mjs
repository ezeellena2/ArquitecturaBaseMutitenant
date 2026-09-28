import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { afterEach, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { generateReferenceData } from './generar.mjs';

const roots = [];
const isoXml = `<?xml version="1.0" encoding="UTF-8"?>
<ISO_4217 Pblshd="2026-09-17"><CcyTbl>
  <CcyNtry><CtryNm>ARGENTINA</CtryNm><CcyNm>Peso argentino</CcyNm><Ccy>ARS</Ccy><CcyNbr>032</CcyNbr><CcyMnrUnts>2</CcyMnrUnts></CcyNtry>
  <CcyNtry><CtryNm>SECOND ENTITY</CtryNm><CcyNm>Peso argentino</CcyNm><Ccy>ARS</Ccy><CcyNbr>032</CcyNbr><CcyMnrUnts>2</CcyMnrUnts></CcyNtry>
  <CcyNtry><CtryNm>UNITED STATES</CtryNm><CcyNm>US Dollar</CcyNm><Ccy>USD</Ccy><CcyNbr>840</CcyNbr><CcyMnrUnts>2</CcyMnrUnts></CcyNtry>
  <CcyNtry><CtryNm>JAPAN</CtryNm><CcyNm>Yen</CcyNm><Ccy>JPY</Ccy><CcyNbr>392</CcyNbr><CcyMnrUnts>0</CcyMnrUnts></CcyNtry>
  <CcyNtry><CtryNm>BAHRAIN</CtryNm><CcyNm>Bahraini Dinar</CcyNm><Ccy>BHD</Ccy><CcyNbr>048</CcyNbr><CcyMnrUnts>3</CcyMnrUnts></CcyNtry>
  <CcyNtry><CtryNm>ARAB MONETARY FUND</CtryNm><CcyNm IsFund="true">Arab Accounting Dinar</CcyNm><Ccy>XAD</Ccy><CcyNbr>396</CcyNbr><CcyMnrUnts>2</CcyMnrUnts></CcyNtry>
  <CcyNtry><CtryNm>GOLD</CtryNm><CcyNm>Gold</CcyNm><Ccy>XAU</Ccy><CcyNbr>959</CcyNbr><CcyMnrUnts>N.A.</CcyMnrUnts></CcyNtry>
</CcyTbl></ISO_4217>`;
const validityXml = `<supplementalData><idValidity>
  <id type="region" idStatus="regular">AQ~R BH JP US AC XK</id>
  <id type="region" idStatus="deprecated">AN</id>
  <id type="region" idStatus="reserved">AA</id>
</idValidity></supplementalData>`;
const codeMappings = { supplemental: { codeMappings: {
  AR: { _alpha3: 'ARG', _numeric: '032' },
  BH: { _alpha3: 'BHR', _numeric: '048' },
  JP: { _alpha3: 'JPN', _numeric: '392' },
  US: { _alpha3: 'USA', _numeric: '840' },
  AQ: { _alpha3: 'ATA', _numeric: '010' },
  AC: { _alpha3: 'ASC' },
  XK: { _alpha3: 'XKK', _numeric: '983' },
  AN: { _alpha3: 'ANT', _numeric: '530' },
  AA: { _alpha3: 'AAA', _numeric: '958' }
} } };
const currencyData = { supplemental: { currencyData: { region: {
  AR: [{ ARS: { _from: '1992-01-01' } }, { ARP: { _to: '1992-01-01' } }],
  BH: [{ BHD: { _from: '1965-01-01' } }],
  JP: [{ JPY: { _from: '1871-01-01' } }],
  US: [{ USD: { _from: '1792-01-01' } }],
  AQ: [{ XXX: { _tender: 'false' } }]
} } } };
const cultures = {
  'es-AR': {
    currencies: {
      ARS: { displayName: 'peso argentino', 'displayName-count-other': 'pesos argentinos', symbol: '$' },
      USD: { displayName: 'dólar estadounidense', 'displayName-count-other': 'dólares estadounidenses', symbol: 'US$' },
      JPY: { displayName: 'yen japonés', 'displayName-count-other': 'yenes japoneses', symbol: 'JPY' },
      BHD: { displayName: 'dinar bareiní', 'displayName-count-other': 'dinares bareiníes' }
    },
    territories: { AR: 'Argentina', BH: 'Baréin', JP: 'Japón', US: 'Estados Unidos', AQ: 'Antártida' }
  },
  en: {
    currencies: {
      ARS: { displayName: 'Argentine Peso', 'displayName-count-other': 'Argentine pesos', symbol: 'ARS' },
      USD: { displayName: 'US Dollar', 'displayName-count-other': 'US dollars', symbol: '$' },
      JPY: { displayName: 'Japanese Yen', 'displayName-count-other': 'Japanese yen', symbol: '¥' },
      BHD: { displayName: 'Bahraini Dinar', 'displayName-count-other': 'Bahraini dinars', symbol: 'BHD' }
    },
    territories: { AR: 'Argentina', BH: 'Bahrain', JP: 'Japan', US: 'United States', AQ: 'Antarctica' }
  }
};

afterEach(async () => {
  await Promise.all(roots.splice(0).map(root => rm(root, { recursive: true, force: true })));
});

function sha256(text) {
  return createHash('sha256').update(text).digest('hex');
}

async function putJson(path, value) {
  await mkdir(join(path, '..'), { recursive: true });
  await writeFile(path, `${JSON.stringify(value, null, 2)}\n`);
}

async function fixture({ xml = isoXml, validity = validityXml, region = currencyData, enabled } = {}) {
  const root = await mkdtemp(join(tmpdir(), 'reference-data-'));
  roots.push(root);
  const cldrRoot = join(root, 'cldr');
  const outputDir = join(root, 'output');
  await mkdir(join(root, 'sources'), { recursive: true });
  await writeFile(join(root, 'sources', 'iso4217-list-one.xml'), xml);
  await writeFile(join(root, 'sources', 'cldr-region-validity.xml'), validity);
  await putJson(join(root, 'sources.lock.json'), {
    asOf: '2026-09-17',
    sources: {
      sixIso4217: { url: 'https://example.test/list-one.xml', version: '2026-09-17', sha256: sha256(xml), path: 'sources/iso4217-list-one.xml' },
      cldrRegionValidity: { url: 'https://example.test/region.xml', version: '48.2.0', sha256: sha256(validity), path: 'sources/cldr-region-validity.xml' }
    },
    packages: { cldr: '48.2.0', libphonenumber: '1.13.14', xmlParser: '5.11.1' }
  });
  await putJson(join(root, 'habilitados.json'), enabled ?? {
    defaults: { country: 'AR', currency: 'ARS', culture: 'es-AR', timeZone: 'America/Argentina/Buenos_Aires' },
    displayCultures: { 'es-AR': 'es-AR', 'en-US': 'en' },
    currencies: { JPY: { isEnabled: true, sortOrder: 5 }, ARS: { sortOrder: 1 } },
    countries: { JP: { isEnabled: true, sortOrder: 4 }, AR: { sortOrder: 1 } },
    timeZones: {}, cultures: {}, taxIdTypes: {}
  });
  await putJson(join(cldrRoot, 'cldr-core/supplemental/codeMappings.json'), codeMappings);
  await putJson(join(cldrRoot, 'cldr-core/supplemental/currencyData.json'), region);
  for (const [locale, values] of Object.entries(cultures)) {
    await putJson(join(cldrRoot, `cldr-numbers-full/main/${locale}/currencies.json`), { main: { [locale]: { numbers: { currencies: values.currencies } } } });
    await putJson(join(cldrRoot, `cldr-localenames-full/main/${locale}/territories.json`), { main: { [locale]: { localeDisplayNames: { territories: values.territories } } } });
  }
  const callingCodes = { AR: '54', BH: '973', JP: '81', US: '1' };
  return { root, outputDir, cldrRoot, callingCodeForCountry: code => callingCodes[code] ?? null };
}

async function generated(options) {
  await generateReferenceData(options);
  return {
    currencies: JSON.parse(await readFile(join(options.outputDir, 'currencies.json'), 'utf8')),
    countries: JSON.parse(await readFile(join(options.outputDir, 'countries.json'), 'utf8'))
  };
}

test('rechaza origen ausente y hash incorrecto antes de generar', async () => {
  const options = await fixture();
  await rm(join(options.root, 'sources', 'iso4217-list-one.xml'));
  await assert.rejects(generateReferenceData(options), /iso4217-list-one\.xml.*(no existe|missing)/i);
  const next = await fixture();
  await writeFile(join(next.root, 'sources', 'iso4217-list-one.xml'), 'alterado');
  await assert.rejects(generateReferenceData(next), /sha-?256|hash/i);
});

test('rechaza un snapshot CLDR alterado aunque SIX conserve su hash', async () => {
  const cldr = await fixture();
  await writeFile(join(cldr.root, 'sources', 'cldr-region-validity.xml'), 'alterado');
  await assert.rejects(generateReferenceData(cldr), /SHA-256.*cldr-region-validity\.xml/i);
});

test('cruza ISO vigente, SIX y CLDR sin códigos históricos o privados', async () => {
  const { currencies, countries } = await generated(await fixture());
  assert.deepEqual(currencies.Currencies.map(value => value.Code), ['ARS', 'BHD', 'JPY', 'USD', 'XAD']);
  assert.deepEqual(countries.Countries.map(value => value.Code), ['AQ', 'AR', 'BH', 'JP', 'US']);
  assert.equal(countries.Countries.find(value => value.Code === 'AQ').DefaultCurrencyCode, null);
  assert.equal(countries.Countries.find(value => value.Code === 'AQ').CallingCode, null);
  assert.equal(countries.Countries.find(value => value.Code === 'AQ').IsEnabled, false);
  assert.equal(countries.Countries.find(value => value.Code === 'AR').DefaultTimeZoneId, null);
  assert.equal(currencies.Currencies.find(value => value.Code === 'ARS').NumericCode, '032');
  assert.equal(countries.Countries.find(value => value.Code === 'AR').NumericCode, '032');
  assert.deepEqual(currencies.Currencies.map(value => value.MinorUnits), [2, 3, 0, 2, 2]);
});

test('expande el rango regular AQ~R de CLDR antes del cruce ISO', async () => {
  const { countries } = await generated(await fixture());
  assert.equal(countries.Countries.some(value => value.Code === 'AQ'), true, 'AQ debe salir del inicio del rango regular.');
  assert.equal(countries.Countries.some(value => value.Code === 'AR'), true, 'AR debe salir del final del rango regular.');
});

test('toma símbolos visibles y nombres plurales de CLDR por cultura', async () => {
  const { currencies, countries } = await generated(await fixture());
  const ars = currencies.Currencies.find(value => value.Code === 'ARS');
  const usd = currencies.Currencies.find(value => value.Code === 'USD');
  assert.deepEqual(ars.Translations.map(value => [value.Culture, value.DisplaySymbol]), [['es-AR', '$'], ['en-US', 'ARS']]);
  assert.deepEqual(usd.Translations.map(value => [value.Culture, value.DisplaySymbol]), [['es-AR', 'US$'], ['en-US', '$']]);
  assert.equal(ars.Translations[0].NamePlural, 'pesos argentinos');
  assert.equal(countries.Countries.find(value => value.Code === 'BH').Translations[0].Name, 'Baréin');
});

test('conserva fondo SIX sin nombre CLDR con fallback oficial deshabilitado', async () => {
  const { currencies } = await generated(await fixture());
  const fund = currencies.Currencies.find(value => value.Code === 'XAD');
  assert.equal(fund.MinorUnits, 2);
  assert.equal(fund.IsEnabled, false);
  assert.equal(fund.Translations[0].Name, 'Arab Accounting Dinar');
  assert.equal(fund.Translations[0].DisplaySymbol, 'XAD');
  assert.equal(currencies.Currencies.some(value => value.Code === 'XAU'), false);
  const enabled = await fixture({ enabled: {
    defaults: { country: 'AR', currency: 'ARS', culture: 'es-AR', timeZone: 'America/Argentina/Buenos_Aires' },
    displayCultures: { 'es-AR': 'es-AR', 'en-US': 'en' },
    currencies: { XAD: { isEnabled: true } }, countries: {},
    timeZones: {}, cultures: {}, taxIdTypes: {}
  } });
  await assert.rejects(generateReferenceData(enabled), /XAD.*traducci.n CLDR/i);
});

test('completa locales delta desde el idioma padre CLDR', async () => {
  const options = await fixture();
  const path = join(options.cldrRoot, 'cldr-numbers-full/main/es-AR/currencies.json');
  await putJson(path, { main: { 'es-AR': { numbers: { currencies: { ARS: { symbol: '$' } } } } } });
  await putJson(join(options.cldrRoot, 'cldr-numbers-full/main/es/currencies.json'),
    { main: { es: { numbers: { currencies: cultures['es-AR'].currencies } } } });
  const { currencies } = await generated(options);
  const ars = currencies.Currencies.find(value => value.Code === 'ARS');
  assert.equal(ars.Translations[0].Name, 'peso argentino');
  assert.equal(ars.Translations[0].DisplaySymbol, '$');
  assert.equal(currencies.Currencies.find(value => value.Code === 'USD').Translations[0].DisplaySymbol, 'US$');
});

test('deriva habilitación y orden solo de habilitados.json', async () => {
  const { currencies, countries } = await generated(await fixture());
  assert.deepEqual(currencies.Currencies.map(value => [value.Code, value.IsEnabled, value.SortOrder]), [
    ['ARS', true, 1], ['BHD', false, null], ['JPY', true, 5], ['USD', false, null], ['XAD', false, null]
  ]);
  assert.deepEqual(countries.Countries.map(value => [value.Code, value.IsEnabled, value.SortOrder]), [
    ['AQ', false, null], ['AR', true, 1], ['BH', false, null], ['JP', true, 4], ['US', false, null]
  ]);
  const options = await fixture({ enabled: {
    defaults: { country: 'US', currency: 'USD', culture: 'en-US', timeZone: 'America/New_York' },
    displayCultures: { 'es-AR': 'es-AR', 'en-US': 'en' },
    currencies: { USD: { sortOrder: 2 } }, countries: { US: { sortOrder: 3 } },
    timeZones: {}, cultures: {}, taxIdTypes: {}
  } });
  const changed = await generated(options);
  assert.equal(changed.currencies.Currencies.find(value => value.Code === 'USD').IsEnabled, true);
  assert.equal(changed.currencies.Currencies.find(value => value.Code === 'ARS').IsEnabled, false);
  assert.equal(changed.countries.Countries.find(value => value.Code === 'US').IsEnabled, true);
});

test('rechaza unidades menores ausentes, duplicados contradictorios, FK rotas y overrides desconocidos', async () => {
  const missingUnits = await fixture({ xml: isoXml.replace('<Ccy>BHD</Ccy><CcyNbr>048</CcyNbr><CcyMnrUnts>3', '<Ccy>BHD</Ccy><CcyNbr>048</CcyNbr><CcyMnrUnts>N.A.') });
  await assert.rejects(generateReferenceData(missingUnits), /BHD.*(MinorUnits|unidades)/i);
  const conflicting = await fixture({ xml: isoXml.replace('<CtryNm>SECOND ENTITY</CtryNm><CcyNm>Peso argentino</CcyNm><Ccy>ARS</Ccy><CcyNbr>032</CcyNbr><CcyMnrUnts>2', '<CtryNm>SECOND ENTITY</CtryNm><CcyNm>Peso argentino</CcyNm><Ccy>ARS</Ccy><CcyNbr>032</CcyNbr><CcyMnrUnts>3') });
  await assert.rejects(generateReferenceData(conflicting), /ARS.*(contradict|inconsistent)/i);
  const brokenFk = await fixture({ xml: isoXml.replace(/<CcyNtry><CtryNm>UNITED STATES<\/CtryNm>.*?<\/CcyNtry>/s, '') });
  await assert.rejects(generateReferenceData(brokenFk), /USD.*(SIX|referencia|FK)/i);
  const unknown = await fixture({ enabled: {
    defaults: { country: 'AR', currency: 'ARS', culture: 'es-AR', timeZone: 'America/Argentina/Buenos_Aires' },
    displayCultures: { 'es-AR': 'es-AR', 'en-US': 'en' },
    currencies: { ZZZ: { isEnabled: true } }, countries: {}, timeZones: {}, cultures: {}, taxIdTypes: {}
  } });
  await assert.rejects(generateReferenceData(unknown), /ZZZ.*(desconocid|unknown)/i);
});

test('regenera ambos JSON byte a byte sin red desde snapshots verificados', async () => {
  const options = await fixture();
  options.fetchImpl = () => { throw new Error('La generación normal no debe usar red'); };
  await generateReferenceData(options);
  const first = await Promise.all(['currencies.json', 'countries.json'].map(name => readFile(join(options.outputDir, name))));
  await generateReferenceData(options);
  const second = await Promise.all(['currencies.json', 'countries.json'].map(name => readFile(join(options.outputDir, name))));
  assert.deepEqual(second, first);
});

test('--refresh descarga snapshots solo de forma explícita y actualiza hashes', async () => {
  const options = await fixture();
  const visited = [];
  options.refresh = true;
  options.fetchImpl = async url => {
    visited.push(url);
    return new Response(url.endsWith('/list-one.xml') ? isoXml : validityXml);
  };
  await generateReferenceData(options);
  assert.equal(visited.length, 2);
  const lock = await readFile(join(options.root, 'sources.lock.json'), 'utf8');
  assert.equal(JSON.parse(lock).sources.sixIso4217.sha256, sha256(isoXml));
  assert.equal(JSON.parse(lock).sources.cldrRegionValidity.sha256, sha256(validityXml));
});

test('--refresh inválido conserva snapshots, lock y salidas previas byte a byte', async () => {
  const options = await fixture();
  await generateReferenceData(options);
  const paths = [
    join(options.root, 'sources/iso4217-list-one.xml'),
    join(options.root, 'sources/cldr-region-validity.xml'),
    join(options.root, 'sources.lock.json'),
    join(options.outputDir, 'currencies.json'),
    join(options.outputDir, 'countries.json')
  ];
  const before = await Promise.all(paths.map(path => readFile(path)));
  const incompatibleIso = isoXml.replace(/<CcyNtry><CtryNm>UNITED STATES<\/CtryNm>.*?<\/CcyNtry>/s, '');
  await assert.rejects(generateReferenceData({
    ...options,
    refresh: true,
    fetchImpl: async url => new Response(url.endsWith('/list-one.xml') ? incompatibleIso : validityXml)
  }), /USD.*(SIX|referencia|FK)/i);
  const after = await Promise.all(paths.map(path => readFile(path)));
  assert.deepEqual(after, before);
});

test('snapshots oficiales regeneran los JSON versionados byte a byte sin red', async () => {
  const root = dirname(fileURLToPath(import.meta.url));
  const outputDir = await mkdtemp(join(tmpdir(), 'reference-data-official-'));
  roots.push(outputDir);
  await generateReferenceData({ root, outputDir, fetchImpl: () => { throw new Error('No se debe usar red'); } });
  const committedDir = join(root, '../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData');
  for (const name of ['currencies.json', 'countries.json']) {
    assert.deepEqual(await readFile(join(outputDir, name)), await readFile(join(committedDir, name)), `${name} no coincide con su snapshot.`);
  }
  const currencies = JSON.parse(await readFile(join(outputDir, 'currencies.json'), 'utf8'));
  const countries = JSON.parse(await readFile(join(outputDir, 'countries.json'), 'utf8'));
  assert.equal(countries.Countries.length, 249);
  assert.equal(currencies.Currencies.some(value => value.MinorUnits === 0), true);
  assert.equal(currencies.Currencies.some(value => value.MinorUnits === 2), true);
  assert.equal(currencies.Currencies.some(value => value.MinorUnits === 3), true);
  assert.equal(currencies.Currencies.find(value => value.Code === 'XAD').IsEnabled, false);
  assert.equal(countries.Countries.every(value => value.CallingCode !== null && value.DefaultCurrencyCode !== null || !value.IsEnabled), true);
  assert.match(currencies.Sources.Packages['libphonenumber-js'].Url, /^https:\/\//);
});

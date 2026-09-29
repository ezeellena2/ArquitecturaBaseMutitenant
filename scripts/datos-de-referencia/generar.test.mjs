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
const ianaTab = `# tzdb fixture
AR\t-3124-06411\tAmerica/Argentina/Cordoba
AR\t-3436-05827\tAmerica/Argentina/Buenos_Aires
AR,BH\t+2517+05132\tAsia/Qatar
BH\t+2518+05133\tAsia/Test_City
JP\t+353916+1394441\tAsia/Tokyo
US\t+404251-0740023\tAmerica/New_York
`;
const cultureSource = { Cultures: [
  { Code: 'es-AR', LanguageCode: 'es', CountryCode: 'AR', DatePattern: 'dd/MM/yyyy', TimePattern: 'HH:mm', DateTimePattern: 'dd/MM/yyyy HH:mm', LongDatePattern: "d 'de' MMMM 'de' yyyy", AmDesignator: 'a. m.', PmDesignator: 'p. m.', DecimalSeparator: ',', GroupSeparator: '.', CurrencyPattern: '{symbol} {number}', PercentPattern: '{number} %', FallbackCulture: null },
  { Code: 'en-US', LanguageCode: 'en', CountryCode: 'US', DatePattern: 'MM/dd/yyyy', TimePattern: 'h:mm tt', DateTimePattern: 'MM/dd/yyyy h:mm tt', LongDatePattern: 'MMMM d, yyyy', AmDesignator: 'AM', PmDesignator: 'PM', DecimalSeparator: '.', GroupSeparator: ',', CurrencyPattern: '{symbol}{number}', PercentPattern: '{number}%', FallbackCulture: 'es-AR' }
] };
const taxIdSource = { TaxIdTypes: [
  { Code: 'AR-CUIT', CountryCode: 'AR', Label: 'CUIT', Mask: '99-99999999-9', ValidatorKey: 'ar-cuit-mod11', AppliesTo: 'Both', Names: { 'es-AR': 'CUIT', 'en-US': 'CUIT' } },
  { Code: 'AR-DNI', CountryCode: 'AR', Label: 'DNI', Mask: '99999999', ValidatorKey: 'ar-dni-length', AppliesTo: 'Person', Names: { 'es-AR': 'DNI', 'en-US': 'National ID' } }
] };
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
    languages: { es: 'español', en: 'inglés' },
    currencies: {
      ARS: { displayName: 'peso argentino', 'displayName-count-other': 'pesos argentinos', symbol: '$' },
      USD: { displayName: 'dólar estadounidense', 'displayName-count-other': 'dólares estadounidenses', symbol: 'US$' },
      JPY: { displayName: 'yen japonés', 'displayName-count-other': 'yenes japoneses', symbol: 'JPY' },
      BHD: { displayName: 'dinar bareiní', 'displayName-count-other': 'dinares bareiníes' }
    },
    territories: { AR: 'Argentina', BH: 'Baréin', JP: 'Japón', US: 'Estados Unidos', AQ: 'Antártida' }
  },
  en: {
    languages: { es: 'Spanish', en: 'English' },
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

async function fixture({ xml = isoXml, validity = validityXml, iana = ianaTab, region = currencyData, enabled, cultureRows = cultureSource, taxRows = taxIdSource } = {}) {
  const root = await mkdtemp(join(tmpdir(), 'reference-data-'));
  roots.push(root);
  const cldrRoot = join(root, 'cldr');
  const outputDir = join(root, 'output');
  await mkdir(join(root, 'sources'), { recursive: true });
  await writeFile(join(root, 'sources', 'iso4217-list-one.xml'), xml);
  await writeFile(join(root, 'sources', 'cldr-region-validity.xml'), validity);
  await writeFile(join(root, 'sources', 'iana-zone1970.tab'), iana);
  await putJson(join(root, 'sources.lock.json'), {
    asOf: '2026-09-17',
    sources: {
      sixIso4217: { url: 'https://example.test/list-one.xml', version: '2026-09-17', sha256: sha256(xml), path: 'sources/iso4217-list-one.xml' },
      cldrRegionValidity: { url: 'https://example.test/region.xml', version: '48.2.0', sha256: sha256(validity), path: 'sources/cldr-region-validity.xml' },
      ianaZone1970: { url: 'https://example.test/tzdb-2026d/zone1970.tab', version: '2026d', sha256: sha256(iana), path: 'sources/iana-zone1970.tab' }
    },
    packages: { cldr: '48.2.0', cldrBcp47: '48.2.0', cldrDates: '48.2.0', libphonenumber: '1.13.14', xmlParser: '5.11.1' }
  });
  await putJson(join(root, 'habilitados.json'), enabled ?? {
    defaults: { country: 'AR', currency: 'ARS', culture: 'es-AR', timeZone: 'America/Argentina/Buenos_Aires' },
    displayCultures: { 'es-AR': 'es-AR', 'en-US': 'en' },
    currencies: { JPY: { isEnabled: true, sortOrder: 5 }, ARS: { sortOrder: 1 } },
    countries: { JP: { isEnabled: true, sortOrder: 4 }, AR: { sortOrder: 1 } },
    timeZones: {}, cultures: {}, taxIdTypes: {}
  });
  await putJson(join(root, 'cultures.source.json'), cultureRows);
  await putJson(join(root, 'tax-id-types.source.json'), taxRows);
  await putJson(join(root, 'ciudades.es.json'), { 'America/New_York': 'Nueva York', 'America/Argentina/Cordoba': 'Córdoba' });
  await putJson(join(root, 'ciudades.en.json'), {});
  await putJson(join(cldrRoot, 'cldr-core/supplemental/codeMappings.json'), codeMappings);
  await putJson(join(cldrRoot, 'cldr-core/supplemental/currencyData.json'), region);
  await putJson(join(cldrRoot, 'cldr-core/supplemental/primaryZones.json'), { supplemental: { primaryZones: {} } });
  await putJson(join(cldrRoot, 'cldr-bcp47/bcp47/timezone.json'), { keyword: { u: { tz: {} } } });
  for (const [locale, values] of Object.entries(cultures)) {
    await putJson(join(cldrRoot, `cldr-numbers-full/main/${locale}/currencies.json`), { main: { [locale]: { numbers: { currencies: values.currencies } } } });
    await putJson(join(cldrRoot, `cldr-localenames-full/main/${locale}/territories.json`), { main: { [locale]: { localeDisplayNames: { territories: values.territories } } } });
    await putJson(join(cldrRoot, `cldr-localenames-full/main/${locale}/languages.json`), { main: { [locale]: { localeDisplayNames: { languages: values.languages } } } });
    await putJson(join(cldrRoot, `cldr-localenames-full/main/${locale}/localeDisplayNames.json`), { main: { [locale]: { localeDisplayNames: { localeDisplayPattern: { localePattern: '{0} ({1})' } } } } });
    await putJson(join(cldrRoot, `cldr-dates-full/main/${locale}/timeZoneNames.json`), { main: { [locale]: { dates: { timeZoneNames: { zone: {
      America: { Argentina: { Buenos_Aires: { exemplarCity: 'Buenos Aires' }, Cordoba: { exemplarCity: 'Cordoba' } }, New_York: { exemplarCity: 'New York' } },
      Asia: { Qatar: { exemplarCity: 'Doha' }, Tokyo: { exemplarCity: 'Tokyo' } }
    } } } } } });
  }
  const callingCodes = { AR: '54', BH: '973', JP: '81', US: '1' };
  return { root, outputDir, cldrRoot, callingCodeForCountry: code => callingCodes[code] ?? null };
}

async function generated(options) {
  await generateReferenceData(options);
  return {
    currencies: JSON.parse(await readFile(join(options.outputDir, 'currencies.json'), 'utf8')),
    countries: JSON.parse(await readFile(join(options.outputDir, 'countries.json'), 'utf8')),
    timeZones: JSON.parse(await readFile(join(options.outputDir, 'time-zones.json'), 'utf8')),
    cultures: JSON.parse(await readFile(join(options.outputDir, 'cultures.json'), 'utf8')),
    taxIdTypes: JSON.parse(await readFile(join(options.outputDir, 'tax-id-types.json'), 'utf8'))
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

test('rechaza un snapshot IANA alterado antes de publicar catálogos', async () => {
  const options = await fixture();
  await writeFile(join(options.root, 'sources', 'iana-zone1970.tab'), 'alterado');
  await assert.rejects(generateReferenceData(options), /SHA-256.*iana-zone1970\.tab/i);
});

test('cruza ISO vigente, SIX y CLDR sin códigos históricos o privados', async () => {
  const { currencies, countries } = await generated(await fixture());
  assert.deepEqual(currencies.Currencies.map(value => value.Code), ['ARS', 'BHD', 'JPY', 'USD', 'XAD']);
  assert.deepEqual(countries.Countries.map(value => value.Code), ['AQ', 'AR', 'BH', 'JP', 'US']);
  assert.equal(countries.Countries.find(value => value.Code === 'AQ').DefaultCurrencyCode, null);
  assert.equal(countries.Countries.find(value => value.Code === 'AQ').CallingCode, null);
  assert.equal(countries.Countries.find(value => value.Code === 'AQ').IsEnabled, false);
  assert.equal(countries.Countries.find(value => value.Code === 'AR').DefaultTimeZoneId, 'America/Argentina/Buenos_Aires');
  assert.equal(currencies.Currencies.find(value => value.Code === 'ARS').NumericCode, '032');
  assert.equal(countries.Countries.find(value => value.Code === 'AR').NumericCode, '032');
  assert.deepEqual(currencies.Currencies.map(value => value.MinorUnits), [2, 3, 0, 2, 2]);
});

test('conserva todos los países de una zona IANA y añade UTC sin país', async () => {
  const { timeZones, countries } = await generated(await fixture());
  const shared = timeZones.TimeZones.find(value => value.Id === 'Asia/Qatar');
  assert.deepEqual(shared.CountryCodes, ['AR', 'BH']);
  assert.equal(shared.IsEnabled, true, 'Alcanza con que AR esté habilitado.');
  assert.deepEqual(timeZones.TimeZones.find(value => value.Id === 'UTC').CountryCodes, []);
  assert.equal(timeZones.TimeZones.find(value => value.Id === 'UTC').IsEnabled, true);
  assert.equal(timeZones.TimeZones.find(value => value.Id === 'America/New_York').IsEnabled, false);
  assert.equal(countries.Countries.find(value => value.Code === 'BH').DefaultTimeZoneId, null,
    'un país con varias zonas y sin zona principal CLDR no debe recibir la primera de IANA');
  assert.equal(countries.Countries.find(value => value.Code === 'AQ').DefaultTimeZoneId, null);
});

test('usa exemplarCity CLDR, override de ciudad y fallback del ID', async () => {
  const { timeZones } = await generated(await fixture());
  const translations = id => timeZones.TimeZones.find(value => value.Id === id).Translations;
  assert.deepEqual(translations('America/New_York').map(value => value.City), ['Nueva York', 'New York']);
  assert.deepEqual(translations('America/Argentina/Cordoba').map(value => value.City), ['Córdoba', 'Cordoba']);
  assert.deepEqual(translations('Asia/Qatar').map(value => value.City), ['Doha', 'Doha']);
  assert.deepEqual(translations('Asia/Test_City').map(value => value.City), ['Test City', 'Test City']);
  assert.deepEqual(translations('UTC').map(value => value.City), ['UTC', 'UTC']);
});

test('resuelve exemplarCity del alias histórico sin override manual', async () => {
  const options = await fixture();
  await putJson(join(options.root, 'ciudades.es.json'), { 'America/New_York': 'Nueva York' });
  await putJson(join(options.cldrRoot, 'cldr-bcp47/bcp47/timezone.json'), { keyword: { u: { tz: {
    arcor: { _alias: 'America/Cordoba America/Argentina/Cordoba', _iana: 'America/Argentina/Cordoba' }
  } } } });
  const path = join(options.cldrRoot, 'cldr-dates-full/main/es-AR/timeZoneNames.json');
  const names = JSON.parse(await readFile(path, 'utf8'));
  delete names.main['es-AR'].dates.timeZoneNames.zone.America.Argentina.Cordoba;
  names.main['es-AR'].dates.timeZoneNames.zone.America.Cordoba = { exemplarCity: 'Córdoba' };
  await putJson(path, names);
  const { timeZones } = await generated(options);
  assert.equal(timeZones.TimeZones.find(value => value.Id === 'America/Argentina/Cordoba').Translations[0].City, 'Córdoba');
});

test('genera culturas traducidas y patrones editables con un único default', async () => {
  const { cultures } = await generated(await fixture());
  const rows = cultures.Cultures;
  assert.deepEqual(rows.map(value => [value.Code, value.IsEnabled, value.IsDefault]), [['es-AR', true, true], ['en-US', true, false]]);
  assert.equal(rows.find(value => value.Code === 'es-AR').DatePattern, 'dd/MM/yyyy');
  assert.equal(rows.find(value => value.Code === 'en-US').TimePattern, 'h:mm tt');
  assert.equal(rows.find(value => value.Code === 'es-AR').AmDesignator, 'a. m.');
  assert.equal(rows.find(value => value.Code === 'en-US').PmDesignator, 'PM');
  assert.equal(rows.find(value => value.Code === 'es-AR').Translations.find(value => value.DisplayCulture === 'es-AR').Name, 'Español (Argentina)');
  assert.equal(rows.find(value => value.Code === 'es-AR').Translations.find(value => value.DisplayCulture === 'en-US').Name, 'Spanish (Argentina)');
  assert.equal(rows.filter(value => value.IsDefault).length, 1);
});

test('nombres de culturas salen de CLDR sin consultar Intl.DisplayNames', async () => {
  const options = await fixture();
  const path = join(options.cldrRoot, 'cldr-localenames-full/main/es-AR/languages.json');
  await putJson(path, { main: { 'es-AR': { localeDisplayNames: { languages: { es: 'castellano del fixture', en: 'inglés' } } } } });
  const original = Intl.DisplayNames;
  Intl.DisplayNames = class { constructor() { throw new Error('Intl.DisplayNames no debe usarse'); } };
  try {
    const { cultures } = await generated(options);
    assert.equal(cultures.Cultures.find(value => value.Code === 'es-AR').Translations[0].Name, 'Castellano del fixture (Argentina)');
  } finally {
    Intl.DisplayNames = original;
  }
});

test('CI fija Node y prueba el generador con npm ci y npm test', async () => {
  const workflow = await readFile(join(dirname(fileURLToPath(import.meta.url)), '../../.github/workflows/ci.yml'), 'utf8');
  const setup = workflow.match(/      - name: Configurar Node[\s\S]*?(?=\n      - name:|$)/)?.[0] ?? '';
  assert.match(setup, /uses:\s*actions\/setup-node@49933ea5288caeca8642d1e84afbd3f7d6820020\s+# v4\.4\.0/);
  assert.match(setup, /node-version-file:\s*scripts\/datos-de-referencia\/\.node-version/);
  const generator = workflow.match(/      - name: Probar generador de datos de referencia[\s\S]*?(?=\n      - name:|$)/)?.[0] ?? '';
  assert.match(generator, /working-directory:\s*scripts\/datos-de-referencia/);
  assert.match(generator, /run:\s*\|\s*\n\s*npm ci\s*\n\s*npm test/);
});

test('genera tipos fiscales desde source y los habilita por país', async () => {
  const { taxIdTypes } = await generated(await fixture());
  const cuit = taxIdTypes.TaxIdTypes.find(value => value.Code === 'AR-CUIT');
  assert.equal(cuit.IsEnabled, true);
  assert.equal(cuit.ValidatorKey, 'ar-cuit-mod11');
  assert.equal(cuit.Mask, '99-99999999-9');
  assert.deepEqual(cuit.Translations.map(value => value.Name), ['CUIT', 'CUIT']);
  assert.equal(taxIdTypes.TaxIdTypes.find(value => value.Code === 'AR-DNI').Translations[1].Name, 'National ID');
});

test('aplica overrides y deshabilita país sin zona propia aunque se pida habilitarlo', async () => {
  const options = await fixture({ iana: ianaTab.replace('JP\t+353916+1394441\tAsia/Tokyo\n', '') });
  const path = join(options.root, 'habilitados.json');
  const enabled = JSON.parse(await readFile(path, 'utf8'));
  enabled.timeZones = { 'Asia/Qatar': { isEnabled: false, sortOrder: 9 }, UTC: { sortOrder: 0 } };
  enabled.cultures = { 'en-US': { isEnabled: false, sortOrder: 2 } };
  enabled.taxIdTypes = { 'AR-DNI': { isEnabled: false, sortOrder: 4 } };
  await putJson(path, enabled);
  const { countries, timeZones, cultures, taxIdTypes } = await generated(options);
  assert.equal(countries.Countries.find(value => value.Code === 'JP').DefaultTimeZoneId, null);
  assert.equal(countries.Countries.find(value => value.Code === 'JP').IsEnabled, false);
  assert.deepEqual(timeZones.TimeZones.find(value => value.Id === 'Asia/Qatar').IsEnabled, false);
  assert.equal(timeZones.TimeZones.find(value => value.Id === 'Asia/Qatar').SortOrder, 9);
  assert.equal(timeZones.TimeZones.find(value => value.Id === 'UTC').SortOrder, 0);
  assert.equal(cultures.Cultures.find(value => value.Code === 'en-US').IsEnabled, false);
  assert.equal(cultures.Cultures.find(value => value.Code === 'en-US').SortOrder, 2);
  assert.equal(taxIdTypes.TaxIdTypes.find(value => value.Code === 'AR-DNI').IsEnabled, false);
  assert.equal(taxIdTypes.TaxIdTypes.find(value => value.Code === 'AR-DNI').SortOrder, 4);
});

test('no habilita tipo fiscal de un país deshabilitado por override', async () => {
  const options = await fixture();
  const path = join(options.root, 'habilitados.json');
  const enabled = JSON.parse(await readFile(path, 'utf8'));
  enabled.countries.AR = { isEnabled: false };
  enabled.taxIdTypes['AR-CUIT'] = { isEnabled: true };
  await putJson(path, enabled);
  const { countries, taxIdTypes } = await generated(options);
  assert.equal(countries.Countries.find(value => value.Code === 'AR').IsEnabled, false);
  assert.equal(taxIdTypes.TaxIdTypes.find(value => value.Code === 'AR-CUIT').IsEnabled, false);
});

test('rechaza país habilitado con moneda o zona predeterminada deshabilitada', async () => {
  for (const [catalog, code, expected] of [
    ['currencies', 'ARS', /AR.*ARS.*deshabilitada/i],
    ['timeZones', 'America/Argentina/Buenos_Aires', /AR.*Buenos_Aires.*deshabilitada/i]
  ]) {
    const options = await fixture();
    const path = join(options.root, 'habilitados.json');
    const enabled = JSON.parse(await readFile(path, 'utf8'));
    enabled[catalog][code] = { isEnabled: false };
    await putJson(path, enabled);
    await assert.rejects(generateReferenceData(options), expected);
  }
});

test('rechaza habilitar cultura sin traducciones en displayCultures', async () => {
  const extraCultures = structuredClone(cultureSource);
  extraCultures.Cultures.push({ ...extraCultures.Cultures[0], Code: 'es-BH', CountryCode: 'BH', FallbackCulture: 'es-AR' });
  const options = await fixture({ cultureRows: extraCultures });
  const path = join(options.root, 'habilitados.json');
  const enabled = JSON.parse(await readFile(path, 'utf8'));
  enabled.cultures['es-BH'] = { isEnabled: true };
  await putJson(path, enabled);
  await assert.rejects(generateReferenceData(options), /es-BH.*displayCultures.*traducciones/i);
});

test('rechaza FK IANA y default incompatibles, y traducciones fiscales faltantes', async () => {
  const unknownCountry = await fixture({ iana: `${ianaTab}ZZ\t+0000+00000\tEtc/Test\n` });
  await assert.rejects(generateReferenceData(unknownCountry), /ZZ.*(IANA|país|FK)/i);
  const wrongDefault = await fixture();
  const configPath = join(wrongDefault.root, 'habilitados.json');
  const config = JSON.parse(await readFile(configPath, 'utf8'));
  config.defaults.timeZone = 'Asia/Tokyo';
  await putJson(configPath, config);
  await assert.rejects(generateReferenceData(wrongDefault), /Asia\/Tokyo.*AR|AR.*Asia\/Tokyo/i);
  const missingName = await fixture({ taxRows: { TaxIdTypes: [{ ...taxIdSource.TaxIdTypes[0], Names: { 'es-AR': 'CUIT' } }] } });
  await assert.rejects(generateReferenceData(missingName), /AR-CUIT.*traducci.n.*en-US/i);
  const disabledDefault = await fixture();
  const defaultPath = join(disabledDefault.root, 'habilitados.json');
  const settings = JSON.parse(await readFile(defaultPath, 'utf8'));
  settings.cultures['es-AR'] = { isEnabled: false };
  await putJson(defaultPath, settings);
  await assert.rejects(generateReferenceData(disabledDefault), /cultura predeterminada habilitada/i);
});

test('los patrones y nombres fiscales salen de source y no del generador', async () => {
  const customCultures = structuredClone(cultureSource);
  customCultures.Cultures[0].DatePattern = 'yyyy-MM-dd';
  customCultures.Cultures[0].AmDesignator = 'mañana';
  const customTaxIds = structuredClone(taxIdSource);
  customTaxIds.TaxIdTypes[0].Names['en-US'] = 'Tax number';
  const { cultures, taxIdTypes } = await generated(await fixture({ cultureRows: customCultures, taxRows: customTaxIds }));
  assert.equal(cultures.Cultures.find(value => value.Code === 'es-AR').DatePattern, 'yyyy-MM-dd');
  assert.equal(cultures.Cultures.find(value => value.Code === 'es-AR').AmDesignator, 'mañana');
  assert.equal(taxIdTypes.TaxIdTypes.find(value => value.Code === 'AR-CUIT').Translations[1].Name, 'Tax number');
});

test('rechaza culturas sin designadores de mañana y tarde', async () => {
  const customCultures = structuredClone(cultureSource);
  delete customCultures.Cultures[0].AmDesignator;
  const options = await fixture({ cultureRows: customCultures });
  await assert.rejects(generateReferenceData(options), /es-AR\.AmDesignator/);
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

test('hereda ciudades CLDR anidadas del idioma padre al combinar un locale delta', async () => {
  const options = await fixture();
  const childPath = join(options.cldrRoot, 'cldr-dates-full/main/es-AR/timeZoneNames.json');
  const childZones = { America: { Argentina: { Cordoba: { exemplarCity: 'Córdoba' } } } };
  await putJson(childPath, { main: { 'es-AR': { dates: { timeZoneNames: { zone: childZones } } } } });
  const parentZones = { America: { Argentina: { Buenos_Aires: { exemplarCity: 'Buenos Aires desde es' } } } };
  await putJson(join(options.cldrRoot, 'cldr-dates-full/main/es/timeZoneNames.json'),
    { main: { es: { dates: { timeZoneNames: { zone: parentZones } } } } });
  const { timeZones } = await generated(options);
  assert.equal(timeZones.TimeZones.find(value => value.Id === 'America/Argentina/Buenos_Aires').Translations[0].City, 'Buenos Aires desde es');
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

test('regenera los cinco JSON byte a byte sin red desde snapshots verificados', async () => {
  const options = await fixture();
  options.fetchImpl = () => { throw new Error('La generación normal no debe usar red'); };
  await generateReferenceData(options);
  const names = ['currencies.json', 'countries.json', 'time-zones.json', 'cultures.json', 'tax-id-types.json'];
  const first = await Promise.all(names.map(name => readFile(join(options.outputDir, name))));
  await generateReferenceData(options);
  const second = await Promise.all(names.map(name => readFile(join(options.outputDir, name))));
  assert.deepEqual(second, first);
});

test('--refresh descarga snapshots solo de forma explícita y actualiza hashes', async () => {
  const options = await fixture();
  const visited = [];
  options.refresh = true;
  options.fetchImpl = async url => {
    visited.push(url);
    return new Response(url.endsWith('/list-one.xml') ? isoXml : url.endsWith('/region.xml') ? validityXml : ianaTab);
  };
  await generateReferenceData(options);
  assert.equal(visited.length, 3);
  const lock = await readFile(join(options.root, 'sources.lock.json'), 'utf8');
  assert.equal(JSON.parse(lock).sources.sixIso4217.sha256, sha256(isoXml));
  assert.equal(JSON.parse(lock).sources.cldrRegionValidity.sha256, sha256(validityXml));
  assert.equal(JSON.parse(lock).sources.ianaZone1970.sha256, sha256(ianaTab));
});

test('--refresh inválido conserva snapshots, lock y salidas previas byte a byte', async () => {
  const options = await fixture();
  await generateReferenceData(options);
  const paths = [
    join(options.root, 'sources/iso4217-list-one.xml'),
    join(options.root, 'sources/cldr-region-validity.xml'),
    join(options.root, 'sources/iana-zone1970.tab'),
    join(options.root, 'sources.lock.json'),
    join(options.outputDir, 'currencies.json'),
    join(options.outputDir, 'countries.json'),
    join(options.outputDir, 'time-zones.json'),
    join(options.outputDir, 'cultures.json'),
    join(options.outputDir, 'tax-id-types.json')
  ];
  const before = await Promise.all(paths.map(path => readFile(path)));
  const incompatibleIso = isoXml.replace(/<CcyNtry><CtryNm>UNITED STATES<\/CtryNm>.*?<\/CcyNtry>/s, '');
  await assert.rejects(generateReferenceData({
    ...options,
    refresh: true,
    fetchImpl: async url => new Response(url.endsWith('/list-one.xml') ? incompatibleIso : url.endsWith('/region.xml') ? validityXml : ianaTab)
  }), /USD.*(SIX|referencia|FK)/i);
  const after = await Promise.all(paths.map(path => readFile(path)));
  assert.deepEqual(after, before);
});

test('--refresh con IANA inválida conserva snapshot, lock y cinco salidas', async () => {
  const options = await fixture();
  await generateReferenceData(options);
  const paths = [
    join(options.root, 'sources/iana-zone1970.tab'),
    join(options.root, 'sources.lock.json'),
    ...['currencies', 'countries', 'time-zones', 'cultures', 'tax-id-types'].map(name => join(options.outputDir, `${name}.json`))
  ];
  const before = await Promise.all(paths.map(path => readFile(path)));
  await assert.rejects(generateReferenceData({
    ...options,
    refresh: true,
    fetchImpl: async url => new Response(url.endsWith('/list-one.xml') ? isoXml : url.endsWith('/region.xml') ? validityXml : 'no es una fila IANA')
  }), /IANA inválida|IANA.*zonas/i);
  assert.deepEqual(await Promise.all(paths.map(path => readFile(path))), before);
});

test('snapshots oficiales regeneran los cinco JSON versionados byte a byte sin red', async () => {
  const root = dirname(fileURLToPath(import.meta.url));
  const outputDir = await mkdtemp(join(tmpdir(), 'reference-data-official-'));
  roots.push(outputDir);
  await generateReferenceData({ root, outputDir, fetchImpl: () => { throw new Error('No se debe usar red'); } });
  const committedDir = join(root, '../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData');
  for (const name of ['currencies.json', 'countries.json', 'time-zones.json', 'cultures.json', 'tax-id-types.json']) {
    assert.deepEqual(await readFile(join(outputDir, name)), await readFile(join(committedDir, name)), `${name} no coincide con su snapshot.`);
  }
  const currencies = JSON.parse(await readFile(join(outputDir, 'currencies.json'), 'utf8'));
  const countries = JSON.parse(await readFile(join(outputDir, 'countries.json'), 'utf8'));
  const timeZones = JSON.parse(await readFile(join(outputDir, 'time-zones.json'), 'utf8'));
  const cultures = JSON.parse(await readFile(join(outputDir, 'cultures.json'), 'utf8'));
  const taxIdTypes = JSON.parse(await readFile(join(outputDir, 'tax-id-types.json'), 'utf8'));
  assert.equal(countries.Countries.length, 249);
  assert.equal(countries.Countries.find(value => value.Code === 'DE').DefaultTimeZoneId, 'Europe/Berlin');
  assert.equal(countries.Countries.find(value => value.Code === 'UA').DefaultTimeZoneId, 'Europe/Kyiv');
  assert.equal(countries.Countries.find(value => value.Code === 'AR').DefaultTimeZoneId, 'America/Argentina/Buenos_Aires');
  assert.equal(countries.Countries.find(value => value.Code === 'US').DefaultTimeZoneId, null);
  assert.equal(currencies.Currencies.some(value => value.MinorUnits === 0), true);
  assert.equal(currencies.Currencies.some(value => value.MinorUnits === 2), true);
  assert.equal(currencies.Currencies.some(value => value.MinorUnits === 3), true);
  assert.equal(currencies.Currencies.find(value => value.Code === 'XAD').IsEnabled, false);
  assert.equal(countries.Countries.every(value => value.CallingCode !== null && value.DefaultCurrencyCode !== null || !value.IsEnabled), true);
  assert.equal(timeZones.TimeZones.length, 313);
  assert.deepEqual(timeZones.TimeZones.find(value => value.Id === 'Asia/Dubai').CountryCodes, ['AE', 'OM', 'RE', 'SC', 'TF']);
  assert.deepEqual(timeZones.TimeZones.find(value => value.Id === 'UTC').CountryCodes, []);
  assert.equal(timeZones.TimeZones.find(value => value.Id === 'Asia/Kolkata').Translations.find(value => value.Culture === 'es-AR').City, 'Calcuta');
  assert.equal(timeZones.TimeZones.find(value => value.Id === 'Europe/Kyiv').Translations.find(value => value.Culture === 'es-AR').City, 'Kiev');
  assert.equal(timeZones.TimeZones.find(value => value.Id === 'America/Argentina/Cordoba').Translations.find(value => value.Culture === 'es-AR').City, 'Córdoba');
  for (const code of ['BV', 'HM']) {
    assert.equal(countries.Countries.find(value => value.Code === code).DefaultTimeZoneId, null);
    assert.equal(countries.Countries.find(value => value.Code === code).IsEnabled, false);
  }
  for (const country of countries.Countries) {
    if (country.DefaultTimeZoneId === null) continue;
    assert(timeZones.TimeZones.find(value => value.Id === country.DefaultTimeZoneId).CountryCodes.includes(country.Code));
  }
  const enabledCultures = cultures.Cultures.filter(value => value.IsEnabled).map(value => value.Code).sort();
  for (const rows of [currencies.Currencies, countries.Countries, timeZones.TimeZones, taxIdTypes.TaxIdTypes]) {
    for (const row of rows.filter(value => value.IsEnabled)) {
      assert.deepEqual(row.Translations.map(value => value.Culture).sort(), enabledCultures);
    }
  }
  for (const row of cultures.Cultures.filter(value => value.IsEnabled)) {
    assert.deepEqual(row.Translations.map(value => value.DisplayCulture).sort(), enabledCultures);
  }
  assert.match(currencies.Sources.Packages['libphonenumber-js'].Url, /^https:\/\//);
  assert.match(timeZones.Sources.Packages['cldr-dates-full'].Url, /^https:\/\//);
});

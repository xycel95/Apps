// Developer check of the production HTML sanitizer and print template.
// Requires Playwright plus an available Chromium executable.
const fs = require('fs');
const path = require('path');
const assert = require('assert/strict');
const { chromium } = require(process.env.CODEX_PRIMARY_RUNTIME_NODE_MODULES + '/playwright');

(async () => {
  const source = fs.readFileSync(path.resolve(__dirname, '../src/CorreoDiario.Windows/PdfRenderer.cs'), 'utf8');
  const begin = source.indexOf('const doc=new DOMParser()');
  const end = source.indexOf('})()', begin) + 4;
  assert(begin > 0 && end > begin);
  const sanitize = input => '(()=>{const input=' + JSON.stringify(input) + ';' + source.slice(begin, end);
  const template = source.match(/var html = """([\s\S]*?)""";/)[1];
  const browser = await chromium.launch({ executablePath: process.env.CORREODIARIO_CHROMIUM || '/usr/bin/chromium', headless: true });
  try {
    const page = await browser.newPage();
    const requests = [];
    page.on('request', r => { if (/^https?:/.test(r.url())) requests.push(r.url()); });
    const png = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j9mUAAAAASUVORK5CYII=';
    const input = {
      html: '<script>window.correoScriptRan=true</script><style>body{display:none}</style>' +
        '<h2 onclick="alert(1)" style="position:fixed;top:0">Solicitud de permiso</h2>' +
        '<p>Buenos días. Adjunto la documentación para revisión.</p>' +
        '<img src="cid:logo"><img src="https://example.invalid/imagen.png" alt="Imagen remota">' +
        '<iframe src="https://example.invalid/marco"></iframe>',
      inline: { logo: png }
    };
    const cleaned = await page.evaluate(sanitize(input));
    assert.equal(cleaned.missing, 0);
    assert.equal(cleaned.external, 1);
    assert(!/<script|<style|<iframe|onclick=|position:/i.test(cleaned.html));
    assert(cleaned.html.includes(png));
    const missing = await page.evaluate(sanitize({ html: '<img src="cid:inexistente">', inline: {} }));
    assert.equal(missing.missing, 1);
    const paragraphs = Array.from({ length: 90 }, (_, i) => '<p>Registro ' + i + ': Solicitud recibida y documentación adjunta. Revisión completa del permiso.</p>').join('');
    const html = template + '<h1>Prueba de exportación</h1>' +
      '<table class="header"><tr><th>De:</th><td>ana@ejemplo.es</td></tr><tr><th>Para:</th><td>rafa@ejemplo.es</td></tr></table>' +
      '<main class="message">' + cleaned.html + paragraphs + '</main></body></html>';
    await page.setContent(html, { waitUntil: 'load' });
    await page.evaluate(async () => { await document.fonts.ready; await Promise.all([...document.images].map(i => i.decode())); });
    assert.equal(await page.evaluate(() => window.correoScriptRan), undefined);
    assert.equal(requests.length, 0, 'No deben descargarse imágenes externas ni abrirse marcos');
    const basename = path.resolve(__dirname, '../../tmp/CorreoDiario-render-check');
    fs.writeFileSync(basename + '.txt', await page.locator('body').innerText());
    await page.pdf({ path: basename + '.pdf', format: 'A4', printBackground: true, displayHeaderFooter: false });
    console.log('PASS Saneado HTML, imágenes adjuntas, recursos externos bloqueados y PDF de varias páginas.');
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });

// Sign-in page: localized headings, password toggle, and an error state for a wrong password.
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { openPage } from '../cdp.mjs';

const artifacts = join(dirname(fileURLToPath(import.meta.url)), '..', 'artifacts');
const sso = 'http://localhost:5222/sso';
const failures = [];
const expect = (ok, message) => { console.log(`${ok ? 'PASS' : 'FAIL'} ${message}`); if (!ok) failures.push(message); };

const page = await openPage();
try {
  await page.send('Network.clearBrowserCookies');

  await page.goto(`${sso}/Login`);
  expect(await page.eval("getComputedStyle(document.querySelector('.auth-card')).borderRadius !== '0px'"), 'stylesheet loads (card is styled)');
  expect(await page.eval("document.querySelector('#login-title').textContent.trim()") === 'Sign In', 'English heading is localized');
  expect(await page.eval("!document.body.innerText.includes('Login_')"), 'no raw resource keys are shown');

  await page.goto(`${sso}/Login?ui_locales=ne`);
  expect(await page.eval("!document.querySelector('#login-title').textContent.includes('Login_')"), 'Nepali heading resolves (not a raw key)');
  expect(await page.eval("/[\u0900-\u097F]/.test(document.querySelector('#login-title').textContent)"), 'Nepali heading uses Devanagari text');

  await page.goto(`${sso}/Login`);
  await page.eval("document.getElementById('password-toggle').click(); true");
  expect(await page.eval("document.getElementById('Input_Password').type") === 'text', 'password toggle reveals the password');
  expect(await page.eval("document.getElementById('password-toggle').getAttribute('aria-pressed')") === 'true', 'toggle exposes aria-pressed');

  await page.eval(`(() => {
    const set = (el, v) => { el.value = v; el.dispatchEvent(new Event('input', { bubbles: true })); };
    set(document.getElementById('Input_Email'), 'broker1@demo.com');
    set(document.getElementById('Input_Password'), 'WrongPassword1!');
    document.querySelector('#login-form button[type=submit]').click();
    return true;
  })()`);
  await page.waitIdle(2000);
  expect(await page.eval('location.pathname') === '/sso/Login', 'wrong password stays on the sign-in page');
  expect(await page.eval("!!document.querySelector('.auth-alert li')"), 'wrong password shows an error message');
  expect(await page.eval("document.getElementById('Input_Email').value") === 'broker1@demo.com', 'email is kept after a failed sign-in');
  await page.screenshot(join(artifacts, 'login-error.png'));
} finally {
  await page.close();
}

if (failures.length) {
  console.log(`\n${failures.length} check(s) failed`);
  process.exit(1);
}
console.log('\nall checks passed');

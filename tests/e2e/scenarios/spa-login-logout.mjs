// Signs a broker in through the SSO login page, checks the SPA holds a token, then signs out.
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { openPage } from '../cdp.mjs';

const artifacts = join(dirname(fileURLToPath(import.meta.url)), '..', 'artifacts');
const ui = 'http://localhost:4200';
const failures = [];
const expect = (ok, message) => { console.log(`${ok ? 'PASS' : 'FAIL'} ${message}`); if (!ok) failures.push(message); };

const page = await openPage();
try {
  // Start from a clean browser state so the SSO session from an earlier run cannot skip the login page.
  await page.send('Network.clearBrowserCookies');
  await page.goto(ui);
  await page.eval('sessionStorage.clear(); true');
  await page.goto(`${ui}/broker/listings`);
  expect((await page.eval('location.pathname')).startsWith('/sso/Login'), 'protected route redirects to SSO login');
  await page.screenshot(join(artifacts, '1-login-page.png'));

  await page.eval(`(() => {
    const set = (el, v) => { el.value = v; el.dispatchEvent(new Event('input', { bubbles: true })); };
    set(document.querySelector('#Input_Email'), 'broker1@demo.com');
    set(document.querySelector('#Input_Password'), 'Password123!');
    document.querySelector('#login-form button[type=submit]').click();
    return true;
  })()`);
  await page.waitIdle(2500);

  expect(await page.eval('location.href') === `${ui}/broker/listings`, 'sign-in returns to the requested page');
  expect(await page.eval("!!sessionStorage.getItem('hb.access_token')"), 'access token stored in sessionStorage');
  await page.screenshot(join(artifacts, '2-signed-in.png'));

  // Sign out through the app's own control.
  await page.eval(`(() => {
    const btn = [...document.querySelectorAll('button, a')].find(el => /sign out/i.test(el.textContent));
    if (!btn) throw new Error('no Sign out control');
    btn.click();
    return true;
  })()`);
  await page.waitIdle(2500);

  expect((await page.eval('location.origin')) === ui, 'sign-out returns to the SPA origin (post-logout URI accepted)');
  expect(await page.eval("!sessionStorage.getItem('hb.access_token')"), 'access token cleared after sign-out');
  await page.screenshot(join(artifacts, '3-signed-out.png'));
} finally {
  await page.close();
}

if (failures.length) {
  console.log(`\n${failures.length} check(s) failed`);
  process.exit(1);
}
console.log('\nall checks passed');

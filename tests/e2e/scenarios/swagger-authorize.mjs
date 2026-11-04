// Drives the authorization-code + PKCE request Swagger UI sends for its "oauth2" scheme and checks that
// the sign-in returns the code to Swagger's registered redirect page.
// Note: Swagger UI opens this request in a popup; headless Chrome does not follow that popup, so the
// scenario starts from the same URL the popup would load.
import { createHash, randomBytes } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { openPage } from '../cdp.mjs';

const artifacts = join(dirname(fileURLToPath(import.meta.url)), '..', 'artifacts');
const redirect = 'http://localhost:5049/api/swagger/oauth2-redirect.html';
const failures = [];
const expect = (ok, message) => { console.log(`${ok ? 'PASS' : 'FAIL'} ${message}`); if (!ok) failures.push(message); };

const verifier = randomBytes(32).toString('base64url');
const challenge = createHash('sha256').update(verifier).digest('base64url');
const authorize = 'http://localhost:5222/sso/connect/authorize?' + new URLSearchParams({
  client_id: 'housebroker-swagger',
  response_type: 'code',
  redirect_uri: redirect,
  scope: 'openid profile listings.read',
  code_challenge: challenge,
  code_challenge_method: 'S256',
  state: 'swagger-e2e',
});

const page = await openPage();
try {
  await page.send('Network.clearBrowserCookies');
  await page.goto(authorize);
  const loginUrl = await page.eval('location.href');
  expect(loginUrl.startsWith('http://localhost:5222/sso/Login') || loginUrl.startsWith('http://localhost:4200/sso/Login'), 'authorize redirects to the SSO login page');
  await page.screenshot(join(artifacts, 'swagger-login.png'));

  await page.eval(`(() => {
    const set = (el, v) => { el.value = v; el.dispatchEvent(new Event('input', { bubbles: true })); };
    set(document.querySelector('#Input_Email'), 'admin@housebroker.com');
    set(document.querySelector('#Input_Password'), 'AdminPassword123!');
    document.querySelector('#login-form button[type=submit]').click();
    return true;
  })()`);
  await page.waitIdle(2500);

  const back = await page.eval('location.href');
  expect(back.startsWith(redirect) && back.includes('code=') && back.includes('state=swagger-e2e'), 'after sign-in the code returns to the Swagger redirect page');
} finally {
  await page.close();
}

if (failures.length) {
  console.log(`\n${failures.length} check(s) failed`);
  process.exit(1);
}
console.log('\nall checks passed');

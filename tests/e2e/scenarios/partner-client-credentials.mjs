// Partner (client_credentials) token: can read, cannot write, cannot reach broker-only routes.
const sso = 'http://localhost:5222/sso';
const api = 'http://localhost:5049/api/v1';
const failures = [];
const expect = (ok, message) => { console.log(`${ok ? 'PASS' : 'FAIL'} ${message}`); if (!ok) failures.push(message); };

const secret = process.env.DEMO_PARTNER_SECRET ?? 'partner_secret_123';
const tokenRes = await fetch(`${sso}/connect/token`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
  body: new URLSearchParams({ grant_type: 'client_credentials', client_id: 'demo-partner', client_secret: secret, scope: 'listings.read contacts.read' }),
});
const token = (await tokenRes.json()).access_token;
expect(tokenRes.status === 200 && !!token, 'partner obtains a client_credentials token');

const claims = JSON.parse(Buffer.from(token.split('.')[1], 'base64url').toString());
expect(claims.aud === 'housebroker-api', 'partner token audience is the API');
expect(JSON.stringify(claims.scp) === JSON.stringify(['listings.read', 'contacts.read']), 'partner token carries requested scopes as scp');

const auth = { Authorization: `Bearer ${token}` };
expect((await fetch(`${api}/listings`, { headers: auth })).status === 200, 'partner can read listings');
expect((await fetch(`${api}/listings`, { method: 'POST', headers: { ...auth, 'Content-Type': 'application/json' }, body: '{}' })).status === 403, 'partner write is refused');
expect((await fetch(`${api}/listings/mine`, { headers: auth })).status === 403, 'partner cannot reach broker-only routes');
expect((await fetch(`${api}/admin/commission-rates`, { headers: auth })).status === 403, 'partner cannot reach admin routes');

if (failures.length) {
  console.log(`\n${failures.length} check(s) failed`);
  process.exit(1);
}
console.log('\nall checks passed');

import { expect, test } from '@playwright/test';
import { getAccessToken } from '../support/auth';

const API = process.env.E2E_API_URL ?? 'http://localhost:5049';

test.describe('@admin commission rates', () => {
  test('admin endpoint authorization and invalid tier validation', async ({ browser }) => {
    const adminToken = await getAccessToken(
      browser,
      process.env.E2E_ADMIN_EMAIL ?? 'admin@housebroker.com',
      process.env.E2E_ADMIN_PASSWORD ?? 'AdminPassword123!',
    );
    const brokerToken = await getAccessToken(
      browser,
      process.env.E2E_BROKER_EMAIL ?? 'broker1@demo.com',
      process.env.E2E_DEMO_PASSWORD ?? 'Password123!',
    );

    const adminHeaders = { Authorization: `Bearer ${adminToken}` };
    const brokerHeaders = { Authorization: `Bearer ${brokerToken}` };

    const anonymousRates = await fetch(`${API}/api/v1/admin/commission-rates`);
    expect(anonymousRates.status).toBe(401);

    const brokerRates = await fetch(`${API}/api/v1/admin/commission-rates`, {
      headers: brokerHeaders,
    });
    expect(brokerRates.status).toBe(403);

    const adminRates = await fetch(`${API}/api/v1/admin/commission-rates`, {
      headers: adminHeaders,
    });
    expect(adminRates.status).toBe(200);
    const rates = await adminRates.json();
    expect(Array.isArray(rates)).toBeTruthy();

    const invalidCreate = await fetch(`${API}/api/v1/admin/commission-rates`, {
      method: 'POST',
      headers: {
        ...adminHeaders,
        'Content-Type': 'application/json',
        'Idempotency-Key': crypto.randomUUID(),
      },
      body: JSON.stringify({
        minPrice: 100,
        minInclusive: true,
        maxPrice: 50,
        maxInclusive: true,
        ratePercent: 2,
        isActive: true,
      }),
    });
    expect(invalidCreate.status).toBeGreaterThanOrEqual(400);
    expect(invalidCreate.status).toBeLessThan(500);
  });
});
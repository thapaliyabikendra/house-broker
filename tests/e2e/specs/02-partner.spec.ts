import { expect, test } from '@playwright/test';

const API = process.env.E2E_API_URL ?? 'http://localhost:5049';
const AUTH = process.env.E2E_AUTH_URL ?? 'http://localhost:5222';

test.describe('@partner partner API', () => {
  test('client_credentials token can read listings', async ({ request }) => {
    const tokenResponse = await request.post(`${AUTH}/sso/connect/token`, {
      form: {
        grant_type: 'client_credentials',
        client_id: 'demo-partner',
        client_secret: process.env.E2E_PARTNER_SECRET ?? 'partner_secret_123',
        scope: 'listings.read',
      },
    });
    expect(tokenResponse.ok()).toBeTruthy();
    const tokenBody = await tokenResponse.json();
    expect(tokenBody.access_token).toBeTruthy();

    const listingsResponse = await request.get(`${API}/api/v1/listings`, {
      headers: { Authorization: `Bearer ${tokenBody.access_token}` },
    });
    expect(listingsResponse.ok()).toBeTruthy();
    const listingsBody = await listingsResponse.json();
    expect(Array.isArray(listingsBody.items)).toBeTruthy();
    for (const listing of listingsBody.items) {
      expect(listing).not.toHaveProperty('commissionAmount');
      expect(listing).not.toHaveProperty('commissionRatePercent');
    }
  });

  test('partner token cannot write listings', async ({ request }) => {
    const tokenResponse = await request.post(`${AUTH}/sso/connect/token`, {
      form: {
        grant_type: 'client_credentials',
        client_id: 'demo-partner',
        client_secret: process.env.E2E_PARTNER_SECRET ?? 'partner_secret_123',
        scope: 'listings.read',
      },
    });
    const tokenBody = await tokenResponse.json();

    const response = await request.post(`${API}/api/v1/listings`, {
      headers: { Authorization: `Bearer ${tokenBody.access_token}` },
      data: {
        title: `Partner write probe ${Date.now()}`,
        propertyTypeId: '00000000-0000-0000-0000-000000000000',
        price: 100000,
        province: 'Bagmati',
        city: 'Kathmandu',
        locality: 'Thamel',
        postalCode: '44600',
        bedrooms: 1,
        bathrooms: 1,
        areaSqFt: 500,
      },
    });
    expect(response.status()).toBeGreaterThanOrEqual(400);
    expect(response.status()).toBeLessThan(500);
  });
});
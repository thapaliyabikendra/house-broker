import { expect, test } from '@playwright/test';
import { getAccessToken } from '../support/auth';

const API = process.env.E2E_API_URL ?? 'http://localhost:5049';

test.describe('@localization contact and time', () => {
  test('public listing detail exposes broker contact and UTC timestamp', async ({ browser }) => {
    const token = await getAccessToken(
      browser,
      process.env.E2E_BROKER_EMAIL ?? 'broker1@demo.com',
      process.env.E2E_DEMO_PASSWORD ?? 'Password123!',
    );
    const authHeaders = { Authorization: `Bearer ${token}` };

    const propertyTypes = await (await fetch(`${API}/api/v1/lookups/property-types`)).json();
    const createResponse = await fetch(`${API}/api/v1/listings`, {
      method: 'POST',
      headers: {
        ...authHeaders,
        'Content-Type': 'application/json',
        'Idempotency-Key': crypto.randomUUID(),
      },
      body: JSON.stringify({
        title: `Localization listing ${Date.now()}`,
        description: 'Localization test',
        propertyTypeId: propertyTypes[0].id,
        price: 4500000,
        province: 'Bagmati',
        city: 'Kathmandu',
        locality: 'Thamel',
        postalCode: '44600',
        bedrooms: 1,
        bathrooms: 1,
        areaSqFt: 500,
        featureIds: [],
      }),
    });
    expect(createResponse.status).toBe(201);
    const created = await createResponse.json();

    const publishResponse = await fetch(
      `${API}/api/v1/listings/${created.id}/publish`,
      { method: 'POST', headers: authHeaders },
    );
    expect(publishResponse.ok).toBeTruthy();
    const published = await publishResponse.json();

    const detailResponse = await fetch(`${API}/api/v1/listings/${published.id}`);
    expect(detailResponse.status).toBe(200);
    const detail = await detailResponse.json();

    expect(detail.brokerContact).toBeTruthy();
    expect(detail.brokerContact.fullName).toBeTruthy();
    expect(detail.createdAt).toMatch(/Z$/);

    await fetch(`${API}/api/v1/listings/${published.id}`, {
      method: 'DELETE',
      headers: authHeaders,
    });
  });

  test('meta supports both configured cultures', async () => {
    for (const culture of ['en', 'ne']) {
      const response = await fetch(`${API}/api/v1/meta`, {
        headers: { 'Accept-Language': culture },
      });
      expect(response.status).toBe(200);
      expect(response.headers.get('content-language')).toBe(culture);
      expect(response.headers.get('vary')?.toLowerCase()).toContain('accept-language');
    }
  });
});
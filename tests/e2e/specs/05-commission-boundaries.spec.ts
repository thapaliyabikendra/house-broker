import { expect, test } from '@playwright/test';
import { getAccessToken } from '../support/auth';

const API = process.env.E2E_API_URL ?? 'http://localhost:5049';

test.describe('@commission boundary values', () => {
  const cases = [
    { price: 4999999.50, rate: 2, amount: 99999.99 },
    { price: 5000000, rate: 1.75, amount: 87500 },
    { price: 10000000, rate: 1.75, amount: 175000 },
    { price: 10000000.01, rate: 1.5, amount: 150000 },
  ];

  test('commission tier boundaries resolve correctly', async ({ browser }) => {
    const token = await getAccessToken(
      browser,
      process.env.E2E_BROKER_EMAIL ?? 'broker1@demo.com',
      process.env.E2E_DEMO_PASSWORD ?? 'Password123!',
    );
    const headers = { Authorization: `Bearer ${token}` };

    const propertyTypes = await (await fetch(`${API}/api/v1/lookups/property-types`)).json();
    const propertyTypeId = propertyTypes[0].id;

    for (const scenario of cases) {
      const response = await fetch(`${API}/api/v1/listings`, {
        method: 'POST',
        headers: {
          ...headers,
          'Content-Type': 'application/json',
          'Idempotency-Key': crypto.randomUUID(),
        },
        body: JSON.stringify({
          title: `Boundary ${scenario.price} ${Date.now()}`,
          description: 'Boundary test',
          propertyTypeId,
          price: scenario.price,
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
      expect(response.status).toBe(201);
      const listing = await response.json();
      expect(listing.commissionRatePercent).toBe(scenario.rate);
      expect(listing.commissionAmount).toBe(scenario.amount);

      await fetch(`${API}/api/v1/listings/${listing.id}`, {
        method: 'DELETE',
        headers,
      });
    }
  });
});
import { expect, test } from '@playwright/test';

const API = process.env.E2E_API_URL ?? 'http://localhost:5049';

test.describe('@smoke smoke', () => {
  test('meta endpoint returns supported cultures and NPR', async ({ request }) => {
    const response = await request.get(`${API}/api/v1/meta`);
    expect(response.ok()).toBeTruthy();
    const body = await response.json();
    expect(body.currencyCode).toBe('NPR');
    expect(body.defaultCulture).toBe('en');
    expect(body.supportedCultures).toEqual(['en', 'ne']);
  });

  test('anonymous listings response has no commission fields', async ({ request }) => {
    const response = await request.get(`${API}/api/v1/listings?pageNumber=1&pageSize=20`);
    expect(response.ok()).toBeTruthy();
    const body = await response.json();
    const listing = body.items?.[0];
    if (listing) {
      expect(listing).not.toHaveProperty('commissionAmount');
      expect(listing).not.toHaveProperty('commissionRatePercent');
      expect(listing).not.toHaveProperty('commissionRateId');
    }
  });
});
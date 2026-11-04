import { expect, test } from '@playwright/test';
import { getAccessToken } from '../support/auth';

const API = process.env.E2E_API_URL ?? 'http://localhost:5049';

test.describe('@broker CRUD and lifecycle', () => {
  test('owner can create, publish, update, sell, archive, and delete a listing', async ({
    browser,
  }) => {
    const brokerToken = await getAccessToken(
      browser,
      process.env.E2E_BROKER_EMAIL ?? 'broker1@demo.com',
      process.env.E2E_DEMO_PASSWORD ?? 'Password123!',
    );
    const otherBrokerToken = await getAccessToken(
      browser,
      process.env.E2E_BROKER_TWO_EMAIL ?? 'broker2@demo.com',
      process.env.E2E_DEMO_PASSWORD ?? 'Password123!',
    );

    const authHeaders = { Authorization: `Bearer ${brokerToken}` };
    const otherHeaders = { Authorization: `Bearer ${otherBrokerToken}` };

    const propertyTypesResponse = await fetch(`${API}/api/v1/lookups/property-types`);
    const propertyTypes = await propertyTypesResponse.json();
    const propertyTypeId = propertyTypes[0].id;

    const title = `E2E listing ${Date.now()}`;
    const createRes = await fetch(`${API}/api/v1/listings`, {
      method: 'POST',
      headers: {
        ...authHeaders,
        'Content-Type': 'application/json',
        'Idempotency-Key': crypto.randomUUID(),
      },
      body: JSON.stringify({
        title,
        description: 'Created by Playwright',
        propertyTypeId,
        price: 4500000,
        province: 'Bagmati',
        city: 'Kathmandu',
        locality: 'Thamel',
        postalCode: '44600',
        bedrooms: 2,
        bathrooms: 2,
        areaSqFt: 1100,
        featureIds: [],
      }),
    });
    expect(createRes.status).toBe(201);
    const created = await createRes.json();
    const listingId = created.id;
    expect(created.status).toBe('Draft');
    expect(created.commissionAmount).toBeGreaterThan(0);

    try {
      const forbiddenRes = await fetch(`${API}/api/v1/listings/${listingId}/publish`, {
        method: 'POST',
        headers: otherHeaders,
      });
      expect(forbiddenRes.status).toBe(403);

      const publishRes = await fetch(`${API}/api/v1/listings/${listingId}/publish`, {
        method: 'POST',
        headers: authHeaders,
      });
      expect(publishRes.ok).toBeTruthy();
      const published = await publishRes.json();
      expect(published.status).toBe('Active');

      const currentVersion = published.version;

      const missingRes = await fetch(`${API}/api/v1/listings/${listingId}`, {
        method: 'PUT',
        headers: { ...authHeaders, 'Content-Type': 'application/json' },
        body: JSON.stringify({
          title,
          description: 'Updated',
          propertyTypeId,
          price: 4600000,
          province: 'Bagmati',
          city: 'Kathmandu',
          locality: 'Thamel',
          postalCode: '44600',
          bedrooms: 2,
          bathrooms: 2,
          areaSqFt: 1100,
          featureIds: [],
          version: currentVersion,
        }),
      });
      expect(missingRes.status).toBe(428);

      const staleRes = await fetch(`${API}/api/v1/listings/${listingId}`, {
        method: 'PUT',
        headers: {
          ...authHeaders,
          'Content-Type': 'application/json',
          'If-Match': '"stale-version"',
        },
        body: JSON.stringify({
          title,
          description: 'Updated',
          propertyTypeId,
          price: 4600000,
          province: 'Bagmati',
          city: 'Kathmandu',
          locality: 'Thamel',
          postalCode: '44600',
          bedrooms: 2,
          bathrooms: 2,
          areaSqFt: 1100,
          featureIds: [],
          version: currentVersion,
        }),
      });
      expect(staleRes.status).toBe(412);

      const updateRes = await fetch(`${API}/api/v1/listings/${listingId}`, {
        method: 'PUT',
        headers: {
          ...authHeaders,
          'Content-Type': 'application/json',
          'If-Match': `"${currentVersion}"`,
        },
        body: JSON.stringify({
          title,
          description: 'Updated',
          propertyTypeId,
          price: 4600000,
          province: 'Bagmati',
          city: 'Kathmandu',
          locality: 'Thamel',
          postalCode: '44600',
          bedrooms: 2,
          bathrooms: 2,
          areaSqFt: 1100,
          featureIds: [],
          version: currentVersion,
        }),
      });
      expect(updateRes.ok).toBeTruthy();
      const updated = await updateRes.json();
      expect(updated.version).not.toBe(currentVersion);
      expect(updated.commissionAmount).toBeGreaterThan(created.commissionAmount);

      const soldRes = await fetch(`${API}/api/v1/listings/${listingId}/sold`, {
        method: 'POST',
        headers: authHeaders,
      });
      expect(soldRes.ok).toBeTruthy();
      const sold = await soldRes.json();
      expect(sold.status).toBe('Sold');

      const archiveRes = await fetch(`${API}/api/v1/listings/${listingId}/archive`, {
        method: 'POST',
        headers: authHeaders,
      });
      expect(archiveRes.ok).toBeTruthy();
      const archived = await archiveRes.json();
      expect(archived.status).toBe('Archived');

      const deleteRes = await fetch(`${API}/api/v1/listings/${listingId}`, {
        method: 'DELETE',
        headers: authHeaders,
      });
      expect(deleteRes.status).toBe(204);

      const detailAfterDelete = await fetch(`${API}/api/v1/listings/${listingId}`);
      expect(detailAfterDelete.status).toBe(404);
    } finally {
      await fetch(`${API}/api/v1/listings/${listingId}`, {
        method: 'DELETE',
        headers: authHeaders,
      }).catch(() => undefined);
    }
  });
});
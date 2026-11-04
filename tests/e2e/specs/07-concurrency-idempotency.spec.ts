import { expect, test } from '@playwright/test';
import { getAccessToken } from '../support/auth';

const API = process.env.E2E_API_URL ?? 'http://localhost:5049';

test.describe('@concurrency idempotency', () => {
  test('parallel updates conflict and idempotency replays/conflicts', async ({ browser }) => {
    const token = await getAccessToken(
      browser,
      process.env.E2E_BROKER_EMAIL ?? 'broker1@demo.com',
      process.env.E2E_DEMO_PASSWORD ?? 'Password123!',
    );
    const authHeaders = { Authorization: `Bearer ${token}` };

    const propertyTypes = await (await fetch(`${API}/api/v1/lookups/property-types`)).json();
    const propertyTypeId = propertyTypes[0].id;

    const title = `Idempotency probe ${Date.now()}`;
    const body = (price: number, valueTitle: string) =>
      JSON.stringify({
        title: valueTitle,
        description: 'Created by Playwright',
        propertyTypeId,
        price,
        province: 'Bagmati',
        city: 'Kathmandu',
        locality: 'Thamel',
        postalCode: '44600',
        bedrooms: 2,
        bathrooms: 2,
        areaSqFt: 1100,
        featureIds: [],
      });

    const key = crypto.randomUUID();

    const firstCreate = await fetch(`${API}/api/v1/listings`, {
      method: 'POST',
      headers: {
        ...authHeaders,
        'Content-Type': 'application/json',
        'Idempotency-Key': key,
      },
      body: body(4500000, title),
    });
    expect(firstCreate.status).toBe(201);
    const firstListing = await firstCreate.json();

    const replayCreate = await fetch(`${API}/api/v1/listings`, {
      method: 'POST',
      headers: {
        ...authHeaders,
        'Content-Type': 'application/json',
        'Idempotency-Key': key,
      },
      body: body(4500000, title),
    });
    expect([200, 201]).toContain(replayCreate.status);
    const replayText = await replayCreate.text();
    const replayListing = JSON.parse(replayText);
    expect(replayListing?.id).toBe(firstListing.id);

    const conflictCreate = await fetch(`${API}/api/v1/listings`, {
      method: 'POST',
      headers: {
        ...authHeaders,
        'Content-Type': 'application/json',
        'Idempotency-Key': key,
      },
      body: body(4500000, `${title} changed`),
    });
    expect(conflictCreate.status).toBe(422);

    const publishResponse = await fetch(`${API}/api/v1/listings/${firstListing.id}/publish`, {
      method: 'POST',
      headers: authHeaders,
    });
    expect(publishResponse.ok).toBeTruthy();
    const published = await publishResponse.json();

    const parallelUpdates = await Promise.all([
      fetch(`${API}/api/v1/listings/${firstListing.id}`, {
        method: 'PUT',
        headers: {
          ...authHeaders,
          'Content-Type': 'application/json',
          'If-Match': `"${published.version}"`,
        },
        body: body(4600000, title),
      }),
      fetch(`${API}/api/v1/listings/${firstListing.id}`, {
        method: 'PUT',
        headers: {
          ...authHeaders,
          'Content-Type': 'application/json',
          'If-Match': `"${published.version}"`,
        },
        body: body(4700000, title),
      }),
    ]);

    const statuses = parallelUpdates.map((response) => response.status).sort();
    expect(statuses).toEqual([200, 412]);

    await fetch(`${API}/api/v1/listings/${firstListing.id}`, {
      method: 'DELETE',
      headers: authHeaders,
    });
  });
});
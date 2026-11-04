import { expect, test } from '@playwright/test';
import { getAccessToken } from '../support/auth';

const API = process.env.E2E_API_URL ?? 'http://localhost:5049';

test.describe('@images upload', () => {
  test('broker can upload an image to an owned listing', async ({ browser }) => {
    const token = await getAccessToken(
      browser,
      process.env.E2E_BROKER_EMAIL ?? 'broker1@demo.com',
      process.env.E2E_DEMO_PASSWORD ?? 'Password123!',
    );
    const headers = { Authorization: `Bearer ${token}` };

    const propertyTypes = await (await fetch(`${API}/api/v1/lookups/property-types`)).json();
    const propertyTypeId = propertyTypes[0].id;

    const createResponse = await fetch(`${API}/api/v1/listings`, {
      method: 'POST',
      headers: {
        ...headers,
        'Content-Type': 'application/json',
        'Idempotency-Key': crypto.randomUUID(),
      },
      body: JSON.stringify({
        title: `Image listing ${Date.now()}`,
        description: 'Image test',
        propertyTypeId,
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
    const listing = await createResponse.json();

    const png = Buffer.from(
      'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAoADQABD1v9RgAAAABJRU5ErkJggg==',
      'base64',
    );

    const form = new FormData();
    form.append('file', new Blob([png], { type: 'image/png' }), 'pixel.png');

    const uploadResponse = await fetch(`${API}/api/v1/listings/${listing.id}/images`, {
      method: 'POST',
      headers,
      body: form,
    });

    expect(uploadResponse.status).toBe(200);
    const uploaded = await uploadResponse.json();
    expect(uploaded.id).toBeTruthy();

    await fetch(`${API}/api/v1/listings/${listing.id}`, {
      method: 'DELETE',
      headers,
    });
  });
});
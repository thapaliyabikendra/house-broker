import { expect, test } from '@playwright/test';

const UI = process.env.E2E_UI_URL ?? 'http://localhost:4200';
const BROKER_EMAIL = process.env.E2E_BROKER_EMAIL ?? 'broker1@demo.com';
const DEMO_PASSWORD = process.env.E2E_DEMO_PASSWORD ?? 'Password123!';

test.describe('@ui browser auth', () => {
  test('broker can sign in and search listings', async ({ page }) => {
    await page.goto(UI);
    await page.getByRole('button', { name: 'Sign in' }).first().click();

    await page.getByLabel('Email Address').fill(BROKER_EMAIL);
    await page.getByLabel('Password').fill(DEMO_PASSWORD);
    await page.getByRole('button', { name: 'Sign In' }).click();

    await expect(page).toHaveURL(/\/$/);
    await expect(page.getByText(BROKER_EMAIL, { exact: true })).toBeVisible();

    await page.getByRole('link', { name: 'Search listings' }).first().click();
    await expect(page).toHaveURL(/\/search$/);
    await expect(page.getByRole('link', { name: /Prime Commercial Space/i })).toBeVisible();
  });
});
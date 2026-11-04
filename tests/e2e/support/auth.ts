import type { Browser } from '@playwright/test';

const UI = process.env.E2E_UI_URL ?? 'http://localhost:4200';

export async function getAccessToken(
  browser: Browser,
  email: string,
  password: string,
): Promise<string> {
  const page = await browser.newPage();
  await page.goto(UI);
  await page.getByRole('button', { name: 'Sign in' }).first().click();
  await page.getByLabel('Email Address').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign In' }).click();
  await page.waitForURL(/\/$/);

  const token = await page.evaluate(() =>
    sessionStorage.getItem('hb.access_token'),
  );
  await page.close();

  if (!token) {
    throw new Error(`No access token for ${email}`);
  }

  return token;
}
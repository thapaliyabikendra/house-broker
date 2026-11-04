import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './specs',
  timeout: 30_000,
  retries: 0,
  use: {
    extraHTTPHeaders: {
      Accept: 'application/json',
    },
  },
});
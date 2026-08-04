import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: true,
  retries: process.env['CI'] ? 2 : 0,
  reporter: 'line',
  use: {
    baseURL: 'http://127.0.0.1:4300',
    trace: 'on-first-retry',
  },
  webServer: {
    command: 'npm run start -- --host 127.0.0.1 --port 4300',
    url: 'http://127.0.0.1:4300',
    reuseExistingServer: !process.env['CI'],
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});

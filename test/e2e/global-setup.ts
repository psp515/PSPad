import { chromium } from '@playwright/test';
import { env } from './env';
import { createAccount, deleteAccount } from './fixtures/keycloak';
import { signIn } from './helpers/sign-in';

async function waitFor(name: string, url: string) {
  const until = Date.now() + 180_000;
  while (Date.now() < until) {
    try {
      const response = await fetch(url);
      if (response.ok) {
        return;
      }
    } catch {
    }
    await new Promise(resolve => setTimeout(resolve, 2_000));
  }
  throw new Error(`${name} at ${url} never became ready`);
}

async function warmUp() {
  await fetch(`${env.keycloakUrl}/realms/${env.realm}/protocol/openid-connect/certs`);
  const account = await createAccount();
  const browser = await chromium.launch();
  try {
    let lastError: unknown;
    for (let attempt = 0; attempt < 3; attempt++) {
      const context = await browser.newContext({ baseURL: env.appUrl });
      try {
        await signIn(await context.newPage(), account);
        return;
      } catch (error) {
        lastError = error;
      } finally {
        await context.close();
      }
    }
    throw lastError;
  } finally {
    await browser.close();
    await deleteAccount(account);
  }
}

export default async function globalSetup() {
  await Promise.all([
    waitFor('API', `${env.apiUrl}/health`),
    waitFor('app', `${env.appUrl}/`),
    waitFor('Keycloak', `${env.keycloakUrl}/realms/${env.realm}`),
  ]);
  await warmUp();
}

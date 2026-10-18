import { env } from './env';

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

export default async function globalSetup() {
  await Promise.all([
    waitFor('API', `${env.apiUrl}/health`),
    waitFor('app', `${env.appUrl}/`),
    waitFor('Keycloak', `${env.keycloakUrl}/realms/${env.realm}`),
  ]);
}

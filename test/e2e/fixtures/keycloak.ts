import { randomBytes } from 'node:crypto';
import { env } from '../env';

export type Account = {
  id: string;
  username: string;
  password: string;
  email: string;
  displayName: string;
  suffix: string;
};

const password = 'e2e-password';

async function adminToken(): Promise<string> {
  const response = await fetch(`${env.keycloakUrl}/realms/master/protocol/openid-connect/token`, {
    method: 'POST',
    headers: { 'content-type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'password',
      client_id: 'admin-cli',
      username: env.adminUser,
      password: env.adminPassword,
    }),
  });
  if (!response.ok) {
    throw new Error(`Keycloak admin token: ${response.status} ${await response.text()}`);
  }
  return ((await response.json()) as { access_token: string }).access_token;
}

export async function createAccount(): Promise<Account> {
  const suffix = randomBytes(4).toString('hex');
  const username = `e2e-${suffix}`;
  const email = `${username}@e2e.local`;
  const token = await adminToken();
  const response = await fetch(`${env.keycloakUrl}/admin/realms/${env.realm}/users`, {
    method: 'POST',
    headers: { authorization: `Bearer ${token}`, 'content-type': 'application/json' },
    body: JSON.stringify({
      username,
      email,
      firstName: 'E2E',
      lastName: suffix,
      enabled: true,
      emailVerified: true,
      credentials: [{ type: 'password', value: password, temporary: false }],
    }),
  });
  if (response.status !== 201) {
    throw new Error(`Keycloak create user: ${response.status} ${await response.text()}`);
  }
  const id = response.headers.get('location')!.split('/').pop()!;
  return { id, username, password, email, displayName: `E2E ${suffix}`, suffix };
}

export async function deleteAccount(account: Account): Promise<void> {
  const token = await adminToken();
  await fetch(`${env.keycloakUrl}/admin/realms/${env.realm}/users/${account.id}`, {
    method: 'DELETE',
    headers: { authorization: `Bearer ${token}` },
  });
}

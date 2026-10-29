import { APIRequestContext, Page, request } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { env } from '../env';
import { syncButton } from './sync';

export type SyncDocuments = Record<string, Array<Record<string, unknown>>>;

export type SyncResponse = {
  marker: number;
  documents: SyncDocuments;
  memberListIds: string[] | null;
};

export type CommandResponse = {
  commandId: string;
  accepted: boolean;
  rejection: string | null;
};

export async function bearerToken(page: Page): Promise<string> {
  const sync = page.waitForRequest(req => req.url().includes('/api/sync'));
  await syncButton(page).click();
  const header = (await sync).headers()['authorization'] ?? '';
  if (!header.startsWith('Bearer ')) {
    throw new Error('the sync request carried no bearer token');
  }
  return header.slice('Bearer '.length);
}

export async function apiFor(token: string): Promise<APIRequestContext> {
  return request.newContext({
    baseURL: env.apiUrl,
    extraHTTPHeaders: { authorization: `Bearer ${token}` },
  });
}

export async function me(api: APIRequestContext): Promise<{ userId: string; displayName: string }> {
  const response = await api.get('/api/me');
  if (!response.ok()) {
    throw new Error(`/api/me: ${response.status()}`);
  }
  return response.json();
}

export async function fullSync(api: APIRequestContext): Promise<SyncResponse> {
  const response = await api.get('/api/sync?since=0');
  if (!response.ok()) {
    throw new Error(`/api/sync: ${response.status()}`);
  }
  return response.json();
}

export async function sendCommand(
  api: APIRequestContext, type: string, payload: Record<string, unknown>,
): Promise<CommandResponse> {
  const response = await api.post('/api/commands', {
    data: [{ type, payload: { commandId: randomUUID(), ...payload } }],
  });
  if (!response.ok()) {
    throw new Error(`/api/commands: ${response.status()} ${await response.text()}`);
  }
  const [first] = (await response.json()) as CommandResponse[];
  return first;
}

export type Device = {
  userId: string;
  send: (type: string, payload: Record<string, unknown>) => Promise<void>;
  documents: (collection: string) => Promise<Array<Record<string, unknown>>>;
  dispose: () => Promise<void>;
};

export async function anotherDevice(page: Page): Promise<Device> {
  const api = await apiFor(await bearerToken(page));
  const { userId } = await me(api);
  return {
    userId,
    send: async (type, payload) => {
      const result = await sendCommand(api, type, { userId, ...payload });
      if (!result.accepted) {
        throw new Error(`${type} rejected: ${result.rejection}`);
      }
    },
    documents: async collection => (await fullSync(api)).documents[collection] ?? [],
    dispose: () => api.dispose(),
  };
}

export async function idOf(device: Device, collection: string, name: string): Promise<string> {
  const found = (await device.documents(collection)).find(doc => (doc['name'] ?? doc['Name']) === name);
  if (!found) {
    throw new Error(`no ${collection} document named ${name}`);
  }
  return String(found['id'] ?? found['Id']);
}

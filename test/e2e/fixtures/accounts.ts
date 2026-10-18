import { test as base } from '@playwright/test';
import { Account, createAccount, deleteAccount } from './keycloak';

type Accounts = {
  account: Account;
  otherAccount: Account;
};

async function provide(use: (account: Account) => Promise<void>) {
  const account = await createAccount();
  await use(account);
  await deleteAccount(account);
}

export const test = base.extend<Accounts>({
  account: async ({}, use) => provide(use),
  otherAccount: async ({}, use) => provide(use),
});

export { expect } from '@playwright/test';

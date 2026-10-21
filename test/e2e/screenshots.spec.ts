import { expect, Page } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { test } from './fixtures/accounts';
import { openArea } from './helpers/areas';
import { addEntry, createBudget } from './helpers/budgets';
import { addDays, utcToday } from './helpers/dates';
import { capture, goToInbox } from './helpers/inbox';
import { createList, openList } from './helpers/lists';
import { goTo } from './helpers/nav';
import { signIn } from './helpers/sign-in';
import { addStep as addStepRaw, addTask, closeTask, openTask as openTaskRaw } from './helpers/tasks';
import { goToMyDay, setDueDate, setRepeating, setTaskTime } from './helpers/today';

const out = fileURLToPath(new URL('../../docs/public/screens/', import.meta.url));
const phone = { width: 390, height: 844 };
const desktop = { width: 1280, height: 800 };

async function shot(page: Page, name: string, theme: 'light' | 'dark', size: { width: number; height: number }) {
  await page.setViewportSize(size);
  await page.waitForTimeout(600);
  const suffix = size === phone ? '-phone' : '';
  await page.screenshot({ path: path.join(out, `${name}${suffix}-${theme}.png`) });
}

async function openTask(page: Page, name: string) {
  await openTaskRaw(page, name);
  await page.waitForTimeout(800);
}

async function addStep(page: Page, name: string) {
  await expect(async () => {
    await addStepRaw(page, name);
  }).toPass({ timeout: 40_000 });
}

async function setTheme(page: Page, theme: 'Light' | 'Dark') {
  await goTo(page, 'Settings');
  await page.getByRole('combobox', { name: 'Theme' }).click();
  await page.locator('.mud-popover-open').getByText(theme, { exact: true }).click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', theme.toLowerCase());
}

async function seed(page: Page) {
  await openArea(page, 'Home');
  await createList(page, 'Errands');
  await createList(page, 'House');
  await openArea(page, 'Work');
  await createList(page, 'Release 2.4');
  await createList(page, 'Reading');

  await openArea(page, 'Home');
  await openList(page, 'Errands');
  await addTask(page, 'Renew the car insurance');
  await openTask(page, 'Renew the car insurance');
  await setDueDate(page, addDays(utcToday(), -1));
  await closeTask(page);
  await addTask(page, 'Pick up the parcel');
  await openTask(page, 'Pick up the parcel');
  await setDueDate(page, 'Today');
  await setTaskTime(page, '17:30', '18:00');
  await closeTask(page);
  await addTask(page, 'Water the plants');
  await openTask(page, 'Water the plants');
  await setRepeating(page);
  await closeTask(page);

  await openArea(page, 'Work');
  await openList(page, 'Release 2.4');
  await addTask(page, 'Write the release notes');
  await openTask(page, 'Write the release notes');
  await addStep(page, 'Collect merged pull requests');
  await addStep(page, 'Draft the summary');
  await setDueDate(page, 'Today');
  await page.waitForTimeout(1000);
  await setTaskTime(page, '09:00', '10:30');
  await closeTask(page);
  await addTask(page, 'Review the budget proposal');
  await openTask(page, 'Review the budget proposal');
  await setDueDate(page, 'Today');
  await closeTask(page);
  await addTask(page, 'Plan the next sprint');
  await openTask(page, 'Plan the next sprint');
  await setDueDate(page, 'Tomorrow');
  await closeTask(page);
  await openArea(page, 'Work');
  await openList(page, 'Reading');
  await addTask(page, 'Getting Things Done, chapter 4');
  await addTask(page, 'Article on offline-first sync');

  await goToInbox(page);
  await capture(page, 'Idea: a weekly review checklist');
  await capture(page, 'Book the dentist');
  await capture(page, 'Ask about the garden fence quote');

  await createBudget(page, 'Household');
  await addEntry(page, 'Income', { name: 'Salary', category: 'Salary', amount: '8400' });
  await addEntry(page, 'Expense', { name: 'Rent', category: 'Housing', amount: '2600' });
  await addEntry(page, 'Expense', { name: 'Groceries', category: 'Food', amount: '640' });
  await addEntry(page, 'Expense', { name: 'Electricity', category: 'Utilities', amount: '210' });
  await addEntry(page, 'Expense', { name: 'Trains', category: 'Transport', amount: '180' });
  await addEntry(page, 'Expense', { name: 'Cinema', category: 'Leisure', amount: '35', currency: 'EUR', rate: '4.27' });
}

test.describe.configure({ timeout: 600_000 });

test('capture the screenshots the docs site shows', async ({ page, account }) => {
  mkdirSync(out, { recursive: true });
  await page.setViewportSize(desktop);
  await signIn(page, account);
  await seed(page);

  for (const theme of ['Light', 'Dark'] as const) {
    const name = theme.toLowerCase() as 'light' | 'dark';
    await setTheme(page, theme);

    await goToMyDay(page);
    await shot(page, 'my-day', name, desktop);
    await shot(page, 'my-day', name, phone);

    await goToInbox(page);
    await shot(page, 'inbox', name, phone);

    await page.setViewportSize(desktop);
    await openArea(page, 'Work');
    await shot(page, 'area', name, desktop);

    await goTo(page, 'Budgets');
    await page.getByRole('link', { name: 'Household' }).first().click();
    await expect(page.getByRole('heading', { name: 'Household', exact: true })).toBeVisible();
    await shot(page, 'budget', name, desktop);
  }
});

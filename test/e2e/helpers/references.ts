import { expect, Locator, Page } from '@playwright/test';
import { closePanel, openFabAction, panel, savePanel } from './panels';

export type FieldKind = 'Automatic' | 'Text' | 'Link' | 'Path' | 'Quantity';

export function referenceRow(page: Page, name: string) {
  return page.locator('.pspad-reference-row').filter({ has: page.locator('.pspad-row-name', { hasText: name }) });
}

export async function addReferenceItem(page: Page, name: string) {
  if (await page.getByRole('button', { name: 'Add item' }).isVisible()) {
    await page.getByRole('button', { name: 'Add item' }).click();
  } else {
    await openFabAction(page, 'Add item');
  }
  await panel(page).getByPlaceholder('Item name').fill(name);
  await savePanel(page);
  await expect(referenceRow(page, name)).toBeVisible();
}

export async function openReferenceItem(page: Page, name: string) {
  await referenceRow(page, name).locator('.pspad-reference-name').click();
  await expect(panel(page).getByPlaceholder('Item name')).toHaveValue(name);
}

export function fieldRow(page: Page, label: string): Locator {
  return panel(page).locator('.pspad-field-row').filter({ has: page.locator('.pspad-field-label', { hasText: label }) });
}

export async function addField(page: Page, label: string, value: string) {
  await panel(page).getByLabel('New label', { exact: true }).fill(label);
  await panel(page).getByLabel('New label value', { exact: true }).fill(value);
  await panel(page).getByLabel('New label value', { exact: true }).press('Enter');
  await expect(panel(page).getByLabel('New label', { exact: true })).toHaveValue('');
  await expect(page.locator('.pspad-reference-row .pspad-row-chip-slot', { hasText: label })).toBeVisible();
}

export async function reopenReferenceItem(page: Page, name: string) {
  await closePanel(page);
  await openReferenceItem(page, name);
}

export async function setFieldKind(page: Page, label: string, kind: FieldKind) {
  await fieldRow(page, label).locator('.pspad-field-row-body').click();
  const editor = panel(page).locator('.pspad-field-editor');
  await expect(page.locator('.mud-popover-open')).toHaveCount(0);
  await editor.locator('.pspad-field-kind-input').click();
  await page.locator('.mud-popover-open').getByText(kind, { exact: true }).click();
  await expect(page.locator('.mud-popover-open')).toHaveCount(0);
  await editor.locator('.pspad-field-save').click();
  await expect(fieldRow(page, label)).toBeVisible();
}

export async function editFieldValue(page: Page, label: string, value: string) {
  await fieldRow(page, label).locator('.pspad-field-row-body').click();
  const editor = panel(page).locator('.pspad-field-editor');
  const input = editor.locator('.pspad-field-value-input textarea, .pspad-field-value-input input').first();
  await input.fill(value);
  await input.press('Tab');
  await expect(input).toHaveValue(value);
  await editor.locator('.pspad-field-save').click();
  await expect(editor).toHaveCount(0);
}

export async function fieldLabels(page: Page): Promise<string[]> {
  return panel(page).locator('.pspad-field-row .pspad-field-label').allInnerTexts();
}

export async function dragRow(source: Locator, target: Locator) {
  await expect(source).toBeInViewport();
  await expect(target).toBeInViewport();
  await source.dragTo(target, { force: true, targetPosition: { x: 20, y: 4 } });
}

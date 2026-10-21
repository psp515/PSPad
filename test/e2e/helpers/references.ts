import { expect, Locator, Page } from '@playwright/test';
import { awaitNoDrawer, closePanel, openFabAction, panel, savePanel } from './panels';

export type FieldKind = 'Automatic' | 'Text' | 'Link' | 'Path' | 'Quantity';

export function referenceRow(page: Page, name: string) {
  return page.locator('.pspad-reference-row').filter({ has: page.locator('.pspad-row-name', { hasText: name }) });
}

export async function addReferenceItem(page: Page, name: string) {
  await awaitNoDrawer(page);
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
  const labelInput = panel(page).getByLabel('New label', { exact: true });
  const valueInput = panel(page).getByLabel('New label value', { exact: true });
  const chip = page.locator('.pspad-reference-row .pspad-row-chip-slot', { hasText: label });
  await expect(async () => {
    if ((await chip.count()) === 0) {
      await labelInput.fill(label);
      await valueInput.fill(value);
      await valueInput.press('Enter');
    }
    await expect(chip).toBeVisible({ timeout: 3_000 });
  }).toPass({ timeout: 30_000 });
  if ((await labelInput.inputValue()) !== '') {
    await labelInput.fill('');
    await valueInput.fill('');
  }
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
  const editor = panel(page).locator('.pspad-field-editor');
  const labelInput = editor.locator('.pspad-field-label-input input').first();
  const input = editor.locator('.pspad-field-value-input textarea, .pspad-field-value-input input').first();
  await expect(async () => {
    if ((await editor.count()) === 0) {
      await fieldRow(page, label).locator('.pspad-field-row-body').click({ timeout: 2_000 });
    }
    await input.fill(value, { timeout: 2_000 });
    await input.press('Tab');
    await expect(input).toHaveValue(value, { timeout: 2_000 });
    if ((await labelInput.inputValue({ timeout: 2_000 })) !== label) {
      await labelInput.fill(label, { timeout: 2_000 });
    }
    await expect(labelInput).toHaveValue(label, { timeout: 2_000 });
    await editor.locator('.pspad-field-save').click({ timeout: 2_000 });
    await expect(editor).toHaveCount(0, { timeout: 3_000 });
  }).toPass({ timeout: 30_000 });
}

export async function removeField(page: Page, label: string) {
  const chip = page.locator('.pspad-reference-row .pspad-row-chip-slot', { hasText: label });
  await expect(async () => {
    if ((await fieldRow(page, label).count()) > 0) {
      await fieldRow(page, label).getByRole('button', { name: `Remove ${label}` }).click({ timeout: 2_000 });
    }
    await expect(chip).toHaveCount(0, { timeout: 3_000 });
  }).toPass({ timeout: 30_000 });
}

export async function fieldLabels(page: Page): Promise<string[]> {
  return panel(page).locator('.pspad-field-row .pspad-field-label').allInnerTexts();
}

export async function dragRow(source: Locator, target: Locator) {
  await expect(source).toBeInViewport();
  await expect(target).toBeInViewport();
  await source.dragTo(target, { force: true, targetPosition: { x: 20, y: 4 } });
}

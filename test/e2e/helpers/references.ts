import { expect, Locator, Page } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { Device } from './api';
import { awaitNoDrawer, closePanel, openFabAction, panel, savePanel } from './panels';

export type FieldKind = 'Automatic' | 'Text' | 'Link' | 'Path' | 'Quantity';

export function referenceRow(page: Page, name: string) {
  return page.getByRole('article', { name });
}

function labelChip(page: Page, label: string) {
  return page.getByRole('list', { name: 'Labels', exact: true }).getByRole('listitem').filter({ hasText: label });
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
  await referenceRow(page, name).getByText(name, { exact: true }).click();
  await expect(panel(page).getByPlaceholder('Item name')).toHaveValue(name);
}

export function fieldRow(page: Page, label: string): Locator {
  return panel(page).getByRole('group', { name: label, exact: true });
}

export async function addField(page: Page, label: string, value: string) {
  const labelInput = panel(page).getByLabel('New label', { exact: true });
  const valueInput = panel(page).getByLabel('New label value', { exact: true });
  await labelInput.fill(label);
  await valueInput.fill(value);
  await valueInput.press('Enter');
  await expect(labelChip(page, label)).toBeVisible();
  await expect(labelInput).toHaveValue('');
}

export async function reopenReferenceItem(page: Page, name: string) {
  await closePanel(page);
  await openReferenceItem(page, name);
}

function fieldEditor(page: Page) {
  return {
    label: panel(page).getByRole('textbox', { name: 'Label', exact: true }),
    value: panel(page).getByRole('textbox', { name: 'Value', exact: true }),
    display: panel(page).getByRole('combobox', { name: 'Display', exact: true }),
    save: panel(page).getByRole('button', { name: 'Save', exact: true }),
  };
}

async function openFieldEditor(page: Page, label: string) {
  const editor = fieldEditor(page);
  await expect(async () => {
    if (!(await editor.label.isVisible())) {
      await fieldRow(page, label).click({ timeout: 2_000 });
    }
    await expect(editor.label).toHaveValue(label, { timeout: 2_000 });
  }).toPass({ timeout: 30_000 });
  return editor;
}

export async function setFieldKind(page: Page, label: string, kind: FieldKind) {
  const editor = await openFieldEditor(page, label);
  await expect(page.getByRole('option')).toHaveCount(0);
  await editor.display.click();
  await page.getByRole('option', { name: kind, exact: true }).click();
  await expect(page.getByRole('option')).toHaveCount(0);
  await editor.save.click();
  await expect(editor.label).toHaveCount(0);
  await expect(fieldRow(page, label)).toBeVisible();
}

export async function editFieldValue(page: Page, label: string, value: string) {
  const editor = await openFieldEditor(page, label);
  await editor.value.fill(value);
  await editor.value.press('Tab');
  await expect(editor.value).toHaveValue(value);
  await expect(editor.label).toHaveValue(label);
  await editor.save.click();
  await expect(editor.label).toHaveCount(0);
}

export async function removeField(page: Page, label: string) {
  await fieldRow(page, label).getByRole('button', { name: `Remove ${label}`, exact: true }).click();
  await expect(fieldRow(page, label)).toHaveCount(0);
  await expect(labelChip(page, label)).toHaveCount(0);
}

export async function fieldLabels(page: Page): Promise<string[]> {
  return panel(page)
    .getByRole('group')
    .filter({ has: page.getByRole('button', { name: /^Remove ./ }) })
    .evaluateAll(groups => groups.map(group => group.getAttribute('aria-label') ?? ''));
}

export async function dragRow(source: Locator, target: Locator) {
  await expect(source).toBeInViewport();
  await expect(target).toBeInViewport();
  await source.dragTo(target, { force: true, targetPosition: { x: 20, y: 4 } });
}

export async function createItemsOn(
  device: Device, listId: string, names: string[], firstPosition: number,
): Promise<Record<string, string>> {
  const ids: Record<string, string> = {};
  for (const [index, name] of names.entries()) {
    ids[name] = randomUUID();
    await device.send('CreateReferenceItem', { itemId: ids[name], listId, name, position: firstPosition + index });
  }
  return ids;
}

export async function addDraftField(page: Page, label: string, value: string) {
  await panel(page).getByLabel('New label', { exact: true }).fill(label);
  const valueInput = panel(page).getByLabel('New label value', { exact: true });
  await valueInput.fill(value);
  await valueInput.press('Enter');
  await expect(fieldRow(page, label)).toBeVisible();
}

export async function addFieldsOn(device: Device, itemId: string, fields: Array<[string, string]>) {
  for (const [label, value] of fields) {
    await device.send('AddReferenceField', { itemId, fieldId: randomUUID(), label, value, display: null });
  }
}

function field(doc: Record<string, unknown>, name: string): unknown {
  return doc[name] ?? doc[name.charAt(0).toUpperCase() + name.slice(1)];
}

export async function storedItem(device: Device, name: string) {
  const doc = (await device.documents('referenceitems')).find(item => field(item, 'name') === name && !field(item, 'deleted'));
  if (!doc) {
    return null;
  }
  const fields = (field(doc, 'fields') as Array<Record<string, unknown>> | undefined) ?? [];
  return {
    listId: String(field(doc, 'listId')),
    labels: fields.map(item => field(item, 'label')),
    displays: fields.map(item => field(item, 'display') ?? null),
    description: field(doc, 'description') ?? null,
  };
}

export async function dropOn(target: Locator, data: Record<string, string>, fileName?: string) {
  const transfer = await target.page().evaluateHandle(({ data, fileName }) => {
    const dropped = new DataTransfer();
    for (const [type, value] of Object.entries(data)) {
      dropped.setData(type, value);
    }
    if (fileName) {
      dropped.items.add(new File(['x'], fileName));
    }
    return dropped;
  }, { data, fileName });
  await target.dispatchEvent('drop', { dataTransfer: transfer });
}

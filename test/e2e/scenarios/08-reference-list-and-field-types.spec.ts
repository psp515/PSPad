import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { createList, openList } from '../helpers/lists';
import { confirm, panel } from '../helpers/panels';
import {
  addField, addReferenceItem, dragRow, editFieldValue, fieldLabels, fieldRow, openReferenceItem, referenceRow, reopenReferenceItem,
  setFieldKind,
  removeField,
} from '../helpers/references';
import { signIn } from '../helpers/sign-in';

test('a reference item renders each field kind and keeps its field order', async ({ page, account }) => {
  const item = `Router ${account.suffix}`;

  await signIn(page, account);
  await createArea(page, `Tech ${account.suffix}`);
  await createList(page, `Devices ${account.suffix}`, 'Reference');
  await openList(page, `Devices ${account.suffix}`);
  await addReferenceItem(page, item);
  await openReferenceItem(page, item);

  await addField(page, 'Model', 'RT-AX88U');
  await addField(page, 'Admin', 'https://192.168.1.1/admin');
  await addField(page, 'Config', '/etc/router/config.yaml');
  await addField(page, 'Ports', '8');
  await addField(page, 'Docs', 'https://example.com/router-docs');
  await expect(async () => {
    await reopenReferenceItem(page, item);
    expect(await fieldLabels(page)).toEqual(['Model', 'Admin', 'Config', 'Ports', 'Docs']);
  }).toPass({ timeout: 30_000 });

  await setFieldKind(page, 'Model', 'Text');
  await setFieldKind(page, 'Admin', 'Link');
  await setFieldKind(page, 'Config', 'Path');
  await setFieldKind(page, 'Ports', 'Quantity');
  await reopenReferenceItem(page, item);

  await expect(fieldRow(page, 'Model').locator('.pspad-field-text')).toHaveText('RT-AX88U');
  await expect(fieldRow(page, 'Admin').locator('a.pspad-field-link')).toHaveAttribute('href', 'https://192.168.1.1/admin');
  await expect(fieldRow(page, 'Config').locator('code.pspad-field-path')).toHaveText('/etc/router/config.yaml');
  await expect(fieldRow(page, 'Ports').locator('.pspad-field-quantity')).toHaveText('8');
  await expect(fieldRow(page, 'Docs').locator('a.pspad-field-link')).toHaveAttribute('href', 'https://example.com/router-docs');

  await dragRow(fieldRow(page, 'Docs').getByLabel('Drag Docs'), fieldRow(page, 'Model'));
  await expect.poll(() => fieldLabels(page)).toEqual(['Docs', 'Model', 'Admin', 'Config', 'Ports']);

  await editFieldValue(page, 'Ports', '12');
  await removeField(page, 'Config');

  await page.reload();
  await expect(panel(page).getByPlaceholder('Item name')).toHaveValue(item);
  await expect.poll(() => fieldLabels(page)).toEqual(['Docs', 'Model', 'Admin', 'Ports']);
  await expect(fieldRow(page, 'Admin').locator('a.pspad-field-link')).toBeVisible();
  await expect(fieldRow(page, 'Ports').locator('.pspad-field-quantity')).toHaveText('12');

  await panel(page).locator('.pspad-item-delete').click();
  await confirm(page, 'Delete');
  await expect(referenceRow(page, item)).toHaveCount(0);
  await expect(page.getByText('No items yet.')).toBeVisible();
});

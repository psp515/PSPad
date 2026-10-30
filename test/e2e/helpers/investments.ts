import { expect, Page } from '@playwright/test';
import { addDays, utcToday } from './dates';
import { openInApp } from './nav';
import { Cell, excelSerial, workbook } from './xlsx';

export const xtbAccount = '12345678';

const header: Cell[] = ['Type', 'Instrument', 'Ticker', 'Category', 'Time', 'Amount', 'ID', 'Comment', 'Product', 'Position ID'];

function row(type: string, instrument: string | null, ticker: string | null, daysAgo: number, amount: number,
  id: number, comment: string, positionId: number | null): Cell[] {
  const time = excelSerial(addDays(utcToday(), -daysAgo)) + 0.5;
  return [type, instrument, ticker, instrument ? 'STOCK' : null, time, amount, id, comment, 'IKE', positionId];
}

export const statementRows = {
  deposit: row('IKE deposit', null, null, 40, 10_000, 900000001, 'IKE deposit', null),
  buyAcme: row('Stock purchase', 'Acme SA', 'ACM.PL', 30, -1_000, 900000002, 'OPEN BUY ACM.PL 10 @ 100', 800000001),
  buyBolt: row('Stock purchase', 'Bolt SA', 'BLT.PL', 25, -500, 900000003, 'OPEN BUY BLT.PL 5 @ 100', 800000002),
  sellBolt: row('Stock sell', 'Bolt SA', 'BLT.PL', 20, 550, 900000004, 'CLOSE BUY BLT.PL 5 @ 110', 800000002),
  split: row('Stock split', 'Acme SA', 'ACM.PL', 15, 1, 900000005, 'Split 1:1', null),
};

export function xtbStatement(name = `account_${xtbAccount}_en_xlsx.xlsx`) {
  const cash: Cell[][] = [
    ['Account number', xtbAccount],
    ['Cash Operations'],
    header,
    ...Object.values(statementRows),
    ['Total', null, null, null, null, 9_051],
  ];
  return {
    name,
    mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    buffer: workbook([{ name: 'Cash Operations', rows: cash }]),
  };
}

export const statementErrorRow = 3 + Object.keys(statementRows).length;

export async function stubQuotes(page: Page): Promise<string[]> {
  const asked: string[] = [];
  await page.context().unroute('**/api/investments/quotes**');
  await page.context().route('**/api/investments/quotes**', route => {
    asked.push(new URL(route.request().url()).searchParams.get('symbol') ?? '');
    return route.fulfill({ status: 404 });
  });
  return asked;
}

export async function openInvestments(page: Page, path = '/investments') {
  await openInApp(page, path);
  await expect(page.getByRole('heading', { name: 'Investments', exact: true })).toBeVisible();
}

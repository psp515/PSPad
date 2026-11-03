import { expect, Page } from '@playwright/test';
import { addDays, isoDate, utcToday } from './dates';
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

export const tradesAccount = '20000001';
export const plansAccount = '20000002';
export const dollarAccount = '20000003';

function productRow(type: string, instrument: string | null, ticker: string | null, daysAgo: number, amount: number,
  id: number, comment: string, positionId: number | null, product: string): Cell[] {
  const time = excelSerial(addDays(utcToday(), -daysAgo)) + 0.5;
  return [type, instrument, ticker, instrument ? 'STOCK' : null, time, amount, id, comment, product, positionId];
}

const trades = 'My Trades';
const plans = 'Investment Plans';
const toPlans = `Transfer from ${tradesAccount} to ${plansAccount}`;

export const productStatementRows = {
  deposit: productRow('Deposit', null, null, 40, 4_000, 910000001, 'Deposit', null, trades),
  conversion: productRow('Transfer', null, null, 36, -400, 910000002,
    `Currency conversion, PLN to USD from TA: ${tradesAccount} to: ${dollarAccount}, Exchange rate:0.25`, null, trades),
  subaccountOut: productRow('Subaccount transfer', null, null, 34, -1_000, 910000003, toPlans, null, trades),
  subaccountIn: productRow('Subaccount transfer', null, null, 34, 1_000, 910000004, toPlans, null, plans),
  buyKappa: productRow('Stock purchase', 'Kappa SA', 'KPA.PL', 30, -500, 910000005, 'OPEN BUY KPA.PL 10 @ 50', 810000001, trades),
  buyZeta: productRow('Stock purchase', 'Zeta SA', 'ZTA.PL', 25, -800, 910000006, 'OPEN BUY ZTA.PL 8 @ 100', 810000002, plans),
  sellKappa: productRow('Stock sell', 'Kappa SA', 'KPA.PL', 20, 600, 910000007, 'CLOSE BUY KPA.PL 10 @ 60', 810000001, trades),
  sellNewco: productRow('Stock sell', 'Newco SA', 'NWC.PL', 18, 175, 910000008, 'CLOSE BUY NWC.PL 7 @ 25', 810000003, trades),
};

const closedHeader: Cell[] = ['Instrument', 'Ticker', 'Category', 'Type', 'Volume', 'Open Price', 'Open Time (UTC)', 'Close Price',
  'Close Time (UTC)', 'Product', 'Profit/Loss', 'Gross Profit', 'Purchase Value', 'Sale Value', 'Position ID', 'Comment'];

export const spunOffPosition: Cell[] = ['Newco SA', 'NWC.PL', 'STOCK', 'BUY', 7, 0.002,
  excelSerial(addDays(utcToday(), -22)) + 0.5, 25, excelSerial(addDays(utcToday(), -18)) + 0.5, trades, 174.99, 174.99, 0.01, 175,
  810000003, 'KPA.PL spinoff'];

export function xtbProductStatement(name = `account_${tradesAccount}_en_xlsx.xlsx`) {
  const cash: Cell[][] = [
    ['Account number', tradesAccount],
    ['Cash Operations'],
    header,
    ...Object.values(productStatementRows),
    ['Total', null, null, null, null, 2_900],
  ];
  return {
    name,
    mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    buffer: workbook([
      { name: 'Cash Operations', rows: cash },
      { name: 'Closed Positions', rows: [['Closed Positions'], closedHeader, spunOffPosition] },
    ]),
  };
}

export const dollarStatementRows = {
  conversion: productRow('Transfer', null, null, 36, 100, 920000001,
    `Currency conversion, PLN to USD from TA: ${tradesAccount} to: ${dollarAccount}, Exchange rate:0.25`, null, trades),
};

export function xtbDollarStatement(name = `account_${dollarAccount}_en_xlsx.xlsx`) {
  const cash: Cell[][] = [
    ['Account number', dollarAccount],
    ['Cash Operations'],
    header,
    ...Object.values(dollarStatementRows),
    ['Total', null, null, null, null, 100],
  ];
  return {
    name,
    mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    buffer: workbook([{ name: 'Cash Operations', rows: cash }]),
  };
}

export async function stubNbpSeries(page: Page, rates: Record<string, number>) {
  await page.context().unroute('**/api/money/nbp-rates?**');
  await page.context().route('**/api/money/nbp-rates?**', route => {
    const query = new URL(route.request().url()).searchParams;
    const currency = query.get('currency') ?? '';
    if (!(currency in rates)) {
      return route.fulfill({ status: 404 });
    }
    const to = new Date(`${query.get('to')}T00:00:00Z`);
    const days: { currency: string; rate: number; effectiveDate: string }[] = [];
    for (let day = new Date(`${query.get('from')}T00:00:00Z`); day <= to; day = addDays(day, 1)) {
      days.push({ currency, rate: rates[currency], effectiveDate: isoDate(day) });
    }
    return route.fulfill({ json: days });
  });
}

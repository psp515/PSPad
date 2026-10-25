export type Scenario = 'quick' | 'vps'
export type KeycloakStore = 'h2' | 'pg'
export type MongoHost = 'vps' | 'atlas'
export type Exposure = 'tunnel' | 'dns'

export interface Choices {
  scenario: Scenario
  kc: KeycloakStore
  mongo: MongoHost
  expose: Exposure
}

export const quick: Choices = { scenario: 'quick', kc: 'h2', mongo: 'vps', expose: 'tunnel' }
export const defaults: Choices = quick

const stores: KeycloakStore[] = ['h2', 'pg']
const hosts: MongoHost[] = ['vps', 'atlas']
const exposures: Exposure[] = ['tunnel', 'dns']

export function combinations(): Choices[] {
  const vps = stores.flatMap((kc) => hosts.flatMap((mongo) => exposures.map((expose) => ({ scenario: 'vps' as const, kc, mongo, expose }))))
  return [quick, ...vps]
}

export function key(c: Choices): string {
  return c.scenario === 'quick' ? 'quick' : `vps-${c.kc}-${c.mongo}-${c.expose}`
}

function pick<T extends string>(allowed: T[], value: string | null, fallback: T): T {
  return allowed.includes(value as T) ? (value as T) : fallback
}

export function parse(params: URLSearchParams): Choices {
  const scenario = pick<Scenario>(['quick', 'vps'], params.get('scenario'), defaults.scenario)
  if (scenario === 'quick') return quick
  return {
    scenario,
    kc: pick(stores, params.get('kc'), defaults.kc),
    mongo: pick(hosts, params.get('mongo'), defaults.mongo),
    expose: pick(exposures, params.get('expose'), defaults.expose)
  }
}

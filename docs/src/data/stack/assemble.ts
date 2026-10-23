import type { Choices } from './options'

const images = {
  api: 'ghcr.io/psp515/pspad-backend:latest',
  app: 'ghcr.io/psp515/pspad-frontend:latest',
  mongo: 'mongo:8.3.11',
  keycloak: 'quay.io/keycloak/keycloak:26.8.1',
  postgres: 'postgres:18.6-alpine3.24',
  cloudflared: 'cloudflare/cloudflared:2026.9.1',
  caddy: 'caddy:2.11.2-alpine'
}

const localMongo = 'mongodb://pspad:${MONGO_PASSWORD}@mongo:27017/pspad?authSource=admin&directConnection=true'
const quickMongo = 'mongodb://pspad:pspad@mongo:27017/pspad?authSource=admin&directConnection=true'

function api(c: Choices): string[] {
  const quick = c.scenario === 'quick'
  const mongo = c.mongo === 'atlas' ? '${MONGO_CONNECTION_STRING}' : quick ? quickMongo : localMongo
  const lines = [
    '  api:',
    `    image: ${images.api}`,
    '    restart: unless-stopped',
    '    environment:',
    `      Mongo__ConnectionString: ${mongo}`,
    '      Mongo__Database: pspad',
    '      Keycloak__Authority: http://keycloak:8080/realms/psplace',
    '      Keycloak__Audience: pspad-backend',
    '      Keycloak__RequireHttpsMetadata: "false"',
    `      Keycloak__AdminUser: ${quick ? 'admin' : '${KEYCLOAK_ADMIN_USER}'}`,
    `      Keycloak__AdminPassword: ${quick ? 'admin' : '${KEYCLOAK_ADMIN_PASSWORD}'}`,
    `      Cors__AllowedOrigins__0: ${quick ? 'http://localhost:5001' : '${APP_BASE_ADDRESS}'}`
  ]
  if (!quick) {
    lines.push('      Proxy__KnownNetworks__0: ${PROXY_NETWORK}')
    lines.push(`      Proxy__ClientIpHeader: ${c.expose === 'tunnel' ? 'CF-Connecting-IP' : 'X-Forwarded-For'}`)
  }
  if (quick) lines.push('    ports: ["5000:8080"]')
  if (c.mongo === 'vps') lines.push('    depends_on:', '      mongo:', '        condition: service_healthy')
  return [...lines, '']
}

function app(c: Choices): string[] {
  const quick = c.scenario === 'quick'
  const lines = [
    '  app:',
    `    image: ${images.app}`,
    '    restart: unless-stopped',
    '    environment:',
    `      API_BASE_ADDRESS: ${quick ? 'http://localhost:5000' : '${API_BASE_ADDRESS}'}`,
    `      APP_BASE_ADDRESS: ${quick ? 'http://localhost:5001' : '${APP_BASE_ADDRESS}'}`,
    `      KEYCLOAK_AUTHORITY: ${quick ? 'http://localhost:8080/realms/psplace' : '${KEYCLOAK_AUTHORITY}'}`,
    '      KEYCLOAK_CLIENT_ID: pspad-frontend'
  ]
  if (quick) lines.push('    ports: ["5001:8080"]')
  lines.push('    depends_on:', '      - api')
  return [...lines, '']
}

function mongo(c: Choices): string[] {
  if (c.mongo !== 'vps') return []
  const password = c.scenario === 'quick' ? 'pspad' : '${MONGO_PASSWORD}'
  return [
    '  mongo:',
    `    image: ${images.mongo}`,
    '    restart: unless-stopped',
    '    entrypoint: ["bash", "/mongo-entrypoint.sh"]',
    '    environment:',
    '      MONGO_INITDB_ROOT_USERNAME: pspad',
    `      MONGO_INITDB_ROOT_PASSWORD: ${password}`,
    '    volumes:',
    '      - mongo-data:/data/db',
    '      - mongo-keyfile:/keyfile',
    '      - ./mongo-entrypoint.sh:/mongo-entrypoint.sh:ro',
    '    healthcheck:',
    `      test: ["CMD-SHELL", "mongosh --quiet -u pspad -p ${password} --eval \\"try { rs.status().ok } catch (e) { rs.initiate({_id:'rs0',members:[{_id:0,host:'localhost:27017'}]}).ok }\\" | grep -q 1"]`,
    '      interval: 5s',
    '      timeout: 10s',
    '      retries: 20',
    '      start_period: 10s',
    ''
  ]
}

function keycloak(c: Choices): string[] {
  const quick = c.scenario === 'quick'
  const lines = [
    '  keycloak:',
    `    image: ${images.keycloak}`,
    '    restart: unless-stopped',
    quick ? '    command: ["start-dev", "--import-realm"]' : '    command: ["start", "--import-realm"]',
    '    environment:',
    `      KC_BOOTSTRAP_ADMIN_USERNAME: ${quick ? 'admin' : '${KEYCLOAK_ADMIN_USER}'}`,
    `      KC_BOOTSTRAP_ADMIN_PASSWORD: ${quick ? 'admin' : '${KEYCLOAK_ADMIN_PASSWORD}'}`,
    '      KC_HEALTH_ENABLED: "true"',
    `      KC_HOSTNAME: ${quick ? 'http://localhost:8080' : '${KEYCLOAK_HOSTNAME}'}`,
    '      KC_HOSTNAME_BACKCHANNEL_DYNAMIC: "true"',
    '      KC_PROXY_HEADERS: xforwarded',
    `      APP_BASE_ADDRESS: ${quick ? 'http://localhost:5001' : '${APP_BASE_ADDRESS}'}`,
    `      API_BASE_ADDRESS: ${quick ? 'http://localhost:5000' : '${API_BASE_ADDRESS}'}`
  ]
  if (!quick) lines.push('      KC_HTTP_ENABLED: "true"', '      KC_HOSTNAME_STRICT: "true"')
  if (c.kc === 'pg') {
    lines.push(
      '      KC_DB: postgres',
      '      KC_DB_URL: jdbc:postgresql://postgres:5432/keycloak',
      '      KC_DB_USERNAME: keycloak',
      '      KC_DB_PASSWORD: ${POSTGRES_PASSWORD}'
    )
  }
  if (quick) lines.push('    ports: ["8080:8080"]')
  lines.push('    volumes:', '      - ./keycloak:/opt/keycloak/data/import:ro')
  if (c.kc === 'h2') lines.push('      - keycloak-data:/opt/keycloak/data')
  if (c.kc === 'pg') lines.push('    depends_on:', '      postgres:', '        condition: service_healthy')
  return [...lines, '']
}

function postgres(c: Choices): string[] {
  if (c.kc !== 'pg') return []
  return [
    '  postgres:',
    `    image: ${images.postgres}`,
    '    restart: unless-stopped',
    '    environment:',
    '      POSTGRES_DB: keycloak',
    '      POSTGRES_USER: keycloak',
    '      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}',
    '    volumes:',
    '      - postgres-data:/var/lib/postgresql',
    '    healthcheck:',
    '      test: ["CMD-SHELL", "pg_isready -U keycloak"]',
    '      interval: 5s',
    '      retries: 20',
    ''
  ]
}

function proxy(c: Choices): string[] {
  if (c.scenario === 'quick') return []
  if (c.expose === 'tunnel') {
    return [
      '  cloudflared:',
      `    image: ${images.cloudflared}`,
      '    restart: unless-stopped',
      '    command: ["tunnel", "--no-autoupdate", "run"]',
      '    environment:',
      '      TUNNEL_TOKEN: ${CLOUDFLARE_TUNNEL_TOKEN}',
      ''
    ]
  }
  return [
    '  caddy:',
    `    image: ${images.caddy}`,
    '    restart: unless-stopped',
    '    ports: ["80:80", "443:443"]',
    '    volumes:',
    '      - ./Caddyfile:/etc/caddy/Caddyfile:ro',
    '      - caddy-data:/data',
    ''
  ]
}

function volumes(c: Choices): string[] {
  const names: string[] = []
  if (c.mongo === 'vps') names.push('mongo-data', 'mongo-keyfile')
  if (c.kc === 'h2') names.push('keycloak-data')
  if (c.kc === 'pg') names.push('postgres-data')
  if (c.scenario === 'vps' && c.expose === 'dns') names.push('caddy-data')
  return ['volumes:', ...names.map((n) => `  ${n}:`)]
}

function networks(c: Choices): string[] {
  if (c.scenario === 'quick') return []
  return ['', 'networks:', '  default:', '    ipam:', '      config:', '        - subnet: 10.77.0.0/24']
}

export function compose(c: Choices): string {
  return [
    'name: pspad',
    '',
    'services:',
    ...api(c),
    ...app(c),
    ...mongo(c),
    ...keycloak(c),
    ...postgres(c),
    ...proxy(c),
    ...volumes(c),
    ...networks(c)
  ].join('\n') + '\n'
}

export function env(c: Choices): string {
  if (c.scenario === 'quick') return ''
  const lines = [
    '# Public addresses, as a browser reaches them.',
    'APP_BASE_ADDRESS=https://pspad.example.com',
    'API_BASE_ADDRESS=https://pspad-api.example.com',
    'KEYCLOAK_HOSTNAME=https://auth.example.com',
    'KEYCLOAK_AUTHORITY=https://auth.example.com/realms/psplace',
    '',
    '# Keycloak administrator. Created on the first start only; the API uses it to delete accounts.',
    'KEYCLOAK_ADMIN_USER=admin',
    'KEYCLOAK_ADMIN_PASSWORD=CHANGE_ME',
    ''
  ]
  if (c.mongo === 'atlas') {
    lines.push('# From Atlas: Connect, Drivers, with the password filled in.')
    lines.push('MONGO_CONNECTION_STRING=mongodb+srv://pspad:CHANGE_ME@CLUSTER.mongodb.net/pspad?retryWrites=true&w=majority', '')
  } else {
    lines.push('# Root password of the bundled MongoDB.', 'MONGO_PASSWORD=CHANGE_ME', '')
  }
  if (c.kc === 'pg') lines.push('# Password of the Keycloak database.', 'POSTGRES_PASSWORD=CHANGE_ME', '')
  if (c.expose === 'tunnel') lines.push('# Token from Zero Trust, Networks, Tunnels.', 'CLOUDFLARE_TUNNEL_TOKEN=CHANGE_ME', '')
  lines.push('# Subnet of this stack, declared in compose.yaml. The API trusts the client address header only from here.')
  lines.push('PROXY_NETWORK=10.77.0.0/24')
  return lines.join('\n') + '\n'
}

export function caddyfile(c: Choices): string {
  if (c.scenario !== 'vps' || c.expose !== 'dns') return ''
  return [
    'pspad.example.com {',
    '    reverse_proxy app:8080',
    '}',
    '',
    'pspad-api.example.com {',
    '    reverse_proxy api:8080',
    '}',
    '',
    'auth.example.com {',
    '    reverse_proxy keycloak:8080',
    '}'
  ].join('\n') + '\n'
}

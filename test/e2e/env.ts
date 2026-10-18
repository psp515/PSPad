export const env = {
  appUrl: process.env.E2E_APP_URL ?? 'http://localhost:5001',
  apiUrl: process.env.E2E_API_URL ?? 'http://localhost:5000',
  keycloakUrl: process.env.E2E_KEYCLOAK_URL ?? 'http://localhost:18080',
  adminUser: process.env.E2E_KEYCLOAK_ADMIN_USER ?? 'admin',
  adminPassword: process.env.E2E_KEYCLOAK_ADMIN_PASSWORD ?? 'admin',
  realm: 'psplace',
};

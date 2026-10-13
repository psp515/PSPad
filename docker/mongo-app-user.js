const database = process.env.MONGO_DATABASE;
const user = process.env.MONGO_APP_USER;
const pwd = process.env.MONGO_APP_PASSWORD;

if (!database || !user || !pwd) {
  throw new Error('MONGO_DATABASE, MONGO_APP_USER and MONGO_APP_PASSWORD must all be set');
}

while (!db.hello().isWritablePrimary) {
  sleep(1000);
}

const target = db.getSiblingDB(database);
const roles = [{ role: 'readWrite', db: database }];

if (target.getUser(user)) {
  target.updateUser(user, { pwd, roles });
  print(`updated ${user} on ${database}`);
} else {
  target.createUser({ user, pwd, roles });
  print(`created ${user} on ${database}`);
}

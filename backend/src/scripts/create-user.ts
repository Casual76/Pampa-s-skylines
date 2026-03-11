import { randomUUID } from "node:crypto";
import bcrypt from "bcryptjs";
import { loadConfig } from "../config.js";
import { FileStore } from "../storage/file-store.js";

async function main() {
  const username = readArg("--username");
  const password = readArg("--password");

  if (!username || !password) {
    throw new Error("Usage: npm.cmd run create-user -- --username <name> --password <password> [--display-name <label>]");
  }

  const displayName = readArg("--display-name") ?? username;

  const config = loadConfig();
  const store = new FileStore(config.dataRoot);
  const existing = await store.getUserByUsername(username);
  if (existing) {
    throw new Error(`User '${username}' already exists.`);
  }

  await store.createUser({
    id: randomUUID(),
    username: username.trim().toLowerCase(),
    displayName,
    passwordHash: await bcrypt.hash(password, 10),
    createdAtUtc: new Date().toISOString()
  });

  console.log(`Created user '${username}'.`);
}

function readArg(name: string): string | undefined {
  const index = process.argv.findIndex((value) => value === name);
  return index >= 0 ? process.argv[index + 1] : undefined;
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : String(error));
  process.exitCode = 1;
});

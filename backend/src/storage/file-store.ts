import { gzipSync, gunzipSync } from "node:zlib";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import type { CitySnapshot, ProfileState, SnapshotWriteReason, SyncHead } from "../contracts.js";
import { computeSnapshotContentHash, migrateCitySnapshot } from "../migrations/city-snapshot.js";

export interface StoredUser {
  id: string;
  username: string;
  displayName: string;
  passwordHash: string;
  createdAtUtc: string;
}

interface UserIndex {
  users: StoredUser[];
}

export interface SnapshotWriteResult {
  applied: boolean;
  head: SyncHead;
  reason: SnapshotWriteReason;
}

export class FileStore {
  constructor(private readonly dataRoot: string) {}

  async createUser(user: StoredUser): Promise<void> {
    const index = await this.readUserIndex();
    index.users.push(user);
    await this.writeJson(this.userIndexPath(), index);
  }

  async getUserById(userId: string): Promise<StoredUser | null> {
    const index = await this.readUserIndex();
    return index.users.find((user) => user.id === userId) ?? null;
  }

  async getUserByUsername(username: string): Promise<StoredUser | null> {
    const canonical = username.trim().toLowerCase();
    const index = await this.readUserIndex();
    return index.users.find((user) => user.username === canonical) ?? null;
  }

  async getProfile(userId: string): Promise<ProfileState | null> {
    const user = await this.getUserById(userId);
    if (!user) {
      return null;
    }

    return {
      userId: user.id,
      username: user.displayName,
      activeCityHead: await this.getCityHead(userId)
    };
  }

  async getCityHead(userId: string): Promise<SyncHead | null> {
    return this.readJson(this.cityHeadPath(userId), null);
  }

  async putSnapshot(userId: string, head: SyncHead, snapshot: CitySnapshot): Promise<SnapshotWriteResult> {
    const migratedSnapshot = migrateCitySnapshot(snapshot);
    const calculatedChecksum = computeSnapshotContentHash(migratedSnapshot);

    if (head.cityId !== migratedSnapshot.cityId || head.version !== migratedSnapshot.version) {
      return {
        applied: false,
        head: await this.getCityHead(userId) ?? {
          ...head,
          checksum: calculatedChecksum
        },
        reason: "version_conflict"
      };
    }

    if (head.checksum !== calculatedChecksum || (migratedSnapshot.contentHash && migratedSnapshot.contentHash !== calculatedChecksum)) {
      return {
        applied: false,
        head: await this.getCityHead(userId) ?? {
          ...head,
          checksum: calculatedChecksum
        },
        reason: "checksum_mismatch"
      };
    }

    const currentHead = await this.getCityHead(userId);
    if (currentHead) {
      if (currentHead.version === migratedSnapshot.version && currentHead.checksum === calculatedChecksum) {
        return {
          applied: true,
          head: currentHead,
          reason: "duplicate_version"
        };
      }

      if (currentHead.version === migratedSnapshot.version && currentHead.checksum !== calculatedChecksum) {
        return {
          applied: false,
          head: currentHead,
          reason: "version_conflict"
        };
      }

      const currentTime = Date.parse(currentHead.clientUpdatedAtUtc);
      const incomingTime = Date.parse(head.clientUpdatedAtUtc);
      if (Number.isFinite(currentTime) && Number.isFinite(incomingTime) && incomingTime < currentTime) {
        return {
          applied: false,
          head: currentHead,
          reason: "stale_head"
        };
      }
    }

    const normalizedHead: SyncHead = {
      ...head,
      checksum: calculatedChecksum,
      clientId: migratedSnapshot.clientId,
      commandCount: migratedSnapshot.commandCount,
      tick: migratedSnapshot.state.tick
    };

    await mkdir(this.snapshotDirectory(userId), { recursive: true });
    await this.writeJsonGzip(this.snapshotPath(userId, normalizedHead.version), migratedSnapshot);
    await this.writeJson(this.cityHeadPath(userId), normalizedHead);

    return {
      applied: true,
      head: normalizedHead,
      reason: "applied"
    };
  }

  async getSnapshot(userId: string, version: string): Promise<CitySnapshot | null> {
    const snapshotPath = this.snapshotPath(userId, version);
    try {
      const compressed = await readFile(snapshotPath);
      const parsed = JSON.parse(gunzipSync(compressed).toString("utf8")) as CitySnapshot;
      return migrateCitySnapshot(parsed);
    } catch {
      return null;
    }
  }

  private async readUserIndex(): Promise<UserIndex> {
    await mkdir(this.dataRoot, { recursive: true });
    return this.readJson(this.userIndexPath(), { users: [] satisfies StoredUser[] });
  }

  private userIndexPath(): string {
    return path.join(this.dataRoot, "users.json");
  }

  private cityRoot(userId: string): string {
    return path.join(this.dataRoot, "cities", userId);
  }

  private cityHeadPath(userId: string): string {
    return path.join(this.cityRoot(userId), "head.json");
  }

  private snapshotDirectory(userId: string): string {
    return path.join(this.cityRoot(userId), "snapshots");
  }

  private snapshotPath(userId: string, version: string): string {
    return path.join(this.snapshotDirectory(userId), `${version}.json.gz`);
  }

  private async readJson<T>(filePath: string, fallback: T): Promise<T> {
    try {
      const raw = await readFile(filePath, "utf8");
      return JSON.parse(raw) as T;
    } catch {
      return fallback;
    }
  }

  private async writeJson(filePath: string, value: unknown): Promise<void> {
    await mkdir(path.dirname(filePath), { recursive: true });
    await writeFile(filePath, `${JSON.stringify(value, null, 2)}\n`, "utf8");
  }

  private async writeJsonGzip(filePath: string, value: unknown): Promise<void> {
    await mkdir(path.dirname(filePath), { recursive: true });
    await writeFile(filePath, gzipSync(JSON.stringify(value)));
  }
}

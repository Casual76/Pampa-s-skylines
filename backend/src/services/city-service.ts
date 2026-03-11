import type { CitySnapshot, ProfileState, SyncHead } from "../contracts.js";
import { FileStore } from "../storage/file-store.js";

export class CityService {
  constructor(private readonly store: FileStore) {}

  getProfile(userId: string): Promise<ProfileState | null> {
    return this.store.getProfile(userId);
  }

  getHead(userId: string): Promise<SyncHead | null> {
    return this.store.getCityHead(userId);
  }

  putSnapshot(userId: string, head: SyncHead, snapshot: CitySnapshot) {
    return this.store.putSnapshot(userId, head, snapshot);
  }

  getSnapshot(userId: string, version: string): Promise<CitySnapshot | null> {
    return this.store.getSnapshot(userId, version);
  }
}

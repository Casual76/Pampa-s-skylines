import { getApps, initializeApp } from "firebase-admin/app";
import { getAuth } from "firebase-admin/auth";
import type { AppConfig } from "../config.js";

export class FirebaseTokenService {
  constructor(private readonly config: AppConfig) {}

  async createCustomToken(userId: string, username: string): Promise<string | null> {
    if (!this.config.firebaseProjectId) {
      return null;
    }

    try {
      const app = getApps()[0] ?? initializeApp({ projectId: this.config.firebaseProjectId });
      return await getAuth(app).createCustomToken(userId, { username });
    } catch {
      return null;
    }
  }
}

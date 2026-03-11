import bcrypt from "bcryptjs";
import jwt from "jsonwebtoken";
import type { AppConfig } from "../config.js";
import type { ProfileState } from "../contracts.js";
import type { StoredUser } from "../storage/file-store.js";
import { FileStore } from "../storage/file-store.js";
import { FirebaseTokenService } from "./firebase-token-service.js";

interface TokenPayload {
  sub: string;
  username: string;
  type: "access" | "refresh";
}

export interface AuthResult {
  accessToken: string;
  refreshToken: string;
  firebaseCustomToken: string | null;
  profile: ProfileState;
}

export class AuthService {
  constructor(
    private readonly store: FileStore,
    private readonly config: AppConfig,
    private readonly firebaseTokenService: FirebaseTokenService
  ) {}

  async login(username: string, password: string): Promise<AuthResult | null> {
    const user = await this.store.getUserByUsername(username);
    if (!user) {
      return null;
    }

    const valid = await bcrypt.compare(password, user.passwordHash);
    if (!valid) {
      return null;
    }

    return this.issueTokens(user);
  }

  async refresh(refreshToken: string): Promise<AuthResult | null> {
    const payload = this.verifyToken(refreshToken, "refresh");
    if (!payload) {
      return null;
    }

    const user = await this.store.getUserById(payload.sub);
    if (!user) {
      return null;
    }

    return this.issueTokens(user);
  }

  verifyAccessToken(token: string): TokenPayload | null {
    return this.verifyToken(token, "access");
  }

  private verifyToken(token: string, expectedType: TokenPayload["type"]): TokenPayload | null {
    try {
      const payload = jwt.verify(token, this.config.jwtSecret) as TokenPayload;
      return payload.type === expectedType ? payload : null;
    } catch {
      return null;
    }
  }

  private async issueTokens(user: StoredUser): Promise<AuthResult> {
    const accessToken = jwt.sign(
      { sub: user.id, username: user.username, type: "access" satisfies TokenPayload["type"] },
      this.config.jwtSecret,
      { expiresIn: "1h" }
    );

    const refreshToken = jwt.sign(
      { sub: user.id, username: user.username, type: "refresh" satisfies TokenPayload["type"] },
      this.config.jwtSecret,
      { expiresIn: "30d" }
    );

    const profile = await this.store.getProfile(user.id);
    if (!profile) {
      throw new Error("PROFILE_NOT_FOUND_AFTER_LOGIN");
    }

    return {
      accessToken,
      refreshToken,
      firebaseCustomToken: await this.firebaseTokenService.createCustomToken(user.id, user.username),
      profile
    };
  }
}

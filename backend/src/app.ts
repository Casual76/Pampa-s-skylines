import express, { type NextFunction, type Request, type Response } from "express";
import { loadConfig, type AppConfig } from "./config.js";
import {
  loginRequestSchema,
  profileSchema,
  refreshRequestSchema,
  rawUploadSnapshotRequestSchema,
  syncHeadSchema,
  versionManifestSchema
} from "./contracts.js";
import { migrateCitySnapshot } from "./migrations/city-snapshot.js";
import { AuthService } from "./services/auth-service.js";
import { CityService } from "./services/city-service.js";
import { FirebaseTokenService } from "./services/firebase-token-service.js";
import { VersionService } from "./services/version-service.js";
import { FileStore } from "./storage/file-store.js";

interface CreateAppDependencies {
  config?: AppConfig;
  store?: FileStore;
  authService?: AuthService;
  cityService?: CityService;
  versionService?: VersionService;
}

interface AuthenticatedRequest extends Request {
  auth?: {
    userId: string;
    username: string;
  };
}

export function createApp(dependencies: CreateAppDependencies = {}) {
  const config = dependencies.config ?? loadConfig();
  const store = dependencies.store ?? new FileStore(config.dataRoot);
  const authService =
    dependencies.authService ??
    new AuthService(store, config, new FirebaseTokenService(config));
  const cityService = dependencies.cityService ?? new CityService(store);
  const versionService = dependencies.versionService ?? new VersionService(config);

  const app = express();
  app.use(express.json({ limit: "20mb" }));

  app.get("/health", (_request, response) => {
    response.json({ ok: true });
  });

  app.post("/auth/login", asyncHandler(async (request, response) => {
    const payload = loginRequestSchema.parse(request.body);
    const result = await authService.login(payload.username, payload.password);

    if (!result) {
      response.status(401).json({ error: "INVALID_CREDENTIALS" });
      return;
    }

    response.json(result);
  }));

  app.post("/auth/refresh", asyncHandler(async (request, response) => {
    const payload = refreshRequestSchema.parse(request.body);
    const result = await authService.refresh(payload.refreshToken);

    if (!result) {
      response.status(401).json({ error: "INVALID_REFRESH_TOKEN" });
      return;
    }

    response.json(result);
  }));

  app.get("/profile", requireAuth(authService), asyncHandler(async (request, response) => {
    const auth = (request as AuthenticatedRequest).auth!;
    const profile = await cityService.getProfile(auth.userId);

    if (!profile) {
      response.status(404).json({ error: "PROFILE_NOT_FOUND" });
      return;
    }

    response.json(profileSchema.parse(profile));
  }));

  app.get("/city/head", requireAuth(authService), asyncHandler(async (request, response) => {
    const auth = (request as AuthenticatedRequest).auth!;
    const head = await cityService.getHead(auth.userId);

    if (!head) {
      response.status(404).json({ error: "CITY_HEAD_NOT_FOUND" });
      return;
    }

    response.json(syncHeadSchema.parse(head));
  }));

  app.put("/city/snapshot", requireAuth(authService), asyncHandler(async (request, response) => {
    const auth = (request as AuthenticatedRequest).auth!;
    const payload = rawUploadSnapshotRequestSchema.parse(request.body);
    const result = await cityService.putSnapshot(auth.userId, payload.head, migrateCitySnapshot(payload.snapshot));
    response.json(result);
  }));

  app.get("/city/snapshot/:version", requireAuth(authService), asyncHandler(async (request, response) => {
    const auth = (request as AuthenticatedRequest).auth!;
    const version = Array.isArray(request.params.version) ? request.params.version[0] : request.params.version;
    const snapshot = await cityService.getSnapshot(auth.userId, version);

    if (!snapshot) {
      response.status(404).json({ error: "SNAPSHOT_NOT_FOUND" });
      return;
    }

    response.json(snapshot);
  }));

  app.get("/version-manifest", (_request, response) => {
    response.json(versionManifestSchema.parse(versionService.getManifest()));
  });

  app.use(errorHandler);
  return app;
}

function requireAuth(authService: AuthService) {
  return (request: Request, response: Response, next: NextFunction) => {
    const header = request.header("authorization");
    if (!header?.startsWith("Bearer ")) {
      response.status(401).json({ error: "MISSING_BEARER_TOKEN" });
      return;
    }

    const payload = authService.verifyAccessToken(header.slice("Bearer ".length));
    if (!payload) {
      response.status(401).json({ error: "INVALID_ACCESS_TOKEN" });
      return;
    }

    (request as AuthenticatedRequest).auth = {
      userId: payload.sub,
      username: payload.username
    };

    next();
  };
}

function asyncHandler(
  handler: (request: Request, response: Response, next: NextFunction) => Promise<void>
) {
  return (request: Request, response: Response, next: NextFunction) => {
    handler(request, response, next).catch(next);
  };
}

function errorHandler(error: unknown, _request: Request, response: Response, _next: NextFunction) {
  if (error instanceof Error && "issues" in error) {
    response.status(400).json({
      error: "VALIDATION_ERROR",
      detail: error.message
    });
    return;
  }

  const message = error instanceof Error ? error.message : "UNKNOWN_ERROR";
  response.status(500).json({ error: "INTERNAL_SERVER_ERROR", detail: message });
}

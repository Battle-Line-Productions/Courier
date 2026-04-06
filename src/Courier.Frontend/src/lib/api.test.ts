import { describe, it, expect, vi, beforeEach } from "vitest";
import { api, ApiClientError } from "./api";

// ---------------------------------------------------------------------------
// Mock fetch globally
// ---------------------------------------------------------------------------
const mockFetch = vi.fn();
vi.stubGlobal("fetch", mockFetch);

// Helper: build a mock Response that `request()` can consume
function okResponse<T>(data: T) {
  return {
    ok: true,
    status: 200,
    statusText: "OK",
    json: () => Promise.resolve(data),
  };
}

function errorResponse(
  status: number,
  statusText: string,
  body: Record<string, unknown>,
) {
  return {
    ok: false,
    status,
    statusText,
    json: () => Promise.resolve(body),
  };
}

// ---------------------------------------------------------------------------
// Reset state between tests
// ---------------------------------------------------------------------------
beforeEach(() => {
  mockFetch.mockReset();
  api.setAccessToken(null);
});

// =========================================================================
// 1. ApiClientError
// =========================================================================
describe("ApiClientError", () => {
  it("sets code, systemMessage, message, and name", () => {
    const err = new ApiClientError({
      code: 2001,
      systemMessage: "job_not_found",
      message: "Job not found",
    });

    expect(err).toBeInstanceOf(Error);
    expect(err.name).toBe("ApiClientError");
    expect(err.code).toBe(2001);
    expect(err.systemMessage).toBe("job_not_found");
    expect(err.message).toBe("Job not found");
    expect(err.details).toBeUndefined();
  });

  it("sets details when provided", () => {
    const details = [
      { field: "name", message: "Name is required" },
      { field: "host", message: "Host is required" },
    ];
    const err = new ApiClientError({
      code: 1001,
      systemMessage: "validation_error",
      message: "Validation failed",
      details,
    });

    expect(err.details).toEqual(details);
    expect(err.details).toHaveLength(2);
  });

  it("inherits from Error and has a stack trace", () => {
    const err = new ApiClientError({
      code: 1000,
      systemMessage: "test",
      message: "test error",
    });

    expect(err.stack).toBeDefined();
    expect(err.stack).toContain("ApiClientError");
  });
});

// =========================================================================
// 2. ApiClient constructor and setAccessToken
// =========================================================================
describe("ApiClient instance (via exported api)", () => {
  it("uses the configured base URL for requests", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: [] }));

    await api.listStepTypes();

    const calledUrl = mockFetch.mock.calls[0][0] as string;
    // The URL should start with the configured base (env or default)
    expect(calledUrl).toContain("/api/v1/step-types");
  });

  it("setAccessToken stores token for subsequent requests", async () => {
    api.setAccessToken("my-jwt-token");
    mockFetch.mockResolvedValueOnce(okResponse({ data: {} }));

    await api.getSetupStatus();

    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(opts.headers).toHaveProperty(
      "Authorization",
      "Bearer my-jwt-token",
    );
  });

  it("setAccessToken(null) clears the token", async () => {
    api.setAccessToken("some-token");
    api.setAccessToken(null);
    mockFetch.mockResolvedValueOnce(okResponse({ data: {} }));

    await api.getSetupStatus();

    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    const headers = opts.headers as Record<string, string>;
    expect(headers.Authorization).toBeUndefined();
  });
});

// =========================================================================
// 3. request() — Authorization header behavior
// =========================================================================
describe("request() — headers", () => {
  it("sends Authorization header when token is set", async () => {
    api.setAccessToken("token-123");
    mockFetch.mockResolvedValueOnce(okResponse({ data: {} }));

    await api.getSetupStatus();

    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    const headers = opts.headers as Record<string, string>;
    expect(headers.Authorization).toBe("Bearer token-123");
  });

  it("does NOT send Authorization header when no token is set", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: {} }));

    await api.getSetupStatus();

    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    const headers = opts.headers as Record<string, string>;
    expect(headers.Authorization).toBeUndefined();
  });

  it("sends Content-Type: application/json when body is present", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { accessToken: "x", refreshToken: "y" } }),
    );

    await api.login({ username: "admin", password: "pass" });

    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    const headers = opts.headers as Record<string, string>;
    expect(headers["Content-Type"]).toBe("application/json");
  });

  it("does NOT send Content-Type when no body is present", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: {} }));

    await api.getSetupStatus();

    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    const headers = opts.headers as Record<string, string>;
    expect(headers["Content-Type"]).toBeUndefined();
  });
});

// =========================================================================
// 4. request() — error handling
// =========================================================================
describe("request() — error handling", () => {
  it("throws ApiClientError with code 10007 on 401 without body.error", async () => {
    mockFetch.mockResolvedValueOnce(
      errorResponse(401, "Unauthorized", { message: "no auth" }),
    );

    await expect(api.getSetupStatus()).rejects.toThrow(ApiClientError);

    try {
      mockFetch.mockResolvedValueOnce(
        errorResponse(401, "Unauthorized", { message: "no auth" }),
      );
      await api.getSetupStatus();
    } catch (e) {
      const err = e as ApiClientError;
      expect(err.code).toBe(10007);
      expect(err.systemMessage).toBe("Unauthorized");
      expect(err.message).toBe(
        "Your session has expired. Please log in again.",
      );
    }
  });

  it("throws generic Error on non-ok response without body.error", async () => {
    mockFetch.mockResolvedValueOnce(
      errorResponse(500, "Internal Server Error", {}),
    );

    await expect(api.getSetupStatus()).rejects.toThrow(Error);
    mockFetch.mockResolvedValueOnce(
      errorResponse(500, "Internal Server Error", {}),
    );
    await expect(api.getSetupStatus()).rejects.toThrow(
      "HTTP 500: Internal Server Error",
    );
  });

  it("throws ApiClientError when body.error is present", async () => {
    mockFetch.mockResolvedValueOnce(
      errorResponse(400, "Bad Request", {
        error: {
          code: 2002,
          systemMessage: "job_invalid",
          message: "Job is invalid",
          details: [{ field: "name", message: "Required" }],
        },
      }),
    );

    try {
      await api.getSetupStatus();
      expect.fail("Should have thrown");
    } catch (e) {
      const err = e as ApiClientError;
      expect(err).toBeInstanceOf(ApiClientError);
      expect(err.code).toBe(2002);
      expect(err.systemMessage).toBe("job_invalid");
      expect(err.message).toBe("Job is invalid");
      expect(err.details).toEqual([{ field: "name", message: "Required" }]);
    }
  });

  it("throws ApiClientError when body.error is present even on 200 status", async () => {
    // Edge case: body has error field even though HTTP status is 200
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      statusText: "OK",
      json: () =>
        Promise.resolve({
          error: {
            code: 9999,
            systemMessage: "unexpected",
            message: "Unexpected error in body",
          },
        }),
    });

    await expect(api.getSetupStatus()).rejects.toThrow(ApiClientError);
  });

  it("returns parsed body on success", async () => {
    const responseData = { data: { isInitialized: true } };
    mockFetch.mockResolvedValueOnce(okResponse(responseData));

    const result = await api.getSetupStatus();
    expect(result).toEqual(responseData);
  });
});

// =========================================================================
// 5. Jobs API methods
// =========================================================================
describe("Jobs API", () => {
  it("listJobs — GET with pagination query params", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 2, pageSize: 5, totalCount: 0 }),
    );

    await api.listJobs(2, 5);

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/jobs?");
    expect(url).toContain("page=2");
    expect(url).toContain("pageSize=5");
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(opts.method).toBeUndefined(); // GET is default
  });

  it("listJobs — includes search and tag filters in query", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 1, pageSize: 10, totalCount: 0 }),
    );

    await api.listJobs(1, 10, { search: "nightly", tag: "production" });

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("search=nightly");
    expect(url).toContain("tag=production");
  });

  it("listJobs — omits search/tag when not provided", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 1, pageSize: 10, totalCount: 0 }),
    );

    await api.listJobs(1, 10);

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).not.toContain("search=");
    expect(url).not.toContain("tag=");
  });

  it("getJob — GET with id in path", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "abc-123", name: "Test Job" } }),
    );

    await api.getJob("abc-123");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/jobs/abc-123");
  });

  it("createJob — POST with JSON body", async () => {
    const jobData = { name: "New Job", description: "desc" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "new-id", ...jobData } }),
    );

    await api.createJob(jobData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/jobs");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify(jobData));
  });

  it("updateJob — PUT with id and JSON body", async () => {
    const updateData = { name: "Updated Job" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "abc-123", ...updateData } }),
    );

    await api.updateJob("abc-123", updateData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/jobs/abc-123");
    expect(opts.method).toBe("PUT");
    expect(opts.body).toBe(JSON.stringify(updateData));
  });

  it("deleteJob — DELETE with id", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: null }));

    await api.deleteJob("abc-123");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/jobs/abc-123");
    expect(opts.method).toBe("DELETE");
  });

  it("triggerJob — POST with id and trigger data", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "exec-1", status: "queued" } }),
    );

    await api.triggerJob("job-1", { triggeredBy: "api" });

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/jobs/job-1/trigger");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify({ triggeredBy: "api" }));
  });

  it("triggerJob — uses default triggeredBy when no data provided", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "exec-2", status: "queued" } }),
    );

    await api.triggerJob("job-1");

    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(opts.body).toBe(JSON.stringify({ triggeredBy: "ui" }));
  });
});

// =========================================================================
// 6. Step Types
// =========================================================================
describe("Step Types API", () => {
  it("listStepTypes — GET with correct path", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [{ typeKey: "file.copy" }] }),
    );

    const result = await api.listStepTypes();

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/step-types");
    expect(result.data).toEqual([{ typeKey: "file.copy" }]);
  });
});

// =========================================================================
// 7. Connections API
// =========================================================================
describe("Connections API", () => {
  it("listConnections — GET with pagination", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 1, pageSize: 10, totalCount: 0 }),
    );

    await api.listConnections(1, 10);

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/connections?");
    expect(url).toContain("page=1");
    expect(url).toContain("pageSize=10");
  });

  it("listConnections — includes all filter params", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 1, pageSize: 10, totalCount: 0 }),
    );

    await api.listConnections(1, 10, {
      search: "prod",
      protocol: "sftp",
      group: "finance",
      status: "active",
      tag: "critical",
    });

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("search=prod");
    expect(url).toContain("protocol=sftp");
    expect(url).toContain("group=finance");
    expect(url).toContain("status=active");
    expect(url).toContain("tag=critical");
  });

  it("testConnection — POST with id", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { success: true, latencyMs: 42 } }),
    );

    await api.testConnection("conn-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/connections/conn-1/test");
    expect(opts.method).toBe("POST");
  });
});

// =========================================================================
// 8. Auth API
// =========================================================================
describe("Auth API", () => {
  it("login — POST with credentials", async () => {
    const loginData = { username: "admin", password: "secret" };
    mockFetch.mockResolvedValueOnce(
      okResponse({
        data: { accessToken: "jwt", refreshToken: "refresh-tok" },
      }),
    );

    await api.login(loginData);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/auth/login");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify(loginData));
  });

  it("refreshToken — POST with refresh token data", async () => {
    const refreshData = { refreshToken: "old-refresh" };
    mockFetch.mockResolvedValueOnce(
      okResponse({
        data: { accessToken: "new-jwt", refreshToken: "new-refresh" },
      }),
    );

    await api.refreshToken(refreshData);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/auth/refresh");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify(refreshData));
  });

  it("logout — POST with refreshToken in body", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: null }));

    await api.logout("my-refresh-token");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/auth/logout");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(
      JSON.stringify({ refreshToken: "my-refresh-token" }),
    );
  });

  it("getMe — GET with no body", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "u1", username: "admin" } }),
    );

    await api.getMe();

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/auth/me");
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(opts.method).toBeUndefined();
  });

  it("changePassword — POST with password data", async () => {
    const pwData = { currentPassword: "old", newPassword: "new" };
    mockFetch.mockResolvedValueOnce(okResponse({ data: null }));

    await api.changePassword(pwData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/auth/change-password");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify(pwData));
  });
});

// =========================================================================
// 9. Filesystem
// =========================================================================
describe("Filesystem API", () => {
  it("browsePath — GET with path query param (encoded)", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { entries: [] } }),
    );

    await api.browseFilesystem("/var/data/files");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/filesystem/browse?path=");
    expect(url).toContain(encodeURIComponent("/var/data/files"));
  });

  it("browsePath — GET without path when not provided", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { entries: [] } }),
    );

    await api.browseFilesystem();

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/filesystem/browse");
    expect(url).not.toContain("?path=");
  });
});

// =========================================================================
// 10. Setup
// =========================================================================
describe("Setup API", () => {
  it("getSetupStatus — GET", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { isInitialized: false } }),
    );

    const result = await api.getSetupStatus();

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/setup/status");
    expect(result.data).toEqual({ isInitialized: false });
  });

  it("initializeSetup — POST with setup data", async () => {
    const setupData = {
      username: "admin",
      password: "P@ssw0rd!",
      displayName: "Admin",
    };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "u1", username: "admin" } }),
    );

    await api.initializeSetup(setupData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/setup/initialize");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify(setupData));
  });
});

// =========================================================================
// 11. Users (admin)
// =========================================================================
describe("Users API", () => {
  it("listUsers — GET with pagination and optional search", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 1, pageSize: 10, totalCount: 0 }),
    );

    await api.listUsers(1, 10, "john");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/users?");
    expect(url).toContain("page=1");
    expect(url).toContain("pageSize=10");
    expect(url).toContain("search=john");
  });

  it("listUsers — omits search param when not provided", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 1, pageSize: 10, totalCount: 0 }),
    );

    await api.listUsers();

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).not.toContain("search=");
  });
});

// =========================================================================
// 12. Tags
// =========================================================================
describe("Tags API", () => {
  it("listTags — GET with optional filter params", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 1, pageSize: 10, totalCount: 0 }),
    );

    await api.listTags({ page: 2, pageSize: 25, search: "env", category: "environment" });

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/tags?");
    expect(url).toContain("page=2");
    expect(url).toContain("pageSize=25");
    expect(url).toContain("search=env");
    expect(url).toContain("category=environment");
  });

  it("createTag — POST with JSON body", async () => {
    const tagData = { name: "production", color: "#ff0000" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "tag-1", ...tagData } }),
    );

    await api.createTag(tagData as any);

    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify(tagData));
  });
});

// =========================================================================
// 13. Chains
// =========================================================================
describe("Chains API", () => {
  it("listChains — GET with pagination and filters", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 1, pageSize: 10, totalCount: 0 }),
    );

    await api.listChains(1, 10, { search: "nightly", tag: "prod" });

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/chains?");
    expect(url).toContain("search=nightly");
    expect(url).toContain("tag=prod");
  });

  it("triggerChain — POST with default triggeredBy", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "ce-1" } }),
    );

    await api.triggerChain("chain-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/chains/chain-1/execute");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify({ triggeredBy: "ui" }));
  });
});

// =========================================================================
// 14. Monitors
// =========================================================================
describe("Monitors API", () => {
  it("listMonitors — GET with filters", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 1, pageSize: 10, totalCount: 0 }),
    );

    await api.listMonitors(1, 10, { search: "sftp", state: "active", tag: "prod" });

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/monitors?");
    expect(url).toContain("search=sftp");
    expect(url).toContain("state=active");
    expect(url).toContain("tag=prod");
  });

  it("activateMonitor — POST with correct path", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "m-1", state: "active" } }),
    );

    await api.activateMonitor("m-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/monitors/m-1/activate");
    expect(opts.method).toBe("POST");
  });
});

// =========================================================================
// 15. Dashboard
// =========================================================================
describe("Dashboard API", () => {
  it("getDashboardSummary — GET", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { totalJobs: 5 } }),
    );

    await api.getDashboardSummary();

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/dashboard/summary");
  });

  it("getRecentExecutions — GET with count param", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: [] }));

    await api.getRecentExecutions(20);

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/dashboard/recent-executions?count=20");
  });

  it("getExpiringKeys — GET with daysAhead param", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: [] }));

    await api.getExpiringKeys(60);

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/dashboard/key-expiry?daysAhead=60");
  });
});

// =========================================================================
// 16. Notification Rules
// =========================================================================
describe("Notification Rules API", () => {
  it("listNotificationRules — GET with filter params", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 1, pageSize: 10, totalCount: 0 }),
    );

    await api.listNotificationRules({
      page: 1,
      pageSize: 10,
      search: "alert",
      entityType: "job",
      channel: "email",
      isEnabled: true,
    });

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("search=alert");
    expect(url).toContain("entityType=job");
    expect(url).toContain("channel=email");
    expect(url).toContain("isEnabled=true");
  });

  it("testNotificationRule — POST with id", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { sent: true } }),
    );

    await api.testNotificationRule("rule-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/notification-rules/rule-1/test");
    expect(opts.method).toBe("POST");
  });
});

// =========================================================================
// 17. Settings
// =========================================================================
describe("Settings API", () => {
  it("getAuthSettings — GET", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { maxLoginAttempts: 5 } }),
    );

    await api.getAuthSettings();

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/settings/auth");
  });

  it("updateSmtpSettings — PUT with JSON body", async () => {
    const smtpData = { host: "smtp.example.com", port: 587 };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: smtpData }),
    );

    await api.updateSmtpSettings(smtpData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/settings/smtp");
    expect(opts.method).toBe("PUT");
    expect(opts.body).toBe(JSON.stringify(smtpData));
  });

  it("testSmtpConnection — POST", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { success: true } }),
    );

    await api.testSmtpConnection();

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/settings/smtp/test");
    expect(opts.method).toBe("POST");
  });
});

// =========================================================================
// 18. Auth Providers
// =========================================================================
describe("Auth Providers API", () => {
  it("listAuthProviders — GET with pagination", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 1, pageSize: 25, totalCount: 0 }),
    );

    await api.listAuthProviders(1, 25);

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/auth-providers?");
    expect(url).toContain("page=1");
    expect(url).toContain("pageSize=25");
  });

  it("getLoginOptions — GET with correct path", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [{ type: "local" }] }),
    );

    await api.getLoginOptions();

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/auth/login-options");
  });

  it("exchangeSsoCode — POST with code in body", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { accessToken: "sso-jwt" } }),
    );

    await api.exchangeSsoCode("auth-code-123");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/auth/sso/exchange");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify({ code: "auth-code-123" }));
  });
});

// =========================================================================
// 19. Audit Log
// =========================================================================
describe("Audit Log API", () => {
  it("listAuditLog — GET with all filter params", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 1, pageSize: 25, totalCount: 0 }),
    );

    await api.listAuditLog(1, 25, {
      entityType: "job",
      operation: "create",
      performedBy: "admin",
      from: "2026-01-01",
      to: "2026-12-31",
    });

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("entityType=job");
    expect(url).toContain("operation=create");
    expect(url).toContain("performedBy=admin");
    expect(url).toContain("from=2026-01-01");
    expect(url).toContain("to=2026-12-31");
  });

  it("listAuditLogByEntity — GET with entityType and entityId in path", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 1, pageSize: 25, totalCount: 0 }),
    );

    await api.listAuditLogByEntity("job", "job-123", 1, 25);

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/audit-log/entity/job/job-123");
    expect(url).toContain("page=1");
    expect(url).toContain("pageSize=25");
  });
});

// =========================================================================
// 20. Execution management
// =========================================================================
describe("Execution management API", () => {
  it("pauseExecution — POST with executionId", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "exec-1", status: "paused" } }),
    );

    await api.pauseExecution("exec-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/jobs/executions/exec-1/pause");
    expect(opts.method).toBe("POST");
  });

  it("resumeExecution — POST with executionId", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "exec-1", status: "running" } }),
    );

    await api.resumeExecution("exec-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/jobs/executions/exec-1/resume");
    expect(opts.method).toBe("POST");
  });

  it("cancelExecution — POST with reason in body", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "exec-1", status: "cancelled" } }),
    );

    await api.cancelExecution("exec-1", "User requested cancellation");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/jobs/executions/exec-1/cancel");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(
      JSON.stringify({ reason: "User requested cancellation" }),
    );
  });
});

// =========================================================================
// 21. Combined behavior: token + body headers together
// =========================================================================
describe("Combined header behavior", () => {
  it("sends both Authorization and Content-Type when token set and body present", async () => {
    api.setAccessToken("my-token");
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "j-1" } }),
    );

    await api.createJob({ name: "Test" } as any);

    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    const headers = opts.headers as Record<string, string>;
    expect(headers.Authorization).toBe("Bearer my-token");
    expect(headers["Content-Type"]).toBe("application/json");
  });

  it("sends only Authorization when token set but no body", async () => {
    api.setAccessToken("my-token");
    mockFetch.mockResolvedValueOnce(okResponse({ data: {} }));

    await api.getJob("j-1");

    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    const headers = opts.headers as Record<string, string>;
    expect(headers.Authorization).toBe("Bearer my-token");
    expect(headers["Content-Type"]).toBeUndefined();
  });
});

// =========================================================================
// 22. Steps API
// =========================================================================
describe("Steps API", () => {
  it("listSteps — GET with jobId in path", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [{ id: "step-1", typeKey: "file.copy" }] }),
    );

    await api.listSteps("job-1");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/jobs/job-1/steps");
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(opts.method).toBeUndefined(); // GET is default
  });

  it("replaceSteps — PUT with jobId and JSON body", async () => {
    const stepsData = { steps: [{ typeKey: "file.copy", configuration: {} }] };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [{ id: "step-1" }] }),
    );

    await api.replaceSteps("job-1", stepsData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/jobs/job-1/steps");
    expect(opts.method).toBe("PUT");
    expect(opts.body).toBe(JSON.stringify(stepsData));
  });
});

// =========================================================================
// 23. Dependencies API
// =========================================================================
describe("Dependencies API", () => {
  it("listJobDependencies — GET with jobId in path", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [{ id: "dep-1", dependsOnJobId: "job-2" }] }),
    );

    await api.listJobDependencies("job-1");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/jobs/job-1/dependencies");
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(opts.method).toBeUndefined();
  });

  it("addJobDependency — POST with jobId and JSON body", async () => {
    const depData = { dependsOnJobId: "job-2" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "dep-1", dependsOnJobId: "job-2" } }),
    );

    await api.addJobDependency("job-1", depData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/jobs/job-1/dependencies");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify(depData));
  });

  it("removeJobDependency — DELETE with jobId and dependencyId", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: null }));

    await api.removeJobDependency("job-1", "dep-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/jobs/job-1/dependencies/dep-1");
    expect(opts.method).toBe("DELETE");
  });
});

// =========================================================================
// 24. PGP Keys API
// =========================================================================
describe("PGP Keys API", () => {
  it("listPgpKeys — GET with pagination and filters", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], pagination: { page: 1, pageSize: 10, totalCount: 0, totalPages: 0 } }),
    );

    await api.listPgpKeys(1, 10, { search: "test", status: "active", keyType: "rsa", algorithm: "rsa4096", tag: "prod" });

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/pgp-keys?");
    expect(url).toContain("page=1");
    expect(url).toContain("pageSize=10");
    expect(url).toContain("search=test");
    expect(url).toContain("status=active");
    expect(url).toContain("keyType=rsa");
    expect(url).toContain("algorithm=rsa4096");
    expect(url).toContain("tag=prod");
  });

  it("listPgpKeys — omits filter params when not provided", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], pagination: { page: 1, pageSize: 10, totalCount: 0, totalPages: 0 } }),
    );

    await api.listPgpKeys(1, 10);

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("page=1");
    expect(url).not.toContain("search=");
    expect(url).not.toContain("status=");
    expect(url).not.toContain("keyType=");
    expect(url).not.toContain("algorithm=");
    expect(url).not.toContain("tag=");
  });

  it("getPgpKey — GET with id in path", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "pgp-1", name: "My Key" } }),
    );

    await api.getPgpKey("pgp-1");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/pgp-keys/pgp-1");
  });

  it("generatePgpKey — POST with JSON body", async () => {
    const genData = { name: "New PGP Key", algorithm: "rsa4096" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "pgp-2", ...genData } }),
    );

    await api.generatePgpKey(genData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/pgp-keys/generate");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify(genData));
  });

  it("importPgpKey — POST with FormData (no Content-Type header)", async () => {
    const formData = new FormData();
    formData.append("file", new Blob(["key-data"]), "key.asc");
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: () => Promise.resolve({ data: { id: "pgp-3" } }),
    });

    await api.importPgpKey(formData);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/pgp-keys/import");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(formData);
    // Should NOT set Content-Type (browser sets it with boundary for FormData)
    const headers = opts.headers as Record<string, string>;
    expect(headers["Content-Type"]).toBeUndefined();
  });

  it("importPgpKey — sends Authorization header when token set", async () => {
    api.setAccessToken("my-jwt");
    const formData = new FormData();
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: () => Promise.resolve({ data: { id: "pgp-4" } }),
    });

    await api.importPgpKey(formData);

    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    const headers = opts.headers as Record<string, string>;
    expect(headers["Authorization"]).toBe("Bearer my-jwt");
  });

  it("importPgpKey — throws ApiClientError when body has error", async () => {
    const formData = new FormData();
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 400,
      json: () =>
        Promise.resolve({
          error: { code: 4001, systemMessage: "invalid_key", message: "Invalid PGP key" },
        }),
    });

    await expect(api.importPgpKey(formData)).rejects.toThrow(ApiClientError);

    try {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 400,
        json: () =>
          Promise.resolve({
            error: { code: 4001, systemMessage: "invalid_key", message: "Invalid PGP key" },
          }),
      });
      await api.importPgpKey(formData);
    } catch (e) {
      const err = e as ApiClientError;
      expect(err.code).toBe(4001);
      expect(err.systemMessage).toBe("invalid_key");
    }
  });

  it("updatePgpKey — PUT with id and JSON body", async () => {
    const updateData = { name: "Updated Key" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "pgp-1", ...updateData } }),
    );

    await api.updatePgpKey("pgp-1", updateData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/pgp-keys/pgp-1");
    expect(opts.method).toBe("PUT");
    expect(opts.body).toBe(JSON.stringify(updateData));
  });

  it("deletePgpKey — DELETE with id", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: null }));

    await api.deletePgpKey("pgp-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/pgp-keys/pgp-1");
    expect(opts.method).toBe("DELETE");
  });

  it("exportPgpPublicKey — GET returning Blob", async () => {
    const blobData = new Blob(["-----BEGIN PGP PUBLIC KEY-----"]);
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      blob: () => Promise.resolve(blobData),
    });

    const result = await api.exportPgpPublicKey("pgp-1");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/pgp-keys/pgp-1/export/public");
    expect(result).toBe(blobData);
  });

  it("exportPgpPublicKey — sends Authorization header when token set", async () => {
    api.setAccessToken("my-jwt");
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      blob: () => Promise.resolve(new Blob(["key"])),
    });

    await api.exportPgpPublicKey("pgp-1");

    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    const headers = opts.headers as Record<string, string>;
    expect(headers["Authorization"]).toBe("Bearer my-jwt");
  });

  it("exportPgpPublicKey — throws Error on non-ok response", async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 404,
      statusText: "Not Found",
    });

    await expect(api.exportPgpPublicKey("pgp-bad")).rejects.toThrow(
      "Export failed: Not Found",
    );
  });

  it("retirePgpKey — POST with id", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "pgp-1", status: "retired" } }),
    );

    await api.retirePgpKey("pgp-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/pgp-keys/pgp-1/retire");
    expect(opts.method).toBe("POST");
  });

  it("revokePgpKey — POST with id", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "pgp-1", status: "revoked" } }),
    );

    await api.revokePgpKey("pgp-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/pgp-keys/pgp-1/revoke");
    expect(opts.method).toBe("POST");
  });

  it("activatePgpKey — POST with id", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "pgp-1", status: "active" } }),
    );

    await api.activatePgpKey("pgp-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/pgp-keys/pgp-1/activate");
    expect(opts.method).toBe("POST");
  });
});

// =========================================================================
// 25. SSH Keys API
// =========================================================================
describe("SSH Keys API", () => {
  it("listSshKeys — GET with pagination and filters", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], pagination: { page: 1, pageSize: 10, totalCount: 0, totalPages: 0 } }),
    );

    await api.listSshKeys(1, 10, { search: "deploy", status: "active", keyType: "ed25519", tag: "infra" });

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/ssh-keys?");
    expect(url).toContain("search=deploy");
    expect(url).toContain("status=active");
    expect(url).toContain("keyType=ed25519");
    expect(url).toContain("tag=infra");
  });

  it("getSshKey — GET with id in path", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "ssh-1", name: "Deploy Key" } }),
    );

    await api.getSshKey("ssh-1");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/ssh-keys/ssh-1");
  });

  it("generateSshKey — POST with JSON body", async () => {
    const genData = { name: "New SSH Key", keyType: "ed25519" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "ssh-2", ...genData } }),
    );

    await api.generateSshKey(genData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/ssh-keys/generate");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify(genData));
  });

  it("importSshKey — POST with FormData (no Content-Type header)", async () => {
    const formData = new FormData();
    formData.append("file", new Blob(["ssh-key-data"]), "id_ed25519.pub");
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: () => Promise.resolve({ data: { id: "ssh-3" } }),
    });

    await api.importSshKey(formData);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/ssh-keys/import");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(formData);
    const headers = opts.headers as Record<string, string>;
    expect(headers["Content-Type"]).toBeUndefined();
  });

  it("importSshKey — sends Authorization header when token set", async () => {
    api.setAccessToken("my-jwt");
    const formData = new FormData();
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: () => Promise.resolve({ data: { id: "ssh-4" } }),
    });

    await api.importSshKey(formData);

    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    const headers = opts.headers as Record<string, string>;
    expect(headers["Authorization"]).toBe("Bearer my-jwt");
  });

  it("importSshKey — throws ApiClientError when body has error", async () => {
    const formData = new FormData();
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 400,
      json: () =>
        Promise.resolve({
          error: { code: 4002, systemMessage: "invalid_ssh_key", message: "Invalid SSH key" },
        }),
    });

    await expect(api.importSshKey(formData)).rejects.toThrow(ApiClientError);
  });

  it("updateSshKey — PUT with id and JSON body", async () => {
    const updateData = { name: "Updated SSH Key" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "ssh-1", ...updateData } }),
    );

    await api.updateSshKey("ssh-1", updateData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/ssh-keys/ssh-1");
    expect(opts.method).toBe("PUT");
    expect(opts.body).toBe(JSON.stringify(updateData));
  });

  it("deleteSshKey — DELETE with id", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: null }));

    await api.deleteSshKey("ssh-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/ssh-keys/ssh-1");
    expect(opts.method).toBe("DELETE");
  });

  it("exportSshPublicKey — GET returning Blob", async () => {
    const blobData = new Blob(["ssh-ed25519 AAAA..."]);
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      blob: () => Promise.resolve(blobData),
    });

    const result = await api.exportSshPublicKey("ssh-1");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/ssh-keys/ssh-1/export/public");
    expect(result).toBe(blobData);
  });

  it("exportSshPublicKey — sends Authorization header when token set", async () => {
    api.setAccessToken("my-jwt");
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      blob: () => Promise.resolve(new Blob(["key"])),
    });

    await api.exportSshPublicKey("ssh-1");

    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    const headers = opts.headers as Record<string, string>;
    expect(headers["Authorization"]).toBe("Bearer my-jwt");
  });

  it("exportSshPublicKey — throws Error on non-ok response", async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 404,
      statusText: "Not Found",
    });

    await expect(api.exportSshPublicKey("ssh-bad")).rejects.toThrow(
      "Export failed: Not Found",
    );
  });

  it("retireSshKey — POST with id", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "ssh-1", status: "retired" } }),
    );

    await api.retireSshKey("ssh-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/ssh-keys/ssh-1/retire");
    expect(opts.method).toBe("POST");
  });

  it("activateSshKey — POST with id", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "ssh-1", status: "active" } }),
    );

    await api.activateSshKey("ssh-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/ssh-keys/ssh-1/activate");
    expect(opts.method).toBe("POST");
  });
});

// =========================================================================
// 26. Monitors API (extended)
// =========================================================================
describe("Monitors API (extended)", () => {
  it("getMonitor — GET with id in path", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "m-1", name: "SFTP Monitor" } }),
    );

    await api.getMonitor("m-1");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/monitors/m-1");
  });

  it("createMonitor — POST with JSON body", async () => {
    const monitorData = { name: "New Monitor", connectionId: "conn-1" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "m-2", ...monitorData } }),
    );

    await api.createMonitor(monitorData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/monitors");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify(monitorData));
  });

  it("updateMonitor — PUT with id and JSON body", async () => {
    const updateData = { name: "Updated Monitor" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "m-1", ...updateData } }),
    );

    await api.updateMonitor("m-1", updateData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/monitors/m-1");
    expect(opts.method).toBe("PUT");
    expect(opts.body).toBe(JSON.stringify(updateData));
  });

  it("deleteMonitor — DELETE with id", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: null }));

    await api.deleteMonitor("m-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/monitors/m-1");
    expect(opts.method).toBe("DELETE");
  });

  it("pauseMonitor — POST with id", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "m-1", state: "paused" } }),
    );

    await api.pauseMonitor("m-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/monitors/m-1/pause");
    expect(opts.method).toBe("POST");
  });

  it("disableMonitor — POST with id", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "m-1", state: "disabled" } }),
    );

    await api.disableMonitor("m-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/monitors/m-1/disable");
    expect(opts.method).toBe("POST");
  });

  it("acknowledgeMonitorError — POST with id", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "m-1", state: "active" } }),
    );

    await api.acknowledgeMonitorError("m-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/monitors/m-1/acknowledge-error");
    expect(opts.method).toBe("POST");
  });

  it("listMonitorFileLog — GET with monitorId and pagination", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 1, pageSize: 25, totalCount: 0 }),
    );

    await api.listMonitorFileLog("m-1", 2, 50);

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/monitors/m-1/file-log?");
    expect(url).toContain("page=2");
    expect(url).toContain("pageSize=50");
  });
});

// =========================================================================
// 27. Tags API (extended)
// =========================================================================
describe("Tags API (extended)", () => {
  it("getTag — GET with id in path", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "tag-1", name: "production" } }),
    );

    await api.getTag("tag-1");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/tags/tag-1");
  });

  it("updateTag — PUT with id and JSON body", async () => {
    const updateData = { name: "staging", color: "#00ff00" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "tag-1", ...updateData } }),
    );

    await api.updateTag("tag-1", updateData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/tags/tag-1");
    expect(opts.method).toBe("PUT");
    expect(opts.body).toBe(JSON.stringify(updateData));
  });

  it("deleteTag — DELETE with id", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: null }));

    await api.deleteTag("tag-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/tags/tag-1");
    expect(opts.method).toBe("DELETE");
  });

  it("assignTags — POST with JSON body", async () => {
    const assignData = { entityType: "job", entityId: "job-1", tagIds: ["tag-1", "tag-2"] };
    mockFetch.mockResolvedValueOnce(okResponse({ data: null }));

    await api.assignTags(assignData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/tags/assign");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify(assignData));
  });

  it("unassignTags — POST with JSON body", async () => {
    const unassignData = { entityType: "job", entityId: "job-1", tagIds: ["tag-1"] };
    mockFetch.mockResolvedValueOnce(okResponse({ data: null }));

    await api.unassignTags(unassignData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/tags/unassign");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify(unassignData));
  });

  it("listTagEntities — GET with tag id and optional params", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 1, pageSize: 10, totalCount: 0 }),
    );

    await api.listTagEntities("tag-1", { entityType: "job", page: 2, pageSize: 20 });

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/tags/tag-1/entities?");
    expect(url).toContain("entityType=job");
    expect(url).toContain("page=2");
    expect(url).toContain("pageSize=20");
  });
});

// =========================================================================
// 28. Chains API (extended)
// =========================================================================
describe("Chains API (extended)", () => {
  it("getChain — GET with id in path", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "chain-1", name: "Nightly Chain" } }),
    );

    await api.getChain("chain-1");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/chains/chain-1");
  });

  it("createChain — POST with JSON body", async () => {
    const chainData = { name: "New Chain", description: "test chain" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "chain-2", ...chainData } }),
    );

    await api.createChain(chainData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/chains");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify(chainData));
  });

  it("updateChain — PUT with id and JSON body", async () => {
    const updateData = { name: "Updated Chain" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "chain-1", ...updateData } }),
    );

    await api.updateChain("chain-1", updateData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/chains/chain-1");
    expect(opts.method).toBe("PUT");
    expect(opts.body).toBe(JSON.stringify(updateData));
  });

  it("deleteChain — DELETE with id", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: null }));

    await api.deleteChain("chain-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/chains/chain-1");
    expect(opts.method).toBe("DELETE");
  });

  it("replaceChainMembers — PUT with chainId and JSON body", async () => {
    const membersData = { members: [{ jobId: "job-1", order: 1 }] };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "chain-1" } }),
    );

    await api.replaceChainMembers("chain-1", membersData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/chains/chain-1/members");
    expect(opts.method).toBe("PUT");
    expect(opts.body).toBe(JSON.stringify(membersData));
  });

  it("listChainExecutions — GET with chainId and pagination", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 1, pageSize: 10, totalCount: 0 }),
    );

    await api.listChainExecutions("chain-1", 2, 20);

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/chains/chain-1/executions?");
    expect(url).toContain("page=2");
    expect(url).toContain("pageSize=20");
  });

  it("getChainExecution — GET with chainId and executionId", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "ce-1", status: "completed" } }),
    );

    await api.getChainExecution("chain-1", "ce-1");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/chains/chain-1/executions/ce-1");
  });

  it("listChainSchedules — GET with chainId", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [{ id: "cs-1", cronExpression: "0 0 * * *" }] }),
    );

    await api.listChainSchedules("chain-1");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/chains/chain-1/schedules");
  });

  it("createChainSchedule — POST with chainId and JSON body", async () => {
    const scheduleData = { cronExpression: "0 0 * * *", isEnabled: true };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "cs-1", ...scheduleData } }),
    );

    await api.createChainSchedule("chain-1", scheduleData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/chains/chain-1/schedules");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify(scheduleData));
  });

  it("updateChainSchedule — PUT with chainId, scheduleId, and JSON body", async () => {
    const updateData = { cronExpression: "0 6 * * *" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "cs-1", ...updateData } }),
    );

    await api.updateChainSchedule("chain-1", "cs-1", updateData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/chains/chain-1/schedules/cs-1");
    expect(opts.method).toBe("PUT");
    expect(opts.body).toBe(JSON.stringify(updateData));
  });

  it("deleteChainSchedule — DELETE with chainId and scheduleId", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: null }));

    await api.deleteChainSchedule("chain-1", "cs-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/chains/chain-1/schedules/cs-1");
    expect(opts.method).toBe("DELETE");
  });

  it("setChainEnabled — PUT with id and isEnabled flag", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "chain-1", isEnabled: false } }),
    );

    await api.setChainEnabled("chain-1", false);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/chains/chain-1");
    expect(opts.method).toBe("PUT");
    expect(opts.body).toBe(JSON.stringify({ name: "", isEnabled: false }));
  });
});

// =========================================================================
// 29. Notification Rules API (extended)
// =========================================================================
describe("Notification Rules API (extended)", () => {
  it("getNotificationRule — GET with id in path", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "rule-1", name: "Alert Rule" } }),
    );

    await api.getNotificationRule("rule-1");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/notification-rules/rule-1");
  });

  it("createNotificationRule — POST with JSON body", async () => {
    const ruleData = { name: "New Rule", channel: "email", entityType: "job" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "rule-2", ...ruleData } }),
    );

    await api.createNotificationRule(ruleData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/notification-rules");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify(ruleData));
  });

  it("updateNotificationRule — PUT with id and JSON body", async () => {
    const updateData = { name: "Updated Rule" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "rule-1", ...updateData } }),
    );

    await api.updateNotificationRule("rule-1", updateData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/notification-rules/rule-1");
    expect(opts.method).toBe("PUT");
    expect(opts.body).toBe(JSON.stringify(updateData));
  });

  it("deleteNotificationRule — DELETE with id", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: null }));

    await api.deleteNotificationRule("rule-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/notification-rules/rule-1");
    expect(opts.method).toBe("DELETE");
  });

  it("listNotificationLogs — GET with filter params", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 1, pageSize: 10, totalCount: 0 }),
    );

    await api.listNotificationLogs({
      page: 1,
      pageSize: 25,
      ruleId: "rule-1",
      entityType: "job",
      entityId: "job-1",
      success: true,
    });

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/notification-logs");
    expect(url).toContain("ruleId=rule-1");
    expect(url).toContain("entityType=job");
    expect(url).toContain("entityId=job-1");
    expect(url).toContain("success=true");
  });

  it("listNotificationLogs — omits query string when no params", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 1, pageSize: 10, totalCount: 0 }),
    );

    await api.listNotificationLogs();

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/notification-logs");
    expect(url).not.toContain("?");
  });
});

// =========================================================================
// 30. Users API (extended)
// =========================================================================
describe("Users API (extended)", () => {
  it("getUser — GET with id in path", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "u-1", username: "john" } }),
    );

    await api.getUser("u-1");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/users/u-1");
  });

  it("createUser — POST with JSON body", async () => {
    const userData = { username: "newuser", password: "Pass123!", displayName: "New User", role: "operator" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "u-2", ...userData } }),
    );

    await api.createUser(userData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/users");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify(userData));
  });

  it("updateUser — PUT with id and JSON body", async () => {
    const updateData = { displayName: "Updated User" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "u-1", ...updateData } }),
    );

    await api.updateUser("u-1", updateData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/users/u-1");
    expect(opts.method).toBe("PUT");
    expect(opts.body).toBe(JSON.stringify(updateData));
  });

  it("deleteUser — DELETE with id", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: null }));

    await api.deleteUser("u-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/users/u-1");
    expect(opts.method).toBe("DELETE");
  });

  it("resetUserPassword — POST with id and password data", async () => {
    const pwData = { newPassword: "NewPass123!" };
    mockFetch.mockResolvedValueOnce(okResponse({ data: null }));

    await api.resetUserPassword("u-1", pwData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/users/u-1/reset-password");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify(pwData));
  });
});

// =========================================================================
// 31. Auth Providers API (extended)
// =========================================================================
describe("Auth Providers API (extended)", () => {
  it("getAuthProvider — GET with id in path", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "ap-1", name: "Entra ID" } }),
    );

    await api.getAuthProvider("ap-1");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/auth-providers/ap-1");
  });

  it("createAuthProvider — POST with JSON body", async () => {
    const providerData = { name: "New Provider", type: "oidc", clientId: "abc" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "ap-2", ...providerData } }),
    );

    await api.createAuthProvider(providerData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/auth-providers");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify(providerData));
  });

  it("updateAuthProvider — PUT with id and JSON body", async () => {
    const updateData = { name: "Updated Provider" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "ap-1", ...updateData } }),
    );

    await api.updateAuthProvider("ap-1", updateData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/auth-providers/ap-1");
    expect(opts.method).toBe("PUT");
    expect(opts.body).toBe(JSON.stringify(updateData));
  });

  it("deleteAuthProvider — DELETE with id", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: null }));

    await api.deleteAuthProvider("ap-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/auth-providers/ap-1");
    expect(opts.method).toBe("DELETE");
  });

  it("testAuthProvider — POST with id", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { success: true } }),
    );

    await api.testAuthProvider("ap-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/auth-providers/ap-1/test");
    expect(opts.method).toBe("POST");
  });
});

// =========================================================================
// 32. Executions API
// =========================================================================
describe("Executions API", () => {
  it("listExecutions — GET with jobId and pagination", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 1, pageSize: 10, totalCount: 0 }),
    );

    await api.listExecutions("job-1", 2, 20);

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/jobs/job-1/executions?");
    expect(url).toContain("page=2");
    expect(url).toContain("pageSize=20");
  });

  it("listExecutions — uses default pagination params", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [], page: 1, pageSize: 10, totalCount: 0 }),
    );

    await api.listExecutions("job-1");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("page=1");
    expect(url).toContain("pageSize=10");
  });

  it("getExecution — GET with executionId in path", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "exec-1", status: "completed" } }),
    );

    await api.getExecution("exec-1");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/jobs/executions/exec-1");
  });
});

// =========================================================================
// 33. Schedules API
// =========================================================================
describe("Schedules API", () => {
  it("listSchedules — GET with jobId", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [{ id: "sched-1", cronExpression: "0 0 * * *" }] }),
    );

    await api.listSchedules("job-1");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/jobs/job-1/schedules");
  });

  it("createSchedule — POST with jobId and JSON body", async () => {
    const scheduleData = { cronExpression: "0 0 * * *", isEnabled: true };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "sched-1", ...scheduleData } }),
    );

    await api.createSchedule("job-1", scheduleData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/jobs/job-1/schedules");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify(scheduleData));
  });

  it("updateSchedule — PUT with jobId, scheduleId, and JSON body", async () => {
    const updateData = { cronExpression: "0 6 * * *" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "sched-1", ...updateData } }),
    );

    await api.updateSchedule("job-1", "sched-1", updateData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/jobs/job-1/schedules/sched-1");
    expect(opts.method).toBe("PUT");
    expect(opts.body).toBe(JSON.stringify(updateData));
  });

  it("deleteSchedule — DELETE with jobId and scheduleId", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: null }));

    await api.deleteSchedule("job-1", "sched-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/jobs/job-1/schedules/sched-1");
    expect(opts.method).toBe("DELETE");
  });
});

// =========================================================================
// 34. Connections API (extended)
// =========================================================================
describe("Connections API (extended)", () => {
  it("getConnection — GET with id in path", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "conn-1", name: "SFTP Prod" } }),
    );

    await api.getConnection("conn-1");

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/connections/conn-1");
  });

  it("createConnection — POST with JSON body", async () => {
    const connData = { name: "New Connection", protocol: "sftp", host: "sftp.example.com" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "conn-2", ...connData } }),
    );

    await api.createConnection(connData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/connections");
    expect(opts.method).toBe("POST");
    expect(opts.body).toBe(JSON.stringify(connData));
  });

  it("updateConnection — PUT with id and JSON body", async () => {
    const updateData = { name: "Updated Connection" };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { id: "conn-1", ...updateData } }),
    );

    await api.updateConnection("conn-1", updateData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/connections/conn-1");
    expect(opts.method).toBe("PUT");
    expect(opts.body).toBe(JSON.stringify(updateData));
  });

  it("deleteConnection — DELETE with id", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: null }));

    await api.deleteConnection("conn-1");

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/connections/conn-1");
    expect(opts.method).toBe("DELETE");
  });
});

// =========================================================================
// 35. Settings API (extended)
// =========================================================================
describe("Settings API (extended)", () => {
  it("updateAuthSettings — PUT with JSON body", async () => {
    const authData = { maxLoginAttempts: 10, lockoutDurationMinutes: 30 };
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: authData }),
    );

    await api.updateAuthSettings(authData as any);

    const url = mockFetch.mock.calls[0][0] as string;
    const opts = mockFetch.mock.calls[0][1] as RequestInit;
    expect(url).toContain("/api/v1/settings/auth");
    expect(opts.method).toBe("PUT");
    expect(opts.body).toBe(JSON.stringify(authData));
  });

  it("getSmtpSettings — GET", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: { host: "smtp.example.com", port: 587 } }),
    );

    await api.getSmtpSettings();

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/settings/smtp");
  });
});

// =========================================================================
// 36. Dashboard API (extended)
// =========================================================================
describe("Dashboard API (extended)", () => {
  it("getActiveMonitors — GET", async () => {
    mockFetch.mockResolvedValueOnce(
      okResponse({ data: [{ id: "m-1", state: "active" }] }),
    );

    await api.getActiveMonitors();

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("/api/v1/dashboard/active-monitors");
  });

  it("getRecentExecutions — uses default count", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: [] }));

    await api.getRecentExecutions();

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("count=10");
  });

  it("getExpiringKeys — uses default daysAhead", async () => {
    mockFetch.mockResolvedValueOnce(okResponse({ data: [] }));

    await api.getExpiringKeys();

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("daysAhead=30");
  });
});

import type { APIRequestContext } from "@playwright/test";
import { TEST_ADMIN } from "../global-setup";

const API_BASE_URL = process.env.API_URL || "http://localhost:5000";

// Cache auth tokens per APIRequestContext to avoid redundant login calls.
// Tokens are short-lived (test lifetime) so staleness is not a concern.
const tokenCache = new WeakMap<APIRequestContext, string>();

export async function getAuthToken(
  request: APIRequestContext
): Promise<string> {
  const cached = tokenCache.get(request);
  if (cached) return cached;

  for (let attempt = 0; attempt < 5; attempt++) {
    const response = await request.post(`${API_BASE_URL}/api/v1/auth/login`, {
      data: {
        username: TEST_ADMIN.username,
        password: TEST_ADMIN.password,
      },
    });

    if (response.status() === 429 || !response.ok()) {
      const delay = response.status() === 429
        ? parseInt(response.headers()["retry-after"] || "2", 10) * 1000
        : 1000;
      await new Promise((r) => setTimeout(r, delay));
      continue;
    }

    try {
      const body = await response.json();
      const token = body.data.accessToken;
      tokenCache.set(request, token);
      return token;
    } catch {
      await new Promise((r) => setTimeout(r, 1000));
      continue;
    }
  }

  throw new Error(`getAuthToken failed after 5 retries`);
}

function authHeaders(token: string) {
  return {
    Authorization: `Bearer ${token}`,
    "Content-Type": "application/json",
  };
}

function uniqueName(prefix: string): string {
  return `e2e-${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
}

// ── Jobs ──

export async function createTestJob(
  request: APIRequestContext,
  overrides?: { name?: string; description?: string }
) {
  const token = await getAuthToken(request);
  const response = await request.post(`${API_BASE_URL}/api/v1/jobs`, {
    headers: authHeaders(token),
    data: {
      name: overrides?.name ?? uniqueName("job"),
      description: overrides?.description ?? "E2E test job",
    },
  });
  const body = await response.json();
  return body.data;
}

export async function deleteTestJob(
  request: APIRequestContext,
  id: string
) {
  const token = await getAuthToken(request);
  await request.delete(`${API_BASE_URL}/api/v1/jobs/${id}`, {
    headers: authHeaders(token),
  });
}

export async function addJobSteps(
  request: APIRequestContext,
  jobId: string,
  steps: Array<{
    name: string;
    typeKey: string;
    stepOrder: number;
    configuration: string;
    timeoutSeconds?: number;
    alias?: string;
  }>
) {
  const token = await getAuthToken(request);
  const response = await request.put(
    `${API_BASE_URL}/api/v1/jobs/${jobId}/steps`,
    {
      headers: authHeaders(token),
      data: {
        steps: steps.map((s) => ({
          ...s,
          timeoutSeconds: s.timeoutSeconds ?? 300,
        })),
      },
    }
  );
  const body = await response.json();
  return body.data;
}

export async function triggerJob(
  request: APIRequestContext,
  jobId: string
) {
  const token = await getAuthToken(request);
  const response = await request.post(
    `${API_BASE_URL}/api/v1/jobs/${jobId}/trigger`,
    {
      headers: authHeaders(token),
      data: { triggeredBy: "e2e-test" },
    }
  );
  const body = await response.json();
  return body.data;
}

// ── Job Schedules ──

export async function createJobSchedule(
  request: APIRequestContext,
  jobId: string,
  overrides?: {
    scheduleType?: string;
    cronExpression?: string;
    isEnabled?: boolean;
  }
) {
  const token = await getAuthToken(request);
  const response = await request.post(
    `${API_BASE_URL}/api/v1/jobs/${jobId}/schedules`,
    {
      headers: authHeaders(token),
      data: {
        scheduleType: overrides?.scheduleType ?? "cron",
        cronExpression: overrides?.cronExpression ?? "0 0 * * *",
        isEnabled: overrides?.isEnabled ?? true,
      },
    }
  );
  const body = await response.json();
  return body.data;
}

export async function deleteJobSchedule(
  request: APIRequestContext,
  jobId: string,
  scheduleId: string
) {
  const token = await getAuthToken(request);
  await request.delete(
    `${API_BASE_URL}/api/v1/jobs/${jobId}/schedules/${scheduleId}`,
    { headers: authHeaders(token) }
  );
}

// ── Connections ──

export async function createTestConnection(
  request: APIRequestContext,
  overrides?: Partial<{
    name: string;
    protocol: string;
    host: string;
    port: number;
    authMethod: string;
    username: string;
    password: string;
  }>
) {
  const token = await getAuthToken(request);
  const response = await request.post(
    `${API_BASE_URL}/api/v1/connections`,
    {
      headers: authHeaders(token),
      data: {
        name: overrides?.name ?? uniqueName("conn"),
        protocol: overrides?.protocol ?? "sftp",
        host: overrides?.host ?? "test.example.com",
        port: overrides?.port ?? 22,
        authMethod: overrides?.authMethod ?? "password",
        username: overrides?.username ?? "testuser",
        password: overrides?.password ?? "testpass",
      },
    }
  );
  const body = await response.json();
  return body.data;
}

export async function deleteTestConnection(
  request: APIRequestContext,
  id: string
) {
  const token = await getAuthToken(request);
  await request.delete(`${API_BASE_URL}/api/v1/connections/${id}`, {
    headers: authHeaders(token),
  });
}

// ── PGP Keys ──

export async function generateTestPgpKey(
  request: APIRequestContext,
  name?: string
) {
  const token = await getAuthToken(request);
  const response = await request.post(
    `${API_BASE_URL}/api/v1/pgp-keys/generate`,
    {
      headers: authHeaders(token),
      data: {
        name: name ?? uniqueName("pgp"),
        algorithm: "ecc_curve25519",
        purpose: "encryption",
        realName: "E2E Test",
        email: "e2e@test.local",
      },
    }
  );
  if (!response.ok()) {
    const text = await response.text();
    throw new Error(`generateTestPgpKey failed (${response.status()}): ${text}`);
  }
  const body = await response.json();
  return body.data;
}

export async function deleteTestPgpKey(
  request: APIRequestContext,
  id: string
) {
  const token = await getAuthToken(request);
  await request.delete(`${API_BASE_URL}/api/v1/pgp-keys/${id}`, {
    headers: authHeaders(token),
  });
}

export async function retirePgpKey(
  request: APIRequestContext,
  id: string
) {
  const token = await getAuthToken(request);
  await request.post(`${API_BASE_URL}/api/v1/pgp-keys/${id}/retire`, {
    headers: authHeaders(token),
  });
}

export async function activatePgpKey(
  request: APIRequestContext,
  id: string
) {
  const token = await getAuthToken(request);
  await request.post(`${API_BASE_URL}/api/v1/pgp-keys/${id}/activate`, {
    headers: authHeaders(token),
  });
}

// ── SSH Keys ──

export async function generateTestSshKey(
  request: APIRequestContext,
  name?: string
) {
  const token = await getAuthToken(request);
  const response = await request.post(
    `${API_BASE_URL}/api/v1/ssh-keys/generate`,
    {
      headers: authHeaders(token),
      data: {
        name: name ?? uniqueName("ssh"),
        keyType: "ed25519",
      },
    }
  );
  const body = await response.json();
  return body.data;
}

export async function deleteTestSshKey(
  request: APIRequestContext,
  id: string
) {
  const token = await getAuthToken(request);
  await request.delete(`${API_BASE_URL}/api/v1/ssh-keys/${id}`, {
    headers: authHeaders(token),
  });
}

export async function retireSshKey(
  request: APIRequestContext,
  id: string
) {
  const token = await getAuthToken(request);
  await request.post(`${API_BASE_URL}/api/v1/ssh-keys/${id}/retire`, {
    headers: authHeaders(token),
  });
}

export async function activateSshKey(
  request: APIRequestContext,
  id: string
) {
  const token = await getAuthToken(request);
  await request.post(`${API_BASE_URL}/api/v1/ssh-keys/${id}/activate`, {
    headers: authHeaders(token),
  });
}

// ── Tags ──

export async function createTestTag(
  request: APIRequestContext,
  overrides?: { name?: string; color?: string; category?: string }
) {
  const token = await getAuthToken(request);
  const response = await request.post(`${API_BASE_URL}/api/v1/tags`, {
    headers: authHeaders(token),
    data: {
      name: overrides?.name ?? uniqueName("tag"),
      color: overrides?.color ?? "#3b82f6",
      category: overrides?.category ?? "general",
    },
  });
  const body = await response.json();
  return body.data;
}

export async function deleteTestTag(
  request: APIRequestContext,
  id: string
) {
  const token = await getAuthToken(request);
  await request.delete(`${API_BASE_URL}/api/v1/tags/${id}`, {
    headers: authHeaders(token),
  });
}

// ── Chains ──

export async function createTestChain(
  request: APIRequestContext,
  overrides?: { name?: string; description?: string }
) {
  const token = await getAuthToken(request);
  const response = await request.post(`${API_BASE_URL}/api/v1/chains`, {
    headers: authHeaders(token),
    data: {
      name: overrides?.name ?? uniqueName("chain"),
      description: overrides?.description ?? "E2E test chain",
    },
  });
  const body = await response.json();
  return body.data;
}

export async function deleteTestChain(
  request: APIRequestContext,
  id: string
) {
  const token = await getAuthToken(request);
  await request.delete(`${API_BASE_URL}/api/v1/chains/${id}`, {
    headers: authHeaders(token),
  });
}

export async function setChainMembers(
  request: APIRequestContext,
  chainId: string,
  members: Array<{
    jobId: string;
    executionOrder: number;
    dependsOnMemberIndex?: number;
    runOnUpstreamFailure?: boolean;
  }>
) {
  const token = await getAuthToken(request);
  const response = await request.put(
    `${API_BASE_URL}/api/v1/chains/${chainId}/members`,
    {
      headers: authHeaders(token),
      data: { members },
    }
  );
  const body = await response.json();
  return body.data;
}

// ── Monitors ──

export async function createTestMonitor(
  request: APIRequestContext,
  overrides?: Partial<{
    name: string;
    watchTarget: string;
    triggerEvents: number;
    pollingIntervalSec: number;
    jobIds: string[];
  }>
) {
  const token = await getAuthToken(request);
  // watchTarget must be a JSON string matching the format the frontend sends
  const rawPath = overrides?.watchTarget ?? "/tmp/e2e-watch";
  const watchTarget = rawPath.startsWith("{")
    ? rawPath
    : JSON.stringify({ type: "local", path: rawPath });
  const response = await request.post(
    `${API_BASE_URL}/api/v1/monitors`,
    {
      headers: authHeaders(token),
      data: {
        name: overrides?.name ?? uniqueName("mon"),
        watchTarget,
        triggerEvents: overrides?.triggerEvents ?? 1,
        pollingIntervalSec: overrides?.pollingIntervalSec ?? 60,
        jobIds: overrides?.jobIds ?? [],
      },
    }
  );
  const body = await response.json();
  return body.data;
}

export async function deleteTestMonitor(
  request: APIRequestContext,
  id: string
) {
  const token = await getAuthToken(request);
  await request.delete(`${API_BASE_URL}/api/v1/monitors/${id}`, {
    headers: authHeaders(token),
  });
}

// ── Notification Rules ──

export async function createTestNotificationRule(
  request: APIRequestContext,
  overrides?: Partial<{
    name: string;
    entityType: string;
    eventTypes: string[];
    channel: string;
    channelConfig: Record<string, unknown>;
  }>
) {
  const token = await getAuthToken(request);
  const response = await request.post(
    `${API_BASE_URL}/api/v1/notification-rules`,
    {
      headers: authHeaders(token),
      data: {
        name: overrides?.name ?? uniqueName("notif"),
        entityType: overrides?.entityType ?? "job",
        eventTypes: overrides?.eventTypes ?? ["job_failed"],
        channel: overrides?.channel ?? "email",
        channelConfig: overrides?.channelConfig ?? {
          recipients: ["e2e@test.local"],
          subjectPrefix: "[E2E]",
        },
        isEnabled: true,
      },
    }
  );
  const body = await response.json();
  return body.data;
}

export async function deleteTestNotificationRule(
  request: APIRequestContext,
  id: string
) {
  const token = await getAuthToken(request);
  await request.delete(
    `${API_BASE_URL}/api/v1/notification-rules/${id}`,
    { headers: authHeaders(token) }
  );
}

// ── Users ──

export async function createTestUser(
  request: APIRequestContext,
  overrides?: Partial<{
    username: string;
    displayName: string;
    email: string;
    password: string;
    role: string;
  }>
) {
  const token = await getAuthToken(request);
  const password = overrides?.password ?? "E2eTestPass123!";
  const response = await request.post(`${API_BASE_URL}/api/v1/users`, {
    headers: authHeaders(token),
    data: {
      username: overrides?.username ?? uniqueName("user"),
      displayName: overrides?.displayName ?? "E2E Test User",
      email: overrides?.email ?? "e2e-user@test.local",
      password,
      confirmPassword: password,
      role: overrides?.role ?? "viewer",
    },
  });
  const body = await response.json();
  return body.data;
}

export async function deleteTestUser(
  request: APIRequestContext,
  id: string
) {
  const token = await getAuthToken(request);
  await request.delete(`${API_BASE_URL}/api/v1/users/${id}`, {
    headers: authHeaders(token),
  });
}

export async function findUserByUsername(
  request: APIRequestContext,
  username: string
) {
  const token = await getAuthToken(request);
  const response = await request.get(
    `${API_BASE_URL}/api/v1/users?search=${encodeURIComponent(username)}`,
    { headers: authHeaders(token) }
  );
  const body = await response.json();
  return body.data?.find(
    (u: { username: string }) => u.username === username
  );
}

export async function updateUser(
  request: APIRequestContext,
  userId: string,
  data: Partial<{
    displayName: string;
    email: string;
    role: string;
    isActive: boolean;
  }>
) {
  const token = await getAuthToken(request);
  const response = await request.put(
    `${API_BASE_URL}/api/v1/users/${userId}`,
    { headers: authHeaders(token), data }
  );
  const body = await response.json();
  return body.data;
}

export async function resetUserPassword(
  request: APIRequestContext,
  userId: string,
  newPassword: string
) {
  const token = await getAuthToken(request);
  return await request.post(
    `${API_BASE_URL}/api/v1/users/${userId}/reset-password`,
    {
      headers: authHeaders(token),
      data: { newPassword, confirmPassword: newPassword },
    }
  );
}

// ── Jobs (extended) ──

export async function updateTestJob(
  request: APIRequestContext,
  jobId: string,
  data: Partial<{
    name: string;
    description: string;
    isEnabled: boolean;
  }>
) {
  const token = await getAuthToken(request);
  const response = await request.put(
    `${API_BASE_URL}/api/v1/jobs/${jobId}`,
    { headers: authHeaders(token), data }
  );
  const body = await response.json();
  return body.data;
}

export async function cancelJobExecution(
  request: APIRequestContext,
  executionId: string
) {
  const token = await getAuthToken(request);
  await request.post(
    `${API_BASE_URL}/api/v1/jobs/executions/${executionId}/cancel`,
    { headers: authHeaders(token) }
  );
}

// ── Monitors (extended) ──

export async function updateTestMonitor(
  request: APIRequestContext,
  monitorId: string,
  data: Partial<{
    name: string;
    description: string;
    watchTarget: string;
    pollingIntervalSec: number;
  }>
) {
  const token = await getAuthToken(request);
  const response = await request.put(
    `${API_BASE_URL}/api/v1/monitors/${monitorId}`,
    { headers: authHeaders(token), data }
  );
  const body = await response.json();
  return body.data;
}

export async function disableMonitor(
  request: APIRequestContext,
  monitorId: string
) {
  const token = await getAuthToken(request);
  await request.post(
    `${API_BASE_URL}/api/v1/monitors/${monitorId}/disable`,
    { headers: authHeaders(token) }
  );
}

export async function acknowledgeMonitorError(
  request: APIRequestContext,
  monitorId: string
) {
  const token = await getAuthToken(request);
  await request.post(
    `${API_BASE_URL}/api/v1/monitors/${monitorId}/acknowledge-error`,
    { headers: authHeaders(token) }
  );
}

// ── Tags (extended) ──

export async function assignTagToEntity(
  request: APIRequestContext,
  tagId: string,
  entityType: string,
  entityId: string
) {
  const token = await getAuthToken(request);
  await request.post(`${API_BASE_URL}/api/v1/tags/assign`, {
    headers: authHeaders(token),
    data: {
      assignments: [{ tagId, entityType, entityId }],
    },
  });
}

export async function unassignTagFromEntity(
  request: APIRequestContext,
  tagId: string,
  entityType: string,
  entityId: string
) {
  const token = await getAuthToken(request);
  await request.post(`${API_BASE_URL}/api/v1/tags/unassign`, {
    headers: authHeaders(token),
    data: {
      assignments: [{ tagId, entityType, entityId }],
    },
  });
}

// ── Chain Schedules ──

export async function createChainSchedule(
  request: APIRequestContext,
  chainId: string,
  overrides?: {
    scheduleType?: string;
    cronExpression?: string;
    isEnabled?: boolean;
  }
) {
  const token = await getAuthToken(request);
  const response = await request.post(
    `${API_BASE_URL}/api/v1/chains/${chainId}/schedules`,
    {
      headers: authHeaders(token),
      data: {
        scheduleType: overrides?.scheduleType ?? "cron",
        cronExpression: overrides?.cronExpression ?? "0 0 * * *",
        isEnabled: overrides?.isEnabled ?? true,
      },
    }
  );
  const body = await response.json();
  return body.data;
}

export async function deleteChainSchedule(
  request: APIRequestContext,
  chainId: string,
  scheduleId: string
) {
  const token = await getAuthToken(request);
  await request.delete(
    `${API_BASE_URL}/api/v1/chains/${chainId}/schedules/${scheduleId}`,
    { headers: authHeaders(token) }
  );
}

// ── PGP Keys (extended) ──

export async function revokePgpKey(
  request: APIRequestContext,
  id: string
) {
  const token = await getAuthToken(request);
  await request.post(`${API_BASE_URL}/api/v1/pgp-keys/${id}/revoke`, {
    headers: authHeaders(token),
  });
}

// ── Connections (extended) ──

export async function testConnectionApi(
  request: APIRequestContext,
  connectionId: string
) {
  const token = await getAuthToken(request);
  const response = await request.post(
    `${API_BASE_URL}/api/v1/connections/${connectionId}/test`,
    { headers: authHeaders(token) }
  );
  const body = await response.json();
  return body.data;
}

// ── Notification Rules (extended) ──

export async function testNotificationRule(
  request: APIRequestContext,
  ruleId: string
) {
  const token = await getAuthToken(request);
  return await request.post(
    `${API_BASE_URL}/api/v1/notification-rules/${ruleId}/test`,
    { headers: authHeaders(token) }
  );
}

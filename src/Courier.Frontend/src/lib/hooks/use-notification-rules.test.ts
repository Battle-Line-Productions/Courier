import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    listNotificationRules: vi.fn(),
    getNotificationRule: vi.fn(),
    listNotificationLogs: vi.fn(),
  },
}));

import { api } from "../api";
import {
  useNotificationRules,
  useNotificationRule,
  useNotificationLogs,
} from "./use-notification-rules";

const mockedApi = api as unknown as {
  listNotificationRules: ReturnType<typeof vi.fn>;
  getNotificationRule: ReturnType<typeof vi.fn>;
  listNotificationLogs: ReturnType<typeof vi.fn>;
};

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: queryClient }, children);
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("useNotificationRules", () => {
  it("calls api.listNotificationRules with default page and pageSize", async () => {
    const mockResponse = {
      data: [{ id: "rule-1", name: "Job Failed Alert" }],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    };
    mockedApi.listNotificationRules.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useNotificationRules(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listNotificationRules).toHaveBeenCalledWith({
      page: 1,
      pageSize: 25,
    });
    expect(result.current.data).toEqual(mockResponse);
  });

  it("calls api.listNotificationRules with custom page and pageSize", async () => {
    const mockResponse = {
      data: [],
      totalCount: 0,
      page: 2,
      pageSize: 10,
    };
    mockedApi.listNotificationRules.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useNotificationRules(2, 10), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listNotificationRules).toHaveBeenCalledWith({
      page: 2,
      pageSize: 10,
    });
  });

  it("passes filters to api.listNotificationRules", async () => {
    const filters = { search: "fail", channel: "email", isEnabled: true };
    const mockResponse = {
      data: [{ id: "rule-1", name: "Job Failed Alert" }],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    };
    mockedApi.listNotificationRules.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useNotificationRules(1, 25, filters), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listNotificationRules).toHaveBeenCalledWith({
      page: 1,
      pageSize: 25,
      search: "fail",
      channel: "email",
      isEnabled: true,
    });
  });

  it("returns error state when api call fails", async () => {
    mockedApi.listNotificationRules.mockRejectedValue(new Error("Server error"));

    const { result } = renderHook(() => useNotificationRules(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toBe("Server error");
  });
});

describe("useNotificationRule", () => {
  it("calls api.getNotificationRule with the provided id", async () => {
    const mockResponse = {
      data: { id: "rule-1", name: "Job Failed Alert", channel: "email" },
    };
    mockedApi.getNotificationRule.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useNotificationRule("rule-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.getNotificationRule).toHaveBeenCalledWith("rule-1");
    expect(result.current.data).toEqual(mockResponse);
  });

  it("does not fetch when id is an empty string", async () => {
    const { result } = renderHook(() => useNotificationRule(""), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe("idle");
    expect(mockedApi.getNotificationRule).not.toHaveBeenCalled();
  });
});

describe("useNotificationLogs", () => {
  it("calls api.listNotificationLogs with default params", async () => {
    const mockResponse = {
      data: [{ id: "log-1", ruleId: "rule-1", success: true }],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    };
    mockedApi.listNotificationLogs.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useNotificationLogs(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listNotificationLogs).toHaveBeenCalledWith({
      page: 1,
      pageSize: 25,
    });
    expect(result.current.data).toEqual(mockResponse);
  });

  it("passes filters including ruleId to api.listNotificationLogs", async () => {
    const filters = { ruleId: "rule-1", success: false };
    const mockResponse = {
      data: [{ id: "log-2", ruleId: "rule-1", success: false }],
      totalCount: 1,
      page: 1,
      pageSize: 10,
    };
    mockedApi.listNotificationLogs.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useNotificationLogs(1, 10, filters), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listNotificationLogs).toHaveBeenCalledWith({
      page: 1,
      pageSize: 10,
      ruleId: "rule-1",
      success: false,
    });
  });
});

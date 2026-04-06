import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    getAuthSettings: vi.fn(),
    updateAuthSettings: vi.fn(),
    getSmtpSettings: vi.fn(),
    updateSmtpSettings: vi.fn(),
    testSmtpConnection: vi.fn(),
  },
}));

import { api } from "../api";
import {
  useAuthSettings,
  useUpdateAuthSettings,
  useSmtpSettings,
  useUpdateSmtpSettings,
  useTestSmtpConnection,
} from "./use-settings";

const mockedApi = api as unknown as {
  getAuthSettings: ReturnType<typeof vi.fn>;
  updateAuthSettings: ReturnType<typeof vi.fn>;
  getSmtpSettings: ReturnType<typeof vi.fn>;
  updateSmtpSettings: ReturnType<typeof vi.fn>;
  testSmtpConnection: ReturnType<typeof vi.fn>;
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

describe("useAuthSettings", () => {
  it("calls api.getAuthSettings and returns data on success", async () => {
    const authData = {
      data: {
        sessionTimeoutMinutes: 30,
        refreshTokenDays: 7,
        passwordMinLength: 8,
        maxLoginAttempts: 5,
        lockoutDurationMinutes: 15,
      },
      success: true,
      timestamp: new Date().toISOString(),
    };
    mockedApi.getAuthSettings.mockResolvedValue(authData);

    const { result } = renderHook(() => useAuthSettings(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.getAuthSettings).toHaveBeenCalledOnce();
    expect(result.current.data).toEqual(authData);
  });

  it("returns error state when api.getAuthSettings fails", async () => {
    mockedApi.getAuthSettings.mockRejectedValue(new Error("Network error"));

    const { result } = renderHook(() => useAuthSettings(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toBe("Network error");
  });
});

describe("useUpdateAuthSettings", () => {
  it("calls api.updateAuthSettings with provided data", async () => {
    const updateRequest = {
      sessionTimeoutMinutes: 60,
      refreshTokenDays: 14,
      passwordMinLength: 12,
      maxLoginAttempts: 3,
      lockoutDurationMinutes: 30,
    };
    const responseData = {
      data: updateRequest,
      success: true,
      timestamp: new Date().toISOString(),
    };
    mockedApi.updateAuthSettings.mockResolvedValue(responseData);

    const { result } = renderHook(() => useUpdateAuthSettings(), {
      wrapper: createWrapper(),
    });

    await act(async () => {
      result.current.mutate(updateRequest);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.updateAuthSettings).toHaveBeenCalledWith(updateRequest);
    expect(result.current.data).toEqual(responseData);
  });

  it("invalidates auth settings query on success", async () => {
    const updateRequest = {
      sessionTimeoutMinutes: 60,
      refreshTokenDays: 14,
      passwordMinLength: 12,
      maxLoginAttempts: 3,
      lockoutDurationMinutes: 30,
    };
    mockedApi.updateAuthSettings.mockResolvedValue({
      data: updateRequest,
      success: true,
      timestamp: new Date().toISOString(),
    });

    const authData = {
      data: {
        sessionTimeoutMinutes: 30,
        refreshTokenDays: 7,
        passwordMinLength: 8,
        maxLoginAttempts: 5,
        lockoutDurationMinutes: 15,
      },
      success: true,
      timestamp: new Date().toISOString(),
    };
    mockedApi.getAuthSettings.mockResolvedValue(authData);

    const wrapper = createWrapper();

    const { result: queryResult } = renderHook(() => useAuthSettings(), {
      wrapper,
    });
    await waitFor(() => expect(queryResult.current.isSuccess).toBe(true));

    const callCountAfterInitialFetch = mockedApi.getAuthSettings.mock.calls.length;

    const { result: mutationResult } = renderHook(
      () => useUpdateAuthSettings(),
      { wrapper }
    );

    await act(async () => {
      mutationResult.current.mutate(updateRequest);
    });

    await waitFor(() => expect(mutationResult.current.isSuccess).toBe(true));

    await waitFor(() =>
      expect(mockedApi.getAuthSettings.mock.calls.length).toBeGreaterThan(
        callCountAfterInitialFetch
      )
    );
  });
});

describe("useSmtpSettings", () => {
  it("calls api.getSmtpSettings and returns data on success", async () => {
    const smtpData = {
      data: {
        host: "smtp.example.com",
        port: 587,
        useSsl: true,
        username: "admin@example.com",
        fromAddress: "noreply@example.com",
        fromName: "Courier",
        isConfigured: true,
      },
      success: true,
      timestamp: new Date().toISOString(),
    };
    mockedApi.getSmtpSettings.mockResolvedValue(smtpData);

    const { result } = renderHook(() => useSmtpSettings(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.getSmtpSettings).toHaveBeenCalledOnce();
    expect(result.current.data).toEqual(smtpData);
  });

  it("returns error state when api.getSmtpSettings fails", async () => {
    mockedApi.getSmtpSettings.mockRejectedValue(new Error("Server error"));

    const { result } = renderHook(() => useSmtpSettings(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toBe("Server error");
  });
});

describe("useUpdateSmtpSettings", () => {
  it("calls api.updateSmtpSettings with provided data", async () => {
    const updateRequest = {
      host: "smtp.newhost.com",
      port: 465,
      useSsl: true,
      username: "user@newhost.com",
      password: "secret123",
      fromAddress: "noreply@newhost.com",
      fromName: "Courier MFT",
    };
    const responseData = {
      data: {
        host: "smtp.newhost.com",
        port: 465,
        useSsl: true,
        username: "user@newhost.com",
        fromAddress: "noreply@newhost.com",
        fromName: "Courier MFT",
        isConfigured: true,
      },
      success: true,
      timestamp: new Date().toISOString(),
    };
    mockedApi.updateSmtpSettings.mockResolvedValue(responseData);

    const { result } = renderHook(() => useUpdateSmtpSettings(), {
      wrapper: createWrapper(),
    });

    await act(async () => {
      result.current.mutate(updateRequest);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.updateSmtpSettings).toHaveBeenCalledWith(updateRequest);
    expect(result.current.data).toEqual(responseData);
  });

  it("invalidates smtp settings query on success", async () => {
    const updateRequest = {
      host: "smtp.newhost.com",
      port: 465,
      useSsl: true,
      username: "user@newhost.com",
      fromAddress: "noreply@newhost.com",
      fromName: "Courier MFT",
    };
    mockedApi.updateSmtpSettings.mockResolvedValue({
      data: updateRequest,
      success: true,
      timestamp: new Date().toISOString(),
    });

    const smtpData = {
      data: {
        host: "smtp.example.com",
        port: 587,
        useSsl: true,
        username: "admin@example.com",
        fromAddress: "noreply@example.com",
        fromName: "Courier",
        isConfigured: true,
      },
      success: true,
      timestamp: new Date().toISOString(),
    };
    mockedApi.getSmtpSettings.mockResolvedValue(smtpData);

    const wrapper = createWrapper();

    const { result: queryResult } = renderHook(() => useSmtpSettings(), {
      wrapper,
    });
    await waitFor(() => expect(queryResult.current.isSuccess).toBe(true));

    const callCountAfterInitialFetch = mockedApi.getSmtpSettings.mock.calls.length;

    const { result: mutationResult } = renderHook(
      () => useUpdateSmtpSettings(),
      { wrapper }
    );

    await act(async () => {
      mutationResult.current.mutate(updateRequest);
    });

    await waitFor(() => expect(mutationResult.current.isSuccess).toBe(true));

    await waitFor(() =>
      expect(mockedApi.getSmtpSettings.mock.calls.length).toBeGreaterThan(
        callCountAfterInitialFetch
      )
    );
  });
});

describe("useTestSmtpConnection", () => {
  it("calls api.testSmtpConnection and returns result on success", async () => {
    const testResult = {
      data: { success: true },
      success: true,
      timestamp: new Date().toISOString(),
    };
    mockedApi.testSmtpConnection.mockResolvedValue(testResult);

    const { result } = renderHook(() => useTestSmtpConnection(), {
      wrapper: createWrapper(),
    });

    await act(async () => {
      result.current.mutate();
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.testSmtpConnection).toHaveBeenCalledOnce();
    expect(result.current.data).toEqual(testResult);
  });

  it("returns error state when api.testSmtpConnection fails", async () => {
    mockedApi.testSmtpConnection.mockRejectedValue(
      new Error("SMTP connection failed")
    );

    const { result } = renderHook(() => useTestSmtpConnection(), {
      wrapper: createWrapper(),
    });

    await act(async () => {
      result.current.mutate();
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toBe("SMTP connection failed");
  });
});

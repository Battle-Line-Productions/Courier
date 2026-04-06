import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    listAuthProviders: vi.fn(),
    getAuthProvider: vi.fn(),
    getLoginOptions: vi.fn(),
    createAuthProvider: vi.fn(),
    updateAuthProvider: vi.fn(),
    deleteAuthProvider: vi.fn(),
    testAuthProvider: vi.fn(),
  },
}));

import { api } from "../api";
import {
  useAuthProviders,
  useAuthProvider,
  useLoginOptions,
  useCreateAuthProvider,
  useUpdateAuthProvider,
  useDeleteAuthProvider,
  useTestAuthProvider,
} from "./use-auth-providers";

const mockedApi = api as unknown as {
  listAuthProviders: ReturnType<typeof vi.fn>;
  getAuthProvider: ReturnType<typeof vi.fn>;
  getLoginOptions: ReturnType<typeof vi.fn>;
  createAuthProvider: ReturnType<typeof vi.fn>;
  updateAuthProvider: ReturnType<typeof vi.fn>;
  deleteAuthProvider: ReturnType<typeof vi.fn>;
  testAuthProvider: ReturnType<typeof vi.fn>;
};

let queryClient: QueryClient;

function createWrapper() {
  queryClient = new QueryClient({
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

describe("useAuthProviders", () => {
  it("calls api.listAuthProviders with default page and pageSize", async () => {
    const mockResponse = { data: [], totalCount: 0, page: 1, pageSize: 25 };
    mockedApi.listAuthProviders.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useAuthProviders(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listAuthProviders).toHaveBeenCalledWith(1, 25);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("calls api.listAuthProviders with custom page and pageSize", async () => {
    const mockResponse = { data: [], totalCount: 0, page: 2, pageSize: 10 };
    mockedApi.listAuthProviders.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useAuthProviders(2, 10), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listAuthProviders).toHaveBeenCalledWith(2, 10);
  });
});

describe("useAuthProvider", () => {
  it("calls api.getAuthProvider with the provided id", async () => {
    const mockResponse = { data: { id: "ap-1", name: "Entra ID" } };
    mockedApi.getAuthProvider.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useAuthProvider("ap-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.getAuthProvider).toHaveBeenCalledWith("ap-1");
    expect(result.current.data).toEqual(mockResponse);
  });

  it("does not fetch when id is an empty string", async () => {
    const { result } = renderHook(() => useAuthProvider(""), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe("idle");
    expect(mockedApi.getAuthProvider).not.toHaveBeenCalled();
  });
});

describe("useLoginOptions", () => {
  it("calls api.getLoginOptions", async () => {
    const mockResponse = { data: { localLoginEnabled: true, ssoProviders: [] } };
    mockedApi.getLoginOptions.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useLoginOptions(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.getLoginOptions).toHaveBeenCalled();
    expect(result.current.data).toEqual(mockResponse);
  });
});

describe("useCreateAuthProvider", () => {
  it("calls api.createAuthProvider and invalidates cache", async () => {
    const newProvider = { name: "New Provider", type: "oidc" };
    const mockResponse = { data: { id: "ap-2", ...newProvider } };
    mockedApi.createAuthProvider.mockResolvedValue(mockResponse);

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useCreateAuthProvider(), { wrapper });

    await act(async () => {
      result.current.mutate(newProvider as any);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.createAuthProvider).toHaveBeenCalledWith(newProvider);
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["auth-providers"] });
  });
});

describe("useUpdateAuthProvider", () => {
  it("calls api.updateAuthProvider with id and data, then invalidates cache", async () => {
    const updateData = { name: "Updated Provider" };
    const mockResponse = { data: { id: "ap-1", name: "Updated Provider" } };
    mockedApi.updateAuthProvider.mockResolvedValue(mockResponse);

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useUpdateAuthProvider(), { wrapper });

    await act(async () => {
      result.current.mutate({ id: "ap-1", data: updateData as any });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.updateAuthProvider).toHaveBeenCalledWith("ap-1", updateData);
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["auth-providers"] });
  });
});

describe("useDeleteAuthProvider", () => {
  it("calls api.deleteAuthProvider and invalidates cache", async () => {
    mockedApi.deleteAuthProvider.mockResolvedValue({ data: null });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useDeleteAuthProvider(), { wrapper });

    await act(async () => {
      result.current.mutate("ap-to-delete");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.deleteAuthProvider).toHaveBeenCalledWith("ap-to-delete");
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["auth-providers"] });
  });
});

describe("useTestAuthProvider", () => {
  it("calls api.testAuthProvider with the provider id", async () => {
    const mockResult = { data: { success: true } };
    mockedApi.testAuthProvider.mockResolvedValue(mockResult);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useTestAuthProvider(), { wrapper });

    await act(async () => {
      result.current.mutate("ap-1");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.testAuthProvider).toHaveBeenCalledWith("ap-1");
    expect(result.current.data).toEqual(mockResult);
  });
});

import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    createConnection: vi.fn(),
    updateConnection: vi.fn(),
    deleteConnection: vi.fn(),
    testConnection: vi.fn(),
  },
}));

import { api } from "../api";
import {
  useCreateConnection,
  useUpdateConnection,
  useDeleteConnection,
  useTestConnection,
} from "./use-connection-mutations";

const mockedApi = api as unknown as {
  createConnection: ReturnType<typeof vi.fn>;
  updateConnection: ReturnType<typeof vi.fn>;
  deleteConnection: ReturnType<typeof vi.fn>;
  testConnection: ReturnType<typeof vi.fn>;
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

describe("useCreateConnection", () => {
  it("calls api.createConnection and invalidates connections cache", async () => {
    const newConnection = {
      name: "New SFTP",
      protocol: "sftp",
      host: "sftp.example.com",
    };
    const mockResponse = {
      data: { id: "new-1", ...newConnection },
    };
    mockedApi.createConnection.mockResolvedValue(mockResponse);

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useCreateConnection(), { wrapper });

    await act(async () => {
      result.current.mutate(newConnection as any);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.createConnection).toHaveBeenCalledWith(newConnection);
    expect(result.current.data).toEqual(mockResponse);
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["connections"],
    });
  });

  it("sets error state when create fails", async () => {
    mockedApi.createConnection.mockRejectedValue(new Error("Validation failed"));

    const wrapper = createWrapper();
    const { result } = renderHook(() => useCreateConnection(), { wrapper });

    await act(async () => {
      result.current.mutate({ name: "", protocol: "sftp", host: "" } as any);
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toBe("Validation failed");
  });
});

describe("useUpdateConnection", () => {
  it("calls api.updateConnection with id and data, then invalidates cache", async () => {
    const updateData = {
      name: "Updated SFTP",
      protocol: "sftp",
      host: "sftp2.example.com",
    };
    const mockResponse = {
      data: { id: "conn-1", ...updateData },
    };
    mockedApi.updateConnection.mockResolvedValue(mockResponse);

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useUpdateConnection("conn-1"), {
      wrapper,
    });

    await act(async () => {
      result.current.mutate(updateData as any);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.updateConnection).toHaveBeenCalledWith("conn-1", updateData);
    expect(result.current.data).toEqual(mockResponse);
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["connections"],
    });
  });
});

describe("useDeleteConnection", () => {
  it("calls api.deleteConnection and invalidates connections cache", async () => {
    mockedApi.deleteConnection.mockResolvedValue({ data: null });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useDeleteConnection(), { wrapper });

    await act(async () => {
      result.current.mutate("conn-to-delete");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.deleteConnection).toHaveBeenCalledWith("conn-to-delete");
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["connections"],
    });
  });

  it("sets error state when delete fails", async () => {
    mockedApi.deleteConnection.mockRejectedValue(new Error("Not found"));

    const wrapper = createWrapper();
    const { result } = renderHook(() => useDeleteConnection(), { wrapper });

    await act(async () => {
      result.current.mutate("nonexistent-id");
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe("Not found");
  });
});

describe("useTestConnection", () => {
  it("calls api.testConnection with the connection id", async () => {
    const mockResult = {
      data: { success: true, message: "Connection successful" },
    };
    mockedApi.testConnection.mockResolvedValue(mockResult);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useTestConnection(), { wrapper });

    await act(async () => {
      result.current.mutate("conn-1");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.testConnection).toHaveBeenCalledWith("conn-1");
    expect(result.current.data).toEqual(mockResult);
  });

  it("does not invalidate connections cache on success", async () => {
    mockedApi.testConnection.mockResolvedValue({
      data: { success: true },
    });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useTestConnection(), { wrapper });

    await act(async () => {
      result.current.mutate("conn-1");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidateSpy).not.toHaveBeenCalled();
  });
});

import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    generateSshKey: vi.fn(),
    importSshKey: vi.fn(),
    updateSshKey: vi.fn(),
    deleteSshKey: vi.fn(),
    retireSshKey: vi.fn(),
    activateSshKey: vi.fn(),
  },
}));

import { api } from "../api";
import {
  useGenerateSshKey,
  useImportSshKey,
  useUpdateSshKey,
  useDeleteSshKey,
  useRetireSshKey,
  useActivateSshKey,
} from "./use-ssh-key-mutations";

const mockedApi = api as unknown as {
  generateSshKey: ReturnType<typeof vi.fn>;
  importSshKey: ReturnType<typeof vi.fn>;
  updateSshKey: ReturnType<typeof vi.fn>;
  deleteSshKey: ReturnType<typeof vi.fn>;
  retireSshKey: ReturnType<typeof vi.fn>;
  activateSshKey: ReturnType<typeof vi.fn>;
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

describe("useGenerateSshKey", () => {
  it("calls api.generateSshKey and invalidates ssh-keys cache", async () => {
    const generateData = {
      name: "New SSH Key",
      keyType: "ed25519",
    };
    const mockResponse = {
      data: { id: "ssh-new", ...generateData },
    };
    mockedApi.generateSshKey.mockResolvedValue(mockResponse);

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useGenerateSshKey(), { wrapper });

    await act(async () => {
      result.current.mutate(generateData as any);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.generateSshKey).toHaveBeenCalledWith(generateData);
    expect(result.current.data).toEqual(mockResponse);
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["ssh-keys"],
    });
  });

  it("sets error state when generate fails", async () => {
    mockedApi.generateSshKey.mockRejectedValue(new Error("Generation failed"));

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGenerateSshKey(), { wrapper });

    await act(async () => {
      result.current.mutate({ name: "" } as any);
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toBe("Generation failed");
  });
});

describe("useImportSshKey", () => {
  it("calls api.importSshKey with FormData and invalidates ssh-keys cache", async () => {
    const formData = new FormData();
    formData.append("file", new Blob(["ssh-key-content"]), "id_ed25519.pub");
    const mockResponse = {
      data: { id: "ssh-imported", name: "Imported Key" },
    };
    mockedApi.importSshKey.mockResolvedValue(mockResponse);

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useImportSshKey(), { wrapper });

    await act(async () => {
      result.current.mutate(formData);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.importSshKey).toHaveBeenCalledWith(formData);
    expect(result.current.data).toEqual(mockResponse);
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["ssh-keys"],
    });
  });
});

describe("useUpdateSshKey", () => {
  it("calls api.updateSshKey with id and data, then invalidates cache", async () => {
    const updateData = { name: "Updated SSH Key" };
    const mockResponse = {
      data: { id: "ssh-1", ...updateData },
    };
    mockedApi.updateSshKey.mockResolvedValue(mockResponse);

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useUpdateSshKey("ssh-1"), { wrapper });

    await act(async () => {
      result.current.mutate(updateData as any);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.updateSshKey).toHaveBeenCalledWith("ssh-1", updateData);
    expect(result.current.data).toEqual(mockResponse);
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["ssh-keys"],
    });
  });
});

describe("useDeleteSshKey", () => {
  it("calls api.deleteSshKey and invalidates ssh-keys cache", async () => {
    mockedApi.deleteSshKey.mockResolvedValue({ data: null });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useDeleteSshKey(), { wrapper });

    await act(async () => {
      result.current.mutate("ssh-to-delete");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.deleteSshKey).toHaveBeenCalledWith("ssh-to-delete");
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["ssh-keys"],
    });
  });

  it("sets error state when delete fails", async () => {
    mockedApi.deleteSshKey.mockRejectedValue(new Error("Not found"));

    const wrapper = createWrapper();
    const { result } = renderHook(() => useDeleteSshKey(), { wrapper });

    await act(async () => {
      result.current.mutate("nonexistent-id");
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe("Not found");
  });
});

describe("useRetireSshKey", () => {
  it("calls api.retireSshKey and invalidates ssh-keys cache", async () => {
    mockedApi.retireSshKey.mockResolvedValue({ data: { id: "ssh-1", status: "retired" } });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useRetireSshKey(), { wrapper });

    await act(async () => {
      result.current.mutate("ssh-1");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.retireSshKey).toHaveBeenCalledWith("ssh-1");
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["ssh-keys"],
    });
  });
});

describe("useActivateSshKey", () => {
  it("calls api.activateSshKey and invalidates ssh-keys cache", async () => {
    mockedApi.activateSshKey.mockResolvedValue({ data: { id: "ssh-1", status: "active" } });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useActivateSshKey(), { wrapper });

    await act(async () => {
      result.current.mutate("ssh-1");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.activateSshKey).toHaveBeenCalledWith("ssh-1");
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["ssh-keys"],
    });
  });
});

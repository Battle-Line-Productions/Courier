import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    generatePgpKey: vi.fn(),
    importPgpKey: vi.fn(),
    updatePgpKey: vi.fn(),
    deletePgpKey: vi.fn(),
    retirePgpKey: vi.fn(),
    revokePgpKey: vi.fn(),
    activatePgpKey: vi.fn(),
  },
}));

import { api } from "../api";
import {
  useGeneratePgpKey,
  useImportPgpKey,
  useUpdatePgpKey,
  useDeletePgpKey,
  useRetirePgpKey,
  useRevokePgpKey,
  useActivatePgpKey,
} from "./use-pgp-key-mutations";

const mockedApi = api as unknown as {
  generatePgpKey: ReturnType<typeof vi.fn>;
  importPgpKey: ReturnType<typeof vi.fn>;
  updatePgpKey: ReturnType<typeof vi.fn>;
  deletePgpKey: ReturnType<typeof vi.fn>;
  retirePgpKey: ReturnType<typeof vi.fn>;
  revokePgpKey: ReturnType<typeof vi.fn>;
  activatePgpKey: ReturnType<typeof vi.fn>;
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

describe("useGeneratePgpKey", () => {
  it("calls api.generatePgpKey and invalidates pgp-keys cache", async () => {
    const generateData = {
      name: "New PGP Key",
      algorithm: "rsa4096",
      email: "user@example.com",
    };
    const mockResponse = {
      data: { id: "pgp-new", ...generateData },
    };
    mockedApi.generatePgpKey.mockResolvedValue(mockResponse);

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useGeneratePgpKey(), { wrapper });

    await act(async () => {
      result.current.mutate(generateData as any);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.generatePgpKey).toHaveBeenCalledWith(generateData);
    expect(result.current.data).toEqual(mockResponse);
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["pgp-keys"],
    });
  });

  it("sets error state when generate fails", async () => {
    mockedApi.generatePgpKey.mockRejectedValue(new Error("Generation failed"));

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGeneratePgpKey(), { wrapper });

    await act(async () => {
      result.current.mutate({ name: "" } as any);
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toBe("Generation failed");
  });
});

describe("useImportPgpKey", () => {
  it("calls api.importPgpKey with FormData and invalidates pgp-keys cache", async () => {
    const formData = new FormData();
    formData.append("file", new Blob(["key-content"]), "key.asc");
    const mockResponse = {
      data: { id: "pgp-imported", name: "Imported Key" },
    };
    mockedApi.importPgpKey.mockResolvedValue(mockResponse);

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useImportPgpKey(), { wrapper });

    await act(async () => {
      result.current.mutate(formData);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.importPgpKey).toHaveBeenCalledWith(formData);
    expect(result.current.data).toEqual(mockResponse);
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["pgp-keys"],
    });
  });
});

describe("useUpdatePgpKey", () => {
  it("calls api.updatePgpKey with id and data, then invalidates cache", async () => {
    const updateData = { name: "Updated PGP Key" };
    const mockResponse = {
      data: { id: "pgp-1", ...updateData },
    };
    mockedApi.updatePgpKey.mockResolvedValue(mockResponse);

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useUpdatePgpKey("pgp-1"), { wrapper });

    await act(async () => {
      result.current.mutate(updateData as any);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.updatePgpKey).toHaveBeenCalledWith("pgp-1", updateData);
    expect(result.current.data).toEqual(mockResponse);
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["pgp-keys"],
    });
  });
});

describe("useDeletePgpKey", () => {
  it("calls api.deletePgpKey and invalidates pgp-keys cache", async () => {
    mockedApi.deletePgpKey.mockResolvedValue({ data: null });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useDeletePgpKey(), { wrapper });

    await act(async () => {
      result.current.mutate("pgp-to-delete");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.deletePgpKey).toHaveBeenCalledWith("pgp-to-delete");
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["pgp-keys"],
    });
  });

  it("sets error state when delete fails", async () => {
    mockedApi.deletePgpKey.mockRejectedValue(new Error("Not found"));

    const wrapper = createWrapper();
    const { result } = renderHook(() => useDeletePgpKey(), { wrapper });

    await act(async () => {
      result.current.mutate("nonexistent-id");
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe("Not found");
  });
});

describe("useRetirePgpKey", () => {
  it("calls api.retirePgpKey and invalidates pgp-keys cache", async () => {
    mockedApi.retirePgpKey.mockResolvedValue({ data: { id: "pgp-1", status: "retired" } });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useRetirePgpKey(), { wrapper });

    await act(async () => {
      result.current.mutate("pgp-1");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.retirePgpKey).toHaveBeenCalledWith("pgp-1");
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["pgp-keys"],
    });
  });
});

describe("useRevokePgpKey", () => {
  it("calls api.revokePgpKey and invalidates pgp-keys cache", async () => {
    mockedApi.revokePgpKey.mockResolvedValue({ data: { id: "pgp-1", status: "revoked" } });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useRevokePgpKey(), { wrapper });

    await act(async () => {
      result.current.mutate("pgp-1");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.revokePgpKey).toHaveBeenCalledWith("pgp-1");
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["pgp-keys"],
    });
  });
});

describe("useActivatePgpKey", () => {
  it("calls api.activatePgpKey and invalidates pgp-keys cache", async () => {
    mockedApi.activatePgpKey.mockResolvedValue({ data: { id: "pgp-1", status: "active" } });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useActivatePgpKey(), { wrapper });

    await act(async () => {
      result.current.mutate("pgp-1");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.activatePgpKey).toHaveBeenCalledWith("pgp-1");
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["pgp-keys"],
    });
  });
});

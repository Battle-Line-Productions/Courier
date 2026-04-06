import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    createTag: vi.fn(),
    updateTag: vi.fn(),
    deleteTag: vi.fn(),
    assignTags: vi.fn(),
    unassignTags: vi.fn(),
  },
}));

import { api } from "../api";
import {
  useCreateTag,
  useUpdateTag,
  useDeleteTag,
  useAssignTags,
  useUnassignTags,
} from "./use-tag-mutations";

const mockedApi = api as unknown as {
  createTag: ReturnType<typeof vi.fn>;
  updateTag: ReturnType<typeof vi.fn>;
  deleteTag: ReturnType<typeof vi.fn>;
  assignTags: ReturnType<typeof vi.fn>;
  unassignTags: ReturnType<typeof vi.fn>;
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

describe("useCreateTag", () => {
  it("calls api.createTag and invalidates tags cache", async () => {
    const newTag = { name: "Production", category: "environment", color: "#00ff00" };
    const mockResponse = { data: { id: "tag-new", ...newTag } };
    mockedApi.createTag.mockResolvedValue(mockResponse);

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useCreateTag(), { wrapper });

    await act(async () => {
      result.current.mutate(newTag as any);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.createTag).toHaveBeenCalledWith(newTag);
    expect(result.current.data).toEqual(mockResponse);
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["tags"] });
  });

  it("sets error state when create fails", async () => {
    mockedApi.createTag.mockRejectedValue(new Error("Validation failed"));

    const wrapper = createWrapper();
    const { result } = renderHook(() => useCreateTag(), { wrapper });

    await act(async () => {
      result.current.mutate({ name: "" } as any);
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toBe("Validation failed");
  });
});

describe("useUpdateTag", () => {
  it("calls api.updateTag with id and data, then invalidates cache", async () => {
    const updateData = { name: "Updated Tag", color: "#ff0000" };
    const mockResponse = { data: { id: "tag-1", ...updateData } };
    mockedApi.updateTag.mockResolvedValue(mockResponse);

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useUpdateTag("tag-1"), { wrapper });

    await act(async () => {
      result.current.mutate(updateData as any);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.updateTag).toHaveBeenCalledWith("tag-1", updateData);
    expect(result.current.data).toEqual(mockResponse);
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["tags"] });
  });
});

describe("useDeleteTag", () => {
  it("calls api.deleteTag and invalidates tags cache", async () => {
    mockedApi.deleteTag.mockResolvedValue({ data: null });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useDeleteTag(), { wrapper });

    await act(async () => {
      result.current.mutate("tag-to-delete");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.deleteTag).toHaveBeenCalledWith("tag-to-delete");
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["tags"] });
  });

  it("sets error state when delete fails", async () => {
    mockedApi.deleteTag.mockRejectedValue(new Error("Not found"));

    const wrapper = createWrapper();
    const { result } = renderHook(() => useDeleteTag(), { wrapper });

    await act(async () => {
      result.current.mutate("nonexistent-id");
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe("Not found");
  });
});

describe("useAssignTags", () => {
  it("calls api.assignTags and invalidates multiple caches", async () => {
    const assignData = {
      tagIds: ["tag-1"],
      entityIds: ["job-1"],
      entityType: "job",
    };
    mockedApi.assignTags.mockResolvedValue({ data: null });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useAssignTags(), { wrapper });

    await act(async () => {
      result.current.mutate(assignData as any);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.assignTags).toHaveBeenCalledWith(assignData);
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["tags"] });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["jobs"] });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["connections"] });
  });
});

describe("useUnassignTags", () => {
  it("calls api.unassignTags and invalidates multiple caches", async () => {
    const unassignData = {
      tagIds: ["tag-1"],
      entityIds: ["job-1"],
      entityType: "job",
    };
    mockedApi.unassignTags.mockResolvedValue({ data: null });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useUnassignTags(), { wrapper });

    await act(async () => {
      result.current.mutate(unassignData as any);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.unassignTags).toHaveBeenCalledWith(unassignData);
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["tags"] });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["jobs"] });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["connections"] });
  });
});

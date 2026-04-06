import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    listTags: vi.fn(),
    getTag: vi.fn(),
    listTagEntities: vi.fn(),
  },
}));

import { api } from "../api";
import { useTags, useTag, useAllTags, useTagEntities } from "./use-tags";

const mockedApi = api as unknown as {
  listTags: ReturnType<typeof vi.fn>;
  getTag: ReturnType<typeof vi.fn>;
  listTagEntities: ReturnType<typeof vi.fn>;
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

describe("useTags", () => {
  it("calls api.listTags with default page and pageSize", async () => {
    const mockResponse = {
      data: [{ id: "tag-1", name: "Production" }],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    };
    mockedApi.listTags.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useTags(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listTags).toHaveBeenCalledWith({ page: 1, pageSize: 25 });
    expect(result.current.data).toEqual(mockResponse);
  });

  it("calls api.listTags with custom page and pageSize", async () => {
    const mockResponse = {
      data: [],
      totalCount: 0,
      page: 3,
      pageSize: 10,
    };
    mockedApi.listTags.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useTags(3, 10), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listTags).toHaveBeenCalledWith({ page: 3, pageSize: 10 });
  });

  it("passes filters to api.listTags", async () => {
    const filters = { search: "prod", category: "environment" };
    const mockResponse = {
      data: [{ id: "tag-1", name: "Production" }],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    };
    mockedApi.listTags.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useTags(1, 25, filters), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listTags).toHaveBeenCalledWith({
      page: 1,
      pageSize: 25,
      search: "prod",
      category: "environment",
    });
  });

  it("returns error state when api call fails", async () => {
    mockedApi.listTags.mockRejectedValue(new Error("Network error"));

    const { result } = renderHook(() => useTags(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toBe("Network error");
  });
});

describe("useTag", () => {
  it("calls api.getTag with the provided id", async () => {
    const mockResponse = {
      data: { id: "tag-1", name: "Production", category: "environment" },
    };
    mockedApi.getTag.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useTag("tag-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.getTag).toHaveBeenCalledWith("tag-1");
    expect(result.current.data).toEqual(mockResponse);
  });

  it("does not fetch when id is an empty string", async () => {
    const { result } = renderHook(() => useTag(""), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe("idle");
    expect(mockedApi.getTag).not.toHaveBeenCalled();
  });
});

describe("useAllTags", () => {
  it("calls api.listTags with pageSize 100", async () => {
    const mockResponse = {
      data: [
        { id: "tag-1", name: "Production" },
        { id: "tag-2", name: "Staging" },
      ],
      totalCount: 2,
      page: 1,
      pageSize: 100,
    };
    mockedApi.listTags.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useAllTags(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listTags).toHaveBeenCalledWith({ pageSize: 100 });
    expect(result.current.data).toEqual(mockResponse);
  });
});

describe("useTagEntities", () => {
  it("calls api.listTagEntities with tag id and default params", async () => {
    const mockResponse = {
      data: [{ entityId: "job-1", entityType: "job" }],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    };
    mockedApi.listTagEntities.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useTagEntities("tag-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listTagEntities).toHaveBeenCalledWith("tag-1", {
      entityType: undefined,
      page: 1,
      pageSize: 25,
    });
    expect(result.current.data).toEqual(mockResponse);
  });

  it("does not fetch when id is an empty string", async () => {
    const { result } = renderHook(() => useTagEntities(""), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe("idle");
    expect(mockedApi.listTagEntities).not.toHaveBeenCalled();
  });
});

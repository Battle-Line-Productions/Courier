import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    browseFilesystem: vi.fn(),
  },
}));

import { api } from "../api";
import { useFilesystemBrowse } from "./use-filesystem";

const mockedApi = api as unknown as {
  browseFilesystem: ReturnType<typeof vi.fn>;
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

describe("useFilesystemBrowse", () => {
  it("calls api.browseFilesystem with the provided path", async () => {
    const mockResponse = {
      data: {
        path: "/data/files",
        entries: [
          { name: "report.csv", type: "file", size: 1024 },
          { name: "archive", type: "directory", size: 0 },
        ],
      },
    };
    mockedApi.browseFilesystem.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useFilesystemBrowse("/data/files"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.browseFilesystem).toHaveBeenCalledWith("/data/files");
    expect(result.current.data).toEqual(mockResponse);
  });

  it("calls api.browseFilesystem with undefined when no path provided", async () => {
    const mockResponse = {
      data: { path: "/", entries: [] },
    };
    mockedApi.browseFilesystem.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useFilesystemBrowse(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.browseFilesystem).toHaveBeenCalledWith(undefined);
  });

  it("returns error state when api call fails", async () => {
    mockedApi.browseFilesystem.mockRejectedValue(new Error("Permission denied"));

    const { result } = renderHook(() => useFilesystemBrowse("/restricted"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe("Permission denied");
  });
});

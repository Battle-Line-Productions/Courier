import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";
import type { ApiResponse, StepTypeMetadataDto } from "../types";

vi.mock("../api", () => ({
  api: {
    listStepTypes: vi.fn(),
  },
}));

import { api } from "../api";
import { useStepTypes } from "./use-step-types";

const mockedApi = api as {
  listStepTypes: ReturnType<typeof vi.fn>;
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

function createWrapperWithClient() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: queryClient }, children);
  return { wrapper, queryClient };
}

const fakeStepTypes: StepTypeMetadataDto[] = [
  {
    typeKey: "file.copy",
    displayName: "Copy File",
    category: "file",
    description: "Copies a file from source to destination",
    inputs: [
      {
        key: "source_path",
        description: "Source file path",
        required: true,
        supportsContextRef: true,
      },
      {
        key: "destination_path",
        description: "Destination file path",
        required: true,
        supportsContextRef: false,
      },
    ],
    outputs: [
      {
        key: "copied_file",
        description: "Path of the copied file",
        valueType: "string",
        conditional: false,
      },
    ],
  },
  {
    typeKey: "sftp.upload",
    displayName: "SFTP Upload",
    category: "transfer",
    description: "Uploads a file via SFTP",
    inputs: [
      {
        key: "local_path",
        description: "Local file path",
        required: true,
        supportsContextRef: true,
      },
    ],
  },
  {
    typeKey: "pgp.encrypt",
    displayName: "PGP Encrypt",
    category: "crypto",
    description: "Encrypts a file using PGP",
  },
];

const fakeResponse: ApiResponse<StepTypeMetadataDto[]> = {
  data: fakeStepTypes,
  timestamp: "2026-01-01T00:00:00Z",
  success: true,
};

beforeEach(() => {
  vi.clearAllMocks();
});

describe("useStepTypes", () => {
  it("calls api.listStepTypes", async () => {
    mockedApi.listStepTypes.mockResolvedValue(fakeResponse);
    const { result } = renderHook(() => useStepTypes(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockedApi.listStepTypes).toHaveBeenCalledOnce();
  });

  it("returns step type metadata on success", async () => {
    mockedApi.listStepTypes.mockResolvedValue(fakeResponse);
    const { result } = renderHook(() => useStepTypes(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.data).toHaveLength(3);
    expect(result.current.data?.data?.[0].typeKey).toBe("file.copy");
    expect(result.current.data?.data?.[1].typeKey).toBe("sftp.upload");
    expect(result.current.data?.data?.[2].typeKey).toBe("pgp.encrypt");
  });

  it("has query key ['step-types']", async () => {
    mockedApi.listStepTypes.mockResolvedValue(fakeResponse);
    const { queryClient, wrapper } = createWrapperWithClient();

    const { result } = renderHook(() => useStepTypes(), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const cache = queryClient.getQueryCache().findAll();
    expect(cache[0]?.queryKey).toEqual(["step-types"]);
  });

  it("sets staleTime to Infinity (never automatically refetches)", async () => {
    mockedApi.listStepTypes.mockResolvedValue(fakeResponse);
    const { queryClient, wrapper } = createWrapperWithClient();

    const { result } = renderHook(() => useStepTypes(), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    // Call the hook a second time - should not re-fetch because staleTime is Infinity
    const callCountAfterFirst = mockedApi.listStepTypes.mock.calls.length;

    renderHook(() => useStepTypes(), { wrapper });

    // Even after rendering a second hook, should not have made another API call
    // because the data is never considered stale
    expect(mockedApi.listStepTypes).toHaveBeenCalledTimes(callCountAfterFirst);
  });

  it("returns error state when api rejects", async () => {
    mockedApi.listStepTypes.mockRejectedValue(new Error("Server error"));
    const { result } = renderHook(() => useStepTypes(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeInstanceOf(Error);
  });

  it("includes inputs and outputs metadata in response", async () => {
    mockedApi.listStepTypes.mockResolvedValue(fakeResponse);
    const { result } = renderHook(() => useStepTypes(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    const fileCopy = result.current.data?.data?.[0];
    expect(fileCopy?.inputs).toHaveLength(2);
    expect(fileCopy?.inputs?.[0].key).toBe("source_path");
    expect(fileCopy?.inputs?.[0].required).toBe(true);
    expect(fileCopy?.inputs?.[0].supportsContextRef).toBe(true);
    expect(fileCopy?.outputs).toHaveLength(1);
    expect(fileCopy?.outputs?.[0].key).toBe("copied_file");
  });

  it("handles step types with no inputs or outputs", async () => {
    mockedApi.listStepTypes.mockResolvedValue(fakeResponse);
    const { result } = renderHook(() => useStepTypes(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    const pgpEncrypt = result.current.data?.data?.[2];
    expect(pgpEncrypt?.typeKey).toBe("pgp.encrypt");
    expect(pgpEncrypt?.inputs).toBeUndefined();
    expect(pgpEncrypt?.outputs).toBeUndefined();
  });
});

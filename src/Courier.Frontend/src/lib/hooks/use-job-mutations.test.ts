import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";
import type {
  ApiResponse,
  JobDto,
  JobStepDto,
  JobExecutionDto,
  CreateJobRequest,
  UpdateJobRequest,
  ReplaceJobStepsRequest,
} from "../types";

vi.mock("../api", () => ({
  api: {
    createJob: vi.fn(),
    updateJob: vi.fn(),
    deleteJob: vi.fn(),
    replaceSteps: vi.fn(),
    triggerJob: vi.fn(),
    pauseExecution: vi.fn(),
    resumeExecution: vi.fn(),
    cancelExecution: vi.fn(),
  },
}));

import { api } from "../api";
import {
  useCreateJob,
  useUpdateJob,
  useDeleteJob,
  useReplaceSteps,
  useTriggerJob,
  usePauseExecution,
  useResumeExecution,
  useCancelExecution,
} from "./use-job-mutations";

const mockedApi = api as {
  createJob: ReturnType<typeof vi.fn>;
  updateJob: ReturnType<typeof vi.fn>;
  deleteJob: ReturnType<typeof vi.fn>;
  replaceSteps: ReturnType<typeof vi.fn>;
  triggerJob: ReturnType<typeof vi.fn>;
  pauseExecution: ReturnType<typeof vi.fn>;
  resumeExecution: ReturnType<typeof vi.fn>;
  cancelExecution: ReturnType<typeof vi.fn>;
};

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

const fakeJob: JobDto = {
  id: "job-1",
  name: "Test Job",
  description: "A test job",
  currentVersion: 1,
  isEnabled: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

const fakeJobResponse: ApiResponse<JobDto> = {
  data: fakeJob,
  timestamp: "2026-01-01T00:00:00Z",
  success: true,
};

const fakeStepsResponse: ApiResponse<JobStepDto[]> = {
  data: [
    {
      id: "step-1",
      jobId: "job-1",
      stepOrder: 1,
      name: "Copy File",
      typeKey: "file.copy",
      configuration: "{}",
      timeoutSeconds: 300,
    },
  ],
  timestamp: "2026-01-01T00:00:00Z",
  success: true,
};

const fakeExecution: JobExecutionDto = {
  id: "exec-1",
  jobId: "job-1",
  state: "queued",
  triggeredBy: "ui",
  createdAt: "2026-01-01T00:00:00Z",
};

const fakeExecutionResponse: ApiResponse<JobExecutionDto> = {
  data: fakeExecution,
  timestamp: "2026-01-01T00:00:00Z",
  success: true,
};

const fakeVoidResponse: ApiResponse<void> = {
  timestamp: "2026-01-01T00:00:00Z",
  success: true,
};

beforeEach(() => {
  vi.clearAllMocks();
});

describe("useCreateJob", () => {
  it("calls api.createJob with the provided data", async () => {
    mockedApi.createJob.mockResolvedValue(fakeJobResponse);
    const { wrapper, queryClient } = createWrapperWithClient();
    const { result } = renderHook(() => useCreateJob(), { wrapper });

    const request: CreateJobRequest = { name: "New Job", description: "desc" };
    await act(() => result.current.mutateAsync(request));

    expect(mockedApi.createJob).toHaveBeenCalledWith(request);
  });

  it("returns the created job on success", async () => {
    mockedApi.createJob.mockResolvedValue(fakeJobResponse);
    const { wrapper } = createWrapperWithClient();
    const { result } = renderHook(() => useCreateJob(), { wrapper });

    let response: ApiResponse<JobDto> | undefined;
    await act(async () => {
      response = await result.current.mutateAsync({ name: "New Job" });
    });

    expect(response?.data?.name).toBe("Test Job");
  });

  it("invalidates ['jobs'] queries on success", async () => {
    mockedApi.createJob.mockResolvedValue(fakeJobResponse);
    const { wrapper, queryClient } = createWrapperWithClient();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useCreateJob(), { wrapper });

    await act(() => result.current.mutateAsync({ name: "New Job" }));

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["jobs"] });
  });
});

describe("useUpdateJob", () => {
  it("calls api.updateJob with the id and data", async () => {
    mockedApi.updateJob.mockResolvedValue(fakeJobResponse);
    const { wrapper } = createWrapperWithClient();
    const { result } = renderHook(() => useUpdateJob("job-1"), { wrapper });

    const request: UpdateJobRequest = { name: "Updated", description: "new desc" };
    await act(() => result.current.mutateAsync(request));

    expect(mockedApi.updateJob).toHaveBeenCalledWith("job-1", request);
  });

  it("invalidates ['jobs'] queries on success", async () => {
    mockedApi.updateJob.mockResolvedValue(fakeJobResponse);
    const { wrapper, queryClient } = createWrapperWithClient();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useUpdateJob("job-1"), { wrapper });

    await act(() => result.current.mutateAsync({ name: "Updated" }));

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["jobs"] });
  });
});

describe("useDeleteJob", () => {
  it("calls api.deleteJob with the provided id", async () => {
    mockedApi.deleteJob.mockResolvedValue(fakeVoidResponse);
    const { wrapper } = createWrapperWithClient();
    const { result } = renderHook(() => useDeleteJob(), { wrapper });

    await act(() => result.current.mutateAsync("job-1"));

    expect(mockedApi.deleteJob).toHaveBeenCalledWith("job-1");
  });

  it("invalidates ['jobs'] queries on success", async () => {
    mockedApi.deleteJob.mockResolvedValue(fakeVoidResponse);
    const { wrapper, queryClient } = createWrapperWithClient();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useDeleteJob(), { wrapper });

    await act(() => result.current.mutateAsync("job-1"));

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["jobs"] });
  });
});

describe("useReplaceSteps", () => {
  it("calls api.replaceSteps with jobId and data", async () => {
    mockedApi.replaceSteps.mockResolvedValue(fakeStepsResponse);
    const { wrapper } = createWrapperWithClient();
    const { result } = renderHook(() => useReplaceSteps("job-1"), { wrapper });

    const request: ReplaceJobStepsRequest = {
      steps: [
        {
          name: "Copy",
          typeKey: "file.copy",
          stepOrder: 1,
          configuration: "{}",
          timeoutSeconds: 300,
        },
      ],
    };
    await act(() => result.current.mutateAsync(request));

    expect(mockedApi.replaceSteps).toHaveBeenCalledWith("job-1", request);
  });

  it("invalidates ['jobs', jobId, 'steps'] on success", async () => {
    mockedApi.replaceSteps.mockResolvedValue(fakeStepsResponse);
    const { wrapper, queryClient } = createWrapperWithClient();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useReplaceSteps("job-1"), { wrapper });

    await act(() =>
      result.current.mutateAsync({ steps: [] })
    );

    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["jobs", "job-1", "steps"],
    });
  });
});

describe("useTriggerJob", () => {
  it("calls api.triggerJob with the jobId", async () => {
    mockedApi.triggerJob.mockResolvedValue(fakeExecutionResponse);
    const { wrapper } = createWrapperWithClient();
    const { result } = renderHook(() => useTriggerJob("job-1"), { wrapper });

    await act(() => result.current.mutateAsync());

    expect(mockedApi.triggerJob).toHaveBeenCalledWith("job-1");
  });

  it("invalidates ['jobs', jobId, 'executions'] on success", async () => {
    mockedApi.triggerJob.mockResolvedValue(fakeExecutionResponse);
    const { wrapper, queryClient } = createWrapperWithClient();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useTriggerJob("job-1"), { wrapper });

    await act(() => result.current.mutateAsync());

    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["jobs", "job-1", "executions"],
    });
  });
});

describe("usePauseExecution", () => {
  it("calls api.pauseExecution with the executionId", async () => {
    mockedApi.pauseExecution.mockResolvedValue(fakeExecutionResponse);
    const { wrapper } = createWrapperWithClient();
    const { result } = renderHook(() => usePauseExecution(), { wrapper });

    await act(() => result.current.mutateAsync("exec-1"));

    expect(mockedApi.pauseExecution).toHaveBeenCalledWith("exec-1");
  });

  it("invalidates both ['executions'] and ['jobs'] on success", async () => {
    mockedApi.pauseExecution.mockResolvedValue(fakeExecutionResponse);
    const { wrapper, queryClient } = createWrapperWithClient();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => usePauseExecution(), { wrapper });

    await act(() => result.current.mutateAsync("exec-1"));

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["executions"] });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["jobs"] });
  });
});

describe("useResumeExecution", () => {
  it("calls api.resumeExecution with the executionId", async () => {
    mockedApi.resumeExecution.mockResolvedValue(fakeExecutionResponse);
    const { wrapper } = createWrapperWithClient();
    const { result } = renderHook(() => useResumeExecution(), { wrapper });

    await act(() => result.current.mutateAsync("exec-1"));

    expect(mockedApi.resumeExecution).toHaveBeenCalledWith("exec-1");
  });

  it("invalidates both ['executions'] and ['jobs'] on success", async () => {
    mockedApi.resumeExecution.mockResolvedValue(fakeExecutionResponse);
    const { wrapper, queryClient } = createWrapperWithClient();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useResumeExecution(), { wrapper });

    await act(() => result.current.mutateAsync("exec-1"));

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["executions"] });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["jobs"] });
  });
});

describe("useCancelExecution", () => {
  it("calls api.cancelExecution with executionId and reason", async () => {
    mockedApi.cancelExecution.mockResolvedValue(fakeExecutionResponse);
    const { wrapper } = createWrapperWithClient();
    const { result } = renderHook(() => useCancelExecution(), { wrapper });

    await act(() =>
      result.current.mutateAsync({
        executionId: "exec-1",
        reason: "User cancelled",
      })
    );

    expect(mockedApi.cancelExecution).toHaveBeenCalledWith("exec-1", "User cancelled");
  });

  it("calls api.cancelExecution without reason when omitted", async () => {
    mockedApi.cancelExecution.mockResolvedValue(fakeExecutionResponse);
    const { wrapper } = createWrapperWithClient();
    const { result } = renderHook(() => useCancelExecution(), { wrapper });

    await act(() =>
      result.current.mutateAsync({ executionId: "exec-1" })
    );

    expect(mockedApi.cancelExecution).toHaveBeenCalledWith("exec-1", undefined);
  });

  it("invalidates both ['executions'] and ['jobs'] on success", async () => {
    mockedApi.cancelExecution.mockResolvedValue(fakeExecutionResponse);
    const { wrapper, queryClient } = createWrapperWithClient();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useCancelExecution(), { wrapper });

    await act(() =>
      result.current.mutateAsync({ executionId: "exec-1" })
    );

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["executions"] });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["jobs"] });
  });
});

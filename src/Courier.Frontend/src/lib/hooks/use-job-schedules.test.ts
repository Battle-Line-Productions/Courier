import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";
import type {
  ApiResponse,
  JobScheduleDto,
  CreateJobScheduleRequest,
  UpdateJobScheduleRequest,
} from "../types";

vi.mock("../api", () => ({
  api: {
    listSchedules: vi.fn(),
    createSchedule: vi.fn(),
    updateSchedule: vi.fn(),
    deleteSchedule: vi.fn(),
  },
}));

import { api } from "../api";
import {
  useJobSchedules,
  useCreateSchedule,
  useUpdateSchedule,
  useDeleteSchedule,
} from "./use-job-schedules";

const mockedApi = api as {
  listSchedules: ReturnType<typeof vi.fn>;
  createSchedule: ReturnType<typeof vi.fn>;
  updateSchedule: ReturnType<typeof vi.fn>;
  deleteSchedule: ReturnType<typeof vi.fn>;
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

const fakeSchedule: JobScheduleDto = {
  id: "sched-1",
  jobId: "job-1",
  scheduleType: "cron",
  cronExpression: "0 0 * * *",
  isEnabled: true,
  nextFireAt: "2026-01-02T00:00:00Z",
};

const fakeScheduleListResponse: ApiResponse<JobScheduleDto[]> = {
  data: [fakeSchedule],
  timestamp: "2026-01-01T00:00:00Z",
  success: true,
};

const fakeScheduleResponse: ApiResponse<JobScheduleDto> = {
  data: fakeSchedule,
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

describe("useJobSchedules", () => {
  it("calls api.listSchedules with the jobId", async () => {
    mockedApi.listSchedules.mockResolvedValue(fakeScheduleListResponse);
    const { result } = renderHook(() => useJobSchedules("job-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockedApi.listSchedules).toHaveBeenCalledWith("job-1");
  });

  it("returns schedule list on success", async () => {
    mockedApi.listSchedules.mockResolvedValue(fakeScheduleListResponse);
    const { result } = renderHook(() => useJobSchedules("job-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.data).toHaveLength(1);
    expect(result.current.data?.data?.[0].cronExpression).toBe("0 0 * * *");
  });

  it("does not fetch when jobId is empty string", async () => {
    mockedApi.listSchedules.mockResolvedValue(fakeScheduleListResponse);
    const { result } = renderHook(() => useJobSchedules(""), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe("idle");
    expect(mockedApi.listSchedules).not.toHaveBeenCalled();
  });

  it("has query key ['jobs', jobId, 'schedules']", async () => {
    mockedApi.listSchedules.mockResolvedValue(fakeScheduleListResponse);
    const { queryClient, wrapper } = createWrapperWithClient();

    const { result } = renderHook(() => useJobSchedules("job-1"), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const cache = queryClient.getQueryCache().findAll();
    expect(cache[0]?.queryKey).toEqual(["jobs", "job-1", "schedules"]);
  });

  it("returns error state when api rejects", async () => {
    mockedApi.listSchedules.mockRejectedValue(new Error("fail"));
    const { result } = renderHook(() => useJobSchedules("job-1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useCreateSchedule", () => {
  it("calls api.createSchedule with jobId and data", async () => {
    mockedApi.createSchedule.mockResolvedValue(fakeScheduleResponse);
    const { wrapper } = createWrapperWithClient();
    const { result } = renderHook(() => useCreateSchedule("job-1"), { wrapper });

    const request: CreateJobScheduleRequest = {
      scheduleType: "cron",
      cronExpression: "0 */6 * * *",
      isEnabled: true,
    };
    await act(() => result.current.mutateAsync(request));

    expect(mockedApi.createSchedule).toHaveBeenCalledWith("job-1", request);
  });

  it("returns the created schedule on success", async () => {
    mockedApi.createSchedule.mockResolvedValue(fakeScheduleResponse);
    const { wrapper } = createWrapperWithClient();
    const { result } = renderHook(() => useCreateSchedule("job-1"), { wrapper });

    let response: ApiResponse<JobScheduleDto> | undefined;
    await act(async () => {
      response = await result.current.mutateAsync({
        scheduleType: "cron",
        cronExpression: "0 0 * * *",
        isEnabled: true,
      });
    });

    expect(response?.data?.scheduleType).toBe("cron");
  });

  it("invalidates ['jobs', jobId, 'schedules'] on success", async () => {
    mockedApi.createSchedule.mockResolvedValue(fakeScheduleResponse);
    const { wrapper, queryClient } = createWrapperWithClient();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useCreateSchedule("job-1"), { wrapper });

    await act(() =>
      result.current.mutateAsync({
        scheduleType: "cron",
        cronExpression: "0 0 * * *",
        isEnabled: true,
      })
    );

    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["jobs", "job-1", "schedules"],
    });
  });
});

describe("useUpdateSchedule", () => {
  it("calls api.updateSchedule with jobId, scheduleId, and data", async () => {
    mockedApi.updateSchedule.mockResolvedValue(fakeScheduleResponse);
    const { wrapper } = createWrapperWithClient();
    const { result } = renderHook(() => useUpdateSchedule("job-1"), { wrapper });

    const data: UpdateJobScheduleRequest = {
      cronExpression: "0 12 * * *",
      isEnabled: false,
    };
    await act(() =>
      result.current.mutateAsync({ scheduleId: "sched-1", data })
    );

    expect(mockedApi.updateSchedule).toHaveBeenCalledWith("job-1", "sched-1", data);
  });

  it("invalidates ['jobs', jobId, 'schedules'] on success", async () => {
    mockedApi.updateSchedule.mockResolvedValue(fakeScheduleResponse);
    const { wrapper, queryClient } = createWrapperWithClient();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useUpdateSchedule("job-1"), { wrapper });

    await act(() =>
      result.current.mutateAsync({
        scheduleId: "sched-1",
        data: { isEnabled: false },
      })
    );

    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["jobs", "job-1", "schedules"],
    });
  });
});

describe("useDeleteSchedule", () => {
  it("calls api.deleteSchedule with jobId and scheduleId", async () => {
    mockedApi.deleteSchedule.mockResolvedValue(fakeVoidResponse);
    const { wrapper } = createWrapperWithClient();
    const { result } = renderHook(() => useDeleteSchedule("job-1"), { wrapper });

    await act(() => result.current.mutateAsync("sched-1"));

    expect(mockedApi.deleteSchedule).toHaveBeenCalledWith("job-1", "sched-1");
  });

  it("invalidates ['jobs', jobId, 'schedules'] on success", async () => {
    mockedApi.deleteSchedule.mockResolvedValue(fakeVoidResponse);
    const { wrapper, queryClient } = createWrapperWithClient();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useDeleteSchedule("job-1"), { wrapper });

    await act(() => result.current.mutateAsync("sched-1"));

    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ["jobs", "job-1", "schedules"],
    });
  });
});

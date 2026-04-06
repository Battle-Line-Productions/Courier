import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";

vi.mock("../api", () => ({
  api: {
    listUsers: vi.fn(),
    getUser: vi.fn(),
    createUser: vi.fn(),
    updateUser: vi.fn(),
    deleteUser: vi.fn(),
    resetUserPassword: vi.fn(),
  },
}));

import { api } from "../api";
import {
  useUsers,
  useUser,
  useCreateUser,
  useUpdateUser,
  useDeleteUser,
  useResetUserPassword,
} from "./use-users";

const mockedApi = api as unknown as {
  listUsers: ReturnType<typeof vi.fn>;
  getUser: ReturnType<typeof vi.fn>;
  createUser: ReturnType<typeof vi.fn>;
  updateUser: ReturnType<typeof vi.fn>;
  deleteUser: ReturnType<typeof vi.fn>;
  resetUserPassword: ReturnType<typeof vi.fn>;
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

describe("useUsers", () => {
  it("calls api.listUsers with default page and pageSize", async () => {
    const mockResponse = { data: [], totalCount: 0, page: 1, pageSize: 10 };
    mockedApi.listUsers.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useUsers(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listUsers).toHaveBeenCalledWith(1, 10, undefined);
    expect(result.current.data).toEqual(mockResponse);
  });

  it("passes search parameter to api.listUsers", async () => {
    const mockResponse = {
      data: [{ id: "u1", username: "admin" }],
      totalCount: 1,
      page: 1,
      pageSize: 10,
    };
    mockedApi.listUsers.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useUsers(1, 10, "admin"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.listUsers).toHaveBeenCalledWith(1, 10, "admin");
  });

  it("returns error state when api call fails", async () => {
    mockedApi.listUsers.mockRejectedValue(new Error("Network error"));

    const { result } = renderHook(() => useUsers(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe("Network error");
  });
});

describe("useUser", () => {
  it("calls api.getUser with the provided id", async () => {
    const mockResponse = { data: { id: "u1", username: "admin" } };
    mockedApi.getUser.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useUser("u1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.getUser).toHaveBeenCalledWith("u1");
    expect(result.current.data).toEqual(mockResponse);
  });

  it("does not fetch when id is an empty string", async () => {
    const { result } = renderHook(() => useUser(""), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe("idle");
    expect(mockedApi.getUser).not.toHaveBeenCalled();
  });
});

describe("useCreateUser", () => {
  it("calls api.createUser and invalidates users cache", async () => {
    const newUser = { username: "newuser", password: "pass123", role: "viewer" };
    const mockResponse = { data: { id: "u2", ...newUser } };
    mockedApi.createUser.mockResolvedValue(mockResponse);

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useCreateUser(), { wrapper });

    await act(async () => {
      result.current.mutate(newUser as any);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.createUser).toHaveBeenCalledWith(newUser);
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["users"] });
  });
});

describe("useUpdateUser", () => {
  it("calls api.updateUser and invalidates both list and detail caches", async () => {
    const updateData = { displayName: "Updated Name" };
    const mockResponse = { data: { id: "u1", displayName: "Updated Name" } };
    mockedApi.updateUser.mockResolvedValue(mockResponse);

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useUpdateUser("u1"), { wrapper });

    await act(async () => {
      result.current.mutate(updateData as any);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.updateUser).toHaveBeenCalledWith("u1", updateData);
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["users"] });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["users", "u1"] });
  });
});

describe("useDeleteUser", () => {
  it("calls api.deleteUser and invalidates users cache", async () => {
    mockedApi.deleteUser.mockResolvedValue({ data: null });

    const wrapper = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    const { result } = renderHook(() => useDeleteUser(), { wrapper });

    await act(async () => {
      result.current.mutate("u-to-delete");
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.deleteUser).toHaveBeenCalledWith("u-to-delete");
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["users"] });
  });
});

describe("useResetUserPassword", () => {
  it("calls api.resetUserPassword with id and data", async () => {
    const passwordData = { newPassword: "newpass123" };
    mockedApi.resetUserPassword.mockResolvedValue({ data: null });

    const wrapper = createWrapper();
    const { result } = renderHook(() => useResetUserPassword("u1"), { wrapper });

    await act(async () => {
      result.current.mutate(passwordData as any);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockedApi.resetUserPassword).toHaveBeenCalledWith("u1", passwordData);
  });
});

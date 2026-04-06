import { renderHook } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";

vi.mock("@/lib/auth", () => ({
  useAuth: vi.fn(),
}));

import { useAuth } from "@/lib/auth";
import { usePermissions } from "./use-permissions";

const mockedUseAuth = vi.mocked(useAuth);

function mockUser(role: string) {
  mockedUseAuth.mockReturnValue({
    user: {
      role,
      id: "1",
      username: role,
      displayName: role.charAt(0).toUpperCase() + role.slice(1),
      isSsoUser: false,
      allowLocalPassword: true,
    },
    isAuthenticated: true,
    isLoading: false,
    login: vi.fn(),
    loginWithTokens: vi.fn(),
    logout: vi.fn(),
  } as any);
}

function mockNoUser() {
  mockedUseAuth.mockReturnValue({
    user: null,
    isAuthenticated: false,
    isLoading: false,
    login: vi.fn(),
    loginWithTokens: vi.fn(),
    logout: vi.fn(),
  } as any);
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("usePermissions", () => {
  describe("admin role", () => {
    it("can() returns true for all permissions", () => {
      mockUser("admin");

      const { result } = renderHook(() => usePermissions());

      expect(result.current.can("JobsView")).toBe(true);
      expect(result.current.can("UsersManage")).toBe(true);
      expect(result.current.can("SettingsManage")).toBe(true);
      expect(result.current.can("AuthProvidersDelete")).toBe(true);
    });

    it("role returns 'admin'", () => {
      mockUser("admin");

      const { result } = renderHook(() => usePermissions());

      expect(result.current.role).toBe("admin");
    });
  });

  describe("viewer role", () => {
    it("can() returns true for view permissions", () => {
      mockUser("viewer");

      const { result } = renderHook(() => usePermissions());

      expect(result.current.can("JobsView")).toBe(true);
      expect(result.current.can("ConnectionsView")).toBe(true);
      expect(result.current.can("DashboardView")).toBe(true);
    });

    it("can() returns false for write permissions", () => {
      mockUser("viewer");

      const { result } = renderHook(() => usePermissions());

      expect(result.current.can("JobsCreate")).toBe(false);
      expect(result.current.can("UsersManage")).toBe(false);
      expect(result.current.can("SettingsManage")).toBe(false);
      expect(result.current.can("ConnectionsCreate")).toBe(false);
    });
  });

  describe("operator role", () => {
    it("can() returns true for job management permissions", () => {
      mockUser("operator");

      const { result } = renderHook(() => usePermissions());

      expect(result.current.can("JobsView")).toBe(true);
      expect(result.current.can("JobsCreate")).toBe(true);
      expect(result.current.can("JobsExecute")).toBe(true);
    });

    it("can() returns false for admin-only permissions", () => {
      mockUser("operator");

      const { result } = renderHook(() => usePermissions());

      expect(result.current.can("UsersManage")).toBe(false);
      expect(result.current.can("ConnectionsCreate")).toBe(false);
      expect(result.current.can("SettingsManage")).toBe(false);
    });
  });

  describe("no user (unauthenticated)", () => {
    it("can() returns false for everything", () => {
      mockNoUser();

      const { result } = renderHook(() => usePermissions());

      expect(result.current.can("JobsView")).toBe(false);
      expect(result.current.can("UsersManage")).toBe(false);
      expect(result.current.can("DashboardView")).toBe(false);
    });

    it("role is null", () => {
      mockNoUser();

      const { result } = renderHook(() => usePermissions());

      expect(result.current.role).toBeNull();
    });
  });

  describe("canAny", () => {
    it("returns true if any permission matches", () => {
      mockUser("viewer");

      const { result } = renderHook(() => usePermissions());

      expect(result.current.canAny("JobsView", "JobsCreate")).toBe(true);
    });

    it("returns false if no permissions match", () => {
      mockUser("viewer");

      const { result } = renderHook(() => usePermissions());

      expect(result.current.canAny("JobsCreate", "UsersManage")).toBe(false);
    });
  });
});

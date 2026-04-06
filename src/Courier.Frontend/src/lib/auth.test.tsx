import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, act, waitFor, renderHook } from "@testing-library/react";
import React from "react";

// Mock next/navigation
const mockPush = vi.fn();
const mockPathname = vi.fn(() => "/dashboard");
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: mockPush }),
  usePathname: () => mockPathname(),
}));

// Mock the api module
vi.mock("./api", () => ({
  api: {
    setAccessToken: vi.fn(),
    login: vi.fn(),
    refreshToken: vi.fn(),
    logout: vi.fn(),
  },
}));

import { api } from "./api";
import { AuthProvider, useAuth } from "./auth";
import type { UserProfileDto } from "./types";

// --- Helpers ---

const mockUser: UserProfileDto = {
  id: "user-1",
  username: "testuser",
  displayName: "Test User",
  email: "test@example.com",
  role: "admin",
  isSsoUser: false,
  allowLocalPassword: true,
};

const mockLoginResponse = {
  data: {
    accessToken: "access-token-123",
    refreshToken: "refresh-token-456",
    expiresIn: 300,
    user: mockUser,
  },
  success: true,
  timestamp: new Date().toISOString(),
};

const mockRefreshResponse = {
  data: {
    accessToken: "new-access-token",
    refreshToken: "new-refresh-token",
    expiresIn: 300,
    user: mockUser,
  },
  success: true,
  timestamp: new Date().toISOString(),
};

function TestConsumer() {
  const auth = useAuth();
  return (
    <div>
      <span data-testid="loading">{String(auth.isLoading)}</span>
      <span data-testid="authenticated">{String(auth.isAuthenticated)}</span>
      <span data-testid="user">{auth.user?.username ?? "none"}</span>
    </div>
  );
}

function TestConsumerWithActions() {
  const auth = useAuth();
  return (
    <div>
      <span data-testid="loading">{String(auth.isLoading)}</span>
      <span data-testid="authenticated">{String(auth.isAuthenticated)}</span>
      <span data-testid="user">{auth.user?.username ?? "none"}</span>
      <button
        data-testid="login-btn"
        onClick={() => auth.login("testuser", "password123")}
      >
        Login
      </button>
      <button
        data-testid="logout-btn"
        onClick={() => auth.logout()}
      >
        Logout
      </button>
      <button
        data-testid="login-tokens-btn"
        onClick={() =>
          auth.loginWithTokens("tok-a", "tok-r", mockUser, 600)
        }
      >
        LoginWithTokens
      </button>
    </div>
  );
}

function renderWithProvider(ui?: React.ReactElement) {
  return render(
    <AuthProvider>{ui ?? <TestConsumer />}</AuthProvider>
  );
}

// --- Tests ---

describe("useAuth", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
    mockPathname.mockReturnValue("/dashboard");
    // Default: no stored token, so restoreSession finishes fast
    vi.mocked(api.refreshToken).mockRejectedValue(new Error("no token"));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("throws when used outside AuthProvider", () => {
    // Suppress React error boundary console output
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
    expect(() => {
      renderHook(() => useAuth());
    }).toThrow("useAuth must be used within an AuthProvider");
    spy.mockRestore();
  });
});

describe("AuthProvider", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
    mockPathname.mockReturnValue("/dashboard");
    // Default: refreshToken rejects so restoreSession completes without setting user
    vi.mocked(api.refreshToken).mockRejectedValue(new Error("no token"));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  // --- Initial state ---

  it("starts with isLoading true, user null, isAuthenticated false", () => {
    // Make restoreSession pend so we can observe the initial loading state
    localStorage.setItem("courier_refresh_token", "pending-token");
    vi.mocked(api.refreshToken).mockReturnValue(new Promise(() => {}));

    const { getByTestId } = renderWithProvider();
    // On first render, before restoreSession completes, isLoading should be true
    expect(getByTestId("loading").textContent).toBe("true");
    expect(getByTestId("authenticated").textContent).toBe("false");
    expect(getByTestId("user").textContent).toBe("none");
  });

  // --- Session restore ---

  it("sets isLoading to false when no stored refresh token exists", async () => {
    renderWithProvider();
    await waitFor(() => {
      expect(screen.getByTestId("loading").textContent).toBe("false");
    });
    expect(api.refreshToken).not.toHaveBeenCalled();
    expect(screen.getByTestId("user").textContent).toBe("none");
  });

  it("restores session when a valid refresh token is stored", async () => {
    localStorage.setItem("courier_refresh_token", "stored-refresh-tok");
    vi.mocked(api.refreshToken).mockResolvedValue(mockRefreshResponse);

    renderWithProvider();

    await waitFor(() => {
      expect(screen.getByTestId("loading").textContent).toBe("false");
    });
    expect(api.refreshToken).toHaveBeenCalledWith({
      refreshToken: "stored-refresh-tok",
    });
    expect(api.setAccessToken).toHaveBeenCalledWith("new-access-token");
    expect(localStorage.getItem("courier_refresh_token")).toBe(
      "new-refresh-token"
    );
    expect(screen.getByTestId("user").textContent).toBe("testuser");
    expect(screen.getByTestId("authenticated").textContent).toBe("true");
  });

  it("clears auth when session restore fails", async () => {
    localStorage.setItem("courier_refresh_token", "expired-token");
    vi.mocked(api.refreshToken).mockRejectedValue(new Error("invalid token"));

    renderWithProvider();

    await waitFor(() => {
      expect(screen.getByTestId("loading").textContent).toBe("false");
    });
    expect(api.setAccessToken).toHaveBeenCalledWith(null);
    expect(localStorage.getItem("courier_refresh_token")).toBeNull();
    expect(screen.getByTestId("user").textContent).toBe("none");
    expect(screen.getByTestId("authenticated").textContent).toBe("false");
  });

  it("handles refreshToken returning response without data on restore", async () => {
    localStorage.setItem("courier_refresh_token", "some-token");
    vi.mocked(api.refreshToken).mockResolvedValue({
      success: false,
      timestamp: new Date().toISOString(),
    });

    renderWithProvider();

    await waitFor(() => {
      expect(screen.getByTestId("loading").textContent).toBe("false");
    });
    // No user should be set since data was undefined
    expect(screen.getByTestId("user").textContent).toBe("none");
  });

  // --- login() ---

  it("logs in successfully and sets auth state", async () => {
    vi.mocked(api.login).mockResolvedValue(mockLoginResponse);

    renderWithProvider(<TestConsumerWithActions />);

    await waitFor(() => {
      expect(screen.getByTestId("loading").textContent).toBe("false");
    });

    await act(async () => {
      screen.getByTestId("login-btn").click();
    });

    expect(api.login).toHaveBeenCalledWith({
      username: "testuser",
      password: "password123",
    });
    expect(api.setAccessToken).toHaveBeenCalledWith("access-token-123");
    expect(localStorage.getItem("courier_refresh_token")).toBe(
      "refresh-token-456"
    );
    expect(screen.getByTestId("user").textContent).toBe("testuser");
    expect(screen.getByTestId("authenticated").textContent).toBe("true");
  });

  it("propagates login errors to the caller", async () => {
    const loginError = new Error("Invalid credentials");
    vi.mocked(api.login).mockRejectedValue(loginError);

    let caughtError: unknown;
    function ErrorTestConsumer() {
      const auth = useAuth();
      return (
        <button
          data-testid="login-err-btn"
          onClick={async () => {
            try {
              await auth.login("bad", "creds");
            } catch (e) {
              caughtError = e;
            }
          }}
        >
          Login
        </button>
      );
    }

    render(
      <AuthProvider>
        <ErrorTestConsumer />
      </AuthProvider>
    );

    await waitFor(() => {
      // Wait for loading to settle
      expect(api.refreshToken).not.toHaveBeenCalled();
    });

    await act(async () => {
      screen.getByTestId("login-err-btn").click();
    });

    expect(caughtError).toBe(loginError);
  });

  it("handles login response without data gracefully", async () => {
    vi.mocked(api.login).mockResolvedValue({
      success: false,
      timestamp: new Date().toISOString(),
    });

    renderWithProvider(<TestConsumerWithActions />);

    await waitFor(() => {
      expect(screen.getByTestId("loading").textContent).toBe("false");
    });

    await act(async () => {
      screen.getByTestId("login-btn").click();
    });

    // User should remain null since response had no data
    expect(screen.getByTestId("user").textContent).toBe("none");
    expect(screen.getByTestId("authenticated").textContent).toBe("false");
  });

  // --- loginWithTokens() ---

  it("sets auth state directly from provided tokens", async () => {
    renderWithProvider(<TestConsumerWithActions />);

    await waitFor(() => {
      expect(screen.getByTestId("loading").textContent).toBe("false");
    });

    await act(async () => {
      screen.getByTestId("login-tokens-btn").click();
    });

    expect(api.setAccessToken).toHaveBeenCalledWith("tok-a");
    expect(localStorage.getItem("courier_refresh_token")).toBe("tok-r");
    expect(screen.getByTestId("user").textContent).toBe("testuser");
    expect(screen.getByTestId("authenticated").textContent).toBe("true");
  });

  // --- logout() ---

  it("logs out, clears auth state, calls api.logout, and redirects", async () => {
    vi.mocked(api.login).mockResolvedValue(mockLoginResponse);
    vi.mocked(api.logout).mockResolvedValue({ timestamp: new Date().toISOString(), success: true });

    renderWithProvider(<TestConsumerWithActions />);

    await waitFor(() => {
      expect(screen.getByTestId("loading").textContent).toBe("false");
    });

    // First, log in
    await act(async () => {
      screen.getByTestId("login-btn").click();
    });
    expect(screen.getByTestId("authenticated").textContent).toBe("true");

    // Then log out
    mockPush.mockClear();
    await act(async () => {
      screen.getByTestId("logout-btn").click();
    });

    expect(api.logout).toHaveBeenCalledWith("refresh-token-456");
    expect(api.setAccessToken).toHaveBeenCalledWith(null);
    expect(localStorage.getItem("courier_refresh_token")).toBeNull();
    expect(screen.getByTestId("user").textContent).toBe("none");
    expect(screen.getByTestId("authenticated").textContent).toBe("false");
    expect(mockPush).toHaveBeenCalledWith("/login");
  });

  it("does not call api.logout when no stored token exists", async () => {
    vi.mocked(api.logout).mockResolvedValue({ timestamp: new Date().toISOString(), success: true });

    renderWithProvider(<TestConsumerWithActions />);

    await waitFor(() => {
      expect(screen.getByTestId("loading").textContent).toBe("false");
    });

    // Logout without ever logging in (no token in localStorage)
    await act(async () => {
      screen.getByTestId("logout-btn").click();
    });

    expect(api.logout).not.toHaveBeenCalled();
    expect(mockPush).toHaveBeenCalledWith("/login");
  });

  it("ignores api.logout errors and still clears auth", async () => {
    vi.mocked(api.login).mockResolvedValue(mockLoginResponse);
    vi.mocked(api.logout).mockRejectedValue(new Error("network error"));

    renderWithProvider(<TestConsumerWithActions />);

    await waitFor(() => {
      expect(screen.getByTestId("loading").textContent).toBe("false");
    });

    // Log in first
    await act(async () => {
      screen.getByTestId("login-btn").click();
    });

    // Log out - should not throw despite api.logout failing
    mockPush.mockClear();
    await act(async () => {
      screen.getByTestId("logout-btn").click();
    });

    expect(api.setAccessToken).toHaveBeenCalledWith(null);
    expect(localStorage.getItem("courier_refresh_token")).toBeNull();
    expect(screen.getByTestId("user").textContent).toBe("none");
    expect(mockPush).toHaveBeenCalledWith("/login");
  });

  // --- Redirect logic ---

  it("redirects to /login when not loading, no user, and on non-public path", async () => {
    mockPathname.mockReturnValue("/dashboard");

    renderWithProvider();

    await waitFor(() => {
      expect(screen.getByTestId("loading").textContent).toBe("false");
    });

    expect(mockPush).toHaveBeenCalledWith("/login");
  });

  it("does not redirect when on /login", async () => {
    mockPathname.mockReturnValue("/login");
    mockPush.mockClear();

    renderWithProvider();

    await waitFor(() => {
      expect(screen.getByTestId("loading").textContent).toBe("false");
    });

    expect(mockPush).not.toHaveBeenCalled();
  });

  it("does not redirect when on /setup", async () => {
    mockPathname.mockReturnValue("/setup");
    mockPush.mockClear();

    renderWithProvider();

    await waitFor(() => {
      expect(screen.getByTestId("loading").textContent).toBe("false");
    });

    expect(mockPush).not.toHaveBeenCalled();
  });

  it("does not redirect when on /auth/callback", async () => {
    mockPathname.mockReturnValue("/auth/callback");
    mockPush.mockClear();

    renderWithProvider();

    await waitFor(() => {
      expect(screen.getByTestId("loading").textContent).toBe("false");
    });

    expect(mockPush).not.toHaveBeenCalled();
  });

  it("does not redirect when on a public sub-path like /auth/callback/extra", async () => {
    mockPathname.mockReturnValue("/auth/callback/extra");
    mockPush.mockClear();

    renderWithProvider();

    await waitFor(() => {
      expect(screen.getByTestId("loading").textContent).toBe("false");
    });

    expect(mockPush).not.toHaveBeenCalled();
  });

  it("does not redirect when user is authenticated", async () => {
    localStorage.setItem("courier_refresh_token", "valid-token");
    vi.mocked(api.refreshToken).mockResolvedValue(mockRefreshResponse);
    mockPush.mockClear();

    renderWithProvider();

    await waitFor(() => {
      expect(screen.getByTestId("authenticated").textContent).toBe("true");
    });

    expect(mockPush).not.toHaveBeenCalledWith("/login");
  });

  it("does not redirect while still loading", () => {
    // Set up a refresh that never resolves to keep isLoading true
    vi.mocked(api.refreshToken).mockReturnValue(new Promise(() => {}));
    localStorage.setItem("courier_refresh_token", "pending-token");

    renderWithProvider();

    // isLoading is still true, redirect should not fire
    expect(screen.getByTestId("loading").textContent).toBe("true");
    expect(mockPush).not.toHaveBeenCalled();
  });

  // --- isAuthenticated ---

  it("isAuthenticated is true when user is set", async () => {
    localStorage.setItem("courier_refresh_token", "valid-token");
    vi.mocked(api.refreshToken).mockResolvedValue(mockRefreshResponse);

    renderWithProvider();

    await waitFor(() => {
      expect(screen.getByTestId("loading").textContent).toBe("false");
    });

    expect(screen.getByTestId("authenticated").textContent).toBe("true");
  });

  it("isAuthenticated is false when user is null", async () => {
    renderWithProvider();

    await waitFor(() => {
      expect(screen.getByTestId("loading").textContent).toBe("false");
    });

    expect(screen.getByTestId("authenticated").textContent).toBe("false");
  });

  // --- scheduleRefresh timer ---

  it("schedules a token refresh that fires before expiry", async () => {
    vi.useFakeTimers();
    vi.mocked(api.login).mockResolvedValue({
      ...mockLoginResponse,
      data: { ...mockLoginResponse.data!, expiresIn: 120 },
    });
    vi.mocked(api.refreshToken).mockRejectedValueOnce(new Error("no stored"));

    const secondRefreshResponse = {
      data: {
        accessToken: "refreshed-access",
        refreshToken: "refreshed-refresh",
        expiresIn: 120,
        user: { ...mockUser, displayName: "Refreshed User" },
      },
      success: true,
      timestamp: new Date().toISOString(),
    };
    // This will be used when the timer fires
    vi.mocked(api.refreshToken).mockResolvedValueOnce(secondRefreshResponse);

    renderWithProvider(<TestConsumerWithActions />);

    // Wait for initial load
    await act(async () => {
      await vi.runAllTimersAsync();
    });

    // Log in
    await act(async () => {
      screen.getByTestId("login-btn").click();
    });

    // expiresIn=120, so refreshMs = (120-60)*1000 = 60000ms
    // Set up the refresh mock for when timer fires
    vi.mocked(api.refreshToken).mockResolvedValueOnce(secondRefreshResponse);
    localStorage.setItem("courier_refresh_token", "refresh-token-456");

    // Advance past the scheduled refresh time
    await act(async () => {
      await vi.advanceTimersByTimeAsync(60_000);
    });

    // The scheduled refresh should have called refreshToken
    expect(api.refreshToken).toHaveBeenCalledWith({
      refreshToken: "refresh-token-456",
    });
  });

  it("enforces a minimum 30s refresh interval for very small expiresIn", async () => {
    vi.useFakeTimers();
    vi.mocked(api.login).mockResolvedValue({
      ...mockLoginResponse,
      data: { ...mockLoginResponse.data!, expiresIn: 10 },
    });
    // First call during restoreSession
    vi.mocked(api.refreshToken).mockRejectedValueOnce(new Error("no stored"));

    renderWithProvider(<TestConsumerWithActions />);

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    // Log in with expiresIn=10 => (10-60)*1000 = -50000 => max(-50000, 30000) = 30000
    await act(async () => {
      screen.getByTestId("login-btn").click();
    });

    localStorage.setItem("courier_refresh_token", "refresh-token-456");
    const refreshResult = {
      data: {
        accessToken: "new-tok",
        refreshToken: "new-ref",
        expiresIn: 300,
        user: mockUser,
      },
      success: true,
      timestamp: new Date().toISOString(),
    };
    vi.mocked(api.refreshToken).mockResolvedValueOnce(refreshResult);

    // Should NOT fire at 29s
    await act(async () => {
      await vi.advanceTimersByTimeAsync(29_000);
    });
    // refreshToken was called once during restoreSession, not again
    const callsAt29s = vi.mocked(api.refreshToken).mock.calls.length;

    // Should fire at 30s
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1_000);
    });

    expect(vi.mocked(api.refreshToken).mock.calls.length).toBeGreaterThan(
      callsAt29s
    );
  });

  it("redirects to /login when scheduled refresh fails", async () => {
    vi.useFakeTimers();

    // No stored token, so restoreSession exits early without calling refreshToken
    // (localStorage is clear from beforeEach)

    renderWithProvider(<TestConsumerWithActions />);

    // Let restoreSession complete (it exits immediately since no stored token)
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });

    // Now log in
    vi.mocked(api.login).mockResolvedValue({
      ...mockLoginResponse,
      data: { ...mockLoginResponse.data!, expiresIn: 120 },
    });

    await act(async () => {
      screen.getByTestId("login-btn").click();
    });

    // login() stores the refresh token and schedules a refresh at (120-60)*1000 = 60s
    // Now set up refreshToken to fail when the scheduled timer fires
    vi.mocked(api.refreshToken).mockReset();
    vi.mocked(api.refreshToken).mockRejectedValue(
      new Error("refresh failed")
    );
    mockPush.mockClear();
    vi.mocked(api.setAccessToken).mockClear();

    // Advance past the scheduled refresh time
    await act(async () => {
      await vi.advanceTimersByTimeAsync(60_000);
    });

    expect(api.setAccessToken).toHaveBeenCalledWith(null);
    expect(mockPush).toHaveBeenCalledWith("/login");
  });

  it("does nothing when scheduled refresh fires but no token in localStorage", async () => {
    vi.useFakeTimers();
    vi.mocked(api.login).mockResolvedValue({
      ...mockLoginResponse,
      data: { ...mockLoginResponse.data!, expiresIn: 120 },
    });
    vi.mocked(api.refreshToken).mockRejectedValueOnce(new Error("no stored"));

    renderWithProvider(<TestConsumerWithActions />);

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    await act(async () => {
      screen.getByTestId("login-btn").click();
    });

    // Remove token from localStorage before timer fires
    localStorage.removeItem("courier_refresh_token");
    vi.mocked(api.refreshToken).mockClear();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(60_000);
    });

    // refreshToken should NOT have been called since there's no stored token
    expect(api.refreshToken).not.toHaveBeenCalled();
  });

  // --- Cleanup on unmount ---

  it("clears the refresh timer on unmount (no stale refresh after unmount)", async () => {
    vi.useFakeTimers();
    // Restore session sets up a scheduled refresh via scheduleRefresh(300)
    localStorage.setItem("courier_refresh_token", "valid-token");
    vi.mocked(api.refreshToken).mockResolvedValue({
      ...mockRefreshResponse,
      data: { ...mockRefreshResponse.data!, expiresIn: 120 },
    });

    const { unmount } = renderWithProvider();

    await act(async () => {
      // Let restoreSession complete (microtask), which schedules refresh at 60s
      await vi.advanceTimersByTimeAsync(0);
    });

    // Clear mocks so we can detect any new calls after unmount
    vi.mocked(api.refreshToken).mockClear();

    unmount();

    // Advance past when the refresh timer would have fired (60s)
    await act(async () => {
      await vi.advanceTimersByTimeAsync(120_000);
    });

    // refreshToken should NOT have been called after unmount
    expect(api.refreshToken).not.toHaveBeenCalled();
  });

  // --- Provider value ---

  it("provides all expected context properties and methods", async () => {
    let contextValue: ReturnType<typeof useAuth> | undefined;

    function ContextCapture() {
      contextValue = useAuth();
      return null;
    }

    render(
      <AuthProvider>
        <ContextCapture />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(contextValue).toBeDefined();
    });

    expect(contextValue).toHaveProperty("user");
    expect(contextValue).toHaveProperty("isAuthenticated");
    expect(contextValue).toHaveProperty("isLoading");
    expect(contextValue).toHaveProperty("login");
    expect(contextValue).toHaveProperty("loginWithTokens");
    expect(contextValue).toHaveProperty("logout");
    expect(typeof contextValue!.login).toBe("function");
    expect(typeof contextValue!.loginWithTokens).toBe("function");
    expect(typeof contextValue!.logout).toBe("function");
  });

  it("renders children correctly", async () => {
    render(
      <AuthProvider>
        <div data-testid="child-content">Hello Courier</div>
      </AuthProvider>
    );

    expect(screen.getByTestId("child-content").textContent).toBe(
      "Hello Courier"
    );
  });
});

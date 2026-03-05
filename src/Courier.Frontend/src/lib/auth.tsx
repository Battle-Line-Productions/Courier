"use client";

import { createContext, useContext, useEffect, useState, useCallback, useRef } from "react";
import { useRouter, usePathname } from "next/navigation";
import { api } from "./api";
import type { UserProfileDto } from "./types";

interface AuthContextType {
  user: UserProfileDto | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | null>(null);

const REFRESH_TOKEN_KEY = "courier_refresh_token";
const PUBLIC_PATHS = ["/login", "/setup"];

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<UserProfileDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const router = useRouter();
  const pathname = usePathname();
  const refreshTimerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  const clearAuth = useCallback(() => {
    api.setAccessToken(null);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    setUser(null);
  }, []);

  const scheduleRefresh = useCallback((expiresIn: number) => {
    if (refreshTimerRef.current) {
      clearTimeout(refreshTimerRef.current);
    }
    // Refresh 60 seconds before expiry
    const refreshMs = Math.max((expiresIn - 60) * 1000, 30000);
    refreshTimerRef.current = setTimeout(async () => {
      const storedToken = localStorage.getItem(REFRESH_TOKEN_KEY);
      if (!storedToken) return;
      try {
        const response = await api.refreshToken({ refreshToken: storedToken });
        if (response.data) {
          api.setAccessToken(response.data.accessToken);
          localStorage.setItem(REFRESH_TOKEN_KEY, response.data.refreshToken);
          setUser(response.data.user);
          scheduleRefresh(response.data.expiresIn);
        }
      } catch {
        clearAuth();
        router.push("/login");
      }
    }, refreshMs);
  }, [clearAuth, router]);

  const login = useCallback(async (username: string, password: string) => {
    const response = await api.login({ username, password });
    if (response.data) {
      api.setAccessToken(response.data.accessToken);
      localStorage.setItem(REFRESH_TOKEN_KEY, response.data.refreshToken);
      setUser(response.data.user);
      scheduleRefresh(response.data.expiresIn);
    }
  }, [scheduleRefresh]);

  const logout = useCallback(async () => {
    const storedToken = localStorage.getItem(REFRESH_TOKEN_KEY);
    if (storedToken) {
      try {
        await api.logout(storedToken);
      } catch {
        // Ignore logout errors
      }
    }
    clearAuth();
    if (refreshTimerRef.current) {
      clearTimeout(refreshTimerRef.current);
    }
    router.push("/login");
  }, [clearAuth, router]);

  // Try to restore session on mount
  useEffect(() => {
    async function restoreSession() {
      const storedToken = localStorage.getItem(REFRESH_TOKEN_KEY);
      if (!storedToken) {
        setIsLoading(false);
        return;
      }
      try {
        const response = await api.refreshToken({ refreshToken: storedToken });
        if (response.data) {
          api.setAccessToken(response.data.accessToken);
          localStorage.setItem(REFRESH_TOKEN_KEY, response.data.refreshToken);
          setUser(response.data.user);
          scheduleRefresh(response.data.expiresIn);
        }
      } catch {
        clearAuth();
      }
      setIsLoading(false);
    }
    restoreSession();
    return () => {
      if (refreshTimerRef.current) {
        clearTimeout(refreshTimerRef.current);
      }
    };
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // Redirect logic
  useEffect(() => {
    if (isLoading) return;
    const isPublic = PUBLIC_PATHS.some((p) => pathname.startsWith(p));
    if (!user && !isPublic) {
      router.push("/login");
    }
  }, [user, isLoading, pathname, router]);

  return (
    <AuthContext.Provider value={{ user, isAuthenticated: !!user, isLoading, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}

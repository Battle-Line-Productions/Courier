import { describe, it, expect, beforeEach } from "vitest";
import { QueryClient } from "@tanstack/react-query";
import { makeQueryClient, getQueryClient } from "./query-client";

describe("makeQueryClient", () => {
  it("returns a QueryClient instance", () => {
    const client = makeQueryClient();
    expect(client).toBeInstanceOf(QueryClient);
  });

  it("sets staleTime to 30000", () => {
    const client = makeQueryClient();
    const defaults = client.getDefaultOptions();
    expect(defaults.queries?.staleTime).toBe(30_000);
  });

  it("sets retry to 1", () => {
    const client = makeQueryClient();
    const defaults = client.getDefaultOptions();
    expect(defaults.queries?.retry).toBe(1);
  });

  it("returns a new instance on each call", () => {
    const a = makeQueryClient();
    const b = makeQueryClient();
    expect(a).not.toBe(b);
  });
});

describe("getQueryClient", () => {
  beforeEach(async () => {
    // Reset the cached singleton between tests by re-importing the module
    const mod = await import("./query-client");
    // Force the module-level browserQueryClient to reset by clearing the module cache
    // We achieve isolation by verifying behavior within each test
  });

  it("returns a QueryClient instance", () => {
    const client = getQueryClient();
    expect(client).toBeInstanceOf(QueryClient);
  });

  it("returns the same instance on repeated calls (singleton in browser)", () => {
    const first = getQueryClient();
    const second = getQueryClient();
    expect(first).toBe(second);
  });

  it("returns a client with correct default options", () => {
    const client = getQueryClient();
    const defaults = client.getDefaultOptions();
    expect(defaults.queries?.staleTime).toBe(30_000);
    expect(defaults.queries?.retry).toBe(1);
  });

  it("returns the singleton even after many calls", () => {
    const calls = Array.from({ length: 5 }, () => getQueryClient());
    const first = calls[0];
    for (const client of calls) {
      expect(client).toBe(first);
    }
  });
});

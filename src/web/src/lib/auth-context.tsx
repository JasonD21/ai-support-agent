"use client";

import {
  createContext,
  useContext,
  useEffect,
  useState,
  ReactNode,
} from "react";
import { apiFetch, refreshAccessToken, setAccessToken, API_BASE } from "./api";

type User = {
  email: string;
  displayName: string | null;
};
type Tenant = {
  id: string;
  name: string;
  siteKey: string;
};

type AuthState = {
  user: User | null;
  tenant: Tenant | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (
    email: string,
    password: string,
    businessName: string,
    displayName?: string,
  ) => Promise<void>;
  logout: () => Promise<void>;
};

const AuthContext = createContext<AuthState | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [tenant, setTenant] = useState<Tenant | null>(null);
  const [loading, setLoading] = useState(true);

  function applyAuth(data: {
    accessToken: string;
    user: User;
    tenant: Tenant;
  }) {
    setAccessToken(data.accessToken);
    setUser(data.user);
    setTenant(data.tenant);
  }

  // On load, try to restore the session from the refresh cookie.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      const token = await refreshAccessToken();
      if (token && !cancelled) {
        const res = await apiFetch("/api/auth/me");
        if (res.ok) {
          const me = await res.json();
          setUser({ email: me.email, displayName: me.displayName });
          setTenant({
            id: me.tenantId,
            name: me.tenantName,
            siteKey: me.siteKey,
          });
        }
      }
      if (!cancelled) setLoading(false);
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  async function login(email: string, password: string) {
    const res = await fetch(`${API_BASE}/api/auth/login`, {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password }),
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.title ?? "Login failed");
    }
    applyAuth(await res.json());
  }

  async function register(
    email: string,
    password: string,
    businessName: string,
    displayName?: string,
  ) {
    const res = await fetch(`${API_BASE}/api/auth/register`, {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password, businessName, displayName }),
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      const fieldErrors = err.errors
        ? Object.values(err.errors).flat().join(" ")
        : null;
      throw new Error(
        (fieldErrors as string) || err.title || "Registration failed",
      );
    }
    applyAuth(await res.json());
  }

  async function logout() {
    await fetch(`${API_BASE}/api/auth/logout`, {
      method: "POST",
      credentials: "include",
    });
    setAccessToken(null);
    setUser(null);
    setTenant(null);
  }

  return (
    <AuthContext.Provider
      value={{ user, tenant, loading, login, register, logout }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}

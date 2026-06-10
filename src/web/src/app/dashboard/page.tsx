"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";

export default function DashboardPage() {
  const { user, tenant, loading, logout } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!loading && !user) router.replace("/login");
  }, [loading, user, router]);

  if (loading)
    return <main className="p-6 text-sm text-gray-500">Loading…</main>;
  if (!user) return null;

  return (
    <main className="mx-auto max-w-2xl space-y-4 p-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Dashboard</h1>
        <button
          onClick={() => logout().then(() => router.replace("/login"))}
          className="rounded border px-3 py-1.5 text-sm"
        >
          Sign out
        </button>
      </div>
      <div className="space-y-2 rounded-xl border p-4 text-sm">
        <p>
          <span className="text-gray-500">Signed in as:</span> {user.email}
        </p>
        {user.displayName && (
          <p>
            <span className="text-gray-500">Name:</span> {user.displayName}
          </p>
        )}
        <p>
          <span className="text-gray-500">Business:</span> {tenant?.name}
        </p>
        <p className="font-mono text-xs break-all">
          <span className="text-gray-500">Site key:</span> {tenant?.siteKey}
        </p>
      </div>
    </main>
  );
}

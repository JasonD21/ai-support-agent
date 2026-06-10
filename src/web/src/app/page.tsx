"use client";

import Link from "next/link";
import { useAuth } from "@/lib/auth-context";

export default function Home() {
  const { user, loading } = useAuth();
  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-4 p-8">
      <h1 className="text-2xl font-semibold">AI Support Agent</h1>
      {loading ? (
        <p className="text-sm text-gray-400">Loading…</p>
      ) : user ? (
        <Link
          href="/dashboard"
          className="rounded bg-indigo-600 px-4 py-2 text-white"
        >
          Go to dashboard
        </Link>
      ) : (
        <div className="flex gap-3">
          <Link href="/login" className="rounded border px-4 py-2">
            Sign in
          </Link>
          <Link
            href="/register"
            className="rounded bg-indigo-600 px-4 py-2 text-white"
          >
            Get started
          </Link>
        </div>
      )}
    </main>
  );
}

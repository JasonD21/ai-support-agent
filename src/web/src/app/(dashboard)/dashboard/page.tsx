"use client";

import Link from "next/link";
import { useAuth } from "@/lib/auth-context";

export default function DashboardPage() {
  const { user, tenant } = useAuth();
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Dashboard</h1>
      <div className="space-y-2 rounded-xl border p-4 text-sm">
        <p>
          <span className="text-gray-500">Signed in as:</span> {user?.email}
        </p>
        <p>
          <span className="text-gray-500">Business:</span> {tenant?.name}
        </p>
      </div>
      <p className="text-sm text-gray-500">
        Configure your agent in{" "}
        <Link href="/settings" className="text-indigo-600">
          Settings
        </Link>
        .
      </p>
    </div>
  );
}

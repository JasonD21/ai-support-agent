"use client";

import { useEffect } from "react";
import { useRouter, usePathname } from "next/navigation";
import Link from "next/link";
import { useAuth } from "@/lib/auth-context";

export default function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const { user, loading, logout } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (!loading && !user) router.replace("/login");
  }, [loading, user, router]);

  if (loading)
    return <main className="p-6 text-sm text-gray-500">Loading…</main>;
  if (!user) return null;

  const linkCls = (href: string) =>
    pathname === href ? "font-semibold" : "text-gray-500";

  return (
    <div className="min-h-screen">
      <header className="border-b">
        <div className="mx-auto flex max-w-4xl items-center justify-between p-4">
          <nav className="flex gap-4 text-sm">
            <Link href="/dashboard" className={linkCls("/dashboard")}>
              Dashboard
            </Link>
            <Link href="/knowledge" className={linkCls("/knowledge")}>
              Knowledge
            </Link>
            <Link href="/preview" className={linkCls("/preview")}>
              Test agent
            </Link>
            <Link href="/settings" className={linkCls("/settings")}>
              Settings
            </Link>
          </nav>
          <button
            onClick={() => logout().then(() => router.replace("/login"))}
            className="rounded border px-3 py-1.5 text-sm"
          >
            Sign out
          </button>
        </div>
      </header>
      <main className="mx-auto max-w-4xl p-6">{children}</main>
    </div>
  );
}

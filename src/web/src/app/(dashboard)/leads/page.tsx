// web/src/app/(dashboard)/leads/page.tsx
"use client";

import { useEffect, useState } from "react";
import { listLeads, updateLeadStatus, type Lead } from "@/lib/dashboard";

const STATUSES = ["New", "Contacted", "Closed"];

export default function LeadsPage() {
  const [leads, setLeads] = useState<Lead[]>([]);
  const [filter, setFilter] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setLoading(true);
    try {
      setLeads(await listLeads(filter || undefined));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed");
    } finally {
      setLoading(false);
    }
  }
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    load(); /* eslint-disable-next-line react-hooks/exhaustive-deps */
  }, [filter]);

  async function setStatus(id: string, status: string) {
    await updateLeadStatus(id, status);
    setLeads((prev) => prev.map((l) => (l.id === id ? { ...l, status } : l)));
  }

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Leads</h1>
      {error && <p className="text-sm text-red-600">{error}</p>}

      <div className="flex gap-2">
        <button
          onClick={() => setFilter("")}
          className={`rounded px-3 py-1 text-sm ${filter === "" ? "bg-indigo-600 text-white" : "border"}`}
        >
          All
        </button>
        {STATUSES.map((s) => (
          <button
            key={s}
            onClick={() => setFilter(s)}
            className={`rounded px-3 py-1 text-sm ${filter === s ? "bg-indigo-600 text-white" : "border"}`}
          >
            {s}
          </button>
        ))}
      </div>

      {loading ? (
        <p className="text-sm text-gray-500">Loading…</p>
      ) : (
        <ul className="space-y-3">
          {leads.map((l) => (
            <li key={l.id} className="rounded-xl border p-4 text-sm">
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="font-medium">
                    {l.contactName || "No contact captured"}
                    {l.contactEmail && (
                      <span className="font-normal text-gray-500">
                        {" "}
                        · {l.contactEmail}
                      </span>
                    )}
                  </p>
                  {l.contactPhone && (
                    <p className="text-gray-500">{l.contactPhone}</p>
                  )}
                  {l.stumpingQuestion && (
                    <p className="mt-1 text-gray-600">
                      Asked: “{l.stumpingQuestion}”
                    </p>
                  )}
                  {l.visitorMessage && (
                    <p className="mt-1 text-gray-600">
                      Note: {l.visitorMessage}
                    </p>
                  )}
                  <p className="mt-1 text-xs text-gray-400">
                    {l.reason} · {new Date(l.createdAt).toLocaleString()}
                  </p>
                </div>
                <select
                  value={l.status}
                  onChange={(e) => setStatus(l.id, e.target.value)}
                  className="rounded border px-2 py-1 text-sm"
                >
                  {STATUSES.map((s) => (
                    <option key={s} value={s}>
                      {s}
                    </option>
                  ))}
                </select>
              </div>
            </li>
          ))}
          {leads.length === 0 && (
            <li className="text-sm text-gray-400">
              No leads{filter ? ` (${filter})` : ""} yet.
            </li>
          )}
        </ul>
      )}
    </div>
  );
}

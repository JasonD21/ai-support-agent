"use client";

import { useEffect, useState } from "react";
import {
  listConversations,
  getConversation,
  deleteConversation,
  type ConversationListItem,
  type ConversationDetail,
} from "@/lib/dashboard";

export default function ConversationsPage() {
  const [items, setItems] = useState<ConversationListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [detail, setDetail] = useState<ConversationDetail | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setLoading(true);
    try {
      const d = await listConversations();
      setItems(d.items);
      setTotal(d.total);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    load();
  }, []);

  async function open(id: string) {
    try {
      setDetail(await getConversation(id));
    } catch {
      /* ignore */
    }
  }
  async function remove(id: string) {
    if (!confirm("Delete this conversation and its messages?")) return;
    await deleteConversation(id);
    setDetail(null);
    load();
  }

  if (loading) return <p className="text-sm text-gray-500">Loading…</p>;

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Conversations</h1>
      {error && <p className="text-sm text-red-600">{error}</p>}
      <p className="text-sm text-gray-500">{total} total</p>

      <ul className="divide-y rounded-xl border">
        {items.map((c) => (
          <li
            key={c.id}
            className="flex items-center justify-between gap-3 px-4 py-3 text-sm"
          >
            <button
              onClick={() => open(c.id)}
              className="min-w-0 flex-1 text-left"
            >
              <span className="font-medium">
                {new Date(c.startedAt).toLocaleString()}
              </span>
              <span className="ml-2 text-gray-500">{c.messageCount} msgs</span>
            </button>
            <div className="flex shrink-0 items-center gap-2">
              <StatusPill status={c.status} />
              {c.hasLead && (
                <span className="rounded bg-amber-50 px-1.5 py-0.5 text-xs text-amber-700">
                  lead
                </span>
              )}
            </div>
          </li>
        ))}
        {items.length === 0 && (
          <li className="px-4 py-6 text-sm text-gray-400">
            No conversations yet.
          </li>
        )}
      </ul>

      {detail && (
        <DetailModal
          detail={detail}
          onClose={() => setDetail(null)}
          onDelete={remove}
        />
      )}
    </div>
  );
}

function StatusPill({ status }: { status: string }) {
  const cls =
    status === "HandedOff"
      ? "bg-amber-50 text-amber-700"
      : status === "Closed"
        ? "bg-gray-100 text-gray-600"
        : "bg-green-50 text-green-700";
  return (
    <span className={`rounded px-1.5 py-0.5 text-xs ${cls}`}>{status}</span>
  );
}

function DetailModal({
  detail,
  onClose,
  onDelete,
}: {
  detail: ConversationDetail;
  onClose: () => void;
  onDelete: (id: string) => void;
}) {
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
      onClick={onClose}
    >
      <div
        className="flex max-h-[85vh] w-full max-w-2xl flex-col rounded-xl bg-white"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between border-b px-4 py-3">
          <div>
            <h2 className="font-medium">Conversation</h2>
            <p className="text-xs text-gray-500">
              {new Date(detail.startedAt).toLocaleString()} · {detail.status}
              {detail.originUrl ? ` · ${detail.originUrl}` : ""}
            </p>
          </div>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-700"
          >
            ✕
          </button>
        </div>

        <div className="flex-1 space-y-3 overflow-y-auto p-4">
          {detail.lead && (
            <div className="rounded-lg border border-amber-300 bg-amber-50 p-3 text-sm">
              <p className="font-medium text-amber-800">
                Lead — {detail.lead.reason} · {detail.lead.status}
              </p>
              {detail.lead.contactName && (
                <p className="text-amber-700">
                  {detail.lead.contactName} · {detail.lead.contactEmail}
                </p>
              )}
            </div>
          )}
          {detail.messages.map((m, i) => (
            <div
              key={i}
              className={
                m.role === "User" ? "flex justify-end" : "flex justify-start"
              }
            >
              <div className="max-w-[85%] space-y-1">
                <div
                  className={`rounded-2xl px-3 py-2 text-sm ${m.role === "User" ? "bg-indigo-600 text-white" : "bg-gray-100 text-gray-900"}`}
                >
                  {m.content}
                </div>
                {m.role === "Assistant" &&
                  (m.topSimilarity != null || m.modelUsed) && (
                    <div className="flex flex-wrap gap-2 px-1 text-xs text-gray-500">
                      {m.wasGrounded != null && (
                        <span
                          className={`rounded px-1.5 py-0.5 ${m.wasGrounded ? "bg-green-50 text-green-700" : "bg-amber-50 text-amber-700"}`}
                        >
                          {m.wasGrounded ? "grounded" : "not grounded"}
                        </span>
                      )}
                      {m.topSimilarity != null && (
                        <span>conf {Math.round(m.topSimilarity * 100)}%</span>
                      )}
                      {m.latencyMs != null && <span>· {m.latencyMs}ms</span>}
                      {m.modelUsed && <span>· {m.modelUsed}</span>}
                      {m.titles && m.titles.length > 0 && (
                        <span className="truncate">
                          · {m.titles.join(", ")}
                        </span>
                      )}
                    </div>
                  )}
              </div>
            </div>
          ))}
        </div>

        <div className="flex justify-between border-t px-4 py-3">
          <button
            onClick={() => onDelete(detail.id)}
            className="text-sm text-red-600"
          >
            Delete
          </button>
          <button
            onClick={onClose}
            className="rounded border px-3 py-1.5 text-sm"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
}

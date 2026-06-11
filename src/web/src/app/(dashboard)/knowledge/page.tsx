"use client";

import { useEffect, useMemo, useState } from "react";
import {
  getTypes,
  listKnowledge,
  createKnowledge,
  updateKnowledge,
  deleteKnowledge,
  type KnowledgeTypeDescriptor,
  type KnowledgeItem,
  type KnowledgeItemType,
} from "@/lib/knowledge";

export default function KnowledgePage() {
  const [types, setTypes] = useState<KnowledgeTypeDescriptor[]>([]);
  const [activeType, setActiveType] = useState<KnowledgeItemType | null>(null);
  const [items, setItems] = useState<KnowledgeItem[]>([]);
  const [form, setForm] = useState<Record<string, string>>({});
  const [editingId, setEditingId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const descriptor = useMemo(
    () => types.find((t) => t.type === activeType) ?? null,
    [types, activeType],
  );

  useEffect(() => {
    (async () => {
      try {
        const t = await getTypes();
        setTypes(t);
        setActiveType(t[0]?.type ?? null);
      } catch (e) {
        setError(e instanceof Error ? e.message : "Failed to load");
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  useEffect(() => {
    if (!activeType) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setEditingId(null);
    setForm({});
    (async () => {
      try {
        const list = await listKnowledge(activeType);
        setItems(list);
        const d = types.find((t) => t.type === activeType);
        if (d?.singleton && list.length > 0) startEdit(list[0], d);
      } catch (e) {
        setError(e instanceof Error ? e.message : "Failed to load items");
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeType]);

  function flash(m: string) {
    setMessage(m);
    setTimeout(() => setMessage(null), 2500);
  }

  function startEdit(item: KnowledgeItem, d = descriptor) {
    setEditingId(item.id);
    const f: Record<string, string> = {};
    for (const field of d?.fields ?? [])
      f[field.name] = (item.payload[field.name] as string | undefined) ?? "";
    setForm(f);
  }

  function resetForm() {
    setEditingId(null);
    setForm({});
  }

  function buildPayload(): Record<string, unknown> {
    const payload: Record<string, unknown> = {};
    for (const field of descriptor?.fields ?? []) {
      const v = (form[field.name] ?? "").trim();
      payload[field.name] = v === "" && !field.required ? null : v;
    }
    return payload;
  }

  async function submit() {
    if (!activeType) return;
    setBusy(true);
    setError(null);
    try {
      const payload = buildPayload();
      if (editingId) await updateKnowledge(editingId, activeType, payload);
      else await createKnowledge(activeType, payload);
      const list = await listKnowledge(activeType);
      setItems(list);
      if (descriptor?.singleton && list.length > 0) startEdit(list[0]);
      else resetForm();
      flash(editingId ? "Saved." : "Added.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Save failed");
    } finally {
      setBusy(false);
    }
  }

  async function remove(id: string) {
    setError(null);
    try {
      await deleteKnowledge(id);
      const list = await listKnowledge(activeType!);
      setItems(list);
      if (editingId === id) resetForm();
      flash("Deleted.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Delete failed");
    }
  }

  if (loading) return <p className="text-sm text-gray-500">Loading…</p>;

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Knowledge</h1>
      <p className="text-sm text-gray-500">
        What your agent knows. Each entry is embedded and used to ground
        answers.
      </p>

      {message && (
        <p className="rounded bg-green-50 px-3 py-2 text-sm text-green-700">
          {message}
        </p>
      )}
      {error && (
        <p className="rounded bg-red-50 px-3 py-2 text-sm text-red-700">
          {error}
        </p>
      )}

      <div className="flex flex-wrap gap-2 border-b pb-2">
        {types.map((t) => (
          <button
            key={t.type}
            onClick={() => setActiveType(t.type)}
            className={`rounded px-3 py-1.5 text-sm ${activeType === t.type ? "bg-indigo-600 text-white" : "border"}`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {descriptor && (
        <section className="space-y-4 rounded-xl border p-5">
          <h2 className="text-lg font-medium">
            {editingId ? "Edit" : "Add"} {descriptor.label}
          </h2>
          {descriptor.fields.map((field) => (
            <label key={field.name} className="block space-y-1">
              <span className="text-sm text-gray-600">
                {field.label}
                {field.required ? "" : " (optional)"}
              </span>
              {field.kind === "textarea" ? (
                <textarea
                  className="input min-h-24"
                  value={form[field.name] ?? ""}
                  onChange={(e) =>
                    setForm({ ...form, [field.name]: e.target.value })
                  }
                />
              ) : (
                <input
                  className="input"
                  value={form[field.name] ?? ""}
                  onChange={(e) =>
                    setForm({ ...form, [field.name]: e.target.value })
                  }
                />
              )}
            </label>
          ))}
          <div className="flex gap-2">
            <button
              onClick={submit}
              disabled={busy}
              className="rounded bg-indigo-600 px-4 py-2 text-white disabled:opacity-50"
            >
              {busy
                ? "Saving…"
                : editingId
                  ? "Save changes"
                  : `Add ${descriptor.label}`}
            </button>
            {editingId && !descriptor.singleton && (
              <button
                onClick={resetForm}
                className="rounded border px-4 py-2 text-sm"
              >
                Cancel
              </button>
            )}
          </div>
        </section>
      )}

      {descriptor && !descriptor.singleton && (
        <section className="space-y-2">
          <h3 className="text-sm font-medium text-gray-600">
            {items.length} {descriptor.label} entr
            {items.length === 1 ? "y" : "ies"}
          </h3>
          <ul className="space-y-2">
            {items.map((item) => (
              <li
                key={item.id}
                className="flex items-start justify-between gap-3 rounded-lg border p-3"
              >
                <div className="min-w-0 text-sm">
                  <p className="truncate font-medium">{primaryText(item)}</p>
                  <p className="line-clamp-2 text-gray-500">
                    {secondaryText(item)}
                  </p>
                </div>
                <div className="flex shrink-0 gap-3 text-sm">
                  <button
                    onClick={() => startEdit(item)}
                    className="text-indigo-600"
                  >
                    edit
                  </button>
                  <button
                    onClick={() => remove(item.id)}
                    className="text-red-600"
                  >
                    delete
                  </button>
                </div>
              </li>
            ))}
            {items.length === 0 && (
              <li className="text-sm text-gray-400">Nothing yet.</li>
            )}
          </ul>
        </section>
      )}
    </div>
  );
}

function primaryText(item: KnowledgeItem): string {
  const p = item.payload;
  return (p.question || p.name || p.title || p.about || "Item") as string;
}
function secondaryText(item: KnowledgeItem): string {
  const p = item.payload;
  return (p.answer || p.description || p.body || "") as string;
}

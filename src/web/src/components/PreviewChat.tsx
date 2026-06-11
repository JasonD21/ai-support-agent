"use client";

import { useRef, useState } from "react";
import { streamPreviewChat, type PreviewHistoryItem } from "@/lib/chat";

interface Meta {
  wasGrounded: boolean;
  topSimilarity?: number;
  latencyMs?: number;
  titles?: string[];
}
interface Turn {
  role: "user" | "assistant";
  content: string;
  handoff?: string;
  meta?: Meta;
  error?: string;
}

export default function PreviewChat() {
  const [messages, setMessages] = useState<Turn[]>([]);
  const [input, setInput] = useState("");
  const [streaming, setStreaming] = useState(false);
  const scrollRef = useRef<HTMLDivElement>(null);

  function scrollDown() {
    requestAnimationFrame(() =>
      scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight }),
    );
  }

  function patchLast(fn: (t: Turn) => Turn) {
    setMessages((prev) => {
      const copy = [...prev];
      copy[copy.length - 1] = fn(copy[copy.length - 1]);
      return copy;
    });
  }

  async function send() {
    const text = input.trim();
    if (!text || streaming) return;

    const history: PreviewHistoryItem[] = messages
      .filter((m) => m.content && !m.error)
      .map((m) => ({ role: m.role, content: m.content }));

    setInput("");
    setStreaming(true);
    setMessages((prev) => [
      ...prev,
      { role: "user", content: text },
      { role: "assistant", content: "" },
    ]);
    scrollDown();

    try {
      await streamPreviewChat(text, history, {
        onToken: (t) => {
          patchLast((m) => ({ ...m, content: m.content + t }));
          scrollDown();
        },
        onHandoff: (e) => patchLast((m) => ({ ...m, handoff: e.reason })),
        onDone: (e) => patchLast((m) => ({ ...m, meta: e })),
        onError: (msg) => patchLast((m) => ({ ...m, error: msg })),
      });
    } catch (e) {
      patchLast((m) => ({
        ...m,
        error: e instanceof Error ? e.message : "Stream failed",
      }));
    } finally {
      setStreaming(false);
      scrollDown();
    }
  }

  return (
    <div className="flex h-[32rem] flex-col rounded-xl border">
      <div className="flex items-center justify-between border-b px-4 py-2">
        <h2 className="text-sm font-medium">Test your agent</h2>
        <button
          onClick={() => setMessages([])}
          disabled={streaming || messages.length === 0}
          className="text-xs text-gray-500 hover:text-gray-800 disabled:opacity-40"
        >
          Clear
        </button>
      </div>

      <div ref={scrollRef} className="flex-1 space-y-3 overflow-y-auto p-4">
        {messages.length === 0 && (
          <p className="text-sm text-gray-400">
            Ask something your knowledge base should cover — or try “Can I talk
            to a human?”
          </p>
        )}
        {messages.map((m, i) => {
          const isLast = i === messages.length - 1;
          const placeholder =
            m.role === "assistant" &&
            streaming &&
            isLast &&
            !m.handoff &&
            !m.error &&
            !m.content;
          const showBubble = m.content.length > 0 || !!m.error || placeholder;
          return (
            <div
              key={i}
              className={
                m.role === "user" ? "flex justify-end" : "flex justify-start"
              }
            >
              <div
                className={`max-w-[80%] space-y-2 ${m.role === "assistant" ? "w-full" : ""}`}
              >
                {showBubble && (
                  <div
                    className={`inline-block rounded-2xl px-3 py-2 text-sm ${
                      m.role === "user"
                        ? "bg-indigo-600 text-white"
                        : "bg-gray-100 text-gray-900"
                    }`}
                  >
                    {m.content}
                    {placeholder && <span className="text-gray-400">…</span>}
                    {m.error && <span className="text-red-600">{m.error}</span>}
                  </div>
                )}
                {m.handoff && (
                  <div className="rounded-lg border border-amber-300 bg-amber-50 px-3 py-2 text-xs text-amber-800">
                    🤝 Would offer a human handoff —{" "}
                    <span className="font-medium">
                      {labelReason(m.handoff)}
                    </span>
                  </div>
                )}
                {m.meta && m.role === "assistant" && <MetaRow meta={m.meta} />}
              </div>
            </div>
          );
        })}
      </div>

      <div className="flex gap-2 border-t p-3">
        <input
          className="input flex-1"
          placeholder="Type a question…"
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") send();
          }}
          disabled={streaming}
        />
        <button
          onClick={send}
          disabled={streaming || !input.trim()}
          className="rounded bg-indigo-600 px-4 py-2 text-sm text-white disabled:opacity-50"
        >
          {streaming ? "…" : "Send"}
        </button>
      </div>
    </div>
  );
}

function MetaRow({ meta }: { meta: Meta }) {
  const pct =
    meta.topSimilarity != null ? Math.round(meta.topSimilarity * 100) : null;
  return (
    <div className="flex flex-wrap items-center gap-2 px-1 text-xs text-gray-500">
      {meta.wasGrounded ? (
        <span className="rounded bg-green-50 px-1.5 py-0.5 text-green-700">
          grounded
        </span>
      ) : (
        <span className="rounded bg-amber-50 px-1.5 py-0.5 text-amber-700">
          not grounded
        </span>
      )}
      {pct != null && <span>confidence {pct}%</span>}
      {meta.latencyMs != null && <span>· {meta.latencyMs} ms</span>}
      {meta.titles && meta.titles.length > 0 && (
        <span className="truncate">· sources: {meta.titles.join(", ")}</span>
      )}
    </div>
  );
}

function labelReason(reason: string) {
  switch (reason) {
    case "Explicit":
      return "visitor asked for a person";
    case "NoGrounding":
      return "no confident answer in the knowledge base";
    default:
      return reason;
  }
}

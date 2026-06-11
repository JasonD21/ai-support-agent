"use client";

import { useEffect, useRef, useState } from "react";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

interface Turn {
  role: "user" | "assistant";
  content: string;
  handoff?: string;
}
interface Session {
  conversationId: string;
  sessionToken: string;
}

export default function EmbedPage() {
  const [siteKey, setSiteKey] = useState<string | null>(null);
  const [hostOrigin, setHostOrigin] = useState("");
  const [config, setConfig] = useState<{
    agentName: string;
    greeting: string;
    themeColor: string;
  } | null>(null);
  const [messages, setMessages] = useState<Turn[]>([]);
  const [input, setInput] = useState("");
  const [streaming, setStreaming] = useState(false);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const session = useRef<Session | null>(null);
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const p = new URLSearchParams(window.location.search);
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setSiteKey(p.get("siteKey"));
    setHostOrigin(p.get("origin") ?? "");
  }, []);

  useEffect(() => {
    if (!siteKey) return;
    (async () => {
      try {
        const cfg = await getJson<{
          agentName: string;
          greeting: string;
          themeColor: string;
        }>("/api/widget/config", siteKey, hostOrigin);
        setConfig(cfg);

        const stored = readSession(siteKey);
        if (stored) {
          session.current = stored;
          try {
            const hist = await getJson<Turn[]>(
              `/api/widget/conversations/${stored.conversationId}/messages`,
              siteKey,
              hostOrigin,
              stored.sessionToken,
            );
            setMessages(
              hist.map((m) => ({ role: m.role, content: m.content })),
            );
          } catch {
            // eslint-disable-next-line react-hooks/immutability
            await startSession(siteKey, hostOrigin, cfg.greeting);
          }
        } else {
          await startSession(siteKey, hostOrigin, cfg.greeting);
        }
        setReady(true);
      } catch {
        setError("Couldn't load the chat. Check the site key.");
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [siteKey]);

  async function startSession(key: string, origin: string, greeting: string) {
    const res = await fetch(`${API_BASE}/api/widget/conversations`, {
      method: "POST",
      headers: { "X-Site-Key": key, "X-Widget-Origin": origin },
    });
    if (!res.ok) throw new Error("start failed");
    const data = await res.json();
    session.current = {
      conversationId: data.conversationId,
      sessionToken: data.sessionToken,
    };
    writeSession(key, session.current);
    if (greeting) setMessages([{ role: "assistant", content: greeting }]);
  }

  function scrollDown() {
    requestAnimationFrame(() =>
      scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight }),
    );
  }
  function patchLast(fn: (t: Turn) => Turn) {
    setMessages((prev) => {
      const c = [...prev];
      c[c.length - 1] = fn(c[c.length - 1]);
      return c;
    });
  }

  async function send() {
    const text = input.trim();
    if (!text || streaming || !session.current || !siteKey) return;
    setInput("");
    setStreaming(true);
    setMessages((prev) => [
      ...prev,
      { role: "user", content: text },
      { role: "assistant", content: "" },
    ]);
    scrollDown();

    try {
      const res = await fetch(
        `${API_BASE}/api/widget/conversations/${session.current.conversationId}/messages`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-Site-Key": siteKey,
            "X-Session-Token": session.current.sessionToken,
            "X-Widget-Origin": hostOrigin,
          },
          body: JSON.stringify({ message: text }),
        },
      );
      if (!res.ok || !res.body) {
        patchLast((m) => ({ ...m, content: "Something went wrong." }));
        setStreaming(false);
        return;
      }

      const reader = res.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";
      while (true) {
        const { value, done } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });
        let idx: number;
        while ((idx = buffer.indexOf("\n\n")) !== -1) {
          const raw = buffer.slice(0, idx);
          buffer = buffer.slice(idx + 2);
          const line = raw.split("\n").find((l) => l.startsWith("data:"));
          if (!line) continue;
          const json = line.slice(5).trim();
          if (!json) continue;
          let evt: { type: string; [k: string]: unknown };
          try {
            evt = JSON.parse(json);
          } catch {
            continue;
          }
          if (evt.type === "token") {
            patchLast((m) => ({
              ...m,
              content: m.content + (evt.value as string),
            }));
            scrollDown();
          } else if (evt.type === "handoff") {
            patchLast((m) => ({ ...m, handoff: evt.reason as string }));
          } else if (evt.type === "error") {
            patchLast((m) => ({ ...m, content: evt.message as string }));
          }
        }
      }
    } catch {
      patchLast((m) => ({ ...m, content: "Connection lost." }));
    } finally {
      setStreaming(false);
      scrollDown();
    }
  }

  const theme = config?.themeColor || "#4f46e5";

  return (
    <div className="flex h-screen flex-col bg-white">
      <header
        className="flex items-center justify-between px-4 py-3 text-white"
        style={{ background: theme }}
      >
        <span className="font-medium">{config?.agentName || "Support"}</span>
        <button
          onClick={() => window.parent.postMessage({ type: "asa:close" }, "*")}
          aria-label="Close"
          className="opacity-90 hover:opacity-100"
        >
          ✕
        </button>
      </header>

      <div ref={scrollRef} className="flex-1 space-y-3 overflow-y-auto p-4">
        {error && <p className="text-sm text-red-600">{error}</p>}
        {messages.map((m, i) => {
          const placeholder =
            m.role === "assistant" &&
            streaming &&
            i === messages.length - 1 &&
            !m.content &&
            !m.handoff;
          return (
            <div
              key={i}
              className={
                m.role === "user" ? "flex justify-end" : "flex justify-start"
              }
            >
              <div className="max-w-[85%] space-y-2">
                {(m.content || placeholder) && (
                  <div
                    className={`rounded-2xl px-3 py-2 text-sm ${m.role === "user" ? "text-white" : "bg-gray-100 text-gray-900"}`}
                    style={
                      m.role === "user" ? { background: theme } : undefined
                    }
                  >
                    {m.content}
                    {placeholder && <span className="text-gray-400">…</span>}
                  </div>
                )}
                {m.handoff && (
                  <div className="rounded-lg border border-amber-300 bg-amber-50 px-3 py-2 text-xs text-amber-800">
                    I’ll connect you with the team — someone will follow up.
                  </div>
                )}
              </div>
            </div>
          );
        })}
      </div>

      <div className="flex gap-2 border-t p-3">
        <input
          className="flex-1 rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2"
          placeholder="Type a message…"
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") send();
          }}
          disabled={!ready || streaming}
        />
        <button
          onClick={send}
          disabled={!ready || streaming || !input.trim()}
          className="rounded px-4 py-2 text-sm text-white disabled:opacity-50"
          style={{ background: theme }}
        >
          Send
        </button>
      </div>
    </div>
  );
}

async function getJson<T>(
  path: string,
  siteKey: string,
  origin: string,
  sessionToken?: string,
): Promise<T> {
  const headers: Record<string, string> = {
    "X-Site-Key": siteKey,
    "X-Widget-Origin": origin,
  };
  if (sessionToken) headers["X-Session-Token"] = sessionToken;
  const res = await fetch(`${API_BASE}${path}`, { headers });
  if (!res.ok) throw new Error(`${res.status}`);
  return res.json();
}
function readSession(siteKey: string): Session | null {
  try {
    const v = sessionStorage.getItem(`asa:session:${siteKey}`);
    return v ? JSON.parse(v) : null;
  } catch {
    return null;
  }
}
function writeSession(siteKey: string, s: Session) {
  try {
    sessionStorage.setItem(`asa:session:${siteKey}`, JSON.stringify(s));
  } catch {
    /* ignore */
  }
}

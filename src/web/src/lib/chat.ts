import { apiFetch } from "./api";

export interface PreviewHistoryItem {
  role: "user" | "assistant";
  content: string;
}
export interface DoneEvent {
  wasGrounded: boolean;
  topSimilarity?: number;
  latencyMs?: number;
  titles?: string[];
}
export interface HandoffEvent {
  reason: string;
}
export interface PreviewCallbacks {
  onToken: (t: string) => void;
  onHandoff: (e: HandoffEvent) => void;
  onDone: (e: DoneEvent) => void;
  onError: (message: string) => void;
}

export async function streamPreviewChat(
  message: string,
  history: PreviewHistoryItem[],
  cb: PreviewCallbacks,
): Promise<void> {
  const res = await apiFetch("/api/agent/preview-chat", {
    method: "POST",
    body: JSON.stringify({ message, history }),
  });
  if (!res.ok || !res.body) {
    cb.onError(`Request failed (${res.status})`);
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

      switch (evt.type) {
        case "token":
          cb.onToken(evt.value as string);
          break;
        case "handoff":
          cb.onHandoff({ reason: evt.reason as string });
          break;
        case "done":
          cb.onDone(evt as unknown as DoneEvent);
          break;
        case "error":
          cb.onError(evt.message as string);
          break;
      }
    }
  }
}

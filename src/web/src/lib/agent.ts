import { apiFetch } from "./api";

export type Tone = "Friendly" | "Professional" | "Concise";
export type BubblePosition = "BottomRight" | "BottomLeft";
export type LlmProvider = "OpenRouter" | "OpenAI" | "Anthropic" | "Other";

export interface HandoffPosture {
  onExplicitRequest: boolean;
  onNoGrounding: boolean;
  onConsecutiveUnresolved: boolean;
  consecutiveUnresolvedThreshold: number;
}
export interface AgentConfig {
  agentName: string;
  greeting: string;
  tone: Tone;
  extraInstructions: string | null;
  themeColor: string;
  bubblePosition: BubblePosition;
  handoff: HandoffPosture;
}
export interface AgentSettings {
  config: AgentConfig;
  siteKey: string;
  allowedOrigins: string[];
  embedSnippet: string;
}
export interface LlmCredential {
  configured: boolean;
  provider: LlmProvider | null;
  baseUrl: string | null;
  maskedKey: string | null;
}

async function json<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    const fieldErrors = err.errors
      ? Object.values(err.errors).flat().join(" ")
      : null;
    throw new Error(
      (fieldErrors as string) || err.title || `Request failed (${res.status})`,
    );
  }
  return res.json();
}

export const getAgent = () => apiFetch("/api/agent").then(json<AgentSettings>);

export const updateAgent = (config: AgentConfig, allowedOrigins: string[]) =>
  apiFetch("/api/agent", {
    method: "PUT",
    body: JSON.stringify({ config, allowedOrigins }),
  }).then(json<AgentSettings>);

export const rotateSiteKey = () =>
  apiFetch("/api/agent/rotate-site-key", { method: "POST" }).then(
    json<{ siteKey: string; embedSnippet: string }>,
  );

export const getLlmCredential = () =>
  apiFetch("/api/agent/llm-credential").then(json<LlmCredential>);

export const upsertLlmCredential = (
  provider: LlmProvider,
  baseUrl: string | null,
  apiKey: string,
) =>
  apiFetch("/api/agent/llm-credential", {
    method: "PUT",
    body: JSON.stringify({ provider, baseUrl, apiKey }),
  }).then(json<LlmCredential>);

export const deleteLlmCredential = () =>
  apiFetch("/api/agent/llm-credential", { method: "DELETE" });

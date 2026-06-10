"use client";

import { useEffect, useState } from "react";
import {
  getAgent,
  updateAgent,
  rotateSiteKey,
  getLlmCredential,
  upsertLlmCredential,
  deleteLlmCredential,
  type AgentConfig,
  type AgentSettings,
  type LlmCredential,
  type LlmProvider,
} from "@/lib/agent";

export default function SettingsPage() {
  const [config, setConfig] = useState<AgentConfig | null>(null);
  const [origins, setOrigins] = useState<string[]>([]);
  const [siteKey, setSiteKey] = useState("");
  const [snippet, setSnippet] = useState("");
  const [newOrigin, setNewOrigin] = useState("");

  const [cred, setCred] = useState<LlmCredential | null>(null);
  const [provider, setProvider] = useState<LlmProvider>("OpenRouter");
  const [baseUrl, setBaseUrl] = useState("");
  const [apiKey, setApiKey] = useState("");

  const [loading, setLoading] = useState(true);
  const [savingConfig, setSavingConfig] = useState(false);
  const [savingCred, setSavingCred] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    (async () => {
      try {
        const [agent, credential] = await Promise.all([
          getAgent(),
          getLlmCredential(),
        ]);
        applyAgent(agent);
        setCred(credential);
        if (credential.provider) setProvider(credential.provider);
        if (credential.baseUrl) setBaseUrl(credential.baseUrl);
      } catch (e) {
        setError(e instanceof Error ? e.message : "Failed to load settings");
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  function applyAgent(a: AgentSettings) {
    setConfig(a.config);
    setOrigins(a.allowedOrigins);
    setSiteKey(a.siteKey);
    setSnippet(a.embedSnippet);
  }
  function flash(msg: string) {
    setMessage(msg);
    setTimeout(() => setMessage(null), 2500);
  }

  async function saveConfig() {
    if (!config) return;
    setSavingConfig(true);
    setError(null);
    try {
      applyAgent(await updateAgent(config, origins));
      flash("Agent settings saved.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Save failed");
    } finally {
      setSavingConfig(false);
    }
  }

  async function rotate() {
    setError(null);
    try {
      const r = await rotateSiteKey();
      setSiteKey(r.siteKey);
      setSnippet(r.embedSnippet);
      flash("Site key rotated — update your embed snippet.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Rotate failed");
    }
  }

  function addOrigin() {
    const o = newOrigin.trim();
    if (o && !origins.includes(o)) setOrigins([...origins, o]);
    setNewOrigin("");
  }

  async function saveCred() {
    setSavingCred(true);
    setError(null);
    try {
      const u = await upsertLlmCredential(provider, baseUrl || null, apiKey);
      setCred(u);
      setApiKey("");
      flash("API key saved.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Save failed");
    } finally {
      setSavingCred(false);
    }
  }

  async function removeCred() {
    setError(null);
    try {
      await deleteLlmCredential();
      setCred({
        configured: false,
        provider: null,
        baseUrl: null,
        maskedKey: null,
      });
      flash("Reverted to the shared key.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Delete failed");
    }
  }

  function copySnippet() {
    navigator.clipboard
      .writeText(snippet)
      .then(() => flash("Embed snippet copied."));
  }

  if (loading)
    return <p className="text-sm text-gray-500">Loading settings…</p>;
  if (!config)
    return (
      <p className="text-sm text-red-600">
        {error ?? "Could not load settings."}
      </p>
    );

  return (
    <div className="space-y-8">
      <h1 className="text-2xl font-semibold">Settings</h1>
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

      <section className="space-y-4 rounded-xl border p-5">
        <h2 className="text-lg font-medium">Agent</h2>
        <Field label="Agent name">
          <input
            className="input"
            value={config.agentName}
            onChange={(e) =>
              setConfig({ ...config, agentName: e.target.value })
            }
          />
        </Field>
        <Field label="Greeting">
          <input
            className="input"
            value={config.greeting}
            onChange={(e) => setConfig({ ...config, greeting: e.target.value })}
          />
        </Field>
        <Field label="Tone">
          <select
            className="input"
            value={config.tone}
            onChange={(e) =>
              setConfig({
                ...config,
                tone: e.target.value as AgentConfig["tone"],
              })
            }
          >
            <option>Friendly</option>
            <option>Professional</option>
            <option>Concise</option>
          </select>
        </Field>
        <Field label="Extra instructions (optional)">
          <textarea
            className="input min-h-20"
            value={config.extraInstructions ?? ""}
            onChange={(e) =>
              setConfig({
                ...config,
                extraInstructions: e.target.value || null,
              })
            }
          />
        </Field>
        <div className="flex gap-4">
          <Field label="Theme colour">
            <input
              type="color"
              className="h-10 w-16 rounded border"
              value={config.themeColor}
              onChange={(e) =>
                setConfig({ ...config, themeColor: e.target.value })
              }
            />
          </Field>
          <Field label="Bubble position">
            <select
              className="input"
              value={config.bubblePosition}
              onChange={(e) =>
                setConfig({
                  ...config,
                  bubblePosition: e.target
                    .value as AgentConfig["bubblePosition"],
                })
              }
            >
              <option value="BottomRight">Bottom right</option>
              <option value="BottomLeft">Bottom left</option>
            </select>
          </Field>
        </div>

        <div className="space-y-2">
          <p className="text-sm font-medium">When to offer a human handoff</p>
          <Checkbox
            label="On explicit request"
            checked={config.handoff.onExplicitRequest}
            onChange={(v) =>
              setConfig({
                ...config,
                handoff: { ...config.handoff, onExplicitRequest: v },
              })
            }
          />
          <Checkbox
            label="When it can't answer (no grounding)"
            checked={config.handoff.onNoGrounding}
            onChange={(v) =>
              setConfig({
                ...config,
                handoff: { ...config.handoff, onNoGrounding: v },
              })
            }
          />
          <Checkbox
            label="After repeated unresolved questions"
            checked={config.handoff.onConsecutiveUnresolved}
            onChange={(v) =>
              setConfig({
                ...config,
                handoff: { ...config.handoff, onConsecutiveUnresolved: v },
              })
            }
          />
          <Field label="Unresolved threshold">
            <input
              type="number"
              min={1}
              max={10}
              className="input w-24"
              value={config.handoff.consecutiveUnresolvedThreshold}
              onChange={(e) =>
                setConfig({
                  ...config,
                  handoff: {
                    ...config.handoff,
                    consecutiveUnresolvedThreshold: Number(e.target.value),
                  },
                })
              }
            />
          </Field>
        </div>

        <div className="space-y-2">
          <p className="text-sm font-medium">
            Allowed origins (domains that may embed the widget)
          </p>
          <div className="flex gap-2">
            <input
              className="input flex-1"
              placeholder="https://example.com"
              value={newOrigin}
              onChange={(e) => setNewOrigin(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter") {
                  e.preventDefault();
                  addOrigin();
                }
              }}
            />
            <button
              type="button"
              onClick={addOrigin}
              className="rounded border px-3 text-sm"
            >
              Add
            </button>
          </div>
          <ul className="space-y-1">
            {origins.map((o) => (
              <li
                key={o}
                className="flex items-center justify-between rounded bg-gray-50 px-3 py-1.5 text-sm"
              >
                <span className="font-mono">{o}</span>
                <button
                  type="button"
                  onClick={() => setOrigins(origins.filter((x) => x !== o))}
                  className="text-red-600"
                >
                  remove
                </button>
              </li>
            ))}
            {origins.length === 0 && (
              <li className="text-sm text-gray-400">None yet.</li>
            )}
          </ul>
        </div>

        <button
          onClick={saveConfig}
          disabled={savingConfig}
          className="rounded bg-indigo-600 px-4 py-2 text-white disabled:opacity-50"
        >
          {savingConfig ? "Saving…" : "Save agent settings"}
        </button>
      </section>

      <section className="space-y-3 rounded-xl border p-5">
        <h2 className="text-lg font-medium">Embed on your site</h2>
        <Field label="Site key">
          <input className="input font-mono" readOnly value={siteKey} />
        </Field>
        <Field label="Embed snippet">
          <textarea
            className="input min-h-20 font-mono text-xs"
            readOnly
            value={snippet}
          />
        </Field>
        <div className="flex gap-2">
          <button
            onClick={copySnippet}
            className="rounded border px-3 py-1.5 text-sm"
          >
            Copy snippet
          </button>
          <button
            onClick={rotate}
            className="rounded border px-3 py-1.5 text-sm text-amber-700"
          >
            Rotate site key
          </button>
        </div>
        <p className="text-xs text-gray-500">
          Rotating invalidates the old key — any site using the old snippet
          stops working until updated.
        </p>
      </section>

      <section className="space-y-3 rounded-xl border p-5">
        <h2 className="text-lg font-medium">
          Bring your own LLM key (optional)
        </h2>
        <p className="text-sm text-gray-500">
          {cred?.configured
            ? `Currently using your ${cred.provider} key (${cred.maskedKey}).`
            : "Currently using the shared free key. Add your own to route answers through your provider."}
        </p>
        <div className="flex gap-4">
          <Field label="Provider">
            <select
              className="input"
              value={provider}
              onChange={(e) => setProvider(e.target.value as LlmProvider)}
            >
              <option>OpenRouter</option>
              <option>OpenAI</option>
              <option>Anthropic</option>
              <option>Other</option>
            </select>
          </Field>
          <Field label="Base URL (optional)">
            <input
              className="input"
              placeholder="https://api.openai.com/v1"
              value={baseUrl}
              onChange={(e) => setBaseUrl(e.target.value)}
            />
          </Field>
        </div>
        <Field label="API key">
          <input
            className="input"
            type="password"
            placeholder="Paste your key"
            value={apiKey}
            onChange={(e) => setApiKey(e.target.value)}
          />
        </Field>
        <div className="flex gap-2">
          <button
            onClick={saveCred}
            disabled={savingCred || !apiKey}
            className="rounded bg-indigo-600 px-4 py-2 text-white disabled:opacity-50"
          >
            {savingCred ? "Saving…" : "Save key"}
          </button>
          {cred?.configured && (
            <button
              onClick={removeCred}
              className="rounded border px-4 py-2 text-sm text-red-600"
            >
              Remove key
            </button>
          )}
        </div>
      </section>
    </div>
  );
}

function Field({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <label className="block space-y-1">
      <span className="text-sm text-gray-600">{label}</span>
      {children}
    </label>
  );
}

function Checkbox({
  label,
  checked,
  onChange,
}: {
  label: string;
  checked: boolean;
  onChange: (v: boolean) => void;
}) {
  return (
    <label className="flex items-center gap-2 text-sm">
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
      />
      {label}
    </label>
  );
}

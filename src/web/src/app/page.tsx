"use client";

import { useEffect, useState } from "react";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

export default function Home() {
  const [status, setStatus] = useState<"loading" | "ok" | "error">("loading");
  const [detail, setDetail] = useState("");

  useEffect(() => {
    fetch(`${API_BASE}/health`)
      .then((res) => {
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        return res.json();
      })
      .then((data) => {
        setStatus("ok");
        setDetail(JSON.stringify(data));
      })
      .catch((err) => {
        setStatus("error");
        setDetail(String(err));
      });
  }, []);

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-4 p-8">
      <h1 className="text-2xl font-semibold">AI Support Agent</h1>
      <p className="text-sm text-gray-500">Foundation smoke test</p>
      <div className="rounded-lg border px-4 py-3 font-mono text-sm">
        API:{" "}
        <span
          className={
            status === "ok"
              ? "text-green-600"
              : status === "error"
                ? "text-red-600"
                : "text-gray-400"
          }
        >
          {status}
        </span>
        {detail && <div className="mt-2 text-xs text-gray-500">{detail}</div>}
      </div>
    </main>
  );
}

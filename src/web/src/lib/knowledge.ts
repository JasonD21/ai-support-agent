import { apiFetch } from "./api";

export type KnowledgeItemType =
  | "Faq"
  | "Service"
  | "Policy"
  | "BusinessProfile";

export interface FieldDescriptor {
  name: string;
  label: string;
  kind: "text" | "textarea" | string;
  required: boolean;
}
export interface KnowledgeTypeDescriptor {
  type: KnowledgeItemType;
  label: string;
  singleton: boolean;
  fields: FieldDescriptor[];
}
export interface KnowledgeItem {
  id: string;
  itemType: KnowledgeItemType;
  payload: Record<string, unknown>;
  createdAt: string;
  updatedAt: string | null;
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

export const getTypes = () =>
  apiFetch("/api/knowledge/types").then(json<KnowledgeTypeDescriptor[]>);

export const listKnowledge = (type?: KnowledgeItemType) =>
  apiFetch(`/api/knowledge${type ? `?type=${type}` : ""}`).then(
    json<KnowledgeItem[]>,
  );

export const createKnowledge = (
  itemType: KnowledgeItemType,
  payload: Record<string, unknown>,
) =>
  apiFetch("/api/knowledge", {
    method: "POST",
    body: JSON.stringify({ itemType, payload }),
  }).then(json<KnowledgeItem>);

export const updateKnowledge = (
  id: string,
  itemType: KnowledgeItemType,
  payload: Record<string, unknown>,
) =>
  apiFetch(`/api/knowledge/${id}`, {
    method: "PUT",
    body: JSON.stringify({ itemType, payload }),
  }).then(json<KnowledgeItem>);

export const deleteKnowledge = (id: string) =>
  apiFetch(`/api/knowledge/${id}`, { method: "DELETE" });

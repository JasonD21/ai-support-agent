import { apiFetch } from "./api";

export interface Summary {
  conversations: number;
  leads: number;
  newLeads: number;
  knowledgeItems: number;
  messagesToday: number;
}
export interface ConversationListItem {
  id: string;
  status: string;
  startedAt: string;
  lastMessageAt: string;
  messageCount: number;
  hasLead: boolean;
}
export interface ConversationList {
  total: number;
  items: ConversationListItem[];
}
export interface ConversationMessage {
  role: string;
  content: string;
  createdAt: string;
  topSimilarity: number | null;
  wasGrounded: boolean | null;
  modelUsed: string | null;
  latencyMs: number | null;
  titles: string[] | null;
}
export interface ConversationDetail {
  id: string;
  status: string;
  originUrl: string | null;
  startedAt: string;
  lastMessageAt: string;
  messages: ConversationMessage[];
  lead: {
    id: string;
    reason: string;
    status: string;
    contactName: string | null;
    contactEmail: string | null;
  } | null;
}
export interface Lead {
  id: string;
  conversationId: string;
  reason: string;
  status: string;
  contactName: string | null;
  contactEmail: string | null;
  contactPhone: string | null;
  visitorMessage: string | null;
  stumpingQuestion: string | null;
  createdAt: string;
}

async function json<T>(res: Response): Promise<T> {
  if (!res.ok)
    throw new Error(
      (await res.json().catch(() => ({}))).title ||
        `Request failed (${res.status})`,
    );
  return res.json();
}

export const getSummary = () =>
  apiFetch("/api/dashboard/summary").then(json<Summary>);
export const listConversations = (offset = 0, limit = 25) =>
  apiFetch(`/api/conversations?offset=${offset}&limit=${limit}`).then(
    json<ConversationList>,
  );
export const getConversation = (id: string) =>
  apiFetch(`/api/conversations/${id}`).then(json<ConversationDetail>);
export const deleteConversation = (id: string) =>
  apiFetch(`/api/conversations/${id}`, { method: "DELETE" });
export const listLeads = (status?: string) =>
  apiFetch(`/api/leads${status ? `?status=${status}` : ""}`).then(json<Lead[]>);
export const updateLeadStatus = (id: string, status: string) =>
  apiFetch(`/api/leads/${id}/status`, {
    method: "PATCH",
    body: JSON.stringify({ status }),
  });

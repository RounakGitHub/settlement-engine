import axios from "axios";
import type {
  Group,
  GroupPreview,
  Expense,
  UserBalance,
  Transfer,
  InitiateSettlementResult,
  ExpenseSplit,
} from "@/types";
import { useAuthStore } from "@/stores/auth-store";

const API_BASE = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

const api = axios.create({
  baseURL: `${API_BASE}/api`,
  withCredentials: true, // sends HttpOnly cookies automatically
  headers: { "Content-Type": "application/json" },
});

// No need to attach Authorization header — access token is in HttpOnly cookie.
// The browser sends it automatically with withCredentials: true.

// Silent refresh on 401 — skip for auth endpoints (login/register handle their own errors)
api.interceptors.response.use(
  (res) => res,
  async (error) => {
    const original = error.config;
    const isAuthEndpoint = original?.url?.startsWith("/auth/");

    if (error.response?.status === 401 && !original._retry && !isAuthEndpoint) {
      original._retry = true;
      try {
        const { data } = await axios.post<{ userId: string; userName: string }>(
          `${API_BASE}/api/auth/refresh`,
          {},
          { withCredentials: true }
        );
        useAuthStore.getState().setAuth(data.userId, data.userName);
        return api(original);
      } catch {
        useAuthStore.getState().logout();
        window.location.href = "/login";
        return Promise.reject(error);
      }
    }
    return Promise.reject(error);
  }
);

// Auth
export const authApi = {
  register: (name: string, email: string, password: string) =>
    api.post<{ userId: string; userName: string }>("/auth/register", { name, email, password }),

  login: (email: string, password: string) =>
    api.post<{ userId: string; userName: string }>("/auth/login", { email, password }),

  refresh: () => api.post<{ userId: string; userName: string }>("/auth/refresh"),

  logout: () => api.post("/auth/logout"),

  getProfile: () =>
    api.get<{ name: string; email: string }>("/auth/profile"),

  updateProfile: (name: string, password?: string) =>
    api.put("/auth/profile", { name, password: password || undefined }),

  deleteAccount: () =>
    api.delete("/auth/account"),
};

// Groups
export const groupsApi = {
  create: (name: string, currency: string, category: string) =>
    api.post<Group>("/groups", { name, currency, category }),

  preview: (code: string) =>
    api.get<GroupPreview>(`/groups/join/${code}`),

  join: (code: string) =>
    api.post(`/groups/join/${code}`),

  leave: (id: string) =>
    api.post(`/groups/${id}/leave`),

  delete: (id: string) =>
    api.delete(`/groups/${id}`),

  regenerateInvite: (id: string) =>
    api.post<{ inviteCode: string }>(`/groups/${id}/regenerate-invite`),

  getBalances: (id: string) =>
    api.get<UserBalance[]>(`/groups/${id}/balances`),

  getExpenses: (id: string) =>
    api.get<Expense[]>(`/groups/${id}/expenses`),

  getSettlementPlan: (id: string) =>
    api.get<Transfer[]>(`/groups/${id}/settlement-plan`),
};

// Expenses
export const expensesApi = {
  add: (
    groupId: string,
    data: {
      amountPaise: number;
      description: string;
      splitType: string;
      splits: { userId: string; amountPaise: number }[];
    }
  ) =>
    api.post<string>(`/groups/${groupId}/expenses`, data, {
      headers: { "X-Idempotency-Key": crypto.randomUUID() },
    }),

  edit: (
    groupId: string,
    expenseId: string,
    data: {
      amountPaise: number;
      description: string;
      splitType: string;
      splits: ExpenseSplit[];
    }
  ) => api.put(`/groups/${groupId}/expenses/${expenseId}`, data),

  delete: (groupId: string, expenseId: string) =>
    api.delete(`/groups/${groupId}/expenses/${expenseId}`),
};

// Settlements
export const settlementsApi = {
  initiate: (groupId: string, payeeId: string, amountPaise: number) =>
    api.post<InitiateSettlementResult>("/settlements/initiate", {
      groupId,
      payeeId,
      amountPaise,
    }, {
      headers: { "X-Idempotency-Key": crypto.randomUUID() },
    }),

  cancel: (id: string) =>
    api.post(`/settlements/${id}/cancel`),
};

export default api;

"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { groupsApi, expensesApi, settlementsApi } from "@/lib/api";
import { useAuthStore } from "@/stores/auth-store";
import { useSignalR } from "@/hooks/use-signalr";
import { formatPaise, getInitials, getMonthDay, groupByMonth, cn } from "@/lib/utils";
import { AddExpenseSheet } from "@/components/add-expense-sheet";
import { UserMenu } from "@/components/user-menu";
import { toast } from "@/stores/toast-store";
import { confirm } from "@/stores/confirm-store";
import api from "@/lib/api";
import type { Group, GroupMember, Expense, UserBalance, Transfer } from "@/types";

type Tab = "expenses" | "settle" | "members";

export default function GroupDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const queryClient = useQueryClient();
  const userId = useAuthStore((s) => s.userId);
  const [activeTab, setActiveTab] = useState<Tab>("expenses");
  const [showAddExpense, setShowAddExpense] = useState(false);
  const [settling, setSettling] = useState<string | null>(null);
  const [showInvite, setShowInvite] = useState(false);
  const [copied, setCopied] = useState(false);
  const [showGroupMenu, setShowGroupMenu] = useState(false);
  const [deletingExpense, setDeletingExpense] = useState<string | null>(null);
  const [leavingGroup, setLeavingGroup] = useState(false);
  const [deletingGroup, setDeletingGroup] = useState(false);
  const [regenerating, setRegenerating] = useState(false);
  const [inviteCode, setInviteCode] = useState<string | null>(null);

  useSignalR(id);

  const { data: group } = useQuery({
    queryKey: ["group", id],
    queryFn: async () => {
      const res = await api.get<Group[]>("/groups");
      return res.data.find((g) => g.id === id);
    },
  });

  const { data: members = [] } = useQuery({
    queryKey: ["members", id],
    queryFn: async () => {
      const res = await api.get<GroupMember[]>(`/groups/${id}/members`);
      return res.data;
    },
  });

  const { data: expenses = [], isLoading: expensesLoading } = useQuery({
    queryKey: ["expenses", id],
    queryFn: async () => {
      const res = await groupsApi.getExpenses(id);
      return res.data;
    },
  });

  const { data: balances = [] } = useQuery({
    queryKey: ["balances", id],
    queryFn: async () => {
      const res = await groupsApi.getBalances(id);
      return res.data;
    },
  });

  const { data: transfers = [] } = useQuery({
    queryKey: ["settlement-plan", id],
    queryFn: async () => {
      const res = await groupsApi.getSettlementPlan(id);
      return res.data;
    },
    enabled: activeTab === "settle",
  });

  const currentMember = members.find((m) => m.userId === userId);
  const isAdmin = currentMember?.role === "Admin";
  const effectiveInviteCode = inviteCode || group?.inviteCode;

  const getErrorMessage = (err: unknown, fallback: string) => {
    if (err && typeof err === "object" && "response" in err) {
      const axiosErr = err as { response?: { data?: { title?: string } } };
      return axiosErr.response?.data?.title || fallback;
    }
    return fallback;
  };

  const handleSettle = async (transfer: Transfer) => {
    setSettling(transfer.from + transfer.to);
    try {
      const res = await settlementsApi.initiate(id, transfer.to, transfer.amountPaise);
      toast.success(`Settlement initiated! Order: ${res.data.razorpayOrderId}`);
    } catch (err) {
      toast.error(getErrorMessage(err, "Failed to initiate settlement."));
    } finally {
      setSettling(null);
    }
  };

  const handleDeleteExpense = async (expenseId: string) => {
    const ok = await confirm({
      title: "Delete expense",
      message: "This expense will be permanently removed and balances will be recalculated.",
      confirmLabel: "Delete",
      destructive: true,
    });
    if (!ok) return;

    setDeletingExpense(expenseId);
    try {
      await expensesApi.delete(id, expenseId);
      queryClient.invalidateQueries({ queryKey: ["expenses", id] });
      queryClient.invalidateQueries({ queryKey: ["balances", id] });
      queryClient.invalidateQueries({ queryKey: ["settlement-plan", id] });
      toast.success("Expense deleted.");
    } catch (err) {
      toast.error(getErrorMessage(err, "Failed to delete expense."));
    } finally {
      setDeletingExpense(null);
    }
  };

  const handleLeaveGroup = async () => {
    const ok = await confirm({
      title: "Leave group",
      message: "You'll need a new invite to rejoin. If you're the last admin, promote someone else first.",
      confirmLabel: "Leave",
      destructive: true,
    });
    if (!ok) return;

    setLeavingGroup(true);
    try {
      await groupsApi.leave(id);
      toast.success("You left the group.");
      router.push("/dashboard");
    } catch (err) {
      toast.error(getErrorMessage(err, "Failed to leave group."));
    } finally {
      setLeavingGroup(false);
    }
  };

  const handleDeleteGroup = async () => {
    const ok = await confirm({
      title: "Delete group",
      message: `"${group?.name}" will be permanently deleted for all members. This cannot be undone.`,
      confirmLabel: "Delete group",
      destructive: true,
    });
    if (!ok) return;

    setDeletingGroup(true);
    try {
      await groupsApi.delete(id);
      toast.success("Group deleted.");
      router.push("/dashboard");
    } catch (err) {
      toast.error(getErrorMessage(err, "Failed to delete group."));
    } finally {
      setDeletingGroup(false);
    }
  };

  const handleRegenerateInvite = async () => {
    const ok = await confirm({
      title: "Regenerate invite",
      message: "The current invite link will stop working. A new one will be generated.",
      confirmLabel: "Regenerate",
    });
    if (!ok) return;

    setRegenerating(true);
    try {
      const res = await groupsApi.regenerateInvite(id);
      setInviteCode(res.data.inviteCode);
      queryClient.invalidateQueries({ queryKey: ["group", id] });
      toast.success("New invite link generated.");
    } catch (err) {
      toast.error(getErrorMessage(err, "Failed to regenerate invite."));
    } finally {
      setRegenerating(false);
    }
  };

  const totalSpent = expenses.reduce((s, e) => s + e.amountPaise, 0);
  const myBalance = balances.find((b) => b.userId === userId);
  const expensesByMonth = groupByMonth(expenses);

  return (
    <div className="min-h-screen bg-[var(--color-background)] pb-24 lg:pb-0">
      {/* ── Navbar ───────────────────────────── */}
      <header
        className="sticky top-0 z-30 backdrop-blur-lg border-b border-[var(--color-border-subtle)]"
        style={{ backgroundColor: "color-mix(in srgb, var(--color-surface) 85%, transparent)" }}
      >
        <div className="max-w-6xl mx-auto px-6 h-16 flex items-center gap-3">
          <button
            onClick={() => router.push("/dashboard")}
            className="w-9 h-9 rounded-lg flex items-center justify-center hover:bg-[var(--color-surface-hover)] text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)] transition-colors"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
              <path strokeLinecap="round" d="M15 19l-7-7 7-7" />
            </svg>
          </button>
          <div className="flex-1 min-w-0">
            <h1 className="text-lg font-bold text-[var(--color-text-primary)] truncate">{group?.name || "Group"}</h1>
            <p className="text-sm text-[var(--color-text-tertiary)]">{members.length} members {group?.currency ? `· ${group.currency}` : ""}</p>
          </div>

          {/* Group settings menu */}
          <div className="relative">
            <button
              onClick={() => setShowGroupMenu(!showGroupMenu)}
              className="w-9 h-9 rounded-lg flex items-center justify-center text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)] hover:bg-[var(--color-surface-hover)] transition-colors"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                <circle cx="12" cy="5" r="1" /><circle cx="12" cy="12" r="1" /><circle cx="12" cy="19" r="1" />
              </svg>
            </button>
            {showGroupMenu && (
              <>
                <div className="fixed inset-0 z-40" onClick={() => setShowGroupMenu(false)} />
                <div className="absolute right-0 top-11 w-56 card shadow-xl overflow-hidden animate-slide-down z-50 border border-[var(--color-border)]">
                  <button
                    onClick={() => { setShowInvite(true); setShowGroupMenu(false); }}
                    className="w-full text-left px-4 py-3 text-sm font-medium hover:bg-[var(--color-surface-hover)] transition-colors flex items-center gap-3"
                  >
                    <svg className="w-4 h-4 text-[var(--color-text-tertiary)]" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24"><path strokeLinecap="round" d="M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z" /></svg>
                    Invite people
                  </button>
                  {isAdmin && (
                    <button
                      onClick={() => { handleRegenerateInvite(); setShowGroupMenu(false); }}
                      disabled={regenerating}
                      className="w-full text-left px-4 py-3 text-sm font-medium hover:bg-[var(--color-surface-hover)] transition-colors flex items-center gap-3 disabled:opacity-50"
                    >
                      <svg className="w-4 h-4 text-[var(--color-text-tertiary)]" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24"><path strokeLinecap="round" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" /></svg>
                      Regenerate invite link
                    </button>
                  )}
                  <div className="border-t border-[var(--color-border-subtle)] my-1" />
                  <button
                    onClick={() => { handleLeaveGroup(); setShowGroupMenu(false); }}
                    disabled={leavingGroup}
                    className="w-full text-left px-4 py-3 text-sm font-medium text-[var(--color-destructive)] hover:bg-[var(--color-destructive-light)] transition-colors flex items-center gap-3 disabled:opacity-50"
                  >
                    <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24"><path strokeLinecap="round" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" /></svg>
                    Leave group
                  </button>
                  {isAdmin && (
                    <button
                      onClick={() => { handleDeleteGroup(); setShowGroupMenu(false); }}
                      disabled={deletingGroup}
                      className="w-full text-left px-4 py-3 text-sm font-medium text-[var(--color-destructive)] hover:bg-[var(--color-destructive-light)] transition-colors flex items-center gap-3 disabled:opacity-50"
                    >
                      <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24"><path strokeLinecap="round" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" /></svg>
                      Delete group
                    </button>
                  )}
                </div>
              </>
            )}
          </div>
          <UserMenu />
        </div>
      </header>

      {/* ── Hero — balance summary ────────────── */}
      <div className="relative overflow-hidden" style={{ background: "linear-gradient(135deg, var(--color-surface) 0%, var(--color-background) 100%)" }}>
        {/* Decorative blobs */}
        <div className="absolute inset-0 overflow-hidden pointer-events-none">
          <div className="absolute -top-20 -right-20 w-80 h-80 rounded-full blur-[100px] opacity-30" style={{ background: "var(--color-primary)" }} />
          <div className="absolute -bottom-16 left-[10%] w-60 h-60 rounded-full blur-[80px] opacity-15" style={{ background: "#6C3CE1" }} />
        </div>

        <div className="relative max-w-6xl mx-auto px-6 pt-8 pb-8">
          <div className="stagger-1">
            <p className="text-sm font-medium text-[var(--color-text-tertiary)] uppercase tracking-wider mb-1">Total group spending</p>
            <p className="text-4xl font-bold text-[var(--color-text-primary)]">
              {formatPaise(totalSpent)}
            </p>
            {myBalance && (
              <p className={cn(
                "text-base font-semibold mt-2",
                myBalance.netBalancePaise > 0 ? "text-[var(--color-success)]"
                  : myBalance.netBalancePaise < 0 ? "text-[var(--color-destructive)]"
                  : "text-[var(--color-text-tertiary)]"
              )}>
                {myBalance.netBalancePaise > 0
                  ? `You are owed ${formatPaise(myBalance.netBalancePaise)}`
                  : myBalance.netBalancePaise < 0
                  ? `You owe ${formatPaise(myBalance.netBalancePaise)}`
                  : "All settled up"}
              </p>
            )}
            {/* Action buttons */}
            <div className="flex gap-3 mt-5">
              <button
                onClick={() => setShowAddExpense(true)}
                className="bg-gradient-to-r from-[#6C3CE1] to-[#7B61FF] hover:from-[#7B4AEF] hover:to-[#8B71FF] text-white px-6 py-3 rounded-xl text-base font-semibold inline-flex items-center gap-2 shadow-lg shadow-[#6C3CE1]/20 transition-all hover:shadow-xl hover:-translate-y-0.5 active:translate-y-0"
              >
                <svg className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth={2.5} viewBox="0 0 24 24"><path strokeLinecap="round" d="M12 6v12m6-6H6" /></svg>
                Add expense
              </button>
              <button
                onClick={() => setActiveTab("settle")}
                className="border border-[var(--color-border)] hover:border-[var(--color-primary)] text-[var(--color-text-primary)] hover:text-[var(--color-primary)] px-6 py-3 rounded-xl text-base font-semibold transition-all hover:bg-[var(--color-primary-light)]"
              >
                Settle up
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* Invite Modal */}
      {showInvite && effectiveInviteCode && (
        <div className="fixed inset-0 z-50 flex items-center justify-center px-4">
          <div className="fixed inset-0 bg-[var(--color-overlay)] animate-fade-in" onClick={() => setShowInvite(false)} />
          <div className="relative bg-[var(--color-surface)] w-full max-w-sm rounded-2xl p-7 shadow-2xl text-center animate-scale-in">
            <div className="w-14 h-14 rounded-2xl bg-[var(--color-primary-light)] flex items-center justify-center mx-auto mb-4">
              <svg className="w-7 h-7 text-[var(--color-primary)]" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24"><path strokeLinecap="round" d="M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z" /></svg>
            </div>
            <h3 className="text-xl font-bold mb-1">Invite to {group?.name}</h3>
            <p className="text-sm text-[var(--color-text-secondary)] mb-5">
              Share this link with friends to join your group.
            </p>
            <div className="bg-[var(--color-background)] rounded-xl px-4 py-3.5 mb-5 border border-[var(--color-border-subtle)]">
              <p className="text-sm font-medium break-all">
                {typeof window !== "undefined" ? `${window.location.origin}/join/${effectiveInviteCode}` : ""}
              </p>
            </div>
            <div className="flex gap-3">
              <button
                onClick={() => setShowInvite(false)}
                className="flex-1 py-3 border border-[var(--color-border)] rounded-xl text-base font-medium hover:bg-[var(--color-surface-hover)] transition-colors"
              >
                Close
              </button>
              <button
                onClick={() => {
                  navigator.clipboard.writeText(`${window.location.origin}/join/${effectiveInviteCode}`);
                  setCopied(true);
                  setTimeout(() => setCopied(false), 2000);
                  toast.success("Link copied to clipboard!");
                }}
                className="flex-1 bg-gradient-to-r from-[#6C3CE1] to-[#7B61FF] hover:from-[#7B4AEF] hover:to-[#8B71FF] text-white py-3 rounded-xl text-base font-semibold shadow-lg shadow-[#6C3CE1]/20 transition-all"
              >
                {copied ? "Copied!" : "Copy Link"}
              </button>
            </div>
            {isAdmin && (
              <button
                onClick={handleRegenerateInvite}
                disabled={regenerating}
                className="mt-4 text-sm text-[var(--color-text-tertiary)] hover:text-[var(--color-text-secondary)] underline underline-offset-2 disabled:opacity-50"
              >
                Regenerate invite link
              </button>
            )}
          </div>
        </div>
      )}

      {/* ── Content ──────────────────────────── */}
      <main className="max-w-6xl mx-auto px-6 mt-6">
        <div className="flex flex-col lg:flex-row gap-6">
          {/* Left — main content */}
          <div className="flex-1 min-w-0">
            <div className="card overflow-hidden stagger-2">
              {/* Tabs */}
              <div className="flex border-b border-[var(--color-border-subtle)]">
                {([
                  { key: "expenses" as Tab, label: "Expenses", count: expenses.length },
                  { key: "settle" as Tab, label: "Settle Up" },
                  { key: "members" as Tab, label: "Members", count: members.length },
                ]).map((tab) => (
                  <button
                    key={tab.key}
                    onClick={() => setActiveTab(tab.key)}
                    className={cn(
                      "px-6 py-4 text-base font-medium transition-colors relative flex items-center gap-2",
                      activeTab === tab.key
                        ? "text-[var(--color-primary)]"
                        : "text-[var(--color-text-tertiary)] hover:text-[var(--color-text-secondary)]"
                    )}
                  >
                    {tab.label}
                    {tab.count !== undefined && (
                      <span className={cn(
                        "text-xs font-semibold px-2 py-0.5 rounded-full",
                        activeTab === tab.key
                          ? "bg-[#6C3CE1]/10 text-[#6C3CE1] dark:bg-[#A78BFA]/10 dark:text-[#A78BFA]"
                          : "bg-[var(--color-surface-hover)] text-[var(--color-text-tertiary)]"
                      )}>
                        {tab.count}
                      </span>
                    )}
                    {activeTab === tab.key && (
                      <span className="absolute bottom-0 left-0 right-0 h-[2px] bg-[var(--color-primary)] rounded-full" />
                    )}
                  </button>
                ))}
              </div>

              {/* Expenses tab */}
              {activeTab === "expenses" && (
                <div>
                  {expensesLoading ? (
                    <div>
                      {[1, 2, 3, 4].map((i) => (
                        <div key={i} className="flex items-center gap-4 px-6 py-5 border-b border-[var(--color-border-subtle)] last:border-0">
                          <div className="w-12 h-12 rounded-xl skeleton" />
                          <div className="flex-1">
                            <div className="h-4 skeleton rounded w-2/5 mb-2.5" />
                            <div className="h-3.5 skeleton rounded w-1/4" />
                          </div>
                          <div className="w-20 h-5 skeleton rounded" />
                        </div>
                      ))}
                    </div>
                  ) : expenses.length > 0 ? (
                    <div>
                      {expensesByMonth.map((monthGroup) => (
                        <div key={monthGroup.label}>
                          <div className="px-6 py-3 bg-[var(--color-surface-hover)]/50 border-b border-[var(--color-border-subtle)]">
                            <span className="text-sm font-semibold text-[var(--color-text-tertiary)] uppercase tracking-wider">{monthGroup.label}</span>
                          </div>
                          {monthGroup.items.map((expense: Expense) => {
                            const { month, day } = getMonthDay(expense.createdAt);
                            const iPaid = expense.paidBy === userId;
                            const myShare = expense.splits?.find((s) => s.userId === userId);
                            const lentAmount = iPaid && myShare ? expense.amountPaise - myShare.amountPaise : 0;
                            const owedAmount = !iPaid && myShare ? myShare.amountPaise : 0;
                            const canDelete = iPaid || isAdmin;

                            return (
                              <div
                                key={expense.id}
                                className="flex items-center px-6 py-4 border-b border-[var(--color-border-subtle)] last:border-0 hover:bg-[var(--color-surface-hover)]/30 transition-colors group/row"
                              >
                                <div className="w-12 text-center shrink-0 mr-4">
                                  <div className="text-xs text-[var(--color-text-tertiary)] uppercase leading-none">{month}</div>
                                  <div className="text-xl font-bold text-[var(--color-text-secondary)] leading-tight">{day}</div>
                                </div>
                                <div className="w-12 h-12 rounded-xl bg-[var(--color-primary-light)] text-[var(--color-primary)] flex items-center justify-center shrink-0 mr-4">
                                  <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M17 9V7a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2m2 4h10a2 2 0 002-2v-6a2 2 0 00-2-2H9a2 2 0 00-2 2v6a2 2 0 002 2zm7-5a2 2 0 11-4 0 2 2 0 014 0z" />
                                  </svg>
                                </div>
                                <div className="flex-1 min-w-0 mr-4">
                                  <p className="text-base font-medium text-[var(--color-text-primary)] truncate">{expense.description}</p>
                                  <p className="text-sm text-[var(--color-text-tertiary)] mt-0.5">
                                    {iPaid ? "You" : expense.paidByName.split(" ")[0]} paid {formatPaise(expense.amountPaise)}
                                  </p>
                                </div>
                                <div className="text-right shrink-0 mr-2">
                                  {iPaid && lentAmount > 0 ? (
                                    <>
                                      <p className="text-xs font-medium text-[var(--color-success)]">you lent</p>
                                      <p className="text-base font-bold text-[var(--color-success)]">
                                        {formatPaise(lentAmount)}
                                      </p>
                                    </>
                                  ) : !iPaid && owedAmount > 0 ? (
                                    <>
                                      <p className="text-xs font-medium text-[var(--color-destructive)]">you borrowed</p>
                                      <p className="text-base font-bold text-[var(--color-destructive)]">
                                        {formatPaise(owedAmount)}
                                      </p>
                                    </>
                                  ) : (
                                    <p className="text-xs font-medium text-[var(--color-text-tertiary)]">not involved</p>
                                  )}
                                </div>
                                {canDelete && (
                                  <button
                                    onClick={() => handleDeleteExpense(expense.id)}
                                    disabled={deletingExpense === expense.id}
                                    className="opacity-0 group-hover/row:opacity-100 transition-opacity shrink-0 w-9 h-9 flex items-center justify-center rounded-lg hover:bg-[var(--color-destructive-light)] text-[var(--color-text-tertiary)] hover:text-[var(--color-destructive)] disabled:opacity-50"
                                    title="Delete expense"
                                  >
                                    <svg className="w-4.5 h-4.5" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24">
                                      <path strokeLinecap="round" strokeLinejoin="round" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                    </svg>
                                  </button>
                                )}
                              </div>
                            );
                          })}
                        </div>
                      ))}
                    </div>
                  ) : (
                    <div className="text-center py-16 px-6">
                      <div className="w-16 h-16 bg-[var(--color-primary-light)] rounded-2xl flex items-center justify-center mx-auto mb-4">
                        <svg className="w-8 h-8 text-[var(--color-primary)]" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M12 6v6m0 0v6m0-6h6m-6 0H6" />
                        </svg>
                      </div>
                      <p className="text-lg font-semibold text-[var(--color-text-primary)] mb-1">No expenses yet</p>
                      <p className="text-sm text-[var(--color-text-tertiary)] mb-5">Add your first expense to get started.</p>
                      <button
                        onClick={() => setShowAddExpense(true)}
                        className="bg-gradient-to-r from-[#6C3CE1] to-[#7B61FF] hover:from-[#7B4AEF] hover:to-[#8B71FF] text-white px-6 py-3 rounded-xl text-base font-semibold shadow-lg shadow-[#6C3CE1]/20 transition-all hover:-translate-y-0.5 active:translate-y-0"
                      >
                        Add Expense
                      </button>
                    </div>
                  )}
                </div>
              )}

              {/* Settle tab */}
              {activeTab === "settle" && (
                <div className="p-6">
                  {transfers.length > 0 ? (
                    <div className="space-y-3">
                      {transfers.map((t: Transfer, i: number) => {
                        const isMe = t.from === userId;
                        return (
                          <div key={i} className="flex items-center gap-4 p-5 rounded-xl bg-[var(--color-background)] border border-[var(--color-border-subtle)]">
                            <div className="flex items-center -space-x-2">
                              <div className="w-10 h-10 rounded-full bg-[var(--color-destructive-light)] text-[var(--color-destructive)] flex items-center justify-center text-xs font-bold border-2 border-[var(--color-surface)] z-10">
                                {getInitials(t.fromName)}
                              </div>
                              <div className="w-10 h-10 rounded-full bg-[var(--color-success-light)] text-[var(--color-success)] flex items-center justify-center text-xs font-bold border-2 border-[var(--color-surface)]">
                                {getInitials(t.toName)}
                              </div>
                            </div>
                            <div className="flex-1 min-w-0">
                              <p className="text-base font-medium">
                                {t.fromName} <span className="text-[var(--color-text-tertiary)]">owes</span> {t.toName}
                              </p>
                              <p className="text-lg font-bold">{formatPaise(t.amountPaise)}</p>
                            </div>
                            {isMe && (
                              <button
                                onClick={() => handleSettle(t)}
                                disabled={settling === t.from + t.to}
                                className="bg-gradient-to-r from-[#6C3CE1] to-[#7B61FF] hover:from-[#7B4AEF] hover:to-[#8B71FF] text-white px-5 py-2.5 rounded-xl text-sm font-semibold disabled:opacity-50 shadow-lg shadow-[#6C3CE1]/20 transition-all"
                              >
                                {settling === t.from + t.to ? "..." : "Pay Now"}
                              </button>
                            )}
                          </div>
                        );
                      })}
                    </div>
                  ) : (
                    <div className="text-center py-16">
                      <div className="w-16 h-16 bg-[var(--color-success-light)] rounded-2xl flex items-center justify-center mx-auto mb-4">
                        <svg className="w-8 h-8 text-[var(--color-success)]" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                        </svg>
                      </div>
                      <p className="text-lg font-semibold text-[var(--color-text-primary)]">All settled up!</p>
                      <p className="text-sm text-[var(--color-text-tertiary)] mt-1">No pending transfers in this group.</p>
                    </div>
                  )}
                </div>
              )}

              {/* Members tab */}
              {activeTab === "members" && (
                <div>
                  {members.map((m: GroupMember, i: number) => (
                    <div
                      key={m.userId}
                      className={`flex items-center gap-4 px-6 py-4 ${
                        i > 0 ? "border-t border-[var(--color-border-subtle)]" : ""
                      }`}
                    >
                      <div className="w-11 h-11 rounded-xl bg-[#6C3CE1]/10 text-[#6C3CE1] dark:bg-[#A78BFA]/10 dark:text-[#A78BFA] flex items-center justify-center text-sm font-bold">
                        {getInitials(m.name)}
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className="text-base font-medium truncate">
                          {m.name}
                          {m.userId === userId && <span className="ml-1.5 text-xs text-[var(--color-primary)] font-semibold">(you)</span>}
                        </p>
                        <p className="text-sm text-[var(--color-text-tertiary)] truncate">{m.email}</p>
                      </div>
                      <span className={cn(
                        "text-xs font-semibold uppercase tracking-wider px-2.5 py-1 rounded-lg",
                        m.role === "Admin"
                          ? "text-[var(--color-primary)] bg-[var(--color-primary-light)]"
                          : "text-[var(--color-text-tertiary)] bg-[var(--color-surface-hover)]"
                      )}>
                        {m.role}
                      </span>
                    </div>
                  ))}
                  <div className="px-6 py-4 border-t border-[var(--color-border-subtle)]">
                    <button
                      onClick={() => setShowInvite(true)}
                      className="text-base font-medium text-[var(--color-primary)] hover:underline underline-offset-2 flex items-center gap-2"
                    >
                      <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2.5} viewBox="0 0 24 24"><path strokeLinecap="round" d="M12 6v12m6-6H6" /></svg>
                      Invite people
                    </button>
                  </div>
                </div>
              )}
            </div>
          </div>

          {/* ── Right sidebar ────────────────── */}
          <div className="w-full lg:w-[340px] shrink-0 space-y-5 stagger-3">
            {/* Group balances */}
            <div className="card overflow-hidden">
              <div className="px-6 py-4 border-b border-[var(--color-border-subtle)]">
                <h3 className="font-bold text-base">Group Balances</h3>
              </div>
              {balances.length > 0 ? (
                <div>
                  {balances.map((b: UserBalance, i: number) => (
                    <div
                      key={b.userId}
                      className={`flex items-center gap-4 px-6 py-4 ${
                        i > 0 ? "border-t border-[var(--color-border-subtle)]" : ""
                      }`}
                    >
                      <div className={cn(
                        "w-10 h-10 rounded-xl flex items-center justify-center text-xs font-bold",
                        b.netBalancePaise > 0
                          ? "bg-[var(--color-success-light)] text-[var(--color-success)]"
                          : b.netBalancePaise < 0
                          ? "bg-[var(--color-destructive-light)] text-[var(--color-destructive)]"
                          : "bg-[var(--color-surface-hover)] text-[var(--color-text-tertiary)]"
                      )}>
                        {getInitials(b.userName)}
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className="text-base font-medium truncate">{b.userName}</p>
                        <p className={cn(
                          "text-sm",
                          b.netBalancePaise > 0 ? "text-[var(--color-success)]"
                            : b.netBalancePaise < 0 ? "text-[var(--color-destructive)]"
                            : "text-[var(--color-text-tertiary)]"
                        )}>
                          {b.netBalancePaise > 0 ? "gets back" : b.netBalancePaise < 0 ? "owes" : "settled"}
                        </p>
                      </div>
                      <p className={cn(
                        "text-base font-bold",
                        b.netBalancePaise > 0 ? "text-[var(--color-success)]"
                          : b.netBalancePaise < 0 ? "text-[var(--color-destructive)]"
                          : "text-[var(--color-text-tertiary)]"
                      )}>
                        {formatPaise(b.netBalancePaise)}
                      </p>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="px-6 py-10 text-center">
                  <p className="text-sm text-[var(--color-text-tertiary)]">No balances yet</p>
                </div>
              )}
            </div>

            {/* Summary */}
            <div className="card p-6">
              <h3 className="font-bold text-base mb-4">Summary</h3>
              <div className="space-y-4">
                <div className="flex justify-between items-center">
                  <span className="text-base text-[var(--color-text-secondary)]">Total expenses</span>
                  <span className="text-base font-semibold">{expenses.length}</span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-base text-[var(--color-text-secondary)]">Members</span>
                  <span className="text-base font-semibold">{members.length}</span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-base text-[var(--color-text-secondary)]">Currency</span>
                  <span className="text-base font-semibold">{group?.currency || "—"}</span>
                </div>
              </div>
            </div>

            {/* Group actions */}
            <div className="card p-6">
              <h3 className="font-bold text-base mb-4">Group Settings</h3>
              <div className="space-y-1">
                <button
                  onClick={() => setShowInvite(true)}
                  className="w-full text-left text-base text-[var(--color-text-secondary)] hover:text-[var(--color-primary)] py-2.5 px-3 rounded-lg hover:bg-[var(--color-primary-light)] transition-all flex items-center gap-3"
                >
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24"><path strokeLinecap="round" d="M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z" /></svg>
                  Invite people
                </button>
                {isAdmin && (
                  <button
                    onClick={handleRegenerateInvite}
                    disabled={regenerating}
                    className="w-full text-left text-base text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)] py-2.5 px-3 rounded-lg hover:bg-[var(--color-surface-hover)] transition-all disabled:opacity-50 flex items-center gap-3"
                  >
                    <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24"><path strokeLinecap="round" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" /></svg>
                    Regenerate invite link
                  </button>
                )}
                <div className="border-t border-[var(--color-border-subtle)] my-2" />
                <button
                  onClick={handleLeaveGroup}
                  disabled={leavingGroup}
                  className="w-full text-left text-base text-[var(--color-destructive)] py-2.5 px-3 rounded-lg hover:bg-[var(--color-destructive-light)] transition-all disabled:opacity-50 flex items-center gap-3"
                >
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24"><path strokeLinecap="round" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" /></svg>
                  Leave group
                </button>
                {isAdmin && (
                  <button
                    onClick={handleDeleteGroup}
                    disabled={deletingGroup}
                    className="w-full text-left text-base text-[var(--color-destructive)] py-2.5 px-3 rounded-lg hover:bg-[var(--color-destructive-light)] transition-all disabled:opacity-50 flex items-center gap-3"
                  >
                    <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24"><path strokeLinecap="round" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" /></svg>
                    Delete group
                  </button>
                )}
              </div>
            </div>
          </div>
        </div>
      </main>

      {/* Mobile bottom bar */}
      <div className="lg:hidden fixed bottom-0 left-0 right-0 bg-[var(--color-surface)] border-t border-[var(--color-border)] px-4 py-4 z-20 flex gap-3">
        <button
          onClick={() => setShowAddExpense(true)}
          className="flex-1 bg-gradient-to-r from-[#6C3CE1] to-[#7B61FF] hover:from-[#7B4AEF] hover:to-[#8B71FF] text-white py-3 rounded-xl text-base font-semibold shadow-lg shadow-[#6C3CE1]/20"
        >
          Add expense
        </button>
        <button
          onClick={() => setActiveTab("settle")}
          className="flex-1 border border-[var(--color-primary)] text-[var(--color-primary)] py-3 rounded-xl text-base font-semibold hover:bg-[var(--color-primary-light)] transition-colors"
        >
          Settle up
        </button>
      </div>

      <AddExpenseSheet
        open={showAddExpense}
        onClose={() => setShowAddExpense(false)}
        groupId={id}
        members={members}
      />
    </div>
  );
}

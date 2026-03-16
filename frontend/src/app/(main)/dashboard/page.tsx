"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { useAuthStore } from "@/stores/auth-store";
import { getInitials } from "@/lib/utils";
import { CreateGroupSheet } from "@/components/create-group-sheet";
import { UserMenu } from "@/components/user-menu";
import api from "@/lib/api";
import type { Group } from "@/types";

const GROUP_COLORS = [
  { bg: "#6C3CE1", light: "rgba(108,60,225,0.1)" },
  { bg: "#0070BA", light: "rgba(0,112,186,0.1)" },
  { bg: "#00A3BF", light: "rgba(0,163,191,0.1)" },
  { bg: "#FF6B6B", light: "rgba(255,107,107,0.1)" },
  { bg: "#FFB74D", light: "rgba(255,183,77,0.1)" },
  { bg: "#00865A", light: "rgba(0,134,90,0.1)" },
  { bg: "#7B61FF", light: "rgba(123,97,255,0.1)" },
  { bg: "#E8A838", light: "rgba(232,168,56,0.1)" },
];

export default function DashboardPage() {
  const router = useRouter();
  const { isAuthenticated } = useAuthStore();
  const userName = useAuthStore((s) => s.userName) || "User";
  const [showCreate, setShowCreate] = useState(false);

  const { data: groups, isLoading } = useQuery({
    queryKey: ["groups"],
    queryFn: async () => {
      const res = await api.get<Group[]>("/groups");
      return res.data;
    },
    enabled: isAuthenticated,
  });

  const getColor = (i: number) => GROUP_COLORS[i % GROUP_COLORS.length];

  const greeting = () => {
    const hour = new Date().getHours();
    if (hour < 12) return "Good morning";
    if (hour < 18) return "Good afternoon";
    return "Good evening";
  };

  const totalMembers = groups?.reduce((s, g) => s + (g.memberCount || 0), 0) || 0;

  return (
    <div className="min-h-screen bg-[var(--color-background)]">
      {/* ── Navbar ───────────────────────────── */}
      <header
        className="sticky top-0 z-30 backdrop-blur-lg border-b border-[var(--color-border-subtle)]"
        style={{ backgroundColor: "color-mix(in srgb, var(--color-surface) 85%, transparent)" }}
      >
        <div className="max-w-6xl mx-auto px-6 h-16 flex items-center justify-between">
          <Link href="/dashboard" className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-lg bg-gradient-to-br from-[#6C3CE1] to-[#00A3BF] flex items-center justify-center shadow-sm">
              <span className="text-white text-base font-bold">S</span>
            </div>
            <span className="font-bold text-xl text-[var(--color-text-primary)]">Splitr</span>
          </Link>
          <UserMenu />
        </div>
      </header>

      {/* ── Hero ─────────────────────────────── */}
      <div className="relative overflow-hidden" style={{ background: "linear-gradient(135deg, var(--color-surface) 0%, var(--color-background) 100%)" }}>
        {/* Decorative blobs — visible in both themes */}
        <div className="absolute inset-0 overflow-hidden pointer-events-none">
          <div className="absolute -top-20 -right-20 w-80 h-80 rounded-full blur-[100px] opacity-30" style={{ background: "var(--color-primary)" }} />
          <div className="absolute -bottom-16 left-[10%] w-60 h-60 rounded-full blur-[80px] opacity-15" style={{ background: "#6C3CE1" }} />
        </div>

        <div className="relative max-w-6xl mx-auto px-6 pt-10 pb-8">
          <div className="flex flex-col sm:flex-row sm:items-end sm:justify-between gap-6 stagger-1">
            <div>
              <p className="text-[var(--color-text-tertiary)] text-base mb-1">{greeting()}</p>
              <h1 className="text-3xl sm:text-4xl font-bold text-[var(--color-text-primary)] leading-tight">
                {userName.split(" ")[0]}
              </h1>
            </div>
            <button
              onClick={() => setShowCreate(true)}
              className="bg-gradient-to-r from-[#6C3CE1] to-[#7B61FF] hover:from-[#7B4AEF] hover:to-[#8B71FF] text-white pl-5 pr-6 py-3 rounded-xl text-base font-semibold inline-flex items-center gap-2 shadow-lg shadow-[#6C3CE1]/20 transition-all hover:shadow-xl hover:shadow-[#6C3CE1]/30 hover:-translate-y-0.5 active:translate-y-0 shrink-0"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth={2.5} viewBox="0 0 24 24">
                <path strokeLinecap="round" d="M12 6v12m6-6H6" />
              </svg>
              New Group
            </button>
          </div>
        </div>
      </div>

      {/* ── Main content ─────────────────────── */}
      <main className="max-w-6xl mx-auto px-6 pt-6 pb-16">
        {/* Stats strip */}
        <div className="flex items-center gap-8 mb-8 stagger-2">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg flex items-center justify-center" style={{ background: "var(--color-primary-light)" }}>
              <svg className="w-5 h-5 text-[var(--color-primary)]" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
              </svg>
            </div>
            <div>
              <p className="text-2xl font-bold text-[var(--color-text-primary)] leading-none">{isLoading ? "—" : groups?.length || 0}</p>
              <p className="text-sm text-[var(--color-text-tertiary)]">Groups</p>
            </div>
          </div>
          <div className="w-px h-10 bg-[var(--color-border-subtle)]" />
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg flex items-center justify-center" style={{ background: "var(--color-success-light)" }}>
              <svg className="w-5 h-5 text-[var(--color-success)]" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" />
              </svg>
            </div>
            <div>
              <p className="text-2xl font-bold text-[var(--color-text-primary)] leading-none">{isLoading ? "—" : totalMembers}</p>
              <p className="text-sm text-[var(--color-text-tertiary)]">Members</p>
            </div>
          </div>
        </div>

        {/* Section header */}
        <div className="mb-5 stagger-3">
          <h2 className="text-xl font-bold text-[var(--color-text-primary)]">Your Groups</h2>
        </div>

        {/* ── Loading skeleton ─────────────── */}
        {isLoading ? (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {[1, 2, 3, 4].map((i) => (
              <div key={i} className="card p-5 flex items-center gap-4">
                <div className="w-14 h-14 rounded-2xl skeleton shrink-0" />
                <div className="flex-1 min-w-0">
                  <div className="h-5 skeleton rounded w-2/3 mb-2.5" />
                  <div className="h-4 skeleton rounded w-1/3" />
                </div>
                <div className="w-8 h-8 skeleton rounded-lg shrink-0" />
              </div>
            ))}
          </div>
        ) : groups && groups.length > 0 ? (
          /* ── Groups list ─────────────────── */
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 stagger-3">
            {groups.map((group, i) => {
              const color = getColor(i);
              return (
                <button
                  key={group.id}
                  onClick={() => router.push(`/groups/${group.id}`)}
                  className="card card-hover flex items-center gap-4 p-5 text-left group"
                >
                  {/* Color-coded avatar */}
                  <div
                    className="w-14 h-14 rounded-2xl flex items-center justify-center text-lg font-bold text-white shrink-0 transition-transform group-hover:scale-105"
                    style={{ backgroundColor: color.bg }}
                  >
                    {getInitials(group.name)}
                  </div>

                  {/* Info */}
                  <div className="flex-1 min-w-0">
                    <p className="text-lg font-semibold text-[var(--color-text-primary)] truncate group-hover:text-[var(--color-primary)] transition-colors">
                      {group.name}
                    </p>
                    <div className="flex items-center gap-2 mt-1">
                      <span className="text-sm text-[var(--color-text-tertiary)]">
                        {group.memberCount || 0} member{(group.memberCount || 0) !== 1 ? "s" : ""}
                      </span>
                      {group.category && (
                        <>
                          <span className="text-[var(--color-border)]">&middot;</span>
                          <span className="text-sm text-[var(--color-text-tertiary)]">{group.category}</span>
                        </>
                      )}
                      <span className="text-[var(--color-border)]">&middot;</span>
                      <span className="text-sm font-medium" style={{ color: color.bg }}>{group.currency}</span>
                    </div>
                  </div>

                  {/* Arrow */}
                  <div className="w-9 h-9 rounded-lg flex items-center justify-center shrink-0 bg-[var(--color-surface-hover)] group-hover:bg-[var(--color-primary-light)] transition-colors">
                    <svg className="w-4 h-4 text-[var(--color-text-tertiary)] group-hover:text-[var(--color-primary)] group-hover:translate-x-0.5 transition-all" fill="none" stroke="currentColor" strokeWidth={2.5} viewBox="0 0 24 24">
                      <path strokeLinecap="round" d="M9 5l7 7-7 7" />
                    </svg>
                  </div>
                </button>
              );
            })}

            {/* Inline create card */}
            <button
              onClick={() => setShowCreate(true)}
              className="card flex items-center gap-4 p-5 text-left group border border-dashed border-[var(--color-border)] hover:border-[var(--color-primary)] transition-all hover:shadow-md"
            >
              <div className="w-14 h-14 rounded-2xl flex items-center justify-center shrink-0 bg-[var(--color-primary-light)] group-hover:bg-[var(--color-primary)] transition-colors">
                <svg className="w-6 h-6 text-[var(--color-primary)] group-hover:text-white transition-colors" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                  <path strokeLinecap="round" d="M12 6v12m6-6H6" />
                </svg>
              </div>
              <div className="flex-1">
                <p className="text-lg font-semibold text-[var(--color-text-primary)] group-hover:text-[var(--color-primary)] transition-colors">Create a new group</p>
                <p className="text-sm text-[var(--color-text-tertiary)]">Trips, roommates, dinners — anything</p>
              </div>
            </button>
          </div>
        ) : (
          /* ── Empty state ─────────────────── */
          <div className="stagger-3">
            {/* Hero empty card */}
            <div className="card overflow-hidden">
              {/* Gradient header */}
              <div className="relative px-8 pt-12 pb-10 text-center overflow-hidden" style={{ background: "linear-gradient(135deg, #6C3CE1, #7B61FF, #00A3BF)" }}>
                <div className="absolute inset-0 opacity-10" style={{ backgroundImage: "radial-gradient(circle, white 1px, transparent 1px)", backgroundSize: "20px 20px" }} />
                <div className="relative">
                  <div className="w-20 h-20 rounded-3xl bg-white/20 backdrop-blur-sm flex items-center justify-center mx-auto mb-5">
                    <svg className="w-10 h-10 text-white" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
                    </svg>
                  </div>
                  <h3 className="text-2xl font-bold text-white mb-2">Create your first group</h3>
                  <p className="text-white/70 text-base max-w-sm mx-auto leading-relaxed">
                    Start splitting expenses with friends, roommates, or travel buddies. It takes just 30 seconds.
                  </p>
                </div>
              </div>

              {/* Action area */}
              <div className="px-8 py-8 text-center">
                <button
                  onClick={() => setShowCreate(true)}
                  className="bg-gradient-to-r from-[#6C3CE1] to-[#7B61FF] hover:from-[#7B4AEF] hover:to-[#8B71FF] text-white px-10 py-4 rounded-xl text-lg font-semibold inline-flex items-center gap-2.5 shadow-lg shadow-[#6C3CE1]/20 transition-all hover:shadow-xl hover:shadow-[#6C3CE1]/30 hover:-translate-y-0.5 active:translate-y-0"
                >
                  <svg className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth={2.5} viewBox="0 0 24 24">
                    <path strokeLinecap="round" d="M12 6v12m6-6H6" />
                  </svg>
                  Create Group
                </button>
              </div>
            </div>

            {/* Feature cards below */}
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mt-6">
              <div className="card p-5 flex items-start gap-4">
                <div className="w-11 h-11 rounded-xl flex items-center justify-center shrink-0" style={{ background: "rgba(108,60,225,0.1)" }}>
                  <svg className="w-5 h-5 text-[#6C3CE1]" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M12 6v12m-3-2.818l.879.659c1.171.879 3.07.879 4.242 0 1.172-.879 1.172-2.303 0-3.182C13.536 12.219 12.768 12 12 12c-.725 0-1.45-.22-2.003-.659-1.106-.879-1.106-2.303 0-3.182s2.9-.879 4.006 0l.415.33M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                </div>
                <div>
                  <p className="font-semibold text-[var(--color-text-primary)] mb-0.5">Smart Splits</p>
                  <p className="text-sm text-[var(--color-text-tertiary)] leading-relaxed">Our algorithm minimizes total payments with debt simplification.</p>
                </div>
              </div>

              <div className="card p-5 flex items-start gap-4">
                <div className="w-11 h-11 rounded-xl flex items-center justify-center shrink-0" style={{ background: "rgba(0,163,191,0.1)" }}>
                  <svg className="w-5 h-5 text-[#00A3BF]" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M13 10V3L4 14h7v7l9-11h-7z" />
                  </svg>
                </div>
                <div>
                  <p className="font-semibold text-[var(--color-text-primary)] mb-0.5">Real-Time Sync</p>
                  <p className="text-sm text-[var(--color-text-tertiary)] leading-relaxed">Every update syncs instantly across all devices. No refresh needed.</p>
                </div>
              </div>

              <div className="card p-5 flex items-start gap-4">
                <div className="w-11 h-11 rounded-xl flex items-center justify-center shrink-0" style={{ background: "rgba(0,134,90,0.1)" }}>
                  <svg className="w-5 h-5 text-[#00865A]" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                </div>
                <div>
                  <p className="font-semibold text-[var(--color-text-primary)] mb-0.5">1-Tap Settle</p>
                  <p className="text-sm text-[var(--color-text-tertiary)] leading-relaxed">See who owes whom and clear debts with a single tap.</p>
                </div>
              </div>
            </div>
          </div>
        )}
      </main>

      <CreateGroupSheet open={showCreate} onClose={() => setShowCreate(false)} />
    </div>
  );
}

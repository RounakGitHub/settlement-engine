"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { groupsApi } from "@/lib/api";

export default function JoinGroupPage() {
  const { code } = useParams<{ code: string }>();
  const router = useRouter();
  const [joining, setJoining] = useState(false);
  const [error, setError] = useState("");

  const { data: preview, isLoading, isError } = useQuery({
    queryKey: ["group-preview", code],
    queryFn: async () => {
      const res = await groupsApi.preview(code);
      return res.data;
    },
  });

  const handleJoin = async () => {
    setError("");
    setJoining(true);
    try {
      await groupsApi.join(code);
      router.push("/dashboard");
    } catch {
      setError("Failed to join group. You may need to sign in first.");
    } finally {
      setJoining(false);
    }
  };

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-[var(--color-background)]">
        <div className="w-10 h-10 border-2 border-[var(--color-primary)] border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  if (isError || !preview) {
    return (
      <div className="min-h-screen flex items-center justify-center px-4 bg-[var(--color-background)]">
        <div className="card text-center p-10 max-w-sm w-full animate-scale-in">
          <div className="w-16 h-16 bg-[var(--color-destructive-light)] rounded-2xl flex items-center justify-center mx-auto mb-4">
            <svg className="w-8 h-8 text-[var(--color-destructive)]" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </div>
          <h2 className="text-xl font-bold mb-2">Invalid Invite</h2>
          <p className="text-base text-[var(--color-text-secondary)]">
            This invite link is invalid or has expired.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex items-center justify-center px-4 bg-[var(--color-background)]">
      <div className="card w-full max-w-sm p-10 text-center animate-scale-in">
        <div className="w-16 h-16 bg-[var(--color-primary-light)] rounded-2xl flex items-center justify-center mx-auto mb-5">
          <svg className="w-8 h-8 text-[var(--color-primary)]" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
          </svg>
        </div>
        <h2 className="text-2xl font-bold mb-1">{preview.name}</h2>
        <p className="text-base text-[var(--color-text-secondary)] mb-8">
          {preview.memberCount} members &middot; {preview.currency}
        </p>

        {error && (
          <div className="bg-[var(--color-destructive-light)] text-[var(--color-destructive)] text-base p-4 rounded-xl mb-5">
            {error}
          </div>
        )}

        <button
          onClick={handleJoin}
          disabled={joining}
          className="w-full bg-gradient-to-r from-[#6C3CE1] to-[#7B61FF] hover:from-[#7B4AEF] hover:to-[#8B71FF] text-white py-3.5 rounded-xl text-base font-semibold disabled:opacity-50 shadow-lg shadow-[#6C3CE1]/20 transition-all hover:-translate-y-0.5 active:translate-y-0"
        >
          {joining ? "Joining..." : "Join Group"}
        </button>
      </div>
    </div>
  );
}

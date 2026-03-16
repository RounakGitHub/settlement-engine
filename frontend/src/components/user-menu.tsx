"use client";

import { useState, useRef, useEffect } from "react";
import { useRouter } from "next/navigation";
import { useTheme } from "next-themes";
import { useAuthStore } from "@/stores/auth-store";
import { authApi } from "@/lib/api";
import { getInitials } from "@/lib/utils";
import { EditProfileSheet } from "@/components/edit-profile-sheet";

export function UserMenu() {
  const [open, setOpen] = useState(false);
  const [showEditProfile, setShowEditProfile] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const router = useRouter();
  const { theme, setTheme } = useTheme();
  const logout = useAuthStore((s) => s.logout);
  const userName = useAuthStore((s) => s.userName) || "User";

  useEffect(() => {
    const handleClick = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, []);

  const handleLogout = async () => {
    try { await authApi.logout(); } catch { /* ignore */ }
    logout();
    router.push("/login");
  };

  const cycleTheme = () => {
    if (theme === "dark") setTheme("light");
    else if (theme === "light") setTheme("system");
    else setTheme("dark");
  };

  const themeIcon = theme === "dark" ? (
    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z" />
    </svg>
  ) : theme === "light" ? (
    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z" />
    </svg>
  ) : (
    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
    </svg>
  );

  const themeLabel = theme === "dark" ? "Dark" : theme === "light" ? "Light" : "Auto";

  return (
    <>
      <div className="relative" ref={ref}>
        <button
          onClick={() => setOpen(!open)}
          className="flex items-center gap-2.5 pl-1 pr-3 py-1 rounded-full border border-[var(--color-border)] hover:border-[var(--color-border-subtle)] hover:bg-[var(--color-surface-hover)] transition-colors"
        >
          <div className="w-8 h-8 rounded-full bg-gradient-to-br from-[#6C3CE1] to-[#7B61FF] text-white flex items-center justify-center text-xs font-bold shadow-sm">
            {getInitials(userName)}
          </div>
          <span className="text-sm font-medium text-[var(--color-text-primary)] hidden sm:block max-w-[100px] truncate">
            {userName.split(" ")[0]}
          </span>
          <svg className="w-3.5 h-3.5 text-[var(--color-text-tertiary)]" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
            <path strokeLinecap="round" d="M19 9l-7 7-7-7" />
          </svg>
        </button>

        {open && (
          <div className="absolute right-0 top-12 w-56 card shadow-lg overflow-hidden animate-slide-down z-50">
            <div className="px-4 py-3 border-b border-[var(--color-border-subtle)]">
              <p className="text-sm font-semibold truncate">{userName}</p>
            </div>

            <div className="py-1">
              <button
                onClick={() => { setShowEditProfile(true); setOpen(false); }}
                className="w-full text-left px-4 py-2.5 text-sm text-[var(--color-text-secondary)] hover:bg-[var(--color-surface-hover)] transition-colors flex items-center gap-3"
              >
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                </svg>
                Edit profile
              </button>

              <button
                onClick={cycleTheme}
                className="w-full text-left px-4 py-2.5 text-sm text-[var(--color-text-secondary)] hover:bg-[var(--color-surface-hover)] transition-colors flex items-center gap-3"
              >
                {themeIcon}
                <span className="flex-1">Theme</span>
                <span className="text-xs font-medium text-[var(--color-text-tertiary)] bg-[var(--color-surface-hover)] px-2 py-0.5 rounded">
                  {themeLabel}
                </span>
              </button>

              <div className="border-t border-[var(--color-border-subtle)] my-1" />

              <button
                onClick={handleLogout}
                className="w-full text-left px-4 py-2.5 text-sm text-[var(--color-destructive)] hover:bg-[var(--color-destructive-light)] transition-colors flex items-center gap-3"
              >
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
                </svg>
                Log out
              </button>
            </div>
          </div>
        )}
      </div>

      <EditProfileSheet open={showEditProfile} onClose={() => setShowEditProfile(false)} />
    </>
  );
}

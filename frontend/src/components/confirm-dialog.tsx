"use client";

import { useConfirmStore } from "@/stores/confirm-store";
import { cn } from "@/lib/utils";

export function ConfirmDialog() {
  const { open, title, message, confirmLabel, destructive, accept, cancel } = useConfirmStore();

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-[200] flex items-center justify-center px-4">
      <div className="fixed inset-0 bg-[var(--color-overlay)] animate-fade-in" onClick={cancel} />
      <div className="relative bg-[var(--color-surface)] w-full max-w-sm rounded-2xl p-7 shadow-2xl animate-scale-in">
        <h3 className="text-xl font-bold mb-2">{title}</h3>
        <p className="text-base text-[var(--color-text-secondary)] mb-6 whitespace-pre-line leading-relaxed">{message}</p>
        <div className="flex flex-col-reverse gap-3">
          <button
            onClick={cancel}
            className="w-full py-3 border border-[var(--color-border)] rounded-xl text-base font-medium hover:bg-[var(--color-surface-hover)] transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={accept}
            className={cn(
              "w-full py-3 rounded-xl text-base font-semibold text-white transition-colors",
              destructive
                ? "bg-[var(--color-destructive)] hover:brightness-110"
                : "bg-gradient-to-r from-[#6C3CE1] to-[#7B61FF] hover:from-[#7B4AEF] hover:to-[#8B71FF] shadow-lg shadow-[#6C3CE1]/20"
            )}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}

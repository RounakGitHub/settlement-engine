"use client";

import { useState, useEffect, useCallback, useRef } from "react";
import { useForm } from "react-hook-form";
import { expensesApi } from "@/lib/api";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "@/stores/toast-store";
import { getInitials } from "@/lib/utils";
import type { GroupMember } from "@/types";

const MEMBER_COLORS = [
  "#6C3CE1", "#00A3BF", "#FF6B6B", "#FFB74D", "#00865A", "#7B61FF", "#E8A838", "#0070BA",
];

/* Isolated slider row — keeps local state during drag, only propagates on commit */
function SliderRow({ name, color, initials, value, max, step, unit, secondaryLabel, onChange }: {
  name: string;
  color: string;
  initials: string;
  value: number;
  max: number;
  step: number;
  unit?: string;
  secondaryLabel?: string;
  onChange: (val: string) => void;
}) {
  const [local, setLocal] = useState(value);
  const dragging = useRef(false);

  // Sync from parent when not dragging
  useEffect(() => {
    if (!dragging.current) setLocal(value);
  }, [value]);

  const pct = max > 0 ? (local / max) * 100 : 0;

  return (
    <div className="p-3 rounded-xl border border-[var(--color-border-subtle)] bg-[var(--color-background)]">
      <div className="flex items-center gap-2.5 mb-2">
        <div
          className="w-7 h-7 rounded-md flex items-center justify-center text-white text-[10px] font-bold shrink-0"
          style={{ backgroundColor: color }}
        >
          {initials}
        </div>
        <span className="text-sm font-medium flex-1 truncate">{name}</span>
        <span className="text-base font-bold tabular-nums">
          {unit === "%" ? `${local.toFixed(1)}%` : local.toFixed(2)}
        </span>
        {secondaryLabel && (
          <span className="text-xs text-[var(--color-text-tertiary)] tabular-nums">{secondaryLabel}</span>
        )}
      </div>
      <input
        type="range"
        min="0"
        max={max || 100}
        step={step}
        value={local}
        onPointerDown={() => { dragging.current = true; }}
        onInput={(e) => {
          const v = (e.target as HTMLInputElement).value;
          setLocal(parseFloat(v));
        }}
        onPointerUp={() => {
          dragging.current = false;
          onChange(String(local));
        }}
        onChange={(e) => {
          // For keyboard / accessibility — commit immediately
          if (!dragging.current) {
            setLocal(parseFloat(e.target.value));
            onChange(e.target.value);
          }
        }}
        className="w-full h-2 rounded-full appearance-none cursor-pointer"
        style={{
          background: `linear-gradient(to right, ${color} ${pct}%, var(--color-border-subtle) ${pct}%)`,
        }}
      />
    </div>
  );
}

interface Props {
  open: boolean;
  onClose: () => void;
  groupId: string;
  members: GroupMember[];
}

interface FormData {
  description: string;
  amount: string;
  splitType: "Equal" | "Exact" | "Percentage";
}

export function AddExpenseSheet({ open, onClose, groupId, members }: Props) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [selectedMembers, setSelectedMembers] = useState<string[]>(
    members.map((m) => m.userId)
  );
  const [exactAmounts, setExactAmounts] = useState<Record<string, string>>({});
  const [percentages, setPercentages] = useState<Record<string, string>>({});
  const queryClient = useQueryClient();
  const { register, handleSubmit, reset, watch, formState: { errors } } = useForm<FormData>({
    defaultValues: { splitType: "Equal" },
  });

  const splitType = watch("splitType");
  const amountStr = watch("amount");

  useEffect(() => {
    setSelectedMembers(members.map((m) => m.userId));
  }, [members]);

  if (!open) return null;

  const toggleMember = (userId: string) => {
    setSelectedMembers((prev) =>
      prev.includes(userId) ? prev.filter((id) => id !== userId) : [...prev, userId]
    );
  };

  const totalAmount = Math.round(parseFloat(amountStr || "0") * 100);

  const getExactTotal = () =>
    selectedMembers.reduce((sum, id) => sum + Math.round(parseFloat(exactAmounts[id] || "0") * 100), 0);

  const getPercentTotal = () =>
    selectedMembers.reduce((sum, id) => sum + parseFloat(percentages[id] || "0"), 0);

  const onSubmit = async (data: FormData) => {
    setError("");
    const amountPaise = Math.round(parseFloat(data.amount) * 100);

    if (isNaN(amountPaise) || amountPaise <= 0) {
      setError("Enter a valid amount.");
      return;
    }

    if (selectedMembers.length === 0) {
      setError("Select at least one member.");
      return;
    }

    let splits: { userId: string; amountPaise: number }[];

    if (splitType === "Equal") {
      const perPerson = Math.floor(amountPaise / selectedMembers.length);
      const remainder = amountPaise - perPerson * selectedMembers.length;
      splits = selectedMembers.map((userId, i) => ({
        userId,
        amountPaise: perPerson + (i < remainder ? 1 : 0),
      }));
    } else if (splitType === "Exact") {
      splits = selectedMembers.map((userId) => ({
        userId,
        amountPaise: Math.round(parseFloat(exactAmounts[userId] || "0") * 100),
      }));
      const splitTotal = splits.reduce((s, x) => s + x.amountPaise, 0);
      if (splitTotal !== amountPaise) {
        setError(`Split amounts total ${(splitTotal / 100).toFixed(2)} but expense is ${(amountPaise / 100).toFixed(2)}.`);
        return;
      }
    } else {
      const totalPct = getPercentTotal();
      if (Math.abs(totalPct - 100) > 0.01) {
        setError(`Percentages total ${totalPct.toFixed(1)}% — must equal 100%.`);
        return;
      }
      let assigned = 0;
      splits = selectedMembers.map((userId, i) => {
        const pct = parseFloat(percentages[userId] || "0");
        let share: number;
        if (i === selectedMembers.length - 1) {
          share = amountPaise - assigned;
        } else {
          share = Math.round((pct / 100) * amountPaise);
          assigned += share;
        }
        return { userId, amountPaise: share };
      });
    }

    setLoading(true);
    try {
      await expensesApi.add(groupId, {
        amountPaise,
        description: data.description,
        splitType: data.splitType,
        splits,
      });
      queryClient.invalidateQueries({ queryKey: ["expenses", groupId] });
      queryClient.invalidateQueries({ queryKey: ["balances", groupId] });
      queryClient.invalidateQueries({ queryKey: ["settlement-plan", groupId] });
      reset();
      setSelectedMembers(members.map((m) => m.userId));
      setExactAmounts({});
      setPercentages({});
      onClose();
      toast.success("Expense added!");
    } catch {
      setError("Failed to add expense.");
    } finally {
      setLoading(false);
    }
  };

  const getMemberName = (userId: string) =>
    members.find((m) => m.userId === userId)?.name || "Unknown";

  return (
    <div className="fixed inset-0 z-50 flex items-end sm:items-center justify-center">
      <div className="fixed inset-0 bg-[var(--color-overlay)] animate-fade-in" onClick={onClose} />
      <div className="relative bg-[var(--color-surface)] w-full max-w-md max-h-[90vh] overflow-y-auto rounded-t-2xl sm:rounded-2xl p-7 shadow-2xl animate-sheet-up">
        <h2 className="text-xl font-bold mb-6">Add Expense</h2>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
          {error && (
            <div className="bg-[var(--color-destructive-light)] text-[var(--color-destructive)] text-base p-4 rounded-xl">
              {error}
            </div>
          )}

          <div>
            <label className="block text-base font-medium mb-1.5">Amount (INR)</label>
            <input
              type="number"
              step="0.01"
              {...register("amount", { required: "Amount is required" })}
              className="w-full px-4 py-3.5 border border-[var(--color-border)] rounded-xl text-2xl text-center font-semibold focus:outline-none focus:ring-2 focus:ring-[var(--color-primary)]"
              placeholder="0.00"
            />
            {errors.amount && <p className="text-[var(--color-destructive)] text-sm mt-1">{errors.amount.message}</p>}
          </div>

          <div>
            <label className="block text-base font-medium mb-1.5">Description</label>
            <input
              {...register("description", { required: "Description is required", maxLength: 500 })}
              className="w-full px-4 py-3 border border-[var(--color-border)] rounded-xl text-base focus:outline-none focus:ring-2 focus:ring-[var(--color-primary)]"
              placeholder="e.g. Dinner at restaurant"
            />
            {errors.description && <p className="text-[var(--color-destructive)] text-sm mt-1">{errors.description.message}</p>}
          </div>

          <div>
            <label className="block text-base font-medium mb-2">Split Type</label>
            <div className="flex gap-2">
              {(["Equal", "Exact", "Percentage"] as const).map((type) => (
                <label
                  key={type}
                  className={`flex-1 text-center py-2.5 rounded-xl text-base font-medium cursor-pointer border transition-all ${
                    splitType === type
                      ? "bg-gradient-to-r from-[#6C3CE1] to-[#7B61FF] text-white border-transparent shadow-md shadow-[#6C3CE1]/20"
                      : "border-[var(--color-border)] hover:bg-[var(--color-surface-hover)]"
                  }`}
                >
                  <input
                    type="radio"
                    value={type}
                    {...register("splitType")}
                    className="sr-only"
                  />
                  {type}
                </label>
              ))}
            </div>
          </div>

          {/* ── Split Among — Equal mode ─────── */}
          {splitType === "Equal" && (
            <div>
              <div className="flex items-center justify-between mb-3">
                <label className="text-base font-medium">Split among</label>
                <span className="text-sm text-[var(--color-text-tertiary)]">
                  {selectedMembers.length} of {members.length} selected
                </span>
              </div>
              <div className="grid grid-cols-2 gap-2 max-h-52 overflow-y-auto">
                {members.map((m, i) => {
                  const selected = selectedMembers.includes(m.userId);
                  const color = MEMBER_COLORS[i % MEMBER_COLORS.length];
                  return (
                    <button
                      type="button"
                      key={m.userId}
                      onClick={() => toggleMember(m.userId)}
                      className={`flex items-center gap-2.5 p-3 rounded-xl border transition-all text-left ${
                        selected
                          ? "border-[var(--color-primary)] bg-[var(--color-primary-light)]"
                          : "border-[var(--color-border)] opacity-50 hover:opacity-75"
                      }`}
                    >
                      <div
                        className="w-8 h-8 rounded-lg flex items-center justify-center text-white text-xs font-bold shrink-0"
                        style={{ backgroundColor: selected ? color : "var(--color-text-tertiary)" }}
                      >
                        {getInitials(m.name)}
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium truncate">{m.name.split(" ")[0]}</p>
                        {selected && totalAmount > 0 && (
                          <p className="text-xs text-[var(--color-text-tertiary)]">
                            {(Math.floor(totalAmount / selectedMembers.length) / 100).toFixed(2)}
                          </p>
                        )}
                      </div>
                      {selected && (
                        <svg className="w-4 h-4 text-[var(--color-primary)] shrink-0" fill="none" stroke="currentColor" strokeWidth={3} viewBox="0 0 24 24">
                          <path strokeLinecap="round" d="M5 13l4 4L19 7" />
                        </svg>
                      )}
                    </button>
                  );
                })}
              </div>
            </div>
          )}

          {/* ── Split Among — Exact mode ──────── */}
          {splitType === "Exact" && (
            <div>
              <div className="flex items-center justify-between mb-3">
                <label className="text-base font-medium">Amount per person</label>
                <span className={`text-sm font-semibold ${
                  totalAmount > 0 && getExactTotal() === totalAmount
                    ? "text-[var(--color-success)]"
                    : "text-[var(--color-text-secondary)]"
                }`}>
                  {(getExactTotal() / 100).toFixed(2)} / {(totalAmount / 100).toFixed(2)}
                </span>
              </div>
              {/* Progress bar */}
              <div className="h-1.5 rounded-full bg-[var(--color-border-subtle)] mb-3 overflow-hidden">
                <div
                  className={`h-full rounded-full transition-all duration-300 ${
                    totalAmount > 0 && getExactTotal() === totalAmount
                      ? "bg-[var(--color-success)]"
                      : "bg-[var(--color-primary)]"
                  }`}
                  style={{ width: `${totalAmount > 0 ? Math.min((getExactTotal() / totalAmount) * 100, 100) : 0}%` }}
                />
              </div>
              <div className="space-y-3 max-h-60 overflow-y-auto">
                {members.filter((m) => selectedMembers.includes(m.userId)).map((m) => {
                  const color = MEMBER_COLORS[members.indexOf(m) % MEMBER_COLORS.length];
                  return (
                    <SliderRow
                      key={m.userId}
                      name={m.name}
                      color={color}
                      initials={getInitials(m.name)}
                      value={parseFloat(exactAmounts[m.userId] || "0")}
                      max={totalAmount / 100 || 100}
                      step={0.01}
                      onChange={(v) => setExactAmounts((prev) => ({ ...prev, [m.userId]: v }))}
                    />
                  );
                })}
              </div>
            </div>
          )}

          {/* ── Split Among — Percentage mode ─── */}
          {splitType === "Percentage" && (
            <div>
              <div className="flex items-center justify-between mb-3">
                <label className="text-base font-medium">Percentage per person</label>
                <span className={`text-sm font-semibold ${
                  Math.abs(getPercentTotal() - 100) < 0.01
                    ? "text-[var(--color-success)]"
                    : "text-[var(--color-text-secondary)]"
                }`}>
                  {getPercentTotal().toFixed(1)}% / 100%
                </span>
              </div>
              {/* Progress bar */}
              <div className="h-1.5 rounded-full bg-[var(--color-border-subtle)] mb-3 overflow-hidden">
                <div
                  className={`h-full rounded-full transition-all duration-300 ${
                    Math.abs(getPercentTotal() - 100) < 0.01
                      ? "bg-[var(--color-success)]"
                      : "bg-[var(--color-primary)]"
                  }`}
                  style={{ width: `${Math.min(getPercentTotal(), 100)}%` }}
                />
              </div>
              <div className="space-y-3 max-h-60 overflow-y-auto">
                {members.filter((m) => selectedMembers.includes(m.userId)).map((m) => {
                  const color = MEMBER_COLORS[members.indexOf(m) % MEMBER_COLORS.length];
                  const pctVal = parseFloat(percentages[m.userId] || "0");
                  return (
                    <SliderRow
                      key={m.userId}
                      name={m.name}
                      color={color}
                      initials={getInitials(m.name)}
                      value={pctVal}
                      max={100}
                      step={0.5}
                      unit="%"
                      secondaryLabel={totalAmount > 0 && pctVal > 0 ? `= ${(Math.round((pctVal / 100) * totalAmount) / 100).toFixed(2)}` : undefined}
                      onChange={(v) => setPercentages((prev) => ({ ...prev, [m.userId]: v }))}
                    />
                  );
                })}
              </div>
            </div>
          )}

          <div className="flex gap-3 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 py-3 border border-[var(--color-border)] rounded-xl text-base font-medium hover:bg-[var(--color-surface-hover)] transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={loading}
              className="flex-1 bg-gradient-to-r from-[#6C3CE1] to-[#7B61FF] hover:from-[#7B4AEF] hover:to-[#8B71FF] text-white py-3 rounded-xl text-base font-semibold disabled:opacity-50 shadow-lg shadow-[#6C3CE1]/20 transition-all"
            >
              {loading ? "Adding..." : "Add Expense"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

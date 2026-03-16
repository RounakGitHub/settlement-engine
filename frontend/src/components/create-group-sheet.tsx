"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { groupsApi } from "@/lib/api";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "@/stores/toast-store";

interface Props {
  open: boolean;
  onClose: () => void;
}

interface FormData {
  name: string;
  currency: string;
  category: string;
}

export function CreateGroupSheet({ open, onClose }: Props) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const queryClient = useQueryClient();
  const { register, handleSubmit, reset, formState: { errors } } = useForm<FormData>({
    defaultValues: { currency: "INR", category: "" },
  });

  if (!open) return null;

  const onSubmit = async (data: FormData) => {
    setError("");
    setLoading(true);
    try {
      await groupsApi.create(data.name, data.currency, data.category);
      queryClient.invalidateQueries({ queryKey: ["groups"] });
      reset();
      onClose();
      toast.success("Group created!");
    } catch {
      setError("Failed to create group.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-end sm:items-center justify-center">
      <div className="fixed inset-0 bg-[var(--color-overlay)] animate-fade-in" onClick={onClose} />
      <div className="relative bg-[var(--color-surface)] w-full max-w-md rounded-t-2xl sm:rounded-2xl p-7 shadow-2xl animate-sheet-up">
        <h2 className="text-xl font-bold mb-6">Create Group</h2>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
          {error && (
            <div className="bg-[var(--color-destructive-light)] text-[var(--color-destructive)] text-base p-4 rounded-xl">
              {error}
            </div>
          )}

          <div>
            <label className="block text-base font-medium text-[var(--color-text-secondary)] mb-1.5">Group Name</label>
            <input
              {...register("name", { required: "Name is required", maxLength: 100 })}
              className="w-full px-4 py-3 border border-[var(--color-border)] rounded-xl text-base focus:outline-none focus:ring-2 focus:ring-[var(--color-primary)]/40 focus:border-[var(--color-primary)] transition-all"
              placeholder="e.g. Goa Trip"
            />
            {errors.name && <p className="text-[var(--color-destructive)] text-sm mt-1">{errors.name.message}</p>}
          </div>

          <div>
            <label className="block text-base font-medium text-[var(--color-text-secondary)] mb-1.5">Currency</label>
            <select
              {...register("currency")}
              className="w-full px-4 py-3 border border-[var(--color-border)] rounded-xl text-base focus:outline-none focus:ring-2 focus:ring-[var(--color-primary)]/40 focus:border-[var(--color-primary)] transition-all"
            >
              <option value="INR">INR</option>
              <option value="USD">USD</option>
              <option value="EUR">EUR</option>
            </select>
          </div>

          <div>
            <label className="block text-base font-medium text-[var(--color-text-secondary)] mb-1.5">Category</label>
            <input
              {...register("category")}
              className="w-full px-4 py-3 border border-[var(--color-border)] rounded-xl text-base focus:outline-none focus:ring-2 focus:ring-[var(--color-primary)]/40 focus:border-[var(--color-primary)] transition-all"
              placeholder="e.g. Travel, Household"
            />
          </div>

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
              {loading ? "Creating..." : "Create"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

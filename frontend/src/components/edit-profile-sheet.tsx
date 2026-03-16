"use client";

import { useState, useEffect } from "react";
import { createPortal } from "react-dom";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { authApi } from "@/lib/api";
import { useAuthStore } from "@/stores/auth-store";
import { toast } from "@/stores/toast-store";
import { confirm } from "@/stores/confirm-store";

interface Props {
  open: boolean;
  onClose: () => void;
}

interface FormData {
  name: string;
  password: string;
}

export function EditProfileSheet({ open, onClose }: Props) {
  const [loading, setLoading] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [fetching, setFetching] = useState(true);
  const [email, setEmail] = useState("");
  const router = useRouter();
  const userId = useAuthStore((s) => s.userId);
  const setAuth = useAuthStore((s) => s.setAuth);
  const logout = useAuthStore((s) => s.logout);

  const { register, handleSubmit, reset, formState: { errors } } = useForm<FormData>({
    defaultValues: { name: "", password: "" },
  });

  useEffect(() => {
    if (!open) return;
    setFetching(true);
    authApi.getProfile()
      .then((res) => {
        reset({ name: res.data.name, password: "" });
        setEmail(res.data.email);
      })
      .catch(() => {
        toast.error("Failed to load profile.");
      })
      .finally(() => setFetching(false));
  }, [open, reset]);

  if (!open) return null;

  const onSubmit = async (data: FormData) => {
    setLoading(true);
    try {
      await authApi.updateProfile(data.name.trim(), data.password || undefined);
      if (userId) setAuth(userId, data.name.trim());
      toast.success("Profile updated!");
      onClose();
    } catch (err: unknown) {
      const msg =
        err && typeof err === "object" && "response" in err
          ? (err as { response?: { data?: { title?: string } } }).response?.data?.title
          : undefined;
      toast.error(msg || "Failed to update profile.");
    } finally {
      setLoading(false);
    }
  };

  const handleDeleteAccount = async () => {
    const confirmed = await confirm({
      title: "Delete your account?",
      message:
        "This will permanently anonymize your data, cancel pending settlements, and remove you from all groups. This action cannot be undone.",
      confirmLabel: "Delete Account",
      destructive: true,
    });

    if (!confirmed) return;

    const reallyConfirmed = await confirm({
      title: "Are you absolutely sure?",
      message: "All your data will be permanently removed. You will not be able to recover your account.",
      confirmLabel: "Yes, delete my account",
      destructive: true,
    });

    if (!reallyConfirmed) return;

    setDeleting(true);
    try {
      await authApi.deleteAccount();
      logout();
      toast.success("Account deleted.");
      router.push("/login");
    } catch {
      toast.error("Failed to delete account.");
    } finally {
      setDeleting(false);
    }
  };

  return createPortal(
    <div className="fixed inset-0 z-[100] flex items-end sm:items-center justify-center">
      <div className="fixed inset-0 bg-[var(--color-overlay)] animate-fade-in" onClick={onClose} />
      <div className="relative bg-[var(--color-surface)] w-full max-w-md mx-4 rounded-2xl p-7 shadow-2xl animate-scale-in">
        <h2 className="text-xl font-bold mb-6">Edit Profile</h2>

        {fetching ? (
          <div className="space-y-5">
            <div className="h-4 w-16 skeleton rounded" />
            <div className="h-12 skeleton rounded-xl" />
            <div className="h-4 w-16 skeleton rounded" />
            <div className="h-12 skeleton rounded-xl" />
          </div>
        ) : (
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
            <div>
              <label className="block text-base font-medium text-[var(--color-text-secondary)] mb-1.5">Email</label>
              <p className="text-base text-[var(--color-text-primary)] px-4 py-3 bg-[var(--color-surface-hover)] rounded-xl">
                {email}
              </p>
            </div>

            <div>
              <label className="block text-base font-medium text-[var(--color-text-secondary)] mb-1.5">Name</label>
              <input
                type="text"
                {...register("name", { required: "Name is required", maxLength: { value: 100, message: "Max 100 characters" } })}
                className="w-full px-4 py-3 border border-[var(--color-border)] rounded-xl text-base focus:outline-none focus:ring-2 focus:ring-[var(--color-primary)]/40 focus:border-[var(--color-primary)] transition-all"
                placeholder="Your name"
              />
              {errors.name && <p className="text-[var(--color-destructive)] text-sm mt-1">{errors.name.message}</p>}
            </div>

            <div>
              <label className="block text-base font-medium text-[var(--color-text-secondary)] mb-1.5">
                Password
                <span className="text-[var(--color-text-tertiary)] font-normal ml-1.5">(leave blank to keep current)</span>
              </label>
              <input
                type="password"
                {...register("password", {
                  validate: (v) => !v || v.length >= 8 || "At least 8 characters",
                })}
                className="w-full px-4 py-3 border border-[var(--color-border)] rounded-xl text-base focus:outline-none focus:ring-2 focus:ring-[var(--color-primary)]/40 focus:border-[var(--color-primary)] transition-all"
                placeholder="New password"
              />
              {errors.password && <p className="text-[var(--color-destructive)] text-sm mt-1">{errors.password.message}</p>}
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
                {loading ? "Saving..." : "Save"}
              </button>
            </div>
          </form>
        )}

        {/* Danger zone */}
        <div className="mt-7 pt-6 border-t border-[var(--color-border-subtle)]">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-base font-medium text-[var(--color-destructive)]">Delete account</p>
              <p className="text-sm text-[var(--color-text-tertiary)] mt-0.5">Permanently remove your account and data</p>
            </div>
            <button
              type="button"
              onClick={handleDeleteAccount}
              disabled={deleting}
              className="px-5 py-2.5 text-base font-medium text-[var(--color-destructive)] border border-[var(--color-destructive)]/30 rounded-xl hover:bg-[var(--color-destructive-light)] transition-colors disabled:opacity-50"
            >
              {deleting ? "Deleting..." : "Delete"}
            </button>
          </div>
        </div>
      </div>
    </div>,
    document.body
  );
}

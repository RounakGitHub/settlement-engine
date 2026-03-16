import { create } from "zustand";

interface ConfirmState {
  open: boolean;
  title: string;
  message: string;
  confirmLabel: string;
  destructive: boolean;
  resolve: ((value: boolean) => void) | null;
  show: (opts: {
    title: string;
    message: string;
    confirmLabel?: string;
    destructive?: boolean;
    resolve: (value: boolean) => void;
  }) => void;
  accept: () => void;
  cancel: () => void;
}

export const useConfirmStore = create<ConfirmState>((set, get) => ({
  open: false,
  title: "",
  message: "",
  confirmLabel: "Confirm",
  destructive: false,
  resolve: null,
  show: (opts) =>
    set({
      open: true,
      title: opts.title,
      message: opts.message,
      confirmLabel: opts.confirmLabel || "Confirm",
      destructive: opts.destructive ?? false,
      resolve: opts.resolve,
    }),
  accept: () => {
    const r = get().resolve;
    set({ open: false, resolve: null });
    r?.(true);
  },
  cancel: () => {
    const r = get().resolve;
    set({ open: false, resolve: null });
    r?.(false);
  },
}));

export const confirm = (opts: {
  title: string;
  message: string;
  confirmLabel?: string;
  destructive?: boolean;
}): Promise<boolean> => {
  return new Promise((resolve) => {
    useConfirmStore.getState().show({ ...opts, resolve });
  });
};
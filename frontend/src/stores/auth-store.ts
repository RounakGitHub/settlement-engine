import { create } from "zustand";
import { persist } from "zustand/middleware";

interface AuthState {
  userId: string | null;
  userName: string | null;
  isAuthenticated: boolean;
  setAuth: (userId: string, userName: string) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      userId: null,
      userName: null,
      isAuthenticated: false,
      setAuth: (userId, userName) => {
        document.cookie = "splitr_authenticated=1; path=/; max-age=2592000";
        set({ userId, userName, isAuthenticated: true });
      },
      logout: () => {
        document.cookie = "splitr_authenticated=; path=/; max-age=0";
        set({ userId: null, userName: null, isAuthenticated: false });
      },
    }),
    { name: "splitr-auth" }
  )
);

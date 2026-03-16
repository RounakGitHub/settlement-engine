import { create } from "zustand";
import type { Group } from "@/types";

interface GroupState {
  groups: Group[];
  currentGroup: Group | null;
  setGroups: (groups: Group[]) => void;
  setCurrentGroup: (group: Group | null) => void;
  addGroup: (group: Group) => void;
}

export const useGroupStore = create<GroupState>((set) => ({
  groups: [],
  currentGroup: null,
  setGroups: (groups) => set({ groups }),
  setCurrentGroup: (currentGroup) => set({ currentGroup }),
  addGroup: (group) => set((s) => ({ groups: [...s.groups, group] })),
}));

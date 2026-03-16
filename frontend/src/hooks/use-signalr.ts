"use client";

import { useEffect, useRef, useCallback } from "react";
import {
  HubConnectionBuilder,
  HubConnection,
  LogLevel,
  HubConnectionState,
} from "@microsoft/signalr";
import { useAuthStore } from "@/stores/auth-store";
import { useQueryClient } from "@tanstack/react-query";

const API_BASE = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

// Custom logger that downgrades "stopped during negotiation" errors to warnings.
// These are expected in dev due to React Strict Mode's double-mount behavior.
const signalRLogger = {
  log(logLevel: LogLevel, message: string) {
    if (message?.includes?.("stopped during negotiation")) return;
    if (logLevel >= LogLevel.Error) console.warn("[SignalR]", message);
    else if (logLevel >= LogLevel.Warning) console.warn("[SignalR]", message);
  },
};

export function useSignalR(groupId: string | null) {
  const connectionRef = useRef<HubConnection | null>(null);
  const queryClient = useQueryClient();
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);

  const invalidateGroup = useCallback(() => {
    if (!groupId) return;
    queryClient.invalidateQueries({ queryKey: ["balances", groupId] });
    queryClient.invalidateQueries({ queryKey: ["expenses", groupId] });
    queryClient.invalidateQueries({ queryKey: ["settlement-plan", groupId] });
  }, [groupId, queryClient]);

  useEffect(() => {
    if (!groupId || !isAuthenticated) return;

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE}/api/hubs/groups`, {
        withCredentials: true,
      })
      .withAutomaticReconnect([0, 1000, 2000, 4000, 8000, 16000, 30000])
      .configureLogging(signalRLogger)
      .build();

    connectionRef.current = connection;

    const events = [
      "ExpenseAdded",
      "ExpenseEdited",
      "ExpenseDeleted",
      "SettlementConfirmed",
      "SettlementProposed",
      "SettlementFailed",
      "BalanceUpdated",
      "MemberJoined",
      "MemberLeft",
      "DebtGraphUpdated",
    ];

    events.forEach((event) => {
      connection.on(event, () => invalidateGroup());
    });

    connection.onreconnected(() => invalidateGroup());

    let stopped = false;

    connection
      .start()
      .then(() => {
        if (!stopped) return connection.invoke("JoinGroup", groupId);
      })
      .catch((err) => {
        // In React Strict Mode (dev only), effects mount/unmount/remount.
        // The first mount's connection gets stopped mid-negotiation — this is expected.
        if (stopped) return;
        console.warn("SignalR connection failed:", err);
      });

    return () => {
      stopped = true;
      if (connection.state !== HubConnectionState.Disconnected) {
        connection.stop();
      }
    };
  }, [groupId, isAuthenticated, invalidateGroup]);

  return connectionRef;
}

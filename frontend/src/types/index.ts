export interface User {
  id: string;
  email: string;
  name: string;
}

export interface AuthResult {
  userId: string;
}

export interface Group {
  id: string;
  name: string;
  currency: string;
  category: string;
  inviteCode: string;
  createdBy: string;
  isArchived: boolean;
  createdAt: string;
  memberCount?: number;
}

export interface GroupMember {
  userId: string;
  name: string;
  email: string;
  role: "Admin" | "Member";
  joinedAt: string;
}

export interface GroupPreview {
  name: string;
  currency: string;
  memberCount: number;
}

export interface Expense {
  id: string;
  groupId: string;
  paidBy: string;
  paidByName: string;
  amountPaise: number;
  description: string;
  splitType: "Equal" | "Exact" | "Percentage";
  splits: ExpenseSplit[];
  createdAt: string;
}

export interface ExpenseSplit {
  userId: string;
  userName?: string;
  amountPaise: number;
}

export interface UserBalance {
  userId: string;
  userName: string;
  netBalancePaise: number;
}

export interface Transfer {
  from: string;
  fromName: string;
  to: string;
  toName: string;
  amountPaise: number;
}

export interface Settlement {
  id: string;
  groupId: string;
  payerId: string;
  payeeId: string;
  amountPaise: number;
  status: "Pending" | "Confirmed" | "Failed" | "Expired" | "Cancelled" | "Review";
  razorpayOrderId: string;
  confirmedAt?: string;
  createdAt: string;
}

export interface InitiateSettlementResult {
  settlementId: string;
  razorpayOrderId: string;
}

export interface ApiError {
  title: string;
  status: number;
  errors?: Record<string, string[]>;
}

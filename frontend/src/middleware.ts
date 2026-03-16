import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

const publicPaths = ["/login", "/register", "/join", "/auth/callback"];

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Allow public paths
  if (publicPaths.some((p) => pathname.startsWith(p))) {
    return NextResponse.next();
  }

  // Check for auth — the refreshToken cookie is set by the backend (different port),
  // so we also check for the client-side auth indicator cookie set after login.
  const hasRefreshToken = request.cookies.get("refreshToken");
  const hasAuthSession = request.cookies.get("splitr_authenticated");
  if (!hasRefreshToken && !hasAuthSession && pathname !== "/login") {
    return NextResponse.redirect(new URL("/login", request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!api|_next/static|_next/image|favicon.ico).*)"],
};

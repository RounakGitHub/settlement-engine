"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { z } from "zod/v4";
import { authApi } from "@/lib/api";
import { useAuthStore } from "@/stores/auth-store";

const schema = z.object({
  email: z.email("Invalid email address"),
  password: z.string().min(1, "Password is required"),
});

type FormData = z.infer<typeof schema>;

export default function LoginPage() {
  const router = useRouter();
  const setAuth = useAuthStore((s) => s.setAuth);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormData>();

  const onSubmit = async (data: FormData) => {
    setError("");
    setLoading(true);
    try {
      const parsed = schema.parse(data);
      const res = await authApi.login(parsed.email, parsed.password);
      setAuth(res.data.userId, res.data.userName);
      router.push("/dashboard");
    } catch (err: unknown) {
      if (err && typeof err === "object" && "response" in err) {
        const axiosErr = err as { response?: { status?: number; data?: { title?: string } } };
        if (axiosErr.response?.status === 429) {
          setError("Too many attempts. Please try again later.");
        } else if (axiosErr.response?.status === 401) {
          setError("Invalid email or password.");
        } else {
          setError(axiosErr.response?.data?.title || "Login failed.");
        }
      } else {
        setError("Login failed.");
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-[#0B0E14] text-white">
      {/* Background effects */}
      <div className="fixed inset-0 overflow-hidden pointer-events-none">
        <div className="absolute top-[-20%] left-[-10%] w-[600px] h-[600px] rounded-full bg-[#6C3CE1]/15 blur-[140px]" />
        <div className="absolute bottom-[-20%] right-[-10%] w-[500px] h-[500px] rounded-full bg-[#00A3BF]/12 blur-[140px]" />
        <div className="absolute top-[50%] right-[30%] w-[300px] h-[300px] rounded-full bg-[#FF6B6B]/6 blur-[100px] animate-float-slow" />
        <div className="absolute inset-0 opacity-[0.025]" style={{ backgroundImage: "radial-gradient(circle, white 1px, transparent 1px)", backgroundSize: "28px 28px" }} />
      </div>

      {/* ── Navbar ───────────────────────────── */}
      <nav className="relative z-10 flex items-center justify-between max-w-6xl mx-auto px-6 py-5">
        <Link href="/dashboard" className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-[#6C3CE1] to-[#00A3BF] flex items-center justify-center shadow-lg shadow-[#6C3CE1]/20">
            <span className="text-white text-lg font-bold">S</span>
          </div>
          <span className="font-bold text-xl text-white">Splitr</span>
        </Link>
        <Link href="/register" className="text-[#A78BFA] font-semibold text-base hover:text-[#C4B5FD] transition-colors">
          Create an account
        </Link>
      </nav>

      {/* ── Hero: Centered form ──────────────── */}
      <section className="relative z-10 max-w-2xl mx-auto px-6 pt-16 pb-20 text-center">
        <div className="stagger-1">
          <h1 className="text-4xl sm:text-5xl font-bold leading-[1.1] mb-12">
            Split expenses,{" "}
            <span className="bg-gradient-to-r from-[#A78BFA] to-[#22D3EE] bg-clip-text text-transparent">not friendships.</span>
          </h1>
        </div>

        {/* Login card — the focal point */}
        <div className="max-w-md mx-auto stagger-2">
          <div className="bg-[#161B22] border border-[#2D333B] rounded-2xl p-8 shadow-2xl shadow-black/30 text-left">
            <h2 className="text-2xl font-bold text-white mb-1 text-center">Log in</h2>
            <p className="text-[#9CA3AF] text-sm mb-6 text-center">Enter your credentials to continue</p>

            <button
              type="button"
              onClick={() => window.location.href = `${process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000"}/api/auth/google`}
              className="w-full flex items-center justify-center gap-3 bg-white hover:bg-gray-100 text-[#1A1A2E] py-3.5 rounded-xl text-base font-medium btn-press transition-colors"
            >
              <svg className="w-5 h-5" viewBox="0 0 24 24">
                <path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 01-2.2 3.32v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.1z" fill="#4285F4"/>
                <path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/>
                <path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill="#FBBC05"/>
                <path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/>
              </svg>
              Log in with Google
            </button>

            <div className="flex items-center gap-3 my-6">
              <div className="flex-1 h-px bg-[#2D333B]" />
              <span className="text-[#9CA3AF] text-sm">or</span>
              <div className="flex-1 h-px bg-[#2D333B]" />
            </div>

            <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
              {error && (
                <div className="bg-[#F4706720] text-[#F47067] text-base p-4 rounded-xl border border-[#F47067]/20">
                  {error}
                </div>
              )}

              <div>
                <label className="block text-sm font-medium text-[#C9D1D9] mb-1.5">Email</label>
                <input
                  type="email"
                  {...register("email", { required: "Email is required" })}
                  className="w-full px-4 py-3 bg-[#0D1117] border border-[#2D333B] rounded-xl text-base text-white placeholder-[#9CA3AF] focus:outline-none focus:ring-2 focus:ring-[#6C3CE1]/50 focus:border-[#6C3CE1] transition-all"
                  placeholder="you@example.com"
                />
                {errors.email && <p className="text-[#F47067] text-sm mt-1">{errors.email.message}</p>}
              </div>

              <div>
                <label className="block text-sm font-medium text-[#C9D1D9] mb-1.5">Password</label>
                <input
                  type="password"
                  {...register("password", { required: "Password is required" })}
                  className="w-full px-4 py-3 bg-[#0D1117] border border-[#2D333B] rounded-xl text-base text-white placeholder-[#9CA3AF] focus:outline-none focus:ring-2 focus:ring-[#6C3CE1]/50 focus:border-[#6C3CE1] transition-all"
                  placeholder="Your password"
                />
                {errors.password && <p className="text-[#F47067] text-sm mt-1">{errors.password.message}</p>}
              </div>

              <button
                type="submit"
                disabled={loading}
                className="w-full bg-gradient-to-r from-[#6C3CE1] to-[#7B61FF] hover:from-[#7B4AEF] hover:to-[#8B71FF] text-white py-3.5 rounded-xl text-base font-semibold disabled:opacity-50 transition-all shadow-lg shadow-[#6C3CE1]/25"
              >
                {loading ? "Logging in..." : "Log In"}
              </button>
            </form>

            <p className="text-sm text-[#9CA3AF] mt-5 text-center">
              Don&apos;t have an account?{" "}
              <Link href="/register" className="text-[#A78BFA] font-semibold hover:text-[#C4B5FD]">Sign up free</Link>
            </p>
          </div>

          {/* Trust badges */}
          <div className="flex items-center justify-center gap-6 mt-6">
            <div className="flex items-center gap-1.5 text-[#9CA3AF] text-sm">
              <svg className="w-4 h-4 text-[#69F0AE]" fill="none" stroke="currentColor" strokeWidth={2.5} viewBox="0 0 24 24"><path strokeLinecap="round" d="M5 13l4 4L19 7" /></svg>
              Free forever
            </div>
            <div className="flex items-center gap-1.5 text-[#9CA3AF] text-sm">
              <svg className="w-4 h-4 text-[#69F0AE]" fill="none" stroke="currentColor" strokeWidth={2.5} viewBox="0 0 24 24"><path strokeLinecap="round" d="M5 13l4 4L19 7" /></svg>
              No credit card
            </div>
            <div className="flex items-center gap-1.5 text-[#9CA3AF] text-sm">
              <svg className="w-4 h-4 text-[#69F0AE]" fill="none" stroke="currentColor" strokeWidth={2.5} viewBox="0 0 24 24"><path strokeLinecap="round" d="M5 13l4 4L19 7" /></svg>
              Instant setup
            </div>
          </div>
        </div>
      </section>

      {/* ── Compact features strip ───────────── */}
      <section className="relative z-10 border-t border-[#1C2230]">
        <div className="max-w-5xl mx-auto px-6 py-12">
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-5">
            <div className="bg-[#161B22] border border-[#2D333B] rounded-2xl p-5 flex items-start gap-4 hover:border-[#A78BFA]/40 transition-colors">
              <div className="w-11 h-11 rounded-xl bg-[#A78BFA]/10 flex items-center justify-center shrink-0">
                <svg className="w-6 h-6 text-[#A78BFA]" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 6v12m-3-2.818l.879.659c1.171.879 3.07.879 4.242 0 1.172-.879 1.172-2.303 0-3.182C13.536 12.219 12.768 12 12 12c-.725 0-1.45-.22-2.003-.659-1.106-.879-1.106-2.303 0-3.182s2.9-.879 4.006 0l.415.33M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
              </div>
              <div>
                <h3 className="font-semibold mb-1">Smart Splits</h3>
                <p className="text-[#B0B8C4] text-sm leading-relaxed">Minimizes total payments with debt simplification.</p>
              </div>
            </div>
            <div className="bg-[#161B22] border border-[#2D333B] rounded-2xl p-5 flex items-start gap-4 hover:border-[#22D3EE]/40 transition-colors">
              <div className="w-11 h-11 rounded-xl bg-[#22D3EE]/10 flex items-center justify-center shrink-0">
                <svg className="w-6 h-6 text-[#22D3EE]" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M13 10V3L4 14h7v7l9-11h-7z" />
                </svg>
              </div>
              <div>
                <h3 className="font-semibold mb-1">Real-Time Sync</h3>
                <p className="text-[#B0B8C4] text-sm leading-relaxed">Every update syncs instantly across all devices.</p>
              </div>
            </div>
            <div className="bg-[#161B22] border border-[#2D333B] rounded-2xl p-5 flex items-start gap-4 hover:border-[#69F0AE]/40 transition-colors">
              <div className="w-11 h-11 rounded-xl bg-[#69F0AE]/10 flex items-center justify-center shrink-0">
                <svg className="w-6 h-6 text-[#69F0AE]" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
              </div>
              <div>
                <h3 className="font-semibold mb-1">1-Tap Settle</h3>
                <p className="text-[#B0B8C4] text-sm leading-relaxed">See who owes whom and clear debts instantly.</p>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ── Testimonials ─────────────────────── */}
      <section className="relative z-10 border-t border-[#1C2230]">
        <div className="max-w-4xl mx-auto px-6 py-14">
          <h2 className="text-2xl font-bold text-center mb-10">Trusted by groups everywhere</h2>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
            <div className="bg-[#161B22] border border-[#2D333B] rounded-2xl p-6">
              <div className="flex gap-1 mb-3">
                {[...Array(5)].map((_, i) => (
                  <svg key={i} className="w-4 h-4 text-[#FFB74D]" fill="currentColor" viewBox="0 0 20 20"><path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z" /></svg>
                ))}
              </div>
              <p className="text-[#C9D1D9] text-sm leading-relaxed mb-4">&ldquo;We used it for our Goa trip — 8 people, 40+ expenses. The debt simplification turned 15 payments into just 4. Absolute lifesaver.&rdquo;</p>
              <div className="flex items-center gap-3">
                <div className="w-8 h-8 rounded-full bg-[#FFB74D] flex items-center justify-center text-xs font-bold text-white">A</div>
                <div>
                  <p className="text-sm font-medium text-white">Ankit M.</p>
                  <p className="text-xs text-[#9CA3AF]">Trip organizer</p>
                </div>
              </div>
            </div>
            <div className="bg-[#161B22] border border-[#2D333B] rounded-2xl p-6">
              <div className="flex gap-1 mb-3">
                {[...Array(5)].map((_, i) => (
                  <svg key={i} className="w-4 h-4 text-[#FFB74D]" fill="currentColor" viewBox="0 0 20 20"><path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z" /></svg>
                ))}
              </div>
              <p className="text-[#C9D1D9] text-sm leading-relaxed mb-4">&ldquo;Our apartment of 4 uses it for rent, groceries, utilities — everything. Real-time sync means no one asks &lsquo;did you add that yet?&rsquo; anymore.&rdquo;</p>
              <div className="flex items-center gap-3">
                <div className="w-8 h-8 rounded-full bg-[#7B61FF] flex items-center justify-center text-xs font-bold text-white">S</div>
                <div>
                  <p className="text-sm font-medium text-white">Sneha R.</p>
                  <p className="text-xs text-[#9CA3AF]">Roommate group admin</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ── Footer CTA ───────────────────────── */}
      <section className="relative z-10 border-t border-[#1C2230]">
        <div className="max-w-6xl mx-auto px-6 py-12 text-center">
          <p className="text-[#B0B8C4] text-lg mb-4">Ready to stop arguing about who owes what?</p>
          <Link
            href="/register"
            className="inline-flex items-center gap-2 bg-gradient-to-r from-[#6C3CE1] to-[#7B61FF] hover:from-[#7B4AEF] hover:to-[#8B71FF] text-white px-8 py-3.5 rounded-xl text-base font-semibold transition-all shadow-lg shadow-[#6C3CE1]/25"
          >
            Create a free account
            <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2.5} viewBox="0 0 24 24"><path strokeLinecap="round" d="M13 7l5 5-5 5M6 12h12" /></svg>
          </Link>
          <p className="text-[#9CA3AF] text-sm mt-4">Free forever. No credit card needed.</p>
        </div>
      </section>
    </div>
  );
}

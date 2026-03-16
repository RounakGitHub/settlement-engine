"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { z } from "zod/v4";
import { authApi } from "@/lib/api";
import { useAuthStore } from "@/stores/auth-store";

const schema = z.object({
  name: z.string().min(1, "Name is required").max(100),
  email: z.email("Invalid email address"),
  password: z.string().min(8, "Password must be at least 8 characters"),
});

type FormData = z.infer<typeof schema>;

/* ── Illustration: built with HTML for readability ───────── */

function GroupIllustration() {
  return (
    <div className="relative w-full max-w-xl mx-auto select-none" aria-hidden="true">
      {/* Central group card */}
      <div className="bg-[#161B22] border border-[#2D333B] rounded-2xl p-6 mx-auto w-64 text-center relative z-10">
        <div className="w-14 h-14 rounded-2xl bg-gradient-to-br from-[#6C3CE1] to-[#7B61FF] flex items-center justify-center mx-auto mb-3 shadow-lg shadow-[#6C3CE1]/20">
          <svg className="w-7 h-7 text-white" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
          </svg>
        </div>
        <p className="text-white font-semibold text-lg">Trip to Goa</p>
        <p className="text-[#9CA3AF] text-sm mt-1">4 members &middot; $371.50 total</p>
      </div>

      {/* Member avatars around the circle */}
      <div className="absolute -top-2 left-1/2 -translate-x-1/2 -translate-y-full flex flex-col items-center">
        <div className="w-12 h-12 rounded-full bg-[#6C3CE1] flex items-center justify-center text-white font-bold text-lg shadow-lg shadow-[#6C3CE1]/30">R</div>
        <p className="text-[#C9D1D9] text-sm font-medium mt-1">Riya</p>
        <div className="w-px h-4 bg-[#2D333B]" />
      </div>

      <div className="absolute top-1/2 -right-2 translate-x-full -translate-y-1/2 flex items-center">
        <div className="w-px h-0 sm:h-0 lg:w-6 lg:h-px bg-[#2D333B]" />
        <div className="flex flex-col items-center">
          <div className="w-12 h-12 rounded-full bg-[#00A3BF] flex items-center justify-center text-white font-bold text-lg shadow-lg shadow-[#00A3BF]/30">A</div>
          <p className="text-[#C9D1D9] text-sm font-medium mt-1">Aman</p>
        </div>
      </div>

      <div className="absolute -bottom-2 left-1/2 -translate-x-1/2 translate-y-full flex flex-col items-center">
        <div className="w-px h-4 bg-[#2D333B]" />
        <p className="text-[#C9D1D9] text-sm font-medium mb-1">Sneha</p>
        <div className="w-12 h-12 rounded-full bg-[#FF6B6B] flex items-center justify-center text-white font-bold text-lg shadow-lg shadow-[#FF6B6B]/30">S</div>
      </div>

      <div className="absolute top-1/2 -left-2 -translate-x-full -translate-y-1/2 flex items-center flex-row-reverse">
        <div className="w-px h-0 sm:h-0 lg:w-6 lg:h-px bg-[#2D333B]" />
        <div className="flex flex-col items-center">
          <div className="w-12 h-12 rounded-full bg-[#FFB74D] flex items-center justify-center text-white font-bold text-lg shadow-lg shadow-[#FFB74D]/30">P</div>
          <p className="text-[#C9D1D9] text-sm font-medium mt-1">Prateek</p>
        </div>
      </div>

      {/* Floating expense cards */}
      <div className="absolute -top-8 -left-8 lg:-left-16 bg-[#161B22] border border-[#2D333B] rounded-xl px-4 py-3 shadow-xl shadow-black/30 animate-float-slow">
        <p className="text-[#C9D1D9] font-medium text-sm">Hotel booking</p>
        <p className="text-[#A78BFA] font-bold text-xl mt-0.5">$240.00</p>
        <div className="flex items-center gap-1.5 mt-1">
          <div className="w-4 h-4 rounded-full bg-[#6C3CE1]" />
          <span className="text-[#9CA3AF] text-xs">Paid by Riya</span>
        </div>
      </div>

      <div className="absolute -top-4 -right-4 lg:-right-12 bg-[#161B22] border border-[#2D333B] rounded-xl px-4 py-3 shadow-xl shadow-black/30 animate-float-medium">
        <p className="text-[#C9D1D9] font-medium text-sm">Dinner</p>
        <p className="text-[#22D3EE] font-bold text-xl mt-0.5">$86.50</p>
        <div className="flex items-center gap-1.5 mt-1">
          <div className="w-4 h-4 rounded-full bg-[#00A3BF]" />
          <span className="text-[#9CA3AF] text-xs">Paid by Aman</span>
        </div>
      </div>

      <div className="absolute -bottom-8 -left-4 lg:-left-12 bg-[#161B22] border border-[#2D333B] rounded-xl px-4 py-3 shadow-xl shadow-black/30 animate-float-slower">
        <p className="text-[#C9D1D9] font-medium text-sm">Cab rides</p>
        <p className="text-[#FFB74D] font-bold text-xl mt-0.5">$45.00</p>
        <div className="flex items-center gap-1.5 mt-1">
          <div className="w-4 h-4 rounded-full bg-[#FFB74D]" />
          <span className="text-[#9CA3AF] text-xs">Paid by Prateek</span>
        </div>
      </div>

      {/* Settlement badge */}
      <div className="absolute -bottom-6 -right-4 lg:-right-16 bg-[#161B22] border border-[#69F0AE]/30 rounded-xl px-4 py-3 shadow-xl shadow-black/30">
        <div className="flex items-center gap-2">
          <div className="w-8 h-8 rounded-full bg-[#69F0AE]/15 flex items-center justify-center">
            <svg className="w-4 h-4 text-[#69F0AE]" fill="none" stroke="currentColor" strokeWidth={2.5} viewBox="0 0 24 24"><path strokeLinecap="round" d="M5 13l4 4L19 7" /></svg>
          </div>
          <div>
            <p className="text-[#69F0AE] font-semibold text-sm">All settled!</p>
            <p className="text-[#9CA3AF] text-xs">3 payments → 1</p>
          </div>
        </div>
      </div>
    </div>
  );
}

/* ── Page ─────────────────────────────────────────────────── */

export default function RegisterPage() {
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
      const res = await authApi.register(parsed.name, parsed.email, parsed.password);
      setAuth(res.data.userId, res.data.userName);
      router.push("/dashboard");
    } catch (err: unknown) {
      if (err && typeof err === "object" && "response" in err) {
        const axiosErr = err as { response?: { status?: number; data?: { title?: string } } };
        if (axiosErr.response?.status === 429) {
          setError("Too many attempts. Please try again later.");
        } else {
          setError(axiosErr.response?.data?.title || "Registration failed.");
        }
      } else {
        setError("Registration failed.");
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-[#0B0E14] text-white">
      {/* Background effects */}
      <div className="fixed inset-0 overflow-hidden pointer-events-none">
        <div className="absolute top-[-15%] right-[-10%] w-[600px] h-[600px] rounded-full bg-[#7B61FF]/15 blur-[140px]" />
        <div className="absolute bottom-[-15%] left-[-10%] w-[500px] h-[500px] rounded-full bg-[#00A3BF]/12 blur-[140px]" />
        <div className="absolute bottom-[30%] left-[30%] w-[250px] h-[250px] rounded-full bg-[#00E676]/6 blur-[100px] animate-float-slower" />
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
        <Link href="/login" className="text-[#A78BFA] font-semibold text-base hover:text-[#C4B5FD] transition-colors">
          Log in
        </Link>
      </nav>

      {/* ── Hero: Headline + Illustration | Form ── */}
      <section className="relative z-10 max-w-6xl mx-auto px-6 pt-8 pb-20">
        <div className="flex flex-col lg:flex-row items-center gap-16 lg:gap-20">
          {/* Left: headline + illustration */}
          <div className="flex-1 stagger-1">
            <h1 className="text-4xl sm:text-5xl font-bold leading-[1.1] mb-4">
              Expenses shared,{" "}
              <span className="bg-gradient-to-r from-[#A78BFA] to-[#22D3EE] bg-clip-text text-transparent">friendships spared.</span>
            </h1>
            <p className="text-[#B0B8C4] text-lg mb-14 max-w-md leading-relaxed">
              Create a free account and start splitting bills in seconds. No spreadsheets, no calculator, no arguments. Just fair splits.
            </p>

            {/* HTML-based illustration — big and readable */}
            <div className="hidden lg:block py-16 px-12">
              <GroupIllustration />
            </div>
          </div>

          {/* Right: Form */}
          <div className="w-full max-w-md lg:max-w-[440px] shrink-0 stagger-2">
            <div className="bg-[#161B22] border border-[#2D333B] rounded-2xl p-7 shadow-2xl shadow-black/30">
              <h2 className="text-2xl font-bold text-white mb-1 text-center">Create an account</h2>
              <p className="text-[#9CA3AF] text-sm mb-5 text-center">Free forever — no credit card needed</p>

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
                Sign up with Google
              </button>

              <div className="flex items-center gap-3 my-5">
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
                  <label className="block text-sm font-medium text-[#C9D1D9] mb-1.5">Name</label>
                  <input
                    type="text"
                    {...register("name", { required: "Name is required" })}
                    className="w-full px-4 py-3 bg-[#0D1117] border border-[#2D333B] rounded-xl text-base text-white placeholder-[#9CA3AF] focus:outline-none focus:ring-2 focus:ring-[#6C3CE1]/50 focus:border-[#6C3CE1] transition-all"
                    placeholder="Full name"
                  />
                  {errors.name && <p className="text-[#F47067] text-sm mt-1">{errors.name.message}</p>}
                </div>

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
                    {...register("password", {
                      required: "Password is required",
                      minLength: { value: 8, message: "At least 8 characters" },
                    })}
                    className="w-full px-4 py-3 bg-[#0D1117] border border-[#2D333B] rounded-xl text-base text-white placeholder-[#9CA3AF] focus:outline-none focus:ring-2 focus:ring-[#6C3CE1]/50 focus:border-[#6C3CE1] transition-all"
                    placeholder="Min. 8 characters"
                  />
                  {errors.password && <p className="text-[#F47067] text-sm mt-1">{errors.password.message}</p>}
                </div>

                <button
                  type="submit"
                  disabled={loading}
                  className="w-full bg-gradient-to-r from-[#6C3CE1] to-[#7B61FF] hover:from-[#7B4AEF] hover:to-[#8B71FF] text-white py-3.5 rounded-xl text-base font-semibold disabled:opacity-50 transition-all shadow-lg shadow-[#6C3CE1]/25"
                >
                  {loading ? "Creating account..." : "Create Account"}
                </button>
              </form>

              <p className="text-sm text-[#9CA3AF] mt-5 text-center">
                Already have an account?{" "}
                <Link href="/login" className="text-[#A78BFA] font-semibold hover:text-[#C4B5FD]">Log in</Link>
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
                Setup in 30s
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ── How it works ─────────────────────── */}
      <section className="relative z-10 border-t border-[#1C2230]">
        <div className="max-w-6xl mx-auto px-6 py-16">
          <h2 className="text-2xl sm:text-3xl font-bold text-center mb-3">How it works</h2>
          <p className="text-[#B0B8C4] text-center text-lg mb-12">Three simple steps to stress-free expense sharing.</p>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-8">
            <div className="text-center">
              <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-[#6C3CE1] to-[#7B61FF] flex items-center justify-center mx-auto mb-5 shadow-lg shadow-[#6C3CE1]/20">
                <span className="text-2xl font-bold">1</span>
              </div>
              <h3 className="text-lg font-semibold mb-2">Create a Group</h3>
              <p className="text-[#B0B8C4] text-sm leading-relaxed max-w-xs mx-auto">
                Set up a group for your trip, apartment, dinner, or any shared occasion. Invite friends with a simple link — they join in one click.
              </p>
            </div>
            <div className="text-center">
              <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-[#00A3BF] to-[#22D3EE] flex items-center justify-center mx-auto mb-5 shadow-lg shadow-[#00A3BF]/20">
                <span className="text-2xl font-bold">2</span>
              </div>
              <h3 className="text-lg font-semibold mb-2">Add Expenses</h3>
              <p className="text-[#B0B8C4] text-sm leading-relaxed max-w-xs mx-auto">
                Log who paid and how to split it — equally, by exact amounts, or by percentages. Add as many expenses as you need.
              </p>
            </div>
            <div className="text-center">
              <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-[#00865A] to-[#69F0AE] flex items-center justify-center mx-auto mb-5 shadow-lg shadow-[#00865A]/20">
                <span className="text-2xl font-bold text-[#0B0E14]">3</span>
              </div>
              <h3 className="text-lg font-semibold mb-2">Settle Up</h3>
              <p className="text-[#B0B8C4] text-sm leading-relaxed max-w-xs mx-auto">
                Our algorithm simplifies debts to the minimum payments needed. Settle with one tap and you&apos;re done.
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* ── Features ─────────────────────────── */}
      <section className="relative z-10 border-t border-[#1C2230]">
        <div className="max-w-6xl mx-auto px-6 py-16">
          <h2 className="text-2xl sm:text-3xl font-bold text-center mb-3">Built for real life</h2>
          <p className="text-[#B0B8C4] text-center text-lg mb-12 max-w-lg mx-auto">Whether it&apos;s a weekend trip, shared apartment, or dinner with friends — Splitr handles it all.</p>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
            <div className="bg-[#161B22] border border-[#2D333B] rounded-2xl p-6">
              <div className="w-10 h-10 rounded-lg bg-[#A78BFA]/10 flex items-center justify-center mb-3">
                <svg className="w-5 h-5 text-[#A78BFA]" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" d="M12 6v12m-3-2.818l.879.659c1.171.879 3.07.879 4.242 0 1.172-.879 1.172-2.303 0-3.182C13.536 12.219 12.768 12 12 12c-.725 0-1.45-.22-2.003-.659-1.106-.879-1.106-2.303 0-3.182s2.9-.879 4.006 0l.415.33M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
              </div>
              <h3 className="font-semibold mb-1">Multiple Split Types</h3>
              <p className="text-[#B0B8C4] text-sm leading-relaxed">Equal, exact amounts, or percentage — split any way you want.</p>
            </div>
            <div className="bg-[#161B22] border border-[#2D333B] rounded-2xl p-6">
              <div className="w-10 h-10 rounded-lg bg-[#22D3EE]/10 flex items-center justify-center mb-3">
                <svg className="w-5 h-5 text-[#22D3EE]" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" d="M13 10V3L4 14h7v7l9-11h-7z" /></svg>
              </div>
              <h3 className="font-semibold mb-1">Instant Updates</h3>
              <p className="text-[#B0B8C4] text-sm leading-relaxed">Every change syncs in real-time. No refreshing, ever.</p>
            </div>
            <div className="bg-[#161B22] border border-[#2D333B] rounded-2xl p-6">
              <div className="w-10 h-10 rounded-lg bg-[#69F0AE]/10 flex items-center justify-center mb-3">
                <svg className="w-5 h-5 text-[#69F0AE]" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" /></svg>
              </div>
              <h3 className="font-semibold mb-1">Secure & Private</h3>
              <p className="text-[#B0B8C4] text-sm leading-relaxed">Your data is encrypted and never shared with anyone.</p>
            </div>
            <div className="bg-[#161B22] border border-[#2D333B] rounded-2xl p-6">
              <div className="w-10 h-10 rounded-lg bg-[#FF8A8A]/10 flex items-center justify-center mb-3">
                <svg className="w-5 h-5 text-[#FF8A8A]" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" /></svg>
              </div>
              <h3 className="font-semibold mb-1">Invite via Link</h3>
              <p className="text-[#B0B8C4] text-sm leading-relaxed">Share a link and friends join your group instantly.</p>
            </div>
            <div className="bg-[#161B22] border border-[#2D333B] rounded-2xl p-6">
              <div className="w-10 h-10 rounded-lg bg-[#FFB74D]/10 flex items-center justify-center mb-3">
                <svg className="w-5 h-5 text-[#FFB74D]" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" d="M3.055 11H5a2 2 0 012 2v1a2 2 0 002 2 2 2 0 012 2v2.945M8 3.935V5.5A2.5 2.5 0 0010.5 8h.5a2 2 0 012 2 2 2 0 104 0 2 2 0 012-2h1.064M15 20.488V18a2 2 0 012-2h3.064M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
              </div>
              <h3 className="font-semibold mb-1">Multi-Currency</h3>
              <p className="text-[#B0B8C4] text-sm leading-relaxed">Set your group&apos;s currency. Perfect for international trips.</p>
            </div>
            <div className="bg-[#161B22] border border-[#2D333B] rounded-2xl p-6">
              <div className="w-10 h-10 rounded-lg bg-[#6C3CE1]/10 flex items-center justify-center mb-3">
                <svg className="w-5 h-5 text-[#A78BFA]" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" /></svg>
              </div>
              <h3 className="font-semibold mb-1">Works Everywhere</h3>
              <p className="text-[#B0B8C4] text-sm leading-relaxed">Responsive web app. Desktop, tablet, and phone.</p>
            </div>
          </div>
        </div>
      </section>

      {/* ── Testimonials ─────────────────────── */}
      <section className="relative z-10 border-t border-[#1C2230]">
        <div className="max-w-4xl mx-auto px-6 py-16">
          <h2 className="text-2xl sm:text-3xl font-bold text-center mb-12">Loved by groups everywhere</h2>
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
          <p className="text-[#B0B8C4] text-lg">Already have an account?</p>
          <Link
            href="/login"
            className="inline-flex items-center gap-2 text-[#A78BFA] font-semibold text-lg hover:text-[#C4B5FD] transition-colors mt-2"
          >
            Log in to your account
            <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2.5} viewBox="0 0 24 24"><path strokeLinecap="round" d="M13 7l5 5-5 5M6 12h12" /></svg>
          </Link>
        </div>
      </section>
    </div>
  );
}

import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Zap, Mail, Lock, Eye, EyeOff, AlertCircle } from "lucide-react";
import { useAuth } from "../context/AuthContext";
import { useToast } from "../context/ToastContext";
import { cn } from "../components/ui";

export default function Login() {
  const { login }   = useAuth();
  const toast       = useToast();
  const navigate    = useNavigate();

  const [form, setForm]     = useState({ email: "", password: "" });
  const [showPw, setShowPw] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError]   = useState("");

  const set = (k) => (e) => setForm(f => ({ ...f, [k]: e.target.value }));

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      await login(form.email.trim(), form.password);
      toast.success("Welcome back!");
      navigate("/");
    } catch (err) {
      setError(err.message ?? "Login failed.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-bg flex items-center justify-center px-4">
      {/* Background glow */}
      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="absolute top-1/4 left-1/2 -translate-x-1/2 w-[600px] h-[600px] bg-accent/5 rounded-full blur-3xl" />
      </div>

      <div className="w-full max-w-md relative z-10">
        {/* Logo */}
        <div className="text-center mb-8">
          <Link to="/" className="inline-flex items-center gap-3 mb-4">
            <div className="w-12 h-12 rounded-2xl bg-accent/20 border border-accent/30 flex items-center justify-center animate-pulse-glow">
              <Zap className="w-6 h-6 text-accent" />
            </div>
            <div className="text-left">
              <p className="font-display font-bold text-txt text-xl leading-none">
                MOMBot<span className="text-accent">Pro</span>
              </p>
              <p className="text-[10px] text-muted mt-0.5">AI Pipeline Engine</p>
            </div>
          </Link>
          <h1 className="font-display font-bold text-txt text-2xl">Welcome back</h1>
          <p className="text-sm text-muted mt-1">Sign in to your account to continue</p>
        </div>

        {/* Card */}
        <div className="bg-surface border border-border rounded-2xl p-8 shadow-xl">
          {error && (
            <div className="flex items-center gap-2 bg-red/10 border border-red/25 rounded-xl px-4 py-3 mb-5 text-sm text-red">
              <AlertCircle className="w-4 h-4 shrink-0" />
              {error}
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-5">
            {/* Email */}
            <div>
              <label className="block text-xs font-semibold text-txt mb-1.5">Work Email</label>
              <div className="relative">
                <Mail className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted" />
                <input
                  type="email"
                  required
                  value={form.email}
                  onChange={set("email")}
                  placeholder="you@company.com"
                  className="w-full bg-surface2 border border-border rounded-xl pl-10 pr-4 py-2.5 text-sm text-txt placeholder:text-muted outline-none focus:border-accent transition-colors"
                />
              </div>
            </div>

            {/* Password */}
            <div>
              <div className="flex items-center justify-between mb-1.5">
                <label className="text-xs font-semibold text-txt">Password</label>
                <button type="button" className="text-xs text-accent hover:underline">Forgot password?</button>
              </div>
              <div className="relative">
                <Lock className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted" />
                <input
                  type={showPw ? "text" : "password"}
                  required
                  value={form.password}
                  onChange={set("password")}
                  placeholder="••••••••"
                  className="w-full bg-surface2 border border-border rounded-xl pl-10 pr-10 py-2.5 text-sm text-txt placeholder:text-muted outline-none focus:border-accent transition-colors"
                />
                <button
                  type="button"
                  onClick={() => setShowPw(p => !p)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-muted hover:text-txt transition-colors"
                >
                  {showPw ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
            </div>

            {/* Submit */}
            <button
              type="submit"
              disabled={loading}
              className={cn(
                "w-full flex items-center justify-center gap-2 py-2.5 rounded-xl text-sm font-semibold transition-all",
                "bg-accent text-white hover:bg-accent/90 shadow-lg shadow-accent/25",
                loading && "opacity-60 cursor-not-allowed"
              )}
            >
              {loading ? (
                <>
                  <span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                  Signing in…
                </>
              ) : "Sign In"}
            </button>
          </form>

          <div className="mt-5 text-center text-sm text-muted">
            Don't have an account?{" "}
            <Link to="/register" className="text-accent hover:underline font-medium">
              Create one
            </Link>
          </div>
        </div>

        {/* Footer note */}
        <p className="text-center text-xs text-muted mt-6">
          Work email required · Personal email domains not accepted
        </p>
      </div>
    </div>
  );
}

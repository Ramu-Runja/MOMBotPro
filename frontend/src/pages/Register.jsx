import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Zap, Mail, Lock, Eye, EyeOff, User, Building2, Globe, AlertCircle, CheckCircle2 } from "lucide-react";
import { useAuth } from "../context/AuthContext";
import { useToast } from "../context/ToastContext";
import { cn } from "../components/ui";

const BLOCKED = ["gmail.com","yahoo.com","hotmail.com","outlook.com","icloud.com","aol.com","protonmail.com","ymail.com"];

export default function Register() {
  const { register } = useAuth();
  const toast        = useToast();
  const navigate     = useNavigate();

  const [form, setForm] = useState({
    fullName: "", companyName: "", domain: "",
    email: "", password: "", confirm: "",
  });
  const [showPw, setShowPw]   = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError]     = useState("");

  const set = (k) => (e) => {
    const v = e.target.value;
    setForm(f => {
      const next = { ...f, [k]: v };
      // Auto-fill domain from email
      if (k === "email" && v.includes("@")) {
        const d = v.split("@")[1]?.toLowerCase() ?? "";
        if (d && !next.domain) next.domain = d;
      }
      return next;
    });
  };

  const emailDomain = form.email.includes("@") ? form.email.split("@")[1]?.toLowerCase() : "";
  const domainBlocked = BLOCKED.includes(emailDomain);

  const pwStrength = (() => {
    const p = form.password;
    if (!p) return 0;
    let s = 0;
    if (p.length >= 8)          s++;
    if (/[A-Z]/.test(p))        s++;
    if (/[0-9]/.test(p))        s++;
    if (/[^A-Za-z0-9]/.test(p)) s++;
    return s;
  })();

  const strengthColor = ["", "bg-red", "bg-orange", "bg-yellow-400", "bg-green"][pwStrength] ?? "bg-green";
  const strengthLabel = ["", "Weak", "Fair", "Good", "Strong"][pwStrength] ?? "Strong";

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    if (domainBlocked)              return setError("Personal email domains are not accepted. Use your work email.");
    if (form.password !== form.confirm) return setError("Passwords do not match.");
    if (form.password.length < 8)   return setError("Password must be at least 8 characters.");
    setLoading(true);
    try {
      await register({
        fullName:        form.fullName.trim(),
        companyName:     form.companyName.trim(),
        domain:          (form.domain || emailDomain || "").trim(),
        email:           form.email.trim(),
        password:        form.password,
        confirmPassword: form.confirm,
      });
      toast.success("Account created! Welcome to MOMBot Pro.");
      navigate("/");
    } catch (err) {
      setError(err.message ?? "Registration failed.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-bg flex items-center justify-center px-4 py-10">
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
          <h1 className="font-display font-bold text-txt text-2xl">Create your account</h1>
          <p className="text-sm text-muted mt-1">Start with 3 free meetings, no credit card required</p>
        </div>

        {/* Card */}
        <div className="bg-surface border border-border rounded-2xl p-8 shadow-xl">
          {error && (
            <div className="flex items-center gap-2 bg-red/10 border border-red/25 rounded-xl px-4 py-3 mb-5 text-sm text-red">
              <AlertCircle className="w-4 h-4 shrink-0" />
              {error}
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-4">
            {/* Full Name */}
            <div>
              <label className="block text-xs font-semibold text-txt mb-1.5">Full Name</label>
              <div className="relative">
                <User className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted" />
                <input
                  type="text"
                  required
                  value={form.fullName}
                  onChange={set("fullName")}
                  placeholder="John Smith"
                  className="w-full bg-surface2 border border-border rounded-xl pl-10 pr-4 py-2.5 text-sm text-txt placeholder:text-muted outline-none focus:border-accent transition-colors"
                />
              </div>
            </div>

            {/* Company */}
            <div>
              <label className="block text-xs font-semibold text-txt mb-1.5">Company Name</label>
              <div className="relative">
                <Building2 className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted" />
                <input
                  type="text"
                  required
                  value={form.companyName}
                  onChange={set("companyName")}
                  placeholder="Acme Corp"
                  className="w-full bg-surface2 border border-border rounded-xl pl-10 pr-4 py-2.5 text-sm text-txt placeholder:text-muted outline-none focus:border-accent transition-colors"
                />
              </div>
            </div>

            {/* Work Email */}
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
                  className={cn(
                    "w-full bg-surface2 border rounded-xl pl-10 pr-10 py-2.5 text-sm text-txt placeholder:text-muted outline-none transition-colors",
                    domainBlocked ? "border-red focus:border-red" : "border-border focus:border-accent"
                  )}
                />
                {form.email && !domainBlocked && emailDomain && (
                  <CheckCircle2 className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-green" />
                )}
              </div>
              {domainBlocked && (
                <p className="text-[11px] text-red mt-1">Personal email domains are not accepted.</p>
              )}
            </div>

            {/* Domain */}
            <div>
              <label className="block text-xs font-semibold text-txt mb-1.5">
                Company Domain <span className="text-muted font-normal">(auto-detected)</span>
              </label>
              <div className="relative">
                <Globe className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted" />
                <input
                  type="text"
                  value={form.domain || emailDomain}
                  onChange={set("domain")}
                  placeholder="company.com"
                  className="w-full bg-surface2 border border-border rounded-xl pl-10 pr-4 py-2.5 text-sm text-txt placeholder:text-muted outline-none focus:border-accent transition-colors"
                />
              </div>
            </div>

            {/* Password */}
            <div>
              <label className="block text-xs font-semibold text-txt mb-1.5">Password</label>
              <div className="relative">
                <Lock className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted" />
                <input
                  type={showPw ? "text" : "password"}
                  required
                  value={form.password}
                  onChange={set("password")}
                  placeholder="Min. 8 characters"
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
              {/* Strength bar */}
              {form.password && (
                <div className="mt-2">
                  <div className="flex gap-1">
                    {[1,2,3,4].map(i => (
                      <div key={i} className={cn("h-1 flex-1 rounded-full transition-all", i <= pwStrength ? strengthColor : "bg-border")} />
                    ))}
                  </div>
                  <p className="text-[11px] text-muted mt-1">Strength: <span className="font-semibold text-txt">{strengthLabel}</span></p>
                </div>
              )}
            </div>

            {/* Confirm Password */}
            <div>
              <label className="block text-xs font-semibold text-txt mb-1.5">Confirm Password</label>
              <div className="relative">
                <Lock className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted" />
                <input
                  type={showPw ? "text" : "password"}
                  required
                  value={form.confirm}
                  onChange={set("confirm")}
                  placeholder="••••••••"
                  className={cn(
                    "w-full bg-surface2 border rounded-xl pl-10 pr-4 py-2.5 text-sm text-txt placeholder:text-muted outline-none transition-colors",
                    form.confirm && form.confirm !== form.password ? "border-red" : "border-border focus:border-accent"
                  )}
                />
              </div>
            </div>

            {/* Submit */}
            <button
              type="submit"
              disabled={loading || domainBlocked}
              className={cn(
                "w-full flex items-center justify-center gap-2 py-2.5 rounded-xl text-sm font-semibold transition-all mt-2",
                "bg-accent text-white hover:bg-accent/90 shadow-lg shadow-accent/25",
                (loading || domainBlocked) && "opacity-60 cursor-not-allowed"
              )}
            >
              {loading ? (
                <>
                  <span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                  Creating account…
                </>
              ) : "Create Account — Free"}
            </button>
          </form>

          <div className="mt-5 text-center text-sm text-muted">
            Already have an account?{" "}
            <Link to="/login" className="text-accent hover:underline font-medium">
              Sign in
            </Link>
          </div>
        </div>

        <p className="text-center text-xs text-muted mt-6">
          By registering you agree to our Terms of Service and Privacy Policy.
        </p>
      </div>
    </div>
  );
}

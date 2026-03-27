import { clsx } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs) {
  return twMerge(clsx(inputs));
}

// ── Spinner ────────────────────────────────────────────────────────────────
export function Spinner({ size = "sm", className }) {
  const s = { sm: "w-4 h-4 border-2", md: "w-6 h-6 border-2", lg: "w-9 h-9 border-[3px]" }[size];
  return (
    <div className={cn("rounded-full border-white/20 border-t-white animate-spin", s, className)} />
  );
}

// ── Card ───────────────────────────────────────────────────────────────────
export function Card({ className, children, ...props }) {
  return (
    <div className={cn("bg-surface border border-border rounded-2xl", className)} {...props}>
      {children}
    </div>
  );
}

// ── Button ─────────────────────────────────────────────────────────────────
export function Button({ variant = "primary", size = "md", className, children, ...props }) {
  const base = "inline-flex items-center gap-2 font-medium rounded-xl transition-all duration-150 cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed";
  const variants = {
    primary:   "bg-accent hover:bg-accent/90 active:scale-[0.98] text-white border border-accent/20",
    secondary: "bg-surface2 hover:bg-border text-txt border border-border",
    ghost:     "bg-transparent hover:bg-surface2 text-muted hover:text-txt border border-transparent",
    danger:    "bg-red/10 hover:bg-red/20 text-red border border-red/20",
  };
  const sizes = {
    sm: "px-3 py-1.5 text-sm",
    md: "px-4 py-2.5 text-sm",
    lg: "px-5 py-3 text-base",
  };
  return (
    <button className={cn(base, variants[variant], sizes[size], className)} {...props}>
      {children}
    </button>
  );
}

// ── Badge ──────────────────────────────────────────────────────────────────
const BADGE_STYLES = {
  Done:      "bg-green/10 text-green border-green/20",
  Running:   "bg-accent/10 text-accent border-accent/20",
  Failed:    "bg-red/10 text-red border-red/20",
  Pending:   "bg-muted/10 text-muted border-muted/20",
  Waiting:   "bg-muted/10 text-muted border-muted/20",
  High:      "bg-red/10 text-red border-red/20",
  Medium:    "bg-orange/10 text-orange border-orange/20",
  Low:       "bg-green/10 text-green border-green/20",
  Open:      "bg-accent/10 text-accent border-accent/20",
  Recording: "bg-red/10 text-red border-red/20",
  InCall:    "bg-accent/10 text-accent border-accent/20",
  Joining:   "bg-orange/10 text-orange border-orange/20",
  Processing:"bg-orange/10 text-orange border-orange/20",
};

export function Badge({ label, variant, className, children }) {
  const key = variant ?? label ?? (typeof children === "string" ? children : "");
  const style = BADGE_STYLES[key] ?? "bg-muted/10 text-muted border-muted/20";
  return (
    <span className={cn("inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs font-semibold border", style, className)}>
      {children ?? label}
    </span>
  );
}

// ── Input ──────────────────────────────────────────────────────────────────
export function Input({ label, error, className, ...props }) {
  return (
    <div className="flex flex-col gap-1.5">
      {label && <label className="text-xs font-semibold text-muted uppercase tracking-wider">{label}</label>}
      <input
        className={cn(
          "bg-surface2 border border-border rounded-xl px-4 py-2.5 text-txt text-sm outline-none",
          "placeholder:text-muted focus:border-accent transition-colors",
          "disabled:opacity-50 disabled:cursor-not-allowed",
          error && "border-red",
          className
        )}
        {...props}
      />
      {error && <span className="text-xs text-red">{error}</span>}
    </div>
  );
}

// ── Textarea ───────────────────────────────────────────────────────────────
export function Textarea({ label, error, className, ...props }) {
  return (
    <div className="flex flex-col gap-1.5">
      {label && <label className="text-xs font-semibold text-muted uppercase tracking-wider">{label}</label>}
      <textarea
        className={cn(
          "bg-surface2 border border-border rounded-xl px-4 py-3 text-txt text-sm outline-none resize-none",
          "placeholder:text-muted focus:border-accent transition-colors",
          "disabled:opacity-50 disabled:cursor-not-allowed",
          error && "border-red",
          className
        )}
        {...props}
      />
      {error && <span className="text-xs text-red">{error}</span>}
    </div>
  );
}

// ── Tabs ───────────────────────────────────────────────────────────────────
export function Tabs({ tabs, active, onChange }) {
  return (
    <div className="flex gap-1 p-1 bg-surface2 rounded-xl border border-border">
      {tabs.map(({ key, label }) => (
        <button
          key={key}
          onClick={() => onChange(key)}
          className={cn(
            "flex-1 px-4 py-2 rounded-lg text-sm font-medium transition-all",
            active === key
              ? "bg-accent text-white shadow"
              : "text-muted hover:text-txt hover:bg-surface"
          )}
        >
          {label}
        </button>
      ))}
    </div>
  );
}

// ── StatCard ───────────────────────────────────────────────────────────────
export function StatCard({ label, value, icon, color = "accent", trend }) {
  const colors = {
    accent: "text-accent bg-accent/10 border-accent/20",
    green:  "text-green bg-green/10 border-green/20",
    orange: "text-orange bg-orange/10 border-orange/20",
    red:    "text-red bg-red/10 border-red/20",
  };
  return (
    <Card className="p-5">
      <div className="flex items-start justify-between">
        <div>
          <p className="text-xs font-semibold text-muted uppercase tracking-wider mb-2">{label}</p>
          <p className="text-3xl font-display font-bold text-txt">{value}</p>
          {trend && <p className="text-xs text-muted mt-1">{trend}</p>}
        </div>
        <div className={cn("w-10 h-10 rounded-xl flex items-center justify-center border text-lg", colors[color])}>
          {icon}
        </div>
      </div>
    </Card>
  );
}

// ── CodeBlock ──────────────────────────────────────────────────────────────
export function CodeBlock({ children, variant }) {
  const v = {
    default: "bg-surface2 border-border text-txt",
    red:     "bg-red/5 border-red/20 text-red/90",
    green:   "bg-green/5 border-green/20 text-green/90",
  }[variant ?? "default"];
  return (
    <pre className={cn("rounded-xl border p-4 text-xs leading-relaxed overflow-x-auto scrollbar-thin whitespace-pre-wrap break-words", v)}>
      {children}
    </pre>
  );
}

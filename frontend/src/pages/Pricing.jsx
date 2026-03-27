import { useState, useEffect } from "react";
import { CheckCircle2, Zap, CreditCard, Crown, Sparkles, X } from "lucide-react";
import { useAuth } from "../context/AuthContext";
import { useToast } from "../context/ToastContext";
import { cn } from "../components/ui";

const PLANS = [
  {
    id:       "free_trial",
    name:     "Free Trial",
    priceUSD: 0,
    priceINR: 0,
    period:   "",
    icon:     Sparkles,
    color:    "text-muted",
    border:   "border-border",
    badge:    null,
    features: [
      "3 Zoom meetings",
      "Full pipeline automation",
      "MOM generation",
      "Jira ticket creation",
      "GitHub PR automation",
      "Basic analytics",
    ],
    unavailable: ["Priority support", "Unlimited meetings", "Custom integrations"],
  },
  {
    id:       "monthly",
    name:     "Monthly",
    priceUSD: 12,
    priceINR: 1000,
    period:   "/ month",
    icon:     CreditCard,
    color:    "text-accent",
    border:   "border-accent/30",
    badge:    "Most Popular",
    features: [
      "Unlimited meetings",
      "Full pipeline automation",
      "MOM + Jira + GitHub PR",
      "Advanced analytics",
      "Priority support",
      "Email notifications",
    ],
    unavailable: ["Custom integrations"],
  },
  {
    id:       "yearly",
    name:     "Yearly",
    priceUSD: 84,
    priceINR: 7000,
    period:   "/ year",
    icon:     Crown,
    color:    "text-orange",
    border:   "border-orange/30",
    badge:    "Save 42%",
    features: [
      "Unlimited meetings",
      "Full pipeline automation",
      "MOM + Jira + GitHub PR",
      "Advanced analytics",
      "Priority support",
      "Email notifications",
      "Custom integrations",
    ],
    unavailable: [],
  },
];

function UpgradeModal({ plan, onClose, onConfirm, loading }) {
  return (
    <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center px-4" onClick={onClose}>
      <div className="bg-surface border border-border rounded-2xl p-8 w-full max-w-md shadow-2xl" onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between mb-5">
          <h3 className="font-display font-bold text-txt text-lg">Upgrade to {plan.name}</h3>
          <button onClick={onClose} className="text-muted hover:text-txt"><X className="w-5 h-5" /></button>
        </div>

        <div className="bg-surface2 border border-border rounded-xl p-4 mb-5">
          <div className="flex items-baseline gap-2">
            <span className="text-3xl font-display font-bold text-txt">${plan.priceUSD}</span>
            <span className="text-muted text-sm">{plan.period}</span>
            <span className="ml-auto text-muted text-sm">₹{plan.priceINR}{plan.period}</span>
          </div>
          <p className="text-xs text-muted mt-1">MOMBot Pro {plan.name} Plan</p>
        </div>

        <div className="space-y-2 mb-6">
          {plan.features.map(f => (
            <div key={f} className="flex items-center gap-2 text-sm text-txt">
              <CheckCircle2 className="w-4 h-4 text-green shrink-0" /> {f}
            </div>
          ))}
        </div>

        <div className="flex gap-3">
          <button
            onClick={onClose}
            className="flex-1 py-2.5 rounded-xl border border-border text-sm text-muted hover:text-txt hover:border-accent/30 transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={() => onConfirm(plan.id)}
            disabled={loading}
            className="flex-1 flex items-center justify-center gap-2 py-2.5 rounded-xl bg-accent text-white text-sm font-semibold hover:bg-accent/90 transition-colors disabled:opacity-60"
          >
            {loading ? (
              <span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
            ) : (
              <><CreditCard className="w-4 h-4" /> Confirm Upgrade</>
            )}
          </button>
        </div>
      </div>
    </div>
  );
}

export default function Pricing() {
  const { user, authFetch } = useAuth();
  const toast               = useToast();
  const [currency, setCurrency] = useState("usd");
  const [selected, setSelected] = useState(null);
  const [loading, setLoading]   = useState(false);

  const handleUpgrade = async (planId) => {
    setLoading(true);
    try {
      const res = await authFetch("http://localhost:5000/api/subscription/upgrade", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ plan: planId }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error ?? "Upgrade failed.");
      toast.success(`Upgraded to ${planId} plan successfully!`);
      setSelected(null);
      // Refresh page to update user plan badge
      window.location.reload();
    } catch (err) {
      toast.error(err.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="max-w-5xl mx-auto space-y-10 pb-10">
      {/* Header */}
      <div className="text-center pt-2">
        <div className="inline-flex items-center gap-2 bg-accent/10 border border-accent/20 rounded-full px-4 py-1.5 text-xs font-semibold text-accent mb-3">
          <Zap className="w-3.5 h-3.5" /> Simple, Transparent Pricing
        </div>
        <h1 className="font-display font-bold text-txt text-3xl mb-2">Choose Your Plan</h1>
        <p className="text-muted text-sm">Start free, upgrade when you need more meetings</p>

        {/* Currency toggle */}
        <div className="flex items-center justify-center gap-1 mt-5 bg-surface2 border border-border rounded-xl p-1 w-fit mx-auto">
          {["usd", "inr"].map(c => (
            <button
              key={c}
              onClick={() => setCurrency(c)}
              className={cn(
                "px-4 py-1.5 rounded-lg text-xs font-semibold transition-all",
                currency === c ? "bg-accent text-white" : "text-muted hover:text-txt"
              )}
            >
              {c === "usd" ? "$ USD" : "₹ INR"}
            </button>
          ))}
        </div>
      </div>

      {/* Current plan indicator */}
      {user?.subscriptionPlan && (
        <div className="bg-green/10 border border-green/20 rounded-xl px-5 py-3 text-sm text-green flex items-center gap-2 max-w-sm mx-auto">
          <CheckCircle2 className="w-4 h-4 shrink-0" />
          Current plan: <strong className="capitalize">{user.subscriptionPlan.replace("_", " ")}</strong>
          {user.subscriptionPlan === "free_trial" && (
            <span className="ml-auto text-orange font-bold">{user.freeTrialMeetingsLeft} left</span>
          )}
        </div>
      )}

      {/* Plan cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        {PLANS.map(plan => {
          const Icon      = plan.icon;
          const isCurrent = user?.subscriptionPlan === plan.id;
          const price     = currency === "usd" ? `$${plan.priceUSD}` : `₹${plan.priceINR}`;

          return (
            <div
              key={plan.id}
              className={cn(
                "relative bg-surface border rounded-2xl p-7 flex flex-col transition-all",
                plan.border,
                plan.badge === "Most Popular" && "shadow-lg shadow-accent/10",
                isCurrent && "ring-2 ring-green/40"
              )}
            >
              {/* Badge */}
              {plan.badge && (
                <div className={cn(
                  "absolute -top-3 left-1/2 -translate-x-1/2 px-3 py-1 rounded-full text-[11px] font-bold text-white",
                  plan.badge === "Most Popular" ? "bg-accent" : "bg-orange"
                )}>
                  {plan.badge}
                </div>
              )}

              {/* Icon + name */}
              <div className="flex items-center gap-3 mb-5">
                <div className={cn("w-10 h-10 rounded-xl flex items-center justify-center", `bg-surface2 border border-border`)}>
                  <Icon className={cn("w-5 h-5", plan.color)} />
                </div>
                <div>
                  <p className="font-display font-bold text-txt text-base">{plan.name}</p>
                  {isCurrent && <p className="text-[10px] text-green font-semibold">Current Plan</p>}
                </div>
              </div>

              {/* Price */}
              <div className="mb-6">
                <div className="flex items-baseline gap-1">
                  <span className="text-4xl font-display font-black text-txt">{price}</span>
                  <span className="text-muted text-sm">{plan.period}</span>
                </div>
                {plan.id === "yearly" && (
                  <p className="text-xs text-orange font-semibold mt-1">That's just $7/month</p>
                )}
              </div>

              {/* Features */}
              <div className="space-y-2.5 flex-1 mb-6">
                {plan.features.map(f => (
                  <div key={f} className="flex items-center gap-2 text-sm text-txt">
                    <CheckCircle2 className="w-3.5 h-3.5 text-green shrink-0" /> {f}
                  </div>
                ))}
                {plan.unavailable.map(f => (
                  <div key={f} className="flex items-center gap-2 text-sm text-muted line-through">
                    <X className="w-3.5 h-3.5 shrink-0" /> {f}
                  </div>
                ))}
              </div>

              {/* CTA */}
              {isCurrent ? (
                <div className="w-full py-2.5 rounded-xl border border-green/30 text-green text-sm font-semibold text-center">
                  Active Plan
                </div>
              ) : plan.id === "free_trial" ? (
                <div className="w-full py-2.5 rounded-xl border border-border text-muted text-sm font-semibold text-center cursor-default">
                  Current Starter
                </div>
              ) : (
                <button
                  onClick={() => setSelected(plan)}
                  className={cn(
                    "w-full py-2.5 rounded-xl text-sm font-semibold transition-all",
                    plan.id === "monthly"
                      ? "bg-accent text-white hover:bg-accent/90 shadow-md shadow-accent/20"
                      : "bg-orange/15 border border-orange/30 text-orange hover:bg-orange/25"
                  )}
                >
                  Upgrade to {plan.name}
                </button>
              )}
            </div>
          );
        })}
      </div>

      {/* FAQ */}
      <div className="max-w-2xl mx-auto space-y-4">
        <h2 className="font-display font-bold text-txt text-xl text-center mb-6">Frequently Asked Questions</h2>
        {[
          { q: "What counts as a 'meeting'?", a: "Each Zoom call where the bot joins and completes a pipeline run counts as one meeting." },
          { q: "Can I cancel anytime?", a: "Yes. Monthly and yearly plans can be cancelled at any time. Your access continues until the period ends." },
          { q: "What payment methods are accepted?", a: "We accept all major credit/debit cards, UPI, and net banking (INR plans)." },
          { q: "Is my data secure?", a: "Yes. Transcripts and meeting data are processed in-memory and never stored permanently without your consent." },
        ].map(({ q, a }) => (
          <div key={q} className="bg-surface border border-border rounded-xl p-5">
            <p className="text-sm font-semibold text-txt mb-1">{q}</p>
            <p className="text-xs text-muted leading-relaxed">{a}</p>
          </div>
        ))}
      </div>

      {/* Upgrade modal */}
      {selected && (
        <UpgradeModal
          plan={selected}
          onClose={() => setSelected(null)}
          onConfirm={handleUpgrade}
          loading={loading}
        />
      )}
    </div>
  );
}

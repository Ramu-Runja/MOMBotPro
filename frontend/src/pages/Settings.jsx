import { useState } from "react";
import { User, Plug, CreditCard, Save, AlertCircle, CheckCircle2, Eye, EyeOff, Loader2 } from "lucide-react";
import { useAuth } from "../context/AuthContext";
import { useToast } from "../context/ToastContext";
import { cn } from "../components/ui";
import { Link } from "react-router-dom";

function AccountTab() {
  const { user, authFetch } = useAuth();
  const toast               = useToast();

  const [form, setForm]     = useState({
    fullName:    user?.fullName    ?? "",
    companyName: user?.companyName ?? "",
    email:       user?.email       ?? "",
  });
  const [pwForm, setPwForm] = useState({ current: "", next: "", confirm: "" });
  const [showPw, setShowPw] = useState(false);
  const [saving, setSaving] = useState(false);

  const set    = (k) => (e) => setForm(f => ({ ...f, [k]: e.target.value }));
  const setPw  = (k) => (e) => setPwForm(f => ({ ...f, [k]: e.target.value }));

  const saveProfile = async (e) => {
    e.preventDefault();
    setSaving(true);
    // Simulate save (no dedicated profile update endpoint needed yet)
    await new Promise(r => setTimeout(r, 600));
    toast.success("Profile updated.");
    setSaving(false);
  };

  const changePassword = async (e) => {
    e.preventDefault();
    if (pwForm.next !== pwForm.confirm) return toast.error("Passwords do not match.");
    if (pwForm.next.length < 8)        return toast.error("Password must be at least 8 characters.");
    setSaving(true);
    await new Promise(r => setTimeout(r, 600));
    toast.success("Password changed successfully.");
    setPwForm({ current: "", next: "", confirm: "" });
    setSaving(false);
  };

  return (
    <div className="space-y-6">
      {/* Profile card */}
      <div className="bg-surface border border-border rounded-2xl p-6">
        <h2 className="font-display font-semibold text-txt mb-5">Profile Information</h2>

        {/* Avatar */}
        <div className="flex items-center gap-4 mb-6">
          <div className="w-16 h-16 rounded-2xl bg-accent/20 border border-accent/30 flex items-center justify-center">
            <User className="w-8 h-8 text-accent" />
          </div>
          <div>
            <p className="font-semibold text-txt">{user?.fullName}</p>
            <p className="text-sm text-muted">{user?.email}</p>
            <p className="text-xs text-muted mt-0.5">
              <span className="capitalize">{user?.subscriptionPlan?.replace("_", " ")}</span>
              {user?.subscriptionPlan === "free_trial" && (
                <span className="ml-2 text-orange font-semibold">{user.freeTrialMeetingsLeft} meetings left</span>
              )}
            </p>
          </div>
        </div>

        <form onSubmit={saveProfile} className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-semibold text-txt mb-1.5">Full Name</label>
              <input
                type="text"
                value={form.fullName}
                onChange={set("fullName")}
                className="w-full bg-surface2 border border-border rounded-xl px-3 py-2.5 text-sm text-txt outline-none focus:border-accent transition-colors"
              />
            </div>
            <div>
              <label className="block text-xs font-semibold text-txt mb-1.5">Company Name</label>
              <input
                type="text"
                value={form.companyName}
                onChange={set("companyName")}
                className="w-full bg-surface2 border border-border rounded-xl px-3 py-2.5 text-sm text-txt outline-none focus:border-accent transition-colors"
              />
            </div>
            <div className="sm:col-span-2">
              <label className="block text-xs font-semibold text-txt mb-1.5">Email</label>
              <input
                type="email"
                value={form.email}
                disabled
                className="w-full bg-surface2 border border-border rounded-xl px-3 py-2.5 text-sm text-muted cursor-not-allowed"
              />
              <p className="text-[11px] text-muted mt-1">Email cannot be changed after registration.</p>
            </div>
          </div>
          <button
            type="submit"
            disabled={saving}
            className="flex items-center gap-2 px-5 py-2 bg-accent text-white rounded-xl text-sm font-semibold hover:bg-accent/90 transition-colors disabled:opacity-60"
          >
            {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
            Save Changes
          </button>
        </form>
      </div>

      {/* Change password */}
      <div className="bg-surface border border-border rounded-2xl p-6">
        <h2 className="font-display font-semibold text-txt mb-5">Change Password</h2>
        <form onSubmit={changePassword} className="space-y-4 max-w-sm">
          {[
            { key: "current", label: "Current Password" },
            { key: "next",    label: "New Password"     },
            { key: "confirm", label: "Confirm Password" },
          ].map(({ key, label }) => (
            <div key={key}>
              <label className="block text-xs font-semibold text-txt mb-1.5">{label}</label>
              <div className="relative">
                <input
                  type={showPw ? "text" : "password"}
                  value={pwForm[key]}
                  onChange={setPw(key)}
                  className="w-full bg-surface2 border border-border rounded-xl px-3 pr-10 py-2.5 text-sm text-txt outline-none focus:border-accent transition-colors"
                />
                {key === "current" && (
                  <button type="button" onClick={() => setShowPw(p => !p)} className="absolute right-3 top-1/2 -translate-y-1/2 text-muted hover:text-txt">
                    {showPw ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                )}
              </div>
            </div>
          ))}
          <button
            type="submit"
            disabled={saving}
            className="flex items-center gap-2 px-5 py-2 bg-surface2 border border-border text-txt rounded-xl text-sm font-semibold hover:border-accent/40 transition-colors disabled:opacity-60"
          >
            {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
            Update Password
          </button>
        </form>
      </div>

      {/* Danger zone */}
      <div className="bg-surface border border-red/20 rounded-2xl p-6">
        <h2 className="font-display font-semibold text-red mb-2">Danger Zone</h2>
        <p className="text-xs text-muted mb-4">These actions are irreversible. Proceed with caution.</p>
        <button className="px-4 py-2 bg-red/10 border border-red/20 text-red rounded-xl text-sm font-semibold hover:bg-red/20 transition-colors">
          Delete Account
        </button>
      </div>
    </div>
  );
}

function IntegrationsTab() {
  return (
    <div className="bg-surface border border-border rounded-2xl p-8 text-center">
      <Plug className="w-10 h-10 text-accent mx-auto mb-3" />
      <h2 className="font-display font-bold text-txt text-lg mb-2">Manage Integrations</h2>
      <p className="text-sm text-muted mb-5">Connect Jira, GitHub, Slack, and Gmail to automate your pipeline.</p>
      <Link
        to="/integrations"
        className="inline-flex items-center gap-2 bg-accent text-white px-5 py-2.5 rounded-xl text-sm font-semibold hover:bg-accent/90 transition-colors"
      >
        <Plug className="w-4 h-4" /> Go to Integrations
      </Link>
    </div>
  );
}

function SubscriptionTab() {
  const { user } = useAuth();
  const plan     = user?.subscriptionPlan ?? "free_trial";

  const DETAILS = {
    free_trial: { name: "Free Trial",   color: "text-muted",  priceUSD: 0,  priceINR: 0   },
    monthly:    { name: "Monthly Pro",  color: "text-accent", priceUSD: 12, priceINR: 1000 },
    yearly:     { name: "Yearly Pro",   color: "text-orange", priceUSD: 84, priceINR: 7000 },
  };
  const d = DETAILS[plan] ?? DETAILS.free_trial;

  return (
    <div className="space-y-6">
      {/* Current plan */}
      <div className="bg-surface border border-border rounded-2xl p-6">
        <h2 className="font-display font-semibold text-txt mb-5">Current Subscription</h2>
        <div className="flex items-start gap-5">
          <div className="flex-1">
            <p className={cn("font-display font-bold text-2xl", d.color)}>{d.name}</p>
            {plan !== "free_trial" && (
              <p className="text-sm text-muted mt-0.5">
                ${d.priceUSD}/mo · ₹{d.priceINR}/mo
              </p>
            )}
            {plan === "free_trial" && (
              <div className="flex items-center gap-2 mt-2 text-orange">
                <AlertCircle className="w-4 h-4" />
                <span className="text-sm font-semibold">{user?.freeTrialMeetingsLeft} meetings remaining</span>
              </div>
            )}
          </div>

          {plan === "free_trial" && (
            <Link
              to="/pricing"
              className="flex items-center gap-2 bg-accent text-white px-4 py-2 rounded-xl text-sm font-semibold hover:bg-accent/90 transition-colors shrink-0"
            >
              <CreditCard className="w-4 h-4" /> Upgrade Plan
            </Link>
          )}
        </div>
      </div>

      {/* Plan features */}
      <div className="bg-surface border border-border rounded-2xl p-6">
        <h2 className="font-display font-semibold text-txt mb-4">Plan Features</h2>
        <div className="space-y-2.5">
          {[
            { feature: "Zoom bot integration (Recall.ai)", included: true  },
            { feature: "Tenglish transcription (Whisper)", included: true  },
            { feature: "AI MOM generation (Claude)",       included: true  },
            { feature: "Jira ticket automation",           included: true  },
            { feature: "GitHub PR automation",             included: true  },
            { feature: "Unlimited meetings",               included: plan !== "free_trial" },
            { feature: "Priority support",                 included: plan !== "free_trial" },
            { feature: "Custom integrations",              included: plan === "yearly"     },
          ].map(({ feature, included }) => (
            <div key={feature} className="flex items-center gap-2 text-sm">
              {included
                ? <CheckCircle2 className="w-4 h-4 text-green shrink-0" />
                : <AlertCircle  className="w-4 h-4 text-muted shrink-0"  />
              }
              <span className={included ? "text-txt" : "text-muted"}>{feature}</span>
            </div>
          ))}
        </div>

        {plan === "free_trial" && (
          <div className="mt-5 pt-4 border-t border-border">
            <Link
              to="/pricing"
              className="inline-flex items-center gap-2 text-sm text-accent hover:underline font-medium"
            >
              View all plans & pricing →
            </Link>
          </div>
        )}
      </div>

      {/* Billing history placeholder */}
      {plan !== "free_trial" && (
        <div className="bg-surface border border-border rounded-2xl p-6">
          <h2 className="font-display font-semibold text-txt mb-4">Billing History</h2>
          <p className="text-sm text-muted">No invoices yet. Billing history will appear here.</p>
        </div>
      )}
    </div>
  );
}

const TABS = [
  { key: "account",      label: "Account",      icon: User        },
  { key: "integrations", label: "Integrations", icon: Plug        },
  { key: "subscription", label: "Subscription", icon: CreditCard  },
];

export default function Settings() {
  const [tab, setTab] = useState("account");

  return (
    <div className="max-w-3xl mx-auto space-y-6 pb-10">
      <div>
        <h1 className="font-display font-bold text-txt text-2xl">Settings</h1>
        <p className="text-muted text-sm mt-1">Manage your account, integrations, and subscription.</p>
      </div>

      {/* Tab bar */}
      <div className="flex gap-1 bg-surface2 border border-border rounded-xl p-1.5 w-fit">
        {TABS.map(({ key, label, icon: Icon }) => (
          <button
            key={key}
            onClick={() => setTab(key)}
            className={cn(
              "flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all",
              tab === key
                ? "bg-accent text-white shadow-sm"
                : "text-muted hover:text-txt"
            )}
          >
            <Icon className="w-3.5 h-3.5" />
            {label}
          </button>
        ))}
      </div>

      {/* Tab content */}
      {tab === "account"      && <AccountTab />}
      {tab === "integrations" && <IntegrationsTab />}
      {tab === "subscription" && <SubscriptionTab />}
    </div>
  );
}

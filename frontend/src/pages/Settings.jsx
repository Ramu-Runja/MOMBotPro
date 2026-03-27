import { useState } from "react";
import {
  User, CreditCard, Save, AlertCircle, CheckCircle2,
  Eye, EyeOff, Loader2, Cpu, Bell,
} from "lucide-react";
import { useAuth } from "../context/AuthContext";
import { useToast } from "../context/ToastContext";
import { cn } from "../components/ui";
import { Link } from "react-router-dom";
import Pricing from "./Pricing";

// ── Account Tab ──────────────────────────────────────────────────
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

  const set   = (k) => (e) => setForm(f  => ({ ...f,  [k]: e.target.value }));
  const setPw = (k) => (e) => setPwForm(f => ({ ...f,  [k]: e.target.value }));

  const saveProfile = async (e) => {
    e.preventDefault();
    setSaving(true);
    await new Promise(r => setTimeout(r, 600));
    toast.success("Profile updated.");
    setSaving(false);
  };

  const changePassword = async (e) => {
    e.preventDefault();
    if (pwForm.next !== pwForm.confirm) return toast.error("Passwords do not match.");
    if (pwForm.next.length < 8)         return toast.error("Password must be at least 8 characters.");
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
        <div className="flex items-center gap-4 mb-6">
          <div className="w-16 h-16 rounded-2xl bg-accent/20 border border-accent/30 flex items-center justify-center">
            <User className="w-8 h-8 text-accent" />
          </div>
          <div>
            <p className="font-semibold text-txt">{user?.fullName}</p>
            <p className="text-sm text-muted">{user?.email}</p>
            <p className="text-xs text-muted mt-0.5 capitalize">
              {user?.subscriptionPlan?.replace("_", " ")}
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
                type="text" value={form.fullName} onChange={set("fullName")}
                className="w-full bg-surface2 border border-border rounded-xl px-3 py-2.5 text-sm text-txt outline-none focus:border-accent transition-colors"
              />
            </div>
            <div>
              <label className="block text-xs font-semibold text-txt mb-1.5">Company Name</label>
              <input
                type="text" value={form.companyName} onChange={set("companyName")}
                className="w-full bg-surface2 border border-border rounded-xl px-3 py-2.5 text-sm text-txt outline-none focus:border-accent transition-colors"
              />
            </div>
            <div className="sm:col-span-2">
              <label className="block text-xs font-semibold text-txt mb-1.5">Email</label>
              <input
                type="email" value={form.email} disabled
                className="w-full bg-surface2 border border-border rounded-xl px-3 py-2.5 text-sm text-muted cursor-not-allowed"
              />
              <p className="text-[11px] text-muted mt-1">Email cannot be changed after registration.</p>
            </div>
          </div>
          <button
            type="submit" disabled={saving}
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
                  value={pwForm[key]} onChange={setPw(key)}
                  className="w-full bg-surface2 border border-border rounded-xl px-3 pr-10 py-2.5 text-sm text-txt outline-none focus:border-accent transition-colors"
                />
                {key === "current" && (
                  <button
                    type="button" onClick={() => setShowPw(p => !p)}
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-muted hover:text-txt"
                  >
                    {showPw ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                )}
              </div>
            </div>
          ))}
          <button
            type="submit" disabled={saving}
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

// ── AI Models Tab ────────────────────────────────────────────────
function AIModelsTab() {
  const toast = useToast();
  const [saved, setSaved] = useState(false);
  const [prefs, setPrefs] = useState(() => {
    try {
      return JSON.parse(localStorage.getItem("mbp_ai_prefs") ?? "{}");
    } catch { return {}; }
  });
  const setP = (k) => (e) => setPrefs(p => ({ ...p, [k]: e.target.value }));

  const MODELS = [
    {
      key:     "transcription",
      label:   "Transcription Model",
      desc:    "Used to convert speech to text",
      default: "Whisper Large v3",
      options: ["Whisper Large v3", "Whisper Medium", "Whisper Small"],
    },
    {
      key:     "analysis",
      label:   "Analysis Model",
      desc:    "Used to extract action items and summaries",
      default: "Claude Sonnet 4.6",
      options: ["Claude Sonnet 4.6", "Claude Haiku 4.5", "Claude Opus 4.6", "GPT-4o"],
    },
    {
      key:     "codeGen",
      label:   "Code Generation Model",
      desc:    "Used to generate code fixes",
      default: "Claude Sonnet 4.6",
      options: ["Claude Sonnet 4.6", "Claude Haiku 4.5", "Claude Opus 4.6", "GPT-4o"],
    },
  ];

  const save = () => {
    localStorage.setItem("mbp_ai_prefs", JSON.stringify(prefs));
    toast.success("AI preferences saved.");
    setSaved(true);
    setTimeout(() => setSaved(false), 2000);
  };

  return (
    <div className="bg-surface border border-border rounded-2xl p-6">
      <h2 className="font-display font-semibold text-txt mb-5">AI Model Preferences</h2>
      {MODELS.map(({ key, label, desc, default: def, options }) => (
        <div key={key} className="mb-5">
          <label className="block text-xs font-semibold text-txt">{label}</label>
          <div className="text-xs text-muted mb-2">{desc}</div>
          <select
            value={prefs[key] ?? def}
            onChange={setP(key)}
            className="w-full bg-surface2 border border-border rounded-xl px-3 py-2.5 text-sm text-txt outline-none focus:border-accent transition-colors cursor-pointer"
          >
            {options.map(o => <option key={o} value={o}>{o}</option>)}
          </select>
        </div>
      ))}
      <button
        onClick={save}
        className="flex items-center gap-2 px-5 py-2 bg-accent text-white rounded-xl text-sm font-semibold hover:bg-accent/90 transition-colors"
      >
        {saved ? <CheckCircle2 className="w-4 h-4" /> : <Save className="w-4 h-4" />}
        {saved ? "Saved!" : "Save Preferences"}
      </button>
    </div>
  );
}

// ── Notifications Tab ────────────────────────────────────────────
function NotificationsTab() {
  const toast = useToast();
  const [notifs, setNotifs] = useState(() => {
    try {
      return JSON.parse(localStorage.getItem("mbp_notif_prefs") ?? "null") ?? {
        pipelineCompleted: true,
        pipelineFailed:    true,
        jiraCreated:       true,
        prRaised:          true,
        weeklySummary:     false,
      };
    } catch {
      return { pipelineCompleted: true, pipelineFailed: true, jiraCreated: true, prRaised: true, weeklySummary: false };
    }
  });

  const toggle = (k) => setNotifs(n => ({ ...n, [k]: !n[k] }));
  const save   = () => {
    localStorage.setItem("mbp_notif_prefs", JSON.stringify(notifs));
    toast.success("Notification preferences saved.");
  };

  const ITEMS = [
    { key: "pipelineCompleted", label: "Pipeline Completed",     desc: "Get notified when a pipeline finishes successfully" },
    { key: "pipelineFailed",    label: "Pipeline Failed",         desc: "Get notified when a pipeline encounters an error"   },
    { key: "jiraCreated",       label: "Jira Ticket Created",     desc: "Get notified when tickets are auto-created"         },
    { key: "prRaised",          label: "PR Raised",               desc: "Get notified when a pull request is raised on GitHub" },
    { key: "weeklySummary",     label: "Weekly Summary",          desc: "Receive a weekly digest of all pipeline activity"   },
  ];

  return (
    <div className="bg-surface border border-border rounded-2xl p-6">
      <h2 className="font-display font-semibold text-txt mb-5">Notification Preferences</h2>
      <div className="space-y-0">
        {ITEMS.map(({ key, label, desc }, i) => (
          <div
            key={key}
            className={cn(
              "flex items-center justify-between py-4",
              i < ITEMS.length - 1 && "border-b border-border"
            )}
          >
            <div>
              <div className="text-sm font-medium text-txt">{label}</div>
              <div className="text-xs text-muted mt-0.5">{desc}</div>
            </div>
            {/* Toggle switch */}
            <button
              onClick={() => toggle(key)}
              className={cn(
                "relative w-11 h-6 rounded-full transition-colors duration-200 shrink-0",
                notifs[key] ? "bg-accent" : "bg-surface2 border border-border"
              )}
            >
              <span
                className={cn(
                  "absolute w-[18px] h-[18px] bg-white rounded-full top-[3px] transition-all duration-200 shadow-sm",
                  notifs[key] ? "left-[22px]" : "left-[3px]"
                )}
              />
            </button>
          </div>
        ))}
      </div>
      <button
        onClick={save}
        className="mt-5 flex items-center gap-2 px-5 py-2 bg-accent text-white rounded-xl text-sm font-semibold hover:bg-accent/90 transition-colors"
      >
        <Save className="w-4 h-4" /> Save Notifications
      </button>
    </div>
  );
}

// ── Subscription Tab ─────────────────────────────────────────────

function SubscriptionTab() {
  return (
    <div className="w-full">
      <Pricing />
    </div>
  );
}

// ── Tabs config ──────────────────────────────────────────────────
const TABS = [
  { key: "account",      label: "Account",       icon: User        },
  { key: "ai",           label: "AI Models",     icon: Cpu         },
  { key: "notifications",label: "Notifications", icon: Bell        },
  { key: "subscription", label: "Subscription",  icon: CreditCard  },
];

// ── Page ─────────────────────────────────────────────────────────
export default function Settings() {
  const [tab, setTab]   = useState("account");
  const tabIndex        = TABS.findIndex(t => t.key === tab);

  return (
    <div className="max-w-3xl mx-auto space-y-6 pb-10">

      {/* Tab bar */}
      <div className="relative flex bg-surface2 border border-border rounded-xl p-1.5">
        {/* Sliding indicator */}
        <div
          className="absolute top-1.5 bottom-1.5 left-1.5 rounded-lg bg-[#111] dark:bg-accent shadow-sm pointer-events-none"
          style={{
            width: `calc((100% - 12px) / ${TABS.length})`,
            transform: `translateX(calc(${tabIndex} * 100%))`,
            transition: "transform 300ms cubic-bezier(0.4, 0, 0.2, 1)",
          }}
        />
        {TABS.map(({ key, label, icon: Icon }) => (
          <button
            key={key}
            onClick={() => setTab(key)}
            className={cn(
              "relative z-10 flex-1 flex items-center justify-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-colors duration-200",
              tab === key ? "text-white" : "text-muted hover:text-txt"
            )}
          >
            <Icon className="w-3.5 h-3.5" />
            {label}
          </button>
        ))}
      </div>

      {/* Tab content */}
      {tab === "account"       && <AccountTab />}
      {tab === "ai"            && <AIModelsTab />}
      {tab === "notifications" && <NotificationsTab />}
      {tab === "subscription"  && <SubscriptionTab />}
    </div>
  );
}

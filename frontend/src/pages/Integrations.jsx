import { useState, useEffect } from "react";
import {
  Plug, CheckCircle2, XCircle, Loader2, AlertCircle,
  Trash2, TestTube2, ChevronDown, ChevronUp,
  Zap, RefreshCw, Settings, Calendar, Video, RepeatIcon,
} from "lucide-react";
import { useToast } from "../context/ToastContext";
import { authFetch, getToken } from "../utils/authFetch";
import { cn } from "../components/ui";

const INTEGRATION_DEFS = [
  {
    type:  "zoom",
    name:  "Zoom",
    color: "text-[#2D8CFF]",
    bg:    "bg-[#2D8CFF]/10 border-[#2D8CFF]/20",
    desc:  "Auto-join scheduled meetings and sync your Zoom calendar.",
    oauth: true,
    oauthLabel:     "Connect with Zoom",
    oauthBtnClass:  "bg-[#2D8CFF] hover:bg-[#1a7ae8] text-white",
    connectedLabel: (i) => i.email ?? "Zoom Account",
    connectedPrefix:"Connected as",
    fields: [],
  },
  {
    type:  "jira",
    name:  "Jira",
    logo:  "https://cdn.worldvectorlogo.com/logos/jira-1.svg",
    color: "text-blue-400",
    bg:    "bg-blue-400/10 border-blue-400/20",
    desc:  "Auto-create Jira tickets from meeting bug reports.",
    oauth: true,
    oauthLabel: "Connect with Jira",
    oauthBtnClass: "bg-blue-600 hover:bg-blue-500 text-white",
    connectedLabel: (i) => i.domain ?? "Atlassian",
    connectedPrefix: "Connected to",
    fields: [
      { key: "domain",   label: "Jira Domain",    placeholder: "yourteam.atlassian.net"   },
      { key: "email",    label: "Account Email",   placeholder: "you@company.com"          },
      { key: "apiToken", label: "API Token",       placeholder: "ATATT3xF..."              },
      { key: "project",  label: "Project Key",     placeholder: "MBP"                     },
    ],
  },
  {
    type:  "github",
    name:  "GitHub",
    logo:  "https://github.githubassets.com/images/modules/logos_page/GitHub-Mark.png",
    color: "text-txt",
    bg:    "bg-surface2 border-border",
    desc:  "Auto-create branches and raise PRs on your repository.",
    oauth: true,
    oauthLabel: "Connect with GitHub",
    oauthBtnClass: "bg-[#238636] hover:bg-[#2ea043] text-white",
    connectedLabel: (i) => i.owner ?? "GitHub",
    connectedPrefix: "Connected as",
    fields: [
      { key: "token", label: "Personal Access Token", placeholder: "ghp_..."         },
      { key: "owner", label: "Repo Owner",            placeholder: "your-username"   },
      { key: "repo",  label: "Repository Name",       placeholder: "my-project"      },
    ],
  },
  {
    type:  "slack",
    name:  "Slack",
    logo:  "https://cdn.worldvectorlogo.com/logos/slack-new-logo.svg",
    color: "text-green",
    bg:    "bg-green/10 border-green/20",
    desc:  "Get pipeline completion notifications in your Slack channel.",
    oauth: false,
    fields: [
      { key: "webhookUrl",  label: "Webhook URL",    placeholder: "https://hooks.slack.com/..."    },
      { key: "channelName", label: "Channel Name",   placeholder: "#meetings"                      },
    ],
  },
  {
    type:  "gmail",
    name:  "Gmail",
    logo:  "https://ssl.gstatic.com/ui/v1/icons/mail/rfr/gmail.ico",
    color: "text-red",
    bg:    "bg-red/10 border-red/20",
    desc:  "Send MOM email summaries directly to client inboxes.",
    oauth: true,
    oauthLabel: "Connect with Google",
    oauthBtnClass: "bg-white hover:bg-gray-50 text-gray-700 border border-gray-300",
    connectedLabel: (i) => i.email ?? "Gmail account",
    connectedPrefix: "Connected as",
    fields: [
      { key: "fromEmail",  label: "From Email",   placeholder: "you@company.com"    },
      { key: "appPassword",label: "App Password", placeholder: "xxxx xxxx xxxx xxxx"},
      { key: "toEmail",    label: "Default To",   placeholder: "client@example.com" },
    ],
  },
];

// ── OAuth logo SVGs (inline, no extra deps) ──────────────────
function GitHubLogo({ className }) {
  return (
    <svg className={className} viewBox="0 0 16 16" fill="currentColor">
      <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"/>
    </svg>
  );
}

function GoogleLogo({ className }) {
  return (
    <svg className={className} viewBox="0 0 24 24">
      <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"/>
      <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"/>
      <path fill="#FBBC05" d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"/>
      <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"/>
    </svg>
  );
}

function AtlassianLogo({ className }) {
  return (
    <svg className={className} viewBox="0 0 32 32" fill="none">
      <path fill="#0052CC" d="M11.53 15.4a.78.78 0 00-1.22.11L5.06 24.1a.78.78 0 00.68 1.16h8.88a.78.78 0 00.7-.44c1.77-3.64.76-7.26-1.79-9.42z"/>
      <path fill="url(#atl_grad)" d="M15.76 1.33a15.2 15.2 0 00-.44 17.05l4.2 8.4a.78.78 0 00.7.44h8.88a.78.78 0 00.68-1.16L16.98 1.44a.78.78 0 00-1.22-.11z"/>
      <defs>
        <linearGradient id="atl_grad" x1="22.1" y1="5.8" x2="15.6" y2="18.4" gradientUnits="userSpaceOnUse">
          <stop offset="0%" stopColor="#0052CC"/>
          <stop offset="100%" stopColor="#2684FF"/>
        </linearGradient>
      </defs>
    </svg>
  );
}

function ZoomLogo({ className }) {
  return (
    <svg className={className} viewBox="0 0 32 32" fill="none">
      <rect width="32" height="32" rx="6" fill="#2D8CFF"/>
      <path d="M6 11.5C6 10.67 6.67 10 7.5 10H18.5C19.33 10 20 10.67 20 11.5V20.5C20 21.33 19.33 22 18.5 22H7.5C6.67 22 6 21.33 6 20.5V11.5Z" fill="white"/>
      <path d="M21 14L26 11V21L21 18V14Z" fill="white"/>
    </svg>
  );
}

function OAuthLogo({ type, className }) {
  if (type === "github") return <GitHubLogo className={className} />;
  if (type === "gmail")  return <GoogleLogo className={className} />;
  if (type === "jira")   return <AtlassianLogo className={className} />;
  if (type === "zoom")   return <ZoomLogo className={className} />;
  return null;
}

// ── ZoomMeetingsSection — shown below Zoom card when connected ────────
function ZoomMeetingsSection({ connected }) {
  const [meetings, setMeetings] = useState([]);
  const [loading,  setLoading]  = useState(false);
  const [syncing,  setSyncing]  = useState(false);
  const toast = useToast();

  const load = async () => {
    setLoading(true);
    try {
      const res  = await authFetch("http://localhost:5000/api/zoom/meetings");
      const data = await res.json();
      setMeetings(Array.isArray(data) ? data : []);
    } catch {
      // silent — meetings are optional
    } finally {
      setLoading(false);
    }
  };

  const sync = async () => {
    setSyncing(true);
    try {
      await authFetch("http://localhost:5000/api/zoom/sync", { method: "POST" });
      toast.success("Zoom calendar synced!");
      await load();
    } catch {
      toast.error("Sync failed.");
    } finally {
      setSyncing(false);
    }
  };

  useEffect(() => { if (connected) load(); }, [connected]);

  if (!connected) return null;

  const statusIcon = (status) => {
    if (status === "cancelled")   return <XCircle className="w-3.5 h-3.5 text-red shrink-0" />;
    if (status === "bot_joining") return <Loader2 className="w-3.5 h-3.5 text-accent animate-spin shrink-0" />;
    if (status === "ended")       return <CheckCircle2 className="w-3.5 h-3.5 text-muted shrink-0" />;
    return <CheckCircle2 className="w-3.5 h-3.5 text-green shrink-0" />;
  };

  const statusLabel = (status) => {
    if (status === "cancelled")   return "Cancelled";
    if (status === "bot_joining") return "Bot joining...";
    if (status === "ended")       return "Ended";
    return "Bot will auto-join";
  };

  return (
    <div className="bg-surface border border-border rounded-2xl overflow-hidden">
      <div className="flex items-center justify-between px-6 py-4 border-b border-border">
        <div className="flex items-center gap-2">
          <Calendar className="w-4 h-4 text-[#2D8CFF]" />
          <span className="text-sm font-semibold text-txt">Synced Meetings</span>
          {meetings.length > 0 && (
            <span className="text-xs text-muted bg-surface2 border border-border rounded-full px-2 py-0.5">
              {meetings.length}
            </span>
          )}
        </div>
        <button
          onClick={sync}
          disabled={syncing}
          className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-muted hover:text-txt bg-surface2 border border-border rounded-lg hover:border-accent/30 transition-all"
        >
          {syncing
            ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
            : <RefreshCw className="w-3.5 h-3.5" />
          }
          Sync now
        </button>
      </div>

      {loading ? (
        <div className="px-6 py-8 flex items-center justify-center">
          <Loader2 className="w-5 h-5 text-muted animate-spin" />
        </div>
      ) : meetings.length === 0 ? (
        <div className="px-6 py-8 text-center">
          <Video className="w-8 h-8 text-muted mx-auto mb-2 opacity-40" />
          <p className="text-sm text-muted">No scheduled meetings found.</p>
          <p className="text-xs text-muted mt-1">Click "Sync now" to fetch your Zoom calendar.</p>
        </div>
      ) : (
        <div className="divide-y divide-border">
          {meetings.map((m) => (
            <div key={m.id} className="flex items-center gap-4 px-6 py-3.5">
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                  <p className="text-sm font-medium text-txt truncate">{m.topic || "Untitled Meeting"}</p>
                  {m.isRecurring && (
                    <RepeatIcon className="w-3 h-3 text-muted shrink-0" title="Recurring" />
                  )}
                </div>
                <p className="text-xs text-muted mt-0.5">
                  {new Date(m.startTime).toLocaleString()} · {m.duration} min
                </p>
              </div>
              <div className="flex items-center gap-1.5 shrink-0">
                {statusIcon(m.status)}
                <span className="text-xs text-muted">{statusLabel(m.status)}</span>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ── IntegrationCard ───────────────────────────────────────────
function IntegrationCard({ def, integration, onConnect, onDisconnect, onTest }) {
  const [expanded,    setExpanded]    = useState(false);
  const [manualOpen,  setManualOpen]  = useState(false);
  const [form,        setForm]        = useState({});
  const [testing,     setTesting]     = useState(false);
  const [oauthLoading, setOAuthLoading] = useState(false);
  const toast = useToast();

  const connected = integration?.isConnected;
  const set = (k) => (e) => setForm(f => ({ ...f, [k]: e.target.value }));

  const handleManualConnect = () => {
    const missing = def.fields.filter(f => !form[f.key]);
    if (missing.length) return;
    onConnect(def.type, form);
    setExpanded(false);
    setManualOpen(false);
  };

  const handleTest = async () => {
    setTesting(true);
    await onTest(def.type);
    setTesting(false);
  };

  const handleOAuth = () => {
    // The /auth endpoint does a 302 → provider, so we navigate directly.
    // JWT is passed via ?token= (read by the backend's JwtBearerEvents).
    const token = getToken();
    window.location.href =
      `http://localhost:5000/api/oauth/${def.type}/auth` +
      (token ? `?token=${encodeURIComponent(token)}` : "");
  };

  const connectedInfo = connected && def.oauth
    ? def.connectedLabel(integration)
    : null;

  return (
    <div className={cn(
      "bg-surface border rounded-2xl overflow-hidden transition-all",
      connected ? "border-green/30" : "border-border"
    )}>
      {/* Header */}
      <div className="flex items-center gap-4 px-6 py-5">
        <div className={cn("w-11 h-11 rounded-xl border flex items-center justify-center shrink-0", def.bg)}>
          <Plug className={cn("w-5 h-5", def.color)} />
        </div>

        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <p className="font-semibold text-txt">{def.name}</p>
            {connected
              ? <span className="flex items-center gap-1 text-[11px] font-semibold text-green"><CheckCircle2 className="w-3.5 h-3.5" /> Connected</span>
              : <span className="flex items-center gap-1 text-[11px] font-semibold text-muted"><XCircle className="w-3.5 h-3.5" /> Not Connected</span>
            }
          </div>
          <p className="text-xs text-muted mt-0.5">{def.desc}</p>
        </div>

        <div className="flex items-center gap-2">
          {connected && (
            <>
              <button
                onClick={handleTest}
                disabled={testing}
                className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-muted hover:text-txt bg-surface2 border border-border rounded-lg hover:border-accent/30 transition-all"
              >
                {testing ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <TestTube2 className="w-3.5 h-3.5" />}
                Test
              </button>
              <button
                onClick={() => onDisconnect(def.type)}
                className="p-1.5 text-muted hover:text-red transition-colors"
                title="Disconnect"
              >
                <Trash2 className="w-4 h-4" />
              </button>
            </>
          )}
          <button
            onClick={() => setExpanded(e => !e)}
            className="p-1.5 text-muted hover:text-txt transition-colors"
          >
            {expanded ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
          </button>
        </div>
      </div>

      {/* Expand panel */}
      {expanded && (
        <div className="border-t border-border px-6 py-5 bg-surface2 space-y-4">

          {def.oauth ? (
            <>
              {/* ── OAuth section ──────────────────────────── */}
              {connected ? (
                <div className="flex items-center justify-between bg-green/5 border border-green/20 rounded-xl px-4 py-3">
                  <div className="flex items-center gap-2">
                    <CheckCircle2 className="w-4 h-4 text-green shrink-0" />
                    <span className="text-sm text-txt">
                      <span className="text-muted">{def.connectedPrefix} </span>
                      <span className="font-semibold">{connectedInfo}</span>
                    </span>
                  </div>
                  <button
                    onClick={handleOAuth}
                    disabled={oauthLoading}
                    className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-muted hover:text-txt bg-surface border border-border rounded-lg hover:border-accent/30 transition-all"
                  >
                    {oauthLoading
                      ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
                      : <RefreshCw className="w-3.5 h-3.5" />
                    }
                    Reconnect
                  </button>
                </div>
              ) : (
                <button
                  onClick={handleOAuth}
                  disabled={oauthLoading}
                  className={cn(
                    "flex items-center justify-center gap-2.5 w-full px-4 py-2.5 rounded-xl text-sm font-semibold transition-colors",
                    def.oauthBtnClass
                  )}
                >
                  {oauthLoading
                    ? <Loader2 className="w-4 h-4 animate-spin" />
                    : <OAuthLogo type={def.type} className="w-4 h-4" />
                  }
                  {def.oauthLabel}
                </button>
              )}

              {/* ── Manual setup toggle ─────────────────────── */}
              <div>
                <button
                  onClick={() => setManualOpen(m => !m)}
                  className="flex items-center gap-1.5 text-xs text-muted hover:text-txt transition-colors"
                >
                  <Settings className="w-3.5 h-3.5" />
                  Manual setup
                  {manualOpen
                    ? <ChevronUp className="w-3.5 h-3.5" />
                    : <ChevronDown className="w-3.5 h-3.5" />
                  }
                </button>

                {manualOpen && (
                  <div className="mt-3 space-y-4 pl-1">
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                      {def.fields.map(field => (
                        <div key={field.key}>
                          <label className="block text-xs font-medium text-muted mb-1">{field.label}</label>
                          <input
                            type={field.key.toLowerCase().includes("password") || field.key.toLowerCase().includes("token") || field.key.toLowerCase().includes("secret") ? "password" : "text"}
                            value={form[field.key] ?? ""}
                            onChange={set(field.key)}
                            placeholder={field.placeholder}
                            className="w-full bg-surface border border-border rounded-xl px-3 py-2 text-sm text-txt placeholder:text-muted outline-none focus:border-accent transition-colors"
                          />
                        </div>
                      ))}
                    </div>
                    <div className="flex gap-3">
                      <button
                        onClick={handleManualConnect}
                        className="flex items-center gap-2 px-4 py-2 bg-accent text-white rounded-xl text-sm font-semibold hover:bg-accent/90 transition-colors"
                      >
                        <CheckCircle2 className="w-4 h-4" />
                        {connected ? "Update" : "Connect"}
                      </button>
                      <button
                        onClick={() => { setManualOpen(false); setExpanded(false); }}
                        className="px-4 py-2 text-sm text-muted hover:text-txt border border-border rounded-xl transition-colors"
                      >
                        Cancel
                      </button>
                    </div>
                  </div>
                )}
              </div>
            </>
          ) : (
            <>
              {/* ── Manual-only card (Slack) ────────────────── */}
              <p className="text-xs font-semibold text-txt uppercase tracking-wider">
                {connected ? "Update Configuration" : "Connect " + def.name}
              </p>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                {def.fields.map(field => (
                  <div key={field.key}>
                    <label className="block text-xs font-medium text-muted mb-1">{field.label}</label>
                    <input
                      type={field.key.toLowerCase().includes("password") || field.key.toLowerCase().includes("token") || field.key.toLowerCase().includes("secret") ? "password" : "text"}
                      value={form[field.key] ?? ""}
                      onChange={set(field.key)}
                      placeholder={field.placeholder}
                      className="w-full bg-surface border border-border rounded-xl px-3 py-2 text-sm text-txt placeholder:text-muted outline-none focus:border-accent transition-colors"
                    />
                  </div>
                ))}
              </div>
              <div className="flex gap-3">
                <button
                  onClick={handleManualConnect}
                  className="flex items-center gap-2 px-4 py-2 bg-accent text-white rounded-xl text-sm font-semibold hover:bg-accent/90 transition-colors"
                >
                  <CheckCircle2 className="w-4 h-4" />
                  {connected ? "Update" : "Connect"}
                </button>
                <button
                  onClick={() => setExpanded(false)}
                  className="px-4 py-2 text-sm text-muted hover:text-txt border border-border rounded-xl transition-colors"
                >
                  Cancel
                </button>
              </div>
            </>
          )}
        </div>
      )}
    </div>
  );
}

// ── Page ──────────────────────────────────────────────────────
export default function Integrations() {
  const toast = useToast();
  const [integrations, setIntegrations] = useState([]);
  const [loading, setLoading]           = useState(true);

  // Handle OAuth callback URL params
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const toasts = {
      zoom:   "Zoom connected successfully!",
      github: "GitHub connected successfully!",
      jira:   "Jira connected successfully!",
      gmail:  "Gmail connected successfully!",
      slack:  "Slack connected successfully!",
    };
    let found = false;
    for (const [key, msg] of Object.entries(toasts)) {
      if (params.get(key) === "connected") {
        toast.success(msg);
        found = true;
      }
    }
    if (found) {
      // Clean the URL without triggering a navigation
      const clean = window.location.pathname;
      window.history.replaceState({}, "", clean);
    }
  }, []);

  const load = () => {
    setLoading(true);
    authFetch("http://localhost:5000/api/integrations")
      .then(r => r.json())
      .then(data => setIntegrations(Array.isArray(data) ? data : []))
      .catch(() => toast.error("Failed to load integrations."))
      .finally(() => setLoading(false));
  };

  useEffect(() => { load(); }, []);

  const getIntegration = (type) => integrations.find(i => i.type === type);

  const handleConnect = async (type, config) => {
    try {
      const res = await authFetch("http://localhost:5000/api/integrations/connect", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ type, configJson: JSON.stringify(config), isConnected: true }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error ?? "Failed to connect.");
      toast.success(`${type} connected successfully!`);
      load();
    } catch (err) {
      toast.error(err.message);
    }
  };

  const handleDisconnect = async (type) => {
    try {
      const res = await authFetch(`http://localhost:5000/api/integrations/${type}`, { method: "DELETE" });
      if (!res.ok) throw new Error("Disconnect failed.");
      toast.success(`${type} disconnected.`);
      load();
    } catch (err) {
      toast.error(err.message);
    }
  };

  const handleTest = async (type) => {
    try {
      const res = await authFetch("http://localhost:5000/api/integrations/test", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ type }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error ?? "Test failed.");
      toast.success(`${type} connection test passed!`);
    } catch (err) {
      toast.error(err.message);
    }
  };

  const connectedCount = INTEGRATION_DEFS.filter(d => getIntegration(d.type)?.isConnected).length;

  return (
    <div className="max-w-3xl mx-auto space-y-6 pb-10">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <h1 className="font-display font-bold text-txt text-2xl">Integrations</h1>
          <p className="text-muted text-sm mt-1">Connect your tools to enable full pipeline automation.</p>
        </div>
        <div className="flex items-center gap-2 bg-surface2 border border-border rounded-xl px-4 py-2">
          <Plug className="w-4 h-4 text-accent" />
          <span className="text-sm font-semibold text-txt">{connectedCount}</span>
          <span className="text-xs text-muted">/ {INTEGRATION_DEFS.length} Connected</span>
        </div>
      </div>

      {/* Info banner */}
      <div className="flex items-start gap-3 bg-accent/10 border border-accent/20 rounded-xl px-5 py-4">
        <Zap className="w-4 h-4 text-accent mt-0.5 shrink-0" />
        <p className="text-sm text-muted leading-relaxed">
          Connect all 5 integrations to enable the full 6-step automation pipeline. Each integration
          is required for different pipeline stages.
        </p>
      </div>

      {/* Cards */}
      {loading ? (
        <div className="space-y-4">
          {[1,2,3,4].map(i => (
            <div key={i} className="bg-surface border border-border rounded-2xl p-5 animate-pulse">
              <div className="flex items-center gap-4">
                <div className="w-11 h-11 rounded-xl bg-surface2" />
                <div className="flex-1 space-y-2">
                  <div className="w-24 h-3 bg-surface2 rounded" />
                  <div className="w-48 h-2 bg-surface2 rounded" />
                </div>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div className="space-y-4">
          {INTEGRATION_DEFS.map(def => (
            <div key={def.type}>
              <IntegrationCard
                def={def}
                integration={getIntegration(def.type)}
                onConnect={handleConnect}
                onDisconnect={handleDisconnect}
                onTest={handleTest}
              />
              {def.type === "zoom" && (
                <div className="mt-3">
                  <ZoomMeetingsSection connected={getIntegration("zoom")?.isConnected} />
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      {/* Pipeline requirement map */}
      <div className="bg-surface border border-border rounded-2xl p-6">
        <h2 className="font-display font-semibold text-txt text-sm mb-4">Pipeline Step Requirements</h2>
        <div className="space-y-2">
          {[
            { step: "Transcribe Audio",      needs: null,     note: "Built-in (Recall.ai + Whisper)" },
            { step: "Generate MOM",          needs: null,     note: "Built-in (Claude AI)"           },
            { step: "Create Jira Ticket",    needs: "jira",   note: "Requires Jira"                  },
            { step: "Scan Codebase",         needs: "github", note: "Requires GitHub"                },
            { step: "Generate Code Fix",     needs: null,     note: "Built-in (Claude AI)"           },
            { step: "Create Branch & PR",    needs: "github", note: "Requires GitHub"                },
          ].map(({ step, needs, note }) => {
            const ok = !needs || getIntegration(needs)?.isConnected;
            return (
              <div key={step} className="flex items-center gap-3 text-sm">
                {ok
                  ? <CheckCircle2 className="w-4 h-4 text-green shrink-0" />
                  : <AlertCircle  className="w-4 h-4 text-orange shrink-0" />
                }
                <span className="text-txt flex-1">{step}</span>
                <span className="text-xs text-muted">{note}</span>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}

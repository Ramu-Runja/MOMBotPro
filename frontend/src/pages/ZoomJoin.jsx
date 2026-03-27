import { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import {
  Bot, Phone, Circle, Settings, CheckCircle2, XCircle,
  Zap, Mic, GitPullRequest, Clock, Calendar, Save,
} from "lucide-react";
import { Card, Button, Input, Spinner, CodeBlock, cn } from "../components/ui";
import { authFetch } from "../utils/authFetch";

const API = "http://localhost:5000/api";

const STATUS_META = {
  Joining:    { label: "Joining Call",         color: "text-orange", border: "border-orange/30", bg: "bg-orange/10", Icon: Bot       },
  InCall:     { label: "In Call",              color: "text-accent", border: "border-accent/30", bg: "bg-accent/10", Icon: Phone     },
  Recording:  { label: "Recording",            color: "text-red",    border: "border-red/30",    bg: "bg-red/10",    Icon: Circle    },
  Processing: { label: "Processing",           color: "text-orange", border: "border-orange/30", bg: "bg-orange/10", Icon: Settings  },
  Done:       { label: "Pipeline Launched",    color: "text-green",  border: "border-green/30",  bg: "bg-green/10",  Icon: CheckCircle2 },
  Failed:     { label: "Failed",               color: "text-red",    border: "border-red/30",    bg: "bg-red/10",    Icon: XCircle   },
};

const STEPS = [
  { key: "Joining",    label: "Bot Joining",       Icon: Bot            },
  { key: "InCall",     label: "In Call",           Icon: Phone          },
  { key: "Recording",  label: "Recording",         Icon: Circle         },
  { key: "Processing", label: "Processing",        Icon: Settings       },
  { key: "Done",       label: "Pipeline Launched", Icon: CheckCircle2   },
];

const HOW_IT_WORKS = [
  { icon: <Bot className="w-5 h-5 text-accent" />,            title: "Bot Joins",              desc: "MOMBot joins your Zoom call as a silent AI participant." },
  { icon: <Mic className="w-5 h-5 text-accent" />,            title: "Records & Transcribes",  desc: "Captures audio and transcribes Tenglish (Telugu+English) speech via Recall.ai." },
  { icon: <GitPullRequest className="w-5 h-5 text-accent" />, title: "Pipeline Auto-Starts",   desc: "Once the call ends, the 6-step pipeline runs: Jira ticket, code fix, PR raised." },
];

const DAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

export default function ZoomJoin() {
  const navigate = useNavigate();
  const [clientName, setClientName] = useState("");
  const [meetingUrl, setMeetingUrl] = useState("");
  const [loading, setLoading]       = useState(false);
  const [error, setError]           = useState("");
  const [session, setSession]       = useState(null);

  // Zoom Settings state
  const [savedLink,      setSavedLink]      = useState("");
  const [isRecurring,    setIsRecurring]    = useState(false);
  const [scheduledTime,  setScheduledTime]  = useState("");
  const [scheduledDays,  setScheduledDays]  = useState([]);
  const [isActive,       setIsActive]       = useState(true);
  const [settingsSaving, setSettingsSaving] = useState(false);
  const [settingsMsg,    setSettingsMsg]    = useState("");

  const pollRef = useRef(null);

  useEffect(() => () => clearTimeout(pollRef.current), []);

  // Load saved Zoom settings on mount
  useEffect(() => {
    authFetch(`${API}/zoom-settings`)
      .then(r => r.ok ? r.json() : null)
      .then(data => {
        if (!data) return;
        setSavedLink(data.zoomLink ?? "");
        setIsRecurring(data.isRecurring ?? false);
        setScheduledTime(data.scheduledTime ?? "");
        setScheduledDays(data.scheduledDays ? data.scheduledDays.split(",").map(d => d.trim()) : []);
        setIsActive(data.isActive ?? true);
        // Pre-fill meeting URL if set
        if (data.zoomLink) setMeetingUrl(data.zoomLink);
      })
      .catch(() => {});
  }, []);

  useEffect(() => {
    if (!session) return;
    if (session.status === "Done" || session.status === "Failed") return;
    pollRef.current = setTimeout(() => poll(session.id), 3000);
  }, [session]);

  useEffect(() => {
    if (session?.status === "Done" && session?.pipelineId) {
      const t = setTimeout(() => navigate(`/pipelines/${session.pipelineId}`), 1500);
      return () => clearTimeout(t);
    }
  }, [session]);

  const poll = async (sessionId) => {
    try {
      const res  = await authFetch(`${API}/zoom/session/${sessionId}`);
      const data = await res.json();
      setSession(data);
    } catch {
      pollRef.current = setTimeout(() => poll(sessionId), 4000);
    }
  };

  const validateZoomUrl = (url) => {
    if (!url.includes("zoom.us"))
      return "Must be a Zoom URL (zoom.us)";
    if (url.includes("/wc/") || url.includes("ref_from=launch"))
      return null; // backend will clean these
    if (!url.includes("/j/"))
      return "Please use a Zoom join link (https://zoom.us/j/MEETINGID)";
    return null;
  };

  const handleJoin = async (e) => {
    e.preventDefault();
    setError("");
    if (!clientName.trim()) { setError("Client name is required."); return; }
    if (!meetingUrl.trim())  { setError("Zoom meeting URL is required."); return; }
    if (!meetingUrl.startsWith("http")) { setError("Please enter a valid URL."); return; }
    const urlErr = validateZoomUrl(meetingUrl);
    if (urlErr) { setError(urlErr); return; }

    setLoading(true);
    try {
      const res  = await authFetch(`${API}/zoom/join`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ clientName: clientName.trim(), meetingUrl: meetingUrl.trim() }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error ?? "Failed to send bot.");
      setSession(data);
    } catch (ex) {
      setError(ex.message);
    } finally {
      setLoading(false);
    }
  };

  const reset = () => { clearTimeout(pollRef.current); setSession(null); setError(""); setClientName(""); setMeetingUrl(""); };

  const toggleDay = (day) => setScheduledDays(prev =>
    prev.includes(day) ? prev.filter(d => d !== day) : [...prev, day]
  );

  const saveSettings = async () => {
    setSettingsSaving(true);
    setSettingsMsg("");
    try {
      const res = await authFetch(`${API}/zoom-settings`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          zoomLink:      savedLink,
          isRecurring,
          scheduledTime: scheduledTime || null,
          scheduledDays: scheduledDays.join(","),
          isActive,
        }),
      });
      if (!res.ok) throw new Error("Failed to save");
      setSettingsMsg("Settings saved!");
      if (savedLink && !meetingUrl) setMeetingUrl(savedLink);
    } catch {
      setSettingsMsg("Failed to save settings.");
    } finally {
      setSettingsSaving(false);
    }
  };

  const meta      = session ? (STATUS_META[session.status] ?? STATUS_META.Joining) : null;
  const stepIndex = session ? STEPS.findIndex(s => s.key === session.status) : -1;

  return (
    <div className="max-w-3xl space-y-6">

      {/* ── Join form ──────────────────────────────────────── */}
      {!session && (
        <>
          <Card className="p-6">
            <div className="flex items-center gap-3 mb-5">
              <div className="w-10 h-10 rounded-xl bg-accent/15 border border-accent/25 flex items-center justify-center">
                <Bot className="w-5 h-5 text-accent" />
              </div>
              <div>
                <h2 className="font-display font-bold text-txt text-base">Send Bot to Meeting</h2>
                <p className="text-xs text-muted">Paste the Zoom invite link and the client's name</p>
              </div>
            </div>

            <form onSubmit={handleJoin} className="space-y-4">
              <Input
                label="Client Name"
                placeholder="e.g. Ravi Kumar"
                value={clientName}
                onChange={e => setClientName(e.target.value)}
                disabled={loading}
              />
              <div>
                <Input
                  label="Zoom Meeting URL"
                  type="url"
                  placeholder="https://zoom.us/j/123456789?pwd=..."
                  value={meetingUrl}
                  onChange={e => setMeetingUrl(e.target.value)}
                  disabled={loading}
                />
                <p style={{
                  fontSize: 12,
                  color: meetingUrl.includes("web.zoom.us") ? "#FF8800" : "#888899",
                  marginTop: 6
                }}>
                  {meetingUrl.includes("web.zoom.us")
                    ? "⚠️ Regional URL detected — will be auto-converted"
                    : meetingUrl.includes("zoom.us/j/")
                    ? "✅ Valid Zoom join URL"
                    : "Use the Join URL from your Zoom invitation email"}
                </p>
              </div>

              {error && (
                <div className="flex items-center gap-2 bg-red/10 border border-red/20 rounded-xl px-4 py-3">
                  <XCircle className="w-4 h-4 text-red shrink-0" />
                  <p className="text-xs text-red">{error}</p>
                </div>
              )}

              <Button type="submit" className="w-full justify-center" disabled={loading}>
                {loading ? <><Spinner size="sm" /> Sending bot...</> : <><Bot className="w-4 h-4" /> Send Bot to Meeting</>}
              </Button>
            </form>
          </Card>

          {/* ── Zoom Settings ─────────────────────────────────── */}
          <Card className="p-6 space-y-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-accent/15 border border-accent/25 flex items-center justify-center">
                <Clock className="w-5 h-5 text-accent" />
              </div>
              <div>
                <h3 className="font-display font-bold text-txt text-base">Zoom Settings</h3>
                <p className="text-xs text-muted">Save your recurring Zoom link for auto-join</p>
              </div>
            </div>

            <Input
              label="Saved Zoom Link"
              type="url"
              placeholder="https://zoom.us/j/123456789?pwd=..."
              value={savedLink}
              onChange={e => setSavedLink(e.target.value)}
            />

            {/* Auto-join toggle */}
            <label className="flex items-center gap-3 cursor-pointer select-none">
              <div
                onClick={() => setIsRecurring(v => !v)}
                className={cn(
                  "w-10 h-5 rounded-full border-2 transition-colors relative cursor-pointer",
                  isRecurring ? "bg-accent border-accent" : "bg-surface2 border-border"
                )}
              >
                <div className={cn(
                  "w-3.5 h-3.5 rounded-full bg-white absolute top-0.5 transition-transform",
                  isRecurring ? "translate-x-[22px]" : "translate-x-0.5"
                )} />
              </div>
              <span className="text-sm text-txt">Auto-join on schedule</span>
            </label>

            {isRecurring && (
              <div className="space-y-3 pl-1">
                <div>
                  <label className="block text-xs text-muted mb-1.5">Time</label>
                  <input
                    type="time"
                    value={scheduledTime}
                    onChange={e => setScheduledTime(e.target.value)}
                    className="bg-surface2 border border-border rounded-xl px-3 py-2 text-sm text-txt focus:outline-none focus:border-accent"
                  />
                </div>

                <div>
                  <label className="block text-xs text-muted mb-2">
                    <Calendar className="w-3.5 h-3.5 inline mr-1" />Days
                  </label>
                  <div className="flex gap-2 flex-wrap">
                    {DAYS.map(day => (
                      <button
                        key={day}
                        type="button"
                        onClick={() => toggleDay(day)}
                        className={cn(
                          "px-3 py-1 rounded-lg border text-xs font-medium transition-colors",
                          scheduledDays.includes(day)
                            ? "bg-accent/20 border-accent/50 text-accent"
                            : "bg-surface2 border-border text-muted hover:border-accent/30"
                        )}
                      >
                        {day}
                      </button>
                    ))}
                  </div>
                </div>

                <label className="flex items-center gap-2 cursor-pointer select-none">
                  <input
                    type="checkbox"
                    checked={isActive}
                    onChange={e => setIsActive(e.target.checked)}
                    className="accent-accent"
                  />
                  <span className="text-xs text-muted">Schedule is active</span>
                </label>
              </div>
            )}

            <div className="flex items-center gap-3">
              <Button size="sm" onClick={saveSettings} disabled={settingsSaving}>
                {settingsSaving ? <Spinner size="sm" /> : <Save className="w-4 h-4" />}
                Save Settings
              </Button>
              {settingsMsg && (
                <span className={cn("text-xs", settingsMsg.startsWith("Failed") ? "text-red" : "text-green")}>
                  {settingsMsg}
                </span>
              )}
            </div>
          </Card>

          {/* How it works */}
          <Card className="p-6">
            <h3 className="font-display font-bold text-txt text-sm mb-4">How it works</h3>
            <div className="space-y-4">
              {HOW_IT_WORKS.map((step, i) => (
                <div key={i} className="flex gap-4 items-start">
                  <div className="w-8 h-8 rounded-xl bg-accent/10 border border-accent/20 flex items-center justify-center shrink-0">
                    {step.icon}
                  </div>
                  <div>
                    <p className="text-sm font-semibold text-txt">{step.title}</p>
                    <p className="text-xs text-muted mt-0.5">{step.desc}</p>
                  </div>
                </div>
              ))}
            </div>
          </Card>
        </>
      )}

      {/* ── Live status ────────────────────────────────────── */}
      {session && (
        <div className="space-y-4">
          <Card className="p-6 space-y-6">

            {/* Header */}
            <div className="flex items-center justify-between">
              <div>
                <h2 className="font-display font-bold text-txt text-xl">{session.clientName}</h2>
                <p className="text-xs text-muted mt-0.5 break-all">{session.meetingUrl}</p>
              </div>
              {meta && (
                <div className={cn("flex items-center gap-2 px-3 py-2 rounded-xl border", meta.bg, meta.border)}>
                  <meta.Icon className={cn("w-4 h-4", meta.color)} />
                  <span className={cn("text-xs font-bold", meta.color)}>{meta.label}</span>
                </div>
              )}
            </div>

            {/* Step progress */}
            <div className="flex items-center">
              {STEPS.map((step, i) => {
                const done   = i < stepIndex;
                const active = i === stepIndex;
                return (
                  <div key={step.key} className="flex-1 flex flex-col items-center gap-2">
                    <div className="relative flex items-center w-full">
                      {i > 0 && (
                        <div className={cn("flex-1 h-0.5", done ? "bg-green/40" : "bg-border")} />
                      )}
                      <div className={cn(
                        "w-8 h-8 rounded-full border-2 flex items-center justify-center shrink-0 z-10",
                        done    ? "border-green bg-green/15"   :
                        active  ? "border-accent bg-accent/15 ring-2 ring-accent/20" :
                        "border-border bg-surface2"
                      )}>
                        {active
                          ? <Spinner size="sm" className="border-accent/30 border-t-accent" />
                          : done
                          ? <CheckCircle2 className="w-4 h-4 text-green" />
                          : <step.Icon className="w-3.5 h-3.5 text-muted" />
                        }
                      </div>
                      {i < STEPS.length - 1 && (
                        <div className={cn("flex-1 h-0.5", done ? "bg-green/40" : "bg-border")} />
                      )}
                    </div>
                    <span className={cn(
                      "text-[10px] font-medium text-center leading-tight",
                      done ? "text-green" : active ? "text-accent" : "text-muted"
                    )}>{step.label}</span>
                  </div>
                );
              })}
            </div>

            {/* Status message */}
            <div className="flex items-center gap-3 bg-surface2 border border-border rounded-xl px-4 py-3">
              {session.status !== "Done" && session.status !== "Failed" && (
                <Spinner size="sm" className="border-muted/30 border-t-muted shrink-0" />
              )}
              <p className="text-sm text-txt">{session.statusMessage}</p>
            </div>

            {/* Done */}
            {session.status === "Done" && session.pipelineId && (
              <div className="flex items-center gap-3 bg-green/10 border border-green/25 rounded-xl px-4 py-3">
                <Zap className="w-4 h-4 text-green shrink-0" />
                <p className="text-sm text-green font-semibold flex-1">Pipeline is running! Navigating to results...</p>
                <Spinner size="sm" className="border-green/30 border-t-green" />
              </div>
            )}

            {/* Failed */}
            {session.status === "Failed" && (
              <div className="flex items-center justify-between gap-4 bg-red/10 border border-red/25 rounded-xl px-4 py-3">
                <p className="text-sm text-red">The bot encountered an error. Check the Zoom URL and try again.</p>
                <Button variant="secondary" size="sm" onClick={reset}>Try Again</Button>
              </div>
            )}
          </Card>

          {/* Transcript preview */}
          {session.transcript && (
            <Card className="p-5">
              <h3 className="font-display font-bold text-txt text-sm mb-3">📄 Captured Transcript</h3>
              <CodeBlock>{session.transcript}</CodeBlock>
            </Card>
          )}

          {/* Cancel */}
          {session.status !== "Done" && session.status !== "Failed" && (
            <Button variant="ghost" size="sm" onClick={reset}>
              <XCircle className="w-4 h-4" /> Cancel &amp; Start New
            </Button>
          )}
        </div>
      )}
    </div>
  );
}

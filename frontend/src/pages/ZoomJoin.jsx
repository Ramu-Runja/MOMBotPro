import { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import {
  Bot, Phone, Circle, Settings, CheckCircle2, XCircle,
  Zap, Mic, GitPullRequest, Clock, Calendar, Save,
  Video, RefreshCw, ExternalLink, AlertCircle,
} from "lucide-react";
import { Card, Button, Input, Spinner, CodeBlock, cn } from "../components/ui";
import { authFetch } from "../utils/authFetch";


const API = "http://localhost:5000/api";

// ─── unchanged constants ──────────────────────────────────────────────────────

const STATUS_META = {
  Joining:    { label: "Joining Call",         color: "text-orange", border: "border-orange/30", bg: "bg-orange/10", Icon: Bot          },
  InCall:     { label: "In Call",              color: "text-accent", border: "border-accent/30", bg: "bg-accent/10", Icon: Phone        },
  Recording:  { label: "Recording",            color: "text-red",    border: "border-red/30",    bg: "bg-red/10",    Icon: Circle       },
  Processing: { label: "Processing",           color: "text-orange", border: "border-orange/30", bg: "bg-orange/10", Icon: Settings     },
  Done:       { label: "Pipeline Launched",    color: "text-green",  border: "border-green/30",  bg: "bg-green/10",  Icon: CheckCircle2 },
  Failed:     { label: "Failed",               color: "text-red",    border: "border-red/30",    bg: "bg-red/10",    Icon: XCircle      },
};

const STEPS = [
  { key: "Joining",    label: "Bot Joining",       Icon: Bot          },
  { key: "InCall",     label: "In Call",           Icon: Phone        },
  { key: "Recording",  label: "Recording",         Icon: Circle       },
  { key: "Processing", label: "Processing",        Icon: Settings     },
  { key: "Done",       label: "Pipeline Launched", Icon: CheckCircle2 },
];

const HOW_IT_WORKS = [
  { icon: <Bot className="w-5 h-5 text-accent" />,            title: "Bot Joins",              desc: "MOMBot joins your Zoom call as a silent AI participant." },
  { icon: <Mic className="w-5 h-5 text-accent" />,            title: "Records & Transcribes",  desc: "Captures audio and transcribes Tenglish (Telugu+English) speech via Recall.ai." },
  { icon: <GitPullRequest className="w-5 h-5 text-accent" />, title: "Pipeline Auto-Starts",   desc: "Once the call ends, the 6-step pipeline runs: Jira ticket, code fix, PR raised." },
];

const DAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

// ─── tab config ───────────────────────────────────────────────────────────────

const TABS = [
  { key: "join",     label: "Join",     Icon: Bot      },
  { key: "meetings", label: "Meetings", Icon: Video    },
  { key: "settings", label: "Settings", Icon: Settings },
];

// ─── component ────────────────────────────────────────────────────────────────

export default function ZoomJoin() {
  const navigate = useNavigate();

  // tab state
  const [activeTab, setActiveTab] = useState("join");

  // ── Join tab state (all original) ─────────────────────────────────────────
  const [clientName, setClientName] = useState("");
  const [meetingUrl, setMeetingUrl] = useState("");
  const [loading,    setLoading]    = useState(false);
  const [error,      setError]      = useState("");
  const [session,    setSession]    = useState(null);

  // ── Settings tab state (all original) ────────────────────────────────────
  const [savedLink,      setSavedLink]      = useState("");
  const [isRecurring,    setIsRecurring]    = useState(false);
  const [scheduledTime,  setScheduledTime]  = useState("");
  const [scheduledDays,  setScheduledDays]  = useState([]);
  const [isActive,       setIsActive]       = useState(true);
  const [settingsSaving, setSettingsSaving] = useState(false);
  const [settingsMsg,    setSettingsMsg]    = useState("");

  // ── Meetings tab state (new) ──────────────────────────────────────────────
  const [meetings,         setMeetings]         = useState([]);
  const [meetingsLoading,  setMeetingsLoading]  = useState(false);
  const [meetingsError,    setMeetingsError]    = useState("");

  const pollRef = useRef(null);

  // ── original effects (unchanged) ─────────────────────────────────────────

  useEffect(() => () => clearTimeout(pollRef.current), []);

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

  // ── meetings fetch (new) ──────────────────────────────────────────────────

  useEffect(() => {
    if (activeTab !== "meetings") return;
    fetchMeetings();
  }, [activeTab]);

  const fetchMeetings = async () => {
    setMeetingsLoading(true);
    setMeetingsError("");
    try {
      const res  = await authFetch(`${API}/zoom/meetings`);
      const data = await res.json();
      if (!res.ok) throw new Error(data.error ?? "Failed to load meetings.");
      setMeetings(data.meetings ?? []);
    } catch (ex) {
      setMeetingsError(ex.message);
    } finally {
      setMeetingsLoading(false);
    }
  };

  // ── original handlers (unchanged) ─────────────────────────────────────────

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
      return null;
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

  const reset = () => {
    clearTimeout(pollRef.current);
    setSession(null);
    setError("");
    setClientName("");
    setMeetingUrl("");
  };

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

  // ── helpers ───────────────────────────────────────────────────────────────

  const meta      = session ? (STATUS_META[session.status] ?? STATUS_META.Joining) : null;
  const stepIndex = session ? STEPS.findIndex(s => s.key === session.status) : -1;
  const tabIndex  = TABS.findIndex(t => t.key === activeTab);

  // pre-fill join form from a meeting card and switch to join tab
  const joinFromMeeting = (url) => {
    setMeetingUrl(url);
    setActiveTab("join");
  };

  // ── render ────────────────────────────────────────────────────────────────

  return (
    <div className="max-w-3xl space-y-6">

      {/* ── Tab bar ──────────────────────────────────────────────────────── */}
      <div className="relative flex bg-surface2 border border-border rounded-xl p-1">
        {/* Sliding indicator */}
        <div
          className="absolute top-1 bottom-1 left-1 rounded-lg bg-black dark:bg-accent shadow-sm pointer-events-none"
          style={{
            width: `calc((100% - 8px) / ${TABS.length})`,
            transform: `translateX(calc(${tabIndex} * 100%))`,
            transition: "transform 300ms cubic-bezier(0.4, 0, 0.2, 1)",
          }}
        />
        {TABS.map(({ key, label, Icon }) => (
          <button
            key={key}
            onClick={() => setActiveTab(key)}
            className={cn(
              "relative z-10 flex-1 flex items-center justify-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-colors duration-200",
              activeTab === key ? "text-white dark:text-white" : "text-muted hover:text-txt"
            )}
          >
            <Icon className="w-4 h-4" />
            {label}
          </button>
        ))}
      </div>
<div className="relative">
  <div key={activeTab}
  className="transition-all duration-300 ease-in-out animate-fade">
    {/* ════════════════════════════════════════════════════════════════════
          JOIN TAB
      ════════════════════════════════════════════════════════════════════ */}
      {activeTab === "join" && (
        <>
          {/* ── Join form ──────────────────────────────────────────────── */}
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
                      marginTop: 6,
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

              {/* ── How it works ─────────────────────────────────────────── */}
              <Card className="p-6">
                <p className="text-xs font-semibold text-muted uppercase tracking-wider mb-4">How it works</p>
                <div className="space-y-4">
                  {HOW_IT_WORKS.map(({ icon, title, desc }) => (
                    <div key={title} className="flex items-start gap-3">
                      <div className="w-8 h-8 rounded-lg bg-accent/10 border border-accent/20 flex items-center justify-center shrink-0 mt-0.5">
                        {icon}
                      </div>
                      <div>
                        <p className="text-sm font-semibold text-txt">{title}</p>
                        <p className="text-xs text-muted mt-0.5">{desc}</p>
                      </div>
                    </div>
                  ))}
                </div>
              </Card>
            </>
          )}

          {/* ── Live status (original, untouched) ──────────────────────── */}
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
        </>
      )}

      {/* ════════════════════════════════════════════════════════════════════
          MEETINGS TAB
      ════════════════════════════════════════════════════════════════════ */}
      {activeTab === "meetings" && (

        <>
          {/* Header row */}
          <div className="flex items-center justify-between">
            <div>
              <h2 className="font-display font-bold text-txt text-base">Your Meetings</h2>
              <p className="text-xs text-muted mt-0.5">Live, upcoming, and past Zoom meetings from your account</p>
            </div>
            <Button variant="secondary" size="sm" onClick={fetchMeetings} disabled={meetingsLoading}>
              <RefreshCw className={cn("w-4 h-4", meetingsLoading && "animate-spin")} />
              Refresh
            </Button>
          </div>

          {/* Loading */}
          {meetingsLoading && (
            <Card className="p-8 flex flex-col items-center gap-3">
              <Spinner size="md" />
              <p className="text-sm text-muted">Fetching meetings...</p>
            </Card>
          )}

          {/* Error */}
          {!meetingsLoading && meetingsError && (
            <Card className="p-5 flex items-center gap-3 bg-red/5 border-red/20">
              <AlertCircle className="w-5 h-5 text-red shrink-0" />
              <div className="flex-1">
                <p className="text-sm text-red font-semibold">Could not load meetings</p>
                <p className="text-xs text-muted mt-0.5">{meetingsError}</p>
              </div>
              <Button variant="secondary" size="sm" onClick={fetchMeetings}>Retry</Button>
            </Card>
          )}

          {/* Meetings list */}
          {!meetingsLoading && !meetingsError && (
            <>
              {/* Live & Upcoming */}
              {(() => {
                const active = meetings.filter(m => m.status === "live" || m.status === "upcoming");
                return active.length > 0 ? (
                  <Card className="p-6">
                    <p className="text-xs font-semibold text-muted uppercase tracking-wider mb-4">Live &amp; Upcoming</p>
                    <div className="divide-y divide-border">
                      {active.map(meeting => (
                        <MeetingRow
                          key={meeting.id}
                          meeting={meeting}
                          onJoin={() => joinFromMeeting(meeting.joinUrl)}
                        />
                      ))}
                    </div>
                  </Card>
                ) : null;
              })()}

              {/* Recent */}
              {(() => {
                const past = meetings.filter(m => m.status === "ended");
                return past.length > 0 ? (
                  <Card className="p-6">
                    <p className="text-xs font-semibold text-muted uppercase tracking-wider mb-4">Recent</p>
                    <div className="divide-y divide-border">
                      {past.map(meeting => (
                        <MeetingRow
                          key={meeting.id}
                          meeting={meeting}
                          onNavigate={() => navigate(`/pipelines/${meeting.pipelineId}`)}
                        />
                      ))}
                    </div>
                  </Card>
                ) : null;
              })()}

              {/* Empty */}
              {meetings.length === 0 && (
                <Card className="p-10 flex flex-col items-center gap-3 text-center">
                  <div className="w-12 h-12 rounded-xl bg-accent/10 border border-accent/20 flex items-center justify-center">
                    <Video className="w-6 h-6 text-accent" />
                  </div>
                  <div>
                    <p className="text-sm font-semibold text-txt">No meetings found</p>
                    <p className="text-xs text-muted mt-1">Your upcoming and past Zoom meetings will appear here.</p>
                  </div>
                </Card>
              )}
            </>
          )}
        </>
      )}

      {/* ════════════════════════════════════════════════════════════════════
          SETTINGS TAB  (original ZoomSettings card, untouched)
      ════════════════════════════════════════════════════════════════════ */}
      {activeTab === "settings" && (
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
      )}
  </div>
</div>
    </div>
  );
}

// ─── MeetingRow sub-component ────────────────────────────────────────────────

const STATUS_BADGE = {
  live:     { label: "Live",     cls: "bg-red/10 text-red border border-red/20"         },
  upcoming: { label: "Upcoming", cls: "bg-accent/10 text-accent border border-accent/20" },
  ended:    { label: "Ended",    cls: "bg-surface2 text-muted border border-border"      },
};

function MeetingRow({ meeting, onJoin, onNavigate }) {
  const badge = STATUS_BADGE[meeting.status] ?? STATUS_BADGE.ended;

  return (
    <div className="flex items-center gap-3 py-3 first:pt-0 last:pb-0">
      {/* Icon */}
      <div className="w-8 h-8 rounded-lg bg-surface2 border border-border flex items-center justify-center shrink-0">
        <Video className="w-3.5 h-3.5 text-muted" />
      </div>

      {/* Info */}
      <div className="flex-1 min-w-0">
        <p className="text-sm font-semibold text-txt truncate">{meeting.topic ?? "Zoom Meeting"}</p>
        <p className="text-xs text-muted mt-0.5">
          {meeting.startTime
            ? new Date(meeting.startTime).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" })
            : "—"}
          {meeting.duration ? ` · ${meeting.duration} min` : ""}
        </p>
      </div>

      {/* Badge */}
      <span className={cn("text-[11px] font-semibold px-2.5 py-1 rounded-lg shrink-0", badge.cls)}>
        {badge.label}
      </span>

      {/* Action */}
      {meeting.status === "live" || meeting.status === "upcoming" ? (
        <Button size="sm" variant="secondary" onClick={onJoin}>
          <Bot className="w-3.5 h-3.5" /> Join
        </Button>
      ) : meeting.pipelineId ? (
        <Button size="sm" variant="ghost" onClick={onNavigate}>
          <ExternalLink className="w-3.5 h-3.5" /> Pipeline
        </Button>
      ) : null}
    </div>
  );
}
import { useState, useEffect, useRef } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  ArrowLeft, Mic, Bug, Ticket, Search, Wrench, GitPullRequest,
  CheckCircle2, XCircle, Clock, FileCode, MapPin, ExternalLink,
  Music, Download, RefreshCw, AlertTriangle, VideoOff, Square, RotateCcw,
} from "lucide-react";
import { Card, Badge, Tabs, Spinner, Button, CodeBlock, cn } from "../components/ui";
import { authFetch } from "../utils/authFetch";

const API = "http://localhost:5000/api";

const PIPELINE_STATUS = { 0: "Pending", 1: "Running", 2: "Done", 3: "Failed" };
const STEP_STATUS     = { 0: "Waiting", 1: "Running", 2: "Done", 3: "Failed" };

function norm(v, map) { return typeof v === "string" ? v : (map[v] ?? String(v ?? "")); }
function normalizePipeline(p) {
  return {
    ...p,
    status: norm(p.status, PIPELINE_STATUS),
    steps: (p.steps ?? []).map(s => ({ ...s, status: norm(s.status, STEP_STATUS) })),
  };
}

const STEP_META = [
  { name: "Transcribe Audio",         icon: Mic,            short: "Transcribe"  },
  { name: "Extract Bug from MOM",     icon: Bug,            short: "Extract Bug" },
  { name: "Create Jira Ticket",       icon: Ticket,         short: "Jira"        },
  { name: "Scan Codebase",            icon: Search,         short: "Scan"        },
  { name: "Generate Fix",             icon: Wrench,         short: "Fix"         },
  { name: "Create Branch & Raise PR", icon: GitPullRequest, short: "Raise PR"    },
];

const TABS = [
  { key: "steps",     label: "📊 Steps"      },
  { key: "mom",       label: "📝 MOM"        },
  { key: "recording", label: "🎙️ Audio & Transcript" },
  { key: "jira",      label: "📋 Jira"       },
  { key: "bug",       label: "🔍 Bug Fix"    },
  { key: "pr",        label: "🚀 PR"         },
];

const SPEAKER_COLORS = ["#534AB7", "#1D9E75", "#888899", "#E8934A", "#E85D8A"];

function getSpeakerColor(speaker, uniqueSpeakers) {
  const idx = uniqueSpeakers.indexOf(speaker);
  return SPEAKER_COLORS[Math.min(idx, SPEAKER_COLORS.length - 1)];
}

function formatTime(secs) {
  const m = Math.floor(secs / 60).toString().padStart(2, "0");
  const s = Math.floor(secs % 60).toString().padStart(2, "0");
  return `${m}:${s}`;
}

function StepIcon({ status, index }) {
  if (status === "Running") return <Spinner size="sm" />;
  if (status === "Done")    return <CheckCircle2 className="w-4 h-4 text-green" />;
  if (status === "Failed")  return <XCircle      className="w-4 h-4 text-red"   />;
  return <span className="text-xs font-bold text-muted">{index + 1}</span>;
}

// CSS-only animated waveform for audio mode
const WaveBar = ({ delay, height }) => (
  <div style={{
    width: 4,
    height,
    borderRadius: 2,
    background: "#534AB7",
    animation: `momWave 0.9s ease-in-out ${delay}s infinite alternate`,
  }} />
);

export default function PipelineDetail() {
  const { id }                          = useParams();
  const navigate                        = useNavigate();
  const [pipeline, setPipeline]         = useState(null);
  const [activeTab, setActiveTab]       = useState("steps");
  const [activeLineIndex, setActiveLineIndex] = useState(-1);
  const [refreshing, setRefreshing]     = useState(false);
  const [refreshError, setRefreshError] = useState("");
  const [stopping, setStopping]         = useState(false);
  const [rerunning, setRerunning]       = useState(false);

  const audioRef      = useRef(null);
  const activeLineRef = useRef(null);

  useEffect(() => {
    const poll = async () => {
      try {
        const res  = await authFetch(`${API}/pipeline/${id}`);
        const data = await res.json();
        const normalized = normalizePipeline(data);
        setPipeline(normalized);
        if (normalized.status === "Running" || normalized.status === "Pending") {
          setTimeout(poll, 1500);
        }
      } catch { setTimeout(poll, 3000); }
    };
    poll();
  }, [id]);

  // Auto-scroll active transcript line into view
  useEffect(() => {
    if (activeLineRef.current) {
      activeLineRef.current.scrollIntoView({ behavior: "smooth", block: "center" });
    }
  }, [activeLineIndex]);

  if (!pipeline) {
    return (
      <div className="flex flex-col items-center justify-center py-24 gap-4 text-muted">
        <Spinner size="lg" />
        <p className="text-sm">Loading pipeline...</p>
      </div>
    );
  }

  const isLive = pipeline.status === "Running" || pipeline.status === "Pending";

  // Recording helpers
  const recordingTranscript = (() => {
    try {
      return pipeline.recordingTranscriptJson
        ? JSON.parse(pipeline.recordingTranscriptJson)
        : [];
    } catch { return []; }
  })();

  const uniqueSpeakers = [...new Set(recordingTranscript.map(l => l.speaker))];

  const hoursLeft = pipeline.recordingExpiresAt
    ? Math.floor((new Date(pipeline.recordingExpiresAt) - Date.now()) / 3_600_000)
    : null;

  const handleTimeUpdate = () => {
    const currentTime = audioRef.current?.currentTime ?? 0;
    const idx = recordingTranscript.findIndex(
      line => currentTime >= line.start && currentTime <= line.end
    );
    setActiveLineIndex(idx);
  };

  const seekTo = (time) => {
    if (audioRef.current) audioRef.current.currentTime = time;
  };

  const downloadTranscript = () => {
    const text = pipeline.transcript || "";
    if (!text) return;
    const blob = new Blob([text], { type: "text/plain" });
    const a    = document.createElement("a");
    a.href     = URL.createObjectURL(blob);
    a.download = `${pipeline.clientName.replace(/\s+/g, "-")}-transcript.txt`;
    a.click();
    URL.revokeObjectURL(a.href);
  };

  const handleDownload = async (url, filename) => {
    try {
      const res  = await fetch(url);
      const blob = await res.blob();
      const href = URL.createObjectURL(blob);
      const a    = document.createElement("a");
      a.href     = href;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(href);
    } catch (e) {
      console.error("Download failed:", e);
    }
  };

  const handleRefreshRecording = async () => {
    setRefreshing(true);
    setRefreshError("");
    try {
      const res     = await authFetch(`${API}/pipeline/${id}/refresh-recording`);
      const updated = await res.json();
      if (updated.recordingVideoUrl || updated.recordingAudioUrl) {
        setPipeline(normalizePipeline(updated));
      } else {
        setRefreshError(
          "Recording still not available on Recall.ai. Make sure cloud recording is enabled in your Zoom account settings."
        );
      }
    } catch (e) {
      setRefreshError("Refresh request failed: " + e.message);
    } finally {
      setRefreshing(false);
    }
  };

  const handleStop = async () => {
    setStopping(true);
    try {
      const res = await authFetch(`${API}/pipeline/${id}/stop`, { method: "POST" });
      if (res.ok) {
        const updated = await authFetch(`${API}/pipeline/${id}`);
        setPipeline(normalizePipeline(await updated.json()));
      }
    } catch (e) {
      console.error("Stop failed:", e);
    } finally {
      setStopping(false);
    }
  };

  const handleRerun = async () => {
    setRerunning(true);
    try {
      const res  = await authFetch(`${API}/pipeline/${id}/rerun`, { method: "POST" });
      const data = await res.json();
      if (data.pipelineId) navigate(`/pipelines/${data.pipelineId}`);
    } catch (e) {
      console.error("Re-run failed:", e);
    } finally {
      setRerunning(false);
    }
  };


  return (
    <>
      {/* Wave animation keyframes injected once */}
      <style>{`
        @keyframes momWave {
          from { transform: scaleY(0.4); opacity: 0.6; }
          to   { transform: scaleY(1.2); opacity: 1;   }
        }
      `}</style>

      <div className="space-y-6 max-w-4xl">

        {/* ── Header ──────────────────────────────────────────── */}
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="sm" onClick={() => navigate("/pipelines")}>
            <ArrowLeft className="w-4 h-4" /> Back
          </Button>
          <div className="flex-1">
            <div className="flex items-center gap-3">
              <h2 className="font-display font-bold text-txt text-xl">{pipeline.clientName}</h2>
              {isLive && <Spinner size="sm" className="border-accent/30 border-t-accent" />}
            </div>
            <p className="text-xs text-muted mt-0.5">{new Date(pipeline.createdAt).toLocaleString("en-IN")}</p>
          </div>
          {isLive && (
            <Button
              variant="ghost"
              size="sm"
              onClick={handleStop}
              disabled={stopping}
              className="text-red hover:text-red hover:bg-red/10 border border-red/30"
            >
              <Square className="w-3.5 h-3.5" />
              {stopping ? "Stopping..." : "Stop"}
            </Button>
          )}
        </div>

        {/* ── Failed pipeline banner ──────────────────────────── */}
        {pipeline.status === "Failed" && (
          <div className="flex gap-3 bg-red/10 border border-red/25 rounded-2xl px-5 py-4">
            <AlertTriangle className="w-5 h-5 text-red shrink-0 mt-0.5" />
            <div className="flex-1 min-w-0">
              <p className="text-sm font-bold text-red mb-2">Pipeline Failed</p>
              <ul className="space-y-1 mb-3">
                {pipeline.steps
                  .filter(s => s.status === "Failed" && s.message)
                  .map((s, i) => (
                    <li key={i} className="text-xs text-red/80 break-words">
                      <span className="font-semibold">{s.name}:</span> {s.message}
                    </li>
                  ))}
              </ul>
              <Button
                variant="secondary"
                size="sm"
                onClick={handleRerun}
                disabled={rerunning}
              >
                {rerunning
                  ? <><Spinner size="sm" /> Starting...</>
                  : <><RotateCcw className="w-3.5 h-3.5" /> Re-run Pipeline</>}
              </Button>
            </div>
          </div>
        )}

        {/* ── Timeline stepper ────────────────────────────────── */}
        <Card className="p-6">
          <h3 className="font-display font-bold text-txt text-sm mb-5">Pipeline Progress</h3>
          <div className="space-y-0">
            {pipeline.steps.map((step, i) => {
              const meta = STEP_META[i] ?? {};
              const Icon = meta.icon ?? Clock;
              const isLast = i === pipeline.steps.length - 1;
              return (
                <div key={step.name} className="flex gap-4">
                  <div className="flex flex-col items-center">
                    <div className={cn(
                      "w-8 h-8 rounded-full border-2 flex items-center justify-center shrink-0",
                      step.status === "Done"    ? "border-green bg-green/10"   :
                      step.status === "Running" ? "border-accent bg-accent/10" :
                      step.status === "Failed"  ? "border-red bg-red/10"       :
                      "border-border bg-surface2"
                    )}>
                      <StepIcon status={step.status} index={i} />
                    </div>
                    {!isLast && (
                      <div className={cn(
                        "w-0.5 flex-1 my-1",
                        step.status === "Done" ? "bg-green/30" : "bg-border"
                      )} style={{ minHeight: "20px" }} />
                    )}
                  </div>

                  <div className={cn("flex-1 pb-5", isLast && "pb-0")}>
                    <div className="flex items-center gap-2 mb-1">
                      <Icon className={cn(
                        "w-3.5 h-3.5",
                        step.status === "Done"    ? "text-green"  :
                        step.status === "Running" ? "text-accent" :
                        step.status === "Failed"  ? "text-red"    : "text-muted"
                      )} />
                      <p className={cn(
                        "text-sm font-semibold",
                        step.status === "Done"    ? "text-txt"   :
                        step.status === "Running" ? "text-accent" :
                        step.status === "Failed"  ? "text-red"    : "text-muted"
                      )}>{step.name}</p>
                      {step.completedAt && (
                        <span className="text-[10px] text-muted ml-auto">
                          {new Date(step.completedAt).toLocaleTimeString("en-IN")}
                        </span>
                      )}
                    </div>
                    {step.message && (
                      <p className={cn(
                        "text-xs leading-relaxed mt-1 px-3 py-1.5 rounded-lg border-l-2 break-words",
                        step.status === "Failed"  ? "text-red/90 bg-red/8 border-red/50"       :
                        step.status === "Done"    ? "text-green/80 bg-green/8 border-green/40" :
                        step.status === "Running" ? "text-accent/80 bg-accent/8 border-accent/40" :
                        "text-muted bg-transparent border-transparent"
                      )}>{step.message}</p>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </Card>

        {/* ── Jira integration error banners ──────────────────── */}
        {pipeline.jiraTicket?.key === "ERROR" && (
          <div className="flex gap-3 bg-red/10 border border-red/25 rounded-2xl px-5 py-4">
            <XCircle className="w-5 h-5 text-red shrink-0 mt-0.5" />
            <div className="min-w-0 flex-1">
              <p className="text-sm font-bold text-red mb-1">Jira Integration Error</p>
              <p className="text-xs text-red/80 break-words leading-relaxed">
                {pipeline.jiraTicket.status.replace(/^Failed: \d+ — /, "")}
              </p>
              <a
                href="https://id.atlassian.com/manage-profile/security/api-tokens"
                target="_blank"
                rel="noreferrer"
                className="inline-flex items-center gap-1 mt-2 text-xs text-red/70 hover:text-red underline"
              >
                Fix: Regenerate API token at Atlassian <ExternalLink className="w-3 h-3" />
              </a>
            </div>
          </div>
        )}
        {pipeline.jiraTicket?.key === "NOT-CONFIGURED" && (
          <div className="flex gap-3 bg-orange/10 border border-orange/25 rounded-2xl px-5 py-4">
            <AlertTriangle className="w-5 h-5 text-orange shrink-0 mt-0.5" />
            <div>
              <p className="text-sm font-bold text-orange mb-1">Jira Not Connected</p>
              <p className="text-xs text-orange/80">Connect Jira in Integrations to auto-create tickets.</p>
              <a href="/integrations" className="inline-block mt-2 text-xs text-orange hover:underline font-semibold">
                Go to Integrations →
              </a>
            </div>
          </div>
        )}

        {/* ── Results tabs (only when done) ───────────────────── */}
        {pipeline.status === "Done" && (
          <div className="space-y-4">
            <Tabs tabs={TABS} active={activeTab} onChange={setActiveTab} />

            {/* Steps tab */}
            {activeTab === "steps" && (
              <Card className="divide-y divide-border">
                {pipeline.steps.map((step, i) => (
                  <div key={step.name} className="flex items-start gap-3 px-5 py-4">
                    <div className={cn(
                      "w-6 h-6 rounded-full border flex items-center justify-center shrink-0 mt-0.5",
                      step.status === "Done"   ? "border-green/40 bg-green/10"  :
                      step.status === "Failed" ? "border-red/40 bg-red/10"      : "border-border bg-surface2"
                    )}>
                      <StepIcon status={step.status} index={i} />
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className="text-sm font-semibold text-txt">{step.name}</p>
                      {step.message && <p className="text-xs text-muted mt-0.5">{step.message}</p>}
                    </div>
                    <Badge label={step.status} />
                  </div>
                ))}
              </Card>
            )}

            {/* MOM tab */}
            {activeTab === "mom" && (
              <Card className="p-5 space-y-4">
                {pipeline.momSummary && (
                  <div>
                    <p className="text-xs font-semibold text-muted uppercase tracking-wider mb-2">Meeting Summary</p>
                    <p className="text-sm text-txt leading-relaxed bg-surface2 border border-border rounded-xl p-4">
                      {pipeline.momSummary}
                    </p>
                  </div>
                )}
                {pipeline.bugSummary && (
                  <div>
                    <p className="text-xs font-semibold text-muted uppercase tracking-wider mb-2">Bug Extracted</p>
                    <p className="text-sm text-txt leading-relaxed bg-red/5 border border-red/20 rounded-xl p-4">
                      {pipeline.bugSummary}
                    </p>
                  </div>
                )}
              </Card>
            )}

            {/* ── Audio & Transcript tab ────────────────────────── */}
            {activeTab === "recording" && (
              <div className="space-y-4">
                {/* Audio player card — shown only if audio URL exists */}
                {pipeline.recordingAudioUrl ? (
                  <Card className="p-5 space-y-4" style={{ background: "#13161F" }}>
                    {/* Top bar: expiry warning + refresh */}
                    <div className="flex items-center justify-between flex-wrap gap-3">
                      <div className="flex items-center gap-2 text-xs font-semibold text-muted">
                        <Music className="w-3.5 h-3.5 text-accent" /> Audio Recording
                      </div>
                      <div className="flex items-center gap-2 flex-wrap">
                        {hoursLeft !== null && hoursLeft < 2 && (
                          <span className="flex items-center gap-1.5 text-xs font-semibold text-orange bg-orange/10 border border-orange/20 px-3 py-1.5 rounded-lg">
                            <AlertTriangle className="w-3.5 h-3.5" />
                            Expires in {hoursLeft}h — download now
                          </span>
                        )}
                        <Button variant="ghost" size="sm" onClick={handleRefreshRecording} disabled={refreshing}>
                          <RefreshCw className={cn("w-3.5 h-3.5", refreshing && "animate-spin")} />
                        </Button>
                      </div>
                    </div>

                    {/* Animated waveform */}
                    <div style={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 5, padding: "20px 0 12px" }}>
                      <WaveBar delay={0}    height={16} />
                      <WaveBar delay={0.15} height={28} />
                      <WaveBar delay={0.05} height={40} />
                      <WaveBar delay={0.2}  height={28} />
                      <WaveBar delay={0.1}  height={16} />
                    </div>

                    {/* Audio player */}
                    <audio
                      ref={audioRef}
                      controls
                      crossOrigin="anonymous"
                      onTimeUpdate={handleTimeUpdate}
                      style={{ width: "100%" }}
                      src={pipeline.recordingAudioUrl}
                    />

                    {/* Download buttons */}
                    <div className="flex items-center gap-2 flex-wrap pt-1">
                      <button
                        onClick={() => handleDownload(pipeline.recordingAudioUrl, `recording-${id}.mp3`)}
                        className="inline-flex items-center gap-1.5 text-xs font-semibold bg-surface2 border border-border text-txt px-3 py-2 rounded-xl hover:border-accent/40 transition-colors"
                      >
                        <Download className="w-3.5 h-3.5" /> Download Audio
                      </button>
                      {pipeline.transcript && (
                        <button
                          onClick={downloadTranscript}
                          className="inline-flex items-center gap-1.5 text-xs font-semibold bg-surface2 border border-border text-txt px-3 py-2 rounded-xl hover:border-accent/40 transition-colors"
                        >
                          <Download className="w-3.5 h-3.5" /> Download Transcript (.txt)
                        </button>
                      )}
                    </div>
                    {refreshError && (
                      <p className="text-xs text-orange/80 leading-relaxed">{refreshError}</p>
                    )}
                  </Card>
                ) : (
                  /* No audio — show refresh prompt */
                  <Card className="p-10 flex flex-col items-center gap-4 text-center" style={{ background: "#13161F" }}>
                    <VideoOff className="w-10 h-10 text-muted opacity-40" />
                    <div>
                      <p className="text-sm font-semibold text-txt mb-1">Audio recording not available</p>
                      <p className="text-xs text-muted leading-relaxed max-w-xs mx-auto">
                        The recording may still be processing on Recall.ai.
                      </p>
                    </div>
                    <div className="flex items-center gap-2 flex-wrap justify-center">
                      <Button variant="secondary" size="sm" onClick={handleRefreshRecording} disabled={refreshing}>
                        {refreshing ? <><Spinner size="sm" /> Refreshing...</> : <><RefreshCw className="w-3.5 h-3.5" /> Try Refresh</>}
                      </Button>
                      {pipeline.transcript && (
                        <button
                          onClick={downloadTranscript}
                          className="inline-flex items-center gap-1.5 text-xs font-semibold bg-surface2 border border-border text-txt px-3 py-2 rounded-xl hover:border-accent/40 transition-colors"
                        >
                          <Download className="w-3.5 h-3.5" /> Download Transcript (.txt)
                        </button>
                      )}
                    </div>
                    {refreshError && (
                      <p className="text-xs text-orange/80 max-w-xs text-center leading-relaxed">{refreshError}</p>
                    )}
                  </Card>
                )}

                {/* Synced transcript — always shown when available */}
                {recordingTranscript.length > 0 && (
                  <Card className="overflow-hidden" style={{ background: "#13161F" }}>
                    <div className="px-5 py-3 border-b border-border flex items-center justify-between">
                      <p className="text-xs font-semibold text-muted uppercase tracking-wider">Meeting Transcript</p>
                      <p className="text-[10px] text-muted">{recordingTranscript.length} segments</p>
                    </div>
                    <div className="overflow-y-auto divide-y divide-border/40" style={{ maxHeight: 360 }}>
                      {recordingTranscript.map((line, i) => {
                        const isActive = i === activeLineIndex;
                        const color    = getSpeakerColor(line.speaker, uniqueSpeakers);
                        return (
                          <div
                            key={i}
                            ref={isActive ? activeLineRef : null}
                            onClick={() => seekTo(line.start)}
                            style={{
                              borderLeft: isActive ? `3px solid ${color}` : "3px solid transparent",
                              background:  isActive ? `${color}14` : "transparent",
                              cursor: "pointer",
                              transition: "all 0.15s ease",
                            }}
                            className="px-4 py-2.5 hover:bg-surface2/60"
                          >
                            <div className="flex items-center gap-2 mb-0.5">
                              <span className="text-[10px] font-mono font-semibold" style={{ color }}>
                                {line.speaker}
                              </span>
                              <span className="text-[10px] text-muted font-mono">[{formatTime(line.start)}]</span>
                            </div>
                            <p className={cn("text-xs leading-relaxed", isActive ? "text-txt font-medium" : "text-muted")}>
                              {line.text}
                            </p>
                          </div>
                        );
                      })}
                    </div>
                  </Card>
                )}

                {/* Plain-text transcript fallback (when no recordingTranscriptJson) */}
                {recordingTranscript.length === 0 && pipeline.transcript && (
                  <Card className="p-5" style={{ background: "#13161F" }}>
                    <div className="flex items-center justify-between mb-3">
                      <p className="text-xs font-semibold text-muted uppercase tracking-wider">Captured Transcript</p>
                      <button
                        onClick={downloadTranscript}
                        className="inline-flex items-center gap-1.5 text-xs font-semibold text-accent hover:underline"
                      >
                        <Download className="w-3 h-3" /> .txt
                      </button>
                    </div>
                    <p className="text-xs text-txt leading-relaxed whitespace-pre-wrap bg-surface2 border border-border rounded-xl p-4">
                      {pipeline.transcript}
                    </p>
                  </Card>
                )}
              </div>
            )}

            {/* Jira tab */}
            {activeTab === "jira" && (
              pipeline.jiraTicket?.key === "ERROR" ? (
                <Card className="p-5 space-y-3">
                  <div className="flex items-center gap-2 text-red">
                    <XCircle className="w-4 h-4" />
                    <p className="text-sm font-bold">Jira Ticket Creation Failed</p>
                  </div>
                  <p className="text-xs text-muted break-words leading-relaxed">
                    {pipeline.jiraTicket.status.replace(/^Failed: \d+ — /, "")}
                  </p>
                  <a
                    href="https://id.atlassian.com/manage-profile/security/api-tokens"
                    target="_blank"
                    rel="noreferrer"
                    className="inline-flex items-center gap-1 text-xs text-accent hover:underline"
                  >
                    Regenerate API token at Atlassian <ExternalLink className="w-3 h-3" />
                  </a>
                </Card>
              ) : pipeline.jiraTicket?.key === "NOT-CONFIGURED" ? (
                <Card className="p-5 text-center space-y-3">
                  <p className="text-sm font-semibold text-txt">Jira not connected</p>
                  <p className="text-xs text-muted">Connect Jira in the Integrations page to auto-create tickets.</p>
                  <a href="/integrations" className="inline-block text-xs text-accent hover:underline font-semibold">Go to Integrations →</a>
                </Card>
              ) : pipeline.jiraTicket ? (
                <Card className="p-5 space-y-4">
                  <div className="flex items-center gap-3 flex-wrap">
                    <span className="font-mono font-bold text-accent text-base bg-accent/10 border border-accent/20 px-3 py-1 rounded-lg">
                      {pipeline.jiraTicket.key}
                    </span>
                    <Badge label={pipeline.jiraTicket.priority} />
                    <Badge label={pipeline.jiraTicket.status}>{pipeline.jiraTicket.status}</Badge>
                    <span className="text-xs text-muted ml-auto">Reporter: {pipeline.jiraTicket.reporter}</span>
                  </div>
                  <p className="text-sm font-semibold text-txt">{pipeline.jiraTicket.summary}</p>
                  <div>
                    <p className="text-xs font-semibold text-muted uppercase tracking-wider mb-2">Description</p>
                    <CodeBlock>{pipeline.jiraTicket.description}</CodeBlock>
                  </div>
                  {pipeline.jiraTicket.url && (
                    <a
                      href={pipeline.jiraTicket.url}
                      target="_blank"
                      rel="noreferrer"
                      className="inline-flex items-center gap-2 text-xs text-accent hover:underline"
                    >
                      <ExternalLink className="w-3.5 h-3.5" /> View on Jira
                    </a>
                  )}
                </Card>
              ) : (
                <Card className="p-5 text-center text-sm text-muted">No Jira ticket data available.</Card>
              )
            )}

            {/* Bug Fix tab */}
            {activeTab === "bug" && pipeline.bugAnalysis && (
              <Card className="p-5 space-y-4">
                <div className="flex items-center gap-3 flex-wrap">
                  <div className="flex items-center gap-2 bg-surface2 border border-border rounded-lg px-3 py-1.5">
                    <FileCode className="w-3.5 h-3.5 text-accent" />
                    <span className="text-xs font-mono font-semibold text-txt">{pipeline.bugAnalysis.fileName}</span>
                  </div>
                  <div className="flex items-center gap-2 bg-surface2 border border-border rounded-lg px-3 py-1.5">
                    <MapPin className="w-3.5 h-3.5 text-orange" />
                    <span className="text-xs font-mono font-semibold text-txt">Line {pipeline.bugAnalysis.lineNumber}</span>
                  </div>
                </div>
                <div>
                  <p className="text-xs font-semibold text-muted uppercase tracking-wider mb-1.5">Root Cause</p>
                  <p className="text-sm text-txt leading-relaxed">{pipeline.bugAnalysis.rootCause}</p>
                </div>
                <div>
                  <p className="text-xs font-semibold text-muted uppercase tracking-wider mb-1.5">Suggested Fix</p>
                  <p className="text-sm text-txt leading-relaxed">{pipeline.bugAnalysis.suggestedFix}</p>
                </div>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <p className="text-xs font-semibold text-red/80 uppercase tracking-wider mb-2 flex items-center gap-1.5">
                      <XCircle className="w-3.5 h-3.5" /> Before
                    </p>
                    <CodeBlock variant="red">{pipeline.bugAnalysis.originalCode}</CodeBlock>
                  </div>
                  <div>
                    <p className="text-xs font-semibold text-green/80 uppercase tracking-wider mb-2 flex items-center gap-1.5">
                      <CheckCircle2 className="w-3.5 h-3.5" /> After
                    </p>
                    <CodeBlock variant="green">{pipeline.bugAnalysis.fixedCode}</CodeBlock>
                  </div>
                </div>
              </Card>
            )}

            {/* PR tab */}
            {activeTab === "pr" && pipeline.gitHubResult && (
              <Card className="p-5 space-y-4">
                <div className="flex items-center gap-3 flex-wrap">
                  <Badge label="Open">Open</Badge>
                  <span className="font-mono text-sm font-bold text-txt">#{pipeline.gitHubResult.prNumber}</span>
                  <span className="text-xs font-mono text-muted bg-surface2 border border-border px-2 py-1 rounded-lg">
                    🌿 {pipeline.gitHubResult.branchName}
                  </span>
                </div>
                <p className="text-sm font-semibold text-txt">{pipeline.gitHubResult.prTitle}</p>
                <div>
                  <p className="text-xs font-semibold text-muted uppercase tracking-wider mb-2">PR Description</p>
                  <CodeBlock>{pipeline.gitHubResult.prDescription}</CodeBlock>
                </div>
                {pipeline.gitHubResult.prUrl && (
                  <a
                    href={pipeline.gitHubResult.prUrl}
                    target="_blank"
                    rel="noreferrer"
                    className="inline-flex items-center gap-2 bg-accent hover:bg-accent/90 text-white text-sm font-semibold px-4 py-2.5 rounded-xl transition-colors"
                  >
                    <ExternalLink className="w-4 h-4" /> View PR on GitHub
                  </a>
                )}
              </Card>
            )}
          </div>
        )}
      </div>
    </>
  );
}

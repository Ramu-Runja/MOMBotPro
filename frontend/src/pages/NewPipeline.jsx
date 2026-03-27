import { useState, useEffect } from "react";
import { useNavigate, Link } from "react-router-dom";
import {
  Play, FileText, Clipboard, AlertCircle, Zap, Bot,
  Upload, Video, X,
} from "lucide-react";
import { Card, Input, Textarea, Spinner, Button, cn } from "../components/ui";
import { authFetch } from "../utils/authFetch";

const API = "http://localhost:5000/api";

const SAMPLE = `Client said: "Bhai login button press cheste emi happen avvatledu. Android lo test chesam, nothing works. Last week varaku correct ga work avutundi. Ippudu all Android users affect avutunnaru. Please fix ASAP."`;

// ── Drag-drop upload zone ───────────────────────────────────────
function DropZone({ file, onFile }) {
  const [dragging, setDragging] = useState(false);

  return (
    <div
      onDragOver={(e) => { e.preventDefault(); setDragging(true);  }}
      onDragLeave={() => setDragging(false)}
      onDrop={(e)  => { e.preventDefault(); setDragging(false); const f = e.dataTransfer.files[0]; if (f) onFile(f); }}
      className={cn(
        "border-2 border-dashed rounded-xl px-6 py-12 flex flex-col items-center transition-all",
        dragging
          ? "border-accent bg-accent/5"
          : "border-border hover:border-border/80 bg-surface2/50"
      )}
    >
      <div className="w-14 h-14 rounded-full bg-surface2 border border-border flex items-center justify-center mb-4 text-muted">
        <Upload className="w-6 h-6" />
      </div>

      {file ? (
        <div className="text-center">
          <div className="font-semibold text-txt text-sm mb-1">{file.name}</div>
          <div className="text-xs text-muted">
            {(file.size / 1024 / 1024).toFixed(1)} MB
          </div>
          <button
            type="button"
            onClick={() => onFile(null)}
            className="mt-3 flex items-center gap-1.5 text-xs text-red hover:underline mx-auto"
          >
            <X className="w-3 h-3" /> Remove
          </button>
        </div>
      ) : (
        <>
          <div className="font-semibold text-txt text-sm mb-1">Drop your Zoom recording here</div>
          <div className="text-xs text-muted mb-4">or click to browse from your computer</div>
          <label className="bg-txt text-surface text-xs font-semibold px-5 py-2 rounded-lg cursor-pointer hover:opacity-80 transition-opacity">
            Choose File
            <input
              type="file"
              accept=".mp4,.mov,.avi,.mkv"
              className="hidden"
              onChange={(e) => { if (e.target.files[0]) onFile(e.target.files[0]); }}
            />
          </label>
          <div className="text-[11px] text-muted mt-3">Supports: MP4, MOV, AVI, MKV</div>
        </>
      )}
    </div>
  );
}

// ── Page ────────────────────────────────────────────────────────
export default function NewPipeline() {
  const navigate                        = useNavigate();
  const [inputMode,   setInputMode]     = useState("text");   // "file" | "text"
  const [file,        setFile]          = useState(null);
  const [clientName,  setClientName]    = useState("");
  const [transcript,  setTranscript]    = useState("");
  const [mode,        setMode]          = useState("text");   // "text" | "sample" (for transcript)
  const [loading,     setLoading]       = useState(false);
  const [error,       setError]         = useState("");
  const [savedZoomLink, setSavedZoomLink] = useState("");

  useEffect(() => {
    authFetch("http://localhost:5000/api/zoom-settings")
      .then(r => r.ok ? r.json() : null)
      .then(d => { if (d?.zoomLink) setSavedZoomLink(d.zoomLink); })
      .catch(() => {});
  }, []);

  const handleSample = () => { setMode("sample"); setTranscript(SAMPLE); };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");

    if (!clientName.trim()) { setError("Meeting title / client name is required."); return; }

    if (inputMode === "file") {
      if (!file) { setError("Please select a recording file."); return; }
      // File upload path — show info since backend may not support it yet
      setError("File upload pipeline coming soon. Please use Text Input mode for now.");
      return;
    }

    if (!transcript.trim()) { setError("Transcript / bug description is required."); return; }

    setLoading(true);
    try {
      const res  = await authFetch(`${API}/pipeline/start`, {
        method: "POST",
        body: JSON.stringify({ clientName: clientName.trim(), transcript: transcript.trim() }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error ?? "Failed to start pipeline.");
      navigate(`/pipelines/${data.pipelineId ?? data.id}`);
    } catch (ex) {
      setError(ex.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="max-w-2xl space-y-6">

      {/* ── Upload card ──────────────────────────────────── */}
      <Card className="p-6">
        {/* Header */}
        <div className="flex items-center gap-3 mb-6">
          <div className="w-10 h-10 rounded-xl bg-accent/15 border border-accent/25 flex items-center justify-center">
            <Zap className="w-5 h-5 text-accent" />
          </div>
          <div>
            <h2 className="font-display font-bold text-txt text-base">New Pipeline</h2>
            <p className="text-xs text-muted">Upload a recording or paste a meeting transcript</p>
          </div>
        </div>

        <form onSubmit={handleSubmit} className="space-y-5">
          {/* Meeting title */}
          <Input
            label="Meeting Title"
            placeholder="e.g. Sprint Planning Q2 2026"
            value={clientName}
            onChange={e => setClientName(e.target.value)}
            disabled={loading}
          />

          {/* Input mode toggle */}
          <div className="flex flex-col gap-2">
            <p className="text-xs font-semibold text-muted uppercase tracking-wider">Input Mode</p>
            <div className="flex gap-2">
              <button
                type="button"
                onClick={() => { setInputMode("file"); setFile(null); }}
                className={cn(
                  "flex-1 flex items-center justify-center gap-2 py-2.5 rounded-xl text-sm font-medium border transition-all",
                  inputMode === "file"
                    ? "bg-accent/15 text-accent border-accent/30"
                    : "bg-surface2 text-muted border-border hover:text-txt"
                )}
              >
                <Video className="w-4 h-4" /> Upload Recording
              </button>
              <button
                type="button"
                onClick={() => { setInputMode("text"); setFile(null); }}
                className={cn(
                  "flex-1 flex items-center justify-center gap-2 py-2.5 rounded-xl text-sm font-medium border transition-all",
                  inputMode === "text"
                    ? "bg-accent/15 text-accent border-accent/30"
                    : "bg-surface2 text-muted border-border hover:text-txt"
                )}
              >
                <FileText className="w-4 h-4" /> Text Input
              </button>
            </div>
          </div>

          {/* File upload zone */}
          {inputMode === "file" && (
            <DropZone file={file} onFile={setFile} />
          )}

          {/* Text transcript */}
          {inputMode === "text" && (
            <>
              {/* Sample toggle */}
              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={() => { setMode("text"); setTranscript(""); }}
                  className={cn(
                    "flex-1 flex items-center justify-center gap-2 py-2 rounded-lg text-xs font-medium border transition-all",
                    mode === "text"
                      ? "bg-surface border-border text-txt"
                      : "bg-surface2 text-muted border-border hover:text-txt"
                  )}
                >
                  <FileText className="w-3.5 h-3.5" /> Custom Transcript
                </button>
                <button
                  type="button"
                  onClick={handleSample}
                  className={cn(
                    "flex-1 flex items-center justify-center gap-2 py-2 rounded-lg text-xs font-medium border transition-all",
                    mode === "sample"
                      ? "bg-surface border-border text-txt"
                      : "bg-surface2 text-muted border-border hover:text-txt"
                  )}
                >
                  <Clipboard className="w-3.5 h-3.5" /> Use Sample
                </button>
              </div>

              <Textarea
                label="Transcript / Bug Description"
                placeholder="Paste the Tenglish meeting transcript or describe the bug..."
                value={transcript}
                onChange={e => setTranscript(e.target.value)}
                disabled={loading}
                rows={7}
              />
            </>
          )}

          {/* Saved Zoom link hint */}
          {savedZoomLink && (
            <div className="flex items-center gap-2 bg-accent/8 border border-accent/20 rounded-xl px-4 py-3">
              <Bot className="w-4 h-4 text-accent shrink-0" />
              <p className="text-xs text-accent flex-1">
                Using your saved Zoom link ·{" "}
                <Link to="/zoom" className="underline underline-offset-2 hover:text-txt">Change</Link>
              </p>
            </div>
          )}

          {/* Error */}
          {error && (
            <div className="flex items-center gap-2 bg-red/10 border border-red/20 rounded-xl px-4 py-3">
              <AlertCircle className="w-4 h-4 text-red shrink-0" />
              <p className="text-xs text-red">{error}</p>
            </div>
          )}

          {/* Submit */}
          <Button type="submit" className="w-full justify-center py-3" disabled={loading}>
            {loading
              ? <><Spinner size="sm" /> Starting pipeline...</>
              : <><Play className="w-4 h-4" /> Start Pipeline</>
            }
          </Button>
        </form>
      </Card>

      {/* ── What happens next card ────────────────────────── */}
      <Card className="p-5">
        <h3 className="font-display font-bold text-txt text-sm mb-3">What happens next?</h3>
        <ol className="space-y-2">
          {[
            "Claude reads the transcript and extracts the bug",
            "A Jira ticket is auto-created with priority",
            "GitHub repo is scanned for the relevant file",
            "Claude generates the before/after code fix",
            "A branch is created and a PR is raised automatically",
          ].map((step, i) => (
            <li key={i} className="flex items-start gap-3 text-xs text-muted">
              <span className="w-5 h-5 rounded-full bg-accent/10 border border-accent/20 text-accent text-[10px] font-bold flex items-center justify-center shrink-0 mt-0.5">
                {i + 1}
              </span>
              {step}
            </li>
          ))}
        </ol>
      </Card>
    </div>
  );
}

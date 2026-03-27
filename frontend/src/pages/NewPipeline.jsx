import { useState, useEffect } from "react";
import { useNavigate, Link } from "react-router-dom";
import { Play, FileText, Clipboard, AlertCircle, Zap, Bot } from "lucide-react";
import { Card, Button, Input, Textarea, Spinner, cn } from "../components/ui";
import { authFetch } from "../utils/authFetch";

const API = "http://localhost:5000/api";

const SAMPLE = `Client said: "Bhai login button press cheste emi happen avvatledu. Android lo test chesam, nothing works. Last week varaku correct ga work avutundi. Ippudu all Android users affect avutunnaru. Please fix ASAP."`;

export default function NewPipeline() {
  const navigate                      = useNavigate();
  const [clientName,  setClientName]  = useState("");
  const [transcript,  setTranscript]  = useState("");
  const [mode,        setMode]        = useState("text"); // "text" | "sample"
  const [loading,     setLoading]     = useState(false);
  const [error,       setError]       = useState("");
  const [savedZoomLink, setSavedZoomLink] = useState("");

  useEffect(() => {
    authFetch("http://localhost:5000/api/zoom-settings")
      .then(r => r.ok ? r.json() : null)
      .then(data => { if (data?.zoomLink) setSavedZoomLink(data.zoomLink); })
      .catch(() => {});
  }, []);

  const handleSample = () => {
    setMode("sample");
    setTranscript(SAMPLE);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    if (!clientName.trim()) { setError("Client name is required."); return; }
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

      <Card className="p-6">
        {/* Header */}
        <div className="flex items-center gap-3 mb-6">
          <div className="w-10 h-10 rounded-xl bg-accent/15 border border-accent/25 flex items-center justify-center">
            <Zap className="w-5 h-5 text-accent" />
          </div>
          <div>
            <h2 className="font-display font-bold text-txt text-base">Manual Pipeline Run</h2>
            <p className="text-xs text-muted">Paste a meeting transcript or describe the bug directly</p>
          </div>
        </div>

        <form onSubmit={handleSubmit} className="space-y-5">
          {/* Client name */}
          <Input
            label="Client Name"
            placeholder="e.g. Ravi Kumar"
            value={clientName}
            onChange={e => setClientName(e.target.value)}
            disabled={loading}
          />

          {/* Mode toggle */}
          <div className="flex flex-col gap-2">
            <p className="text-xs font-semibold text-muted uppercase tracking-wider">Input Mode</p>
            <div className="flex gap-2">
              <button
                type="button"
                onClick={() => { setMode("text"); setTranscript(""); }}
                className={cn(
                  "flex-1 flex items-center justify-center gap-2 py-2.5 rounded-xl text-sm font-medium border transition-all",
                  mode === "text"
                    ? "bg-accent/15 text-accent border-accent/30"
                    : "bg-surface2 text-muted border-border hover:text-txt"
                )}
              >
                <FileText className="w-4 h-4" /> Custom Transcript
              </button>
              <button
                type="button"
                onClick={handleSample}
                className={cn(
                  "flex-1 flex items-center justify-center gap-2 py-2.5 rounded-xl text-sm font-medium border transition-all",
                  mode === "sample"
                    ? "bg-accent/15 text-accent border-accent/30"
                    : "bg-surface2 text-muted border-border hover:text-txt"
                )}
              >
                <Clipboard className="w-4 h-4" /> Use Sample
              </button>
            </div>
          </div>

          {/* Transcript */}
          <Textarea
            label="Transcript / Bug Description"
            placeholder="Paste the Tenglish meeting transcript or describe the bug..."
            value={transcript}
            onChange={e => setTranscript(e.target.value)}
            disabled={loading}
            rows={7}
          />

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
              : <><Play className="w-4 h-4" /> Run Pipeline</>}
          </Button>
        </form>
      </Card>

      {/* Info card */}
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

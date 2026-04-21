import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { GitPullRequest, CheckCircle2, Loader2, ListTodo, RefreshCw, ArrowRight, Plus } from "lucide-react";
import { Card, Badge, StatCard, Spinner, Button, cn } from "../components/ui";
import { authFetch } from "../utils/authFetch";

const API = "http://localhost:5000/api";

const PIPELINE_STATUS = { 0: "Pending", 1: "Running", 2: "Done", 3: "Failed" };
const STEP_STATUS     = { 0: "Waiting", 1: "Running", 2: "Done", 3: "Failed" };

function norm(v, map) {
  return typeof v === "string" ? v : (map[v] ?? String(v ?? ""));
}
function normalizePipeline(p) {
  return {
    ...p,
    status: norm(p.status, PIPELINE_STATUS),
    steps: (p.steps ?? []).map(s => ({ ...s, status: norm(s.status, STEP_STATUS) })),
  };
}

const STATUS_DOT = {
  Done:    "bg-green",
  Running: "bg-accent animate-pulse",
  Failed:  "bg-red",
  Pending: "bg-muted",
};

export default function PipelineDashboard() {
  const navigate                  = useNavigate();
  const [pipelines, setPipelines] = useState([]);
  const [loading, setLoading]     = useState(true);

  const load = async () => {
    setLoading(true);
    try {
      const res  = await authFetch(`${API}/pipeline`);
      const data = await res.json();
      setPipelines(data.map(normalizePipeline));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const total     = pipelines.length;
  const completed = pipelines.filter(p => p.status === "Done").length;
  const running   = pipelines.filter(p => p.status === "Running" || p.status === "Pending").length;
  const prs       = pipelines.filter(p => p.gitHubResult?.prNumber).length;

  return (
    <div className="space-y-6">

      {/* ── Stat cards ─────────────────────────────────────── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard label="Total Runs"  value={total}     icon={<ListTodo       className="w-5 h-5" />} color="accent"  />
        <StatCard label="Completed"   value={completed} icon={<CheckCircle2   className="w-5 h-5" />} color="green"   />
        <StatCard label="Running"     value={running}   icon={<Loader2        className="w-5 h-5" />} color="orange"  />
        <StatCard label="PRs Raised"  value={prs}       icon={<GitPullRequest className="w-5 h-5" />} color="accent"  />
      </div>

      {/* ── Pipeline list ──────────────────────────────────── */}
      <Card>
        <div className="flex items-center justify-between px-6 py-4 border-b border-border">
          <div>
            <h2 className="font-display font-bold text-txt text-base">Pipeline Runs</h2>
            <p className="text-xs text-muted mt-0.5">{total} total executions</p>
          </div>
          <div className="flex items-center gap-2">
            <Button variant="secondary" size="sm" onClick={() => navigate("/pipelines/new")}>
              <Plus className="w-3.5 h-3.5" /> New Run
            </Button>
            <Button variant="ghost" size="sm" onClick={load} disabled={loading}>
              <RefreshCw className={cn("w-3.5 h-3.5", loading && "animate-spin")} />
            </Button>
          </div>
        </div>

        {loading ? (
          <div className="flex items-center justify-center py-16 gap-3 text-muted">
            <Spinner size="md" />
            <span className="text-sm">Loading pipelines...</span>
          </div>
        ) : pipelines.length === 0 ? (
          <div className="text-center py-16">
            <p className="text-4xl mb-3">📭</p>
            <p className="text-sm font-semibold text-txt">No pipeline runs yet</p>
            <p className="text-xs text-muted mt-1">Start a run from the sidebar</p>
          </div>
        ) : (
          <div className="divide-y divide-border/10">
            {pipelines.map((p) => {
              const doneCount = p.steps.filter(s => s.status === "Done").length;
              return (
                <div
                  key={p.id}
                  onClick={() => navigate(`/pipelines/${p.id}`)}
                  className="flex items-center gap-4 px-6 py-4 hover:bg-surface2 cursor-pointer transition-colors group"
                >
                  {/* Status indicator */}
                  <div className={cn("w-2 h-2 rounded-full shrink-0", STATUS_DOT[p.status] ?? "bg-muted")} />

                  {/* Client + time */}
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-semibold text-txt truncate">{p.clientName}</p>
                    <p className="text-xs text-muted mt-0.5">
                      {new Date(p.createdAt).toLocaleString("en-IN")}
                    </p>
                  </div>

                  {/* Step progress dots */}
                  <div className="hidden md:flex items-center gap-1">
                    {p.steps.map((s, i) => (
                      <div
                        key={i}
                        title={s.name}
                        className={cn(
                          "w-1.5 h-1.5 rounded-full",
                          s.status === "Done"    ? "bg-green" :
                          s.status === "Running" ? "bg-accent animate-pulse" :
                          s.status === "Failed"  ? "bg-red" : "bg-border"
                        )}
                      />
                    ))}
                    <span className="text-[11px] text-muted ml-1.5">{doneCount}/6</span>
                  </div>

                  {/* Badges */}
                  <div className="flex items-center gap-2 shrink-0">
                    {p.jiraTicket?.key && (
                      <span className="hidden sm:inline text-[10px] font-mono bg-accent/10 text-accent border border-accent/20 px-2 py-0.5 rounded">
                        {p.jiraTicket.key}
                      </span>
                    )}
                    {p.gitHubResult?.prNumber && (
                      <span className="hidden sm:inline text-[10px] font-mono bg-green/10 text-green border border-green/20 px-2 py-0.5 rounded">
                        PR #{p.gitHubResult.prNumber}
                      </span>
                    )}
                    <Badge label={p.status} />
                  </div>

                  <ArrowRight className="w-4 h-4 text-muted opacity-0 group-hover:opacity-60 transition-opacity shrink-0" />
                </div>
              );
            })}
          </div>
        )}
      </Card>
    </div>
  );
}

import { useState, useEffect } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  Video, FileText, GitPullRequest, TrendingUp,
  CheckCircle2, AlertCircle, Clock,
} from "lucide-react";
import { useAuth } from "../context/AuthContext";
import { cn } from "../components/ui";

// ── Status badge ─────────────────────────────────────────────────
function StatusBadge({ status }) {
  const conf = {
    Done:    ["bg-green/10 text-green",   "completed"  ],
    Running: ["bg-accent/10 text-accent", "processing" ],
    Pending: ["bg-accent/10 text-accent", "processing" ],
    Failed:  ["bg-red/10 text-red",       "failed"     ],
  };
  const [cls, label] = conf[status] ?? conf.Pending;
  return (
    <span className={cn("px-2.5 py-0.5 rounded-full text-xs font-medium", cls)}>
      {label}
    </span>
  );
}

// ── 6 pipeline stages ────────────────────────────────────────────
const STAGES = [
  { label: "Upload",     sub: "Meeting file uploaded"     },
  { label: "Transcribe", sub: "Converting speech to text" },
  { label: "Analyze",    sub: "AI analyzing content"      },
  { label: "Extract",    sub: "Extracting action items"   },
  { label: "Generate",   sub: "Creating tickets & PRs"    },
  { label: "Complete",   sub: "Pipeline finished"         },
];

// ── Page ─────────────────────────────────────────────────────────
export default function Dashboard() {
  const { authFetch }         = useAuth();
  const navigate              = useNavigate();
  const [stats, setStats]     = useState(null);
  const [pipes, setPipes]     = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // Stats
    authFetch("http://localhost:5000/api/analytics?period=30d")
      .then(r => r.ok ? r.json() : null)
      .then(d => { if (d) setStats(d); })
      .catch(() => {});

    // Recent pipelines (last 6)
    authFetch("http://localhost:5000/api/pipeline")
      .then(r => r.json())
      .then(d => setPipes(Array.isArray(d) ? d.slice(0, 6) : []))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [authFetch]);

  const normSt = (p) => {
    const map = { 0: "Pending", 1: "Running", 2: "Done", 3: "Failed" };
    return typeof p.status === "string" ? p.status : (map[p.status] ?? "Pending");
  };

  const fmtDate = (d) => d
    ? new Date(d).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })
    : "—";

  const STAT_CARDS = [
    {
      icon:   <Video className="w-[18px] h-[18px]" />,
      value:  stats?.totalCalls    ?? "—",
      label:  "Meetings Processed",
      change: stats ? "+12% from last month" : "No data yet",
      up:     true,
    },
    {
      icon:   <FileText className="w-[18px] h-[18px]" />,
      value:  stats?.momsGenerated ?? "—",
      label:  "MOMs Generated",
      change: stats ? "+18% from last month" : "No data yet",
      up:     true,
    },
    {
      icon:   <GitPullRequest className="w-[18px] h-[18px]" />,
      value:  stats?.prsRaised    ?? "—",
      label:  "PRs Generated",
      change: stats ? "+7% from last month" : "No data yet",
      up:     true,
    },
    {
      icon:   <TrendingUp className="w-[18px] h-[18px]" />,
      value:  stats ? `${stats.successRate}%` : "—",
      label:  "Success Rate",
      change: stats ? "Based on last 30 days" : "No data yet",
      up:     true,
    },
  ];

  return (
    <div>
      

      {/* ── Stat Cards ─────────────────────────────────────── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-7">
        {STAT_CARDS.map((s, i) => (
          <div key={i} className="bg-surface border border-border rounded-xl p-5 shadow-sm">
            <div className="w-9 h-9 bg-surface2 border border-border rounded-lg flex items-center justify-center mb-3 text-muted">
              {s.icon}
            </div>
            <div className="text-[28px] font-display font-bold text-txt mb-0.5">{s.value}</div>
            <div className="text-[13px] text-muted mb-1.5">{s.label}</div>
            <div className={cn("text-xs font-medium", s.up ? "text-green" : "text-red")}>
              {s.change}
            </div>
          </div>
        ))}
      </div>

      {/* ── Recent Pipelines ───────────────────────────────── */}
      <div className="bg-surface border border-border rounded-xl p-6 mb-6 shadow-sm">
        <div className="flex items-center justify-between mb-5">
          <div>
            <div className="font-display font-bold text-txt">Recent Pipelines</div>
            <div className="text-xs text-muted mt-0.5">Recent and active automation pipelines</div>
          </div>
          <button
            onClick={() => navigate("/pipelines/new")}
            className="bg-txt text-surface text-[13px] font-semibold px-4 py-2 rounded-lg hover:opacity-80 transition-opacity"
          >
            New Pipeline
          </button>
        </div>

        {/* Table header */}
        <div className="hidden md:grid grid-cols-[1fr_130px_140px_70px_70px_110px] pb-2 border-b border-border mb-1">
          {["Meeting Name", "Date", "Status", "Tickets", "PRs", ""].map((h, i) => (
            <div key={i} className="text-[13px] font-semibold text-muted">{h}</div>
          ))}
        </div>

        {loading ? (
          <div className="py-8 flex justify-center">
            <div className="w-5 h-5 border-2 border-border border-t-accent rounded-full animate-spin" />
          </div>
        ) : pipes.length === 0 ? (
          <div className="py-8 text-center text-sm text-muted">
            No pipelines yet.{" "}
            <Link to="/pipelines/new" className="text-accent hover:underline">
              Start your first run
            </Link>.
          </div>
        ) : (
          pipes.map((p, i) => {
            const st = normSt(p);
            return (
              <div
                key={p.id ?? i}
                className={cn(
                  "grid grid-cols-1 md:grid-cols-[1fr_130px_140px_70px_70px_110px] py-3.5 gap-1 md:gap-0 items-center",
                  i < pipes.length - 1 && "border-b border-border/40"
                )}
              >
                {/* Name */}
                <div className="flex items-center gap-2 min-w-0">
                  {st === "Done"
                    ? <CheckCircle2 className="w-[18px] h-[18px] text-green shrink-0" />
                    : st === "Failed"
                    ? <AlertCircle  className="w-[18px] h-[18px] text-red shrink-0" />
                    : <Clock        className="w-[18px] h-[18px] text-accent animate-pulse shrink-0" />
                  }
                  <span className="text-sm font-medium text-txt truncate">
                    {p.clientName ?? `Pipeline #${p.id}`}
                  </span>
                </div>
                {/* Date */}
                <div className="text-[13px] text-muted hidden md:block">{fmtDate(p.createdAt)}</div>
                {/* Status */}
                <div className="hidden md:block"><StatusBadge status={st} /></div>
                {/* Tickets */}
                <div className="text-sm text-txt hidden md:block">{p.jiraResult ? "1" : "0"}</div>
                {/* PRs */}
                <div className="text-sm text-txt hidden md:block">{p.gitHubResult?.prNumber ? "1" : "0"}</div>
                {/* Action */}
                <div className="hidden md:block">
                  {(st === "Done" || st === "Running") && (
                    <Link
                      to={`/pipelines/${p.id}`}
                      className="text-accent text-[13px] font-medium hover:underline"
                    >
                      {st === "Done" ? "View Results" : "View Live"}
                    </Link>
                  )}
                </div>
              </div>
            );
          })
        )}
      </div>

      {/* ── Pipeline Automation Stages ─────────────────────── */}
      <div className="bg-surface border border-border rounded-xl p-6 shadow-sm">
        <div className="font-display font-bold text-txt mb-1">Pipeline Automation Stages</div>
        <div className="text-[13px] text-muted mb-7">6 automated stages from upload to completion</div>

        {/* Stages row — scrollable on small screens */}
        <div className="overflow-x-auto">
          <div className="flex items-start relative min-w-[540px]">
            {/* Connector line */}
            <div className="absolute top-5 left-5 right-5 h-0.5 bg-border z-0" />
            {STAGES.map((s, i) => (
              <div key={i} className="flex-1 flex flex-col items-center z-10">
                <div className="w-10 h-10 rounded-full bg-txt text-surface flex items-center justify-center mb-2.5 shadow-sm">
                  <CheckCircle2 className="w-4 h-4" />
                </div>
                <div className="text-[13px] font-semibold text-txt text-center leading-tight">{s.label}</div>
                <div className="text-[11px] text-muted text-center mt-0.5 px-1 leading-tight">{s.sub}</div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

import { useState, useEffect, useRef, useCallback } from "react";
import {
  Chart as ChartJS,
  CategoryScale, LinearScale, BarElement, LineElement, PointElement,
  ArcElement, Title, Tooltip, Legend, Filler,
} from "chart.js";
import { Bar, Doughnut, Line } from "react-chartjs-2";
import {
  Phone, FileText, Clock, GitPullRequest,
  TrendingUp, RefreshCw, ExternalLink,
} from "lucide-react";
import { Card, Badge, Button, Spinner, cn } from "../components/ui";
import { authFetch } from "../utils/authFetch";

ChartJS.register(
  CategoryScale, LinearScale, BarElement, LineElement, PointElement,
  ArcElement, Title, Tooltip, Legend, Filler
);

const API = "http://localhost:5000/api";

// ── Chart.js global defaults ───────────────────────────────────────────────
ChartJS.defaults.color          = "#888899";
ChartJS.defaults.borderColor    = "rgba(255,255,255,0.06)";
ChartJS.defaults.font.family    = "DM Sans, sans-serif";
ChartJS.defaults.plugins.legend.labels.boxWidth = 12;
ChartJS.defaults.plugins.legend.labels.padding  = 16;

// ── Colors ────────────────────────────────────────────────────────────────
const C = {
  purple:     "#534AB7",
  purpleAlpha:"rgba(83,74,183,0.12)",
  green:      "#1D9E75",
  greenAlpha: "rgba(29,158,117,0.12)",
  red:        "#E24B4A",
  orange:     "#FF8800",
  border:     "rgba(255,255,255,0.08)",
  surface2:   "#1A1D28",
};

const PERIODS = [
  { key: "7d",  label: "Last 7 days"  },
  { key: "30d", label: "Last 30 days" },
  { key: "90d", label: "Last 90 days" },
];

// ── Skeleton loader ────────────────────────────────────────────────────────
function Skeleton({ className }) {
  return <div className={cn("bg-surface2 rounded-xl animate-pulse", className)} />;
}

function StatCardSkeleton() {
  return (
    <Card className="p-5">
      <Skeleton className="h-3 w-24 mb-3" />
      <Skeleton className="h-8 w-16 mb-2" />
      <Skeleton className="h-3 w-20" />
    </Card>
  );
}

function ChartSkeleton({ height = "h-52" }) {
  return (
    <Card className="p-5">
      <Skeleton className="h-4 w-36 mb-4" />
      <Skeleton className={cn(height, "w-full")} />
    </Card>
  );
}

// ── Stat card ──────────────────────────────────────────────────────────────
function AnalyticStatCard({ label, value, icon: Icon, subValue, trend, color }) {
  const colors = {
    purple: { icon: "text-[#534AB7] bg-[#534AB7]/10 border-[#534AB7]/20", trend: "text-[#534AB7]" },
    green:  { icon: "text-[#1D9E75] bg-[#1D9E75]/10 border-[#1D9E75]/20", trend: "text-[#1D9E75]" },
    orange: { icon: "text-orange bg-orange/10 border-orange/20",            trend: "text-orange"    },
    red:    { icon: "text-red bg-red/10 border-red/20",                     trend: "text-red"       },
  };
  const c = colors[color] ?? colors.purple;

  return (
    <Card className="p-5">
      <div className="flex items-start justify-between">
        <div className="flex-1 min-w-0">
          <p className="text-xs font-semibold text-muted uppercase tracking-wider mb-2">{label}</p>
          <p className="text-3xl font-display font-bold text-txt leading-none">{value}</p>
          {subValue && <p className="text-xs text-muted mt-1.5">{subValue}</p>}
        </div>
        <div className={cn("w-10 h-10 rounded-xl flex items-center justify-center border shrink-0 ml-3", c.icon)}>
          <Icon className="w-5 h-5" />
        </div>
      </div>
      {trend && (
        <div className={cn("flex items-center gap-1 mt-3 text-xs font-semibold", c.trend)}>
          <TrendingUp className="w-3 h-3" />
          {trend} vs last period
        </div>
      )}
    </Card>
  );
}

// ── Chart wrappers (stable refs prevent remount flicker) ──────────────────
function BarChart({ data, options }) {
  return <Bar data={data} options={options} />;
}
function DoughnutChart({ data, options }) {
  return <Doughnut data={data} options={options} />;
}
function LineChart({ data, options }) {
  return <Line data={data} options={options} />;
}

// ── Horizontal bar (built from canvas manually for simplicity) ─────────────
function HorizontalBar({ clients }) {
  const max = Math.max(...clients.map(c => c.bugsFixed), 1);
  return (
    <div className="space-y-3">
      {clients.map((c, i) => (
        <div key={i} className="flex items-center gap-3">
          <span className="text-xs text-muted w-20 truncate text-right shrink-0">{c.name}</span>
          <div className="flex-1 bg-surface2 rounded-full h-2.5 overflow-hidden">
            <div
              className="h-full rounded-full transition-all duration-700"
              style={{
                width: `${(c.bugsFixed / max) * 100}%`,
                background: C.purple,
              }}
            />
          </div>
          <span className="text-xs font-bold text-txt w-4 shrink-0">{c.bugsFixed}</span>
        </div>
      ))}
    </div>
  );
}

// ── Main component ─────────────────────────────────────────────────────────
export default function Analytics() {
  const [data,    setData]    = useState(null);
  const [period,  setPeriod]  = useState("30d");
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState("");
  const timerRef = useRef(null);

  const fetchData = useCallback(async (p) => {
    setLoading(true);
    setError("");
    try {
      const res = await authFetch(`${API}/analytics?period=${p}`);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const json = await res.json();
      setData(json);
    } catch (ex) {
      setError(ex.message);
    } finally {
      setLoading(false);
    }
  }, []);

  // Initial load + period change
  useEffect(() => { fetchData(period); }, [period]);

  // Poll every 30 s
  useEffect(() => {
    timerRef.current = setInterval(() => fetchData(period), 30_000);
    return () => clearInterval(timerRef.current);
  }, [period, fetchData]);

  // ── chart data ────────────────────────────────────────────────────────
  const barData = data ? {
    labels:   data.monthlyStats.map(m => m.month),
    datasets: [
      {
        label:           "Calls",
        data:            data.monthlyStats.map(m => m.calls),
        backgroundColor: C.purple,
        borderRadius:    6,
        barPercentage:   0.55,
      },
      {
        label:           "MOMs",
        data:            data.monthlyStats.map(m => m.moms),
        backgroundColor: C.green,
        borderRadius:    6,
        barPercentage:   0.55,
      },
    ],
  } : null;

  const barOpts = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { position: "top" },
      tooltip: { backgroundColor: "#1A1D28", borderColor: C.border, borderWidth: 1 },
    },
    scales: {
      x: { grid: { color: C.border } },
      y: { grid: { color: C.border }, beginAtZero: true, ticks: { stepSize: 5 } },
    },
  };

  const doughnutData = data ? {
    labels:   ["Done", "Running", "Failed"],
    datasets: [{
      data: [
        data.pipelineStatusBreakdown.done,
        data.pipelineStatusBreakdown.running,
        data.pipelineStatusBreakdown.failed,
      ],
      backgroundColor: [C.green, C.purple, C.red],
      borderWidth:     0,
      hoverOffset:     6,
    }],
  } : null;

  const doughnutOpts = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { position: "bottom" },
      tooltip: { backgroundColor: "#1A1D28", borderColor: C.border, borderWidth: 1 },
    },
    cutout: "68%",
  };

  const lineData = data ? {
    labels:   data.weeklyDurations.map((_, i) => `W${i + 1}`),
    datasets: [{
      label:           "Avg Duration (min)",
      data:            data.weeklyDurations,
      borderColor:     C.purple,
      backgroundColor: C.purpleAlpha,
      borderWidth:     2,
      pointRadius:     3,
      pointBackgroundColor: C.purple,
      fill:            true,
      tension:         0.4,
    }],
  } : null;

  const lineOpts = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false },
      tooltip: { backgroundColor: "#1A1D28", borderColor: C.border, borderWidth: 1 },
    },
    scales: {
      x: { grid: { color: C.border } },
      y: { grid: { color: C.border }, beginAtZero: true },
    },
  };

  // ── render ─────────────────────────────────────────────────────────────
  return (
    <div className="space-y-6">

      {/* ── Toolbar ──────────────────────────────────────────── */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div className="flex gap-1 p-1 bg-surface border border-border rounded-xl">
          {PERIODS.map(({ key, label }) => (
            <button
              key={key}
              onClick={() => setPeriod(key)}
              className={cn(
                "px-4 py-2 rounded-lg text-xs font-semibold transition-all",
                period === key
                  ? "bg-accent text-white"
                  : "text-muted hover:text-txt hover:bg-surface2"
              )}
            >
              {label}
            </button>
          ))}
        </div>
        <Button variant="secondary" size="sm" onClick={() => fetchData(period)} disabled={loading}>
          <RefreshCw className={cn("w-3.5 h-3.5", loading && "animate-spin")} />
          Refresh
        </Button>
      </div>

      {error && (
        <div className="bg-red/10 border border-red/20 rounded-xl px-4 py-3 text-sm text-red">
          Failed to load analytics: {error}
        </div>
      )}

      {/* ── Stat cards ───────────────────────────────────────── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        {loading || !data ? (
          [1,2,3,4].map(i => <StatCardSkeleton key={i} />)
        ) : (
          <>
            <AnalyticStatCard
              label="Total Calls"       value={data.totalCalls}
              icon={Phone}              color="purple"
              subValue={`${data.momSuccessRate}% MOM success rate`}
              trend={data.vsLastPeriod}
            />
            <AnalyticStatCard
              label="MOMs Generated"    value={data.momsGenerated}
              icon={FileText}           color="green"
              subValue={`${data.momSuccessRate}% success rate`}
            />
            <AnalyticStatCard
              label="Avg Call Duration" value={`${data.avgCallDurationMinutes}m`}
              icon={Clock}              color="orange"
              subValue="Per Zoom session"
            />
            <AnalyticStatCard
              label="PRs Raised"        value={data.prsRaised}
              icon={GitPullRequest}     color="purple"
              subValue={`${data.prSuccessRate}% success rate`}
            />
          </>
        )}
      </div>

      {/* ── Charts row 1: Monthly + Doughnut ─────────────────── */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">

        {/* Monthly calls vs MOMs — 2/3 width */}
        {loading || !data ? (
          <ChartSkeleton height="h-56" className="lg:col-span-2" />
        ) : (
          <Card className="p-5 lg:col-span-2">
            <p className="text-xs font-semibold text-muted uppercase tracking-wider mb-4">Monthly Calls vs MOMs</p>
            <div className="h-56">
              <BarChart data={barData} options={barOpts} />
            </div>
          </Card>
        )}

        {/* Pipeline status breakdown */}
        {loading || !data ? (
          <ChartSkeleton height="h-56" />
        ) : (
          <Card className="p-5">
            <p className="text-xs font-semibold text-muted uppercase tracking-wider mb-4">Pipeline Status</p>
            <div className="h-48">
              <DoughnutChart data={doughnutData} options={doughnutOpts} />
            </div>
            <div className="grid grid-cols-3 gap-2 mt-4">
              {[
                { label: "Done",    val: data.pipelineStatusBreakdown.done,    color: "text-[#1D9E75]" },
                { label: "Running", val: data.pipelineStatusBreakdown.running, color: "text-[#534AB7]" },
                { label: "Failed",  val: data.pipelineStatusBreakdown.failed,  color: "text-[#E24B4A]" },
              ].map(({ label, val, color }) => (
                <div key={label} className="text-center">
                  <p className={cn("text-lg font-display font-bold", color)}>{val}</p>
                  <p className="text-[10px] text-muted">{label}</p>
                </div>
              ))}
            </div>
          </Card>
        )}
      </div>

      {/* ── Charts row 2: Duration line + Top clients bar ──────── */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">

        {/* Weekly duration trend */}
        {loading || !data ? (
          <ChartSkeleton height="h-48" />
        ) : (
          <Card className="p-5">
            <p className="text-xs font-semibold text-muted uppercase tracking-wider mb-4">Weekly Avg Call Duration (min)</p>
            <div className="h-48">
              <LineChart data={lineData} options={lineOpts} />
            </div>
          </Card>
        )}

        {/* Bugs fixed per client */}
        {loading || !data ? (
          <ChartSkeleton height="h-48" />
        ) : (
          <Card className="p-5">
            <p className="text-xs font-semibold text-muted uppercase tracking-wider mb-5">Bugs Fixed per Client (Top 5)</p>
            {data.topClients.length === 0 ? (
              <p className="text-sm text-muted text-center py-8">No data yet</p>
            ) : (
              <HorizontalBar clients={data.topClients} />
            )}
          </Card>
        )}
      </div>

      {/* ── Recent pipelines table ────────────────────────────── */}
      <Card>
        <div className="px-6 py-4 border-b border-border">
          <p className="font-display font-bold text-txt text-base">Recent Pipelines</p>
          <p className="text-xs text-muted mt-0.5">Latest runs in the selected period</p>
        </div>

        {loading || !data ? (
          <div className="p-6 space-y-3">
            {[1,2,3,4].map(i => <Skeleton key={i} className="h-10 w-full" />)}
          </div>
        ) : !data.recentPipelines?.length ? (
          <div className="text-center py-12 text-muted">
            <p className="text-3xl mb-2">📭</p>
            <p className="text-sm">No pipelines in this period</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border">
                  {["Client", "MOM Summary", "Bug Found", "Duration", "PR", "Status"].map(h => (
                    <th key={h} className="px-5 py-3 text-left text-[11px] font-semibold text-muted uppercase tracking-wider">
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-border/10">
                {data.recentPipelines.map((p) => (
                  <tr key={p.id} className="hover:bg-surface2 transition-colors">
                    <td className="px-5 py-3.5 font-semibold text-txt whitespace-nowrap">{p.clientName}</td>
                    <td className="px-5 py-3.5 text-muted max-w-xs">
                      <p className="truncate">{p.momSummary ?? "—"}</p>
                    </td>
                    <td className="px-5 py-3.5 font-mono text-xs text-muted whitespace-nowrap">
                      {p.bugFound ?? "—"}
                    </td>
                    <td className="px-5 py-3.5 text-muted whitespace-nowrap">
                      {p.durationMin > 0 ? `${p.durationMin}m` : "—"}
                    </td>
                    <td className="px-5 py-3.5">
                      {p.prNumber ? (
                        <a
                          href={p.prUrl ?? "#"}
                          target="_blank"
                          rel="noreferrer"
                          className="inline-flex items-center gap-1 text-xs text-[#1D9E75] hover:underline font-mono"
                        >
                          #{p.prNumber} <ExternalLink className="w-3 h-3" />
                        </a>
                      ) : (
                        <span className="text-muted">—</span>
                      )}
                    </td>
                    <td className="px-5 py-3.5">
                      <Badge label={p.status} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </div>
  );
}

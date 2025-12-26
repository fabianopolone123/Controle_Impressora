const statusEl = document.getElementById("status");
const totalPagesEl = document.getElementById("totalPages");
const totalJobsEl = document.getElementById("totalJobs");
const topUserEl = document.getElementById("topUser");
const topPrinterEl = document.getElementById("topPrinter");
const chartUsersEl = document.getElementById("chartUsers");
const chartPrintersEl = document.getElementById("chartPrinters");
const jobsBodyEl = document.getElementById("jobsBody");
const emptyStateEl = document.getElementById("emptyState");

const fromDateEl = document.getElementById("fromDate");
const toDateEl = document.getElementById("toDate");
const userFilterEl = document.getElementById("userFilter");
const machineFilterEl = document.getElementById("machineFilter");
const printerFilterEl = document.getElementById("printerFilter");
const minPagesEl = document.getElementById("minPages");
const maxPagesEl = document.getElementById("maxPages");

const formatter = new Intl.NumberFormat("pt-BR");

document.getElementById("applyBtn").addEventListener("click", () => loadData());
document.getElementById("resetBtn").addEventListener("click", () => {
  fromDateEl.value = "";
  toDateEl.value = "";
  userFilterEl.value = "";
  machineFilterEl.value = "";
  printerFilterEl.value = "";
  minPagesEl.value = "";
  maxPagesEl.value = "";
  loadData();
});

document.getElementById("exportBtn").addEventListener("click", () => {
  const query = buildQuery();
  window.location.href = `/api/print-jobs/export?${query}`;
});

function buildQuery() {
  const params = new URLSearchParams();

  if (fromDateEl.value) {
    params.set("from", `${fromDateEl.value}T00:00:00`);
  }
  if (toDateEl.value) {
    params.set("to", `${toDateEl.value}T23:59:59`);
  }
  if (userFilterEl.value.trim()) {
    params.set("user", userFilterEl.value.trim());
  }
  if (machineFilterEl.value.trim()) {
    params.set("machine", machineFilterEl.value.trim());
  }
  if (printerFilterEl.value.trim()) {
    params.set("printer", printerFilterEl.value.trim());
  }
  if (minPagesEl.value) {
    params.set("minPages", minPagesEl.value);
  }
  if (maxPagesEl.value) {
    params.set("maxPages", maxPagesEl.value);
  }

  params.set("limit", "500");
  return params.toString();
}

async function fetchJson(url) {
  const response = await fetch(url, { cache: "no-store" });
  if (!response.ok) {
    throw new Error(`HTTP ${response.status}`);
  }
  return response.json();
}

function setStatus(text, ok) {
  statusEl.textContent = text;
  statusEl.style.background = ok ? "#e7f3ef" : "#f8e4e4";
  statusEl.style.color = ok ? "#0b5f5a" : "#8c2b2b";
}

function formatBytes(bytes) {
  if (!bytes) return "0";
  const units = ["B", "KB", "MB", "GB"];
  let index = 0;
  let value = bytes;
  while (value >= 1024 && index < units.length - 1) {
    value /= 1024;
    index++;
  }
  return `${value.toFixed(1)} ${units[index]}`;
}

function formatDate(value) {
  if (!value) return "-";
  const date = new Date(value);
  return date.toLocaleString("pt-BR");
}

function renderSummary(summary, users, printers) {
  totalPagesEl.textContent = formatter.format(summary.totalPages || 0);
  totalJobsEl.textContent = formatter.format(summary.totalJobs || 0);
  topUserEl.textContent = users.length ? `${users[0].key} (${formatter.format(users[0].totalPages)})` : "-";
  topPrinterEl.textContent = printers.length ? `${printers[0].key} (${formatter.format(printers[0].totalPages)})` : "-";
}

function renderBars(container, items) {
  container.innerHTML = "";
  if (!items.length) {
    container.innerHTML = "<div class=\"empty\">Sem dados.</div>";
    return;
  }

  const max = Math.max(...items.map((item) => item.totalPages || 0), 1);
  items.forEach((item) => {
    const row = document.createElement("div");
    row.className = "chart-row";

    const label = document.createElement("div");
    label.textContent = item.key || "-";

    const bar = document.createElement("div");
    bar.className = "chart-bar";
    const fill = document.createElement("span");
    fill.style.width = `${(item.totalPages / max) * 100}%`;
    bar.appendChild(fill);

    const value = document.createElement("div");
    value.textContent = formatter.format(item.totalPages || 0);

    row.append(label, bar, value);
    container.appendChild(row);
  });
}

function renderTable(rows) {
  jobsBodyEl.innerHTML = "";
  if (!rows.length) {
    emptyStateEl.style.display = "block";
    return;
  }

  emptyStateEl.style.display = "none";
  rows.forEach((row) => {
    const tr = document.createElement("tr");
    tr.innerHTML = `
      <td>${formatDate(row.printedAt)}</td>
      <td>${row.userName}</td>
      <td>${row.machineName}</td>
      <td>${row.printerName}</td>
      <td>${formatter.format(row.pages || 0)}</td>
      <td>${formatBytes(row.bytes || 0)}</td>
    `;
    jobsBodyEl.appendChild(tr);
  });
}

async function loadData() {
  setStatus("Carregando...", true);
  const query = buildQuery();

  try {
    const [jobs, summary, users, printers] = await Promise.all([
      fetchJson(`/api/print-jobs?${query}`),
      fetchJson(`/api/stats/summary?${query}`),
      fetchJson(`/api/stats/users?${query}`),
      fetchJson(`/api/stats/printers?${query}`)
    ]);

    renderSummary(summary, users, printers);
    renderBars(chartUsersEl, users);
    renderBars(chartPrintersEl, printers);
    renderTable(jobs);

    setStatus("Conectado", true);
  } catch (error) {
    console.error(error);
    setStatus("Erro de conexao", false);
  }
}

loadData();
